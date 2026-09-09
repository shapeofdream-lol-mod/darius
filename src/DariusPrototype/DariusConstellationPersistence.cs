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
public static class DariusConstellationPersistence
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

    private static void CapturePurchased(DewProfile profile, Dictionary<string, int> output)
    {
        IDictionary dict = ReadMember(profile, "newStars") as IDictionary;
        if (dict == null) return;
        foreach (DictionaryEntry entry in dict)
        {
            string key = entry.Key as string;
            if (string.IsNullOrEmpty(key) || !key.StartsWith(StarPrefix, StringComparison.Ordinal)) continue;
            try { output[key] = Math.Max(1, Convert.ToInt32(entry.Value)); } catch { output[key] = 1; }
        }
    }

    private static void CaptureLoadouts(DewProfile profile, List<SavedPage> output)
    {
        if (profile.heroLoadouts == null) return;
        List<HeroLoadoutData> pages;
        if (!profile.heroLoadouts.TryGetValue(HeroName, out pages) || pages == null) return;
        for (int i = 0; i < pages.Count; i++)
        {
            HeroLoadoutData loadout = pages[i];
            SavedPage page = new SavedPage();
            if (loadout != null)
            {
                CaptureBranch(loadout.cDestruction, page.destruction);
                CaptureBranch(loadout.cLife, page.life);
                CaptureBranch(loadout.cImagination, page.imagination);
                CaptureBranch(loadout.cFlexible, page.flexible);
            }
            output.Add(page);
        }
    }

    private static void CaptureBranch(List<LoadoutStarItem> branch, List<SavedSlot> output)
    {
        if (branch == null) return;
        for (int i = 0; i < branch.Count; i++)
        {
            LoadoutStarItem item = branch[i];
            if (string.IsNullOrEmpty(item.name) || !item.name.StartsWith(StarPrefix, StringComparison.Ordinal)) continue;
            output.Add(new SavedSlot { index = i, name = item.name, level = Math.Max(1, item.level) });
        }
    }

    private static string CaptureUnlockedSlots(DewProfile profile)
    {
        try
        {
            IDictionary dict = ReadMember(profile, "heroUnlockedStarSlots") as IDictionary;
            if (dict == null || !dict.Contains(HeroName)) return null;
            object value = dict[HeroName];
            return value != null ? JsonConvert.SerializeObject(value) : null;
        }
        catch { return null; }
    }

    private static void Restore(DewProfile profile, SavedState state)
    {
        if (profile == null || state == null) return;
        RestorePurchased(profile, state.purchased);
        RestoreUnlockedSlots(profile, state.unlockedSlotsJson);
        RestoreLoadouts(profile, state.pages);
    }

    private static void RestorePurchased(DewProfile profile, Dictionary<string, int> purchased)
    {
        if (purchased == null || purchased.Count == 0) return;
        IDictionary dict = ReadMember(profile, "newStars") as IDictionary;
        if (dict == null) return;
        Type valueType = GetDictionaryValueType(dict.GetType());
        foreach (KeyValuePair<string, int> kv in purchased)
        {
            try { dict[kv.Key] = ConvertScalar(kv.Value, valueType); } catch { }
        }
    }

    private static void RestoreUnlockedSlots(DewProfile profile, string json)
    {
        if (string.IsNullOrEmpty(json)) return;
        try
        {
            IDictionary dict = ReadMember(profile, "heroUnlockedStarSlots") as IDictionary;
            if (dict == null) return;
            Type valueType = GetDictionaryValueType(dict.GetType());
            object value = JsonConvert.DeserializeObject(json, valueType);
            if (value != null) dict[HeroName] = value;
        }
        catch (Exception e) { DariusLog.DebugInfo("STAR-PERSIST", "Unlocked-slot restore fallback: " + e.GetType().Name); }
    }

    private static void RestoreLoadouts(DewProfile profile, List<SavedPage> savedPages)
    {
        if (profile.heroLoadouts == null || savedPages == null || savedPages.Count == 0) return;
        List<HeroLoadoutData> pages;
        if (!profile.heroLoadouts.TryGetValue(HeroName, out pages) || pages == null) return;
        int count = Math.Min(pages.Count, savedPages.Count);
        for (int i = 0; i < count; i++)
        {
            HeroLoadoutData loadout = pages[i];
            SavedPage saved = savedPages[i];
            if (loadout == null || saved == null) continue;
            RestoreBranch(loadout.cDestruction, saved.destruction);
            RestoreBranch(loadout.cLife, saved.life);
            RestoreBranch(loadout.cImagination, saved.imagination);
            RestoreBranch(loadout.cFlexible, saved.flexible);
        }
    }

    private static void RestoreBranch(List<LoadoutStarItem> branch, List<SavedSlot> saved)
    {
        if (branch == null || saved == null) return;
        // Remove only Darius entries. Global/stock stars are never touched.
        for (int i = 0; i < branch.Count; i++)
            if (!string.IsNullOrEmpty(branch[i].name) && branch[i].name.StartsWith(StarPrefix, StringComparison.Ordinal))
                branch[i] = default(LoadoutStarItem);

        for (int i = 0; i < saved.Count; i++)
        {
            SavedSlot slot = saved[i];
            if (slot == null || string.IsNullOrEmpty(slot.name)) continue;
            int index = slot.index;
            if (index < 0 || index >= branch.Count || !string.IsNullOrEmpty(branch[index].name))
            {
                index = -1;
                for (int j = 0; j < branch.Count; j++) if (string.IsNullOrEmpty(branch[j].name)) { index = j; break; }
            }
            if (index < 0) continue;
            LoadoutStarItem item = default(LoadoutStarItem);
            item.name = slot.name;
            item.level = Math.Max(1, slot.level);
            branch[index] = item;
        }
    }

    private static bool HasCustomData(SavedState state)
    {
        if (state == null) return false;
        if (state.purchased != null && state.purchased.Count > 0) return true;
        if (state.pages != null)
            for (int i = 0; i < state.pages.Count; i++)
            {
                SavedPage p = state.pages[i];
                if (p != null && (p.destruction.Count + p.life.Count + p.imagination.Count + p.flexible.Count) > 0) return true;
            }
        return false;
    }

    private static bool ContainsAllCustomData(SavedState current, SavedState expected)
    {
        if (expected == null) return true;
        if (current == null) return false;
        if (expected.purchased != null)
            foreach (KeyValuePair<string, int> kv in expected.purchased)
            {
                int level;
                if (current.purchased == null || !current.purchased.TryGetValue(kv.Key, out level) || level < kv.Value) return false;
            }

        if (expected.pages != null)
        {
            if (current.pages == null || current.pages.Count < expected.pages.Count) return false;
            for (int i = 0; i < expected.pages.Count; i++)
            {
                if (!PageContains(current.pages[i], expected.pages[i])) return false;
            }
        }
        return true;
    }

    private static bool ContainsAllPurchasedData(SavedState current, SavedState expected)
    {
        if (expected == null || expected.purchased == null || expected.purchased.Count == 0) return true;
        if (current == null || current.purchased == null) return false;
        foreach (KeyValuePair<string, int> kv in expected.purchased)
        {
            int level;
            if (!current.purchased.TryGetValue(kv.Key, out level) || level < kv.Value) return false;
        }
        return true;
    }

    private static bool PageContains(SavedPage current, SavedPage expected)
    {
        if (expected == null) return true;
        if (current == null) return false;
        return BranchContains(current.destruction, expected.destruction) && BranchContains(current.life, expected.life) &&
               BranchContains(current.imagination, expected.imagination) && BranchContains(current.flexible, expected.flexible);
    }

    private static bool BranchContains(List<SavedSlot> current, List<SavedSlot> expected)
    {
        if (expected == null || expected.Count == 0) return true;
        if (current == null) return false;
        for (int i = 0; i < expected.Count; i++)
        {
            SavedSlot e = expected[i]; bool found = false;
            for (int j = 0; j < current.Count; j++)
                if (current[j].name == e.name && current[j].level >= e.level) { found = true; break; }
            if (!found) return false;
        }
        return true;
    }

    private static Dictionary<string, double> CaptureDust(DewProfile profile)
    {
        Dictionary<string, double> result = new Dictionary<string, double>(StringComparer.Ordinal);
        if (profile == null) return result;
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        for (Type t = profile.GetType(); t != null; t = t.BaseType)
        {
            foreach (FieldInfo f in t.GetFields(flags))
            {
                if (!IsDustName(f.Name) || !IsNumeric(f.FieldType)) continue;
                try { result["F:" + t.FullName + ":" + f.Name] = Convert.ToDouble(f.GetValue(profile)); } catch { }
            }
            foreach (PropertyInfo p in t.GetProperties(flags))
            {
                if (!p.CanRead || !p.CanWrite || p.GetIndexParameters().Length != 0 || !IsDustName(p.Name) || !IsNumeric(p.PropertyType)) continue;
                try { result["P:" + t.FullName + ":" + p.Name] = Convert.ToDouble(p.GetValue(profile, null)); } catch { }
            }
        }
        return result;
    }

    private static void RestoreDustOnlyIfIncreased(DewProfile profile, Dictionary<string, double> before)
    {
        if (profile == null || before == null || before.Count == 0) return;
        foreach (KeyValuePair<string, double> kv in before)
        {
            MemberInfo member = ResolveDustMember(profile.GetType(), kv.Key);
            if (member == null) continue;
            try
            {
                object currentObj = GetMemberValue(profile, member);
                if (currentObj == null) continue;
                double current = Convert.ToDouble(currentObj);
                // Never grant currency. Only undo an increase that happened while custom star data
                // simultaneously disappeared, which is the observed refund exploit path.
                if (current > kv.Value + 0.0001) SetMemberValue(profile, member, kv.Value);
            }
            catch { }
        }
    }

    private static bool IsDustName(string name)
    {
        return !string.IsNullOrEmpty(name) && (name.IndexOf("stardust", StringComparison.OrdinalIgnoreCase) >= 0 || string.Equals(name, "dust", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsNumeric(Type t)
    {
        if (t == null) return false;
        TypeCode c = Type.GetTypeCode(Nullable.GetUnderlyingType(t) ?? t);
        return c == TypeCode.Byte || c == TypeCode.SByte || c == TypeCode.Int16 || c == TypeCode.UInt16 ||
               c == TypeCode.Int32 || c == TypeCode.UInt32 || c == TypeCode.Int64 || c == TypeCode.UInt64 ||
               c == TypeCode.Single || c == TypeCode.Double || c == TypeCode.Decimal;
    }

    private static MemberInfo ResolveDustMember(Type root, string key)
    {
        if (root == null || string.IsNullOrEmpty(key)) return null;
        string[] parts = key.Split(new[] { ':' }, 3);
        if (parts.Length != 3) return null;
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        for (Type t = root; t != null; t = t.BaseType)
        {
            if (!string.Equals(t.FullName, parts[1], StringComparison.Ordinal)) continue;
            return parts[0] == "F" ? (MemberInfo)t.GetField(parts[2], flags) : t.GetProperty(parts[2], flags);
        }
        return null;
    }

    private static object GetMemberValue(object target, MemberInfo member)
    {
        FieldInfo f = member as FieldInfo; if (f != null) return f.GetValue(target);
        PropertyInfo p = member as PropertyInfo; return p != null ? p.GetValue(target, null) : null;
    }

    private static void SetMemberValue(object target, MemberInfo member, double value)
    {
        FieldInfo f = member as FieldInfo;
        if (f != null && !f.IsInitOnly) { f.SetValue(target, ConvertScalar(value, f.FieldType)); return; }
        PropertyInfo p = member as PropertyInfo;
        if (p != null && p.CanWrite) p.SetValue(target, ConvertScalar(value, p.PropertyType), null);
    }

    private static object ReadMember(object target, string name)
    {
        if (target == null) return null;
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        for (Type t = target.GetType(); t != null; t = t.BaseType)
        {
            try
            {
                FieldInfo f = t.GetField(name, flags); if (f != null) return f.GetValue(target);
                PropertyInfo p = t.GetProperty(name, flags); if (p != null && p.CanRead && p.GetIndexParameters().Length == 0) return p.GetValue(target, null);
            }
            catch { }
        }
        return null;
    }

    private static Type GetDictionaryValueType(Type t)
    {
        if (t != null && t.IsGenericType)
        {
            Type[] args = t.GetGenericArguments(); if (args.Length == 2) return args[1];
        }
        return typeof(int);
    }

    private static object ConvertScalar(object value, Type targetType)
    {
        Type t = Nullable.GetUnderlyingType(targetType) ?? targetType;
        return Convert.ChangeType(value, t);
    }

    private static void EnsureDiskLoaded()
    {
        if (_diskLoaded) return;
        _diskLoaded = true;
        try
        {
            if (!File.Exists(SavePath)) return;
            _diskState = JsonConvert.DeserializeObject<SavedState>(File.ReadAllText(SavePath));
            if (_diskState != null && _diskState.version != FileVersion) _diskState = null;
        }
        catch (Exception e) { DariusLog.Exception("STAR-PERSIST", e, "Could not load Darius constellation backup"); _diskState = null; }
    }

    private static void SaveToDisk(SavedState state)
    {
        if (state == null) return;
        try
        {
            string path = SavePath;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string tmp = path + ".tmp";
            File.WriteAllText(tmp, JsonConvert.SerializeObject(state, Formatting.Indented));
            if (File.Exists(path)) File.Delete(path);
            File.Move(tmp, path);
            _diskState = state;
            _diskLoaded = true;
        }
        catch (Exception e) { DariusLog.Exception("STAR-PERSIST", e, "Could not write Darius constellation backup"); }
    }
}
