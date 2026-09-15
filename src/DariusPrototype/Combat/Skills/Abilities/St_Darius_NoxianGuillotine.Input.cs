using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Mirror;
using UnityEngine;

public sealed partial class St_Darius_NoxianGuillotine : SkillTrigger
{
    // A false result means ControlManager rejected the press before R gameplay started. If there is
    // a legal enemy and the charge is available, perform a full restore and retry the same intent
    // directly once. This covers the cursor/soft-target failure case requested by the user.
    public void NotifyControlCastRejected(string source)
    {
        if (!NetworkServer.active || _executionActive) return;
        if (Time.frameCount == _lastControlRejectFrame) return;
        _lastControlRejectFrame = Time.frameCount;
        DariusLog.DebugInfoThrottled("R-INPUT", "rejected:" + source,
            "ControlManager rejected R before cast-start; no charge/cooldown was modified by Darius recovery code.", 0.25);
    }

    private IEnumerator ControlAttemptWatchdog(int token, float attemptAt, string source)
    {
        yield return new WaitForSecondsRealtime(ControlAttemptWatchdogDelay);
        if (token != _controlAttemptToken || _executionActive) yield break;
        if (_lastOnCastCompleteAt >= attemptAt) yield break;

        Hero caster = owner as Hero;
        if (caster == null || caster.IsNullOrInactive()) yield break;
        DariusLog.DebugInfoThrottled("R-WATCHDOG", "no-cast-start:" + source,
            "R intent did not reach OnCastComplete within watchdog window; clearing only custom intent state. " +
            "charge=" + GetCurrentChargeSafe() + " recharge=" + CurrentRechargeTimeSafe().ToString("0.###"), 0.25);
        CancelBufferedCast("input watchdog timeout");
        _queuedDuringExecution = false;
    }

    private IEnumerator ReadyStateWatchdog(int token, string reason)
    {
        yield return new WaitForSecondsRealtime(ReadyWatchdogDelay);
        if (token != _readyWatchdogToken) yield break;
        DariusLog.DebugInfo("R-READY", "Post-terminal-state check reason=" + reason +
            " charge=" + GetCurrentChargeSafe() + " recharge=" + CurrentRechargeTimeSafe().ToString("0.###") +
            " canBeCast=" + SafeCanBeCast());
    }

    private void RecoverNativeReadyState(Hero caster, string reason, bool restoreCharge)
    {
        // Full trigger reset. Any coroutine reaching an old execution/buffer token after this point
        // becomes inert, so a failed input cannot leave a hidden Darius-side busy state behind.
        _executionActive = false;
        _executionToken++;
        _bufferActive = false;
        _bufferToken++;
        _queuedDuringExecution = false;
        _queuedUntil = 0f;
        _controlAttemptToken++;
        try { DariusTriggerConfigRuntimeEditor.AttachAll(this); } catch { }
        ForceChargeReady(caster, reason, restoreCharge);
        StartCoroutine(LogRecoveredReadyState(reason));
    }

    private void TryUnlockCastState(int configIndex)
    {
        try
        {
            // AbilityTrigger's exact cast-lock helper is not public in every game build. Resolve it
            // narrowly by name instead of hard-binding a private API. If present, invoke it for the
            // same config indices whose cooldown locks we clear.
            MethodInfo method = typeof(AbilityTrigger).GetMethod("UnlockCast",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null, new[] { typeof(int) }, null);
            if (method != null)
            {
                method.Invoke(this, new object[] { configIndex });
                return;
            }

            // Some builds name the per-instance lock release after the spawned actor rather than
            // the cast. Keep the fallback list deliberately small; never scan/mutate arbitrary fields.
            foreach (string name in new[] { "UnlockCastUntilKilled", "ReleaseCastLock" })
            {
                method = typeof(AbilityTrigger).GetMethod(name,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                    null, new[] { typeof(int) }, null);
                if (method == null) continue;
                method.Invoke(this, new object[] { configIndex });
                return;
            }
        }
        catch (Exception e)
        {
            DariusLog.DebugInfo("R-RECOVER", "Cast-lock reflection cleanup skipped config=" + configIndex +
                " reason=" + e.GetType().Name);
        }
    }

    private void ForceChargeReady(Hero caster, string reason, bool restoreCharge)
    {
        try
        {
            if (restoreCharge) SetChargeAll(1);
            SetCooldownTimeAll(0f, false);
            SetMinimumDelayAll(0f);
            if (configs != null)
            {
                for (int i = 0; i < configs.Length; i++)
                {
                    try { UnlockCooldown(i); } catch { }
                    TryUnlockCastState(i);
                }
            }

            // Public arrays are the authoritative runtime state. Write the same values as a backup
            // in case an interrupted native cast left one configuration desynchronized.
            if (restoreCharge && currentCharges != null)
                for (int i = 0; i < currentCharges.Length; i++) currentCharges[i] = 1;
            if (currentUnscaledCooldownTimes != null)
                for (int i = 0; i < currentUnscaledCooldownTimes.Length; i++) currentUnscaledCooldownTimes[i] = 0f;
            if (currentMinimumDelays != null)
                for (int i = 0; i < currentMinimumDelays.Length; i++) currentMinimumDelays[i] = 0f;

            DariusLog.Info("R-RECOVER", "Ultimate charge state restored reason=" + reason +
                " restoreCharge=" + restoreCharge + " charge=" + GetCurrentChargeSafe() +
                " recharge=" + CurrentRechargeTimeSafe().ToString("0.###"));
        }
        catch (Exception e)
        {
            DariusLog.Exception("R-RECOVER", e, "Failed fully restoring R state reason=" + reason);
        }
    }

    private IEnumerator LogRecoveredReadyState(string reason)
    {
        yield return null;
        DariusLog.Info("R-RECOVER", "Post-recovery verification reason=" + reason +
            " charge=" + GetCurrentChargeSafe() + " recharge=" + CurrentRechargeTimeSafe().ToString("0.###") +
            " canBeCast=" + SafeCanBeCast());
    }

    private int GetCurrentChargeSafe()
    {
        try { return currentConfigCurrentCharge; } catch { }
        try
        {
            int index = Mathf.Clamp(currentConfigIndex, 0, currentCharges != null ? currentCharges.Length - 1 : 0);
            if (currentCharges != null && currentCharges.Length > 0) return currentCharges[index];
        }
        catch { }
        return 0;
    }

    private float CurrentRechargeTimeSafe()
    {
        try { return currentConfigUnscaledCooldownTime; } catch { }
        try
        {
            int index = Mathf.Clamp(currentConfigIndex, 0, currentUnscaledCooldownTimes != null ? currentUnscaledCooldownTimes.Length - 1 : 0);
            if (currentUnscaledCooldownTimes != null && currentUnscaledCooldownTimes.Length > 0) return currentUnscaledCooldownTimes[index];
        }
        catch { }
        return 0f;
    }

    private bool SafeCanBeCast()
    {
        try { return CanBeCast(); } catch { return GetCurrentChargeSafe() > 0; }
    }
}