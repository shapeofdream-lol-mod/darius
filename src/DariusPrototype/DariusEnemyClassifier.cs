using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public enum DariusEnemyClass
{
    Common,
    Elite,
    Boss
}

public static class DariusEnemyClassifier
{
    private static readonly string[] BossTokens = { "boss", "guardian", "finalboss", "final_boss", "dreamer", "nightmare" };
    private static readonly string[] StrongBossTokens = { "boss", "finalboss", "final_boss" };
    private static readonly string[] EliteTokens = { "elite", "champion", "miniboss", "mini_boss", "mini-boss", "mini boss", "midboss", "subboss", "rare", "special" };
    private static readonly string[] StrongEliteTokens = { "elite", "champion", "miniboss", "mini_boss", "mini-boss", "mini boss", "midboss", "subboss" };
    private static readonly string[] BossBoolNames = { "isBoss", "IsBoss", "boss", "isMajorBoss", "IsMajorBoss", "isFinalBoss", "IsFinalBoss" };
    private static readonly string[] EliteBoolNames = {
        "isElite", "IsElite", "elite", "isChampion", "IsChampion", "champion",
        "isMiniBoss", "IsMiniBoss", "isMiniboss", "IsMiniboss", "miniBoss", "miniboss",
        "isRare", "IsRare", "rare"
    };
    private static readonly string[] RankMemberTokens = { "rank", "tier", "grade", "rarity", "class", "elite", "boss", "champion", "mini" };
    private static readonly string[] ModifierCollectionTokens = { "status", "effect", "buff", "modifier", "affix", "trait" };
    // Shape of Dreams can promote an ordinary monster prefab into a mini-boss at runtime.
    // The Entity name therefore remains a normal Mon_* name while one or more mini-boss
    // modifiers are attached. These are confirmed public modifier names / mini-boss archetypes
    // plus generic internal naming forms so Noxian Arena does not depend on one exact game build.
    private static readonly string[] MiniBossModifierTokens = {
        "minibossmodifier", "mini_boss_modifier", "minibossaffix", "mini_boss_affix",
        "bloodthorn", "blood_thorn", "auraofcold", "aura_of_cold", "arrowstorm", "arrow_storm"
    };
    private static readonly string[] KnownMiniBossArchetypeTokens = {
        "redgiant", "red_giant", "phasebug", "phase_bug", "spinningarrow", "spinning_arrow"
    };

    public static DariusEnemyClass Classify(Entity entity)
    {
        string reason;
        return Classify(entity, out reason);
    }

    // Elite/miniboss status in Shape of Dreams is not guaranteed to live on the concrete monster
    // class itself. Some encounters promote an ordinary monster prefab at runtime, so looking only
    // at entity.name / entity.GetType() can classify a miniboss as Common. Inspect the full runtime
    // entity/component/status graph when Noxian Arena evaluates nearby enemies. The arena runtime throttles
    // this classification pass to a 0.25-second cadence rather than running it every frame.
    public static DariusEnemyClass Classify(Entity entity, out string reason)
    {
        reason = "null/common";
        if (entity == null) return DariusEnemyClass.Common;

        string member;
        if (ReadBoolRecursive(entity, BossBoolNames, out member))
        {
            reason = "entity-bool:" + member;
            return DariusEnemyClass.Boss;
        }
        if (ReadBoolRecursive(entity, EliteBoolNames, out member))
        {
            reason = "entity-bool:" + member;
            return DariusEnemyClass.Elite;
        }

        DariusEnemyClass rankKind;
        if (TryReadRankMarker(entity, out rankKind, out member))
        {
            reason = "entity-rank:" + member;
            return rankKind;
        }

        // The critical v0.1.7 gap: elite/miniboss flags can be stored on EntityStatus, EntityAI,
        // encounter modifiers, health-bar/rank components, or a child runtime component rather than
        // on Entity. Scan those components before falling back to prefab naming.
        Component[] components = null;
        try { components = entity.GetComponentsInChildren<Component>(true); } catch { }
        if (components != null)
        {
            for (int i = 0; i < components.Length; i++)
            {
                Component c = components[i];
                if (c == null || ReferenceEquals(c, entity)) continue;

                if (ReadBoolRecursive(c, BossBoolNames, out member))
                {
                    reason = "component-bool:" + c.GetType().Name + "." + member;
                    return DariusEnemyClass.Boss;
                }
                if (ReadBoolRecursive(c, EliteBoolNames, out member))
                {
                    reason = "component-bool:" + c.GetType().Name + "." + member;
                    return DariusEnemyClass.Elite;
                }
                if (TryReadRankMarker(c, out rankKind, out member))
                {
                    reason = "component-rank:" + c.GetType().Name + "." + member;
                    return rankKind;
                }

                string componentLabel = SafeLabel(c);
                if (ContainsAny(componentLabel, StrongBossTokens))
                {
                    reason = "component-name:" + c.GetType().Name;
                    return DariusEnemyClass.Boss;
                }
                if (ContainsAny(componentLabel, StrongEliteTokens))
                {
                    reason = "component-name:" + c.GetType().Name;
                    return DariusEnemyClass.Elite;
                }

                if (TryScanModifierCollections(c, out rankKind, out member))
                {
                    reason = "component-modifier:" + c.GetType().Name + "." + member;
                    return rankKind;
                }
            }
        }

        // Also scan collections directly exposed by Entity in case StatusEffects/Affixes are actor
        // objects rather than MonoBehaviours in the hierarchy.
        if (TryScanModifierCollections(entity, out rankKind, out member))
        {
            reason = "entity-modifier:" + member;
            return rankKind;
        }

        string label = SafeLabel(entity);
        if (ContainsAny(label, KnownMiniBossArchetypeTokens))
        {
            reason = "known-miniboss-archetype:" + label;
            return DariusEnemyClass.Elite;
        }
        if (ContainsAny(label, BossTokens))
        {
            reason = "entity-name:" + label;
            return DariusEnemyClass.Boss;
        }
        if (ContainsAny(label, EliteTokens))
        {
            reason = "entity-name:" + label;
            return DariusEnemyClass.Elite;
        }

        // Late native-behaviour fallback. Mini-boss/empowered monsters use the same native
        // crowd-control-immunity gate that blocks standard KnockUp/Knockback. Only use this
        // after every explicit Boss/Elite marker above, so a named Boss never gets demoted.
        try
        {
            if (entity.Status != null && entity.Status.hasCrowdControlImmunity)
            {
                reason = "native-cc-immunity";
                return DariusEnemyClass.Elite;
            }
        }
        catch { }

        reason = "no-runtime-rank-marker";
        return DariusEnemyClass.Common;
    }

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
