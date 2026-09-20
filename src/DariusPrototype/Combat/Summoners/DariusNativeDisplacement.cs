using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Thin Apprehend forced-movement helper.
// Shape of Dreams' Knockback is the official non-friendly displacement helper; E already computes
// the exact destination, so distance + target-to-destination direction fully describe the pull.
public static class DariusNativeDisplacement
{
    public static bool TryPull(Entity target, Vector3 destination, float duration, string reason)
    {
        if (target == null || target.IsNullInactiveDeadOrKnockedOut()) return false;

        Vector3 delta = destination - target.transform.position;
        delta.y = 0f;
        float distance = delta.magnitude;
        if (distance <= 0.001f) return true;

        try
        {
            Knockback pull = new Knockback
            {
                distance = distance,
                duration = Mathf.Max(Displacement.MinimumDisplacementDuration, duration),
                // Apprehend's pull timing is authored gameplay timing, not a CC-duration scalar.
                ignoreTenacity = true
            };
            pull.ApplyWithDirection(delta / distance, target);
            DariusLog.Info("E-DISPLACE",
                "Official Knockback pull target=" + DariusLog.EntityLabel(target) +
                " dest=" + DariusLog.Vec(destination) +
                " distance=" + distance.ToString("0.###") +
                " duration=" + duration.ToString("0.###") +
                " reason=" + reason);
            return true;
        }
        catch (Exception e)
        {
            DariusLog.Exception("E-DISPLACE", e,
                "Official Knockback pull failed; using smooth server fallback");
            return false;
        }
    }

    // Safety fallback only if the official displacement helper throws in the target build.
    // Keep it time-based so failure never degrades into a one-frame teleport.
    public static IEnumerator SmoothFallback(List<PullEntry> entries, float duration)
    {
        if (entries == null || entries.Count == 0) yield break;

        float startTime = Time.time;
        float safeDuration = Mathf.Max(0.05f, duration);
        while (Time.time - startTime < safeDuration)
        {
            float t = Mathf.Clamp01((Time.time - startTime) / safeDuration);
            float eased = t * t * (3f - 2f * t);
            for (int i = 0; i < entries.Count; i++)
            {
                PullEntry e = entries[i];
                if (e.target == null || e.target.IsNullInactiveDeadOrKnockedOut()) continue;
                Vector3 p = Vector3.Lerp(e.start, e.destination, eased);
                p.y = e.target.transform.position.y;
                e.target.transform.position = p;
            }
            yield return null;
        }

        for (int i = 0; i < entries.Count; i++)
        {
            PullEntry e = entries[i];
            if (e.target == null || e.target.IsNullInactiveDeadOrKnockedOut()) continue;
            Vector3 p = e.destination;
            p.y = e.target.transform.position.y;
            e.target.transform.position = p;
        }
    }

    public sealed class PullEntry
    {
        public Entity target;
        public Vector3 start;
        public Vector3 destination;
    }
}
