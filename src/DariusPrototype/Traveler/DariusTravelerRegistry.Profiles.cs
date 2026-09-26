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
    private static void EnsureProfiles()
    {
        DewProfile profile = DewSave.profileMain;
        DewProfileStats stats = DewSave.profileStats;
        if (profile == null || stats == null)
            throw new InvalidOperationException("Dew profile services are not ready.");

        // Custom Hero/Skill types have no public registration API for their stats records.
        // Create only the neutral stats containers required by the stock public Unlock* methods.
        if (stats.heroes == null)
            stats.heroes = new Dictionary<string, DewProfileStats.HeroData>();
        if (!stats.heroes.ContainsKey(HeroName))
            stats.heroes.Add(HeroName, new DewProfileStats.HeroData());

        DariusDejaVuRegistry.RegisterProfileStats(stats);

        // Resource compatibility is installed before this late phase. If a public Unlock* call
        // fails now, keep the failure visible instead of fabricating an unlocked profile entry.
        profile.UnlockHero(HeroName);

        string[] requiredSkills =
        {
            DariusFormalRegistry.Decimate.name,
            DariusFormalRegistry.NoxianGuillotine.name,
            DariusFormalRegistry.Hemorrhage.name,
            DariusFormalRegistry.CripplingStrike.name,
            DariusFormalRegistry.Apprehend.name,
            DariusFormalRegistry.Flash.name,
            DariusFormalRegistry.Ghost.name
        };
        foreach (string skillName in requiredSkills)
            profile.UnlockSkill(skillName);

        for (int si = 0; si < SkinSpecs.Length; si++)
            profile.UnlockSkin(SkinSpecs[si].name, "local.darius.independent");

        // Only after stock unlock state exists do we create Darius-specific loadout/constellation
        // containers that the game has no public custom-Hero constructor for.
        EnsureHeroProfileEntries(profile);
        RepairLegacySelectedSkinAliases(profile, "EnsureProfiles");

        RepairDariusConstellationLoadout(profile);

        if (profile.preferredGameSettings != null)
        {
            foreach (PreferredGameSettings settings in profile.preferredGameSettings.Values)
            {
                try { if (settings != null) settings.Validate(); }
                catch (Exception e) { DariusLog.Exception("TRAVELER-PROFILE", e, "PreferredGameSettings.Validate failed"); }
            }
        }

        DariusLog.Info("TRAVELER-PROFILE", "Native UnlockHero/UnlockSkill/UnlockSkin completed; Darius loadout containers ensured without unlock fallbacks.");
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
}