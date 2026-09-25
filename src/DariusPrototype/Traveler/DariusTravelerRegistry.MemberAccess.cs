using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;

public static partial class DariusTravelerRegistry
{
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
        DariusRuntimeResourceDatabaseBridge.SetMap(DewResources.database, fieldName, key, value);
    }

    private static void RemoveDatabaseMappingIfOwned(string fieldName, object key, object expected)
    {
        try { DariusRuntimeResourceDatabaseBridge.RemoveMapIfOwned(DewResources.database, fieldName, key, expected); }
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

}
