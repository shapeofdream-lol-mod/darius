using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Mirror;
using UnityEngine;
using DewInternal;

public static partial class DariusDejaVuRegistry
{
    private static void AddStringToMember(object target, string memberName, string value)
    {
        if (target == null) return;
        Type type = target.GetType();
        BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        FieldInfo field = type.GetField(memberName, flags);
        PropertyInfo property = field == null ? type.GetProperty(memberName, flags) : null;
        object current = field != null ? field.GetValue(target) : (property != null && property.CanRead ? property.GetValue(target, null) : null);

        if (current is string[] arr)
        {
            if (arr.Contains(value)) return;
            string[] next = new string[arr.Length + 1];
            Array.Copy(arr, next, arr.Length);
            next[arr.Length] = value;
            if (field != null) field.SetValue(target, next);
            else if (property != null && property.CanWrite) property.SetValue(target, next, null);
            DariusLog.DebugInfo("DEJAVU", "Added " + value + " to " + memberName + "[] count=" + next.Length);
            return;
        }

        IList list = current as IList;
        if (list != null)
        {
            if (!list.Contains(value)) list.Add(value);
            DariusLog.DebugInfo("DEJAVU", "Ensured " + value + " in " + memberName + " count=" + list.Count);
            return;
        }

        if (field != null || property != null)
            DariusLog.DebugInfo("DEJAVU", "Member " + memberName + " exists but is null/unsupported type=" + (current != null ? current.GetType().FullName : "<null>"));
    }

    private static void AddDictionaryEntryByMember(object target, string memberName, string key, Func<Type, object> factory, bool updateExisting = false)
    {
        try
        {
            object mapObj = GetMemberValue(target, memberName);
            IDictionary map = mapObj as IDictionary;
            if (map == null)
            {
                DariusLog.DebugInfo("DEJAVU", "Dictionary member unavailable: " + target.GetType().Name + "." + memberName);
                return;
            }

            Type[] args = mapObj.GetType().GetGenericArguments();
            Type valueType = args.Length >= 2 ? args[1] : typeof(object);
            if (!map.Contains(key))
            {
                map.Add(key, factory(valueType));
                DariusLog.DebugInfo("DEJAVU", "Added dictionary key " + memberName + "[" + key + "] valueType=" + valueType.FullName);
            }
            else if (updateExisting)
            {
                object existing = map[key];
                QualifyItemData(existing);
                map[key] = existing;
                DariusLog.DebugInfo("DEJAVU", "Updated qualification for " + memberName + "[" + key + "]");
            }
        }
        catch (Exception e) { DariusLog.Exception("DEJAVU", e, "Dictionary injection failed member=" + memberName + " key=" + key); }
    }

    private static object GetMemberValue(object target, string memberName)
    {
        if (target == null) return null;
        BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        FieldInfo f = target.GetType().GetField(memberName, flags);
        if (f != null) return f.GetValue(target);
        PropertyInfo p = target.GetType().GetProperty(memberName, flags);
        if (p != null && p.CanRead) return p.GetValue(target, null);
        return null;
    }

    private static int CountDictionaryMember(object target, string memberName)
    {
        IDictionary map = GetMemberValue(target, memberName) as IDictionary;
        return map != null ? map.Count : -1;
    }

    private static void SetMember(object target, string name, object value)
    {
        if (target == null) return;
        BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        FieldInfo f = target.GetType().GetField(name, flags);
        if (f != null)
        {
            if (value == null || f.FieldType.IsInstanceOfType(value)) f.SetValue(target, value);
            else if (f.FieldType.IsEnum) f.SetValue(target, Enum.ToObject(f.FieldType, Convert.ToInt32(value)));
            return;
        }
        PropertyInfo p = target.GetType().GetProperty(name, flags);
        if (p != null && p.CanWrite)
        {
            if (value == null || p.PropertyType.IsInstanceOfType(value)) p.SetValue(target, value, null);
            else if (p.PropertyType.IsEnum) p.SetValue(target, Enum.ToObject(p.PropertyType, Convert.ToInt32(value)), null);
        }
    }

    private static void SetNumericAtLeast(object target, string name, long minimum)
    {
        if (target == null) return;
        BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        FieldInfo f = target.GetType().GetField(name, flags);
        if (f != null)
        {
            long current = Convert.ToInt64(f.GetValue(target));
            if (current < minimum) f.SetValue(target, Convert.ChangeType(minimum, f.FieldType));
            return;
        }
        PropertyInfo p = target.GetType().GetProperty(name, flags);
        if (p != null && p.CanRead && p.CanWrite)
        {
            long current = Convert.ToInt64(p.GetValue(target, null));
            if (current < minimum) p.SetValue(target, Convert.ChangeType(minimum, p.PropertyType), null);
        }
    }

    private static void LogDejaVuDiscoveryOnce()
    {
        if (_loggedDiscovery) return;
        _loggedDiscovery = true;
        try
        {
            Assembly asm = typeof(Dew).Assembly;
            string[] hits = asm.GetTypes()
                .Where(t => t.FullName != null && (t.FullName.IndexOf("Deja", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                                   t.FullName.IndexOf("Reverie", StringComparison.OrdinalIgnoreCase) >= 0))
                .Select(t => t.FullName)
                .OrderBy(s => s)
                .Take(80)
                .ToArray();
            DariusLog.Info("DEJAVU-DISCOVERY", "Types containing Deja/Reverie in " + asm.GetName().Name + ": " +
                (hits.Length > 0 ? string.Join(" | ", hits) : "<none>"));

            foreach (string keyword in new[] { "deja", "reverie" })
            {
                string[] methods = asm.GetTypes().SelectMany(t =>
                {
                    try { return t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static); }
                    catch { return new MethodInfo[0]; }
                }).Where(m => m.Name.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                  .Select(MethodLabel).Distinct().Take(80).ToArray();
                DariusLog.Info("DEJAVU-DISCOVERY", "Methods containing '" + keyword + "': " +
                    (methods.Length > 0 ? string.Join(" | ", methods) : "<none>"));
            }
        }
        catch (Exception e) { DariusLog.Exception("DEJAVU-DISCOVERY", e, "Reflection discovery failed"); }
    }

    private static string MethodLabel(MethodBase m)
    {
        try { return m.DeclaringType.FullName + "." + m.Name + "(" + string.Join(",", m.GetParameters().Select(p => p.ParameterType.Name).ToArray()) + ")"; }
        catch { return m != null ? m.Name : "<null>"; }
    }

    private static bool LooksLikeNameRequest(MethodBase method, string key)
    {
        string methodName = method != null ? method.Name : string.Empty;
        if (!string.IsNullOrEmpty(methodName) &&
            (methodName.IndexOf("Name", StringComparison.OrdinalIgnoreCase) >= 0 || methodName.IndexOf("Title", StringComparison.OrdinalIgnoreCase) >= 0))
            return true;
        if (string.IsNullOrEmpty(key)) return false;
        return key.EndsWith(".name", StringComparison.OrdinalIgnoreCase) ||
               key.EndsWith("_name", StringComparison.OrdinalIgnoreCase) ||
               key.IndexOf(".title", StringComparison.OrdinalIgnoreCase) >= 0 ||
               key.IndexOf("_title", StringComparison.OrdinalIgnoreCase) >= 0;
    }
}