using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;

public static partial class DariusFormalRegistry
{
    private static void ConfigureSkillPrefab(SkillTrigger skill, Rarity rarity, float cooldown, string iconKey, string locationName, AbilityInstance spawnedInstance, Action<TriggerConfig> extra)
    {
        skill.rarity = rarity;
        skill.type = SkillType.Normal;
        skill.tags = DescriptionTags.None;
        skill.excludeFromPool = false;
        skill.isLevelUpEnabled = true;
        skill.useCustomSkillHastePerLevel = false;
        skill.startEffect = null;
        skill.endEffect = null;
        skill.characterSkillOwner = string.Empty;
        SetSkillLocation(skill, locationName);
        skill.configs = new TriggerConfig[]
        {
            DariusTriggerConfigRuntimeEditor.CreateBase(skill, cooldown),
            DariusTriggerConfigRuntimeEditor.CreateBase(skill, cooldown)
        };

        for (int i = 0; i < skill.configs.Length; i++)
        {
            TriggerConfig cfg = skill.configs[i];
            cfg.triggerIcon = DariusPrototypeIcons.Get(iconKey);
            cfg.spawnedInstance = spawnedInstance;
            cfg.isActive = true;
            cfg.canReceiveCooldownReduction = true;
            cfg.castMethod = cfg.castMethod ?? new CastMethodData();
            extra?.Invoke(cfg);
        }

        DariusTriggerConfigRuntimeEditor.AttachAll(skill);
        DariusLog.Info("CONFIG", "Prefab preconfigured " + skill.name + " configs=" + skill.configs.Length +
            " cooldown=" + cooldown + " icon=" + iconKey + " spawnedInstance=" +
            (spawnedInstance != null ? spawnedInstance.name : "<null>"));
    }

    private static void ConfigureSummonerSkillPrefab(SkillTrigger skill, string iconKey, SkillTrigger nativeMovement)
    {
        if (skill == null) return;
        skill.rarity = Rarity.Character;
        skill.type = SkillType.Normal;
        skill.tags = DescriptionTags.None;
        skill.excludeFromPool = true;
        skill.isLevelUpEnabled = false;
        skill.useCustomSkillHastePerLevel = false;
        skill.startEffect = null;
        skill.endEffect = null;
        skill.characterSkillOwner = "Hero_Darius";
        if (!CopySkillLocation(nativeMovement, skill, "SUMMONER-CONFIG")) SetSkillLocation(skill, "Movement");
        bool isFlash = string.Equals(iconKey, "FLASH", StringComparison.OrdinalIgnoreCase);
        skill.configs = new TriggerConfig[]
        {
            isFlash ? DariusSummonerBalance.CreateFlashConfig(skill) : DariusSummonerBalance.CreateGhostConfig(skill),
            isFlash ? DariusSummonerBalance.CreateFlashConfig(skill) : DariusSummonerBalance.CreateGhostConfig(skill)
        };
        for (int i = 0; i < skill.configs.Length; i++)
        {
            skill.configs[i].triggerIcon = DariusPrototypeIcons.Get(iconKey);
        }
        DariusTriggerConfigRuntimeEditor.AttachAll(skill);
        DariusLog.Info("SUMMONER-CONFIG", "Configured " + skill.name + " as Hero_Darius movement choice; source=" +
            (nativeMovement != null ? nativeMovement.name : "<fallback>") + " cooldown=" + DariusSummonerBalance.Cooldown.ToString("0.###") +
            " charges=" + DariusSummonerBalance.MaxCharges + " icon=" + iconKey);
    }

    private static bool CopySkillLocation(SkillTrigger source, SkillTrigger destination, string logTag)
    {
        if (source == null || destination == null) return false;
        try
        {
            FieldInfo field = typeof(SkillTrigger).GetField("<skillType>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance);
            if (field == null) return false;
            object value = field.GetValue(source);
            field.SetValue(destination, value);
            DariusLog.Info(logTag, "Copied skill location from " + source.name + " to " + destination.name + " value=" + value);
            return true;
        }
        catch (Exception e)
        {
            DariusLog.Exception(logTag, e, "Failed copying native movement skill location");
            return false;
        }
    }

    private static void ConfigureIdentityPrefab(St_D_Darius_Hemorrhage identity)
    {
        identity.rarity = Rarity.Rare;
        identity.type = SkillType.Normal;
        identity.tags = DescriptionTags.None;
        identity.excludeFromPool = false;
        identity.isLevelUpEnabled = true;
        identity.useCustomSkillHastePerLevel = false;
        identity.startEffect = null;
        identity.endEffect = null;
        identity.characterSkillOwner = "Hero_Darius";
        SetSkillLocation(identity, "Identity");
        identity.configs = new TriggerConfig[]
        {
            DariusTriggerConfigRuntimeEditor.CreateBase(identity, 9999f),
            DariusTriggerConfigRuntimeEditor.CreateBase(identity, 9999f)
        };
        for (int i = 0; i < identity.configs.Length; i++)
        {
            TriggerConfig cfg = identity.configs[i];
            cfg.triggerIcon = DariusPrototypeIcons.Get("H");
            cfg.spawnedInstance = null;
            cfg.isActive = false;
            cfg.canReceiveCooldownReduction = false;
            cfg.alwaysCastImmediately = false;
            cfg.castMethod = cfg.castMethod ?? new CastMethodData();
            cfg.castMethod.type = CastMethodType.None;
        }
        DariusTriggerConfigRuntimeEditor.AttachAll(identity);
        DariusLog.Info("IDENTITY-CONFIG", "Configured Hemorrhage as upgradeable Hero_Darius Identity Memory icon=" + (DariusPrototypeIcons.Get("H") != null));
    }

    private static SkillTrigger TryResolveNativeSkill(string typeName, string logTag)
    {
        Type nativeType = AccessTools.TypeByName(typeName);
        if (nativeType == null)
        {
            DariusLog.Warn(logTag, "Native skill type not found: " + typeName);
            return null;
        }
        MethodInfo[] methods = typeof(DewResources).GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
        for (int i = 0; i < methods.Length; i++)
        {
            MethodInfo m = methods[i];
            if (m.Name != "GetByType" || m.IsGenericMethod) continue;
            ParameterInfo[] ps = m.GetParameters();
            if (ps.Length < 1 || ps[0].ParameterType != typeof(Type)) continue;
            try
            {
                object[] args = new object[ps.Length];
                args[0] = nativeType;
                for (int j = 1; j < ps.Length; j++)
                {
                    if (ps[j].HasDefaultValue) args[j] = ps[j].DefaultValue;
                    else args[j] = ps[j].ParameterType.IsValueType ? Activator.CreateInstance(ps[j].ParameterType) : null;
                }
                object result = m.Invoke(null, args);
                SkillTrigger skill = result as SkillTrigger;
                if (skill != null) return skill;
                GameObject go = result as GameObject;
                if (go != null)
                {
                    skill = go.GetComponent<SkillTrigger>();
                    if (skill != null) return skill;
                }
                Component component = result as Component;
                if (component != null)
                {
                    skill = component.GetComponent<SkillTrigger>();
                    if (skill != null) return skill;
                }
            }
            catch (Exception e)
            {
                DariusLog.DebugInfo(logTag, "GetByType probe failed for " + typeName + " via " + m + ": " + e.GetType().Name);
            }
        }
        DariusLog.Warn(logTag, "Could not resolve native skill resource for " + typeName);
        return null;
    }
}