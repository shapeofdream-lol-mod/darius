using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Mirror;
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