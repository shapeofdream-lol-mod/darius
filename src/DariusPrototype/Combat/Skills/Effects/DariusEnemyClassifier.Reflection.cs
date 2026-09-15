using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public static partial class DariusEnemyClassifier
{
    private static bool ReadBoolRecursive(object obj, string[] names, out string matchedMember)
    {
        matchedMember = string.Empty;
        if (obj == null) return false;
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        for (Type t = obj.GetType(); t != null; t = t.BaseType)
        {
            FieldInfo[] fields = null;
            PropertyInfo[] props = null;
            try { fields = t.GetFields(flags); } catch { }
            if (fields != null)
            {
                for (int i = 0; i < fields.Length; i++)
                {
                    FieldInfo f = fields[i];
                    if (f == null || f.FieldType != typeof(bool) || !NameMatches(f.Name, names)) continue;
                    try
                    {
                        if ((bool)f.GetValue(obj)) { matchedMember = t.Name + "." + f.Name; return true; }
                    }
                    catch { }
                }
            }
            try { props = t.GetProperties(flags); } catch { }
            if (props != null)
            {
                for (int i = 0; i < props.Length; i++)
                {
                    PropertyInfo p = props[i];
                    if (p == null || p.PropertyType != typeof(bool) || !p.CanRead || p.GetIndexParameters().Length != 0 || !NameMatches(p.Name, names)) continue;
                    try
                    {
                        if ((bool)p.GetValue(obj, null)) { matchedMember = t.Name + "." + p.Name; return true; }
                    }
                    catch { }
                }
            }
        }
        return false;
    }

    private static bool TryReadRankMarker(object obj, out DariusEnemyClass kind, out string matchedMember)
    {
        kind = DariusEnemyClass.Common;
        matchedMember = string.Empty;
        if (obj == null) return false;
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        for (Type t = obj.GetType(); t != null; t = t.BaseType)
        {
            FieldInfo[] fields = null;
            PropertyInfo[] props = null;
            try { fields = t.GetFields(flags); } catch { }
            if (fields != null)
            {
                for (int i = 0; i < fields.Length; i++)
                {
                    FieldInfo f = fields[i];
                    if (f == null || !ContainsAny((f.Name ?? string.Empty).ToLowerInvariant(), RankMemberTokens)) continue;
                    if (f.FieldType != typeof(string) && !f.FieldType.IsEnum) continue;
                    try
                    {
                        object value = f.GetValue(obj);
                        if (ClassifyMarkerValue(value, out kind)) { matchedMember = t.Name + "." + f.Name + "=" + SafeValue(value); return true; }
                    }
                    catch { }
                }
            }
            try { props = t.GetProperties(flags); } catch { }
            if (props != null)
            {
                for (int i = 0; i < props.Length; i++)
                {
                    PropertyInfo p = props[i];
                    if (p == null || !p.CanRead || p.GetIndexParameters().Length != 0 || !ContainsAny((p.Name ?? string.Empty).ToLowerInvariant(), RankMemberTokens)) continue;
                    if (p.PropertyType != typeof(string) && !p.PropertyType.IsEnum) continue;
                    try
                    {
                        object value = p.GetValue(obj, null);
                        if (ClassifyMarkerValue(value, out kind)) { matchedMember = t.Name + "." + p.Name + "=" + SafeValue(value); return true; }
                    }
                    catch { }
                }
            }
        }
        return false;
    }

    private static bool TryScanModifierCollections(object obj, out DariusEnemyClass kind, out string matchedMember)
    {
        kind = DariusEnemyClass.Common;
        matchedMember = string.Empty;
        if (obj == null) return false;
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        for (Type t = obj.GetType(); t != null; t = t.BaseType)
        {
            FieldInfo[] fields = null;
            PropertyInfo[] props = null;
            try { fields = t.GetFields(flags); } catch { }
            if (fields != null)
            {
                for (int i = 0; i < fields.Length; i++)
                {
                    FieldInfo f = fields[i];
                    string fn = f != null ? (f.Name ?? string.Empty).ToLowerInvariant() : string.Empty;
                    if (f == null || !ContainsAny(fn, ModifierCollectionTokens)) continue;
                    object value = null;
                    try { value = f.GetValue(obj); } catch { }
                    if (TryClassifyEnumerable(value, out kind, out matchedMember))
                    {
                        matchedMember = t.Name + "." + f.Name + "->" + matchedMember;
                        return true;
                    }
                }
            }

            // Some game systems expose active status/affix lists through a read-only property rather
            // than a field. Read only members whose names explicitly look like modifier containers.
            try { props = t.GetProperties(flags); } catch { }
            if (props != null)
            {
                for (int i = 0; i < props.Length; i++)
                {
                    PropertyInfo p = props[i];
                    string pn = p != null ? (p.Name ?? string.Empty).ToLowerInvariant() : string.Empty;
                    if (p == null || !p.CanRead || p.GetIndexParameters().Length != 0 || !ContainsAny(pn, ModifierCollectionTokens)) continue;
                    if (!typeof(IEnumerable).IsAssignableFrom(p.PropertyType) || p.PropertyType == typeof(string)) continue;
                    object value = null;
                    try { value = p.GetValue(obj, null); } catch { }
                    if (TryClassifyEnumerable(value, out kind, out matchedMember))
                    {
                        matchedMember = t.Name + "." + p.Name + "->" + matchedMember;
                        return true;
                    }
                }
            }
        }
        return false;
    }

    private static bool TryClassifyEnumerable(object value, out DariusEnemyClass kind, out string matchedMember)
    {
        kind = DariusEnemyClass.Common;
        matchedMember = string.Empty;
        IEnumerable seq = value as IEnumerable;
        if (seq == null || value is string) return false;
        int checkedCount = 0;
        try
        {
            foreach (object item in seq)
            {
                if (item == null) continue;
                if (++checkedCount > 64) break;
                string itemLabel = SafeLabel(item);
                if (ContainsAny(itemLabel, StrongBossTokens)) { kind = DariusEnemyClass.Boss; matchedMember = item.GetType().Name; return true; }
                if (ContainsAny(itemLabel, StrongEliteTokens) || ContainsAny(itemLabel, MiniBossModifierTokens))
                {
                    kind = DariusEnemyClass.Elite;
                    matchedMember = item.GetType().Name + ":" + itemLabel;
                    return true;
                }
                string nested;
                if (ReadBoolRecursive(item, BossBoolNames, out nested)) { kind = DariusEnemyClass.Boss; matchedMember = nested; return true; }
                if (ReadBoolRecursive(item, EliteBoolNames, out nested)) { kind = DariusEnemyClass.Elite; matchedMember = nested; return true; }
                if (TryReadRankMarker(item, out kind, out nested)) { matchedMember = nested; return true; }
            }
        }
        catch { }
        return false;
    }
}