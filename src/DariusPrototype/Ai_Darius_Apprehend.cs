using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;

public sealed class Ai_Darius_Apprehend : AbilityInstance
{
    public const float Range = 5.35f;
    public const float HalfAngle = 30f;
    public const float PullFrontDistance = 1.35f;
    public const float SlowStrength = 0.40f;
    public const float SlowDuration = 1.25f;
    public const float PullDuration = 0.22f;
    public const int UtilityGrowthMilestoneEveryLevels = 5;
    public const float RangePerStep = 0.10f;
    public const float SlowDurationPerStep = 0.08f;

    public static float RangeAtLevel(int level)
    {
        return DariusMemoryScaling.MilestoneStepped(Range, RangePerStep, level, UtilityGrowthMilestoneEveryLevels);
    }

    public static float SlowDurationAtLevel(int level)
    {
        return DariusMemoryScaling.MilestoneStepped(SlowDuration, SlowDurationPerStep, level, UtilityGrowthMilestoneEveryLevels);
    }
    // Spell3 reaches the actual hook/pull phase shortly after the cast starts.
    // Keep the gameplay snapshot on that motion instead of pulling on frame zero.
    public const float HookDelay = 0.28f;

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
            DariusLog.Error("E", "cast#" + castId + " aborted: caster is not a Hero.");
            yield break;
        }

        DariusEquipmentRuntime.NotifySkillCast(owner, "E cast#" + castId);

        int memoryLevel = DariusMemoryScaling.GetLevel(sourceTrigger);
        float memoryScale = DariusMemoryScaling.Multiplier(memoryLevel);
        float scaledRange = RangeAtLevel(memoryLevel);
        float scaledSlowDuration = SlowDurationAtLevel(memoryLevel);

        Vector3 origin = owner.transform.position;
        Vector3 forward = castInfo.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.001f) forward = owner.transform.forward;
        forward.y = 0f;
        forward.Normalize();

        DariusLog.Info("E", "cast#" + castId + " start owner=" + DariusLog.EntityLabel(owner) +
            " origin=" + DariusLog.Vec(origin) + " forward=" + DariusLog.Vec(forward) +
            " range=" + scaledRange.ToString("0.###") + " halfAngle=" + HalfAngle +
            " memoryLevel=" + memoryLevel + " memoryScale=" + memoryScale.ToString("0.###") +
            " slowDuration=" + scaledSlowDuration.ToString("0.###"));

        try { DariusSkinAnimationHooks.PlayE(owner); }
        catch (Exception e) { DariusLog.Exception("E-ANIM", e, "cast#" + castId + " Darius skin E animation failed"); }
        try { DariusPrototypeVfx.CreateEWindup(owner); }
        catch (Exception e) { DariusLog.Exception("E-VFX", e, "cast#" + castId + " weapon-follow windup failed"); }

        // Spell3 reaches the forward hook beat around 0.28 s. Delay the visible hook crescent
        // to that exact beat instead of drawing the old cone on frame zero.
        yield return new UnityEngine.WaitForSeconds(HookDelay);

        if (owner == null || owner.IsNullOrInactive())
        {
            DariusLog.Warn("E", "cast#" + castId + " caster became inactive before hook frame.");
            yield break;
        }

        // The cone direction is intentionally snapshotted at cast start, matching the visible Spell3 swing.
        // The origin is refreshed at the hook frame so a moving caster pulls around its actual position.
        origin = owner.transform.position;
        try { DariusPrototypeVfx.CreateECone(owner, origin, forward, scaledRange, HalfAngle); }
        catch (Exception e) { DariusLog.Exception("E-VFX", e, "cast#" + castId + " hook-beat VFX failed"); }

        if (!NetworkServer.active)
        {
            DariusLog.DebugInfo("E", "cast#" + castId + " client visual-only branch after hook delay=" + HookDelay);
            yield break;
        }

        List<DariusNativeDisplacement.PullEntry> fallbackPulls = new List<DariusNativeDisplacement.PullEntry>();
        int pullIndex = 0;
        int nativePulls = 0;
        int enemiesInSphere = 0;
        try
        {
            Collider[] colliders = Physics.OverlapSphere(origin, scaledRange);
            HashSet<Entity> resolved = new HashSet<Entity>();
            DariusLog.DebugInfo("E", "cast#" + castId + " overlap colliders=" + colliders.Length +
                " hookDelay=" + HookDelay.ToString("0.###") + " pullDuration=" + PullDuration.ToString("0.###"));

            for (int i = 0; i < colliders.Length; i++)
            {
                Entity target = colliders[i].GetComponentInParent<Entity>();
                if (target == null || target == owner || !resolved.Add(target)) continue;
                if (target.IsNullInactiveDeadOrKnockedOut()) continue;
                if (!owner.GetRelation(target).HasFlag(EntityRelation.Enemy)) continue;
                enemiesInSphere++;

                Vector3 delta = target.transform.position - origin;
                delta.y = 0f;
                float distance = delta.magnitude;
                if (distance <= 0.001f || distance > scaledRange) continue;
                float angle = Vector3.Angle(forward, delta / distance);
                if (angle > HalfAngle)
                {
                    DariusLog.DebugInfo("E", "cast#" + castId + " outside cone target=" + DariusLog.EntityLabel(target) +
                        " dist=" + distance.ToString("0.###") + " angle=" + angle.ToString("0.##"));
                    continue;
                }

                float side = ((pullIndex & 1) == 0 ? 1f : -1f) * (pullIndex / 2) * 0.22f;
                Vector3 perpendicular = new Vector3(-forward.z, 0f, forward.x);
                Vector3 destination = origin + forward * PullFrontDistance + perpendicular * side;
                destination.y = target.transform.position.y;
                Vector3 before = target.transform.position;
                try { DariusPrototypeVfx.CreateEPull(owner, before, destination); }
                catch (Exception e) { DariusLog.Exception("E-VFX", e, "cast#" + castId + " pull streak failed"); }

                // Native-first: ask EntityControl to run Shape of Dreams' Displacement contract.
                // If this game build exposes a different signature, keep a short time-based pull
                // rather than reverting to the old one-frame Entity.Teleport.
                bool native = DariusNativeDisplacement.TryPull(target, destination, PullDuration, "E cast#" + castId);
                if (native) nativePulls++;
                else fallbackPulls.Add(new DariusNativeDisplacement.PullEntry { target = target, start = before, destination = destination });

                DariusSlowHelper.Apply(target, SlowStrength, scaledSlowDuration, "E cast#" + castId);
                DariusConstellationRuntime.ApplyLegacyApprehendMark(owner, target);
                DariusLog.Info("E-PULL", "cast#" + castId + " target=" + DariusLog.EntityLabel(target) +
                    " dist=" + distance.ToString("0.###") + " angle=" + angle.ToString("0.##") +
                    " from=" + DariusLog.Vec(before) + " requestedDest=" + DariusLog.Vec(destination) +
                    " mode=" + (native ? "native-displacement" : "smooth-fallback"));
                pullIndex++;
            }
        }
        catch (Exception e)
        {
            DariusLog.Exception("E", e, "cast#" + castId + " server pull selection failed");
        }

        if (fallbackPulls.Count > 0)
            yield return DariusNativeDisplacement.SmoothFallback(fallbackPulls, PullDuration);

        if (pullIndex > 0)
        {
            try { DariusPrototypeVfx.PlayEPull(owner, origin); }
            catch (Exception e) { DariusLog.Exception("E-SFX", e, "cast#" + castId + " pull audio failed"); }
        }
        DariusLog.Info("E", "cast#" + castId + " complete enemiesInSphere=" + enemiesInSphere +
            " pulled=" + pullIndex + " native=" + nativePulls + " fallback=" + fallbackPulls.Count);

        yield break;
    }
}
