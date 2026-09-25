using System.Collections.Generic;

public static partial class DariusDejaVuRegistry
{
    public static void RegisterContentSettings(DewGameContentSettings content)
    {
        DariusTravelerRegistry.RegisterContent(content);
    }

    public static void RegisterCollectables()
    {
        // DariusTravelerRegistry owns the single unsupported type-cache registration.
        DariusLog.Info("DEJAVU", "Collectables use the canonical Darius skill type-cache registration.");
    }

    public static void RegisterProfileStats(DewProfileStats stats)
    {
        if (stats == null)
        {
            DariusLog.Warn("DEJAVU", "DewProfileStats unavailable; Deja Vu qualification registration deferred.");
            return;
        }

        if (stats.skills == null)
            stats.skills = new Dictionary<string, DewProfileStats.ItemData>();

        foreach (string skillName in SkillNames)
        {
            if (!stats.skills.ContainsKey(skillName))
                stats.skills.Add(skillName, new DewProfileStats.ItemData());
        }

        DariusLog.Info("DEJAVU", "ProfileStats registration complete. skills=" + stats.skills.Count +
            " (Hemorrhage registered as Identity skill; no fabricated wins/playCount).");
    }
}
