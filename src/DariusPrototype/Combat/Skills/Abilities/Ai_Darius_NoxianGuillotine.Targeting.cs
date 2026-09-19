using System;
using System.Collections;
using UnityEngine;
using Mirror;

public sealed partial class Ai_Darius_NoxianGuillotine : AbilityInstance
{
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