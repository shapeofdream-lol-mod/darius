using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public static partial class DariusEnemyClassifier
{
    private static bool ClassifyMarkerValue(object value, out DariusEnemyClass kind)
    {
        kind = DariusEnemyClass.Common;
        string text = SafeValue(value).ToLowerInvariant();
        if (ContainsAny(text, BossTokens)) { kind = DariusEnemyClass.Boss; return true; }
        if (ContainsAny(text, StrongEliteTokens) || text == "rare" || text.EndsWith(".rare", StringComparison.OrdinalIgnoreCase))
        {
            kind = DariusEnemyClass.Elite;
            return true;
        }
        return false;
    }

    private static bool NameMatches(string name, string[] names)
    {
        if (string.IsNullOrEmpty(name) || names == null) return false;
        for (int i = 0; i < names.Length; i++)
            if (string.Equals(name, names[i], StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    private static bool ContainsAny(string text, string[] tokens)
    {
        if (string.IsNullOrEmpty(text) || tokens == null) return false;
        for (int i = 0; i < tokens.Length; i++)
        {
            string token = tokens[i];
            if (!string.IsNullOrEmpty(token) && text.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0) return true;
        }
        return false;
    }

    private static string SafeLabel(object obj)
    {
        if (obj == null) return string.Empty;
        string type = string.Empty, name = string.Empty, value = string.Empty;
        try { type = obj.GetType().Name ?? string.Empty; } catch { }
        try
        {
            UnityEngine.Object uo = obj as UnityEngine.Object;
            if (uo != null) name = uo.name ?? string.Empty;
        }
        catch { }
        if (!(obj is UnityEngine.Object))
        {
            try { value = obj.ToString() ?? string.Empty; } catch { }
        }
        return (name + " " + type + " " + value).ToLowerInvariant();
    }

    private static string SafeValue(object value)
    {
        if (value == null) return string.Empty;
        try { return value.ToString() ?? string.Empty; } catch { return string.Empty; }
    }
}