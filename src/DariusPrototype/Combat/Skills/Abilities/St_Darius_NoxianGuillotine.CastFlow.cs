using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Mirror;
using UnityEngine;

public sealed partial class St_Darius_NoxianGuillotine : SkillTrigger
{
    public override AbilityInstance OnCastComplete(int configIndex, CastInfo info)
    {
        Hero caster = info.caster as Hero;
        if (caster == null) return null;

        // Reaching this method proves the ControlManager/native input path did not eat the press.
        // Cancel any input-layer watchdog that was waiting to diagnose a pre-OnCastComplete stall.
        _lastOnCastCompleteAt = Time.unscaledTime;
        _controlAttemptToken++;

        // A second press during the 0.42 s execute animation is an intent for the next reset cast,
        // not a second parallel cast. Preserve the latest intent and consume it after the execute.
        if (_executionActive)
        {
            _queuedDuringExecution = true;
            _queuedConfigIndex = configIndex;
            _queuedInfo = info;
            _queuedUntil = Time.unscaledTime + QueuedIntentWindow;
            DariusLog.DebugInfoThrottled("R-INTENT", "during-execute",
                "R input queued while execute is active. point=" + DariusLog.Vec(info.point), 0.10);
            return null;
        }

        Entity target = ResolveCastTarget(caster, info, true);
        if (target == null)
        {
            QueueBufferedCast(configIndex, info, caster);
            return null;
        }

        CancelBufferedCast("resolved immediately");
        return CompleteResolvedCast(configIndex, info, caster, target, "immediate");
    }

    private AbilityInstance CompleteResolvedCast(int configIndex, CastInfo inputInfo, Hero caster, Entity target, string source)
    {
        if (caster == null || target == null || _executionActive) return null;

        CastInfo resolvedInfo = new CastInfo(caster, target);
        DariusLog.Info("R-TRIGGER", "Resolved R target through point-intent layer; entering native AbilityInstance pipeline source=" + source +
            " configIndex=" + configIndex + " caster=" + DariusLog.EntityLabel(caster) +
            " target=" + DariusLog.EntityLabel(target) + " inputPoint=" + DariusLog.Vec(inputInfo.point));

        try
        {
            int chargeBefore = GetCurrentChargeSafe();
            AbilityInstance instance = base.OnCastComplete(configIndex, resolvedInfo);
            if (instance != null)
            {
                _executionActive = true;
                int executionToken = ++_executionToken;
                StartCoroutine(ExecutionWatchdog(executionToken, caster));
                DariusLog.Info("R-TRIGGER", "Native R AbilityInstance spawned=" + instance.name +
                    " charge=" + chargeBefore + "->" + GetCurrentChargeSafe() +
                    " recharge=" + CurrentRechargeTimeSafe().ToString("0.###"));
                return instance;
            }
            DariusLog.Warn("R-TRIGGER", "Native R completion returned no AbilityInstance; using compatibility fallback execution.");
        }
        catch (Exception e)
        {
            DariusLog.Exception("R-TRIGGER", e, "Native R AbilityInstance pipeline failed; using compatibility fallback");
        }

        // Safety net for a future game build that rejects runtime AbilityInstance prefabs. The normal
        // route above is required for full Gem compatibility and should be the only path seen in logs.
        _executionActive = true;
        int fallbackToken = ++_executionToken;
        try
        {
            StartCoroutine(RunResolvedExecution(resolvedInfo, fallbackToken));
            StartCoroutine(ExecutionWatchdog(fallbackToken, caster));
            DariusLog.Warn("R-FALLBACK", "Direct R execution started because native AbilityInstance creation failed.");
        }
        catch (Exception e)
        {
            _executionActive = false;
            DariusLog.Exception("R-FALLBACK", e, "Direct R fallback failed to start");
            RecoverNativeReadyState(caster, "R fallback failed to start", true);
        }
        return null;
    }

    private IEnumerator RunResolvedExecution(CastInfo resolvedInfo, int executionToken)
    {
        yield return Ai_Darius_NoxianGuillotine.Execute(resolvedInfo, this, null);
        if (executionToken != _executionToken) yield break;
        NotifyNativeExecutionFinished("compatibility fallback completed");
    }

    public void NotifyNativeExecutionFinished(string reason)
    {
        if (!_executionActive) return;
        int executionToken = _executionToken;
        _executionActive = false;
        DariusLog.DebugInfo("R-STATE", "R execution state released reason=" + (reason ?? "<none>"));
        if (_queuedDuringExecution) StartCoroutine(ConsumeQueuedIntent(executionToken));
    }

    // Called by the spawned AbilityInstance when Unity/native action interruption destroys the
    // execute actor before its normal terminal callback. This is the path that previously let a
    // basic attack/action interruption leave charge/minimum-delay/locks desynchronized forever.
    public void NotifyNativeExecutionInterrupted(string reason)
    {
        Hero caster = owner as Hero;
        DariusLog.Warn("R-INTERRUPT", "Native Guillotine execution was interrupted before terminal cleanup. reason=" +
            (reason ?? "<none>") + " charge=" + GetCurrentChargeSafe() +
            " recharge=" + CurrentRechargeTimeSafe().ToString("0.###"));
        RecoverNativeReadyState(caster, "interrupted execute: " + (reason ?? "unknown"), true);
    }

    private IEnumerator ExecutionWatchdog(int executionToken, Hero caster)
    {
        yield return new WaitForSeconds(ExecutionWatchdogDelay);
        if (executionToken != _executionToken || !_executionActive) yield break;

        // The authored execute is only ~0.42 s. Reaching 1.20 s means the native instance/action
        // failed to reach its terminal callback. Merely clearing our private latch was not enough:
        // charge/cooldown/minimumDelay or a cast-until-killed lock could remain stale and make R
        // permanently unusable. Treat this as a failed execute and restore the complete ready tuple.
        DariusLog.Warn("R-WATCHDOG", "Execute state exceeded " + ExecutionWatchdogDelay.ToString("0.##") +
            "s; performing full Guillotine recovery (charge/cooldown/minimumDelay/cast locks).");
        RecoverNativeReadyState(caster, "execution watchdog timeout", true);
    }

    private IEnumerator ConsumeQueuedIntent(int executionToken)
    {
        while (_queuedDuringExecution && Time.unscaledTime <= _queuedUntil)
        {
            yield return new WaitForSecondsRealtime(InputBufferPollInterval);
            if (_executionActive || executionToken != _executionToken) yield break;

            Hero caster = _queuedInfo.caster as Hero;
            if (caster == null || caster.IsNullOrInactive()) break;

            bool ready = false;
            try { ready = CanBeCast(); } catch { ready = true; }
            if (!ready) continue;

            int configIndex = _queuedConfigIndex;
            CastInfo queuedInfo = _queuedInfo;
            _queuedDuringExecution = false;
            Entity target = ResolveCastTarget(caster, queuedInfo, false);
            if (target != null)
            {
                DariusLog.Info("R-INTENT", "Consuming queued reset-cast intent target=" + DariusLog.EntityLabel(target));
                CompleteResolvedCast(configIndex, queuedInfo, caster, target, "queued-after-execute");
            }
            else
            {
                QueueBufferedCast(configIndex, queuedInfo, caster);
            }
            yield break;
        }

        if (_queuedDuringExecution)
        {
            _queuedDuringExecution = false;
            DariusLog.DebugInfo("R-INTENT", "Queued execute-time R input expired because the skill never became ready.");
        }
    }
}