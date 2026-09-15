using System;
using System.Reflection;
using HarmonyLib;
using Mirror;

// Watches the ControlManager layer rather than only SkillTrigger.OnCastComplete.
// A broken R press can fail during target selection / command reservation before the SkillTrigger
// ever sees OnCastComplete, so the R itself cannot diagnose that failure without an earlier hook.
public static class DariusRInputGuard
{
    private static bool _installed;

    public static void Install(Harmony harmony)
    {
        if (_installed || harmony == null) return;
        int patched = 0;
        string[] boolCastHelpers =
        {
            "CastAbilityAtCursor",
            "CastAbilityAuto",
            "CastAbilityGamepad",
            "CastAbilityInDirectionOfMovement",
            "CastAbilityWithAim"
        };

        for (int i = 0; i < boolCastHelpers.Length; i++)
        {
            MethodInfo method = AccessTools.Method(typeof(ControlManager), boolCastHelpers[i], new[] { typeof(AbilityTrigger) });
            if (method == null)
            {
                DariusLog.Warn("R-INPUT", "ControlManager." + boolCastHelpers[i] + " was not found; that wrapper will not have R recovery telemetry.");
                continue;
            }
            try
            {
                harmony.Patch(method,
                    prefix: new HarmonyMethod(AccessTools.Method(typeof(DariusRInputGuard), nameof(CastAttemptPrefix))),
                    postfix: new HarmonyMethod(AccessTools.Method(typeof(DariusRInputGuard), nameof(CastAttemptPostfix))));
                patched++;
            }
            catch (Exception e)
            {
                DariusLog.Exception("R-INPUT", e, "Failed patching ControlManager." + boolCastHelpers[i]);
            }
        }

        // The public void CastAbility(trigger, info, shouldMoveToCast) is the common command path
        // after target acquisition. Patch it as well so a future game/input mode that bypasses one
        // of the bool wrappers still records the actual R attempt. Same-frame de-duplication lives
        // inside St_Darius_NoxianGuillotine.
        try
        {
            MethodInfo core = AccessTools.Method(typeof(ControlManager), "CastAbility",
                new[] { typeof(AbilityTrigger), typeof(CastInfo), typeof(bool) });
            if (core != null)
            {
                harmony.Patch(core,
                    prefix: new HarmonyMethod(AccessTools.Method(typeof(DariusRInputGuard), nameof(CastCorePrefix))));
                patched++;
            }
            else DariusLog.Warn("R-INPUT", "ControlManager.CastAbility(AbilityTrigger,CastInfo,bool) was not found.");
        }
        catch (Exception e)
        {
            DariusLog.Exception("R-INPUT", e, "Failed patching ControlManager.CastAbility core path");
        }

        // Shape of Dreams also exposes a first-class cast-failure event. Subscribe on every
        // ControlManager instance; this catches explicit no-target / rejected-command failures
        // even when a wrapper's return value is not the final user-visible result.
        try
        {
            MethodInfo awake = AccessTools.Method(typeof(ControlManager), "Awake");
            if (awake != null)
            {
                harmony.Patch(awake,
                    postfix: new HarmonyMethod(AccessTools.Method(typeof(DariusRInputGuard), nameof(ControlManagerAwakePostfix))));
                patched++;
            }
            else DariusLog.Warn("R-INPUT", "ControlManager.Awake was not found; onCastFailed subscription is unavailable.");
        }
        catch (Exception e)
        {
            DariusLog.Exception("R-INPUT", e, "Failed patching ControlManager.Awake for onCastFailed subscription");
        }

        // In case the manager already exists when this mod boots, attach immediately too.
        try { AttachFailureEvent(ManagerBase<ControlManager>.softInstance); } catch { }

        _installed = true;
        DariusLog.Info("R-INPUT", "Installed ControlManager R-attempt/failure guard paths=" + patched + "/7");
    }

    private static void ControlManagerAwakePostfix(ControlManager __instance)
    {
        AttachFailureEvent(__instance);
    }

    private static void AttachFailureEvent(ControlManager manager)
    {
        if (manager == null) return;
        try
        {
            manager.onCastFailed -= OnCastFailed;
            manager.onCastFailed += OnCastFailed;
        }
        catch (Exception e)
        {
            DariusLog.Exception("R-INPUT", e, "Failed subscribing ControlManager.onCastFailed");
        }
    }

    private static void OnCastFailed(AbilityTrigger trigger)
    {
        St_Darius_NoxianGuillotine r = trigger as St_Darius_NoxianGuillotine;
        if (r == null) return;
        r.NotifyControlCastRejected("onCastFailed");
    }

    // Same hardening used by Cho'Gath R: the stock cursor/target wrapper can reject a runtime
    // ultimate before SkillTrigger.OnCastComplete even when the charge is visibly ready. Darius R
    // now owns that last input hop and then re-enters the native SkillTrigger completion pipeline.
    private static bool CastAttemptPrefix(AbilityTrigger trigger, MethodBase __originalMethod, ref bool __result)
    {
        St_Darius_NoxianGuillotine r = trigger as St_Darius_NoxianGuillotine;
        if (r == null) return true;
        if (!NetworkServer.active) return true;
        string source = __originalMethod != null ? __originalMethod.Name : "ControlManager.CastAbility";
        __result = r.TryCastDirectFromControl(source);
        return false;
    }

    private static void CastAttemptPostfix(AbilityTrigger trigger, bool __result, MethodBase __originalMethod)
    {
        if (__result) return;
        St_Darius_NoxianGuillotine r = trigger as St_Darius_NoxianGuillotine;
        if (r == null) return;
        r.NotifyControlCastRejected(__originalMethod != null ? __originalMethod.Name : "ControlManager.CastAbility");
    }

    private static bool CastCorePrefix(AbilityTrigger trigger, CastInfo info)
    {
        St_Darius_NoxianGuillotine r = trigger as St_Darius_NoxianGuillotine;
        if (r == null) return true;
        if (!NetworkServer.active) return true;
        r.TryCastDirectFromControl("CastAbility(core)", info);
        return false;
    }
}
