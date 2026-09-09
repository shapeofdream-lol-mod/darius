using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace DariusModelAnimationRuntime
{
    internal static class ModelUtils
    {
        public static GameObject ResolveVisualModel(Hero hero)
        {
            if (hero == null) return null;
            try
            {
                object visual = GetMemberRecursive(hero, "Visual") ?? GetMemberRecursive(hero, "visual");
                if (visual == null) return null;
                object model = GetMemberRecursive(visual, "model");
                GameObject go = model as GameObject;
                if (go != null) return go;
                Component c = model as Component;
                if (c != null) return c.gameObject;
            }
            catch (Exception e) { RuntimeLog.Exception("VISUAL", e, "ResolveVisualModel failed"); }
            return null;
        }

        private static object GetMemberRecursive(object obj, string name)
        {
            if (obj == null) return null;
            Type t = obj.GetType();
            BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            while (t != null)
            {
                PropertyInfo p = t.GetProperty(name, flags);
                if (p != null && p.CanRead) { try { return p.GetValue(obj, null); } catch { } }
                FieldInfo f = t.GetField(name, flags);
                if (f != null) { try { return f.GetValue(obj); } catch { } }
                t = t.BaseType;
            }
            return null;
        }

        public static string NormalizeBoneName(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            char[] chars = value.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray();
            return new string(chars);
        }

        public static Transform FindByAliases(Transform[] all, string[] aliases)
        {
            if (all == null || aliases == null) return null;
            Transform best = null;
            int bestScore = -1;
            for (int i = 0; i < all.Length; i++)
            {
                Transform t = all[i];
                if (t == null) continue;
                string n = NormalizeBoneName(t.name);
                for (int j = 0; j < aliases.Length; j++)
                {
                    string a = NormalizeBoneName(aliases[j]);
                    if (a.Length == 0) continue;
                    int score = n == a ? 100 : (n.EndsWith(a, StringComparison.Ordinal) ? 80 : (n.Contains(a) ? 40 : -1));
                    if (score > bestScore) { bestScore = score; best = t; }
                }
            }
            return bestScore >= 40 ? best : null;
        }
    }
}
