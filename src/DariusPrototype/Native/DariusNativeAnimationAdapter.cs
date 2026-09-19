using System;
using UnityEngine;

// Thin adapter between Darius presentation hooks and the active native model bridge.
// Shape of Dreams' EntityAnimation API accepts DewAnimationClip resources, while the native
// AssetBundle path owns Unity AnimationClips in its AnimatorController. Do not fabricate
// DewAnimationClip wrappers at runtime; dispatch the known Darius states to the bridge instead.
internal static class DariusNativeAnimationAdapter
{
    public static bool PlayAbility(Hero hero, string animationName, bool instant = false)
    {
        Vector3 direction = hero != null && hero.transform != null ? hero.transform.forward : Vector3.forward;
        return PlayAbility(hero, animationName, direction, instant);
    }

    public static bool PlayAbility(Hero hero, string animationName, Vector3 direction, bool instant = false)
    {
        if (hero == null || string.IsNullOrEmpty(animationName)) return false;

        DariusOfficialActionRuntime official = GetOfficialActionRuntime(hero);
        if (official != null && official.IsReady)
        {
            try
            {
                switch (animationName)
                {
                    case "Spell1":
                        return official.PlayQ(instant);
                    case "Spell2":
                        return official.PlayW(direction);
                    case "Spell2_Idle":
                        return official.SetWArmed(true);
                    case "Spell3":
                        return official.PlayE();
                    case "Spell4":
                        return official.PlayR();
                    case "Attack1":
                        return official.PlayAttack(false, false, direction);
                    case "Attack2":
                        return official.PlayAttack(true, false, direction);
                    case "Crit":
                        return official.PlayAttack(false, true, direction);
                }
            }
            catch (Exception e)
            {
                DariusLog.Exception("OFFICIAL-ACTION", e, "Fresh EntityModel action dispatch failed: " + animationName);
                return true;
            }
        }

        DariusNativeModelBridge bridge = GetBridge(hero);
        if (bridge == null) return false;

        try
        {
            switch (animationName)
            {
                case "Spell1":
                    bridge.PlayQ(instant);
                    return true;
                case "Spell2":
                    bridge.PlayWAttack(direction);
                    return true;
                case "Spell2_Idle":
                    bridge.SetWArmed(true);
                    return true;
                case "Spell3":
                case "Spell4":
                    bridge.PlayOneShot(animationName);
                    return true;
                case "Attack1":
                    bridge.PlayAttack(false, false, direction);
                    return true;
                case "Attack2":
                    bridge.PlayAttack(true, false, direction);
                    return true;
                case "Crit":
                    bridge.PlayAttack(false, true, direction);
                    return true;
                default:
                    return false;
            }
        }
        catch (Exception e)
        {
            DariusLog.Exception("NATIVE-ANIM", e, "Native bridge animation dispatch failed: " + animationName);
            return true;
        }
    }

    public static bool StopAbility(Hero hero)
    {
        if (hero == null) return false;

        DariusOfficialActionRuntime official = GetOfficialActionRuntime(hero);
        if (official != null && official.IsReady)
        {
            try
            {
                return official.SetWArmed(false);
            }
            catch (Exception e)
            {
                DariusLog.Exception("OFFICIAL-ACTION", e, "Fresh EntityModel StopAbility failed");
                return true;
            }
        }

        DariusNativeModelBridge bridge = GetBridge(hero);
        if (bridge == null) return false;

        try
        {
            bridge.SetWArmed(false);
            return true;
        }
        catch (Exception e)
        {
            DariusLog.Exception("NATIVE-ANIM", e, "Native bridge StopAbility failed");
            return true;
        }
    }

    private static DariusOfficialActionRuntime GetOfficialActionRuntime(Hero hero)
    {
        Hero_Darius darius = hero as Hero_Darius;
        if (darius == null || darius.Visual == null || darius.Visual.model == null) return null;
        return darius.Visual.model.GetComponent<DariusOfficialActionRuntime>();
    }

    private static DariusNativeModelBridge GetBridge(Hero hero)
    {
        Hero_Darius darius = hero as Hero_Darius;
        if (darius != null) return darius.NativeModelBridge;

        DariusNativeModelBridge bridge = hero.GetComponentInChildren<DariusNativeModelBridge>(true);
        return bridge != null && bridge.IsReady ? bridge : null;
    }
}
