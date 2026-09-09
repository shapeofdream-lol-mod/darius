using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Mirror;
using UnityEngine;

public sealed class St_Darius_NoxianGuillotine : SkillTrigger
{
    // The gameplay execution still has one target, like League Darius R. The SoD native
    // CastMethodType.Target acquisition proved too brittle for a runtime-created Hero skill:
    // if the pointer was not directly on an enemy collider, the input could be rejected before
    // OnCastComplete and there was no Darius-side log at all. Use a point cast as the input layer,
    // then resolve the intended enemy in a tightly bounded aim-assist pass.
    public const float CastRange = 6.0f;
    public const float MaxResolvedCenterRange = 6.75f;
    public const float AimPointAssistRadius = 3.0f;
    public const float AimDirectionAssistAngle = 80.0f;
    public const float InputBufferWindow = 0.40f;
    public const float InputBufferPollInterval = 0.025f;
    public const float QueuedIntentWindow = 0.60f;
    public const float ReadyWatchdogDelay = 0.18f;
    public const float ExecutionWatchdogDelay = 1.20f;
    public const float ControlAttemptWatchdogDelay = 0.20f;

    private bool _bufferActive;
    private int _bufferToken;
    private int _bufferConfigIndex;
    private CastInfo _bufferInfo;
    private float _bufferUntil;

    private bool _executionActive;
    private int _executionToken;
    private bool _queuedDuringExecution;
    private int _queuedConfigIndex;
    private CastInfo _queuedInfo;
    private float _queuedUntil;
    private int _readyWatchdogToken;
    private int _controlAttemptToken;
    private int _lastControlAttemptFrame = -1;
    private int _lastControlRejectFrame = -1;
    private float _lastOnCastCompleteAt = -999f;
    private string _lastHardResetRoomToken;

    protected override void OnPrepare()
    {
        try
        {
            base.OnPrepare();
            if (configs == null || configs.Length < 2 || configs[0] == null || configs[1] == null)
                configs = new TriggerConfig[]
                {
                    DariusTriggerConfigRuntimeEditor.CreateBase(this, 24.0f),
                    DariusTriggerConfigRuntimeEditor.CreateBase(this, 24.0f)
                };
            else
            {
                DariusTriggerConfigRuntimeEditor.AttachAll(this);
                for (int i = 0; i < configs.Length; i++)
                {
                    if (configs[i] == null) continue;
                    DariusTriggerConfigRuntimeEditor.SetManaCost(configs[i], 0f);
                    DariusTriggerConfigRuntimeEditor.SetMaxCharges(configs[i], 1);
                    DariusTriggerConfigRuntimeEditor.SetAddedCharges(configs[i], 1);
                    configs[i].startCharges = 1;
                    // For R, cooldownTime is the recharge duration of the single ultimate charge.
                    // Availability is governed by currentCharges, not by a second ordinary-CD gate.
                    DariusTriggerConfigRuntimeEditor.SetCooldownTime(configs[i], 24.0f);
                    DariusTriggerConfigRuntimeEditor.SetMinimumDelay(configs[i], 0.02f);
                }
            }

            for (int i = 0; i < configs.Length; i++)
            {
                TriggerConfig cfg = configs[i];
                if (cfg == null) continue;
                // Standard SkillTrigger -> AbilityInstance -> PrepareAndSpawn is the authoritative path.
                // This gives Gems a real EventInfoCast.instance and preserves parentActor/firstTrigger.
                cfg.spawnedInstance = DariusFormalRegistry.NoxianGuillotineAbility;
                // R owns every visual/audio beat itself. Explicitly sever inherited template
                // presentation every time OnPrepare runs so no stock impact/cast sound can leak in.
                cfg.startAnim = null;
                cfg.endAnim = null;
                cfg.castVoice = null;
                cfg.effectOnCast = null;
                cfg.appliedStatusEffect = null;
                cfg.destroyExistingEffect = false;
                if (cfg.triggerIcon == null) cfg.triggerIcon = DariusPrototypeIcons.Get("R");
                cfg.isActive = true;
                cfg.canReceiveCooldownReduction = true;
                cfg.faceForward = true;
                // Runtime-created point casts can leave a remote client in the aim-confirm layer
                // without ever submitting the command to the host. R needs no second click: submit
                // the current facing/point immediately and let the server resolve the legal target.
                cfg.alwaysCastImmediately = true;
                cfg.castMethod = cfg.castMethod ?? new CastMethodData();

                // v0.18.5: collect an aim point first, then resolve the enemy ourselves. Native Target
                // mode could eat the button press before this trigger was reached at all.
                cfg.castMethod.type = CastMethodType.Point;
                cfg.castMethod._range = CastRange;
                cfg.castMethod._radius = AimPointAssistRadius;
                cfg.castMethod._isClamping = true;
                // Point input is only an aiming gesture. Native target validation can reject an
                // empty cursor point before Darius' own target resolver runs, which made R appear
                // to do nothing. The execute validates and resolves a living enemy itself.
                cfg.targetValidator = null;
            }

            Hero preparedCaster = owner as Hero;
            bool awooActive = preparedCaster != null && DariusEquipmentRuntime.Get(preparedCaster, false) != null &&
                DariusEquipmentRuntime.Get(preparedCaster, false).GetLevel(DariusEquipmentStarIds.Awoo) > 0;
            ApplyAwooCooldownOverride(awooActive);

            DariusLog.Info("R-CONFIG", "OnPrepare configured point-aim execute cooldown=" + (awooActive ? "12" : "24") + " castRange=" + CastRange +
                " assistRadius=" + AimPointAssistRadius + " assistAngle=" + AimDirectionAssistAngle +
                " spawnedInstance=" + (DariusFormalRegistry.NoxianGuillotineAbility != null ? DariusFormalRegistry.NoxianGuillotineAbility.name : "<null>") +
                " icon=" + (configs.Length > 0 && configs[0] != null && configs[0].triggerIcon != null));
        }
        catch (Exception e)
        {
            DariusLog.Exception("R-CONFIG", e, "OnPrepare failed");
            throw;
        }
    }

