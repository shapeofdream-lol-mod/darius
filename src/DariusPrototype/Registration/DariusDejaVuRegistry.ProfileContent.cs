using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

public static partial class DariusDejaVuRegistry
{
    public static void RegisterContentSettings(DewGameContentSettings content)
    {
        // Traveler owns the single content-registration path for Hero_Darius and all of its skills.
        // Keeping Deja Vu on that path avoids maintaining a second reflective content injector.
        DariusTravelerRegistry.RegisterContent(content);
    }

    public static void RegisterCollectables()
    {
        // There is no public custom-type registration API in the current SoD surface. Keep this
        // unsupported operation isolated here until the custom resource identity bridge is replaced.
        try { _ = Dew.allSkills; } catch { }
        AddTypesToDewBackingList("_allSkills", SkillTypes);

        DariusLog.Info("DEJAVU", "Collectables type cache includes 4 combat Memories + 1 Hero_Darius Identity Memory.");
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

    private static void AddTypesToDewBackingList(string fieldName, IEnumerable<Type> types)
    {
        try
        {
            FieldInfo field = typeof(Dew).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);
            if (field == null)
            {
                DariusLog.Warn("DEJAVU", "Dew backing field not found: " + fieldName);
                return;
            }

            IList list = field.GetValue(null) as IList;
            if (list == null)
            {
                DariusLog.Warn("DEJAVU", "Dew backing list unavailable: " + fieldName);
                return;
            }

            Type[] currentTypes = types != null ? types.Where(t => t != null).ToArray() : Array.Empty<Type>();
            HashSet<string> currentNames = new HashSet<string>(currentTypes.Select(t => t.Name), StringComparer.Ordinal);

            int removed = 0;
            for (int i = list.Count - 1; i >= 0; i--)
            {
                Type existing = list[i] as Type;
                if (existing == null || !currentNames.Contains(existing.Name)) continue;
                if (!currentTypes.Contains(existing))
                {
                    list.RemoveAt(i);
                    removed++;
                }
            }

            int added = 0;
            foreach (Type type in currentTypes)
            {
                if (!list.Contains(type)) { list.Add(type); added++; }
            }
            DariusLog.DebugInfo("DEJAVU", fieldName + " count=" + list.Count + " added=" + added + " staleRemoved=" + removed);
        }
        catch (Exception e) { DariusLog.Exception("DEJAVU", e, "Adding types to " + fieldName + " failed"); }
    }
}
