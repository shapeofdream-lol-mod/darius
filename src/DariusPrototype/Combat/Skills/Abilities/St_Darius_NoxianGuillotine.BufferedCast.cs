using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Mirror;
using UnityEngine;

public sealed partial class St_Darius_NoxianGuillotine : SkillTrigger
{
    private void QueueBufferedCast(int configIndex, CastInfo info, Hero caster)
    {
        if (caster == null) return;
        _bufferConfigIndex = configIndex;
        _bufferInfo = info;

        if (_bufferActive)
        {
            // Repeated presses refresh both the aim point and the timeout instead of spawning parallel casts.
            _bufferUntil = Time.unscaledTime + InputBufferWindow;
            DariusLog.DebugInfoThrottled("R-BUFFER", "repeat",
                "Repeated R input refreshed existing intent buffer. point=" + DariusLog.Vec(info.point), 0.10);
            return;
        }

        _bufferActive = true;
        _bufferUntil = Time.unscaledTime + InputBufferWindow;
        int token = ++_bufferToken;
        DariusLog.Info("R-BUFFER", "No legal target on the input frame; preserving R intent for " +
            InputBufferWindow.ToString("0.##") + "s. caster=" + DariusLog.EntityLabel(caster) +
            " point=" + DariusLog.Vec(info.point));
        StartCoroutine(ResolveBufferedCast(token));
    }

    private IEnumerator ResolveBufferedCast(int token)
    {
        Hero lastCaster = _bufferInfo.caster as Hero;
        while (_bufferActive && token == _bufferToken && Time.unscaledTime <= _bufferUntil)
        {
            yield return new WaitForSecondsRealtime(InputBufferPollInterval);
            if (!_bufferActive || token != _bufferToken) yield break;

            Hero caster = _bufferInfo.caster as Hero;
            lastCaster = caster;
            if (caster == null || caster.IsNullOrInactive()) break;

            // Do not gate this retry on CanBeCast(). The input already reached OnCastComplete but
            // base.OnCastComplete was intentionally not called, so a transient native busy flag is
            // precisely the state that previously caused the buffered press to be discarded.
            Entity target = ResolveCastTarget(caster, _bufferInfo, false);
            if (target == null) continue;

            int configIndex = _bufferConfigIndex;
            CastInfo bufferedInfo = _bufferInfo;
            _bufferActive = false;
            _bufferToken++;
            DariusLog.Info("R-BUFFER", "Buffered R acquired target=" + DariusLog.EntityLabel(target));
            CompleteResolvedCast(configIndex, bufferedInfo, caster, target, "buffered");
            yield break;
        }

        if (token == _bufferToken)
        {
            _bufferActive = false;
            // No gameplay cast happened and base.OnCastComplete was never called, so no charge/CD
            // state is ours to repair. Treat this as a clean failed intent.
            DariusLog.DebugInfo("R-BUFFER", "Buffered R intent expired without a legal target; native charge/cooldown state left untouched.");
        }
    }

    private void CancelBufferedCast(string reason)
    {
        if (!_bufferActive) return;
        _bufferActive = false;
        _bufferToken++;
        DariusLog.DebugInfo("R-BUFFER", "Cancelled pending R buffer: " + reason);
    }

    public void NotifyCooldownShouldBeReady(string reason)
    {
        // Kept as a compatibility name for existing callers. R readiness is charge-authoritative.
        int token = ++_readyWatchdogToken;
        StartCoroutine(ReadyStateWatchdog(token, reason));
    }

    public void RestoreUltimateCharge(string reason)
    {
        Hero caster = owner as Hero;
        // Execute/refund is a confirmed post-cast terminal state, so restoring the complete
        // charge/cooldown/minimum-delay tuple is legitimate here. Failed input intents never call this.
        ForceChargeReady(caster, reason, true);
    }
}