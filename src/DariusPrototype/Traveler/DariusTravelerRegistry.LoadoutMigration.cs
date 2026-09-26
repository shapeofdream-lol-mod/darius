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
                    // A null lookup is unresolved, not proof that the selection belongs to another
                    // Hero. Only remove entries whose resolved resource explicitly names one.
                    clear = prefab != null &&
                            prefab.heroType != null &&
                            prefab.heroType != typeof(Hero_Darius);
                }
                catch (Exception e)
                {
                    // Profile migration is destructive, so unresolved resources are preserved.
                    // A transient lookup failure must never be interpreted as proof that a user's
                    // selected star belongs to another Hero.
                    DariusLog.DebugInfo("CONSTELLATION-PROFILE",
                        "Preserving unresolved star during migration name=" + item.name +
                        " error=" + e.GetType().Name);
                    clear = false;
                }
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

        DariusOfficialEntityModelMarker marker = skin.GetComponent<DariusOfficialEntityModelMarker>();
        if (marker == null)
        {
            errors.Add(skin.name + " fresh EntityModel marker missing");
            return;
        }
        if (!string.Equals(marker.variantKey, spec.variantKey, StringComparison.Ordinal))
            errors.Add(skin.name + " variant profile wrong: " + marker.variantKey + " expected=" + spec.variantKey);

        if (skin.GetComponent<DariusOfficialActionRuntime>() == null)
            errors.Add(skin.name + " fresh action runtime missing");
    }
}