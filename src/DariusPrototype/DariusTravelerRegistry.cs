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

public static class DariusTravelerRegistry
{
    public const string HeroName = "Hero_Darius";
    public const string DefaultSkinName = "Skin_Darius_Default";
    public const string GodKingSkinName = "Skin_Darius_GodKing";
    public const string DunkmasterSkinName = "Skin_Darius_Dunkmaster";
    public const string MechaSkinName = "Skin_Darius_Mecha";
    // v0.13-v0.15 used this Vesper-hosted skin name and it can remain in profile-selected-skin
    // data across upgrades. Treat it as a compatibility alias instead of letting GetByName fail.
    public const string LegacySkinAlias = "Skin_Vesper_Darius";
    public const string HeroGuid = "com.openai.sod.darius.hero.v1";
    public const string SkinGuid = "com.openai.sod.darius.skin.default.v1";
    public const string GodKingSkinGuid = "com.openai.sod.darius.skin.godking.v1";
    public const string DunkmasterSkinGuid = "com.openai.sod.darius.skin.dunkmaster.v1";
    public const string MechaSkinGuid = "com.openai.sod.darius.skin.mecha.v1";
    public const uint HeroAssetId = 2804836319u;

    private sealed class DariusSkinSpec
    {
        public string name, guid, modelFile, displayName, variantKey, previewIconKey;
        public float modelScale, modelYOffset, modelYaw;
        public int expectedPrimitives, expectedBones, expectedAnimations;
        public bool godKing;
        public string idle, idleVariant, run, death, attack1, attack2, crit, qIntro, q, w, e, r;
        public string attack1ToIdle, attack2ToIdle, critToIdle, eToRun, eToIdle, rToRun;
    }

    // Every skin has an explicit profile derived from its own GLB. Never infer animation names,
    // bone counts, primitive counts, or scale from another skin.
    private static readonly DariusSkinSpec[] SkinSpecs = new DariusSkinSpec[]
    {
        new DariusSkinSpec { name=DefaultSkinName, guid=SkinGuid, modelFile="darius.glb", displayName="经典德莱厄斯", variantKey="Classic", previewIconKey="SKIN_CLASSIC", modelScale=0.00921f, modelYOffset=0.04f, modelYaw=0f, expectedPrimitives=1, expectedBones=89, expectedAnimations=22,
            idle="Idle1", idleVariant="Idle2", run="Run", death="Death", attack1="Attack1", attack2="Attack2", crit="Crit", qIntro="Darius_Spell1_IN.anm", q="Spell1", w="Spell2", e="Spell3", r="Spell4" },
        new DariusSkinSpec { name=GodKingSkinName, guid=GodKingSkinGuid, modelFile="darius_godking.glb", displayName="神王德莱厄斯", variantKey="GodKing", previewIconKey="SKIN_GODKING", godKing=true, modelScale=0.00921f, modelYOffset=0.04f, modelYaw=180f, expectedPrimitives=5, expectedBones=179, expectedAnimations=48,
            idle="Idle1_Base", idleVariant="Idle2_Base", run="Run_Normal", death="Death", attack1="Attack1", attack2="Attack2", crit="Crit", qIntro="Darius_Skin15_Spell1_IN.anm", q="Spell1", w="Spell2", e="Spell3", r="Spell4",
            attack1ToIdle="Attack1_ToIdle", attack2ToIdle="Attack2_ToIdle", critToIdle="Crit_ToIdle", eToRun="Spell3_ToRun", eToIdle="Spell3_ToIdle", rToRun="Spell4_ToRun" },
        new DariusSkinSpec { name=DunkmasterSkinName, guid=DunkmasterSkinGuid, modelFile="darius_dunkmaster.glb", displayName="灌篮高手 德莱厄斯", variantKey="Dunkmaster", previewIconKey="SKIN_DUNKMASTER", modelScale=0.00921f, modelYOffset=0.04f, modelYaw=0f, expectedPrimitives=1, expectedBones=99, expectedAnimations=36,
            idle="Idle1_Base", idleVariant="Idle2_Base", run="Darius_Skin04_Run.anm", death="Death", attack1="Attack1", attack2="Attack2", crit="Crit", qIntro="Darius_Skin04_Spell1_IN.anm", q="Spell1", w="Spell2", e="Spell3", r="Darius_Skin04_Spell4_A.anm" },
        new DariusSkinSpec { name=MechaSkinName, guid=MechaSkinGuid, modelFile="darius_mecha.glb", displayName="机神 德莱厄斯", variantKey="Mecha", previewIconKey="SKIN_MECHA", modelScale=0.00921f, modelYOffset=0.04f, modelYaw=180f, expectedPrimitives=13, expectedBones=246, expectedAnimations=59,
            idle="Idle1_Base", idleVariant="Idle2_legacy.SKINS_Darius_Skin67.anm", run="Run_Normal", death="Death", attack1="Attack1", attack2="Attack2", crit="Crit", qIntro="Spell1_IN_Stand.SKINS_Darius_Skin67.anm", q="Spell1", w="Spell2", e="Spell3", r="Spell4",
            attack1ToIdle="Attack1_ToIdle", attack2ToIdle="Attack2_ToIdle", critToIdle="Crit_ToIdle", eToRun="Spell3_ToRun", eToIdle="Spell3_ToIdle", rToRun="Spell4_ToRun" }
    };
    private static readonly Dictionary<string, Skin> SkinsByName = new Dictionary<string, Skin>(StringComparer.Ordinal);

    public const string AttackName = "At_DariusAxe";
    public const string AttackGuid = "com.openai.sod.darius.attack.axe.v1";
    public const uint AttackAssetId = 2804836320u;
    public const string AttackInstanceName = "Ai_DariusAxe";
    public const string AttackInstanceGuid = "com.openai.sod.darius.attack.axe.instance.v1";
    public const uint AttackInstanceAssetId = 2804836321u;
    public const string AttackCritInstanceName = "Ai_DariusAxe_Crit";
    public const string AttackCritInstanceGuid = "com.openai.sod.darius.attack.axe.crit.v1";
    public const uint AttackCritInstanceAssetId = 2804836322u;

    private static readonly Dictionary<string, UnityEngine.Object> ResourcesByGuid = new Dictionary<string, UnityEngine.Object>(StringComparer.Ordinal);
    private static readonly Dictionary<Type, UnityEngine.Object> ResourcesByType = new Dictionary<Type, UnityEngine.Object>();
    private static readonly Dictionary<uint, GameObject> NetworkPrefabs = new Dictionary<uint, GameObject>();
    private static readonly List<GameObject> OwnedObjects = new List<GameObject>();

    private static GameObject _resourceRoot;
    private static bool _registered;
    private static bool _registering;
    private static bool _repairing;
    private static bool _presentationGenerationRebuilt;

    public static Hero_Darius HeroPrefab { get; private set; }
    public static Skin DefaultSkin { get; private set; }
    public static Skin GodKingSkin { get; private set; }
    public static Skin DunkmasterSkin { get; private set; }
    public static Skin MechaSkin { get; private set; }
    public static At_DariusAxe AttackPrefab { get; private set; }
    public static Ai_DariusAxe AttackInstancePrefab { get; private set; }
    public static Ai_DariusAxe_Crit AttackCritInstancePrefab { get; private set; }

    private static DariusSkinSpec FindSkinSpec(string key)
    {
        if (string.IsNullOrEmpty(key)) return null;
        for (int i = 0; i < SkinSpecs.Length; i++)
            if (string.Equals(SkinSpecs[i].name, key, StringComparison.Ordinal) || string.Equals(SkinSpecs[i].guid, key, StringComparison.Ordinal)) return SkinSpecs[i];
        return null;
    }

    public static bool IsRuntimeSkinKey(string key)
    {
        if (string.IsNullOrEmpty(key)) return false;
        return FindSkinSpec(key) != null;
    }

    public static bool TryResolveRuntimeSkin(string key, out Skin skin)
    {
        skin = null;
        if (string.IsNullOrEmpty(key)) return false;
        DariusSkinSpec spec = FindSkinSpec(key);
        if (spec == null) return false;
        return SkinsByName.TryGetValue(spec.name, out skin) && skin != null;
    }

    private static bool AreSkinResourcesReady()
    {
        if (SkinSpecs == null || SkinSpecs.Length == 0) return false;
        for (int i = 0; i < SkinSpecs.Length; i++)
        {
            Skin skin;
            if (!SkinsByName.TryGetValue(SkinSpecs[i].name, out skin) || skin == null || skin.gameObject == null) return false;
            // A run teardown can leave the managed Skin wrapper alive while destroying the Unity
            // presentation graph that CharacterModelDisplay later instantiates. Treat a partially
            // alive Skin as stale instead of trusting only Unity's wrapper-null test.
            if (skin.GetComponent<EntityModel>() == null || skin.GetComponent<DariusSkinModelBinding>() == null ||
                skin.GetComponent<DariusTravelerModelInstance>() == null) return false;
        }
        return true;
    }

    public static IEnumerator InitializeWhenReady()
    {
        // Workshop mods can be instantiated before DewBuildProfile/profile services are ready, while
        // the lobby/reward UI may already contain persisted Hero_Darius references from the prior
        // session. Waiting for *all* profile services here creates a lookup race. Only the resource
        // database is required to create the runtime Hero/Skin/type bridges, so register that core
        // as soon as possible and finish profile/content integration later.
        DariusLog.Info("TRAVELER", "Waiting only for DewResources.database before core Hero_Darius registration.");
        while (DewResources.database == null)
            yield return null;

        EnsureCoreRegisteredForLookup("InitializeWhenReady core phase");

        // Profile/content may come online a few frames later on Workshop boot. Once ready, perform
        // the same native unlock/loadout validation pass used by local installs.
        while (DewBuildProfile.current == null || DewBuildProfile.current.content == null ||
               DewSave.profileMain == null || DewSave.profileStats == null)
            yield return null;

        CompleteProfileRegistration("InitializeWhenReady profile/content phase");
    }

    public static bool EnsureCoreRegisteredForLookup(string reason)
    {
        if (_registered && HeroPrefab != null && AreSkinResourcesReady()) return true;
        // Registration itself can invoke Dew/UI callbacks. Never recursively enter Register(); once
        // Skin+Hero have been created those callbacks can safely consume them, otherwise they defer.
        if (_registering) return HeroPrefab != null && DefaultSkin != null;
        if (DewResources.database == null) return false;

        try
        {
            // Skills must exist before the HeroSkill AssetRef arrays are built.
            DariusFormalRegistry.Register();
            if (!_registered) Register();
            else RepairRuntimeRegistration(reason ?? "core lookup self-heal");
            bool ok = HeroPrefab != null && AreSkinResourcesReady();
            if (ok)
                DariusLog.DebugInfoThrottled("TRAVELER-EARLY", reason ?? "lookup", "Core Hero_Darius/Skin_Darius_Default resources available before profile completion.", 2.0);
            return ok;
        }
        catch (Exception e)
        {
            DariusLog.Exception("TRAVELER-EARLY", e, "Core registration failed reason=" + (reason ?? "<unknown>"));
            return false;
        }
    }

    private static void CompleteProfileRegistration(string reason)
    {
        if (!_registered || HeroPrefab == null || !AreSkinResourcesReady())
        {
            if (!EnsureCoreRegisteredForLookup(reason + " core prerequisite")) return;
        }

        try
        {
            RegisterTypes();
            RegisterContent(DewBuildProfile.current != null ? DewBuildProfile.current.content : null);
            EnsureProfiles();
            RepairRuntimeRegistration(reason);
            NotifyDewMissingReferenceRepair();
            DariusLog.Info("TRAVELER", "Late profile/content registration completed reason=" + reason);
        }
        catch (Exception e)
        {
            DariusLog.Exception("TRAVELER", e, "Late profile/content registration failed reason=" + reason);
        }
    }

    private static void NotifyDewMissingReferenceRepair()
    {
        try
        {
            // The public DewResources repair pair is the authoritative way to revisit already-created
            // AssetRef/UI references after a runtime resource becomes available. Merely invoking
            // onRepairMissingReferences is not equivalent to running the repair transaction.
            DewResources.RepairMissingReferences_Prepare();
            DewResources.RepairMissingReferences_Repair();
            // RepairMissingReferences can replace/light-load resource references. Reassert our
            // runtime Hero.icon/mainColor and every Skin.previewImage only after that transaction.
            RepairHeroCosmeticContract(HeroPrefab);
            DariusLog.Info("TRAVELER-EARLY", "Executed DewResources missing-reference Prepare->Repair and rebound Hero/Skin presentation fields after Workshop profile completion.");
        }
        catch (Exception e)
        {
            DariusLog.Exception("TRAVELER-EARLY", e, "DewResources missing-reference Prepare/Repair failed");
        }
    }

    public static void Register()
    {
        if (_registered || _registering) return;
        if (DewResources.database == null) throw new InvalidOperationException("DewResources.database is null.");
        if (DariusFormalRegistry.Decimate == null || DariusFormalRegistry.NoxianGuillotine == null || DariusFormalRegistry.Hemorrhage == null)
            throw new InvalidOperationException("Darius skill resources must be registered before Hero_Darius.");

        _registering = true;
        try
        {
            CreateResourceRoot();
            ConfigureDariusSkillOwnership();
            CreateAndRegisterNativeBasicAttack();
            CreateAndRegisterSkin();
            CreateAndRegisterHero();
            RegisterTypes();
            RegisterContent(DewBuildProfile.current != null ? DewBuildProfile.current.content : null);
            EnsureProfiles();
            ValidateRegistration();
            _registered = true;
            CreateLifecycleBridge();
            RepairRuntimeRegistration("initial post-register finalization");

            DariusLog.Info("TRAVELER", "Hero_Darius registration READY. heroGuid=" + HeroGuid + " heroAssetId=" + HeroAssetId +
                " skins=" + string.Join(",", Array.ConvertAll(SkinSpecs, s => s.name)) + " Q=" + DariusFormalRegistry.Decimate.name + " R=" + DariusFormalRegistry.NoxianGuillotine.name +
                " Identity=" + DariusFormalRegistry.Hemorrhage.name);
        }
        catch
        {
            // Awake may race the final initialization of stock native templates on Workshop boot.
            // Never keep a half-created runtime graph; a clean Start/coroutine retry is safer.
            try { UnregisterRuntimeOnly(); } catch { }
            throw;
        }
        finally
        {
            _registering = false;
        }
    }

    private static void CreateResourceRoot()
    {
        if (_resourceRoot != null) return;
        _resourceRoot = new GameObject("DariusTraveler_RuntimeResources");
        _resourceRoot.hideFlags = HideFlags.HideAndDontSave;
        _resourceRoot.SetActive(false);
        UnityEngine.Object.DontDestroyOnLoad(_resourceRoot);
    }

    private static void CreateLifecycleBridge()
    {
        DariusTravelerLifecycleBridge existing = UnityEngine.Object.FindFirstObjectByType<DariusTravelerLifecycleBridge>();
        if (existing != null) return;
        GameObject go = new GameObject("DariusTraveler_PersistentLifecycle");
        go.hideFlags = HideFlags.HideAndDontSave;
        go.AddComponent<DariusTravelerLifecycleBridge>();
        UnityEngine.Object.DontDestroyOnLoad(go);
        OwnedObjects.Add(go);
        DariusLog.Info("TRAVELER-LIFECYCLE", "Persistent scene lifecycle bridge created. Runtime Hero/Skin/Profile maps will be reasserted around every scene transition.");
    }

