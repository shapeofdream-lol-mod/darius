using System;
using UnityEngine;

// Thin adapter between Darius presentation hooks and the fresh native EntityModel action overlay.
// SoD owns gameplay/action lifecycle; this adapter only maps Darius semantic clip names onto the
// model-local presentation layer.
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

        DariusOfficialActionRuntime action = GetOfficialActionRuntime(hero);
        if (action == null || !action.IsReady)
        {
            DariusLog.Error("OFFICIAL-ACTION",
                "Fresh EntityModel action runtime unavailable clip=" + animationName +
                " hero=" + DariusLog.EntityLabel(hero));
            return false;
        }

        try
        {
            switch (animationName)
            {
                case "Spell1":
                    return action.PlayQ(instant);
                case "Spell2":
                    return action.PlayW(direction);
                case "Spell2_Idle":
                    return action.SetWArmed(true);
                case "Spell3":
                    return action.PlayE();
                case "Spell4":
                    return action.PlayR();
                case "Attack1":
                    return action.PlayAttack(false, false, direction);
                case "Attack2":
                    return action.PlayAttack(true, false, direction);
                case "Crit":
                    return action.PlayAttack(false, true, direction);
                default:
                    return false;
            }
        }
        catch (Exception e)
        {
            DariusLog.Exception("OFFICIAL-ACTION", e, "Fresh EntityModel action dispatch failed: " + animationName);
            return true;
        }
    }

    public static bool StopAbility(Hero hero)
    {
        if (hero == null) return false;
        DariusOfficialActionRuntime action = GetOfficialActionRuntime(hero);
        if (action == null || !action.IsReady) return false;

        try
        {
            return action.SetWArmed(false);
        }
        catch (Exception e)
        {
            DariusLog.Exception("OFFICIAL-ACTION", e, "Fresh EntityModel StopAbility failed");
            return true;
        }
    }

    private static DariusOfficialActionRuntime GetOfficialActionRuntime(Hero hero)
    {
        Hero_Darius darius = hero as Hero_Darius;
        EntityModel model = darius != null && darius.Visual != null ? darius.Visual.model : null;
        return model != null ? model.GetComponent<DariusOfficialActionRuntime>() : null;
    }
}
