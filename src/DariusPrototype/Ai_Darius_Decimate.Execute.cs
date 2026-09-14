public sealed partial class Ai_Darius_Decimate : AbilityInstance
{
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
            DariusLog.Error("Q", "cast#" + castId + " aborted: caster is not a Hero.");
            yield break;
        }

        DariusEquipmentRuntime.NotifySkillCast(owner, "Q cast#" + castId);

        int memoryLevel = DariusMemoryScaling.GetLevel(sourceTrigger);
        float memoryScale = DariusMemoryScaling.Multiplier(memoryLevel);
        int instantQLevel = DariusConstellationRuntime.GetStarLevel(owner, DariusConstellationIds.IInstantDecimate);
        bool instantQ = instantQLevel > 0;
        float actualWindup = instantQ ? 0f : Windup;
        float qDamageMultiplier = DariusConstellationRuntime.GetInstantQDamageMultiplier(owner);

        DariusLog.Info("Q", "cast#" + castId + " start caster=" + DariusLog.EntityLabel(owner) +
            " windup=" + actualWindup + " inner=" + InnerRadius + " outer=" + OuterRadius +
            " memoryLevel=" + memoryLevel + " memoryScale=" + memoryScale.ToString("0.###") +
            " instantStarLevel=" + instantQLevel + " damageMultiplier=" + qDamageMultiplier.ToString("0.###") +
            " castPoint=" + DariusLog.Vec(castInfo.point));

        try { DariusSkinAnimationHooks.PlayQ(owner, instantQ); }
        catch (Exception e) { DariusLog.Exception("Q-ANIM", e, "cast#" + castId + " Darius skin Q animation failed"); }

        if (!instantQ)
        {
            try
            {
                // Movement is deliberately NOT locked during the normal windup.
                DariusPrototypeVfx.CreateQTelegraph(owner.transform, InnerRadius, OuterRadius, actualWindup);
            }
            catch (Exception e)
            {
                DariusLog.Exception("Q-VFX", e, "cast#" + castId + " telegraph failed");
            }
            yield return new UnityEngine.WaitForSeconds(actualWindup);
        }
        else
        {
            // No windup animation and no gameplay wait. Fast-forward the Riot Q ring to its authored
            // spin phase and resolve this same frame. Healing remains disabled by design.
            try { DariusPrototypeVfx.CreateQInstantSwing(owner); }
            catch (Exception e) { DariusLog.Exception("Q-VFX", e, "cast#" + castId + " instant swing failed"); }
            DariusLog.Info("Q-STAR", "cast#" + castId + " Instant Decimate active: no windup, no heal, all hits forced to OUTER damage/bleed rules.");
        }

        if (owner == null || owner.IsNullOrInactive())
        {
            DariusLog.Warn("Q", "cast#" + castId + " ended after windup because caster became inactive.");
            yield break;
        }

        DariusLog.DebugInfo("Q", "cast#" + castId + " windup complete at center=" + DariusLog.Vec(owner.transform.position));

        if (!NetworkServer.active)
        {
            DariusLog.DebugInfo("Q", "cast#" + castId + " client visual-only branch; gameplay resolution skipped.");
            if (!instantQ)
            {
                try { DariusPrototypeVfx.CreateQSwing(owner, owner.transform.position, InnerRadius, OuterRadius); }
                catch (Exception e) { DariusLog.Exception("Q-VFX", e, "cast#" + castId + " client swing failed"); }
            }
            yield break;
        }

        ResolveServerHit(owner, castId, instantQ, memoryLevel, memoryScale, qDamageMultiplier, sourceTrigger, sourceActor);
    }
}