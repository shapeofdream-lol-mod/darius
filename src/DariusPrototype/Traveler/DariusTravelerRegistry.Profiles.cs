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
            if (stats.heroes == null)
                stats.heroes = new Dictionary<string, DewProfileStats.HeroData>();
            if (!stats.heroes.ContainsKey(HeroName))
                stats.heroes.Add(HeroName, new DewProfileStats.HeroData());
        }

        if (profile != null) EnsureHeroProfileEntries(profile);

        // Never run a whole-profile validation while the Mod loader is still enumerating character
        // assemblies. With several runtime Travelers installed, each Validate postfix repairs every
        // other registry and mutates the same Dew type collections being enumerated. Runtime logs
        // showed one call blocking the Unity main thread for 246 seconds before throwing.
        if (profile != null && profile.preferredGameSettings != null)
        {
            foreach (PreferredGameSettings settings in profile.preferredGameSettings.Values)
            {
                try { if (settings != null) settings.Validate(); }
                catch (Exception e) { DariusLog.Exception("TRAVELER-PROFILE", e, "PreferredGameSettings.Validate failed"); }
            }
        }

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
                    DariusLog.Exception("TRAVELER-PROFILE", e, "UnlockSkin failed name=" + skinName + "; using public profile fallback");
                    if (profile.skins == null)
                        profile.skins = new Dictionary<string, DewProfile.CosmeticsData>();
                    profile.skins[skinName] = new DewProfile.CosmeticsData
                    {
                        isUnlocked = true,
                        isNew = false,
                        ownershipKey = "local.darius.independent"
                    };
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
                catch (Exception e)
                {
                    DariusLog.Exception("TRAVELER-PROFILE", e, "UnlockSkill failed name=" + skillName + "; using public profile fallback");
                    if (profile.skills == null)
                        profile.skills = new Dictionary<string, DewProfile.UnlockData>();
                    profile.skills[skillName] = new DewProfile.UnlockData
                    {
                        status = UnlockStatus.Complete,
                        didReadMemory = true,
                        isNewHeroOrHeroSkill = true
                    };
                }
            }

            try { profile.UnlockHero(HeroName); }
            catch (Exception e)
            {
                DariusLog.Exception("TRAVELER-PROFILE", e, "UnlockHero failed; using public profile fallback");
                if (profile.heroes == null)
                    profile.heroes = new Dictionary<string, DewProfile.UnlockData>();
                profile.heroes[HeroName] = new DewProfile.UnlockData
                {
                    status = UnlockStatus.Complete,
                    isNewHeroOrHeroSkill = true
                };
            }

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
}