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
    private static DewGameContentSettings _contentOwner;

    private static readonly Type[] DariusRegisteredSkillTypes =
    {
        typeof(St_Darius_Decimate),
        typeof(St_Darius_NoxianGuillotine),
        typeof(St_D_Darius_Hemorrhage),
        typeof(St_Darius_CripplingStrike),
        typeof(St_Darius_Apprehend),
        typeof(St_Darius_Flash),
        typeof(St_Darius_Ghost)
    };

    private static readonly Type[] DariusRegisteredHeroSkillTypes =
    {
        typeof(St_Darius_Decimate),
        typeof(St_Darius_NoxianGuillotine),
        typeof(St_D_Darius_Hemorrhage),
        typeof(St_Darius_Flash),
        typeof(St_Darius_Ghost)
    };

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
        DariusUnsupportedResourceBridge.EnsureHeroTypes(
            new[] { typeof(Hero_Darius) }, "TRAVELER-TYPE");
        DariusUnsupportedResourceBridge.EnsureSkillTypes(
            DariusRegisteredSkillTypes, "TRAVELER-TYPE");
        DariusUnsupportedResourceBridge.EnsureHeroSkillTypes(
            DariusRegisteredHeroSkillTypes, "TRAVELER-TYPE");
        DariusConstellationRegistry.RegisterTypeCache();

        DariusLog.DebugInfoThrottled("TRAVELER-TYPE", "type-caches",
            "Unsupported Dew type-cache bridge registered Hero_Darius, Darius skills and constellation StarEffects.", 20.0);
    }

    private static void EnsureContentEntry(ref string[] serializedNames, List<string> runtimeNames, string value)
    {
        if (string.IsNullOrEmpty(value)) return;

        if (serializedNames == null)
        {
            serializedNames = new[] { value };
        }
        else if (!serializedNames.Contains(value))
        {
            string[] next = new string[serializedNames.Length + 1];
            Array.Copy(serializedNames, next, serializedNames.Length);
            next[serializedNames.Length] = value;
            serializedNames = next;
        }

        if (runtimeNames != null && !runtimeNames.Contains(value))
            runtimeNames.Add(value);
    }

    private static void RemoveContentEntry(ref string[] serializedNames, List<string> runtimeNames, string value)
    {
        if (string.IsNullOrEmpty(value)) return;
        if (serializedNames != null)
            serializedNames = serializedNames.Where(x => !string.Equals(x, value, StringComparison.Ordinal)).ToArray();
        if (runtimeNames != null)
            runtimeNames.RemoveAll(x => string.Equals(x, value, StringComparison.Ordinal));
    }

    public static void UnregisterContent()
    {
        DewGameContentSettings content = _contentOwner;
        _contentOwner = null;
        if (content == null) return;

        RemoveContentEntry(ref content._availableHeroes, content.availableHeroes, HeroName);

        string[] skillNames =
        {
            "St_Darius_Decimate",
            "St_Darius_NoxianGuillotine",
            "St_D_Darius_Hemorrhage",
            "St_Darius_CripplingStrike",
            "St_Darius_Apprehend",
            "St_Darius_Flash",
            "St_Darius_Ghost"
        };
        for (int i = 0; i < skillNames.Length; i++)
            RemoveContentEntry(ref content._availableSkills, content.availableSkills, skillNames[i]);

        if (content._availableStars != null)
            content._availableStars = content._availableStars
                .Where(x => string.IsNullOrEmpty(x) || !x.StartsWith("Se_Star_Darius_", StringComparison.Ordinal))
                .ToArray();
        if (content.availableStars != null)
            content.availableStars.RemoveAll(x =>
                !string.IsNullOrEmpty(x) && x.StartsWith("Se_Star_Darius_", StringComparison.Ordinal));

        DariusLog.DebugInfo("TRAVELER-CONTENT",
            "Removed runtime Hero_Darius/skill/star entries from the owned DewGameContentSettings instance.");
    }

    public static void RegisterContent(DewGameContentSettings content)
    {
        if (content == null) return;
        if (_contentOwner != null && !ReferenceEquals(_contentOwner, content))
            UnregisterContent();
        _contentOwner = content;

        EnsureContentEntry(ref content._availableHeroes, content.availableHeroes, HeroName);

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
            EnsureContentEntry(ref content._availableSkills, content.availableSkills, skill);

        foreach (Type starType in Dew.allStarTypes)
        {
            if (starType == null || !starType.Name.StartsWith("Se_Star_Darius_", StringComparison.Ordinal)) continue;
            EnsureContentEntry(ref content._availableStars, content.availableStars, starType.Name);
        }

        // DewGameContentSettings has no skin list in the documented runtime contract. Skin unlock
        // and selection are handled through DewProfile and the custom resource identity bridge.
        DariusLog.DebugInfoThrottled("TRAVELER-CONTENT", "content",
            "Content includes Hero_Darius and seven Darius skill resources including Flash/Ghost movement choices.", 20.0);
    }
}
