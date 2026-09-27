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
        DariusLog.DebugInfo("PIPELINE", "skill=R stage=trigger-input config=" + configIndex +
            " caster=" + DariusLog.EntityLabel(info.caster) +
            " target=" + DariusLog.EntityLabel(info.target) +
            " point=" + DariusLog.Vec(info.point) +
            " state={" + DariusRuntimeAudit.DescribeAbilityState(this, configIndex) + "}");
        if (caster == null) return null;

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
        DariusLog.DebugInfo("PIPELINE", "skill=R stage=target-resolution-output config=" + configIndex +
            " target=" + DariusLog.EntityLabel(target) + " buffered=" + (target == null));
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
        DariusLog.DebugInfo("PIPELINE", "skill=R stage=native-complete-input source=" + source +
            " config=" + configIndex + " caster=" + DariusLog.EntityLabel(caster) +
            " target=" + DariusLog.EntityLabel(target) +
            " state={" + DariusRuntimeAudit.DescribeAbilityState(this, configIndex) + "}");

        try
        {
            int chargeBefore = GetCurrentChargeSafe();
            AbilityInstance instance = base.OnCastComplete(configIndex, resolvedInfo);
            DariusLog.DebugInfo("PIPELINE", "skill=R stage=native-complete-output source=" + source +
                " config=" + configIndex + " result=" + (instance != null ? instance.name : "<null>") +
                " charge=" + chargeBefore + "->" + GetCurrentChargeSafe() +
                " recharge=" + CurrentRechargeTimeSafe().ToString("0.###") +
                " state={" + DariusRuntimeAudit.DescribeAbilityState(this, configIndex) + "}");
            if (instance == null)
            {
                DariusLog.Error("R-TRIGGER", "Native R OnCastComplete returned no AbilityInstance; direct fallback is disabled. state={" +
                    DariusRuntimeAudit.DescribeAbilityState(this, configIndex) + "}");
                return null;
            }

            _executionActive = true;
            int executionToken = ++_executionToken;
            StartCoroutine(ExecutionWatchdog(executionToken, caster));
            DariusLog.Info("R-TRIGGER", "Native R AbilityInstance spawned=" + instance.name +
                " charge=" + chargeBefore + "->" + GetCurrentChargeSafe() +
                " recharge=" + CurrentRechargeTimeSafe().ToString("0.###") +
                " state={" + DariusRuntimeAudit.DescribeAbilityState(this, configIndex) + "}");
            return instance;
        }
        catch (Exception e)
        {
            DariusLog.Exception("R-TRIGGER", e, "Native R AbilityInstance pipeline failed; no direct fallback will run. state={" +
                DariusRuntimeAudit.DescribeAbilityState(this, configIndex) + "}");
            throw;
        }
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