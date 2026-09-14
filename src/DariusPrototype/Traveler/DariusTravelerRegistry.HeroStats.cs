public static partial class DariusTravelerRegistry
{
    private static void RepairHeroGrowthStatContract(Hero source, Hero_Darius target)
    {
        if (source == null || target == null) return;
        int copied = 0;
        int cloned = 0;
        int tuned = 0;
        copied += CopySameNamedMember(source, target, "scalingStats");
        EntityStatus sourceStatus = source.GetComponent<EntityStatus>();
        EntityStatus targetStatus = target.GetComponent<EntityStatus>();
        if (sourceStatus != null && targetStatus != null)
            copied += CopySameNamedMember(sourceStatus, targetStatus, "scalingStats");

        object heroScaling = CloneMemberValue(source, "scalingStats");
        if (heroScaling != null)
        {
            tuned += ApplyDariusGrowthStatTuning(heroScaling);
            if (TryAssignCount(target, "scalingStats", heroScaling)) cloned++;
        }

        if (sourceStatus != null && targetStatus != null)
        {
            object statusScaling = CloneMemberValue(sourceStatus, "scalingStats") ?? CloneMemberValue(source, "scalingStats");
            if (statusScaling != null)
            {
                tuned += ApplyDariusGrowthStatTuning(statusScaling);
                if (TryAssignCount(targetStatus, "scalingStats", statusScaling)) cloned++;
            }
        }

        DariusLog.Info("STAT-CONTRACT", "Hero_Darius growth-stat contract normalized from native Hero_Vesper curve copiedMembers=" + copied +
            " clonedMembers=" + cloned + " tunedFields=" + tuned +
            " profile=juggernaut growth(AD +3, AP +0, HP +25%, Armor +2)");
    }

    private static int ApplyDariusGrowthStatTuning(object scalingStats)
    {
        if (scalingStats == null) return 0;
        int changed = 0;
        changed += TryScaleNumericMember(scalingStats, new[] { "attackDamageFlat" }, 1.50f);
        changed += TryScaleNumericMember(scalingStats, new[] { "abilityPowerFlat" }, 0.00f);
        changed += TryScaleNumericMember(scalingStats, new[] { "maxHealthPercentage" }, 1.00f);
        changed += TryScaleNumericMember(scalingStats, new[] { "armorFlat" }, 1.00f);
        return changed;
    }

    private static object CloneMemberValue(object owner, string name)
    {
        object value = ReadMemberValue(owner, name);
        return ClonePlainObject(value);
    }

    private static object ReadMemberValue(object owner, string name)
    {
        if (owner == null || string.IsNullOrEmpty(name)) return null;
        BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        FieldInfo f = FindFieldRecursive(owner.GetType(), name) ?? FindFieldRecursive(owner.GetType(), "<" + name + ">k__BackingField");
        if (f != null)
        {
            try { return f.GetValue(owner); } catch { }
        }
        PropertyInfo p = owner.GetType().GetProperty(name, flags);
        if (p != null && p.CanRead)
        {
            try { return p.GetValue(owner, null); } catch { }
        }
        return null;
    }

    private static object ClonePlainObject(object source)
    {
        if (source == null) return null;
        Type type = source.GetType();
        if (type.IsValueType || type == typeof(string)) return source;
        try
        {
            object clone;
            try { clone = Activator.CreateInstance(type, true); }
            catch { clone = System.Runtime.Serialization.FormatterServices.GetUninitializedObject(type); }
            for (Type t = type; t != null; t = t.BaseType)
            {
                foreach (FieldInfo field in t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    if (field.IsStatic) continue;
                    try { field.SetValue(clone, field.GetValue(source)); } catch { }
                }
            }
            return clone;
        }
        catch (Exception e)
        {
            DariusLog.Exception("STAT-CONTRACT", e, "Could not clone member object type=" + type.FullName);
            return null;
        }
    }

    private static int ApplyDariusBaseStatTuning(object baseStats)
    {
        if (baseStats == null) return 0;
        int changed = 0;
        changed += TryScaleNumericMember(baseStats, new[] { "maxHealth", "health", "hp" }, 1.12f);
        changed += TryScaleNumericMember(baseStats, new[] { "attackDamage", "damage", "physicalDamage", "attackPower" }, 1.10f);
        changed += TryScaleNumericMember(baseStats, new[] { "armor", "defense", "physicalDefence", "physicalDefense" }, 1.08f);
        changed += TryScaleNumericMember(baseStats, new[] { "moveSpeed", "movementSpeed", "speed" }, 0.97f);
        return changed;
    }

    private static int TryScaleNumericMember(object owner, string[] names, float scale)
    {
        if (owner == null || names == null) return 0;
        for (int i = 0; i < names.Length; i++)
        {
            if (TryScaleNumericMember(owner, names[i], scale)) return 1;
        }
        return 0;
    }

    private static bool TryScaleNumericMember(object owner, string name, float scale)
    {
        if (owner == null || string.IsNullOrEmpty(name)) return false;
        BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        FieldInfo field = FindFieldRecursive(owner.GetType(), name) ?? FindFieldRecursive(owner.GetType(), "<" + name + ">k__BackingField");
        if (field != null && !field.IsInitOnly)
            return TryScaleNumericTarget(owner, field.FieldType, () => field.GetValue(owner), v => field.SetValue(owner, v), scale, name);
        PropertyInfo property = owner.GetType().GetProperty(name, flags);
        if (property != null && property.CanRead && property.CanWrite)
            return TryScaleNumericTarget(owner, property.PropertyType, () => property.GetValue(owner, null), v => property.SetValue(owner, v, null), scale, name);
        return false;
    }

    private static bool TryScaleNumericTarget(object owner, Type memberType, Func<object> getter, Action<object> setter, float scale, string name)
    {
        try
        {
            object raw = getter();
            if (raw == null) return false;
            if (memberType == typeof(float))
            {
                float current = (float)raw;
                setter(Mathf.Round(current * scale * 100f) / 100f);
                return true;
            }
            if (memberType == typeof(double))
            {
                double current = (double)raw;
                setter(Math.Round(current * scale, 2));
                return true;
            }
            if (memberType == typeof(int))
            {
                int current = (int)raw;
                setter(Mathf.RoundToInt(current * scale));
                return true;
            }
            if (memberType == typeof(long))
            {
                long current = (long)raw;
                setter((long)Math.Round(current * scale));
                return true;
            }
        }
        catch (Exception e)
        {
            DariusLog.DebugInfo("STAT-CONTRACT", "Failed scaling base stat member " + owner.GetType().Name + "." + name + ": " + e.Message);
        }
        return false;
    }
}