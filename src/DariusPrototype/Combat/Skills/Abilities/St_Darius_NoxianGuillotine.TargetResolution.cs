using System;
using System.Collections.Generic;
using UnityEngine;

public sealed partial class St_Darius_NoxianGuillotine : SkillTrigger
{
    private static Entity ResolveCastTarget(Hero caster, CastInfo info, bool logFailure)
    {
        if (caster == null) return null;

        if (IsValidCastTarget(caster, info.target))
        {
            DariusLog.DebugInfo("R-TARGET", "Using explicit input target=" + DariusLog.EntityLabel(info.target));
            return info.target;
        }

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

        ListReturnHandle<Entity> handle = default;
        List<Entity> candidates;
        try
        {
            candidates = DewPhysics.OverlapCircleAllEntities(out handle, casterPos, MaxResolvedCenterRange);
        }
        catch (Exception e)
        {
            DariusLog.Exception("R-TARGET", e, "DewPhysics.OverlapCircleAllEntities failed during target resolution");
            return null;
        }

        Entity best = null;
        Entity nearestFront = null;
        Entity nearestAny = null;
        float bestScore = float.PositiveInfinity;
        float nearestFrontDist = float.PositiveInfinity;
        float nearestAnyDist = float.PositiveInfinity;
        string bestReason = null;
        float bestDist = 0f, bestPointDist = 0f, bestAngle = 0f, bestLateral = 0f;
        int candidateCount = candidates != null ? candidates.Count : 0;
        int legalCount = 0;

        try
        {
            for (int i = 0; i < candidateCount; i++)
            {
                Entity candidate = candidates[i];
                if (!IsValidCastTarget(caster, candidate)) continue;
                legalCount++;

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
        }
        finally
        {
            if (handle.needToReturn) handle.Return();
        }

        if (best == null && nearestFront != null && nearestFrontDist <= CastRange)
        {
            best = nearestFront;
            bestReason = "front-nearest-fallback";
            bestDist = nearestFrontDist;
        }
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
                " candidates=" + candidateCount + " legal=" + legalCount);
            return best;
        }

        if (logFailure)
        {
            DariusLog.Warn("R-TARGET", "No legal target within R range after DewPhysics point/ray/fallback resolution. point=" +
                DariusLog.Vec(aimPoint) + " aimDir=" + DariusLog.Vec(aimDir) +
                " candidates=" + candidateCount + " legal=" + legalCount);
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
