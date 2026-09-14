public static partial class DariusConstellationPersistence
{
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
}