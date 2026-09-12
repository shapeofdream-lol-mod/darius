using System;
using System.Collections;
using UnityEngine;
using Mirror;

public sealed class Ai_Darius_NoxianGuillotine : AbilityInstance
{
    // Pass1 hotfix2 balance: R was substantially overtuned in Shape of Dreams. Reduce the
    // entire level-scaled baseline by 25% while preserving Hemorrhage stack multipliers and the
    // native Memory per-level growth curve. 2.60 -> 1.95, 1.80 -> 1.35.
    public const float NoEssenceAdRatio = 1.95f;
    public const float EssenceBaseAdRatio = 1.35f;
    public const float DamagePerHemorrhageStack = 0.25f;
    public const float DamageGrowthPerAdditionalLevel = 0.20f;
    // Spell4 reaches the airborne apex around 0.28 s and the axe-down impact around 0.42 s.
    public const float LeapMoveDelay = 0.28f;
    public const float ImpactDelay = 0.42f;

    private St_Darius_NoxianGuillotine _sourceDariusTrigger;
    private bool _normalTerminalReached;

    public static float DamageRatioAtLevel(bool hemorrhageActive, int stacks, int level)
    {
        float baseRatio = hemorrhageActive
            ? EssenceBaseAdRatio * (1f + DamagePerHemorrhageStack * Mathf.Max(0, stacks))
            : NoEssenceAdRatio;
        return DariusMemoryScaling.Linear(baseRatio, level, DamageGrowthPerAdditionalLevel);
    }

    protected override void OnCreate()
    {
        base.OnCreate();
        _sourceDariusTrigger = firstTrigger as St_Darius_NoxianGuillotine;
        _normalTerminalReached = false;
        StartCoroutine(RunExecute());
    }

    private IEnumerator RunExecute()
    {
        yield return Execute(info, firstTrigger, this);
        // Mark terminal before Destroy(), otherwise Unity OnDestroy would misclassify our own normal
        // cleanup as an interruption and incorrectly refund a legitimately spent non-kill R.
        _normalTerminalReached = true;
        if (_sourceDariusTrigger != null)
            _sourceDariusTrigger.NotifyNativeExecutionFinished("AbilityInstance coroutine completed");
        if (NetworkServer.active) Destroy();
    }

    private void OnDestroy()
    {
        if (!NetworkServer.active || _normalTerminalReached || _sourceDariusTrigger == null) return;
        // A native Action/attack cancellation can destroy this actor without allowing RunExecute to
        // reach the line above. Recover the whole R state instead of leaving a hidden cast lock.
        _sourceDariusTrigger.NotifyNativeExecutionInterrupted("AbilityInstance destroyed before normal terminal callback");
        _sourceDariusTrigger = null;
    }

