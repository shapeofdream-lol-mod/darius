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

public static class DariusLobbyConstellationPresentation
{
    public static void ApplyHeroPortrait(Component origin)
    {
        if (origin == null) return;
        try
        {
            if (DewPlayer.local == null || !string.Equals(DewPlayer.local.selectedHeroType, DariusTravelerRegistry.HeroName, StringComparison.Ordinal)) return;
            Sprite portrait = DariusPrototypeIcons.Get("HERO");
            if (portrait == null) return;

            int applied = 0;
            Transform cursor = origin.transform;
            Transform highest = cursor;
            for (int i = 0; cursor != null && i < 10; i++, cursor = cursor.parent) highest = cursor;
            Component[] components = highest != null ? highest.GetComponentsInChildren<Component>(true) : origin.GetComponentsInChildren<Component>(true);
            foreach (Component component in components)
            {
                if (component == null || component.gameObject == null) continue;
                string objectName = component.gameObject.name.ToLowerInvariant();
                string typeName = component.GetType().Name.ToLowerInvariant();
                bool namedPortrait = objectName.Contains("hero") || objectName.Contains("portrait") || objectName.Contains("traveler") ||
                                    objectName.Contains("character") || objectName.Contains("profile") ||
                                    typeName.Contains("heroicon") || typeName.Contains("portrait");
                if (!namedPortrait || objectName.Contains("skill") || objectName.Contains("memory") || objectName.Contains("star") || objectName.Contains("lock")) continue;
                if (TrySetSpriteAndWhite(component, portrait)) applied++;
            }

            // Serialized controller references are more reliable than GameObject names on some UI prefabs.
            cursor = origin.transform;
            for (int i = 0; cursor != null && i < 10; i++, cursor = cursor.parent)
            {
                foreach (Component controller in cursor.GetComponents<Component>())
                {
                    if (controller == null) continue;
                    string tn = controller.GetType().Name;
                    if (tn.IndexOf("Constellation", StringComparison.OrdinalIgnoreCase) < 0 &&
                        tn.IndexOf("Hero", StringComparison.OrdinalIgnoreCase) < 0) continue;
                    applied += ApplyControllerPortraitReferences(controller, portrait);
                }
            }

            // Some 1.3.x constellation prefabs expose the large traveler portrait as one or more
            // generically named square Images instead of a serialized heroIcon field. Always sweep
            // the screen for the largest portrait-like square targets, because named/controller paths
            // may fix one preview widget yet still leave the top-left profile square on the stock fog.
            applied += ApplyLargeSquarePortraitCandidates(components, portrait);

            DariusLog.DebugInfoThrottled("HERO-PORTRAIT", "constellation-root",
                "Rebound Darius champion portrait in constellation UI targets=" + applied, 0.75);
        }
        catch (Exception e)
        {
            DariusLog.Exception("HERO-PORTRAIT", e, "Constellation hero portrait repair failed");
        }
    }

    private static int ApplyLargeSquarePortraitCandidates(Component[] components, Sprite portrait)
    {
        if (components == null || portrait == null) return 0;
        List<KeyValuePair<Component, float>> candidates = new List<KeyValuePair<Component, float>>();
        foreach (Component component in components)
        {
            if (component == null || component.gameObject == null) continue;
            string n = component.gameObject.name.ToLowerInvariant();
            if (n.Contains("background") || n.Contains("panel") || n.Contains("frame") || n.Contains("star") ||
                n.Contains("skill") || n.Contains("memory") || n.Contains("lock") || n.Contains("tab") ||
                n.Contains("branch") || n.Contains("line")) continue;
            PropertyInfo sp = component.GetType().GetProperty("sprite", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            PropertyInfo tp = component.GetType().GetProperty("texture", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            bool spriteTarget = sp != null && sp.CanWrite && sp.PropertyType == typeof(Sprite);
            bool textureTarget = tp != null && tp.CanWrite && typeof(Texture).IsAssignableFrom(tp.PropertyType);
            if (!spriteTarget && !textureTarget) continue;
            RectTransform rect = component.transform as RectTransform;
            if (rect == null) continue;
            float width = Mathf.Abs(rect.rect.width);
            float height = Mathf.Abs(rect.rect.height);
            if (width < 96f || height < 96f) continue;
            float ratio = height > 0.01f ? width / height : 0f;
            if (ratio < 0.72f || ratio > 1.38f) continue;
            float area = width * height;
            if (area < 9000f || area > 180000f) continue;
            candidates.Add(new KeyValuePair<Component, float>(component, area));
        }

        candidates.Sort((a, b) => b.Value.CompareTo(a.Value));
        int applied = 0;
        int limit = Mathf.Min(3, candidates.Count);
        for (int i = 0; i < limit; i++)
        {
            KeyValuePair<Component, float> kv = candidates[i];
            if (TrySetSpriteAndWhite(kv.Key, portrait))
            {
                applied++;
                DariusLog.DebugInfoThrottled("HERO-PORTRAIT", "square-target:" + kv.Key.GetInstanceID(),
                    "Applied Darius portrait to square constellation image name=" + kv.Key.gameObject.name +
                    " area=" + kv.Value.ToString("0"), 2.0);
            }
        }
        return applied;
    }

    private static int ApplyControllerPortraitReferences(Component controller, Sprite portrait)
    {
        int applied = 0;
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        foreach (FieldInfo field in controller.GetType().GetFields(flags))
        {
            string n = field.Name.ToLowerInvariant();
            if (!n.Contains("hero") && !n.Contains("portrait") && !n.Contains("traveler") && !n.Contains("character")) continue;
            object value = null;
            try { value = field.GetValue(controller); } catch { }
            Component component = value as Component;
            if (component != null && TrySetSpriteAndWhite(component, portrait)) applied++;
        }
        foreach (PropertyInfo property in controller.GetType().GetProperties(flags))
        {
            if (!property.CanRead || property.GetIndexParameters().Length != 0) continue;
            string n = property.Name.ToLowerInvariant();
            if (!n.Contains("hero") && !n.Contains("portrait") && !n.Contains("traveler") && !n.Contains("character")) continue;
            object value = null;
            try { value = property.GetValue(controller, null); } catch { }
            Component component = value as Component;
            if (component != null && TrySetSpriteAndWhite(component, portrait)) applied++;
        }
        return applied;
    }

    private static bool TrySetSpriteAndWhite(Component component, Sprite sprite)
    {
        if (component == null || sprite == null) return false;
        try
        {
            bool applied = false;
            PropertyInfo p = component.GetType().GetProperty("sprite", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (p != null && p.CanWrite && p.PropertyType == typeof(Sprite))
            {
                p.SetValue(component, sprite, null);
                applied = true;
            }
            PropertyInfo tp = component.GetType().GetProperty("texture", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (tp != null && tp.CanWrite && typeof(Texture).IsAssignableFrom(tp.PropertyType) && tp.PropertyType.IsAssignableFrom(sprite.texture.GetType()))
            {
                tp.SetValue(component, sprite.texture, null);
                applied = true;
            }
            if (!applied) return false;

            PropertyInfo cp = component.GetType().GetProperty("color", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (cp != null && cp.CanWrite && cp.PropertyType == typeof(Color))
            {
                Color old = Color.white;
                try { if (cp.CanRead) old = (Color)cp.GetValue(component, null); } catch { }
                cp.SetValue(component, new Color(1f, 1f, 1f, old.a), null);
            }
            return true;
        }
        catch { return false; }
    }
}