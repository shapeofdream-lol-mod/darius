using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Mirror;
using UnityEngine;

public sealed partial class St_Darius_NoxianGuillotine : SkillTrigger
{
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
}