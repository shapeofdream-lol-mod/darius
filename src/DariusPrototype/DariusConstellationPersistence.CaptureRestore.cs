public static partial class DariusConstellationPersistence
{
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
}