using System;
using System.Collections.Generic;

public static partial class DariusTravelerRegistry
{
    private static void EnsureHeroProfileEntries(DewProfile profile)
    {
        if (profile.newStars == null)
            profile.newStars = new Dictionary<string, DewProfile.StarData>();

        foreach (Type starType in Dew.allStarTypes)
        {
            if (starType != null &&
                starType.Name.StartsWith("Se_Star_Darius_", StringComparison.Ordinal) &&
                !profile.newStars.ContainsKey(starType.Name))
            {
                profile.newStars.Add(starType.Name, new DewProfile.StarData());
            }
        }

        if (profile.heroes == null)
            profile.heroes = new Dictionary<string, DewProfile.UnlockData>();
        if (!profile.heroes.ContainsKey(HeroName) || profile.heroes[HeroName] == null)
            profile.heroes[HeroName] = new DewProfile.UnlockData();

        if (profile.heroUnlockedStarSlots == null)
            profile.heroUnlockedStarSlots = new Dictionary<string, DewProfile.HeroStarSlotUnlockData>();
        DewProfile.HeroStarSlotUnlockData unlockedSlots;
        if (!profile.heroUnlockedStarSlots.TryGetValue(HeroName, out unlockedSlots) || unlockedSlots == null)
        {
            unlockedSlots = new DewProfile.HeroStarSlotUnlockData();
            profile.heroUnlockedStarSlots[HeroName] = unlockedSlots;
        }

        if (profile.heroLoadouts == null)
            profile.heroLoadouts = new Dictionary<string, List<HeroLoadoutData>>();
        List<HeroLoadoutData> loadouts;
        if (!profile.heroLoadouts.TryGetValue(HeroName, out loadouts) || loadouts == null)
        {
            loadouts = new List<HeroLoadoutData>();
            profile.heroLoadouts[HeroName] = loadouts;
        }

        while (loadouts.Count < HeroLoadoutData.HeroLoadoutCount)
            loadouts.Add(new HeroLoadoutData());
        while (loadouts.Count > HeroLoadoutData.HeroLoadoutCount)
            loadouts.RemoveAt(loadouts.Count - 1);

        for (int i = 0; i < loadouts.Count; i++)
        {
            if (loadouts[i] == null) loadouts[i] = new HeroLoadoutData();
            loadouts[i].Validate_Imp(HeroName, true, false, unlockedSlots);
        }

        if (profile.heroSelectedSkins == null)
            profile.heroSelectedSkins = new Dictionary<string, string>();
        string selectedSkin;
        if (!profile.heroSelectedSkins.TryGetValue(HeroName, out selectedSkin) || string.IsNullOrEmpty(selectedSkin))
            profile.heroSelectedSkins[HeroName] = DefaultSkinName;

        if (profile.heroEquippedAccs == null)
            profile.heroEquippedAccs = new Dictionary<string, List<string>>();
        if (!profile.heroEquippedAccs.ContainsKey(HeroName) || profile.heroEquippedAccs[HeroName] == null)
            profile.heroEquippedAccs[HeroName] = new List<string>();

        if (profile.receivedLevelUpRewards == null)
            profile.receivedLevelUpRewards = new Dictionary<string, int>();
        if (!profile.receivedLevelUpRewards.ContainsKey(HeroName))
            profile.receivedLevelUpRewards[HeroName] = 0;
    }
}
