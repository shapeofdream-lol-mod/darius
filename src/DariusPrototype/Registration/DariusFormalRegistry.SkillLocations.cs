public static partial class DariusFormalRegistry
{
    private static void SetSkillLocation(SkillTrigger skill, string locationName)
    {
        if (skill == null || string.IsNullOrEmpty(locationName)) return;
        try
        {
            FieldInfo field = typeof(SkillTrigger).GetField("<skillType>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance);
            if (field == null)
            {
                DariusLog.Warn("CONFIG", "SkillTrigger skillType backing field not found for " + skill.name);
                return;
            }
            object value;
            if (field.FieldType.IsEnum)
            {
                string match = Enum.GetNames(field.FieldType).FirstOrDefault(n => string.Equals(n, locationName, StringComparison.OrdinalIgnoreCase));
                if (string.IsNullOrEmpty(match))
                {
                    DariusLog.Warn("CONFIG", "HeroSkillLocation value not found: " + locationName + " for " + skill.name);
                    return;
                }
                value = Enum.Parse(field.FieldType, match, true);
            }
            else
            {
                int numeric = string.Equals(locationName, "Q", StringComparison.OrdinalIgnoreCase) ? 0 :
                              string.Equals(locationName, "W", StringComparison.OrdinalIgnoreCase) ? 1 :
                              string.Equals(locationName, "E", StringComparison.OrdinalIgnoreCase) ? 2 :
                              string.Equals(locationName, "R", StringComparison.OrdinalIgnoreCase) ? 3 :
                              string.Equals(locationName, "Identity", StringComparison.OrdinalIgnoreCase) ? 4 :
                              string.Equals(locationName, "Movement", StringComparison.OrdinalIgnoreCase) ? 5 : 0;
                value = numeric;
            }
            field.SetValue(skill, value);
            DariusLog.Info("CONFIG", "Assigned " + skill.name + " to HeroSkillLocation." + locationName + " value=" + value);
        }
        catch (Exception e)
        {
            DariusLog.Exception("CONFIG", e, "Failed to set skill location " + locationName + " for " + skill.name);
        }
    }

    private static void LogSkillPrefabState(string label, SkillTrigger skill)
    {
        if (skill == null)
        {
            DariusLog.Error("DROP-CONFIG", label + " prefab=<null>");
            return;
        }

        string cfgState = skill.configs == null ? "<null>" : skill.configs.Length.ToString();
        string cfg0 = skill.configs != null && skill.configs.Length > 0 && skill.configs[0] != null ? "ok" : "null";
        string cfg1 = skill.configs != null && skill.configs.Length > 1 && skill.configs[1] != null ? "ok" : "null";
        DariusLog.Info("DROP-CONFIG", label + " prefab=" + skill.name +
            " configs=" + cfgState + " cfg0=" + cfg0 + " cfg1=" + cfg1 + " level=" + skill.level);
    }

    private static void TrySpawnSkill<T>(string label, T prefab, Vector3 pos, DewPlayer player) where T : SkillTrigger
    {
        try
        {
            Dew.CreateSkillTrigger<T>(prefab, pos, 1, player, null);
            DariusLog.Info("DROP-" + label, "CreateSkillTrigger call completed without exception pos=" + DariusLog.Vec(pos));
        }
        catch (Exception e)
        {
            DariusLog.Exception("DROP-" + label, e, "CreateSkillTrigger failed at " + DariusLog.Vec(pos));
        }
    }

    private static void TrySpawnGem(string label, Gem_Darius_Hemorrhage prefab, Vector3 pos, DewPlayer player)
    {
        try
        {
            Dew.CreateGem<Gem_Darius_Hemorrhage>(prefab, pos, 1, player, null);
            DariusLog.Info("DROP-" + label, "CreateGem call completed without exception pos=" + DariusLog.Vec(pos));
        }
        catch (Exception e)
        {
            DariusLog.Exception("DROP-" + label, e, "CreateGem failed at " + DariusLog.Vec(pos));
        }
    }

    private static Vector3 GoodDropPosition(Vector3 center, float radius)
    {
        try { return Dew.GetGoodRewardPosition(center, radius); }
        catch
        {
            Vector3 random = UnityEngine.Random.insideUnitSphere;
            random.y = 0f;
            if (random.sqrMagnitude < 0.001f) random = Vector3.forward;
            return center + random.normalized * radius;
        }
    }
}