    private static void ConfigureDariusSkillOwnership()
    {
        ConfigureCharacterSkill(DariusFormalRegistry.Decimate, Rarity.Character, HeroName, "Q", true);
        ConfigureCharacterSkill(DariusFormalRegistry.NoxianGuillotine, Rarity.Character, HeroName, "R", true);
        ConfigureCharacterSkill(DariusFormalRegistry.Hemorrhage, Rarity.Identity, HeroName, "Identity", true);
        ConfigureCharacterSkill(DariusFormalRegistry.Flash, Rarity.Character, HeroName, "Movement", true);
        ConfigureCharacterSkill(DariusFormalRegistry.Ghost, Rarity.Character, HeroName, "Movement", true);

        // W/E remain ordinary Memories and are not welded to Hero_Darius.
        ConfigureCharacterSkill(DariusFormalRegistry.CripplingStrike, Rarity.Common, string.Empty, "W", false);
        ConfigureCharacterSkill(DariusFormalRegistry.Apprehend, Rarity.Common, string.Empty, "E", false);
    }

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
        AttackTrigger sourceAttack = ResolveResourceByTypeName<AttackTrigger>("At_Atk_VesperMace");
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
        if (sourceNormal == null) sourceNormal = ResolveResourceByTypeName<MeleeAttackInstance>("Ai_Atk_VesperMace");
        if (sourceCrit == null) sourceCrit = ResolveResourceByTypeName<MeleeAttackInstance>("Ai_Atk_VesperMace_Crit");
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
                // Directional melee intent: Cone accepts an aim direction and does not require a
                // target, so air attacks are legal. The spawned MeleeAttackInstance remains the
                // native cadence/crit/on-hit carrier; final geometry is the same sector.
                cfg.castMethod.type = CastMethodType.Cone;
                cfg.castMethod._range = At_DariusAxe.AttackRange;
                cfg.castMethod._radius = At_DariusAxe.AttackRange;
                cfg.castMethod._angle = At_DariusAxe.AttackArcDegrees;
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
        ConfigureNetworkIdentity(identity, AttackAssetId);
        // activeSelf=true is intentional, but the inactive resource root keeps the construction
        // prefab inactiveInHierarchy. Mirror can build its cache without firing DewCollider.OnEnable,
        // while a live clone inherits activeSelf=true and initializes before Dew assigns parentActor.
        ReinitializeNetworkBehaviours(identity);

