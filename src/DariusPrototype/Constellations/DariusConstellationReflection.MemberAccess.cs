using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;

public static partial class DariusConstellationReflection
{
    private static object CloneSerializedValue(object value)
    {
        if (value == null) return null;
        Array array = value as Array;
        if (array != null) return array.Clone();
        IList list = value as IList;
        if (list != null)
        {
            try
            {
                IList clone = Activator.CreateInstance(value.GetType()) as IList;
                if (clone != null)
                {
                    foreach (object item in list) clone.Add(item);
                    return clone;
                }
            }
            catch { }
        }
        return value;
    }

    private static void EnsureSerializedSequenceCapacity(StarEffect star, int count)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        foreach (FieldInfo field in typeof(StarEffect).GetFields(flags))
        {
            if (field.IsStatic || field.IsInitOnly) continue;
            if (!field.IsPublic && !Attribute.IsDefined(field, typeof(SerializeField))) continue;
            try
            {
                if (field.FieldType.IsArray)
                {
                    Array old = field.GetValue(star) as Array;
                    if (old == null || old.Length == 0 || old.Length >= count) continue;
                    Type element = field.FieldType.GetElementType();
                    Array expanded = Array.CreateInstance(element, count);
                    for (int i = 0; i < count; i++) expanded.SetValue(old.GetValue(Mathf.Min(i, old.Length - 1)), i);
                    field.SetValue(star, expanded);
                }
                else if (typeof(IList).IsAssignableFrom(field.FieldType))
                {
                    IList list = field.GetValue(star) as IList;
                    if (list == null || list.Count == 0 || list.Count >= count) continue;
                    object last = list[list.Count - 1];
                    while (list.Count < count) list.Add(last);
                }
            }
            catch { }
        }
    }

    public static bool SetFloat(object target, float value, params string[] names)
    {
        if (target == null || names == null) return false;
        for (int i = 0; i < names.Length; i++)
            if (TrySetMember(target, names[i], value)) return true;
        return false;
    }

    public static int ReadEntityLevel(object target)
    {
        if (target == null) return 1;
        foreach (string name in new[] { "level", "currentLevel", "_level", "<level>k__BackingField" })
        {
            object value;
            if (TryReadMember(target, name, out value))
            {
                try { return Mathf.Max(1, Convert.ToInt32(value)); } catch { }
            }
        }
        return 1;
    }

    private static bool TryReadMember(object target, string name, out object value)
    {
        value = null;
        if (target == null || string.IsNullOrEmpty(name)) return false;
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        for (Type t = target.GetType(); t != null; t = t.BaseType)
        {
            try
            {
                FieldInfo f = t.GetField(name, flags | BindingFlags.DeclaredOnly);
                if (f != null) { value = f.GetValue(target); return true; }
                PropertyInfo p = t.GetProperty(name, flags | BindingFlags.DeclaredOnly);
                if (p != null && p.CanRead && p.GetIndexParameters().Length == 0) { value = p.GetValue(target, null); return true; }
            }
            catch { }
        }
        return false;
    }

    private static bool TrySetMember(object target, string name, object value)
    {
        if (target == null || string.IsNullOrEmpty(name)) return false;
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        for (Type t = target.GetType(); t != null; t = t.BaseType)
        {
            try
            {
                FieldInfo f = t.GetField(name, flags | BindingFlags.DeclaredOnly);
                if (f != null && !f.IsInitOnly)
                {
                    f.SetValue(target, ConvertValue(value, f.FieldType));
                    return true;
                }
                PropertyInfo p = t.GetProperty(name, flags | BindingFlags.DeclaredOnly);
                if (p != null && p.CanWrite && p.GetIndexParameters().Length == 0)
                {
                    p.SetValue(target, ConvertValue(value, p.PropertyType), null);
                    return true;
                }
            }
            catch { }
        }
        return false;
    }

    private static object ConvertValue(object value, Type type)
    {
        if (value == null || type == null) return value;
        if (type.IsInstanceOfType(value)) return value;
        try { return Convert.ChangeType(value, type); } catch { return value; }
    }

    private static void LogLevelMembers(object target)
    {
        if (target == null) return;
        try
        {
            List<string> names = new List<string>();
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            for (Type t = target.GetType(); t != null; t = t.BaseType)
            {
                foreach (FieldInfo f in t.GetFields(flags))
                    if (f.Name.IndexOf("level", StringComparison.OrdinalIgnoreCase) >= 0) names.Add(t.Name + "." + f.Name + ":" + f.FieldType.Name);
                foreach (PropertyInfo p in t.GetProperties(flags))
                    if (p.Name.IndexOf("level", StringComparison.OrdinalIgnoreCase) >= 0) names.Add(t.Name + "." + p.Name + ":" + p.PropertyType.Name);
            }
            DariusLog.DebugInfo("CONSTELLATION-LEVEL", target.GetType().Name + " level members=" + string.Join(",", names.ToArray()));
        }
        catch { }
    }
}