    public static IEnumerator Execute(CastInfo castInfo, AbilityTrigger sourceTrigger, Actor sourceActor = null)
    {
        int castId = DariusLog.NextId();
        Hero owner = castInfo.caster as Hero;
        Entity target = castInfo.target;
        if (owner == null)
        {
            DariusLog.Error("R", "cast#" + castId + " aborted: owner null.");
            yield break;
        }

        Vector3 preferredPoint = target != null ? target.transform.position : owner.transform.position + owner.transform.forward * 2f;
        if (!IsLegalTarget(owner, target))
        {
            target = FindRetarget(owner, target, preferredPoint, castId, "cast-start");
            if (target == null)
            {
                RefundCooldown(owner, sourceTrigger, castId, "target invalid before animation");
                yield break;
            }
            preferredPoint = target.transform.position;
        }

        DariusEquipmentRuntime.NotifySkillCast(owner, "R cast#" + castId);
        bool awooCastActive = DariusEquipmentRuntime.NotifyAwooRCast(owner);

        try { DariusSkinAnimationHooks.PlayR(owner); }
        catch (Exception e) { DariusLog.Exception("R-ANIM", e, "cast#" + castId + " Darius skin R animation failed"); }
        try { DariusPrototypeVfx.CreateRWindup(owner); }
        catch (Exception e) { DariusLog.Exception("R-VFX", e, "cast#" + castId + " weapon-follow execute trail failed"); }

        int memoryLevel = DariusMemoryScaling.GetLevel(sourceTrigger);
        float memoryScale = DariusMemoryScaling.Multiplier(memoryLevel);
        DariusHemorrhageRuntime hemorrhage = owner.GetComponent<DariusHemorrhageRuntime>();
        bool essenceActive = hemorrhage != null && hemorrhage.isEnabled;
        int stacks = essenceActive ? hemorrhage.GetStacks(target) : 0;
        float adSnapshot = owner.Status.finalStats.attackDamage;
        float ratio;
        float awooRatio;
        bool awooRatioActive = DariusEquipmentRuntime.TryGetAwooRRatio(owner, out awooRatio);
        ratio = awooRatioActive ? awooRatio : DamageRatioAtLevel(essenceActive, stacks, memoryLevel);
        bool awooResultResolved = false;
        float rStarMultiplier = DariusConstellationRuntime.GetRDamageMultiplier(owner);
        float plannedDamage = adSnapshot * ratio * rStarMultiplier;

        DariusLog.Info("R", "cast#" + castId + " start owner=" + DariusLog.EntityLabel(owner) +
            " target=" + DariusLog.EntityLabel(target) + " hemorrhage=" + essenceActive +
            " stacks=" + stacks + " AD=" + adSnapshot.ToString("0.##") +
            " memoryLevel=" + memoryLevel + " memoryScale=" + memoryScale.ToString("0.###") +
            " ratio=" + ratio.ToString("0.###") + " runeMultiplier=" + rStarMultiplier.ToString("0.###") +
            " plannedPureDamage=" + plannedDamage.ToString("0.##"));

        yield return new WaitForSeconds(LeapMoveDelay);

        if (owner == null || owner.IsNullOrInactive())
        {
            DariusLog.Warn("R", "cast#" + castId + " caster became inactive before leap movement frame.");
            if (awooCastActive && !awooResultResolved) { DariusEquipmentRuntime.NotifyAwooRResult(owner, false, "caster inactive cast#" + castId); awooResultResolved = true; }
            yield break;
        }

        if (!IsLegalTarget(owner, target))
        {
            Entity old = target;
            target = FindRetarget(owner, old, preferredPoint, castId, "before-leap");
            if (target == null)
            {
                DariusLog.Warn("R", "cast#" + castId + " original target died before leap and no replacement exists; refunding cooldown.");
                if (awooCastActive && !awooResultResolved) { DariusEquipmentRuntime.NotifyAwooRResult(owner, false, "target died before leap cast#" + castId); awooResultResolved = true; }
                RefundCooldown(owner, sourceTrigger, castId, "target died before leap");
                yield break;
            }
            preferredPoint = target.transform.position;
        }

        Vector3 leapFrom = owner.transform.position;
        Vector3 leapTo = leapFrom;
        if (NetworkServer.active)
        {
            try
            {
                leapTo = TeleportNearTarget(owner, target);
                DariusLog.Info("R-MOVE", "cast#" + castId + " animation leap frame=" + LeapMoveDelay.ToString("0.00") +
                    " from=" + DariusLog.Vec(leapFrom) + " target=" + DariusLog.EntityLabel(target) +
                    " actual=" + DariusLog.Vec(leapTo));
            }
            catch (Exception e)
            {
                DariusLog.Exception("R-MOVE", e, "cast#" + castId + " teleport failed");
            }
        }
        try { DariusPrototypeVfx.CreateRLeap(owner, leapFrom, leapTo); }
        catch (Exception e) { DariusLog.Exception("R-VFX", e, "cast#" + castId + " leap-beat VFX failed"); }

        float remainingImpactDelay = Mathf.Max(0f, ImpactDelay - LeapMoveDelay);
        if (remainingImpactDelay > 0f)
            yield return new WaitForSeconds(remainingImpactDelay);

        if (!NetworkServer.active)
        {
            DariusLog.DebugInfo("R", "cast#" + castId + " client visual-only branch after impact frame=" + ImpactDelay);
            yield break;
        }
        if (owner == null || owner.IsNullOrInactive()) yield break;

        if (!IsLegalTarget(owner, target))
        {
            Entity old = target;
            target = FindRetarget(owner, old, preferredPoint, castId, "before-impact");
            if (target == null)
            {
                DariusLog.Warn("R", "cast#" + castId + " target died before impact and no replacement exists; refunding cooldown.");
                if (awooCastActive && !awooResultResolved) { DariusEquipmentRuntime.NotifyAwooRResult(owner, false, "target died before impact cast#" + castId); awooResultResolved = true; }
                RefundCooldown(owner, sourceTrigger, castId, "target died before impact");
                yield break;
            }

            // The axe-down frame should visually/gameplay land on the replacement, not empty space.
            try
            {
                Vector3 corrected = TeleportNearTarget(owner, target);
                DariusLog.Info("R-RETARGET-MOVE", "cast#" + castId + " corrected landing to replacement target actual=" + DariusLog.Vec(corrected));
            }
            catch (Exception e) { DariusLog.Exception("R-RETARGET-MOVE", e, "cast#" + castId + " landing correction failed"); }
        }

        try
        {
            // Use the replacement target's current stacks at the impact frame. This also prevents a
            // stale full-stack snapshot from being transferred to a newly selected enemy.
            stacks = essenceActive && hemorrhage != null ? hemorrhage.GetStacks(target) : 0;
            float ad = owner.Status.finalStats.attackDamage;
            // Awoo rewrites the R ratio itself. Re-read it at the impact frame so a room-local
            // execute streak is authoritative and cannot be replaced by the normal Hemorrhage ratio.
            ratio = DariusEquipmentRuntime.TryGetAwooRRatio(owner, out awooRatio)
                ? awooRatio
                : DamageRatioAtLevel(essenceActive, stacks, memoryLevel);
            rStarMultiplier = DariusConstellationRuntime.GetRDamageMultiplier(owner);
            float damage = ad * ratio * rStarMultiplier;
            float hpBefore = target.currentHealth;
            DariusMemoryEffectBridge.DealPure(owner, sourceActor, sourceTrigger, damage, 1f, target, "R cast#" + castId);
            float hpAfter = target != null ? target.currentHealth : 0f;

            try { DariusPrototypeVfx.CreateRImpact(owner, target, target != null ? target.transform.position : owner.transform.position); }
            catch (Exception e) { DariusLog.Exception("R-VFX", e, "cast#" + castId + " impact VFX failed"); }

            bool killed = target == null || target.IsNullInactiveDeadOrKnockedOut() || hpAfter <= 0f;
            if (awooCastActive && !awooResultResolved)
            {
                DariusEquipmentRuntime.NotifyAwooRResult(owner, killed, "R cast#" + castId);
                awooResultResolved = true;
            }
            DariusConstellationRuntime.NotifyDamageHit(owner, target, "R cast#" + castId);
            if (killed) DariusConstellationRuntime.NotifyKill(owner, target, "R execute cast#" + castId);
            DariusLog.Info("R-HIT", "cast#" + castId + " target=" + DariusLog.EntityLabel(target) +
                " stacks=" + stacks + " memoryLevel=" + memoryLevel + " ratio=" + ratio.ToString("0.###") +
                " damage=" + damage.ToString("0.##") + " hpBefore=" + hpBefore.ToString("0.##") +
                " hpAfter=" + hpAfter.ToString("0.##") + " killed=" + killed);

            if (killed)
            {
                try
                {
                    DariusHemorrhageRuntime passive = owner.GetComponent<DariusHemorrhageRuntime>();
                    bool granted = passive != null && passive.GrantNoxianMightFromExecute("R execute cast#" + castId);
                    DariusLog.Info("R-EXECUTE-MIGHT", "cast#" + castId + " kill -> immediate Noxian Might granted=" + granted +
                        " passive=" + (passive != null));
                }
                catch (Exception e)
                {
                    DariusLog.Exception("R-EXECUTE-MIGHT", e, "cast#" + castId + " execute grant failed");
                }

                if (sourceTrigger != null)
                {
                    St_Darius_NoxianGuillotine dariusR = sourceTrigger as St_Darius_NoxianGuillotine;
                    if (dariusR != null)
                    {
                        dariusR.RestoreUltimateCharge("execute reset cast#" + castId);
                        DariusLog.Info("R-RESET", "cast#" + castId + " ultimate charge restored trigger=" + sourceTrigger.name);
                    }
                    else
                    {
                        // Compatibility fallback for an unexpected non-Darius trigger.
                        sourceTrigger.SetChargeAll(1);
                        sourceTrigger.SetCooldownTimeAll(0f, false);
                        DariusLog.Warn("R-RESET", "cast#" + castId + " used generic charge restore because source trigger type was " + sourceTrigger.GetType().Name);
                    }
                }
                else
                {
                    DariusLog.Error("R-RESET", "cast#" + castId + " kill confirmed but firstTrigger is null; ultimate charge was NOT restored.");
                }
                try { DariusPrototypeVfx.CreateRReset(owner, owner.transform.position); }
                catch (Exception e) { DariusLog.Exception("R-VFX", e, "cast#" + castId + " reset VFX failed"); }
            }
        }
        catch (Exception e)
        {
            if (awooCastActive && !awooResultResolved) { DariusEquipmentRuntime.NotifyAwooRResult(owner, false, "R exception cast#" + castId); awooResultResolved = true; }
            DariusLog.Exception("R", e, "cast#" + castId + " damage/reset failed");
        }
    }

