using System;
using System.Collections;
using UnityEngine;
using Mirror;

public sealed class Ai_Darius_CripplingStrike : AbilityInstance
{
    protected override void OnCreate()
    {
        base.OnCreate();
        StartCoroutine(RunExecute());
    }

    private IEnumerator RunExecute()
    {
        yield return Execute(info, firstTrigger, this);
        if (NetworkServer.active) Destroy();
    }

    // The exact gameplay/VFX routine is also exposed for the SkillTrigger emergency fallback.
    // Normally this runs through a real AbilityInstance. If the runtime prefab cannot be parented
    // by the stock Actor factory, the trigger can still execute the same CastInfo after vanilla
    // cooldown/charge handling has already completed.
    public static IEnumerator Execute(CastInfo castInfo, AbilityTrigger sourceTrigger = null, Actor sourceActor = null)
    {
        int castId = DariusLog.NextId();
        Hero owner = castInfo.caster as Hero;
        if (owner == null)
        {
            DariusLog.Error("W", "cast#" + castId + " aborted: caster is not a Hero.");
            yield break;
        }

        DariusEquipmentRuntime.NotifySkillCast(owner, "W cast#" + castId);

        int memoryLevel = DariusMemoryScaling.GetLevel(sourceTrigger);
        float memoryScale = DariusMemoryScaling.Multiplier(memoryLevel);
        DariusLog.Info("W", "cast#" + castId + " cast start owner=" + DariusLog.EntityLabel(owner) +
            " memoryLevel=" + memoryLevel + " memoryScale=" + memoryScale.ToString("0.###"));
        // W is an attack modifier. Do not play the attack swing on button press;
        // At_DariusAxe.OnCastStart now owns the empowered swing timing.
        if (!NetworkServer.active)
        {
            DariusLog.DebugInfo("W", "cast#" + castId + " client visual-only branch.");
            yield break;
        }

        try
        {
            DariusCripplingStrikeRuntime runtime = owner.GetComponent<DariusCripplingStrikeRuntime>();
            if (runtime == null)
            {
                runtime = owner.gameObject.AddComponent<DariusCripplingStrikeRuntime>();
                DariusLog.DebugInfo("W", "cast#" + castId + " created DariusCripplingStrikeRuntime.");
            }
            // W only arms the next native basic attack. It deliberately does NOT reset the
            // attack timer: the current design/tooltip does not promise an auto-attack reset.
            runtime.Arm(owner, castId, memoryLevel, sourceTrigger);
            DariusLog.Info("W", "cast#" + castId + " armed next basic attack without modifying attack cooldown.");
        }
        catch (Exception e)
        {
            DariusLog.Exception("W", e, "cast#" + castId + " arm/reset failed");
        }

        yield break;
    }
}

public sealed class DariusCripplingStrikeRuntime : MonoBehaviour
{
    public const float ArmDuration = 4.0f;
    public const float BonusAdRatio = 1.00f;
    public const float SlowStrength = 0.90f;
    public const float SlowDuration = 1.25f;
    public const float DamageGrowthPerAdditionalLevel = 0.20f;
    public const int SlowGrowthMilestoneEveryLevels = 5;
    public const float SlowDurationPerStep = 0.08f;

    public static float BonusAdRatioAtLevel(int level)
    {
        return DariusMemoryScaling.Linear(BonusAdRatio, level, DamageGrowthPerAdditionalLevel);
    }

    public static float SlowDurationAtLevel(int level)
    {
        return DariusMemoryScaling.MilestoneStepped(SlowDuration, SlowDurationPerStep, level, SlowGrowthMilestoneEveryLevels);
    }

    private Hero _owner;
    private bool _armed;
    private float _expiresAt;
    private int _castId;
    private int _memoryLevel = 1;
    private AbilityTrigger _sourceTrigger;
    private GameObject _armVfx;

    public bool IsArmed { get { return _armed; } }

    public void Arm(Hero owner, int castId, int memoryLevel = 1, AbilityTrigger sourceTrigger = null)
    {
        if (_owner != owner)
        {
            Unsubscribe();
            _owner = owner;
            _owner.ActorEvent_OnAttackHit += OnAttackHit;
            DariusLog.Info("W", "cast#" + castId + " subscribed native attack-hit consumption on " + DariusLog.EntityLabel(owner));
        }
        _castId = castId;
        _memoryLevel = DariusMemoryScaling.NormalizeLevel(memoryLevel);
        _sourceTrigger = sourceTrigger;
        _armed = true;
        DariusSkinAnimationHooks.SetWArmed(owner, true);
        _expiresAt = Time.time + ArmDuration;
        ClearArmVfx();
        try { _armVfx = DariusPrototypeVfx.CreateWArm(owner); }
        catch (Exception e) { DariusLog.Exception("W-VFX", e, "cast#" + castId + " persistent arm VFX failed"); }
        DariusLog.Info("W", "cast#" + castId + " armed for " + ArmDuration + "s; memoryLevel=" + _memoryLevel +
            " coreScale=" + DariusMemoryScaling.Multiplier(_memoryLevel).ToString("0.###") + " expiresAt=" + _expiresAt.ToString("0.000"));
    }

    private void Update()
    {
        if (_armed && Time.time >= _expiresAt)
        {
            _armed = false;
            _sourceTrigger = null;
            DariusSkinAnimationHooks.SetWArmed(_owner, false);
            ClearArmVfx();
            DariusLog.Info("W", "cast#" + _castId + " expired without consuming an attack.");
        }
    }

