using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

// RepairDariusConstellationLoadout performs schema migrations between constellation branches.
// Older migration code clears the source slot when the destination branch is full, which can
// silently unequip a valid purchased star. Keep the migration itself native to the registry, but
// wrap it transactionally: a Darius star may move branches, but it must never disappear entirely.
public static class DariusConstellationMigrationGuard
{
    private const string HeroName = DariusTravelerRegistry.HeroName;
    private const string StarPrefix = "Se_Star_Darius_";
    private static bool _installed;

    private enum Branch : byte
    {
        Destruction,
        Life,
        Imagination,
        Flexible
    }

    private sealed class SavedStar
    {
        public int page;
        public Branch branch;
        public int index;
        public LoadoutStarItem item;
    }

    private sealed class RepairSnapshot
    {
        public readonly List<SavedStar> stars = new List<SavedStar>();
    }

    public static bool Install(Harmony harmony)
    {
        if (_installed) return true;
        if (harmony == null) return false;

        try
        {
            MethodInfo target = AccessTools.Method(
                typeof(DariusTravelerRegistry),
                "RepairDariusConstellationLoadout",
                new[] { typeof(DewProfile) });
            MethodInfo prefix = AccessTools.Method(typeof(DariusConstellationMigrationGuard), nameof(BeforeRepair));
            MethodInfo postfix = AccessTools.Method(typeof(DariusConstellationMigrationGuard), nameof(AfterRepair));
            if (target == null || prefix == null || postfix == null)
            {
                DariusLog.Warn("CONSTELLATION-MIGRATE", "Could not install migration transaction guard; repair method signature was not found.");
                return false;
            }

            harmony.Patch(target, prefix: new HarmonyMethod(prefix), postfix: new HarmonyMethod(postfix));
            _installed = true;
            DariusLog.Info("CONSTELLATION-MIGRATE", "Installed loadout migration transaction guard.");
            return true;
        }
        catch (Exception e)
        {
            DariusLog.Exception("CONSTELLATION-MIGRATE", e, "Could not install migration transaction guard");
            return false;
        }
    }

    private static void BeforeRepair(DewProfile __0, out RepairSnapshot __state)
    {
        __state = Capture(__0);
    }

    private static void AfterRepair(DewProfile __0, RepairSnapshot __state)
    {
        if (__0 == null || __state == null || __state.stars.Count == 0 || __0.heroLoadouts == null) return;

        List<HeroLoadoutData> pages;
        if (!__0.heroLoadouts.TryGetValue(HeroName, out pages) || pages == null) return;

        int restored = 0;
        for (int i = 0; i < __state.stars.Count; i++)
        {
            SavedStar saved = __state.stars[i];
            if (saved == null || string.IsNullOrEmpty(saved.item.name)) continue;
            if (saved.page < 0 || saved.page >= pages.Count) continue;

            HeroLoadoutData loadout = pages[saved.page];
            if (loadout == null || ContainsStar(loadout, saved.item.name)) continue;

            List<LoadoutStarItem> source = GetBranch(loadout, saved.branch);
            if (source == null) continue;

            int slot = saved.index;
            if (slot < 0 || slot >= source.Count || !string.IsNullOrEmpty(source[slot].name))
            {
                slot = -1;
                for (int j = 0; j < source.Count; j++)
                {
                    if (!string.IsNullOrEmpty(source[j].name)) continue;
                    slot = j;
                    break;
                }
            }

            if (slot < 0)
            {
                DariusLog.Error("CONSTELLATION-MIGRATE", "Migration removed star=" + saved.item.name +
                    " from page=" + saved.page + " but its original branch has no free slot for rollback.");
                continue;
            }

            source[slot] = saved.item;
            restored++;
            DariusLog.Warn("CONSTELLATION-MIGRATE", "Rolled back lossy migration for star=" + saved.item.name +
                " page=" + saved.page + " branch=" + saved.branch + " level=" + saved.item.level +
                "; destination branch had no capacity, so the original equipped selection was preserved.");
        }

        if (restored > 0)
            DariusLog.Info("CONSTELLATION-MIGRATE", "Migration transaction guard restored " + restored + " equipped Darius star(s) that would otherwise have disappeared.");
    }

    private static RepairSnapshot Capture(DewProfile profile)
    {
        RepairSnapshot snapshot = new RepairSnapshot();
        if (profile == null || profile.heroLoadouts == null) return snapshot;

        List<HeroLoadoutData> pages;
        if (!profile.heroLoadouts.TryGetValue(HeroName, out pages) || pages == null) return snapshot;

        for (int page = 0; page < pages.Count; page++)
        {
            HeroLoadoutData loadout = pages[page];
            if (loadout == null) continue;
            CaptureBranch(snapshot, page, Branch.Destruction, loadout.cDestruction);
            CaptureBranch(snapshot, page, Branch.Life, loadout.cLife);
            CaptureBranch(snapshot, page, Branch.Imagination, loadout.cImagination);
            CaptureBranch(snapshot, page, Branch.Flexible, loadout.cFlexible);
        }
        return snapshot;
    }

    private static void CaptureBranch(RepairSnapshot snapshot, int page, Branch branch, List<LoadoutStarItem> items)
    {
        if (snapshot == null || items == null) return;
        for (int i = 0; i < items.Count; i++)
        {
            LoadoutStarItem item = items[i];
            if (string.IsNullOrEmpty(item.name) || !item.name.StartsWith(StarPrefix, StringComparison.Ordinal)) continue;
            snapshot.stars.Add(new SavedStar { page = page, branch = branch, index = i, item = item });
        }
    }

    private static bool ContainsStar(HeroLoadoutData loadout, string name)
    {
        return ContainsStar(loadout.cDestruction, name) ||
               ContainsStar(loadout.cLife, name) ||
               ContainsStar(loadout.cImagination, name) ||
               ContainsStar(loadout.cFlexible, name);
    }

    private static bool ContainsStar(List<LoadoutStarItem> items, string name)
    {
        if (items == null || string.IsNullOrEmpty(name)) return false;
        for (int i = 0; i < items.Count; i++)
            if (string.Equals(items[i].name, name, StringComparison.Ordinal)) return true;
        return false;
    }

    private static List<LoadoutStarItem> GetBranch(HeroLoadoutData loadout, Branch branch)
    {
        if (loadout == null) return null;
        switch (branch)
        {
            case Branch.Destruction: return loadout.cDestruction;
            case Branch.Life: return loadout.cLife;
            case Branch.Imagination: return loadout.cImagination;
            case Branch.Flexible: return loadout.cFlexible;
            default: return null;
        }
    }
}
