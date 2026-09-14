public static partial class DariusTravelerRegistry
{
    private static void ReplaceDirectComponentReferences(GameObject root, Component oldComponent, Component newComponent)
    {
        if (root == null || oldComponent == null || newComponent == null) return;
        Component[] components = root.GetComponentsInChildren<Component>(true);
        int replaced = 0;
        for (int ci = 0; ci < components.Length; ci++)
        {
            Component c = components[ci];
            if (c == null || c == oldComponent || c == newComponent) continue;
            Type t = c.GetType();
            while (t != null && typeof(Component).IsAssignableFrom(t))
            {
                FieldInfo[] fields = t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                for (int fi = 0; fi < fields.Length; fi++)
                {
                    FieldInfo f = fields[fi];
                    if (f.IsStatic || f.IsInitOnly) continue;
                    try
                    {
                        object value = f.GetValue(c);
                        if (ReferenceEquals(value, oldComponent))
                        {
                            if (f.FieldType.IsAssignableFrom(newComponent.GetType()) || f.FieldType.IsAssignableFrom(typeof(Hero_Darius)) || f.FieldType == typeof(Hero) || f.FieldType == typeof(Entity) || f.FieldType == typeof(Actor) || f.FieldType == typeof(Component))
                            {
                                f.SetValue(c, newComponent);
                                replaced++;
                            }
                            continue;
                        }
                        Array array = value as Array;
                        if (array != null)
                        {
                            Type elementType = array.GetType().GetElementType();
                            if (elementType != null && elementType.IsAssignableFrom(newComponent.GetType()))
                            {
                                for (int i = 0; i < array.Length; i++)
                                {
                                    if (ReferenceEquals(array.GetValue(i), oldComponent))
                                    {
                                        array.SetValue(newComponent, i);
                                        replaced++;
                                    }
                                }
                            }
                            continue;
                        }

                        IList list = value as IList;
                        if (list != null && !list.IsReadOnly && !list.IsFixedSize)
                        {
                            for (int i = 0; i < list.Count; i++)
                            {
                                if (!ReferenceEquals(list[i], oldComponent)) continue;
                                try { list[i] = newComponent; replaced++; } catch { }
                            }
                        }
                    }
                    catch { }
                }
                t = t.BaseType;
            }
        }
        DariusLog.Info("TRAVELER-COPY", "Rebound direct prefab references " + oldComponent.GetType().Name + " -> " + newComponent.GetType().Name + " count=" + replaced);
    }

    private static int CountDirectComponentReferences(GameObject root, Component oldComponent, Component replacement)
    {
        if (root == null || ReferenceEquals(oldComponent, null)) return 0;
        int count = 0;
        Component[] components = root.GetComponentsInChildren<Component>(true);
        for (int ci = 0; ci < components.Length; ci++)
        {
            Component c = components[ci];
            if (c == null || ReferenceEquals(c, oldComponent) || ReferenceEquals(c, replacement)) continue;
            Type t = c.GetType();
            while (t != null && typeof(Component).IsAssignableFrom(t))
            {
                FieldInfo[] fields = t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                for (int fi = 0; fi < fields.Length; fi++)
                {
                    FieldInfo f = fields[fi];
                    if (f.IsStatic) continue;
                    try
                    {
                        object value = f.GetValue(c);
                        if (ReferenceEquals(value, oldComponent)) { count++; continue; }
                        Array array = value as Array;
                        if (array != null)
                        {
                            for (int i = 0; i < array.Length; i++) if (ReferenceEquals(array.GetValue(i), oldComponent)) count++;
                            continue;
                        }
                        IList list = value as IList;
                        if (list != null)
                        {
                            for (int i = 0; i < list.Count; i++) if (ReferenceEquals(list[i], oldComponent)) count++;
                        }
                    }
                    catch { }
                }
                t = t.BaseType;
            }
        }
        return count;
    }

    private static int GetArrayLength(object owner, string fieldName)
    {
        FieldInfo f = FindFieldRecursive(owner.GetType(), fieldName);
        Array a = f != null ? f.GetValue(owner) as Array : null;
        return a != null ? a.Length : 0;
    }

    private static FieldInfo FindFieldRecursive(Type t, string name)
    {
        while (t != null)
        {
            FieldInfo f = t.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            if (f != null) return f;
            t = t.BaseType;
        }
        return null;
    }

    private static void AddUniqueTypeToDew(string fieldName, Type type)
    {
        FieldInfo f = typeof(Dew).GetField(fieldName, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        IList list = f != null ? f.GetValue(null) as IList : null;
        if (list == null)
        {
            DariusLog.Warn("TRAVELER-TYPE", "Dew backing list unavailable: " + fieldName);
            return;
        }

        bool haveCurrent = false;
        int removed = 0;
        // Mod assemblies are timestamp-renamed on reload. An old Hero_Darius Type from the previous
        // loaded assembly is not reference-equal to the new Type, even though the game sees the same
        // hero name. Remove stale same-FullName entries before adding the current canonical Type.
        for (int i = list.Count - 1; i >= 0; i--)
        {
            Type existing = list[i] as Type;
            if (existing == null || !string.Equals(existing.FullName, type.FullName, StringComparison.Ordinal)) continue;
            if (Equals(existing, type) && !haveCurrent) { haveCurrent = true; continue; }
            list.RemoveAt(i);
            removed++;
        }
        if (!haveCurrent) list.Add(type);
        if (removed > 0) DariusLog.Info("TRAVELER-TYPE", "Removed stale/duplicate " + type.FullName + " entries=" + removed + " from " + fieldName);
    }

    private static void RemoveTypeFromDew(string fieldName, Type type)
    {
        try
        {
            FieldInfo f = typeof(Dew).GetField(fieldName, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            IList list = f != null ? f.GetValue(null) as IList : null;
            if (list == null) return;
            for (int i = list.Count - 1; i >= 0; i--)
            {
                Type existing = list[i] as Type;
                if (existing != null && string.Equals(existing.FullName, type.FullName, StringComparison.Ordinal)) list.RemoveAt(i);
            }
        }
        catch { }
    }
}