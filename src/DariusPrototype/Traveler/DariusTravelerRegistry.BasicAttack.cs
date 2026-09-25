using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;

public static partial class DariusTravelerRegistry
{
    private static void ConfigureCharacterSkill(SkillTrigger skill, Rarity rarity, string owner, string location, bool excludeFromPool)
    {
        if (skill == null) return;
        skill.rarity = rarity;
        skill.characterSkillOwner = owner ?? string.Empty;
        skill.excludeFromPool = excludeFromPool;
        SetEnumLikeMember(skill, "skillType", location);
        DariusLog.DebugInfoThrottled("TRAVELER-SKILL", skill.name, skill.name + " rarity=" + rarity + " owner=" + (owner ?? "") + " slot=" + location + " exclude=" + excludeFromPool, 20.0);
    }

    private static void CreateAndRegisterNativeBasicAttack()
    {
        // v0.18.2: use the stock melee prefabs strictly as construction-time structural templates.
        // This preserves Actor/Mirror initialization, targeting/cadence and any generic helper
        // components which Dew expects on a native attack prefab, but the concrete Vesper attack
        // components and every presentation component are removed BEFORE registration. Runtime
        // Hero_Darius therefore spawns only At_DariusAxe/Ai_DariusAxe resources.
        Type sourceAttackType = AccessTools.TypeByName("At_Atk_VesperMace");
        AttackTrigger sourceAttack = sourceAttackType != null
            ? DewResources.GetByType<AttackTrigger>(sourceAttackType)
            : null;
        if (sourceAttack == null)
            throw new InvalidOperationException("Could not resolve stock melee AttackTrigger structural template.");

        MeleeAttackInstance sourceNormal = null;
        MeleeAttackInstance sourceCrit = null;
        if (sourceAttack.configs != null)
        {
            for (int i = 0; i < sourceAttack.configs.Length; i++)
            {
                TriggerConfig sourceCfg = sourceAttack.configs[i];
                MeleeAttackInstance inst = sourceCfg != null ? sourceCfg.spawnedInstance as MeleeAttackInstance : null;
                if (inst == null) continue;
                if (inst.GetType().Name.IndexOf("Crit", StringComparison.OrdinalIgnoreCase) >= 0) sourceCrit = inst;
                else if (sourceNormal == null) sourceNormal = inst;
            }
        }
        if (sourceNormal == null)
        {
            Type normalType = AccessTools.TypeByName("Ai_Atk_VesperMace");
            if (normalType != null) sourceNormal = DewResources.GetByType<MeleeAttackInstance>(normalType);
        }
        if (sourceCrit == null)
        {
            Type critType = AccessTools.TypeByName("Ai_Atk_VesperMace_Crit");
            if (critType != null) sourceCrit = DewResources.GetByType<MeleeAttackInstance>(critType);
        }
        if (sourceNormal == null) throw new InvalidOperationException("Could not resolve stock melee AttackInstance structural template.");
        if (sourceCrit == null) sourceCrit = sourceNormal;

        AttackInstancePrefab = CreateDariusAttackInstance<Ai_DariusAxe>(sourceNormal, AttackInstanceName, AttackInstanceGuid, AttackInstanceAssetId);
        AttackCritInstancePrefab = CreateDariusAttackInstance<Ai_DariusAxe_Crit>(sourceCrit, AttackCritInstanceName, AttackCritInstanceGuid, AttackCritInstanceAssetId);

        GameObject go = CloneTemplateInactive(sourceAttack.gameObject, AttackName);
        go.name = AttackName;
        go.hideFlags = HideFlags.HideAndDontSave;
        // Keep the construction prefab inactiveInHierarchy through the inactive resource root, but
        // leave activeSelf=true. Unity copies activeSelf to spawned clones; this guarantees the live
        // At_DariusAxe clone runs Awake before Dew assigns parentActor, while the bootstrap template
        // itself still cannot fire DewCollider.OnEnable before DewPhysics is ready.
        go.transform.SetParent(_resourceRoot.transform, false);
        go.SetActive(true);

        AttackTrigger oldAttack = go.GetComponent<AttackTrigger>();
        if (oldAttack == null)
        {
            UnityEngine.Object.Destroy(go);
            throw new InvalidOperationException("Cloned stock melee attack template has no AttackTrigger.");
        }
        Dictionary<FieldInfo, object> attackFields = CaptureUnitySerializedFields(oldAttack, typeof(AttackTrigger));
        TriggerConfig[] sourceConfigs = oldAttack.configs;

        // IMPORTANT: rebind references while oldAttack is still a live Unity Object. DestroyImmediate
        // makes Unity's overloaded == null return true; the old v0.18 code destroyed first, so the
        // rebinder exited immediately and helper components kept MissingReference links to Vesper's
        // AttackTrigger. That is why the native preset existed but could never actually attack.
        At_DariusAxe attack = go.AddComponent<At_DariusAxe>();
        RestoreUnitySerializedFields(attack, attackFields);
        ReplaceDirectComponentReferences(go, oldAttack, attack);
        int attackResidualRefs = CountDirectComponentReferences(go, oldAttack, attack);
        if (attackResidualRefs > 0)
            DariusLog.Warn("ATK-NATIVE-PREFAB", "Structural attack template still has " + attackResidualRefs + " direct references to the old component before destruction.");
        UnityEngine.Object.DestroyImmediate(oldAttack);
        attack.name = AttackName;
        attack.allowNonTargetedCast = true;
        attack.ignoreRangeCheck = false;

        if (sourceConfigs == null || sourceConfigs.Length == 0)
        {
            UnityEngine.Object.Destroy(go);
            throw new InvalidOperationException("Stock melee AttackTrigger structural template has no configs.");
        }

        TriggerConfig[] configs = new TriggerConfig[sourceConfigs.Length];
        for (int i = 0; i < sourceConfigs.Length; i++)
        {
            TriggerConfig sourceCfg = sourceConfigs[i];
            TriggerConfig cfg = CloneTriggerConfig(sourceCfg);
            // TriggerConfig cloning is shallow. CastMethodData is mutable, so editing a copied
            // Vesper range without cloning this object mutates the stock attack and other mods too.
            cfg.castMethod = CloneCastMethodData(sourceCfg != null ? sourceCfg.castMethod : null);
            bool crit = sourceCfg != null && sourceCfg.spawnedInstance != null &&
                        sourceCfg.spawnedInstance.GetType().Name.IndexOf("Crit", StringComparison.OrdinalIgnoreCase) >= 0;
            cfg.spawnedInstance = crit ? (AbilityInstance)AttackCritInstancePrefab : AttackInstancePrefab;
            cfg.startAnim = null;
            cfg.endAnim = null;
            cfg.castVoice = null;
            cfg.effectOnCast = null;
            cfg.appliedStatusEffect = null;
            if (cfg.castMethod != null)
            {
                // Match the stock melee input contract: Target acquisition is native, while
                // allowNonTargetedCast keeps empty-space swings legal. Darius owns only the final
                // forward-sector hit filter.
                cfg.castMethod.type = CastMethodType.Target;
                cfg.castMethod._range = At_DariusAxe.AttackRange;
                cfg.castMethod._radius = 0f;
                cfg.castMethod._angle = 0f;
                cfg.castMethod._isClamping = false;
                cfg.faceForward = true;
            }
            configs[i] = cfg;

            CastMethodData method = cfg.castMethod;
            DariusLog.Info("ATK-NATIVE-CONFIG", "index=" + i +
                " crit=" + crit +
                " method=" + (method != null ? method.type.ToString() : "<null>") +
                " range=" + (method != null ? method._range.ToString("0.###") : "<null>") +
                " radius=" + (method != null ? method._radius.ToString("0.###") : "<null>") +
                " angle=" + (method != null ? method._angle.ToString("0.###") : "<null>") +
                " spawned=" + (cfg.spawnedInstance != null ? cfg.spawnedInstance.name : "<null>"));
        }
        attack.configs = configs;
        StripConstructionTemplatePresentation(go, attack);
        DariusTriggerConfigRuntimeEditor.AttachAll(attack);

        NetworkIdentity identity = go.GetComponent<NetworkIdentity>();
        if (identity == null) identity = go.AddComponent<NetworkIdentity>();
        DariusUnsupportedResourceBridge.ConfigureTemplateIdentity(identity, AttackAssetId, AttackName);
        // activeSelf=true is intentional, but the inactive resource root keeps the construction
        // prefab inactiveInHierarchy. Mirror can build its cache without firing DewCollider.OnEnable,
        // while a live clone inherits activeSelf=true and initializes before Dew assigns parentActor.
        DariusUnsupportedResourceBridge.RebuildNetworkBehaviours(identity, AttackName);

        go.hideFlags = HideFlags.None;
        AttackPrefab = attack;
        OwnedObjects.Add(go);
        RegisterTypedResource(attack, go, AttackName, AttackGuid, AttackAssetId);
        DariusLog.Info("ATK-NATIVE-PREFAB", "Registered At_DariusAxe from a native structural template after replacing the concrete stock attack component. configs=" + configs.Length +
            " normal=" + AttackInstanceName + " crit=" + AttackCritInstanceName + " method=Target allowNonTargeted=" + attack.allowNonTargetedCast +
            " activeSelf=" + go.activeSelf + " activeInHierarchy=" + go.activeInHierarchy +
            " runtimeVesperComponents=" + CountVesperNamedComponents(go));
    }
}