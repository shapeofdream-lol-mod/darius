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
    private static object FindDescriptionSettings(object[] args)
    {
        if (args == null) return null;
        for (int i = 0; i < args.Length; i++)
        {
            object candidate = args[i];
            if (candidate == null) continue;
            Type type = candidate.GetType();
            if (type.Name.IndexOf("DescriptionSettings", StringComparison.OrdinalIgnoreCase) >= 0)
                return candidate;
        }

        // Compatibility fallback for builds where the nested settings type was renamed.
        // Require at least two characteristic members so ordinary arguments cannot be mistaken
        // for the tooltip settings object.
        for (int i = 0; i < args.Length; i++)
        {
            object candidate = args[i];
            if (candidate == null) continue;
            int matches = 0;
            if (HasMember(candidate, "contextObject")) matches++;
            if (HasMember(candidate, "contextEntity")) matches++;
            if (HasMember(candidate, "currentLevel")) matches++;
            if (HasMember(candidate, "showLevelScaling")) matches++;
            if (matches >= 2) return candidate;
        }
        return null;
    }

    private static bool HasMember(object target, string name)
    {
        if (target == null || string.IsNullOrEmpty(name)) return false;
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        try
        {
            Type type = target.GetType();
            return type.GetField(name, flags) != null || type.GetProperty(name, flags) != null;
        }
        catch { return false; }
    }

    private static Hero ResolveTooltipHero(object settings, SkillTrigger skill)
    {
        // DescriptionSettings.contextEntity is the authoritative entity for stock tooltip
        // expressions.  Some UI paths only provide contextObject, so follow the same ownership
        // chain before falling back to the local player hero.
        try
        {
            object contextEntity = ReadMember(settings, "contextEntity");
            Hero hero = contextEntity as Hero;
            if (hero != null) return hero;

            if (contextEntity != null)
            {
                hero = ReadMember(contextEntity, "hero") as Hero;
                if (hero == null) hero = ReadMember(contextEntity, "owner") as Hero;
                if (hero == null) hero = ReadMember(contextEntity, "entity") as Hero;
                if (hero != null) return hero;
            }

            object contextObject = ReadMember(settings, "contextObject");
            hero = contextObject as Hero;
            if (hero != null) return hero;

            SkillTrigger contextSkill = contextObject as SkillTrigger;
            if (contextSkill == null)
            {
                GameObject go = contextObject as GameObject;
                if (go != null) contextSkill = go.GetComponent<SkillTrigger>();
            }
            if (contextSkill == null)
            {
                Component component = contextObject as Component;
                if (component != null) contextSkill = component.GetComponent<SkillTrigger>();
            }

            Entity owner = ReadSkillOwner(contextSkill != null ? contextSkill : skill);
            hero = owner as Hero;
            if (hero != null) return hero;
        }
        catch { }

        try { return DewPlayer.local != null ? DewPlayer.local.hero : null; }
        catch { return null; }
    }

    private static SkillTrigger CacheTooltipSkill(string key, SkillTrigger skill)
    {
        if (!string.IsNullOrEmpty(key) && skill != null) TooltipSkillCache[key] = skill;
        return skill;
    }

    private static string BuildTooltipCacheKey(string key, SkillTrigger skill, int currentLevel, int? previousLevel, bool showScaling)
    {
        Hero hero = null;
        try { hero = DewPlayer.local != null ? DewPlayer.local.hero : null; } catch { }
        int heroId = hero != null ? hero.GetInstanceID() : 0;
        if (_tooltipCacheHeroId != heroId)
        {
            _tooltipCacheHeroId = heroId;
            TooltipSkillCache.Clear();
            TooltipTextCache.Clear();
        }

        int ad10 = 0, hp10 = 0, stateA = 0, stateB = 0;
        try
        {
            if (hero != null && hero.Status != null)
            {
                ad10 = Mathf.RoundToInt(hero.Status.finalStats.attackDamage * 10f);
                hp10 = Mathf.RoundToInt(hero.Status.maxHealth * 10f);
            }
            if (hero != null)
            {
                if (key.Contains("Darius_Decimate"))
                {
                    stateA = DariusConstellationRuntime.GetStarLevel(hero, DariusConstellationIds.IInstantDecimate);
                    stateB = DariusConstellationRuntime.GetStarLevel(hero, DariusConstellationIds.LRevitalize);
                }
                else if (key.Contains("Darius_NoxianGuillotine"))
                {
                    DariusHemorrhageRuntime hem = hero.GetComponent<DariusHemorrhageRuntime>();
                    stateA = hem != null ? hem.identityMemoryLevel : 1;
                }
                else if (DariusFormalLocalization.IsHemorrhageKey(key))
                {
                    stateA = DariusConstellationRuntime.GetWarFervorLevel(hero);
                    DariusHemorrhageRuntime hem = hero.GetComponent<DariusHemorrhageRuntime>();
                    stateB = hem != null ? hem.identityMemoryLevel : currentLevel;
                }
            }
        }
        catch { }

        return key + "|" + currentLevel + "|" + (previousLevel.HasValue ? previousLevel.Value : -1) + "|" +
               (showScaling ? 1 : 0) + "|" + heroId + "|" + ad10 + "|" + hp10 + "|" + stateA + "|" + stateB;
    }
}