    protected override void OnLevelChange(int oldLevel, int newLevel)
    {
        // Current 1.3.x can set level before the spawned SkillTrigger has an owner.
        // Calling the vanilla implementation in that transient state dereferences null.
        if (newLevel < 1) return;
        if (owner == null)
        {
            DariusLog.DebugInfo("LEVEL", name + " deferred base OnLevelChange old=" + oldLevel + " new=" + newLevel + " owner=<null>");
            ClientSkillEvent_OnLevelChange?.Invoke(oldLevel, newLevel);
            DariusMemoryScaling.LogLevelChange(this, oldLevel, newLevel, 24.0f);
            return;
        }
        base.OnLevelChange(oldLevel, newLevel);
        DariusMemoryScaling.LogLevelChange(this, oldLevel, newLevel, 24.0f);
    }

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

    // Server-side direct input path. Darius R was still intermittently eaten inside the stock
    // ControlManager targeting wrappers, so the ready charge now resolves its own legal target and
    // enters SkillTrigger.base.OnCastComplete directly. Native cooldown/Gem/AbilityInstance behavior
    // remains authoritative after this input hop.
    public bool TryCastDirectFromControl(string source)
    {
        if (!NetworkServer.active) return false;
        Hero caster = owner as Hero;
        if (caster == null || caster.IsNullOrInactive()) return false;
        Entity target = FindRecoveryTarget(caster);
        if (target == null)
        {
            DariusLog.DebugInfoThrottled("R-DIRECT", "no-target:" + source,
                "Direct R input found no legal enemy in Guillotine range; input left unconsumed. source=" + source, 0.20);
            return false;
        }
        CastInfo info = new CastInfo(caster, target);
        return TryCastDirectResolved(source, info, caster, target);
    }

