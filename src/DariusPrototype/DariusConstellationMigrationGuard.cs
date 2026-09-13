using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

// RepairDariusConstellationLoadout migrates legacy equipped selections between constellation
// branches. The profile purchase record and the equipped loadout are separate concerns: this guard
// keeps every migrated selection on its current schema branch when a legal slot exists, but never
// writes a star back into an obsolete branch merely to keep it equipped. If the destination branch
// is genuinely full, the star remains purchased and is intentionally left unequipped.
public static class DariusConstellationMigrationGuard
{
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
        public Branch targetBranch;
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
                DariusLog.Warn("CONSTELLATION-MIGRATE", "Could not install migration guard; repair method signature was not found.");
                return false;
            }

            harmony.Patch(target, prefix: new HarmonyMethod(prefix), postfix: new HarmonyMethod(postfix));
            _installed = true;
            DariusLog.Info("CONSTELLATION-MIGRATE", "Installed schema-safe loadout migration guard.");
            return true;
        }
        catch (Exception e)
        {
            DariusLog.Exception("CONSTELLATION-MIGRATE", e, "Could not install migration guard");
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
        if (!__0.heroLoadouts.TryGetValue(DariusTravelerRegistry.HeroName, out pages) || pages == null) return;

        int repaired = 0;
        int overflowed = 0;
        for (int i = 0; i < __state.stars.Count; i++)
        {
            SavedStar saved = __state.stars[i];
            if (saved == null || string.IsNullOrEmpty(saved.item.name)) continue;
            if (saved.page < 0 || saved.page >= pages.Count) continue;

            HeroLoadoutData loadout = pages[saved.page];
            if (loadout == null) continue;

            Branch currentBranch;
            int currentIndex;
            LoadoutStarItem currentItem;
            if (TryFindStar(loadout, saved.item.name, out currentBranch, out currentIndex, out currentItem))
            {
                if (currentBranch == saved.targetBranch) continue;

                List<LoadoutStarItem> target = GetBranch(loadout, saved.targetBranch);
                int empty = FindEmptySlot(target);
                List<LoadoutStarItem> current = GetBranch(loadout, currentBranch);
                if (empty >= 0 && current != null && currentIndex >= 0 && currentIndex < current.Count)
                {
                    target[empty] = currentItem;
                    current[currentIndex] = default(LoadoutStarItem);
                    repaired++;
                    DariusLog.Warn("CONSTELLATION-MIGRATE", "Corrected legacy branch placement for star=" + saved.item.name +
                        " page=" + saved.page + " from=" + currentBranch + " to=" + saved.targetBranch + ".");
                    continue;
                }

                // Keeping the selection in an obsolete branch would create a loadout whose branch no
                // longer matches the registered StarType. Prefer a legal unequipped purchase instead.
                if (current != null && currentIndex >= 0 && currentIndex < current.Count)
                    current[currentIndex] = default(LoadoutStarItem);
                overflowed++;
                LogOverflow(saved, "legacy selection was still on " + currentBranch);
                continue;
            }

            // Native repair normally moved the star or intentionally cleared it because the target
            // branch was full. If a legal target slot still exists, restore directly into that target
            // branch only; never roll back to the obsolete source branch.
            List<LoadoutStarItem> destination = GetBranch(loadout, saved.targetBranch);
            int slot = FindEmptySlot(destination);
            if (slot >= 0)
            {
                destination[slot] = saved.item;
                repaired++;
                DariusLog.Warn("CONSTELLATION-MIGRATE", "Recovered migrated selection star=" + saved.item.name +
                    " page=" + saved.page + " directly into schema branch=" + saved.targetBranch +
                    " level=" + saved.item.level + ".");
                continue;
            }

            overflowed++;
            LogOverflow(saved, "destination branch has no free slot");
        }

        if (repaired > 0)
            DariusLog.Info("CONSTELLATION-MIGRATE", "Schema-safe migration guard repaired " + repaired + " equipped selection(s).");
        if (overflowed > 0)
            DariusLog.Warn("CONSTELLATION-MIGRATE", "Left " + overflowed +
                " migration overflow selection(s) unequipped because no legal destination slot existed; purchase records were not modified.");
    }

    private static void LogOverflow(SavedStar saved, string reason)
    {
        DariusLog.Warn("CONSTELLATION-MIGRATE", "Migration overflow star=" + saved.item.name +
            " page=" + saved.page + " target=" + saved.targetBranch + " level=" + saved.item.level +
            "; " + reason + ". The purchased star is preserved by the profile purchase record, but it cannot remain equipped in an obsolete branch.");
    }

    private static RepairSnapshot Capture(DewProfile profile)
    {
        RepairSnapshot snapshot = new RepairSnapshot();
        if (profile == null || profile.heroLoadouts == null) return snapshot;

        List<HeroLoadoutData> pages;
        if (!profile.heroLoadouts.TryGetValue(DariusTravelerRegistry.HeroName, out pages) || pages == null) return snapshot;

        for (int page = 0; page < pages.Count; page++)
        {
            HeroLoadoutData loadout = pages[page];
            if (loadout == null) continue;
            CaptureBranch(snapshot, page, loadout.cDestruction);
            CaptureBranch(snapshot, page, loadout.cLife);
            CaptureBranch(snapshot, page, loadout.cImagination);
            CaptureBranch(snapshot, page, loadout.cFlexible);
        }
        return snapshot;
    }

    private static void CaptureBranch(RepairSnapshot snapshot, int page, List<LoadoutStarItem> items)
    {
        if (snapshot == null || items == null) return;
        for (int i = 0; i < items.Count; i++)
        {
            LoadoutStarItem item = items[i];
            Branch target;
            if (string.IsNullOrEmpty(item.name) || !TryGetMigrationTarget(item.name, out target)) continue;
            snapshot.stars.Add(new SavedStar { page = page, targetBranch = target, item = item });
        }
    }

    private static bool TryGetMigrationTarget(string name, out Branch target)
    {
        target = Branch.Flexible;
        if (string.IsNullOrEmpty(name)) return false;

        if (name == DariusConstellationIds.ICripplingStrike ||
            name == DariusConstellationIds.IApprehend ||
            name == DariusConstellationIds.IInstantDecimate ||
            name == DariusConstellationIds.IWarFervor ||
            name == DariusConstellationIds.FCosmicInsight ||
            name == DariusConstellationIds.DAxiomArcanist ||
            name == DariusConstellationIds.LRevitalize)
        {
            target = Branch.Flexible;
            return true;
        }

        if (name == DariusConstellationIds.FNimbusCloak ||
            name == DariusConstellationIds.FCelerity ||
            name == DariusConstellationIds.FGatheringStorm)
        {
            target = Branch.Imagination;
            return true;
        }

        return false;
    }

    private static bool TryFindStar(HeroLoadoutData loadout, string name, out Branch branch, out int index, out LoadoutStarItem item)
    {
        branch = Branch.Destruction;
        index = -1;
        item = default(LoadoutStarItem);
        if (loadout == null || string.IsNullOrEmpty(name)) return false;

        if (TryFindStar(loadout.cDestruction, name, out index, out item)) { branch = Branch.Destruction; return true; }
        if (TryFindStar(loadout.cLife, name, out index, out item)) { branch = Branch.Life; return true; }
        if (TryFindStar(loadout.cImagination, name, out index, out item)) { branch = Branch.Imagination; return true; }
        if (TryFindStar(loadout.cFlexible, name, out index, out item)) { branch = Branch.Flexible; return true; }
        return false;
    }

    private static bool TryFindStar(List<LoadoutStarItem> items, string name, out int index, out LoadoutStarItem item)
    {
        index = -1;
        item = default(LoadoutStarItem);
        if (items == null) return false;
        for (int i = 0; i < items.Count; i++)
        {
            if (!string.Equals(items[i].name, name, StringComparison.Ordinal)) continue;
            index = i;
            item = items[i];
            return true;
        }
        return false;
    }

    private static int FindEmptySlot(List<LoadoutStarItem> items)
    {
        if (items == null) return -1;
        for (int i = 0; i < items.Count; i++)
            if (string.IsNullOrEmpty(items[i].name)) return i;
        return -1;
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