        go.hideFlags = HideFlags.None;
        AttackPrefab = attack;
        OwnedObjects.Add(go);
        RegisterTypedResource(attack, go, AttackName, AttackGuid, AttackAssetId);
        DariusLog.Info("ATK-NATIVE-PREFAB", "Registered At_DariusAxe from a native structural template after replacing the concrete stock attack component. configs=" + configs.Length +
            " normal=" + AttackInstanceName + " crit=" + AttackCritInstanceName + " activeSelf=" + go.activeSelf +
            " activeInHierarchy=" + go.activeInHierarchy + " runtimeVesperComponents=" + CountVesperNamedComponents(go));
    }

    private static T CreateDariusAttackInstance<T>(MeleeAttackInstance source, string name, string guid, uint assetId) where T : MeleeAttackInstance
    {
        GameObject go = CloneTemplateInactive(source.gameObject, name);
        go.name = name;
        go.hideFlags = HideFlags.HideAndDontSave;
        // Same lifecycle contract as Hero_Darius: the resource root keeps this object
        // inactiveInHierarchy, while activeSelf=true is preserved so a runtime Instantiate() is
        // born active and initializes Actor/MeleeAttackInstance before parentActor is assigned.
        go.transform.SetParent(_resourceRoot.transform, false);
        go.SetActive(true);

        MeleeAttackInstance oldInstance = go.GetComponent<MeleeAttackInstance>();
        if (oldInstance == null)
        {
            UnityEngine.Object.Destroy(go);
            throw new InvalidOperationException("Cloned stock melee instance template has no MeleeAttackInstance: " + name);
        }
        Dictionary<FieldInfo, object> fields = CaptureUnitySerializedFields(oldInstance, typeof(MeleeAttackInstance));
        T instance = go.AddComponent<T>();
        RestoreUnitySerializedFields(instance, fields);
        ReplaceDirectComponentReferences(go, oldInstance, instance);
        int instanceResidualRefs = CountDirectComponentReferences(go, oldInstance, instance);
        if (instanceResidualRefs > 0)
            DariusLog.Warn("ATK-NATIVE-PREFAB", name + " structural template still has " + instanceResidualRefs + " direct references to the old component before destruction.");
        UnityEngine.Object.DestroyImmediate(oldInstance);

        // Gameplay fields are retained; all character-specific presentation is explicitly severed.
        instance.fxHitMain = null;
        instance.fxHitSub = null;
        instance.startEffectNoStop = null;
        instance.startEffect = null;
        instance.endEffect = null;
        instance.name = name;
        StripConstructionTemplatePresentation(go, instance);
        ConfigureDariusDirectionalAttackHitVolume(go, name);
        // Normalize the inherited overlap volume, then let MeleeAttackInstance scale it from the
        // live TriggerConfig.effectiveRange. A separate center-distance guard prevents stale/shared
        // target-range data from producing remote melee hits.
        TryAssignIfCompatible(instance, "scaleRangeWithTriggerRange", false);

        NetworkIdentity identity = go.GetComponent<NetworkIdentity>();
        if (identity == null) identity = go.AddComponent<NetworkIdentity>();
        ConfigureNetworkIdentity(identity, assetId);
        // The bootstrap object is still inactiveInHierarchy because its parent resource root is
        // inactive. Its activeSelf=true is preserved specifically for the spawned clone lifecycle.
        ReinitializeNetworkBehaviours(identity);

        go.hideFlags = HideFlags.None;
        OwnedObjects.Add(go);
        RegisterTypedResource(instance, go, name, guid, assetId);
        DariusLog.Info("ATK-NATIVE-PREFAB", "Registered " + name + " from native MeleeAttackInstance structure with Darius concrete component; activeSelf=" + go.activeSelf +
            " activeInHierarchy=" + go.activeInHierarchy + " runtimeVesperComponents=" + CountVesperNamedComponents(go));
        return instance;
    }

    private static void ConfigureDariusDirectionalAttackHitVolume(GameObject root, string label)
    {
        if (root == null) return;
        // Broad phase is a caster-centred circle. scaleRangeWithTriggerRange is disabled so no
        // inherited Vesper multiplier can silently expand this volume. DoBasicAttackHit is then
        // narrowed to the exact forward sector by the directional geometry patch.
        float radius = At_DariusAxe.AttackRange;
        float diameter = radius * 2f;
        int sphere = 0, capsule = 0, box = 0, dewFields = 0;
        try
        {
            Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                SphereCollider sc = colliders[i] as SphereCollider;
                if (sc != null) { sc.radius = radius; sphere++; continue; }
                CapsuleCollider cc = colliders[i] as CapsuleCollider;
                if (cc != null) { cc.radius = radius; cc.height = Mathf.Max(diameter, cc.height); capsule++; continue; }
                BoxCollider bc = colliders[i] as BoxCollider;
                if (bc != null)
                {
                    Vector3 size = bc.size; size.x = diameter; size.z = diameter; bc.size = size; box++; continue;
                }
            }

            Component[] components = root.GetComponentsInChildren<Component>(true);
            for (int i = 0; i < components.Length; i++)
            {
                Component component = components[i];
                if (component == null || component.GetType().Name.IndexOf("DewCollider", StringComparison.OrdinalIgnoreCase) < 0) continue;
                for (Type t = component.GetType(); t != null && t != typeof(Component); t = t.BaseType)
                {
                    FieldInfo[] fields = t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                    for (int f = 0; f < fields.Length; f++)
                    {
                        FieldInfo field = fields[f];
                        if (field.IsStatic || field.IsInitOnly) continue;
                        string n = field.Name.ToLowerInvariant();
                        try
                        {
                            if (field.FieldType == typeof(float) && (n.Contains("radius") || n.Contains("range")))
                            {
                                float old = (float)field.GetValue(component);
                                if (old > 0.001f) { field.SetValue(component, radius); dewFields++; }
                            }
                            else if (field.FieldType == typeof(Vector3) && n.Contains("size"))
                            {
                                Vector3 value = (Vector3)field.GetValue(component);
                                value.x = diameter; value.z = diameter; field.SetValue(component, value); dewFields++;
                            }
                        }
                        catch { }
                    }
                }
            }
            DariusLog.Info("ATK-HITBOX", "Directional broad-phase " + label + " radius=" + radius.ToString("0.###") +
                " scaleWithTriggerRange=false sphere=" + sphere + " capsule=" + capsule + " box=" + box + " dewFields=" + dewFields);
        }
        catch (Exception e)
        {
            DariusLog.Exception("ATK-HITBOX", e, "Failed configuring directional broad-phase for " + label);
        }
    }

    private static GameObject CloneTemplateInactive(GameObject source, string debugName)
    {
        if (source == null) throw new ArgumentNullException("source");
        // Instantiate directly under the inactive DontDestroyOnLoad resource root. This prevents
        // inherited activeSelf=true from ever becoming activeInHierarchy during construction, so
        // DewCollider.OnEnable cannot call DewPhysics before its proxy pool exists. The stock
        // template itself is never toggled or mutated.
        GameObject clone = UnityEngine.Object.Instantiate(source, _resourceRoot.transform, false);
        clone.SetActive(false);
        DariusLog.DebugInfo("TRAVELER-COPY", "Cloned native template under inactive resource root name=" +
            debugName + " source=" + source.name);
        return clone;
    }

    private static void StripConstructionTemplatePresentation(GameObject root, Component keepMain)
    {
        if (root == null) return;
        Component[] components = root.GetComponentsInChildren<Component>(true);
        int removed = 0;
        for (int i = components.Length - 1; i >= 0; i--)
        {
            Component c = components[i];
            if (c == null || c == keepMain || c is Transform || c is NetworkIdentity) continue;
            bool presentation = c is Renderer || c is AudioSource || c is ParticleSystem;
            string typeName = c.GetType().Name;
            bool characterSpecific = typeName.IndexOf("Vesper", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                     typeName.IndexOf("Mace", StringComparison.OrdinalIgnoreCase) >= 0;
            if (!presentation && !characterSpecific) continue;
            try { UnityEngine.Object.DestroyImmediate(c); removed++; } catch { }
        }
        DariusLog.DebugInfo("ATK-NATIVE-PREFAB", "Construction-time template scrub root=" + root.name + " removedComponents=" + removed);
    }

    private static int CountVesperNamedComponents(GameObject root)
    {
        if (root == null) return 0;
        int count = 0;
        Component[] components = root.GetComponentsInChildren<Component>(true);
        for (int i = 0; i < components.Length; i++)
        {
            Component c = components[i];
            if (c != null && c.GetType().Name.IndexOf("Vesper", StringComparison.OrdinalIgnoreCase) >= 0) count++;
        }
        return count;
    }

    private static TriggerConfig CloneTriggerConfig(TriggerConfig source)
    {
        TriggerConfig clone = new TriggerConfig();
        if (source == null) return clone;
        Type t = typeof(TriggerConfig);
        while (t != null && t != typeof(object))
        {
            FieldInfo[] fields = t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            for (int i = 0; i < fields.Length; i++)
            {
                FieldInfo f = fields[i];
                if (f.IsStatic || f.IsInitOnly) continue;
                try { f.SetValue(clone, f.GetValue(source)); } catch { }
            }
            t = t.BaseType;
        }
        return clone;
    }

    private static CastMethodData CloneCastMethodData(CastMethodData source)
    {
        if (source == null) return null;
        CastMethodData clone = new CastMethodData();
        Type t = source.GetType();
        while (t != null && t != typeof(object))
        {
            FieldInfo[] fields = t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            for (int i = 0; i < fields.Length; i++)
            {
                FieldInfo f = fields[i];
                if (f.IsStatic || f.IsInitOnly) continue;
                try { f.SetValue(clone, f.GetValue(source)); } catch { }
            }
            t = t.BaseType;
        }
        return clone;
    }

    private static T ResolveResourceByTypeName<T>(string typeName) where T : UnityEngine.Object
    {
        Type resourceType = AccessTools.TypeByName(typeName);
        if (resourceType == null) return null;
        MethodInfo[] methods = typeof(DewResources).GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
        for (int i = 0; i < methods.Length; i++)
        {
            MethodInfo m = methods[i];
            if (m.Name != "GetByType" || m.IsGenericMethod) continue;
            ParameterInfo[] ps = m.GetParameters();
            if (ps.Length < 1 || ps[0].ParameterType != typeof(Type)) continue;
            object[] args = new object[ps.Length];
            args[0] = resourceType;
            for (int j = 1; j < ps.Length; j++)
                args[j] = ps[j].HasDefaultValue ? ps[j].DefaultValue : (ps[j].ParameterType.IsValueType ? Activator.CreateInstance(ps[j].ParameterType) : null);
            try
            {
                UnityEngine.Object result = m.Invoke(null, args) as UnityEngine.Object;
                T direct = result as T;
                if (direct != null) return direct;
                GameObject resultGo = result as GameObject;
                if (resultGo != null)
                {
                    T component = resultGo.GetComponent(resourceType) as T;
                    if (component != null) return component;
                }
                Component c = result as Component;
                if (c != null)
                {
                    T component = c.GetComponent(resourceType) as T;
                    if (component != null) return component;
                }
            }
            catch { }
        }
        return null;
    }

    private static void CreateAndRegisterSkin()
    {
        Skin source = ResolveGenericResource<Skin>("GetByName", "Skin_Vesper_Default", false);
        if (source == null) throw new InvalidOperationException("Could not resolve stock Skin template contract.");
        EntityModel sourceModel = source.GetComponent<EntityModel>();
        if (sourceModel == null) throw new InvalidOperationException("Stock Skin template has no EntityModel contract.");

        for (int i = 0; i < SkinSpecs.Length; i++)
        {
            DariusSkinSpec spec = SkinSpecs[i];
            Skin existing;
            if (SkinsByName.TryGetValue(spec.name, out existing) && existing != null) continue;
            Skin created = CreateAndRegisterSkinResource(sourceModel, spec);
            SkinsByName[spec.name] = created;
        }
        Skin defaultSkin; SkinsByName.TryGetValue(DefaultSkinName, out defaultSkin); DefaultSkin = defaultSkin;
        Skin godKingSkin; SkinsByName.TryGetValue(GodKingSkinName, out godKingSkin); GodKingSkin = godKingSkin;
        Skin dunkmasterSkin; SkinsByName.TryGetValue(DunkmasterSkinName, out dunkmasterSkin); DunkmasterSkin = dunkmasterSkin;
        Skin mechaSkin; SkinsByName.TryGetValue(MechaSkinName, out mechaSkin); MechaSkin = mechaSkin;

        DariusLog.Info("TRAVELER-SKIN", "Darius skins ready count=" + SkinsByName.Count + ". Every skin owns an explicit GLB/animation profile.");
    }

    private static Skin CreateAndRegisterSkinResource(EntityModel sourceModel, DariusSkinSpec spec)
    {
        GameObject go = new GameObject(spec.name);
        go.transform.SetParent(_resourceRoot.transform, false);
        go.SetActive(true);
        go.hideFlags = HideFlags.None;

        Skin skin = go.AddComponent<Skin>();
        skin.name = spec.name;
        skin.category = HeroName;
        skin.rarity = SkinRarity.Default;
        skin.requiredLevel = 0;
        skin.generatedFromServer = false;
        ClearArrayMember(skin, "skillVisuals");
        // Native wardrobe contract: UI_SkinList_Item.Setup reads Skin.previewImage directly.
        // Do not scan/patch arbitrary UI Image or portrait-like members; each Skin owns its
        // authentic League selection portrait as a normal resource field.
        Sprite skinPreview = DariusPrototypeIcons.Get(spec.previewIconKey);
        if (skinPreview != null) TryAssignIfCompatible(skin, "previewImage", skinPreview);
        bool previewAssigned = skinPreview != null && object.ReferenceEquals(ReadMemberValue(skin, "previewImage"), skinPreview);
        if (!previewAssigned)
            DariusLog.Error("SKIN-ICON", "Failed assigning native Skin.previewImage skin=" + spec.name + " key=" + spec.previewIconKey);
        else
            DariusLog.Info("SKIN-ICON", "Assigned native Skin.previewImage skin=" + spec.name + " key=" + spec.previewIconKey + " sprite=" + skinPreview.name);

        DariusSkinModelBinding binding = go.AddComponent<DariusSkinModelBinding>();
        binding.skinResourceName = spec.name; binding.modelFile = spec.modelFile; binding.displayName = spec.displayName;
        binding.variantKey = spec.variantKey; binding.isGodKingSkin = spec.godKing; binding.modelScale = spec.modelScale; binding.modelYOffset = spec.modelYOffset; binding.modelYaw = spec.modelYaw;
        binding.expectedPrimitives = spec.expectedPrimitives; binding.expectedBones = spec.expectedBones; binding.expectedAnimations = spec.expectedAnimations;
        binding.idleClip = spec.idle; binding.idleVariantClip = spec.idleVariant; binding.runClip = spec.run; binding.deathClip = spec.death;
        binding.attack1Clip = spec.attack1; binding.attack2Clip = spec.attack2; binding.critClip = spec.crit;
        binding.qIntroClip = spec.qIntro; binding.qClip = spec.q; binding.wClip = spec.w; binding.eClip = spec.e; binding.rClip = spec.r;
        binding.attack1ToIdleClip = spec.attack1ToIdle; binding.attack2ToIdleClip = spec.attack2ToIdle; binding.critToIdleClip = spec.critToIdle;
        binding.eToRunClip = spec.eToRun; binding.eToIdleClip = spec.eToIdle; binding.rToRunClip = spec.rToRun;

        EntityModel model = go.AddComponent<EntityModel>();
        RestoreUnitySerializedFields(model, CaptureUnitySerializedFields(sourceModel, typeof(EntityModel)));
        model.name = spec.name; model.bodyRenderers = new Renderer[0]; model.fxLoop = null; model.fxDeath = null; model.fxTakeDamage = null;
        model.customMappings = new List<EntityModelCustomMapping>();
        Transform healthAnchor = CreateSkinAnchor(go.transform, spec.name + "_HealthBarAnchor", new Vector3(0f, 2.75f, 0f));
        Transform weaponAnchor = CreateSkinAnchor(go.transform, spec.name + "_WeaponAnchor", new Vector3(0f, 1.25f, 0.45f));
        Transform holsteredAnchor = CreateSkinAnchor(go.transform, spec.name + "_HolsteredWeaponAnchor", new Vector3(0f, 1.2f, -0.25f));
        model.healthBarPosition = healthAnchor; model.weapon = weaponAnchor; model.holsteredWeapon = holsteredAnchor;

        if (go.GetComponent<DariusTravelerModelInstance>() == null) go.AddComponent<DariusTravelerModelInstance>();
        OwnedObjects.Add(go);
        RegisterNamedResource(skin, go, spec.name, spec.guid);
        DariusLog.Info("TRAVELER-SKIN", "Created runtime skin=" + spec.name + " guid=" + spec.guid + " model=" + spec.modelFile + " profile=" + spec.variantKey + " display=" + spec.displayName);
        return skin;
    }

    private static Transform CreateSkinAnchor(Transform parent, string name, Vector3 localPosition)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;
        return go.transform;
    }

    private static void ClearArrayMember(object owner, string memberName)
    {
        if (owner == null || string.IsNullOrEmpty(memberName)) return;
        Type t = owner.GetType();
        FieldInfo field = FindFieldRecursive(t, memberName);
        if (field != null && field.FieldType.IsArray)
        {
            try { field.SetValue(owner, Array.CreateInstance(field.FieldType.GetElementType(), 0)); } catch { }
            return;
        }
        PropertyInfo property = t.GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (property != null && property.CanWrite && property.PropertyType.IsArray)
        {
            try { property.SetValue(owner, Array.CreateInstance(property.PropertyType.GetElementType(), 0), null); } catch { }
        }
    }

    private static void CreateAndRegisterHero()
    {
        Hero source = ResolveGenericResource<Hero>("GetByShortTypeName", "Hero_Vesper", false);
        if (source == null) throw new InvalidOperationException("Could not resolve stock Hero_Vesper prefab.");

        GameObject go = UnityEngine.Object.Instantiate(source.gameObject, _resourceRoot.transform, false);
        // The parent resourceRoot is inactive; activeSelf remains true for future spawned clones.
        go.SetActive(true);
        go.name = HeroName;
        go.hideFlags = HideFlags.None;

        Hero oldHero = go.GetComponent<Hero>();
        if (oldHero == null) throw new InvalidOperationException("Cloned Hero_Vesper has no Hero component.");
        Dictionary<FieldInfo, object> serializedHeroFields = CaptureUnitySerializedFields(oldHero, typeof(Hero));

        // Rewire the generic component graph BEFORE destroying the template Hero component.
        // Destroying first turns oldHero into Unity-null and made the previous reference rebinder a
        // no-op, leaving EntityStatus/EntityAbility/etc. with stale MissingReference owners. This
        // manifested as a dead basic-attack path and nonsensical 500-valued detail-panel entries.
        Hero_Darius hero = go.AddComponent<Hero_Darius>();
        RestoreUnitySerializedFields(hero, serializedHeroFields);
        ReplaceDirectComponentReferences(go, oldHero, hero);
        int heroResidualRefs = CountDirectComponentReferences(go, oldHero, hero);
        if (heroResidualRefs > 0)
            DariusLog.Warn("TRAVELER-COPY", "Hero component graph still has " + heroResidualRefs + " direct references to old Hero_Vesper before destruction.");
        UnityEngine.Object.DestroyImmediate(oldHero);
        hero.name = HeroName;
        // Phase 1 still uses a stock melee Hero GameObject only as a serialized generic component graph.
        // The Hero_Vesper component itself has already been removed/replaced above; Skin, attack,
        // Identity and movement loadout references are then rebound to Darius-owned resources.
        EntityAbility entityAbility = go.GetComponent<EntityAbility>();
        if (entityAbility == null) throw new InvalidOperationException("Cloned Hero prefab has no EntityAbility component.");
        if (AttackPrefab == null) throw new InvalidOperationException("At_DariusAxe must be registered before Hero_Darius.");
        entityAbility.attackAbilityPreset = new AssetRef<AttackTrigger>(AttackPrefab);
        ClearEntityAbilityRuntimeAttackState(entityAbility);
        DariusNativeAttackBinder binder = go.GetComponent<DariusNativeAttackBinder>();
        if (binder == null) binder = go.AddComponent<DariusNativeAttackBinder>();
        binder.EnsureBound("Hero_Darius prefab construction");
        RepairHeroBaseStatContract(source, hero);
        RepairHeroGrowthStatContract(source, hero);
        DariusLog.Info("ATK-NATIVE", "Hero_Darius EntityAbility.attackAbilityPreset -> At_DariusAxe with a per-Hero native binder. Vesper attack trigger is no longer the runtime attack preset.");

        // Reuse the stock generic HeroSkill component, but replace the actual loadout arrays.
        HeroSkill heroSkill = go.GetComponent<HeroSkill>();
        if (heroSkill == null) throw new InvalidOperationException("Cloned Hero prefab has no HeroSkill component.");
        // These must be the real serialized arrays: both the native UI and server-side
        // HeroLoadoutData.Validate_Imp read these exact arrays. AssetRef<T> is a public
        // Dew.Core value type and the skill resources are already registered at this point.
        heroSkill.loadoutQ = new[] { new AssetRef<SkillTrigger>(DariusFormalRegistry.Decimate) };
        heroSkill.loadoutR = new[] { new AssetRef<SkillTrigger>(DariusFormalRegistry.NoxianGuillotine) };
        heroSkill.loadoutTrait = new[] { new AssetRef<SkillTrigger>(DariusFormalRegistry.Hemorrhage) };
        heroSkill.loadoutMovement = new[]
        {
            new AssetRef<SkillTrigger>(DariusFormalRegistry.Flash),
            new AssetRef<SkillTrigger>(DariusFormalRegistry.Ghost)
        };
        DariusLog.Info("TRAVELER-LOADOUT", "Native AssetRef arrays configured Q/R/Identity; Movement replaced with selectable Flash/Ghost.");

        // Runtime container exists on every Hero_Darius instance before native constellation effects
        // are created. StarEffect.OnStartServer then only has to populate the equipped star levels.
        if (go.GetComponent<DariusConstellationRuntime>() == null)
            go.AddComponent<DariusConstellationRuntime>();

        hero.cDestruction.defaultCount = 4; hero.cDestruction.maxCount = 6; hero.cDestruction.angleOffset = 0f;
        hero.cLife.defaultCount = 2; hero.cLife.maxCount = 4; hero.cLife.angleOffset = 15f;
        hero.cImagination.defaultCount = 1; hero.cImagination.maxCount = 4; hero.cImagination.angleOffset = -15f;
        hero.cFlexible.defaultCount = 2; hero.cFlexible.maxCount = 4; hero.cFlexible.angleOffset = 30f;
        try
        {
            DariusLog.Info("CONSTELLATION-SETTINGS", "Darius Noxian offense slot profile " +
                "D=" + hero.cDestruction.defaultCount + "/" + hero.cDestruction.maxCount +
                " L=" + hero.cLife.defaultCount + "/" + hero.cLife.maxCount +
                " I=" + hero.cImagination.defaultCount + "/" + hero.cImagination.maxCount +
                " F=" + hero.cFlexible.defaultCount + "/" + hero.cFlexible.maxCount);
        }
        catch (Exception e)
        {
            DariusLog.Exception("CONSTELLATION-SETTINGS", e, "Could not log inherited constellation slot settings; values were left untouched.");
        }

        // Bind the public-facing hero presentation to Darius-owned cosmetic resources.
        Sprite heroPortrait = DariusPrototypeIcons.Get("HERO");
        // Native hero icon contract: UI_HeroIcon / UI_InGame_HeroInfoBar read Hero.icon and
        // Hero.mainColor. Set those exact resource fields and let stock UI render them.
        TryAssignIfCompatible(hero, "icon", heroPortrait);
        TryAssignIfCompatible(hero, "mainColor", new Color(0.36f, 0.075f, 0.055f, 1f));
        RepairHeroCosmeticContract(hero);

        NetworkIdentity identity = go.GetComponent<NetworkIdentity>();
        if (identity == null) throw new InvalidOperationException("Cloned Hero prefab has no NetworkIdentity.");
        ConfigureNetworkIdentity(identity, HeroAssetId);
        ReinitializeNetworkBehaviours(identity);

        HeroPrefab = hero;
        ValidateNativeCosmeticIconContract("hero-created");
        OwnedObjects.Add(go);
        RegisterTypedResource(hero, go, HeroName, HeroGuid, HeroAssetId);
        DariusLog.Info("TRAVELER-HERO", "Created Hero_Darius from generic stock entity component graph; Darius Skin + At_DariusAxe are independent runtime resources; movement choices=Flash/Ghost.");
    }


    private static void RepairHeroCosmeticContract(Hero_Darius hero)
    {
        if (hero == null || DefaultSkin == null) return;
        int rebound = 0;
        Sprite portrait = DariusPrototypeIcons.Get("HERO");
        object skinRef = null;
        try { skinRef = new AssetRef<Skin>(DefaultSkin); } catch { }

        // The generic Hero template contains presentation members that can still point at its stock
        // default cosmetic after the concrete Hero component is replaced. Rebind only skin/portrait
        // members; gameplay, constellation and class data remain inherited from the native Hero graph.
        foreach (string name in new[] { "defaultSkinName", "skinName", "selectedSkinName", "defaultSkinType" })
        {
            if (TryAssignCount(hero, name, DefaultSkinName)) rebound++;
        }
        foreach (string name in new[] { "defaultSkin", "skin", "skinPreset", "defaultSkinPreset", "skinRef", "defaultSkinRef" })
        {
            if (TryAssignCount(hero, name, DefaultSkin)) rebound++;
            if (skinRef != null && TryAssignCount(hero, name, skinRef)) rebound++;
        }
        if (portrait != null && TryAssignCount(hero, "icon", portrait)) rebound++;
        if (TryAssignCount(hero, "mainColor", new Color(0.36f, 0.075f, 0.055f, 1f))) rebound++;
        // Skin thumbnails are not the hero portrait. Rebind each Skin.previewImage to its own
        // packaged League selection portrait so UI_SkinList can read the native field directly.
        for (int si = 0; si < SkinSpecs.Length; si++)
        {
            Skin skin;
            if (!SkinsByName.TryGetValue(SkinSpecs[si].name, out skin) || skin == null) continue;
            Sprite preview = DariusPrototypeIcons.Get(SkinSpecs[si].previewIconKey);
            if (preview != null && TryAssignCount(skin, "previewImage", preview)) rebound++;
        }

        // Scrub inherited serialized skin members by type as well as by common names. In particular,
        // stock Heroes may keep their selectable/default cosmetics in an array, which otherwise leaves
        // the wardrobe presenting the template Hero even though Hero_Darius has its own Skin resource.
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        for (Type t = hero.GetType(); t != null && t != typeof(UnityEngine.Object); t = t.BaseType)
        {
            foreach (FieldInfo field in t.GetFields(flags))
            {
                if (field.IsStatic || field.IsInitOnly || field.Name.IndexOf("skin", StringComparison.OrdinalIgnoreCase) < 0) continue;
                try
                {
                    Type ft = field.FieldType;
                    if (ft == typeof(string))
                    {
                        string value = field.GetValue(hero) as string;
                        if (!string.IsNullOrEmpty(value) && value.IndexOf("Vesper", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            field.SetValue(hero, DefaultSkinName);
                            rebound++;
                        }
                    }
                    else if (typeof(Skin).IsAssignableFrom(ft))
                    {
                        field.SetValue(hero, DefaultSkin);
                        rebound++;
                    }
                    else if (skinRef != null && ft.IsInstanceOfType(skinRef))
                    {
                        field.SetValue(hero, skinRef);
                        rebound++;
                    }
                    else if (ft.IsArray)
                    {
                        Type element = ft.GetElementType();
                        if (element == typeof(string))
                        {
                            Array array = Array.CreateInstance(element, 1);
                            array.SetValue(DefaultSkinName, 0);
                            field.SetValue(hero, array);
                            rebound++;
                        }
                        else if (typeof(Skin).IsAssignableFrom(element))
                        {
                            Array array = Array.CreateInstance(element, 1);
                            array.SetValue(DefaultSkin, 0);
                            field.SetValue(hero, array);
                            rebound++;
                        }
                        else if (skinRef != null && element.IsInstanceOfType(skinRef))
                        {
                            Array array = Array.CreateInstance(element, 1);
                            array.SetValue(skinRef, 0);
                            field.SetValue(hero, array);
                            rebound++;
                        }
                    }
                }
                catch { }
            }
        }
        DariusLog.Info("TRAVELER-SKIN", "Hero_Darius cosmetic references rebound to " + DefaultSkinName + " members=" + rebound);
    }


    private static bool TryAssignCount(object obj, string name, object value)
    {
        if (obj == null) return false;
        Type t = obj.GetType();
        BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        while (t != null)
        {
            FieldInfo f = t.GetField(name, flags | BindingFlags.DeclaredOnly) ?? t.GetField("<" + name + ">k__BackingField", flags | BindingFlags.DeclaredOnly);
            if (f != null && !f.IsInitOnly && IsCompatible(f.FieldType, value))
            {
                try { f.SetValue(obj, value); return true; } catch { return false; }
            }
            t = t.BaseType;
        }
        try
        {
            PropertyInfo p = obj.GetType().GetProperty(name, flags);
            if (p != null && p.CanWrite && IsCompatible(p.PropertyType, value))
            {
                p.SetValue(obj, value, null);
                return true;
            }
        }
        catch { }
        return false;
    }


    private static void RepairHeroBaseStatContract(Hero source, Hero_Darius target)
    {
        if (source == null || target == null) return;
        int copied = 0;
        int cloned = 0;
        int tuned = 0;
        copied += CopySameNamedMember(source, target, "baseStats");
        EntityStatus sourceStatus = source.GetComponent<EntityStatus>();
        EntityStatus targetStatus = target.GetComponent<EntityStatus>();
        if (sourceStatus != null && targetStatus != null)
            copied += CopySameNamedMember(sourceStatus, targetStatus, "baseStats");

        object heroStats = CloneMemberValue(source, "baseStats");
        if (heroStats != null)
        {
            tuned += ApplyDariusBaseStatTuning(heroStats);
            if (TryAssignCount(target, "baseStats", heroStats)) cloned++;
        }

        if (sourceStatus != null && targetStatus != null)
        {
            object statusStats = CloneMemberValue(sourceStatus, "baseStats") ?? CloneMemberValue(source, "baseStats");
            if (statusStats != null)
            {
                tuned += ApplyDariusBaseStatTuning(statusStats);
                if (TryAssignCount(targetStatus, "baseStats", statusStats)) cloned++;
            }
        }

        DariusLog.Info("STAT-CONTRACT", "Hero_Darius base-stat contract normalized from native Hero template copiedMembers=" + copied +
            " clonedMembers=" + cloned + " tunedFields=" + tuned +
            " profile=juggernaut(+health,+attack,+armor,-speed)");
    }

    private static void RepairHeroGrowthStatContract(Hero source, Hero_Darius target)
    {
        if (source == null || target == null) return;
        int copied = 0;
        int cloned = 0;
        int tuned = 0;
        copied += CopySameNamedMember(source, target, "scalingStats");
        EntityStatus sourceStatus = source.GetComponent<EntityStatus>();
        EntityStatus targetStatus = target.GetComponent<EntityStatus>();
        if (sourceStatus != null && targetStatus != null)
            copied += CopySameNamedMember(sourceStatus, targetStatus, "scalingStats");

        object heroScaling = CloneMemberValue(source, "scalingStats");
        if (heroScaling != null)
        {
            tuned += ApplyDariusGrowthStatTuning(heroScaling);
            if (TryAssignCount(target, "scalingStats", heroScaling)) cloned++;
        }

        if (sourceStatus != null && targetStatus != null)
        {
            object statusScaling = CloneMemberValue(sourceStatus, "scalingStats") ?? CloneMemberValue(source, "scalingStats");
            if (statusScaling != null)
            {
                tuned += ApplyDariusGrowthStatTuning(statusScaling);
                if (TryAssignCount(targetStatus, "scalingStats", statusScaling)) cloned++;
            }
        }

        DariusLog.Info("STAT-CONTRACT", "Hero_Darius growth-stat contract normalized from native Hero_Vesper curve copiedMembers=" + copied +
            " clonedMembers=" + cloned + " tunedFields=" + tuned +
            " profile=juggernaut growth(AD +3, AP +0, HP +25%, Armor +2)");
    }

    private static int ApplyDariusGrowthStatTuning(object scalingStats)
    {
        if (scalingStats == null) return 0;
        int changed = 0;
        changed += TryScaleNumericMember(scalingStats, new[] { "attackDamageFlat" }, 1.50f);
        changed += TryScaleNumericMember(scalingStats, new[] { "abilityPowerFlat" }, 0.00f);
        changed += TryScaleNumericMember(scalingStats, new[] { "maxHealthPercentage" }, 1.00f);
        changed += TryScaleNumericMember(scalingStats, new[] { "armorFlat" }, 1.00f);
        return changed;
    }

    private static object CloneMemberValue(object owner, string name)
    {
        object value = ReadMemberValue(owner, name);
        return ClonePlainObject(value);
    }

    private static object ReadMemberValue(object owner, string name)
    {
        if (owner == null || string.IsNullOrEmpty(name)) return null;
        BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        FieldInfo f = FindFieldRecursive(owner.GetType(), name) ?? FindFieldRecursive(owner.GetType(), "<" + name + ">k__BackingField");
        if (f != null)
        {
            try { return f.GetValue(owner); } catch { }
        }
        PropertyInfo p = owner.GetType().GetProperty(name, flags);
        if (p != null && p.CanRead)
        {
            try { return p.GetValue(owner, null); } catch { }
        }
        return null;
    }

    private static object ClonePlainObject(object source)
    {
        if (source == null) return null;
        Type type = source.GetType();
        if (type.IsValueType || type == typeof(string)) return source;
        try
        {
            object clone;
            try { clone = Activator.CreateInstance(type, true); }
            catch { clone = System.Runtime.Serialization.FormatterServices.GetUninitializedObject(type); }
            for (Type t = type; t != null; t = t.BaseType)
            {
                foreach (FieldInfo field in t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    if (field.IsStatic) continue;
                    try { field.SetValue(clone, field.GetValue(source)); } catch { }
                }
            }
            return clone;
        }
        catch (Exception e)
        {
            DariusLog.Exception("STAT-CONTRACT", e, "Could not clone member object type=" + type.FullName);
            return null;
        }
    }

    private static int ApplyDariusBaseStatTuning(object baseStats)
    {
        if (baseStats == null) return 0;
        int changed = 0;
        changed += TryScaleNumericMember(baseStats, new[] { "maxHealth", "health", "hp" }, 1.12f);
        changed += TryScaleNumericMember(baseStats, new[] { "attackDamage", "damage", "physicalDamage", "attackPower" }, 1.10f);
        changed += TryScaleNumericMember(baseStats, new[] { "armor", "defense", "physicalDefence", "physicalDefense" }, 1.08f);
        changed += TryScaleNumericMember(baseStats, new[] { "moveSpeed", "movementSpeed", "speed" }, 0.97f);
        return changed;
    }

    private static int TryScaleNumericMember(object owner, string[] names, float scale)
    {
        if (owner == null || names == null) return 0;
        for (int i = 0; i < names.Length; i++)
        {
            if (TryScaleNumericMember(owner, names[i], scale)) return 1;
        }
        return 0;
    }

    private static bool TryScaleNumericMember(object owner, string name, float scale)
    {
        if (owner == null || string.IsNullOrEmpty(name)) return false;
        BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        FieldInfo field = FindFieldRecursive(owner.GetType(), name) ?? FindFieldRecursive(owner.GetType(), "<" + name + ">k__BackingField");
        if (field != null && !field.IsInitOnly)
            return TryScaleNumericTarget(owner, field.FieldType, () => field.GetValue(owner), v => field.SetValue(owner, v), scale, name);
        PropertyInfo property = owner.GetType().GetProperty(name, flags);
        if (property != null && property.CanRead && property.CanWrite)
            return TryScaleNumericTarget(owner, property.PropertyType, () => property.GetValue(owner, null), v => property.SetValue(owner, v, null), scale, name);
        return false;
    }

    private static bool TryScaleNumericTarget(object owner, Type memberType, Func<object> getter, Action<object> setter, float scale, string name)
    {
        try
        {
            object raw = getter();
            if (raw == null) return false;
            if (memberType == typeof(float))
            {
                float current = (float)raw;
                setter(Mathf.Round(current * scale * 100f) / 100f);
                return true;
            }
            if (memberType == typeof(double))
            {
                double current = (double)raw;
                setter(Math.Round(current * scale, 2));
                return true;
            }
            if (memberType == typeof(int))
            {
                int current = (int)raw;
                setter(Mathf.RoundToInt(current * scale));
                return true;
            }
            if (memberType == typeof(long))
            {
                long current = (long)raw;
                setter((long)Math.Round(current * scale));
                return true;
            }
        }
        catch (Exception e)
        {
            DariusLog.DebugInfo("STAT-CONTRACT", "Failed scaling base stat member " + owner.GetType().Name + "." + name + ": " + e.Message);
        }
        return false;
    }

    private static int CopySameNamedMember(object source, object target, string name)
    {
        if (source == null || target == null) return 0;
        BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        FieldInfo sf = FindFieldRecursive(source.GetType(), name) ?? FindFieldRecursive(source.GetType(), "<" + name + ">k__BackingField");
        FieldInfo tf = FindFieldRecursive(target.GetType(), name) ?? FindFieldRecursive(target.GetType(), "<" + name + ">k__BackingField");
        if (sf != null && tf != null && !tf.IsInitOnly && tf.FieldType.IsAssignableFrom(sf.FieldType))
        {
            try { tf.SetValue(target, sf.GetValue(source)); return 1; } catch { }
        }
        PropertyInfo sp = source.GetType().GetProperty(name, flags);
        PropertyInfo tp = target.GetType().GetProperty(name, flags);
        if (sp != null && sp.CanRead && tp != null && tp.CanWrite && tp.PropertyType.IsAssignableFrom(sp.PropertyType))
        {
            try { tp.SetValue(target, sp.GetValue(source, null), null); return 1; } catch { }
        }
        return 0;
    }


    private static void ClearEntityAbilityRuntimeAttackState(EntityAbility ability)
    {
        if (ability == null) return;
        string[] fields = { "_originalAttackAbility", "_overridenAttackAbility", "_attackAbilityOverrides" };
        for (int i = 0; i < fields.Length; i++)
        {
            FieldInfo f = FindFieldRecursive(ability.GetType(), fields[i]);
            if (f == null) continue;
            try
            {
                object current = f.GetValue(ability);
                IList list = current as IList;
                if (list != null)
                {
                    list.Clear();
                    continue;
                }
                f.SetValue(ability, f.FieldType.IsValueType ? Activator.CreateInstance(f.FieldType) : null);
            }
            catch { }
        }
    }

    private static void RegisterTypes()
    {
        // Force lazy caches to exist first, then append only our exact types.
        try { var _ = Dew.allHeroes; } catch { }
        try { var _ = Dew.allSkills; } catch { }
        AddUniqueTypeToDew("_allHeroes", typeof(Hero_Darius));
        AddUniqueTypeToDew("_allSkills", typeof(St_Darius_Decimate));
        AddUniqueTypeToDew("_allSkills", typeof(St_Darius_NoxianGuillotine));
        AddUniqueTypeToDew("_allSkills", typeof(St_D_Darius_Hemorrhage));
        AddUniqueTypeToDew("_allSkills", typeof(St_Darius_CripplingStrike));
        AddUniqueTypeToDew("_allSkills", typeof(St_Darius_Apprehend));
        AddUniqueTypeToDew("_allSkills", typeof(St_Darius_Flash));
        AddUniqueTypeToDew("_allSkills", typeof(St_Darius_Ghost));
        AddUniqueTypeToDew("_allHeroSkills", typeof(St_Darius_Decimate));
        AddUniqueTypeToDew("_allHeroSkills", typeof(St_Darius_NoxianGuillotine));
        AddUniqueTypeToDew("_allHeroSkills", typeof(St_D_Darius_Hemorrhage));
        AddUniqueTypeToDew("_allHeroSkills", typeof(St_Darius_Flash));
        AddUniqueTypeToDew("_allHeroSkills", typeof(St_Darius_Ghost));
        DariusConstellationRegistry.ReassertTypeCache();
        DariusLog.DebugInfoThrottled("TRAVELER-TYPE", "type-caches", "Dew type caches appended: Hero_Darius + Q/R/Identity + Flash/Ghost movement + ordinary W/E Memories + Darius constellation StarEffects.", 20.0);
    }

    public static void RegisterContent(DewGameContentSettings content)
    {
        if (content == null) return;
        AddStringMember(content, "_availableHeroes", HeroName);
        AddStringMember(content, "availableHeroes", HeroName);
        for (int i = 0; i < SkinSpecs.Length; i++)
        {
            AddStringMember(content, "_availableSkins", SkinSpecs[i].name);
            AddStringMember(content, "availableSkins", SkinSpecs[i].name);
        }

        string[] skillNames =
        {
            DariusFormalRegistry.Decimate != null ? DariusFormalRegistry.Decimate.name : "St_Darius_Decimate",
            DariusFormalRegistry.NoxianGuillotine != null ? DariusFormalRegistry.NoxianGuillotine.name : "St_Darius_NoxianGuillotine",
            DariusFormalRegistry.Hemorrhage != null ? DariusFormalRegistry.Hemorrhage.name : "St_D_Darius_Hemorrhage",
            DariusFormalRegistry.CripplingStrike != null ? DariusFormalRegistry.CripplingStrike.name : "St_Darius_CripplingStrike",
            DariusFormalRegistry.Apprehend != null ? DariusFormalRegistry.Apprehend.name : "St_Darius_Apprehend",
            DariusFormalRegistry.Flash != null ? DariusFormalRegistry.Flash.name : "St_Darius_Flash",
            DariusFormalRegistry.Ghost != null ? DariusFormalRegistry.Ghost.name : "St_Darius_Ghost"
        };
        foreach (string skill in skillNames)
        {
            AddStringMember(content, "_availableSkills", skill);
            AddStringMember(content, "availableSkills", skill);
        }
        foreach (Type starType in Dew.allStarTypes)
        {
            if (starType == null || !starType.Name.StartsWith("Se_Star_Darius_", StringComparison.Ordinal)) continue;
            AddStringMember(content, "_availableStars", starType.Name);
            AddStringMember(content, "availableStars", starType.Name);
        }
        DariusLog.DebugInfoThrottled("TRAVELER-CONTENT", "content", "Content includes Hero_Darius, " + SkinSpecs.Length + " explicit skins, and seven Darius skill resources including Flash/Ghost movement choices.", 20.0);
    }

    private static void EnsureProfiles()
    {
        DewProfile profile = DewSave.profileMain;
        DewProfileStats stats = DewSave.profileStats;

        // Sanitize legacy Vesper-derived selections BEFORE the first profile validation. rc6 did this
        // only after Validate(), which made validation repeatedly probe five stale Vesper StarEffect
        // names and missing skin Addressable keys, producing a burst of avoidable errors at boot.
        if (profile != null)
        {
            DariusConstellationPersistence.RestoreBootBackup(profile);
            RepairDariusConstellationLoadout(profile);
            RepairLegacySelectedSkinAliases(profile, "EnsureProfiles-prevalidate");
        }

        // DewProfile.Validate may call UnlockHero internally. Stock UnlockHero can immediately
        // index the hero progression entry in profileStats, so create that exact default entry BEFORE
        // the first validation pass instead of relying on a later recovery pass. This removes the
        // startup Validate -> UnlockHero null race seen in the runtime logs.
        if (stats != null)
        {
            EnsureDictionaryEntry(stats, "heroes", HeroName, CreateDefaultDictionaryValue);
            EnsureDictionaryEntry(stats, "heroStats", HeroName, CreateDefaultDictionaryValue);
        }

        if (profile != null) EnsureHeroProfileEntries(profile);

        // Never run a whole-profile validation while the Mod loader is still enumerating character
        // assemblies. With several runtime Travelers installed, each Validate postfix repairs every
        // other registry and mutates the same Dew type collections being enumerated. Runtime logs
        // showed one call blocking the Unity main thread for 246 seconds before throwing.
        if (profile != null) InvokePreferredSettingsValidate(profile);

        if (profile != null)
        {
            // Use the public profile API when available instead of fabricating cosmetic/profile
            // values. These calls are idempotent in the stock profile implementation.
            for (int si = 0; si < SkinSpecs.Length; si++)
            {
                string skinName = SkinSpecs[si].name;
                try { profile.UnlockSkin(skinName, "local.darius.independent"); }
                catch (Exception e)
                {
                    DariusLog.Exception("TRAVELER-PROFILE", e, "Unlock Darius skin failed name=" + skinName + "; applying dictionary fallback");
                    EnsureDictionaryEntry(profile, "skins", skinName, CreateCosmeticUnlockValue);
                }
            }
            RepairLegacySelectedSkinAliases(profile, "EnsureProfiles");

            string[] requiredSkills =
            {
                DariusFormalRegistry.Decimate != null ? DariusFormalRegistry.Decimate.name : "St_Darius_Decimate",
                DariusFormalRegistry.NoxianGuillotine != null ? DariusFormalRegistry.NoxianGuillotine.name : "St_Darius_NoxianGuillotine",
                DariusFormalRegistry.Hemorrhage != null ? DariusFormalRegistry.Hemorrhage.name : "St_D_Darius_Hemorrhage",
                DariusFormalRegistry.CripplingStrike != null ? DariusFormalRegistry.CripplingStrike.name : "St_Darius_CripplingStrike",
                DariusFormalRegistry.Apprehend != null ? DariusFormalRegistry.Apprehend.name : "St_Darius_Apprehend",
                DariusFormalRegistry.Flash != null ? DariusFormalRegistry.Flash.name : "St_Darius_Flash",
                DariusFormalRegistry.Ghost != null ? DariusFormalRegistry.Ghost.name : "St_Darius_Ghost"
            };
            foreach (string skillName in requiredSkills)
            {
                try { profile.UnlockSkill(skillName); }
                catch { EnsureSkillUnlock(profile, skillName); }
            }
            try { profile.UnlockHero(HeroName); }
            catch { TryInvokeHeroUnlock(profile, HeroName); }

            // Hero_Darius originally inherited a generic melee Hero graph. Older prototype builds could
            // therefore leave Vesper constellation names inside the freshly-created Darius loadout.
            // Keep the native slot counts/unlock progression, but clear only non-Darius star selections
            // so the profile validator never tries to resolve stock-Vesper stars for Hero_Darius.
            DariusConstellationPersistence.RestoreBootBackup(profile);
            RepairDariusConstellationLoadout(profile);
        }

        DariusConstellationPersistence.RestoreBootBackup(profile);
        DariusLog.Info("TRAVELER-PROFILE", "Targeted profile entries completed without recursive whole-profile validation.");
    }


    private static void RepairDariusConstellationLoadout(DewProfile profile)
    {
        if (profile == null || profile.heroLoadouts == null) return;
        try
        {
            List<HeroLoadoutData> loadouts;
            if (!profile.heroLoadouts.TryGetValue(HeroName, out loadouts) || loadouts == null) return;

            int removed = 0;
            for (int page = 0; page < loadouts.Count; page++)
            {
                HeroLoadoutData loadout = loadouts[page];
                if (loadout == null) continue;
                removed += ClearForeignStars(loadout.cDestruction);
                removed += ClearForeignStars(loadout.cLife);
                removed += ClearForeignStars(loadout.cImagination);
                removed += ClearForeignStars(loadout.cFlexible);
                MigrateSpecialStarsToFlexible(loadout);
                MigrateRuneStarsToImagination(loadout);
            }
            if (removed > 0)
                DariusLog.Info("CONSTELLATION-PROFILE", "Cleared " + removed + " stale non-Darius constellation selections across " + loadouts.Count + " loadout pages while preserving native slot progression.");
        }
        catch (Exception e)
        {
            DariusLog.Exception("CONSTELLATION-PROFILE", e, "Could not repair Hero_Darius constellation loadout");
        }
    }

    private static void MigrateSpecialStarsToFlexible(HeroLoadoutData loadout)
    {
        if (loadout == null) return;
        if (loadout.cFlexible == null) loadout.cFlexible = new List<LoadoutStarItem>();

        int maxFlexible = loadout.cFlexible.Count;
        try
        {
            if (HeroPrefab != null) maxFlexible = Mathf.Max(maxFlexible, HeroPrefab.cFlexible.maxCount);
        }
        catch { }
        while (loadout.cFlexible.Count < maxFlexible) loadout.cFlexible.Add(default(LoadoutStarItem));

        int moved = 0;
        moved += MoveSelectedStarsToFlexible(loadout.cImagination, loadout.cFlexible, new[]
        {
            DariusConstellationIds.ICripplingStrike,
            DariusConstellationIds.IApprehend,
            DariusConstellationIds.IInstantDecimate,
            DariusConstellationIds.IWarFervor,
            DariusConstellationIds.FCosmicInsight
        }, "Imagination");

        // rc9e: every constellation that directly modifies a Darius skill belongs to Flexible.
        // Axiom Arcanist modifies R; Revitalize modifies Q healing. Preserve any already-equipped
        // selection/level while migrating profiles created before the branch change.
        moved += MoveSelectedStarsToFlexible(loadout.cDestruction, loadout.cFlexible, new[]
        {
            DariusConstellationIds.DAxiomArcanist
        }, "Destruction");
        moved += MoveSelectedStarsToFlexible(loadout.cLife, loadout.cFlexible, new[]
        {
            DariusConstellationIds.LRevitalize
        }, "Life");

        if (moved > 0)
            DariusLog.Info("CONSTELLATION-MIGRATE", "Moved " + moved + " direct-skill Darius stars into Flexible while preserving star levels where a Flexible slot was available.");
    }

    private static int MoveSelectedStarsToFlexible(List<LoadoutStarItem> source, List<LoadoutStarItem> flexible, string[] movedIds, string sourceLabel)
    {
        if (source == null || flexible == null || movedIds == null) return 0;
        int moved = 0;
        for (int i = 0; i < source.Count; i++)
        {
            LoadoutStarItem item = source[i];
            if (string.IsNullOrEmpty(item.name) || Array.IndexOf(movedIds, item.name) < 0) continue;

            bool alreadyPresent = false;
            for (int j = 0; j < flexible.Count; j++)
            {
                if (string.Equals(flexible[j].name, item.name, StringComparison.Ordinal))
                {
                    alreadyPresent = true;
                    break;
                }
            }

            if (!alreadyPresent)
            {
                int empty = -1;
                for (int j = 0; j < flexible.Count; j++)
                {
                    if (string.IsNullOrEmpty(flexible[j].name))
                    {
                        empty = j;
                        break;
                    }
                }
                if (empty < 0)
                {
                    DariusLog.Warn("CONSTELLATION-MIGRATE", "No free Flexible slot for migrated star=" + item.name +
                        " from=" + sourceLabel + "; leaving it unequipped rather than replacing another star.");
                    source[i] = default(LoadoutStarItem);
                    continue;
                }
                flexible[empty] = item;
            }

            source[i] = default(LoadoutStarItem);
            moved++;
        }
        return moved;
    }

    private static void MigrateRuneStarsToImagination(HeroLoadoutData loadout)
    {
        if (loadout == null || loadout.cFlexible == null) return;
        if (loadout.cImagination == null) loadout.cImagination = new List<LoadoutStarItem>();

        string[] movedIds =
        {
            DariusConstellationIds.FNimbusCloak,
            DariusConstellationIds.FCelerity,
            DariusConstellationIds.FGatheringStorm
        };
        int maxImagination = loadout.cImagination.Count;
        try
        {
            if (HeroPrefab != null) maxImagination = Mathf.Max(maxImagination, HeroPrefab.cImagination.maxCount);
        }
        catch { }
        while (loadout.cImagination.Count < maxImagination) loadout.cImagination.Add(default(LoadoutStarItem));

        int moved = 0;
        for (int i = 0; i < loadout.cFlexible.Count; i++)
        {
            LoadoutStarItem item = loadout.cFlexible[i];
            if (string.IsNullOrEmpty(item.name) || Array.IndexOf(movedIds, item.name) < 0) continue;

            bool alreadyPresent = false;
            for (int j = 0; j < loadout.cImagination.Count; j++)
                if (string.Equals(loadout.cImagination[j].name, item.name, StringComparison.Ordinal)) { alreadyPresent = true; break; }

            if (!alreadyPresent)
            {
                int empty = -1;
                for (int j = 0; j < loadout.cImagination.Count; j++)
                    if (string.IsNullOrEmpty(loadout.cImagination[j].name)) { empty = j; break; }
                if (empty < 0)
                {
                    DariusLog.Warn("CONSTELLATION-MIGRATE", "No free Imagination slot for legacy rune star=" + item.name + "; unequipping it rather than replacing another selected star.");
                    loadout.cFlexible[i] = default(LoadoutStarItem);
                    continue;
                }
                loadout.cImagination[empty] = item;
            }
            loadout.cFlexible[i] = default(LoadoutStarItem);
            moved++;
        }
        if (moved > 0) DariusLog.Info("CONSTELLATION-MIGRATE", "Moved " + moved + " legacy rune stars from Flexible to Imagination while preserving star levels.");
    }

    private static int ClearForeignStars(List<LoadoutStarItem> stars)
    {
        if (stars == null) return 0;
        int removed = 0;
        for (int i = 0; i < stars.Count; i++)
        {
            LoadoutStarItem item = stars[i];
            if (string.IsNullOrEmpty(item.name) || item.name.StartsWith("Se_Star_Darius_", StringComparison.Ordinal)) continue;

            // Keep stock/global stars (heroType == null). Only remove invalid leftovers or
            // character-specific stars belonging to a different Hero, especially the Vesper
            // entries inherited by pre-constellation Darius prototype loadouts.
            bool clear = item.name.StartsWith("Se_Star_Vesper_", StringComparison.Ordinal);
            if (!clear)
            {
                try
                {
                    StarEffect prefab = DewResources.GetByShortTypeName<StarEffect>(item.name);
                    clear = prefab == null || (prefab.heroType != null && prefab.heroType != typeof(Hero_Darius));
                }
                catch { clear = true; }
            }
            if (!clear) continue;
            stars[i] = default(LoadoutStarItem);
            removed++;
        }
        return removed;
    }


    private static void RepairLegacySelectedSkinAliases(DewProfile profile, string reason)
    {
        if (profile == null || profile.heroSelectedSkins == null) return;
        int migrated = 0;
        List<string> keys = new List<string>(profile.heroSelectedSkins.Keys);
        for (int i = 0; i < keys.Count; i++)
        {
            string key = keys[i];
            string value = null;
            if (!profile.heroSelectedSkins.TryGetValue(key, out value)) continue;
            if (string.Equals(value, LegacySkinAlias, StringComparison.Ordinal))
            {
                profile.heroSelectedSkins[key] = DefaultSkinName;
                migrated++;
            }
        }

        string previousSkin = null;
        profile.heroSelectedSkins.TryGetValue(HeroName, out previousSkin);
        bool validDariusSkin = FindSkinSpec(previousSkin) != null;
        if (!validDariusSkin)
        {
            profile.heroSelectedSkins[HeroName] = DefaultSkinName;
            migrated++;
        }
        if (migrated > 0)
            DariusLog.Info("TRAVELER-PROFILE", "Migrated stale/foreign selected-skin aliases to a valid Darius skin count=" + migrated + " reason=" + reason);
    }


    private static void ValidateSkinModelBinding(Skin skin, DariusSkinSpec spec, List<string> errors)
    {
        if (skin == null || spec == null) return;
        DariusSkinModelBinding binding = skin.GetComponent<DariusSkinModelBinding>();
        if (binding == null) { errors.Add(skin.name + " model binding missing"); return; }
        if (!string.Equals(binding.modelFile, spec.modelFile, StringComparison.OrdinalIgnoreCase)) errors.Add(skin.name + " model binding wrong: " + binding.modelFile + " expected=" + spec.modelFile);
        if (!string.Equals(binding.displayName, spec.displayName, StringComparison.Ordinal)) errors.Add(skin.name + " display binding wrong");
        if (!string.Equals(binding.variantKey, spec.variantKey, StringComparison.Ordinal)) errors.Add(skin.name + " variant profile wrong");
        if (binding.modelScale <= 0f) errors.Add(skin.name + " modelScale invalid");
        string[] required = { binding.idleClip, binding.runClip, binding.deathClip, binding.attack1Clip, binding.attack2Clip, binding.qClip, binding.wClip, binding.eClip, binding.rClip };
        for (int i = 0; i < required.Length; i++) if (string.IsNullOrEmpty(required[i])) errors.Add(skin.name + " core animation binding missing index=" + i);
    }


    private static void ValidateRegistration()
    {
        List<string> errors = new List<string>();
        if (HeroPrefab == null) errors.Add("HeroPrefab null");
        if (DefaultSkin == null) errors.Add("DefaultSkin null");
        if (GodKingSkin == null) errors.Add("GodKingSkin null");
        if (AttackPrefab == null) errors.Add("AttackPrefab null");
        if (AttackInstancePrefab == null) errors.Add("AttackInstancePrefab null");
        if (AttackCritInstancePrefab == null) errors.Add("AttackCritInstancePrefab null");
        if (!ResourcesByGuid.ContainsKey(HeroGuid)) errors.Add("Hero GUID bridge missing");
        if (!ResourcesByGuid.ContainsKey(SkinGuid)) errors.Add("Skin GUID bridge missing");
        if (!ResourcesByGuid.ContainsKey(AttackGuid)) errors.Add("Darius attack GUID bridge missing");
        if (!ResourcesByGuid.ContainsKey(AttackInstanceGuid)) errors.Add("Darius attack instance GUID bridge missing");
        if (!ResourcesByGuid.ContainsKey(AttackCritInstanceGuid)) errors.Add("Darius crit attack instance GUID bridge missing");
        if (HeroPrefab != null)
        {
            GameObject go = HeroPrefab.gameObject;
            string[] required = { "EntityAI", "EntityAbility", "EntityStatus", "EntityAnimation", "EntityControl", "EntityVisual", "EntitySound", "HeroSkill", "NetworkIdentity" };
            foreach (string typeName in required)
            {
                bool found = go.GetComponents<Component>().Any(c => c != null && c.GetType().Name == typeName);
                if (!found) errors.Add("Hero component missing " + typeName);
            }
            EntityAbility ea = go.GetComponent<EntityAbility>();
            if (ea == null) errors.Add("EntityAbility missing");
            else
            {
                try
                {
                    AttackTrigger preset = ea.attackAbilityPreset.asset;
                    if (preset == null || preset.GetType() != typeof(At_DariusAxe))
                    {
                        DariusNativeAttackBinder binder = go.GetComponent<DariusNativeAttackBinder>();
                        if (binder != null) binder.EnsureBound("ValidateRegistration deferred AssetRef resolution");
                        DariusLog.Warn("TRAVELER-ASSERT", "attackAbilityPreset AssetRef is not resolvable yet; deferring until runtime resource hooks are installed.");
                    }
                }
                catch (Exception e) { errors.Add("attackAbilityPreset resolve threw " + e.GetType().Name); }
            }
            HeroSkill hs = go.GetComponent<HeroSkill>();
            if (hs == null || GetArrayLength(hs, "loadoutQ") < 1) errors.Add("loadoutQ empty");
            if (hs == null || GetArrayLength(hs, "loadoutR") < 1) errors.Add("loadoutR empty");
            if (hs == null || GetArrayLength(hs, "loadoutTrait") < 1) errors.Add("loadoutTrait empty");
            if (hs == null || GetArrayLength(hs, "loadoutMovement") < 2) errors.Add("loadoutMovement does not expose both Flash/Ghost");
        }
        for (int si = 0; si < SkinSpecs.Length; si++)
        {
            DariusSkinSpec spec = SkinSpecs[si]; Skin skin;
            if (!SkinsByName.TryGetValue(spec.name, out skin) || skin == null) { errors.Add(spec.name + " skin missing"); continue; }
            if (skin.GetComponent<EntityModel>() == null) errors.Add(spec.name + " EntityModel missing");
            ValidateSkinModelBinding(skin, spec, errors);
            try { if (!skin.IsValidFor(HeroName)) errors.Add(spec.name + ".IsValidFor(Hero_Darius)=false"); }
            catch (Exception e) { errors.Add(spec.name + ".IsValidFor threw " + e.GetType().Name); }
        }
        try
        {
            string mappedGuid;
            if (!DewResources.database.netObjectAssetIdToGuid.TryGetValue(HeroAssetId, out mappedGuid) || mappedGuid != HeroGuid)
                errors.Add("Hero Mirror assetId map missing/wrong");
            if (!DewResources.database.netObjectAssetIdToGuid.TryGetValue(AttackAssetId, out mappedGuid) || mappedGuid != AttackGuid)
                errors.Add("At_DariusAxe Mirror assetId map missing/wrong");
            if (!DewResources.database.netObjectAssetIdToGuid.TryGetValue(AttackInstanceAssetId, out mappedGuid) || mappedGuid != AttackInstanceGuid)
                errors.Add("Ai_DariusAxe Mirror assetId map missing/wrong");
            if (!DewResources.database.netObjectAssetIdToGuid.TryGetValue(AttackCritInstanceAssetId, out mappedGuid) || mappedGuid != AttackCritInstanceGuid)
                errors.Add("Ai_DariusAxe_Crit Mirror assetId map missing/wrong");
        }
        catch { errors.Add("Hero/attack Mirror assetId maps unreadable"); }
        try
        {
            if (!Dew.allHeroes.Contains(typeof(Hero_Darius))) errors.Add("Dew.allHeroes missing Hero_Darius");
        }
        catch { errors.Add("Dew.allHeroes unavailable"); }
        DewProfile p = DewSave.profileMain;
        if (p != null)
        {
            if (p.heroes == null || !p.heroes.ContainsKey(HeroName)) errors.Add("profile.heroes missing Hero_Darius");
            if (p.heroLoadouts == null || !p.heroLoadouts.ContainsKey(HeroName)) errors.Add("profile.heroLoadouts missing Hero_Darius");
            if (p.heroSelectedSkins == null || !p.heroSelectedSkins.ContainsKey(HeroName)) errors.Add("profile.heroSelectedSkins missing Hero_Darius");
        }

        if (errors.Count > 0)
        {
            string message = string.Join("; ", errors.ToArray());
            DariusLog.Error("TRAVELER-ASSERT", "Startup assertions FAILED: " + message);
            throw new InvalidOperationException(message);
        }
        DariusLog.Info("TRAVELER-ASSERT", "Startup assertions passed for Hero/Skin/native-attack/Loadout/resource contract.");
    }

    public static bool TryLoad(string key, out UnityEngine.Object obj)
    {
        obj = null;
        if (string.IsNullOrEmpty(key)) return false;
        if (ResourcesByGuid.TryGetValue(key, out obj) && obj != null) return true;
        if (key == HeroName && HeroPrefab != null) { obj = HeroPrefab; return true; }
        if (key == DefaultSkinName && DefaultSkin != null) { obj = DefaultSkin; return true; }
        Skin skinByName;
        if (SkinsByName.TryGetValue(key, out skinByName) && skinByName != null) { obj = skinByName; return true; }
        if (key == AttackName && AttackPrefab != null) { obj = AttackPrefab; return true; }
        if (key == AttackInstanceName && AttackInstancePrefab != null) { obj = AttackInstancePrefab; return true; }
        if (key == AttackCritInstanceName && AttackCritInstancePrefab != null) { obj = AttackCritInstancePrefab; return true; }
        return false;
    }

    public static bool TryGetByType(Type type, out UnityEngine.Object obj)
    {
        obj = null;
        return type != null && ResourcesByType.TryGetValue(type, out obj) && obj != null;
    }

    public static bool TryGetNetworkPrefab(uint assetId, out GameObject prefab)
    {
        return NetworkPrefabs.TryGetValue(assetId, out prefab) && prefab != null;
    }

    public static bool IsRuntimeKey(string key)
    {
        if (string.IsNullOrEmpty(key)) return false;
        if (key == HeroGuid || key == HeroName) return true;
        if (FindSkinSpec(key) != null) return true;
        return key == AttackGuid || key == AttackName ||
               key == AttackInstanceGuid || key == AttackInstanceName ||
               key == AttackCritInstanceGuid || key == AttackCritInstanceName;
    }


    public static void RefreshHeroPresentationFast()
    {
        if (!_registered || HeroPrefab == null || !AreSkinResourcesReady()) return;
        try
        {
            Sprite portrait = DariusPrototypeIcons.Get("HERO");
            if (portrait != null) TryAssignIfCompatible(HeroPrefab, "icon", portrait);
            TryAssignIfCompatible(HeroPrefab, "mainColor", new Color(0.36f, 0.075f, 0.055f, 1f));
            for (int i = 0; i < SkinSpecs.Length; i++)
            {
                Skin skin;
                if (!SkinsByName.TryGetValue(SkinSpecs[i].name, out skin) || skin == null) continue;
                Sprite preview = DariusPrototypeIcons.Get(SkinSpecs[i].previewIconKey);
                if (preview != null) TryAssignIfCompatible(skin, "previewImage", preview);
            }
            RepairHeroCosmeticContract(HeroPrefab);
            ValidateNativeCosmeticIconContract("fast-refresh");
        }
        catch (Exception e)
        {
            DariusLog.Exception("HERO-PRESENTATION", e, "Fast Hero_Darius presentation refresh failed");
        }
    }

    private static void ValidateNativeCosmeticIconContract(string reason)
    {
        try
        {
            object heroIcon = HeroPrefab != null ? ReadMemberValue(HeroPrefab, "icon") : null;
            object mainColor = HeroPrefab != null ? ReadMemberValue(HeroPrefab, "mainColor") : null;
            int ready = 0;
            for (int i = 0; i < SkinSpecs.Length; i++)
            {
                Skin skin;
                object preview = null;
                if (SkinsByName.TryGetValue(SkinSpecs[i].name, out skin) && skin != null)
                    preview = ReadMemberValue(skin, "previewImage");
                if (preview != null) ready++;
                else DariusLog.Error("SKIN-ICON", "Native previewImage missing skin=" + SkinSpecs[i].name + " reason=" + reason);
            }
            DariusLog.Info("HERO-ICON-CONTRACT", "reason=" + reason + " hero.icon=" + (heroIcon != null) +
                " mainColor=" + (mainColor != null) + " skin.previewImage=" + ready + "/" + SkinSpecs.Length);
        }
        catch (Exception e)
        {
            DariusLog.Exception("HERO-ICON-CONTRACT", e, "Native cosmetic icon validation failed reason=" + reason);
        }
    }

    public static void RepairRuntimeRegistration(string reason)
    {
        if (_repairing || !_registered || DewResources.database == null) return;
        _repairing = true;
        try
        {
            // A completed run can destroy the runtime Hero prefab itself while leaving the Skin,
            // formal Skills and this static registry alive. In that state merely re-inserting the
            // old reference into Dew maps cannot work because Unity destroyed objects compare null.
            // Recreate any missing runtime prefab first, then rebuild every lookup bridge.
            EnsurePersistentRuntimeObjects(reason ?? "runtime repair");
            if (HeroPrefab == null || DefaultSkin == null)
            {
                DariusLog.Error("TRAVELER-SELFHEAL", "Runtime repair could not restore required Hero/Skin resources reason=" + (reason ?? "<unknown>"));
                return;
            }

            // Dew/BuildProfile caches and parts of the runtime database are rebuilt when leaving a
            // run. Reassert Hero/Skin/type/content/network/profile paths after every rebuild beat.
            RegisterTypes();
            RegisterContent(DewBuildProfile.current != null ? DewBuildProfile.current.content : null);

            Type heroType = typeof(Hero_Darius);
            string aqn = heroType.AssemblyQualifiedName;
            DewResources.database.typeAssemblyQualifiedNameToGuid[aqn] = HeroGuid;
            if (!DewResources.database.allGuids.Contains(HeroGuid)) DewResources.database.allGuids.Add(HeroGuid);
            for (int si = 0; si < SkinSpecs.Length; si++)
                if (!DewResources.database.allGuids.Contains(SkinSpecs[si].guid)) DewResources.database.allGuids.Add(SkinSpecs[si].guid);
            SetDatabaseMap("typeToGuid", heroType, HeroGuid);
            SetDatabaseMap("guidToType", HeroGuid, heroType);
            SetDatabaseMap("typeNameToGuid", HeroName, HeroGuid);
            SetDatabaseMap("typeNameToType", HeroName, heroType);
            SetDatabaseMap("nameToGuid", HeroName, HeroGuid);
            SetDatabaseMap("guidToName", HeroGuid, HeroName);
            SetDatabaseMap("objectToGuidFallback", HeroPrefab, HeroGuid);
            SetDatabaseMap("objectToGuidFallback", HeroPrefab.gameObject, HeroGuid);

            for (int si = 0; si < SkinSpecs.Length; si++)
            {
                DariusSkinSpec spec = SkinSpecs[si]; Skin skin;
                if (!SkinsByName.TryGetValue(spec.name, out skin) || skin == null) continue;
                SetDatabaseMap("nameToGuid", spec.name, spec.guid);
                SetDatabaseMap("guidToName", spec.guid, spec.name);
                SetDatabaseMap("objectToGuidFallback", skin, spec.guid);
                SetDatabaseMap("objectToGuidFallback", skin.gameObject, spec.guid);
                ResourcesByGuid[spec.guid] = skin;
            }

            ResourcesByGuid[HeroGuid] = HeroPrefab;
            ResourcesByType[heroType] = HeroPrefab;
            DewResources.database.netObjectAssetIdToGuid[HeroAssetId] = HeroGuid;
            NetworkPrefabs[HeroAssetId] = HeroPrefab.gameObject;
            try { NetworkClient.RegisterSpawnHandler(HeroAssetId, SpawnHandler, UnspawnHandler); } catch { }

            ReassertTypedRuntimeResource(AttackPrefab, AttackName, AttackGuid, AttackAssetId);
            ReassertTypedRuntimeResource(AttackInstancePrefab, AttackInstanceName, AttackInstanceGuid, AttackInstanceAssetId);
            ReassertTypedRuntimeResource(AttackCritInstancePrefab, AttackCritInstanceName, AttackCritInstanceGuid, AttackCritInstanceAssetId);

            DewProfile profile = DewSave.profileMain;
            RepairLegacySelectedSkinAliases(profile, reason ?? "runtime repair");
            DariusNativeAttackBinder prefabBinder = HeroPrefab != null ? HeroPrefab.GetComponent<DariusNativeAttackBinder>() : null;
            if (prefabBinder != null) prefabBinder.EnsureBound("RepairRuntimeRegistration");

            if (_presentationGenerationRebuilt)
            {
                // Existing lobby/reward CharacterModelDisplay instances can already hold AssetRefs
                // to the destroyed generation. Re-run Dew's authoritative repair transaction only
                // when we actually replaced the presentation generation.
                NotifyDewMissingReferenceRepair();
                _presentationGenerationRebuilt = false;
            }

            DariusLog.DebugInfoThrottled("TRAVELER-REPAIR", reason ?? "<unknown>", "Reasserted Hero_Darius + Darius native attack resource/type/network/profile bridges reason=" + (reason ?? "<unknown>"), 15.0);
        }
        catch (Exception e)
        {
            DariusLog.Exception("TRAVELER-REPAIR", e, "Runtime registration repair failed reason=" + (reason ?? "<unknown>"));
        }
        finally
        {
            _repairing = false;
        }
    }

    private static void RebuildSkinPresentationGeneration(string reason)
    {
        List<GameObject> stale = new List<GameObject>();
        foreach (Skin skin in SkinsByName.Values)
        {
            if (skin != null && skin.gameObject != null && !stale.Contains(skin.gameObject)) stale.Add(skin.gameObject);
        }
        SkinsByName.Clear();
        DefaultSkin = null; GodKingSkin = null; DunkmasterSkin = null; MechaSkin = null;
        for (int i = 0; i < stale.Count; i++)
        {
            GameObject go = stale[i];
            OwnedObjects.Remove(go);
            if (go != null)
            {
                try { UnityEngine.Object.Destroy(go); } catch { }
            }
        }
        CreateAndRegisterSkin();
        _presentationGenerationRebuilt = true;
        DariusLog.Warn("TRAVELER-SELFHEAL", "Rebuilt complete Skin/EntityModel presentation generation after teardown reason=" + reason + " stale=" + stale.Count);
    }

    private static void EnsurePersistentRuntimeObjects(string reason)
    {
        // Remove Unity-destroyed entries from our ownership list so later application-quit cleanup
        // only walks live objects. UnityEngine.Object's overloaded == makes destroyed GameObjects
        // compare null even though the managed wrapper still exists.
        OwnedObjects.RemoveAll(go => go == null);
        CreateResourceRoot();
        ConfigureDariusSkillOwnership();

        bool rebuiltAttack = false;
        bool rebuiltSkin = false;
        bool rebuiltHero = false;

        // Hero construction depends on the Darius attack preset. Rebuild the complete attack chain
        // only if a teardown unexpectedly removed any of it; the normal first-run bug destroys only
        // Hero_Darius, so this branch should ordinarily remain untouched.
        if (AttackPrefab == null || AttackInstancePrefab == null || AttackCritInstancePrefab == null)
        {
            DariusLog.Warn("TRAVELER-SELFHEAL", "Darius native attack runtime prefab missing; rebuilding attack chain reason=" + reason);
            CreateAndRegisterNativeBasicAttack();
            rebuiltAttack = true;
        }

        bool heroPresentationWasDestroyed = HeroPrefab == null;
        if (heroPresentationWasDestroyed)
        {
            // The observed run teardown destroys Hero_* and invalidates the Skin -> EntityModel
            // presentation generation in the same transition even when Skin wrappers still compare
            // non-null. Rebuild the whole generation transactionally before constructing the Hero.
            DariusLog.Warn("TRAVELER-SELFHEAL", "Hero_Darius runtime prefab was destroyed; invalidating Skin presentation generation reason=" + reason);
            RebuildSkinPresentationGeneration(reason);
            rebuiltSkin = true;
        }
        else if (!AreSkinResourcesReady())
        {
            DariusLog.Warn("TRAVELER-SELFHEAL", "One or more Darius Skin/EntityModel presentation resources are stale; rebuilding full skin generation reason=" + reason);
            RebuildSkinPresentationGeneration(reason);
            rebuiltSkin = true;
            RepairHeroCosmeticContract(HeroPrefab);
        }

        if (HeroPrefab == null)
        {
            DariusLog.Warn("TRAVELER-SELFHEAL", "Hero_Darius runtime prefab was destroyed by run teardown; rebuilding from native structural template reason=" + reason);
            CreateAndRegisterHero();
            rebuiltHero = true;
        }

        if (rebuiltAttack || rebuiltSkin || rebuiltHero)
        {
            CreateLifecycleBridge();
            DariusLog.Info("TRAVELER-SELFHEAL", "Rebuilt runtime resources hero=" + rebuiltHero +
                " skin=" + rebuiltSkin + " attack=" + rebuiltAttack +
                " heroInstanceId=" + (HeroPrefab != null ? HeroPrefab.GetInstanceID().ToString() : "<null>") +
                " reason=" + reason);
        }
    }

    private static void ReassertTypedRuntimeResource(Component component, string name, string guid, uint assetId)
    {
        if (component == null || DewResources.database == null) return;
        Type type = component.GetType();
        string aqn = type.AssemblyQualifiedName;
        DewResources.database.typeAssemblyQualifiedNameToGuid[aqn] = guid;
        if (!DewResources.database.allGuids.Contains(guid)) DewResources.database.allGuids.Add(guid);
        SetDatabaseMap("typeToGuid", type, guid);
        SetDatabaseMap("guidToType", guid, type);
        SetDatabaseMap("typeNameToGuid", type.Name, guid);
        SetDatabaseMap("typeNameToType", type.Name, type);
        SetDatabaseMap("nameToGuid", name, guid);
        SetDatabaseMap("guidToName", guid, name);
        SetDatabaseMap("objectToGuidFallback", component, guid);
        SetDatabaseMap("objectToGuidFallback", component.gameObject, guid);
        ResourcesByGuid[guid] = component;
        ResourcesByType[type] = component;
        DewResources.database.netObjectAssetIdToGuid[assetId] = guid;
        NetworkPrefabs[assetId] = component.gameObject;
        try { NetworkClient.RegisterSpawnHandler(assetId, SpawnHandler, UnspawnHandler); } catch { }
    }

    private static string RuntimeGuidForAssetId(uint assetId)
    {
        if (assetId == HeroAssetId) return HeroGuid;
        if (assetId == AttackAssetId) return AttackGuid;
        if (assetId == AttackInstanceAssetId) return AttackInstanceGuid;
        if (assetId == AttackCritInstanceAssetId) return AttackCritInstanceGuid;
        return null;
    }

    public static void UnregisterRuntimeOnly()
    {
        _registered = false;
        _registering = false;
        foreach (uint id in NetworkPrefabs.Keys.ToArray())
        {
            try { NetworkClient.UnregisterSpawnHandler(id); } catch { }
            try
            {
                string expected = RuntimeGuidForAssetId(id);
                string existing;
                if (!string.IsNullOrEmpty(expected) && DewResources.database != null &&
                    DewResources.database.netObjectAssetIdToGuid.TryGetValue(id, out existing) && existing == expected)
                    DewResources.database.netObjectAssetIdToGuid.Remove(id);
            }
            catch { }
        }
        NetworkPrefabs.Clear();

        RemoveTypeFromDew("_allHeroes", typeof(Hero_Darius));
        RemoveTypeFromDew("_allSkills", typeof(St_Darius_Decimate));
        RemoveTypeFromDew("_allSkills", typeof(St_Darius_NoxianGuillotine));
        RemoveTypeFromDew("_allSkills", typeof(St_D_Darius_Hemorrhage));
        RemoveTypeFromDew("_allSkills", typeof(St_Darius_CripplingStrike));
        RemoveTypeFromDew("_allSkills", typeof(St_Darius_Apprehend));
        RemoveTypeFromDew("_allSkills", typeof(St_Darius_Flash));
        RemoveTypeFromDew("_allSkills", typeof(St_Darius_Ghost));
        RemoveTypeFromDew("_allHeroSkills", typeof(St_Darius_Decimate));
        RemoveTypeFromDew("_allHeroSkills", typeof(St_Darius_NoxianGuillotine));
        RemoveTypeFromDew("_allHeroSkills", typeof(St_D_Darius_Hemorrhage));
        RemoveTypeFromDew("_allHeroSkills", typeof(St_Darius_Flash));
        RemoveTypeFromDew("_allHeroSkills", typeof(St_Darius_Ghost));

        // Only remove maps where our exact value is still installed. User profile data is intentionally preserved.
        for (int si = 0; si < SkinSpecs.Length; si++)
        {
            RemoveDatabaseMappingIfOwned("nameToGuid", SkinSpecs[si].name, SkinSpecs[si].guid);
            RemoveDatabaseMappingIfOwned("guidToName", SkinSpecs[si].guid, SkinSpecs[si].name);
        }
        RemoveTypedMappings(typeof(Hero_Darius), HeroGuid, HeroName);
        RemoveTypedMappings(typeof(At_DariusAxe), AttackGuid, AttackName);
        RemoveTypedMappings(typeof(Ai_DariusAxe), AttackInstanceGuid, AttackInstanceName);
        RemoveTypedMappings(typeof(Ai_DariusAxe_Crit), AttackCritInstanceGuid, AttackCritInstanceName);
        List<string> runtimeGuids = new List<string> { HeroGuid, AttackGuid, AttackInstanceGuid, AttackCritInstanceGuid };
        for (int si = 0; si < SkinSpecs.Length; si++) runtimeGuids.Add(SkinSpecs[si].guid);
        for (int i = 0; i < runtimeGuids.Count; i++)
        {
            try { if (DewResources.database != null && DewResources.database.allGuids.Contains(runtimeGuids[i])) DewResources.database.allGuids.Remove(runtimeGuids[i]); } catch { }
        }

        foreach (GameObject go in OwnedObjects)
            if (go != null) UnityEngine.Object.Destroy(go);
        OwnedObjects.Clear();
        ResourcesByGuid.Clear();
        ResourcesByType.Clear();
        HeroPrefab = null;
        DefaultSkin = null;
        GodKingSkin = null;
        DunkmasterSkin = null;
        MechaSkin = null;
        SkinsByName.Clear();
        AttackPrefab = null;
        AttackInstancePrefab = null;
        AttackCritInstancePrefab = null;
        if (_resourceRoot != null) UnityEngine.Object.Destroy(_resourceRoot);
        _resourceRoot = null;
        DariusLog.Info("TRAVELER", "Runtime Hero_Darius resources unregistered; persistent profile data left untouched.");
    }

    private static void RegisterTypedResource(Component component, GameObject go, string name, string guid, uint assetId)
    {
        Type type = component.GetType();
        object db = DewResources.database;
        if (db == null) throw new InvalidOperationException("database null");
        string aqn = type.AssemblyQualifiedName;
        DewResources.database.typeAssemblyQualifiedNameToGuid[aqn] = guid;
        if (!DewResources.database.allGuids.Contains(guid)) DewResources.database.allGuids.Add(guid);
        SetDatabaseMap("typeToGuid", type, guid);
        SetDatabaseMap("guidToType", guid, type);
        SetDatabaseMap("typeNameToGuid", type.Name, guid);
        SetDatabaseMap("typeNameToType", type.Name, type);
        SetDatabaseMap("nameToGuid", name, guid);
        SetDatabaseMap("guidToName", guid, name);
        SetDatabaseMap("objectToGuidFallback", component, guid);
        SetDatabaseMap("objectToGuidFallback", go, guid);
        ResourcesByGuid[guid] = component;
        ResourcesByType[type] = component;

        string collision;
        if (DewResources.database.netObjectAssetIdToGuid.TryGetValue(assetId, out collision) && collision != guid)
            throw new InvalidOperationException("Mirror assetId collision: " + assetId + " already maps to " + collision);
        DewResources.database.netObjectAssetIdToGuid[assetId] = guid;
        NetworkPrefabs[assetId] = go;
        try { NetworkClient.RegisterSpawnHandler(assetId, SpawnHandler, UnspawnHandler); }
        catch (Exception e) { DariusLog.Exception("TRAVELER-NET", e, "RegisterSpawnHandler failed assetId=" + assetId); }
    }

    private static void RegisterNamedResource(Component component, GameObject go, string name, string guid)
    {
        if (!DewResources.database.allGuids.Contains(guid)) DewResources.database.allGuids.Add(guid);
        SetDatabaseMap("nameToGuid", name, guid);
        SetDatabaseMap("guidToName", guid, name);
        SetDatabaseMap("objectToGuidFallback", component, guid);
        SetDatabaseMap("objectToGuidFallback", go, guid);
        ResourcesByGuid[guid] = component;
        // Deliberately do NOT write typeToGuid[typeof(Skin)]: Skin is a shared stock type.
    }

    private static GameObject SpawnHandler(SpawnMessage msg)
    {
        GameObject prefab;
        if (!NetworkPrefabs.TryGetValue(msg.assetId, out prefab) || prefab == null) return null;
        try
        {
            GameObject spawned = UnityEngine.Object.Instantiate(prefab, msg.position, msg.rotation);
            spawned.name = prefab.name;
            spawned.transform.localScale = msg.scale;
            // Mirror owns the live NetworkIdentity spawn state. The old handler rewrote assetId/scene state
            // and rebuilt NetworkBehaviours after Instantiate(), which can corrupt Host-client spawn
            // bookkeeping and disconnect the local client on a basic attack. A custom spawn handler
            // should only construct and return the object; Mirror applies the SpawnMessage afterwards.
            if (!spawned.activeSelf) spawned.SetActive(true);
            DariusLog.Info("TRAVELER-NET", "Client spawned " + prefab.name + " assetId=" + msg.assetId);
            return spawned;
        }
        catch (Exception e)
        {
            DariusLog.Exception("TRAVELER-NET", e, "Hero_Darius spawn handler failed assetId=" + msg.assetId);
            return null;
        }
    }

    private static void UnspawnHandler(GameObject spawned)
    {
        if (spawned != null) SpawnManager.Destroy(spawned);
    }

    private static void ConfigureNetworkIdentity(NetworkIdentity identity, uint assetId)
    {
        if (identity == null) return;
        FieldInfo field = typeof(NetworkIdentity).GetField("_assetId", BindingFlags.Instance | BindingFlags.NonPublic)
                       ?? typeof(NetworkIdentity).GetField("assetId", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                       ?? typeof(NetworkIdentity).GetField("<assetId>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
        if (field != null) field.SetValue(identity, assetId);
        else
        {
            PropertyInfo p = typeof(NetworkIdentity).GetProperty("assetId", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (p != null && p.CanWrite) p.SetValue(identity, assetId, null);
        }
        identity.sceneId = 0UL;
        FieldInfo scene = typeof(NetworkIdentity).GetField("_isSceneObject", BindingFlags.Instance | BindingFlags.NonPublic);
        if (scene != null) scene.SetValue(identity, false);
        FieldInfo spawned = typeof(NetworkIdentity).GetField("hasSpawned", BindingFlags.Instance | BindingFlags.NonPublic);
        if (spawned != null) spawned.SetValue(identity, false);
    }

    internal static void ReinitializeNetworkBehaviours(NetworkIdentity identity)
    {
        if (identity == null) return;
        try
        {
            MethodInfo m = typeof(NetworkIdentity).GetMethod("InitializeNetworkBehaviours", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (m != null) m.Invoke(identity, null);
        }
        catch (Exception e) { DariusLog.Exception("TRAVELER-NET", e, "InitializeNetworkBehaviours failed for " + identity.name); }
    }

    private static T ResolveGenericResource<T>(string methodName, string key, bool loadLight) where T : UnityEngine.Object
    {
        MethodInfo[] methods = typeof(DewResources).GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
        foreach (MethodInfo raw in methods)
        {
            if (raw.Name != methodName || !raw.IsGenericMethodDefinition) continue;
            MethodInfo m;
            try { m = raw.MakeGenericMethod(typeof(T)); } catch { continue; }
            ParameterInfo[] ps = m.GetParameters();
            if (ps.Length < 1 || ps[0].ParameterType != typeof(string)) continue;
            object[] args = new object[ps.Length];
            args[0] = key;
            for (int i = 1; i < ps.Length; i++)
            {
                if (ps[i].ParameterType == typeof(bool) && ps[i].Name != null && ps[i].Name.IndexOf("light", StringComparison.OrdinalIgnoreCase) >= 0)
                    args[i] = loadLight;
                else if (ps[i].HasDefaultValue) args[i] = ps[i].DefaultValue;
                else args[i] = ps[i].ParameterType.IsValueType ? Activator.CreateInstance(ps[i].ParameterType) : null;
            }
            try
            {
                T result = m.Invoke(null, args) as T;
                if (result != null) return result;
            }
            catch { }
        }
        return null;
    }

    private static Dictionary<FieldInfo, object> CaptureUnitySerializedFields(Component source, Type startType)
    {
        Dictionary<FieldInfo, object> values = new Dictionary<FieldInfo, object>();
        Type t = startType;
        while (t != null && typeof(Component).IsAssignableFrom(t))
        {
            foreach (FieldInfo f in t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            {
                if (f.IsStatic || f.IsInitOnly || f.IsNotSerialized) continue;
                bool serialized = f.IsPublic || f.GetCustomAttributes(typeof(SerializeField), true).Length > 0 || f.GetCustomAttributes(typeof(SerializeReference), true).Length > 0;
                if (!serialized) continue;
                try { values[f] = f.GetValue(source); } catch { }
            }
            t = t.BaseType;
        }
        return values;
    }

    private static void RestoreUnitySerializedFields(Component target, Dictionary<FieldInfo, object> values)
    {
        foreach (KeyValuePair<FieldInfo, object> kv in values)
        {
            try { kv.Key.SetValue(target, kv.Value); }
            catch (Exception e) { DariusLog.DebugInfo("TRAVELER-COPY", "Skip field " + kv.Key.DeclaringType.Name + "." + kv.Key.Name + ": " + e.Message); }
        }
    }

    private static void ReplaceDirectComponentReferences(GameObject root, Component oldComponent, Component newComponent)
    {
        if (root == null || oldComponent == null || newComponent == null) return;
        Component[] components = root.GetComponentsInChildren<Component>(true);
        int replaced = 0;
        for (int ci = 0; ci < components.Length; ci++)
        {
            Component c = components[ci];
            if (c == null || c == oldComponent || c == newComponent) continue;
            Type t = c.GetType();
            while (t != null && typeof(Component).IsAssignableFrom(t))
            {
                FieldInfo[] fields = t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                for (int fi = 0; fi < fields.Length; fi++)
                {
                    FieldInfo f = fields[fi];
                    if (f.IsStatic || f.IsInitOnly) continue;
                    try
                    {
                        object value = f.GetValue(c);
                        if (ReferenceEquals(value, oldComponent))
                        {
                            if (f.FieldType.IsAssignableFrom(newComponent.GetType()) || f.FieldType.IsAssignableFrom(typeof(Hero_Darius)) || f.FieldType == typeof(Hero) || f.FieldType == typeof(Entity) || f.FieldType == typeof(Actor) || f.FieldType == typeof(Component))
                            {
                                f.SetValue(c, newComponent);
                                replaced++;
                            }
                            continue;
                        }
                        Array array = value as Array;
                        if (array != null)
                        {
                            Type elementType = array.GetType().GetElementType();
                            if (elementType != null && elementType.IsAssignableFrom(newComponent.GetType()))
                            {
                                for (int i = 0; i < array.Length; i++)
                                {
                                    if (ReferenceEquals(array.GetValue(i), oldComponent))
                                    {
                                        array.SetValue(newComponent, i);
                                        replaced++;
                                    }
                                }
                            }
                            continue;
                        }

                        IList list = value as IList;
                        if (list != null && !list.IsReadOnly && !list.IsFixedSize)
                        {
                            for (int i = 0; i < list.Count; i++)
                            {
                                if (!ReferenceEquals(list[i], oldComponent)) continue;
                                try { list[i] = newComponent; replaced++; } catch { }
                            }
                        }
                    }
                    catch { }
                }
                t = t.BaseType;
            }
        }
        DariusLog.Info("TRAVELER-COPY", "Rebound direct prefab references " + oldComponent.GetType().Name + " -> " + newComponent.GetType().Name + " count=" + replaced);
    }

    private static int CountDirectComponentReferences(GameObject root, Component oldComponent, Component replacement)
    {
        if (root == null || ReferenceEquals(oldComponent, null)) return 0;
        int count = 0;
        Component[] components = root.GetComponentsInChildren<Component>(true);
        for (int ci = 0; ci < components.Length; ci++)
        {
            Component c = components[ci];
            if (c == null || ReferenceEquals(c, oldComponent) || ReferenceEquals(c, replacement)) continue;
            Type t = c.GetType();
            while (t != null && typeof(Component).IsAssignableFrom(t))
            {
                FieldInfo[] fields = t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                for (int fi = 0; fi < fields.Length; fi++)
                {
                    FieldInfo f = fields[fi];
                    if (f.IsStatic) continue;
                    try
                    {
                        object value = f.GetValue(c);
                        if (ReferenceEquals(value, oldComponent)) { count++; continue; }
                        Array array = value as Array;
                        if (array != null)
                        {
                            for (int i = 0; i < array.Length; i++) if (ReferenceEquals(array.GetValue(i), oldComponent)) count++;
                            continue;
                        }
                        IList list = value as IList;
                        if (list != null)
                        {
                            for (int i = 0; i < list.Count; i++) if (ReferenceEquals(list[i], oldComponent)) count++;
                        }
                    }
                    catch { }
                }
                t = t.BaseType;
            }
        }
        return count;
    }

    private static int GetArrayLength(object owner, string fieldName)
    {
        FieldInfo f = FindFieldRecursive(owner.GetType(), fieldName);
        Array a = f != null ? f.GetValue(owner) as Array : null;
        return a != null ? a.Length : 0;
    }

    private static FieldInfo FindFieldRecursive(Type t, string name)
    {
        while (t != null)
        {
            FieldInfo f = t.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            if (f != null) return f;
            t = t.BaseType;
        }
        return null;
    }

    private static void AddUniqueTypeToDew(string fieldName, Type type)
    {
        FieldInfo f = typeof(Dew).GetField(fieldName, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        IList list = f != null ? f.GetValue(null) as IList : null;
        if (list == null)
        {
            DariusLog.Warn("TRAVELER-TYPE", "Dew backing list unavailable: " + fieldName);
            return;
        }

        bool haveCurrent = false;
        int removed = 0;
        // Mod assemblies are timestamp-renamed on reload. An old Hero_Darius Type from the previous
        // loaded assembly is not reference-equal to the new Type, even though the game sees the same
        // hero name. Remove stale same-FullName entries before adding the current canonical Type.
        for (int i = list.Count - 1; i >= 0; i--)
        {
            Type existing = list[i] as Type;
            if (existing == null || !string.Equals(existing.FullName, type.FullName, StringComparison.Ordinal)) continue;
            if (Equals(existing, type) && !haveCurrent) { haveCurrent = true; continue; }
            list.RemoveAt(i);
            removed++;
        }
        if (!haveCurrent) list.Add(type);
        if (removed > 0) DariusLog.Info("TRAVELER-TYPE", "Removed stale/duplicate " + type.FullName + " entries=" + removed + " from " + fieldName);
    }

    private static void RemoveTypeFromDew(string fieldName, Type type)
    {
        try
        {
            FieldInfo f = typeof(Dew).GetField(fieldName, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            IList list = f != null ? f.GetValue(null) as IList : null;
            if (list == null) return;
            for (int i = list.Count - 1; i >= 0; i--)
            {
                Type existing = list[i] as Type;
                if (existing != null && string.Equals(existing.FullName, type.FullName, StringComparison.Ordinal)) list.RemoveAt(i);
            }
        }
        catch { }
    }

    private static void AddStringMember(object owner, string memberName, string value)
    {
        if (owner == null || string.IsNullOrEmpty(value)) return;
        BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        FieldInfo f = owner.GetType().GetField(memberName, flags);
        PropertyInfo p = owner.GetType().GetProperty(memberName, flags);
        object current = null;
        Type memberType = null;
        if (f != null) { current = f.GetValue(owner); memberType = f.FieldType; }
        else if (p != null && p.CanRead) { current = p.GetValue(owner, null); memberType = p.PropertyType; }
        else return;

        IList list = current as IList;
        if (list != null)
        {
            foreach (object item in list) if (string.Equals(item as string, value, StringComparison.Ordinal)) return;
            if (!list.IsFixedSize) list.Add(value);
            else if (memberType != null && memberType.IsArray)
            {
                Array old = (Array)current;
                Array next = Array.CreateInstance(memberType.GetElementType(), old.Length + 1);
                Array.Copy(old, next, old.Length);
                next.SetValue(value, old.Length);
                if (f != null) f.SetValue(owner, next); else if (p.CanWrite) p.SetValue(owner, next, null);
            }
            return;
        }
        if (memberType != null && memberType.IsArray)
        {
            Array old = current as Array;
            int n = old != null ? old.Length : 0;
            for (int i = 0; i < n; i++) if (string.Equals(old.GetValue(i) as string, value, StringComparison.Ordinal)) return;
            Array next = Array.CreateInstance(memberType.GetElementType(), n + 1);
            if (old != null) Array.Copy(old, next, n);
            next.SetValue(value, n);
            if (f != null) f.SetValue(owner, next); else if (p != null && p.CanWrite) p.SetValue(owner, next, null);
        }
    }

    private static void SetEnumLikeMember(object obj, string name, string enumName)
    {
        if (obj == null) return;
        FieldInfo f = FindFieldRecursive(obj.GetType(), name) ?? FindFieldRecursive(obj.GetType(), "<" + name + ">k__BackingField");
        if (f != null && f.FieldType.IsEnum)
        {
            string match = Enum.GetNames(f.FieldType).FirstOrDefault(x => string.Equals(x, enumName, StringComparison.OrdinalIgnoreCase));
            if (match != null) f.SetValue(obj, Enum.Parse(f.FieldType, match));
            return;
        }
        PropertyInfo p = obj.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (p != null && p.CanWrite && p.PropertyType.IsEnum)
        {
            string match = Enum.GetNames(p.PropertyType).FirstOrDefault(x => string.Equals(x, enumName, StringComparison.OrdinalIgnoreCase));
            if (match != null) p.SetValue(obj, Enum.Parse(p.PropertyType, match), null);
        }
    }

    private static void TryAssignIfCompatible(object obj, string name, object value)
    {
        if (obj == null) return;
        Type t = obj.GetType();
        BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        while (t != null)
        {
            FieldInfo f = t.GetField(name, flags | BindingFlags.DeclaredOnly) ?? t.GetField("<" + name + ">k__BackingField", flags | BindingFlags.DeclaredOnly);
            if (f != null && !f.IsInitOnly && IsCompatible(f.FieldType, value)) { f.SetValue(obj, value); return; }
            t = t.BaseType;
        }
        PropertyInfo p = obj.GetType().GetProperty(name, flags);
        if (p != null && p.CanWrite && IsCompatible(p.PropertyType, value)) p.SetValue(obj, value, null);
    }

    private static bool IsCompatible(Type targetType, object value)
    {
        if (value == null) return !targetType.IsValueType || Nullable.GetUnderlyingType(targetType) != null;
        return targetType.IsInstanceOfType(value) || (targetType.IsEnum && value.GetType() == targetType);
    }

    private static void SetDatabaseMap(string fieldName, object key, object value)
    {
        if (DewResources.database == null || key == null) return;
        FieldInfo f = DewResources.database.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        IDictionary map = f != null ? f.GetValue(DewResources.database) as IDictionary : null;
        if (map == null) return;
        map[key] = value;
    }

    private static void RemoveDatabaseMappingIfOwned(string fieldName, object key, object expected)
    {
        if (DewResources.database == null || key == null) return;
        try
        {
            FieldInfo f = DewResources.database.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            IDictionary map = f != null ? f.GetValue(DewResources.database) as IDictionary : null;
            if (map != null && map.Contains(key) && Equals(map[key], expected)) map.Remove(key);
        }
        catch { }
    }

    private static void RemoveTypedMappings(Type type, string guid, string name)
    {
        if (DewResources.database == null) return;
        try
        {
            string aqn = type.AssemblyQualifiedName;
            string current;
            if (DewResources.database.typeAssemblyQualifiedNameToGuid.TryGetValue(aqn, out current) && current == guid)
                DewResources.database.typeAssemblyQualifiedNameToGuid.Remove(aqn);
        }
        catch { }
        RemoveDatabaseMappingIfOwned("typeToGuid", type, guid);
        RemoveDatabaseMappingIfOwned("guidToType", guid, type);
        RemoveDatabaseMappingIfOwned("typeNameToGuid", type.Name, guid);
        RemoveDatabaseMappingIfOwned("typeNameToType", type.Name, type);
        RemoveDatabaseMappingIfOwned("nameToGuid", name, guid);
        RemoveDatabaseMappingIfOwned("guidToName", guid, name);
    }

    private static void InvokePreferredSettingsValidate(DewProfile profile)
    {
        if (profile == null) return;
        HashSet<object> visited = new HashSet<object>();
        BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        foreach (FieldInfo f in profile.GetType().GetFields(flags))
        {
            object value = null;
            try { value = f.GetValue(profile); } catch { }
            InvokeValidateIfPreferred(value, visited);
        }
        foreach (PropertyInfo p in profile.GetType().GetProperties(flags))
        {
            if (!p.CanRead || p.GetIndexParameters().Length != 0) continue;
            object value = null;
            try { value = p.GetValue(profile, null); } catch { }
            InvokeValidateIfPreferred(value, visited);
        }
    }

    private static void InvokeValidateIfPreferred(object value, HashSet<object> visited)
    {
        if (value == null || visited.Contains(value)) return;
        visited.Add(value);
        Type t = value.GetType();
        if (t.Name.IndexOf("PreferredGameSettings", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            MethodInfo validate = t.GetMethod("Validate", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
            if (validate != null) { try { validate.Invoke(value, null); } catch { } }
            return;
        }
        IDictionary dict = value as IDictionary;
        if (dict != null)
        {
            foreach (DictionaryEntry e in dict) InvokeValidateIfPreferred(e.Value, visited);
            return;
        }
        IEnumerable enumerable = value as IEnumerable;
        if (!(value is string) && enumerable != null)
            foreach (object item in enumerable) InvokeValidateIfPreferred(item, visited);
    }

    private static void EnsureDictionaryEntry(object owner, string memberName, string key, Func<Type, object> valueFactory)
    {
        IDictionary dict = GetDictionary(owner, memberName);
        if (dict == null || dict.Contains(key)) return;
        Type valueType = GetDictionaryValueType(dict.GetType());
        object value = valueFactory(valueType);
        dict[key] = value;
    }

    private static void SetDictionaryValue(object owner, string memberName, object key, object value)
    {
        IDictionary dict = GetDictionary(owner, memberName);
        if (dict != null) dict[key] = value;
    }

    private static IDictionary GetDictionary(object owner, string memberName)
    {
        if (owner == null) return null;
        BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        FieldInfo f = owner.GetType().GetField(memberName, flags);
        if (f != null) return f.GetValue(owner) as IDictionary;
        PropertyInfo p = owner.GetType().GetProperty(memberName, flags);
        if (p != null && p.CanRead) return p.GetValue(owner, null) as IDictionary;
        return null;
    }

    private static Type GetDictionaryValueType(Type dictionaryType)
    {
        if (dictionaryType.IsGenericType)
        {
            Type[] args = dictionaryType.GetGenericArguments();
            if (args.Length == 2) return args[1];
        }
        return typeof(object);
    }

    private static object CreateDefaultDictionaryValue(Type valueType)
    {
        if (valueType == null || valueType == typeof(object)) return new object();
        try { return Activator.CreateInstance(valueType); }
        catch { return null; }
    }

    private static object CreateCosmeticUnlockValue(Type valueType)
    {
        object value = valueType != null && valueType != typeof(object) ? Activator.CreateInstance(valueType) : new object();
        TryAssignIfCompatible(value, "isUnlocked", true);
        TryAssignIfCompatible(value, "isNew", false);
        TryAssignIfCompatible(value, "generatedFromServer", false);
        return value;
    }

    private static void EnsureSkillUnlock(DewProfile profile, string skillName)
    {
        if (profile == null || string.IsNullOrEmpty(skillName) || profile.skills == null || profile.skills.ContainsKey(skillName)) return;
        profile.skills.Add(skillName, new DewProfile.UnlockData
        {
            status = UnlockStatus.Complete,
            didReadMemory = true,
            isNewHeroOrHeroSkill = true
        });
    }

    private static void EnsureHeroProfileEntries(DewProfile profile)
    {
        if (profile.newStars == null) profile.newStars = new Dictionary<string, DewProfile.StarData>();
        foreach (Type starType in Dew.allStarTypes)
        {
            if (starType != null && starType.Name.StartsWith("Se_Star_Darius_", StringComparison.Ordinal) &&
                !profile.newStars.ContainsKey(starType.Name))
                profile.newStars.Add(starType.Name, new DewProfile.StarData());
        }

        if (profile.heroes == null) profile.heroes = new Dictionary<string, DewProfile.UnlockData>();
        DewProfile.UnlockData heroUnlock;
        if (!profile.heroes.TryGetValue(HeroName, out heroUnlock) || heroUnlock == null) profile.heroes[HeroName] = new DewProfile.UnlockData();
        if (profile.heroUnlockedStarSlots == null) profile.heroUnlockedStarSlots = new Dictionary<string, DewProfile.HeroStarSlotUnlockData>();
        DewProfile.HeroStarSlotUnlockData unlockedSlots;
        if (!profile.heroUnlockedStarSlots.TryGetValue(HeroName, out unlockedSlots) || unlockedSlots == null) profile.heroUnlockedStarSlots[HeroName] = unlockedSlots = new DewProfile.HeroStarSlotUnlockData();
        if (profile.heroLoadouts == null) profile.heroLoadouts = new Dictionary<string, List<HeroLoadoutData>>();
        List<HeroLoadoutData> loadouts;
        if (!profile.heroLoadouts.TryGetValue(HeroName, out loadouts) || loadouts == null) profile.heroLoadouts[HeroName] = loadouts = new List<HeroLoadoutData>();
        while (loadouts.Count < HeroLoadoutData.HeroLoadoutCount) loadouts.Add(new HeroLoadoutData());
        while (loadouts.Count > HeroLoadoutData.HeroLoadoutCount) loadouts.RemoveAt(loadouts.Count - 1);
        for (int i = 0; i < loadouts.Count; i++) { if (loadouts[i] == null) loadouts[i] = new HeroLoadoutData(); loadouts[i].Validate_Imp(HeroName, true, false, unlockedSlots); }
        if (profile.heroSelectedSkins == null) profile.heroSelectedSkins = new Dictionary<string, string>();
        string selectedSkin;
        if (!profile.heroSelectedSkins.TryGetValue(HeroName, out selectedSkin) || string.IsNullOrEmpty(selectedSkin)) profile.heroSelectedSkins[HeroName] = DefaultSkinName;
        if (profile.heroEquippedAccs == null) profile.heroEquippedAccs = new Dictionary<string, List<string>>();
        if (!profile.heroEquippedAccs.ContainsKey(HeroName) || profile.heroEquippedAccs[HeroName] == null) profile.heroEquippedAccs[HeroName] = new List<string>();
        if (profile.receivedLevelUpRewards == null) profile.receivedLevelUpRewards = new Dictionary<string, int>();
        if (!profile.receivedLevelUpRewards.ContainsKey(HeroName)) profile.receivedLevelUpRewards[HeroName] = 0;
    }

    private static void TryInvokeHeroUnlock(DewProfile profile, string heroName)
    {
        if (profile == null) return;
        foreach (MethodInfo m in profile.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            if (m.Name != "UnlockHero") continue;
            ParameterInfo[] ps = m.GetParameters();
            if (ps.Length < 1 || ps[0].ParameterType != typeof(string)) continue;
            object[] args = new object[ps.Length];
            args[0] = heroName;
            for (int i = 1; i < ps.Length; i++) args[i] = ps[i].HasDefaultValue ? ps[i].DefaultValue : (ps[i].ParameterType.IsValueType ? Activator.CreateInstance(ps[i].ParameterType) : null);
            try { m.Invoke(profile, args); return; } catch { }
        }
    }
}

// Lives on the clean Skin_Darius_Default EntityModel. The Skin object copies only the generic
// native EntityModel data contract; the visible hierarchy is Darius-only from the first frame.