    private static bool IsLegalTarget(Hero owner, Entity target)
    {
        if (owner == null || target == null || target == owner) return false;
        if (target.IsNullInactiveDeadOrKnockedOut()) return false;
        try { return owner.GetRelation(target).HasFlag(EntityRelation.Enemy); }
        catch { return false; }
    }

    private static Entity FindRetarget(Hero owner, Entity previous, Vector3 preferredPoint, int castId, string phase)
    {
        if (owner == null) return null;
        Entity best = null;
        float bestScore = float.PositiveInfinity;
        int candidates = 0;
        try
        {
            Collider[] colliders = Physics.OverlapSphere(owner.transform.position, St_Darius_NoxianGuillotine.MaxResolvedCenterRange);
            for (int i = 0; i < colliders.Length; i++)
            {
                Entity candidate = colliders[i] != null ? colliders[i].GetComponentInParent<Entity>() : null;
                if (!IsLegalTarget(owner, candidate) || candidate == previous) continue;

                Vector3 fromOwner = candidate.transform.position - owner.transform.position;
                fromOwner.y = 0f;
                if (fromOwner.sqrMagnitude > St_Darius_NoxianGuillotine.MaxResolvedCenterRange * St_Darius_NoxianGuillotine.MaxResolvedCenterRange)
                    continue;
                candidates++;

                Vector3 fromPreferred = candidate.transform.position - preferredPoint;
                fromPreferred.y = 0f;
                float score = fromPreferred.magnitude * 0.70f + fromOwner.magnitude * 0.30f;
                if (score >= bestScore) continue;
                bestScore = score;
                best = candidate;
            }
        }
        catch (Exception e)
        {
            DariusLog.Exception("R-RETARGET", e, "cast#" + castId + " retarget scan failed phase=" + phase);
            return null;
        }

        if (best != null)
        {
            DariusLog.Info("R-RETARGET", "cast#" + castId + " phase=" + phase + " old=" + DariusLog.EntityLabel(previous) +
                " -> new=" + DariusLog.EntityLabel(best) + " candidates=" + candidates + " score=" + bestScore.ToString("0.###"));
        }
        return best;
    }