    private void OnDestroy()
    {
        DariusLog.DebugInfo("W", "runtime destroyed.");
        DariusSkinAnimationHooks.SetWArmed(_owner, false);
        _sourceTrigger = null;
        ClearArmVfx();
        Unsubscribe();
    }

    private void Unsubscribe()
    {
        if (_owner != null)
        {
            try
            {
                _owner.ActorEvent_OnAttackHit -= OnAttackHit;
                DariusLog.DebugInfo("W", "unsubscribed native attack-hit consumption from " + _owner.name);
            }
            catch (Exception e) { DariusLog.Exception("W", e, "unsubscribe attack event failed"); }
        }
        _owner = null;
    }


    private void ClearArmVfx()
    {
        if (_armVfx != null)
        {
            try { UnityEngine.Object.Destroy(_armVfx); } catch { }
            _armVfx = null;
        }
    }


    public void NotifyNativeAttackStarted(Vector3 direction)
    {
        if (!_armed || _owner == null) return;
        try
        {
            DariusSkinAnimationHooks.PlayW(_owner, direction);
            DariusPrototypeVfx.CreateWAttackSwing(_owner);
            try { DariusMedia.PlayForSkin("w_swing", _owner, _owner.transform.position, 0.62f); } catch { }
            DariusLog.DebugInfo("W-ANIM", "cast#" + _castId + " At_DariusAxe cast start -> Spell2 + weapon trail.");
        }
        catch (Exception e)
        {
            DariusLog.Exception("W-ANIM", e, "cast#" + _castId + " native empowered attack animation failed");
        }
    }

    private void OnAttackHit(EventInfoAttackHit info)
    {
        if (!_armed || _owner == null) return;
        if (info.attacker != _owner)
        {
            DariusLog.DebugInfo("W", "cast#" + _castId + " ignored attack event: attacker mismatch.");
            return;
        }
        if (info.victim == null)
        {
            DariusLog.Warn("W", "cast#" + _castId + " attack event has null victim.");
            return;
        }
        if (!_owner.GetRelation(info.victim).HasFlag(EntityRelation.Enemy))
        {
            DariusLog.DebugInfo("W", "cast#" + _castId + " ignored non-enemy victim=" + DariusLog.EntityLabel(info.victim));
            return;
        }

        try
        {
            AbilityTrigger sourceTrigger = _sourceTrigger;
            _armed = false;
            _sourceTrigger = null;
            DariusSkinAnimationHooks.SetWArmed(_owner, false);
            ClearArmVfx();
            float ad = _owner.Status.finalStats.attackDamage;
            float scaledBonusAdRatio = BonusAdRatioAtLevel(_memoryLevel);
            float extra = ad * scaledBonusAdRatio;
            extra = DariusEquipmentRuntime.ModifyBasicSkillDamage(_owner, extra);
            DariusLog.Info("W-HIT", "cast#" + _castId + " consumed on target=" + DariusLog.EntityLabel(info.victim) +
                " AD=" + ad.ToString("0.##") + " memoryLevel=" + _memoryLevel +
                " extraRatio=" + scaledBonusAdRatio.ToString("0.###") + " extraDamage=" + extra.ToString("0.##"));
            DariusMemoryEffectBridge.DealPhysical(_owner, sourceTrigger, extra, 1f, info.victim, "W cast#" + _castId);
            DariusEquipmentRuntime.NotifyShojinSkillHit(_owner, "W cast#" + _castId);
            DariusEquipmentRuntime.ApplyStridebreakerW(_owner, info.victim, sourceTrigger, sourceTrigger, "W cast#" + _castId);
            bool killed = info.victim == null || info.victim.IsNullInactiveDeadOrKnockedOut() || info.victim.currentHealth <= 0f;
            if (killed) DariusConstellationRuntime.NotifyKill(_owner, info.victim, "W cast#" + _castId);
            if (!killed)
            {
                float slowDuration = SlowDurationAtLevel(_memoryLevel) + DariusConstellationRuntime.GetLegacyWSlowDurationBonus(_owner);
                DariusSlowHelper.Apply(info.victim, SlowStrength, slowDuration, "W cast#" + _castId);
            }
            float refundPerStack = DariusConstellationRuntime.GetLegacyWRefundPerHemorrhageStack(_owner);
            if (refundPerStack > 0f && sourceTrigger != null)
            {
                DariusHemorrhageRuntime hem = _owner.GetComponent<DariusHemorrhageRuntime>();
                int stacks = hem != null ? hem.GetStacks(info.victim) : 0;
                if (stacks > 0)
                {
                    St_Darius_CripplingStrike wTrigger = sourceTrigger as St_Darius_CripplingStrike;
                    if (wTrigger != null) wTrigger.RefundLegacyCooldown(refundPerStack * stacks, "Hemorrhage stacks=" + stacks);
                }
            }
            try { DariusPrototypeVfx.CreateWImpact(_owner, info.victim.transform.position); }
            catch (Exception e) { DariusLog.Exception("W-VFX", e, "cast#" + _castId + " impact VFX failed"); }
        }
        catch (Exception e)
        {
            DariusLog.Exception("W", e, "cast#" + _castId + " empowered hit failed");
        }
    }
}
