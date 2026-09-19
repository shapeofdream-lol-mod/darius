using System;
using System.Collections;
using UnityEngine;
using Mirror;

public sealed partial class Ai_Darius_NoxianGuillotine : AbilityInstance
{
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

            if (killed) ResolveExecuteReset(owner, sourceTrigger, castId);
        }
        catch (Exception e)
        {
            if (awooCastActive && !awooResultResolved) { DariusEquipmentRuntime.NotifyAwooRResult(owner, false, "R exception cast#" + castId); awooResultResolved = true; }
            DariusLog.Exception("R", e, "cast#" + castId + " damage/reset failed");
        }
    }
}