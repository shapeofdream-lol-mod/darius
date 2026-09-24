using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Mirror;
using UnityEngine;

public static partial class DariusRuntimeResourceCompatibility
{
    private static void HeroIconSetupPostfix(object __instance, string __0)
    {
        if (__instance == null || (__0 != DariusTravelerRegistry.HeroName && __0 != DariusTravelerRegistry.HeroGuid)) return;
        try
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            Type t = __instance.GetType();
            object iconTarget = null;
            FieldInfo field = t.GetField("icon", flags);
            if (field != null) iconTarget = field.GetValue(__instance);
            if (iconTarget == null)
            {
                PropertyInfo prop = t.GetProperty("icon", flags);
                if (prop != null && prop.CanRead && prop.GetIndexParameters().Length == 0) iconTarget = prop.GetValue(__instance, null);
            }
            if (iconTarget == null)
            {
                DariusLog.Warn("HERO-ICON-TINT", "UI_HeroIcon.Setup resolved Darius but serialized icon target was null; native Hero.icon remains untouched.");
                return;
            }

            // Verify that stock Setup actually installed the native Hero_Darius.icon before changing
            // only its multiplicative UI colour. Never force a sprite into arbitrary UI children.
            Sprite expected = DariusTravelerRegistry.HeroPrefab != null ? DariusTravelerRegistry.HeroPrefab.icon : null;
            PropertyInfo spriteProp = iconTarget.GetType().GetProperty("sprite", flags);
            Sprite actual = spriteProp != null && spriteProp.CanRead ? spriteProp.GetValue(iconTarget, null) as Sprite : null;
            if (expected == null || actual != expected)
            {
                DariusLog.Warn("HERO-ICON-TINT", "Stock UI_HeroIcon did not resolve the native Hero_Darius.icon; tint neutralizer skipped rather than replacing the sprite.");
                return;
            }

            PropertyInfo colorProp = iconTarget.GetType().GetProperty("color", flags);
            if (colorProp != null && colorProp.CanRead && colorProp.CanWrite && colorProp.PropertyType == typeof(Color))
            {
                Color current = (Color)colorProp.GetValue(iconTarget, null);
                colorProp.SetValue(iconTarget, new Color(1f, 1f, 1f, current.a), null);
                DariusLog.DebugInfoThrottled("HERO-ICON-TINT", "native", "Neutralized stock UI_HeroIcon RGB multiply while retaining Hero_Darius.icon as the sprite source.", 1.0);
            }
        }
        catch (Exception e)
        {
            DariusLog.Exception("HERO-ICON-TINT", e, "Native Darius hero-icon tint neutralizer failed; no sprite override was attempted.");
        }
    }

    private static void CharacterModelDisplaySetupPostfix(object __instance, string __0)
    {
        if (__instance == null || !string.Equals(__0, DariusTravelerRegistry.DefaultSkinName, StringComparison.Ordinal)) return;
        if (!string.Equals(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name, "Title", StringComparison.OrdinalIgnoreCase)) return;

        try
        {
            Component component = __instance as Component;
            if (component == null)
            {
                DariusLog.Warn("TITLE-MODEL-DIAG", "CharacterModelDisplay.Setup resolved Darius on Title, but instance was not a Component.");
                return;
            }

            Transform t = component.transform;
            string parent = t.parent != null ? t.parent.name : "<root>";
            string scene = component.gameObject.scene.IsValid() ? component.gameObject.scene.name : "<invalid>";
            Transform[] all = t.GetComponentsInChildren<Transform>(true);
            string children = string.Empty;
            int childCount = all != null ? Mathf.Max(0, all.Length - 1) : 0;
            int emitted = 0;
            if (all != null)
            {
                for (int i = 0; i < all.Length && emitted < 16; i++)
                {
                    Transform child = all[i];
                    if (child == null || child == t) continue;
                    if (children.Length > 0) children += ";";
                    children += child.name +
                        "@scene=" + (child.gameObject.scene.IsValid() ? child.gameObject.scene.name : "<invalid>") +
                        "@localPos=" + DariusLog.Vec(child.localPosition) +
                        "@localScale=" + DariusLog.Vec(child.localScale) +
                        "@active=" + child.gameObject.activeInHierarchy;
                    emitted++;
                }
            }

            DariusLog.Info("TITLE-MODEL-DIAG",
                "CharacterModelDisplay.Setup skin=" + __0 +
                " display=" + component.name +
                " id=" + component.GetInstanceID() +
                " scene=" + scene +
                " parent=" + parent +
                " worldPos=" + DariusLog.Vec(t.position) +
                " localPos=" + DariusLog.Vec(t.localPosition) +
                " localScale=" + DariusLog.Vec(t.localScale) +
                " active=" + component.gameObject.activeInHierarchy +
                " children=" + childCount +
                " childState=[" + children + "]");
        }
        catch (Exception e)
        {
            DariusLog.Exception("TITLE-MODEL-DIAG", e, "Failed inspecting Title CharacterModelDisplay for " + __0);
        }
    }

    private static float _nextHeroDetailLogTime;

    private static float _nextHeroDetailRefreshTime;

    private static int _lastHeroDetailWindowId;

    private static bool HeroDetailUpdateTextPrefix(object __instance)
    {
        if (__instance == null) return true;
        Hero_Darius hero = null;
        try
        {
            DewPlayer local = DewPlayer.local;
            hero = local != null ? local.hero as Hero_Darius : null;
        }
        catch { }
        if (hero == null) return true;

        // The stock detail window can call UpdateText many times per second while open. Darius'
        // reflection-based replacement only needs UI-rate refreshes, not frame-rate refreshes.
        int windowId = 0;
        try
        {
            UnityEngine.Object uo = __instance as UnityEngine.Object;
            windowId = uo != null ? uo.GetInstanceID() : __instance.GetHashCode();
        }
        catch { }
        if (windowId == _lastHeroDetailWindowId && Time.unscaledTime < _nextHeroDetailRefreshTime) return false;
        _lastHeroDetailWindowId = windowId;
        _nextHeroDetailRefreshTime = Time.unscaledTime + 0.20f;

        try
        {
            EntityStatus status = hero.Status;
            EntityAbility ability = hero.Ability;
            EntityControl control = hero.Control;
            if (status == null || ability == null || control == null) return true;

            var final = status.finalStats;
            float currentHealth = hero.currentHealth;
            float attackSpeedMultiplier = status.attackSpeedMultiplier;
            float movementSpeedMultiplier = status.movementSpeedMultiplier;
            float baseAgentSpeed = control.baseAgentSpeed;
            float attackCooldown = 0f;
            try
            {
                AttackTrigger attack = ability.attackAbility as AttackTrigger;
                if (attack == null) attack = DariusTravelerRegistry.AttackPrefab;
                if (attack != null && attack.configs != null && attack.configs.Length > 0 && attack.configs[0] != null)
                    attackCooldown = attack.configs[0].cooldownTime;
            }
            catch { }
            float attackSpeed = attackCooldown > 0.0001f ? (1f / attackCooldown) * attackSpeedMultiplier : 0f;
            float moveSpeed = movementSpeedMultiplier * baseAgentSpeed * 100f;

            int written = 0;
            written += SetUiText(__instance, "healthText", currentHealth.ToString("0") + "/" + final.maxHealth.ToString("0"));
            written += SetUiText(__instance, "adText", final.attackDamage.ToString("0.##"));
            written += SetUiText(__instance, "apText", final.abilityPower.ToString("0.##"));
            written += SetUiText(__instance, "skillHasteText", final.abilityHaste.ToString("0.##"));
            written += SetUiText(__instance, "attackSpeedText", attackSpeed.ToString("0.00"));
            written += SetUiText(__instance, "critChanceText", (final.critChance * 100f).ToString("0.#") + "%");
            written += SetUiText(__instance, "armorText", final.armor.ToString("0.##"));
            written += SetUiText(__instance, "fireAmpText", "x" + (1f + final.fireEffectAmp).ToString("0.##"));
            written += SetUiText(__instance, "movementSpeedText", moveSpeed.ToString("0.##"));

            if (Time.unscaledTime >= _nextHeroDetailLogTime)
            {
                _nextHeroDetailLogTime = Time.unscaledTime + 20f;
                DariusLog.DebugInfo("STAT-PANEL", "Darius live panel changedFields=" + written +
                    " HP=" + currentHealth.ToString("0.##") + "/" + final.maxHealth.ToString("0.##") +
                    " AD=" + final.attackDamage.ToString("0.##") + " AP=" + final.abilityPower.ToString("0.##") +
                    " haste=" + final.abilityHaste.ToString("0.##") + " atkSpeed=" + attackSpeed.ToString("0.###") +
                    " crit=" + final.critChance.ToString("0.###") + " armor=" + final.armor.ToString("0.##") +
                    " move=" + moveSpeed.ToString("0.##") + " attackCd=" + attackCooldown.ToString("0.###"));
            }

            // Darius live values were resolved successfully. Always skip the stock method for this
            // refresh, even if every string was already identical. Returning true when changed=0
            // would let stock text overwrite the live values and then make the next Darius refresh
            // overwrite them again, producing visible text jitter.
            return false;
        }
        catch (Exception e)
        {
            DariusLog.Exception("STAT-PANEL", e, "Failed to supply live Hero_Darius detail values; falling back to stock UI method");
            return true;
        }
    }

    private static int SetUiText(object owner, string fieldName, string value)
    {
        try
        {
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            FieldInfo field = owner.GetType().GetField(fieldName, flags);
            object textObject = field != null ? field.GetValue(owner) : null;
            if (textObject == null) return 0;
            PropertyInfo text = textObject.GetType().GetProperty("text", flags);
            if (text == null || !text.CanWrite) return 0;
            if (text.CanRead)
            {
                string existing = text.GetValue(textObject, null) as string;
                if (string.Equals(existing, value, StringComparison.Ordinal)) return 0;
            }
            text.SetValue(textObject, value, null);
            return 1;
        }
        catch { return 0; }
    }
}