    public bool TryCastDirectFromControl(string source, CastInfo inputInfo)
    {
        if (!NetworkServer.active) return false;
        Hero caster = inputInfo.caster as Hero;
        if (caster == null) caster = owner as Hero;
        if (caster == null || caster.IsNullOrInactive()) return false;
        Entity target = ResolveCastTarget(caster, inputInfo, false);
        if (target == null) target = FindRecoveryTarget(caster);
        if (target == null)
        {
            DariusLog.DebugInfoThrottled("R-DIRECT", "no-target:" + source,
                "Direct R core input found no legal enemy in Guillotine range; input left unconsumed. source=" + source, 0.20);
            return false;
        }
        return TryCastDirectResolved(source, inputInfo, caster, target);
    }

    private bool TryCastDirectResolved(string source, CastInfo inputInfo, Hero caster, Entity target)
    {
        if (caster == null || target == null) return false;
        int configIndex = 0;
        try
        {
            int max = configs != null ? configs.Length - 1 : 0;
            configIndex = Mathf.Clamp(currentConfigIndex, 0, Mathf.Max(0, max));
        }
        catch { configIndex = 0; }

        if (_executionActive)
        {
            _queuedDuringExecution = true;
            _queuedConfigIndex = configIndex;
            _queuedInfo = new CastInfo(caster, target);
            _queuedUntil = Time.unscaledTime + QueuedIntentWindow;
            DariusLog.DebugInfoThrottled("R-DIRECT", "queue:" + source,
                "Direct R input arrived during Guillotine execution and was queued for the next ready state. source=" + source, 0.10);
            return true;
        }

        int charge = GetCurrentChargeSafe();
        float recharge = CurrentRechargeTimeSafe();
        if (charge <= 0)
        {
            DariusLog.DebugInfoThrottled("R-DIRECT", "cooldown:" + source,
                "Direct R input correctly rejected because no Guillotine charge is ready. recharge=" + recharge.ToString("0.###") +
                " source=" + source, 0.20);
            return false;
        }

        if (!SafeCanBeCast() && recharge <= 0.001f)
        {
            try
            {
                SetMinimumDelayAll(0f);
                if (currentMinimumDelays != null)
                    for (int i = 0; i < currentMinimumDelays.Length; i++) currentMinimumDelays[i] = 0f;
            }
            catch { }
            DariusLog.Warn("R-DIRECT", "Guillotine had a ready charge but CanBeCast=false; cleared stale minimum-delay gate before direct execution. source=" + source);
        }

        _lastOnCastCompleteAt = Time.unscaledTime;
        _controlAttemptToken++;
        CancelBufferedCast("direct ControlManager interception");
        int beforeToken = _executionToken;
        AbilityInstance instance = CompleteResolvedCast(configIndex, inputInfo, caster, target, "direct-control:" + source);
        bool started = instance != null || _executionActive || _executionToken != beforeToken;
        DariusLog.Info("R-DIRECT", "ControlManager R interception source=" + source +
            " target=" + DariusLog.EntityLabel(target) + " started=" + started +
            " charge=" + charge + "->" + GetCurrentChargeSafe() +
            " recharge=" + CurrentRechargeTimeSafe().ToString("0.###"));
        return started;
    }

    // User-requested absolute safety net: entering a new gameplay room invalidates every
    // transient Guillotine state (input buffer, execute latch, watchdogs, native charge timers).
    // Skill level/upgrades and constellation progression are deliberately untouched.
    public void HardResetForNewRoom(string roomName)
    {
        if (!NetworkServer.active) return;
        if (string.Equals(_lastHardResetRoomToken, roomName, StringComparison.Ordinal)) return;
        _lastHardResetRoomToken = roomName;
        Hero caster = owner as Hero;
        _executionActive = false;
        _executionToken++;
        _bufferActive = false;
        _bufferToken++;
        _queuedDuringExecution = false;
        _queuedUntil = 0f;
        _readyWatchdogToken++;
        _controlAttemptToken++;
        _lastControlAttemptFrame = -1;
        _lastControlRejectFrame = -1;
        _lastOnCastCompleteAt = -999f;
        try { DariusTriggerConfigRuntimeEditor.AttachAll(this); } catch { }
        bool awooActive = caster != null && DariusEquipmentRuntime.Get(caster, false) != null &&
            DariusEquipmentRuntime.Get(caster, false).GetLevel(DariusEquipmentStarIds.Awoo) > 0;
        ApplyAwooCooldownOverride(awooActive);
        ForceChargeReady(caster, "new-room-hard-reset:" + roomName, true);
        DariusLog.Info("R-ROOM-RESET", "Completely reset transient Guillotine state for room=" + roomName +
            " while preserving memory level=" + DariusMemoryScaling.GetLevel(this) + ".");
    }

