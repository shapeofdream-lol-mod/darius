using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Newtonsoft.Json;
using UnityEngine;

// Protects custom constellation purchases/loadouts from DewProfile.Validate removing runtime-only
// StarEffect names before the mod has fully rebuilt its resource maps. Native profile data remains
// authoritative; the JSON file is only a boot-time safety copy for the narrow window before the
// custom StarEffects are registered.
public static partial class DariusConstellationPersistence
{
    private const string HeroName = "Hero_Darius";

    private const string StarPrefix = "Se_Star_Darius_";

    private const int FileVersion = 1;

    private static SavedState _diskState;

    private static bool _diskLoaded;

    private static bool _bootRestoreLogged;

    private static bool _bootRestoreComplete;

    public sealed class GuardState
    {
        internal SavedState data;
        internal Dictionary<string, double> dust;
        internal bool hadCustomData;
    }

    [Serializable]
    internal sealed class SavedSlot
    {
        public int index;
        public string name;
        public int level;
    }

    [Serializable]
    internal sealed class SavedPage
    {
        public List<SavedSlot> destruction = new List<SavedSlot>();
        public List<SavedSlot> life = new List<SavedSlot>();
        public List<SavedSlot> imagination = new List<SavedSlot>();
        public List<SavedSlot> flexible = new List<SavedSlot>();
    }

    [Serializable]
    internal sealed class SavedState
    {
        public int version = FileVersion;
        public Dictionary<string, int> purchased = new Dictionary<string, int>(StringComparer.Ordinal);
        public List<SavedPage> pages = new List<SavedPage>();
        public string unlockedSlotsJson;
        public Dictionary<string, double> dust = new Dictionary<string, double>(StringComparer.Ordinal);
    }

    private static string SavePath
    {
        get
        {
            string root = Path.Combine(Application.persistentDataPath, "ShapeOfDreams_OpenAI_TravelerMods");
            return Path.Combine(root, "Darius_constellation_state.json");
        }
    }

    public static GuardState BeforeValidate(DewProfile profile)
    {
        try
        {
            SavedState data = Capture(profile);
            return new GuardState
            {
                data = data,
                dust = CaptureDust(profile),
                hadCustomData = HasCustomData(data)
            };
        }
        catch (Exception e)
        {
            DariusLog.Exception("STAR-PERSIST", e, "Could not snapshot Darius constellation state before Validate");
            return null;
        }
    }

    public static void AfterValidate(DewProfile profile, GuardState before)
    {
        if (profile == null) return;
        try
        {
            bool restored = false;
            if (before != null && before.hadCustomData)
            {
                SavedState after = Capture(profile);
                if (!ContainsAllCustomData(after, before.data))
                {
                    Restore(profile, before.data);
                    RestoreDustOnlyIfIncreased(profile, before.dust);
                    restored = true;
                    DariusLog.Warn("STAR-REFUND-GUARD", "DewProfile.Validate removed Darius constellation data; restored purchases/loadout and neutralized any Stardust refund.");
                }
            }

            SavedState stable = Capture(profile);
            if (HasCustomData(stable) || restored || _bootRestoreComplete) SaveToDisk(stable);
        }
        catch (Exception e) { DariusLog.Exception("STAR-PERSIST", e, "Darius post-Validate persistence guard failed"); }
    }

    // Called around the first explicit profile repair. If the game performed a Validate before this
    // mod registered its StarEffects, recover the previous session and clamp only a suspicious
    // Stardust increase. A lower/equal current balance is never increased.
    public static void RestoreBootBackup(DewProfile profile)
    {
        if (profile == null) return;
        try
        {
            EnsureDiskLoaded();
            if (_diskState == null || !HasCustomData(_diskState)) { _bootRestoreComplete = true; return; }

            if (_bootRestoreComplete) return;
            SavedState current = Capture(profile);
            if (ContainsAllCustomData(current, _diskState)) { _bootRestoreComplete = true; return; }

            Restore(profile, _diskState);
            RestoreDustOnlyIfIncreased(profile, _diskState.dust);
            if (!_bootRestoreLogged)
            {
                _bootRestoreLogged = true;
                DariusLog.Warn("STAR-PERSIST", "Recovered Darius constellation purchases/loadout from boot backup after early profile cleanup; suspicious Stardust refund was clamped.");
            }
            SavedState check = Capture(profile);
            if (ContainsAllCustomData(check, _diskState)) _bootRestoreComplete = true;
        }
        catch (Exception e) { DariusLog.Exception("STAR-PERSIST", e, "Darius boot constellation restore failed"); }
    }

    public static void SaveCurrent(DewProfile profile, string reason)
    {
        if (profile == null) return;
        try
        {
            SavedState state = Capture(profile);
            // ModBehaviour teardown can run after DewProfile has already removed dynamic stars.
            // Never replace a known-good backup with that transient empty/partial snapshot.
            if (!HasCustomData(state))
            {
                DariusLog.DebugInfo("STAR-PERSIST", "Skipped invalid empty constellation snapshot reason=" + reason);
                return;
            }
            EnsureDiskLoaded();
            if (_diskState != null && !ContainsAllPurchasedData(state, _diskState))
            {
                DariusLog.Warn("STAR-PERSIST", "Skipped incomplete constellation snapshot; retained last valid backup reason=" + reason);
                return;
            }
            state.dust = CaptureDust(profile);
            SaveToDisk(state);
            DariusLog.DebugInfo("STAR-PERSIST", "Saved Darius constellation backup reason=" + reason + " pages=" + state.pages.Count + " purchased=" + state.purchased.Count);
        }
        catch (Exception e) { DariusLog.Exception("STAR-PERSIST", e, "Could not save Darius constellation backup reason=" + reason); }
    }

    private static SavedState Capture(DewProfile profile)
    {
        SavedState state = new SavedState();
        if (profile == null) return state;

        CapturePurchased(profile, state.purchased);
        CaptureLoadouts(profile, state.pages);
        state.unlockedSlotsJson = CaptureUnlockedSlots(profile);
        state.dust = CaptureDust(profile);
        return state;
    }
}