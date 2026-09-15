using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Mirror;
using UnityEngine;
using DewInternal;

public static partial class DariusDejaVuRegistry
{
    private static SkillTrigger ResolveDariusTooltipSkill(object settings, string renderedText)
    {
        object context = ReadMember(settings, "contextObject");
        SkillTrigger skill = context as SkillTrigger;
        if (skill == null)
        {
            GameObject go = context as GameObject;
            if (go != null) skill = go.GetComponent<SkillTrigger>();
        }
        if (skill == null)
        {
            Component component = context as Component;
            if (component != null) skill = component.GetComponent<SkillTrigger>();
        }
        if (skill != null && DariusFormalLocalization.IsDariusMemoryKey(skill.GetType().Name))
            return CacheTooltipSkill(skill.GetType().Name, skill);

        string key = DetectDariusMemoryKey(renderedText);
        if (!DariusFormalLocalization.IsDariusMemoryKey(key)) return null;

        Hero cacheHero = null;
        try { cacheHero = DewPlayer.local != null ? DewPlayer.local.hero : null; } catch { }
        int cacheHeroId = cacheHero != null ? cacheHero.GetInstanceID() : 0;
        if (_tooltipCacheHeroId != cacheHeroId)
        {
            _tooltipCacheHeroId = cacheHeroId;
            TooltipSkillCache.Clear();
            TooltipTextCache.Clear();
        }
        SkillTrigger cachedSkill;
        // Registered prefabs are intentionally inactive, so inactivity must not invalidate this
        // cache. The level-aware renderer can still use DescriptionSettings.currentLevel with them.
        if (TooltipSkillCache.TryGetValue(key, out cachedSkill) && cachedSkill != null)
            return cachedSkill;

        // Fallback for UI paths where DescriptionSettings.contextObject is absent. rc7 used
        // FindObjectsOfType<SkillTrigger>() here, which scans the entire scene whenever the Ctrl
        // skill-edit UI opens/closes and caused a visible one-frame hitch. Only inspect the local
        // hero's tiny equipped-ability dictionary; if the Memory is not equipped, use the registered
        // prefab and the level supplied by DescriptionSettings.
        Hero localHero = null;
        try { localHero = DewPlayer.local != null ? DewPlayer.local.hero : null; } catch { }
        if (localHero != null && localHero.Ability != null && localHero.Ability.abilities != null)
        {
            try
            {
                foreach (var pair in localHero.Ability.abilities)
                {
                    SkillTrigger candidate = pair.Value as SkillTrigger;
                    if (candidate == null) continue;
                    if (MemoryKeyMatchesSkill(key, candidate))
                    {
                        TooltipSkillCache[key] = candidate;
                        return candidate;
                    }
                }
            }
            catch { }
        }
        return RegisteredSkillForKey(key);
    }

    private static Entity ReadSkillOwner(SkillTrigger skill)
    {
        if (skill == null) return null;
        object value = ReadMember(skill, "owner");
        return value as Entity;
    }

    private static bool MemoryKeyMatchesSkill(string key, SkillTrigger skill)
    {
        if (skill == null || string.IsNullOrEmpty(key)) return false;
        string typeName = skill.GetType().Name;
        if (key.Contains("Darius_Decimate")) return typeName.Contains("Darius_Decimate");
        if (key.Contains("Darius_CripplingStrike")) return typeName.Contains("Darius_CripplingStrike");
        if (key.Contains("Darius_Apprehend")) return typeName.Contains("Darius_Apprehend");
        if (key.Contains("Darius_NoxianGuillotine")) return typeName.Contains("Darius_NoxianGuillotine");
        if (DariusFormalLocalization.IsHemorrhageKey(key)) return DariusFormalLocalization.IsHemorrhageKey(typeName);
        return false;
    }

    private static SkillTrigger RegisteredSkillForKey(string key)
    {
        if (string.IsNullOrEmpty(key)) return null;
        if (key.Contains("Darius_Decimate")) return CacheTooltipSkill(key, DariusFormalRegistry.Decimate);
        if (key.Contains("Darius_CripplingStrike")) return CacheTooltipSkill(key, DariusFormalRegistry.CripplingStrike);
        if (key.Contains("Darius_Apprehend")) return CacheTooltipSkill(key, DariusFormalRegistry.Apprehend);
        if (key.Contains("Darius_NoxianGuillotine")) return CacheTooltipSkill(key, DariusFormalRegistry.NoxianGuillotine);
        if (DariusFormalLocalization.IsHemorrhageKey(key)) return CacheTooltipSkill(key, DariusFormalRegistry.Hemorrhage);
        return null;
    }

    private static string DetectDariusMemoryKey(string text)
    {
        if (string.IsNullOrEmpty(text)) return null;
        // Use structural phrases rather than numeric literals: native value markup inserts TMP
        // tags between the words and numbers, so old tests such as "蓄力0.75秒" stop matching.
        if (text.IndexOf("斧刃外圈", StringComparison.Ordinal) >= 0 && text.IndexOf("出血", StringComparison.Ordinal) >= 0)
            return "St_Darius_Decimate";
        if (text.IndexOf("下一次普攻", StringComparison.Ordinal) >= 0 && text.IndexOf("减速", StringComparison.Ordinal) >= 0)
            return "St_Darius_CripplingStrike";
        if (text.IndexOf("拉至身前", StringComparison.Ordinal) >= 0 && text.IndexOf("60°", StringComparison.Ordinal) >= 0)
            return "St_Darius_Apprehend";
        if (text.IndexOf("纯粹伤害", StringComparison.Ordinal) >= 0 && text.IndexOf("诺克萨斯之力", StringComparison.Ordinal) >= 0)
            return "St_Darius_NoxianGuillotine";
        if (text.IndexOf("每秒每层造成", StringComparison.Ordinal) >= 0 && text.IndexOf("出血", StringComparison.Ordinal) >= 0)
            return "St_D_Darius_Hemorrhage";
        return null;
    }

    private static object ReadMember(object target, string name)
    {
        if (target == null || string.IsNullOrEmpty(name)) return null;
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly;
        try
        {
            for (Type type = target.GetType(); type != null; type = type.BaseType)
            {
                FieldInfo field = type.GetField(name, flags);
                if (field != null) return field.GetValue(target);
                PropertyInfo property = type.GetProperty(name, flags);
                if (property != null && property.CanRead) return property.GetValue(target, null);
            }
        }
        catch { }
        return null;
    }

    private static int? ReadNullableIntMember(object target, string name)
    {
        object value = ReadMember(target, name);
        if (value == null) return null;
        try { return Convert.ToInt32(value); } catch { return null; }
    }

    private static bool ReadBoolMember(object target, string name, bool fallback)
    {
        object value = ReadMember(target, name);
        if (value == null) return fallback;
        try { return Convert.ToBoolean(value); } catch { return fallback; }
    }

    private static void LocalizationNodesPostfix(MethodBase __originalMethod, string __0, ref List<LocaleNode> __result)
    {
        string text;
        if (DariusLanguage.IsJapanese && DariusJapaneseLocalization.TryGet(__0, true, out text))
            __result = DariusFormalLocalization.Nodes(text);
        else if (DariusConstellationLocalization.TryDescription(__0, out text))
            __result = DariusFormalLocalization.Nodes(text);
        else if (DariusFormalLocalization.TrySkillDescription(__0, out text))
            __result = DariusFormalLocalization.Nodes(text);
        else if (DariusFormalLocalization.IsHemorrhageKey(__0))
            __result = DariusFormalLocalization.Nodes(DariusFormalLocalization.HemorrhageDescription);
    }
}