    // Awoo is a direct R mechanic rewrite, so it owns the R recharge configuration rather
    // than multiplying a generic cooldown stat. The value is reapplied on Prepare, room reset
    // and star equip/unequip so native charge recovery and the tooltip stay on one source value.
    public void ApplyAwooCooldownOverride(bool active)
    {
        float cooldown = active ? 12.0f : 24.0f;
        try
        {
            if (configs != null)
            {
                for (int i = 0; i < configs.Length; i++)
                {
                    if (configs[i] == null) continue;
                    DariusTriggerConfigRuntimeEditor.SetCooldownTime(configs[i], cooldown);
                }
            }
            DariusLog.DebugInfo("STAR-AWOO", "Applied Guillotine recharge override active=" + active + " cooldown=" + cooldown.ToString("0.##"));
        }
        catch (Exception e)
        {
            DariusLog.Exception("STAR-AWOO", e, "Failed applying Guillotine cooldown override active=" + active);
        }
    }

    public static void ApplyAwooCooldownOverrideToHero(Hero hero, bool active)
    {
        if (hero == null) return;
        try
        {
            EntityAbility ability = hero.Ability;
            if (ability != null && ability.abilities != null)
            {
                foreach (var pair in ability.abilities)
                {
                    St_Darius_NoxianGuillotine trigger = pair.Value as St_Darius_NoxianGuillotine;
                    if (trigger == null) continue;
                    trigger.ApplyAwooCooldownOverride(active);
                }
            }
        }
        catch (Exception e)
        {
            DariusLog.Exception("STAR-AWOO", e, "Could not locate live Guillotine trigger for cooldown override");
        }
    }

    public static int HardResetAllLiveForNewRoom(string roomName)
    {
        if (!NetworkServer.active) return 0;
        int count = 0;
        try
        {
            St_Darius_NoxianGuillotine[] all = Resources.FindObjectsOfTypeAll<St_Darius_NoxianGuillotine>();
            for (int i = 0; i < all.Length; i++)
            {
                St_Darius_NoxianGuillotine r = all[i];
                if (r == null || !r.gameObject.scene.IsValid()) continue;
                Hero caster = r.owner as Hero;
                if (caster == null || caster.IsNullOrInactive()) continue;
                r.HardResetForNewRoom(roomName);
                count++;
            }
        }
        catch (Exception e)
        {
            DariusLog.Exception("R-ROOM-RESET", e, "Failed locating live Guillotine trigger(s) for room reset");
        }
        return count;
    }

    // Called at the ControlManager layer before the native cast helper runs. If one charge exists
    // but CanBeCast() is false, the trigger is in a stale state and can be repaired before the same
    // button press continues through the stock input path.
    public void NotifyControlCastAttempt(string source)
    {
        if (!NetworkServer.active) return;
        if (Time.frameCount == _lastControlAttemptFrame) return;
        _lastControlAttemptFrame = Time.frameCount;

        Hero caster = owner as Hero;
        if (caster == null || caster.IsNullOrInactive()) return;
        int token = ++_controlAttemptToken;
        float attemptAt = Time.unscaledTime;
        StartCoroutine(ControlAttemptWatchdog(token, attemptAt, source));
    }

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