    private static Vector3 TeleportNearTarget(Hero owner, Entity target)
    {
        Vector3 delta = target.transform.position - owner.transform.position;
        delta.y = 0f;
        Vector3 direction = delta.sqrMagnitude > 0.001f ? delta.normalized : owner.transform.forward;
        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.001f) direction = Vector3.forward;
        direction.Normalize();
        Vector3 landing = target.transform.position - direction * 1.0f;
        owner.Teleport(owner, landing);
        return owner.transform.position;
    }

    private static void RefundCooldown(Hero owner, AbilityTrigger sourceTrigger, int castId, string reason)
    {
        if (!NetworkServer.active || owner == null || sourceTrigger == null) return;
        try
        {
            St_Darius_NoxianGuillotine dariusR = sourceTrigger as St_Darius_NoxianGuillotine;
            if (dariusR != null)
                dariusR.RestoreUltimateCharge("refund cast#" + castId + ": " + reason);
            else
            {
                sourceTrigger.SetChargeAll(1);
                sourceTrigger.SetCooldownTimeAll(0f, false);
            }
            DariusLog.Info("R-REFUND", "cast#" + castId + " refunded ultimate charge because R dealt no damage. reason=" + reason);
        }
        catch (Exception e)
        {
            DariusLog.Exception("R-REFUND", e, "cast#" + castId + " failed to refund ultimate charge reason=" + reason);
        }
    }
}
