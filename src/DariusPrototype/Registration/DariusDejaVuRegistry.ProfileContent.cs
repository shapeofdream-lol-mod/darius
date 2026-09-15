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
    public static void RegisterContentSettings(DewGameContentSettings content)
    {
        if (content == null)
        {
            DariusLog.Warn("DEJAVU", "Content settings unavailable; candidate injection deferred.");
            return;
        }

        foreach (string skillName in SkillNames)
        {
            AddStringToMember(content, "_availableSkills", skillName);
            AddStringToMember(content, "availableSkills", skillName);
        }
        DariusLog.DebugInfoThrottled("DEJAVU", "content-settings", "Content settings injected. skills=" + string.Join(",", SkillNames) + " Hemorrhage=IdentityMemory", 20.0);
    }

    public static void RegisterCollectables()
    {
        // Force initialization of the public skill cache, then append our Types to the backing list.
        try { _ = Dew.allSkills; } catch { }
        AddTypesToDewBackingList("_allSkills", SkillTypes);

        DariusLog.Info("DEJAVU", "Collectables type lists injected for 4 combat Memories + 1 Hero_Darius Identity Memory.");
    }

    public static void RegisterProfile(DewProfile profile)
    {
        if (profile == null)
        {
            DariusLog.Warn("DEJAVU", "DewProfile unavailable; profile injection deferred.");
            return;
        }

        foreach (string skillName in SkillNames)
        {
            try
            {
                if (profile.skills != null && !profile.skills.ContainsKey(skillName))
                    profile.skills.Add(skillName, new DewProfile.UnlockData
                    {
                        status = UnlockStatus.Complete,
                        didReadMemory = true,
                        isNewHeroOrHeroSkill = false
                    });
            }
            catch (Exception e) { DariusLog.Exception("DEJAVU", e, "Adding profile skill " + skillName + " failed"); }

            EnsureDejaVuTimestamp(profile, skillName);
        }

        DariusLog.Info("DEJAVU", "Profile unlock injection complete. skillKeys=" + CountDictionaryMember(profile, "skills") +
            " dejavuCostKeys=" + CountDictionaryMember(profile, "dejavuCostReductionPeriodTimestamp") +
            " Hemorrhage=skill/Identity (not gem)");
    }

    public static void RegisterProfileStats(DewProfileStats stats)
    {
        if (stats == null)
        {
            DariusLog.Warn("DEJAVU", "DewProfileStats unavailable; Deja Vu qualification injection deferred.");
            return;
        }

        foreach (string skillName in SkillNames)
        {
            try
            {
                if (stats.skills != null)
                {
                    DewProfileStats.ItemData data;
                    if (!stats.skills.TryGetValue(skillName, out data))
                    {
                        data = new DewProfileStats.ItemData();
                        object boxed = data;
                        QualifyItemData(boxed);
                        data = (DewProfileStats.ItemData)boxed;
                        stats.skills.Add(skillName, data);
                    }
                    else
                    {
                        object boxed = data;
                        QualifyItemData(boxed);
                        stats.skills[skillName] = (DewProfileStats.ItemData)boxed;
                    }
                }
            }
            catch (Exception e) { DariusLog.Exception("DEJAVU", e, "Adding profile stats skill " + skillName + " failed"); }
        }

        DariusLog.Info("DEJAVU", "ProfileStats registration complete. skills=" + CountDictionaryMember(stats, "skills") +
            " (Hemorrhage registered as Identity skill; no fabricated wins/playCount).");
    }

    private static void EnsureDejaVuTimestamp(DewProfile profile, string key)
    {
        try
        {
            if (profile.dejavuCostReductionPeriodTimestamp != null &&
                !profile.dejavuCostReductionPeriodTimestamp.ContainsKey(key))
                profile.dejavuCostReductionPeriodTimestamp.Add(key, 0L);
        }
        catch (Exception e) { DariusLog.DebugInfo("DEJAVU", "dejavu timestamp " + key + ": " + e.Message); }
    }

    private static object MakeUnlockData(Type valueType)
    {
        object data = Activator.CreateInstance(valueType);
        SetMember(data, "status", UnlockStatus.Complete);
        SetMember(data, "didReadMemory", true);
        SetMember(data, "isNewHeroOrHeroSkill", false);
        return data;
    }

    private static void QualifyItemData(object data)
    {
        // Match the working Elemental Summon reference mod: merely having an ItemData
        // entry is enough for the profile systems. Do not fabricate wins/playCount.
        // Vanilla Deja Vu cost reads ItemData.wins, so leaving the natural value (0 for
        // a newly injected item) preserves a normal non-zero rarity-based Stardust cost.
        if (data == null) return;
    }

    private static void AddTypesToDewBackingList(string fieldName, IEnumerable<Type> types)
    {
        try
        {
            FieldInfo field = typeof(Dew).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);
            if (field == null)
            {
                DariusLog.Warn("DEJAVU", "Dew backing field not found: " + fieldName);
                return;
            }

            IList list = field.GetValue(null) as IList;
            if (list == null)
            {
                DariusLog.Warn("DEJAVU", "Dew backing list unavailable: " + fieldName);
                return;
            }

            Type[] currentTypes = types != null ? types.Where(t => t != null).ToArray() : Array.Empty<Type>();
            HashSet<string> currentNames = new HashSet<string>(currentTypes.Select(t => t.Name), StringComparer.Ordinal);

            // DewMod can hot-reload an assembly in the same process. Types from the previous
            // Darius assembly are not reference-equal to the new Types, so remove stale entries
            // with the same resource names before adding the current assembly's Types.
            int removed = 0;
            for (int i = list.Count - 1; i >= 0; i--)
            {
                Type existing = list[i] as Type;
                if (existing == null || !currentNames.Contains(existing.Name)) continue;
                if (!currentTypes.Contains(existing))
                {
                    list.RemoveAt(i);
                    removed++;
                }
            }

            int added = 0;
            foreach (Type type in currentTypes)
            {
                if (!list.Contains(type)) { list.Add(type); added++; }
            }
            DariusLog.DebugInfo("DEJAVU", fieldName + " count=" + list.Count + " added=" + added + " staleRemoved=" + removed);
        }
        catch (Exception e) { DariusLog.Exception("DEJAVU", e, "Adding types to " + fieldName + " failed"); }
    }
}