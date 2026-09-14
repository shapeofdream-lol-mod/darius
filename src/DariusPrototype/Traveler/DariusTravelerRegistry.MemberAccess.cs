public static partial class DariusTravelerRegistry
{
    private static void AddStringMember(object owner, string memberName, string value)
    {
        if (owner == null || string.IsNullOrEmpty(value)) return;
        BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        FieldInfo f = owner.GetType().GetField(memberName, flags);
        PropertyInfo p = owner.GetType().GetProperty(memberName, flags);
        object current = null;
        Type memberType = null;
        if (f != null) { current = f.GetValue(owner); memberType = f.FieldType; }
        else if (p != null && p.CanRead) { current = p.GetValue(owner, null); memberType = p.PropertyType; }
        else return;

        IList list = current as IList;
        if (list != null)
        {
            foreach (object item in list) if (string.Equals(item as string, value, StringComparison.Ordinal)) return;
            if (!list.IsFixedSize) list.Add(value);
            else if (memberType != null && memberType.IsArray)
            {
                Array old = (Array)current;
                Array next = Array.CreateInstance(memberType.GetElementType(), old.Length + 1);
                Array.Copy(old, next, old.Length);
                next.SetValue(value, old.Length);
                if (f != null) f.SetValue(owner, next); else if (p.CanWrite) p.SetValue(owner, next, null);
            }
            return;
        }
        if (memberType != null && memberType.IsArray)
        {
            Array old = current as Array;
            int n = old != null ? old.Length : 0;
            for (int i = 0; i < n; i++) if (string.Equals(old.GetValue(i) as string, value, StringComparison.Ordinal)) return;
            Array next = Array.CreateInstance(memberType.GetElementType(), n + 1);
            if (old != null) Array.Copy(old, next, n);
            next.SetValue(value, n);
            if (f != null) f.SetValue(owner, next); else if (p != null && p.CanWrite) p.SetValue(owner, next, null);
        }
    }

    private static void SetEnumLikeMember(object obj, string name, string enumName)
    {
        if (obj == null) return;
        FieldInfo f = FindFieldRecursive(obj.GetType(), name) ?? FindFieldRecursive(obj.GetType(), "<" + name + ">k__BackingField");
        if (f != null && f.FieldType.IsEnum)
        {
            string match = Enum.GetNames(f.FieldType).FirstOrDefault(x => string.Equals(x, enumName, StringComparison.OrdinalIgnoreCase));
            if (match != null) f.SetValue(obj, Enum.Parse(f.FieldType, match));
            return;
        }
        PropertyInfo p = obj.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (p != null && p.CanWrite && p.PropertyType.IsEnum)
        {
            string match = Enum.GetNames(p.PropertyType).FirstOrDefault(x => string.Equals(x, enumName, StringComparison.OrdinalIgnoreCase));
            if (match != null) p.SetValue(obj, Enum.Parse(p.PropertyType, match), null);
        }
    }

    private static void TryAssignIfCompatible(object obj, string name, object value)
    {
        if (obj == null) return;
        Type t = obj.GetType();
        BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        while (t != null)
        {
            FieldInfo f = t.GetField(name, flags | BindingFlags.DeclaredOnly) ?? t.GetField("<" + name + ">k__BackingField", flags | BindingFlags.DeclaredOnly);
            if (f != null && !f.IsInitOnly && IsCompatible(f.FieldType, value)) { f.SetValue(obj, value); return; }
            t = t.BaseType;
        }
        PropertyInfo p = obj.GetType().GetProperty(name, flags);
        if (p != null && p.CanWrite && IsCompatible(p.PropertyType, value)) p.SetValue(obj, value, null);
    }

    private static bool IsCompatible(Type targetType, object value)
    {
        if (value == null) return !targetType.IsValueType || Nullable.GetUnderlyingType(targetType) != null;
        return targetType.IsInstanceOfType(value) || (targetType.IsEnum && value.GetType() == targetType);
    }

    private static void SetDatabaseMap(string fieldName, object key, object value)
    {
        if (DewResources.database == null || key == null) return;
        FieldInfo f = DewResources.database.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        IDictionary map = f != null ? f.GetValue(DewResources.database) as IDictionary : null;
        if (map == null) return;
        map[key] = value;
    }

    private static void RemoveDatabaseMappingIfOwned(string fieldName, object key, object expected)
    {
        if (DewResources.database == null || key == null) return;
        try
        {
            FieldInfo f = DewResources.database.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            IDictionary map = f != null ? f.GetValue(DewResources.database) as IDictionary : null;
            if (map != null && map.Contains(key) && Equals(map[key], expected)) map.Remove(key);
        }
        catch { }
    }

    private static void RemoveTypedMappings(Type type, string guid, string name)
    {
        if (DewResources.database == null) return;
        try
        {
            string aqn = type.AssemblyQualifiedName;
            string current;
            if (DewResources.database.typeAssemblyQualifiedNameToGuid.TryGetValue(aqn, out current) && current == guid)
                DewResources.database.typeAssemblyQualifiedNameToGuid.Remove(aqn);
        }
        catch { }
        RemoveDatabaseMappingIfOwned("typeToGuid", type, guid);
        RemoveDatabaseMappingIfOwned("guidToType", guid, type);
        RemoveDatabaseMappingIfOwned("typeNameToGuid", type.Name, guid);
        RemoveDatabaseMappingIfOwned("typeNameToType", type.Name, type);
        RemoveDatabaseMappingIfOwned("nameToGuid", name, guid);
        RemoveDatabaseMappingIfOwned("guidToName", guid, name);
    }

    private static void InvokePreferredSettingsValidate(DewProfile profile)
    {
        if (profile == null) return;
        HashSet<object> visited = new HashSet<object>();
        BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        foreach (FieldInfo f in profile.GetType().GetFields(flags))
        {
            object value = null;
            try { value = f.GetValue(profile); } catch { }
            InvokeValidateIfPreferred(value, visited);
        }
        foreach (PropertyInfo p in profile.GetType().GetProperties(flags))
        {
            if (!p.CanRead || p.GetIndexParameters().Length != 0) continue;
            object value = null;
            try { value = p.GetValue(profile, null); } catch { }
            InvokeValidateIfPreferred(value, visited);
        }
    }

    private static void InvokeValidateIfPreferred(object value, HashSet<object> visited)
    {
        if (value == null || visited.Contains(value)) return;
        visited.Add(value);
        Type t = value.GetType();
        if (t.Name.IndexOf("PreferredGameSettings", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            MethodInfo validate = t.GetMethod("Validate", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
            if (validate != null) { try { validate.Invoke(value, null); } catch { } }
            return;
        }
        IDictionary dict = value as IDictionary;
        if (dict != null)
        {
            foreach (DictionaryEntry e in dict) InvokeValidateIfPreferred(e.Value, visited);
            return;
        }
        IEnumerable enumerable = value as IEnumerable;
        if (!(value is string) && enumerable != null)
            foreach (object item in enumerable) InvokeValidateIfPreferred(item, visited);
    }
}