    private Entity FindRecoveryTarget(Hero caster)
    {
        if (caster == null) return null;
        try
        {
            ControlManager cm = ManagerBase<ControlManager>.instance;
            Entity soft = cm != null ? cm.targetEnemy : null;
            if (IsValidCastTarget(caster, soft)) return soft;
        }
        catch { }

        Entity best = null;
        float bestDistSq = float.PositiveInfinity;
        try
        {
            Collider[] colliders = Physics.OverlapSphere(caster.transform.position, MaxResolvedCenterRange);
            for (int i = 0; i < colliders.Length; i++)
            {
                Entity candidate = colliders[i] != null ? colliders[i].GetComponentInParent<Entity>() : null;
                if (!IsValidCastTarget(caster, candidate)) continue;
                Vector3 delta = candidate.transform.position - caster.transform.position;
                delta.y = 0f;
                float distSq = delta.sqrMagnitude;
                if (distSq >= bestDistSq) continue;
                bestDistSq = distSq;
                best = candidate;
            }
        }
        catch { }
        return best;
    }

    private static Entity ResolveCastTarget(Hero caster, CastInfo info, bool logFailure)
    {
        if (caster == null) return null;

        if (IsValidCastTarget(caster, info.target))
        {
            DariusLog.DebugInfo("R-TARGET", "Using explicit input target=" + DariusLog.EntityLabel(info.target));
            return info.target;
        }

        // The game's current soft target is more stable than a point sample during rapid attack/R
        // transitions. Prefer it when legal, then fall back to spatial aim resolution.
        try
        {
            ControlManager cm = ManagerBase<ControlManager>.instance;
            Entity softTarget = cm != null ? cm.targetEnemy : null;
            if (IsValidCastTarget(caster, softTarget))
            {
                DariusLog.DebugInfo("R-TARGET", "Using ControlManager soft target=" + DariusLog.EntityLabel(softTarget));
                return softTarget;
            }
        }
        catch { }

        Vector3 casterPos = caster.transform.position;
        Vector3 aimPoint = info.point;
        Vector3 toPoint = aimPoint - casterPos;
        toPoint.y = 0f;
        bool hasAimPoint = toPoint.sqrMagnitude > 0.04f;

        Vector3 aimDir = hasAimPoint ? toPoint.normalized : Vector3.zero;
        if (aimDir.sqrMagnitude <= 0.001f)
        {
            try { aimDir = info.forward; aimDir.y = 0f; } catch { aimDir = Vector3.zero; }
        }
        if (aimDir.sqrMagnitude <= 0.001f) { aimDir = caster.transform.forward; aimDir.y = 0f; }
        if (aimDir.sqrMagnitude <= 0.001f) aimDir = Vector3.forward;
        aimDir.Normalize();

        Collider[] colliders;
        try { colliders = Physics.OverlapSphere(casterPos, MaxResolvedCenterRange); }
        catch (Exception e)
        {
            DariusLog.Exception("R-TARGET", e, "Physics.OverlapSphere failed during target resolution");
            return null;
        }

        HashSet<Entity> seen = new HashSet<Entity>();
        Entity best = null;
        Entity nearestFront = null;
        Entity nearestAny = null;
        float bestScore = float.PositiveInfinity;
        float nearestFrontDist = float.PositiveInfinity;
        float nearestAnyDist = float.PositiveInfinity;
        string bestReason = null;
        float bestDist = 0f, bestPointDist = 0f, bestAngle = 0f, bestLateral = 0f;

        for (int i = 0; i < colliders.Length; i++)
        {
            Entity candidate = colliders[i] != null ? colliders[i].GetComponentInParent<Entity>() : null;
            if (candidate == null || !seen.Add(candidate) || !IsValidCastTarget(caster, candidate)) continue;

            Vector3 toTarget = candidate.transform.position - casterPos;
            toTarget.y = 0f;
            float dist = toTarget.magnitude;
            if (dist <= 0.001f) continue;
            Vector3 targetDir = toTarget / dist;
            float angle = Vector3.Angle(aimDir, targetDir);
            float forwardDistance = Vector3.Dot(toTarget, aimDir);
            Vector3 closestOnRay = casterPos + aimDir * Mathf.Clamp(forwardDistance, 0f, CastRange);
            Vector3 lateralDelta = candidate.transform.position - closestOnRay;
            lateralDelta.y = 0f;
            float lateral = lateralDelta.magnitude;
            Vector3 pointDelta = candidate.transform.position - aimPoint;
            pointDelta.y = 0f;
            float pointDist = hasAimPoint ? pointDelta.magnitude : float.PositiveInfinity;

            if (dist <= CastRange && dist < nearestAnyDist)
            {
                nearestAny = candidate;
                nearestAnyDist = dist;
            }
            if (angle <= 90f && dist < nearestFrontDist)
            {
                nearestFront = candidate;
                nearestFrontDist = dist;
            }

            bool nearPoint = hasAimPoint && pointDist <= AimPointAssistRadius;
            bool nearAimRay = forwardDistance >= -0.15f && angle <= AimDirectionAssistAngle && lateral <= AimPointAssistRadius;
            if (!nearPoint && !nearAimRay) continue;

            // Pointer proximity wins first. Otherwise score distance from the full aim ray, not only
            // distance from the clamped endpoint; this makes R reliable when the cursor sits beyond
            // the target or at maximum range while still refusing enemies behind Darius.
            float score = nearPoint
                ? pointDist * 0.55f + angle * 0.012f + dist * 0.01f
                : 2.0f + lateral * 0.60f + angle * 0.018f + dist * 0.012f;
            if (score >= bestScore) continue;
            best = candidate;
            bestScore = score;
            bestReason = nearPoint ? "pointer" : "aim-ray";
            bestDist = dist;
            bestPointDist = pointDist;
            bestAngle = angle;
            bestLateral = lateral;
        }

        if (best == null && nearestFront != null && nearestFrontDist <= CastRange)
        {
            best = nearestFront;
            bestReason = "front-nearest-fallback";
            bestDist = nearestFrontDist;
        }
        // R is fundamentally a single-target ability. Once the player has committed the cast,
        // do not discard a legal in-range enemy merely because the point-cast cursor landed in
        // empty ground. Pointer/ray/front candidates still take priority; this final fallback
        // guarantees that a nearby legal target produces gameplay instead of another dead input.
        if (best == null && nearestAny != null && nearestAnyDist <= CastRange)
        {
            best = nearestAny;
            bestReason = "nearest-in-range-fallback";
            bestDist = nearestAnyDist;
        }

        if (best != null)
        {
            DariusLog.Info("R-TARGET", "Resolved target=" + DariusLog.EntityLabel(best) +
                " reason=" + bestReason + " centerDist=" + bestDist.ToString("0.###") +
                " pointDist=" + (hasAimPoint ? bestPointDist.ToString("0.###") : "<none>") +
                " lateral=" + bestLateral.ToString("0.###") + " angle=" + bestAngle.ToString("0.##") +
                " colliders=" + colliders.Length + " unique=" + seen.Count);
            return best;
        }

        if (logFailure)
        {
            DariusLog.Warn("R-TARGET", "No legal target within R range after point/ray/fallback resolution. point=" +
                DariusLog.Vec(aimPoint) + " aimDir=" + DariusLog.Vec(aimDir) + " colliders=" + colliders.Length + " unique=" + seen.Count);
            DariusLog.Warn("R-TARGET", "No legal living enemy on the input frame; entering short input buffer. caster=" +
                DariusLog.EntityLabel(caster) + " point=" + DariusLog.Vec(info.point) + " forward=" + DariusLog.Vec(info.forward));
        }
        return null;
    }

    private static bool IsValidCastTarget(Hero caster, Entity target)
    {
        if (caster == null || target == null || target == caster) return false;
        if (target.IsNullInactiveDeadOrKnockedOut()) return false;
        if (!caster.GetRelation(target).HasFlag(EntityRelation.Enemy)) return false;
        Vector3 delta = target.transform.position - caster.transform.position;
        delta.y = 0f;
        return delta.sqrMagnitude <= MaxResolvedCenterRange * MaxResolvedCenterRange + 0.0001f;
    }
}
