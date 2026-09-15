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