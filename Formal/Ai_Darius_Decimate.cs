using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;

// Darius Q. Core invariant: one delayed resolution, one physics snapshot,
// mutually-exclusive inner/outer classification, one damage event per target.
public sealed class Ai_Darius_Decimate : AbilityInstance
{
    public const float Windup = 0.75f;
    public const float InnerRadius = 2.40f;
    public const float OuterRadius = 4.25f;
    // PvE baseline: Q has a real opportunity cost because Darius must deliberately space into the
    // axe edge during a 0.75 s windup. Its sustain therefore needs to repay the damage taken while
    // setting up the outer ring, rather than copying the lower PvP values from League.
    public const float InnerAdRatio = 1.50f;
    public const float OuterNoEssenceAdRatio = 2.25f;
    public const float HealMaxHpPerOuterTarget = 0.10f;
    public const float HealCapMaxHp = 0.50f; // Five outer targets = 50% max Health.

    public const float DamageGrowthPerAdditionalLevel = 0.20f;

    public static float DamageRatioAtLevel(float baseRatio, int level)
    {
        return DariusMemoryScaling.Linear(baseRatio, level, DamageGrowthPerAdditionalLevel);
    }

    // Q already receives continuous damage growth. Sustain is deliberately stable so an infinitely
    // levelled Memory does not become permanent full-healing: spacing into the outer blade is worth
    // 10% per enemy from level 1 onward, capped at five enemies / 50% before constellation modifiers.
    public static float HealPerTargetAtLevel(int level)
    {
        return HealMaxHpPerOuterTarget;
    }

    public static float HealCapAtLevel(int level)
    {
        return HealCapMaxHp;
    }

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

        try
        {
            // Exactly ONE physics snapshot at the end of the windup.
            Vector3 center = owner.transform.position;
            Collider[] colliders = Physics.OverlapSphere(center, OuterRadius);
            HashSet<Entity> resolved = new HashSet<Entity>();
            DariusHemorrhageRuntime hemorrhage = owner.GetComponent<DariusHemorrhageRuntime>();
            bool essenceActive = hemorrhage != null && hemorrhage.isEnabled;
            int outerTargets = 0;
            int innerTargets = 0;
            int enemyCandidates = 0;
            float adSnapshot = owner.Status.finalStats.attackDamage;

            DariusLog.Info("Q", "cast#" + castId + " resolving snapshot colliders=" + colliders.Length +
                " center=" + DariusLog.Vec(center) + " AD=" + adSnapshot.ToString("0.##") +
                " hemorrhage=" + essenceActive);

            for (int i = 0; i < colliders.Length; i++)
            {
                Entity target = colliders[i].GetComponentInParent<Entity>();
                if (target == null || target == owner || !resolved.Add(target)) continue;
                if (target.IsNullInactiveDeadOrKnockedOut()) continue;
                if (!owner.GetRelation(target).HasFlag(EntityRelation.Enemy)) continue;
                enemyCandidates++;

                Vector3 d = target.transform.position - center;
                d.y = 0f;
                float dist = d.magnitude;
                if (dist > OuterRadius)
                {
                    DariusLog.DebugInfo("Q", "cast#" + castId + " rejected outside radius target=" + DariusLog.EntityLabel(target) + " dist=" + dist.ToString("0.###"));
                    continue;
                }

                // Instant Decimate intentionally removes the spacing minigame together with its
                // windup. Any legal Q hit is treated as an outer-blade hit. It gets the actual outer
                // damage ratio even when Hemorrhage is equipped; Hemorrhage is still required to
                // create a bleed stack (the star never grants the Identity by itself).
                bool inner = !instantQ && dist <= InnerRadius;
                bool outer = instantQ || !inner;
                float ratio = instantQ ? OuterNoEssenceAdRatio : InnerAdRatio;

                if (outer)
                {
                    outerTargets++;
                    if (!essenceActive || instantQ) ratio = OuterNoEssenceAdRatio;
                }
                else
                {
                    innerTargets++;
                }

                ratio = DamageRatioAtLevel(ratio, memoryLevel) * qDamageMultiplier;
                float amount = adSnapshot * ratio;
                amount = DariusEquipmentRuntime.ModifyBasicSkillDamage(owner, amount);
                float hpBefore = target.currentHealth;
                DariusLog.Info("Q-HIT", "cast#" + castId + " target=" + DariusLog.EntityLabel(target) +
                    " dist=" + dist.ToString("0.###") + " zone=" + (outer ? "OUTER" : "INNER") +
                    " ratio=" + ratio.ToString("0.###") + " damage=" + amount.ToString("0.##") +
                    " hpBefore=" + hpBefore.ToString("0.##"));

                // Damage resolves before the new Hemorrhage stack. This prevents the fifth stack
                // from granting Noxian Might early enough to buff the very Q hit that created it.
                DariusMemoryEffectBridge.DealPhysical(owner, sourceActor, sourceTrigger, amount, 1f, target, "Q cast#" + castId);
                DariusConstellationRuntime.NotifyDamageHit(owner, target, "Q cast#" + castId);
                DariusEquipmentRuntime.NotifyPhysicalSkillHit(owner, target, "Q cast#" + castId, true);
                if (target == null || target.IsNullInactiveDeadOrKnockedOut() || target.currentHealth <= 0f)
                    DariusConstellationRuntime.NotifyKill(owner, target, "Q cast#" + castId);

                if (outer && essenceActive && target != null && !target.IsNullInactiveDeadOrKnockedOut())
                {
                    int before = hemorrhage.GetStacks(target);
                    hemorrhage.Apply(target, 1, "Q cast#" + castId);
                    DariusLog.Info("Q-BLEED", "cast#" + castId + " target=" + target.name + " stacks " + before + " -> " + hemorrhage.GetStacks(target));
                }

                try { DariusPrototypeVfx.CreateHitFlash(owner, target.transform.position, outer); }
                catch (Exception e) { DariusLog.Exception("Q-VFX", e, "cast#" + castId + " hit flash failed"); }
            }

            // User design lock: Hemorrhage removes Q outer-edge BONUS DAMAGE, not the heal.
            // Therefore every outer-edge hit can heal regardless of whether the Essence is equipped.
            if (outerTargets > 0 && !instantQ)
            {
                float fraction = Mathf.Min(
                    HealCapAtLevel(memoryLevel),
                    HealPerTargetAtLevel(memoryLevel) * outerTargets);
                float healMultiplier = DariusConstellationRuntime.GetQHealMultiplier(owner);
                float amount = owner.Status.maxHealth * fraction * healMultiplier;
                DariusMemoryEffectBridge.HealSelf(owner, sourceActor, sourceTrigger, amount, "Q cast#" + castId);
                DariusLog.Info("Q-HEAL", "cast#" + castId + " outerTargets=" + outerTargets +
                    " hemorrhage=" + essenceActive + " healFractionMaxHP=" + fraction.ToString("0.###") +
                    " memoryLevel=" + memoryLevel + " memoryScale=" + memoryScale.ToString("0.###") +
                    " starMultiplier=" + healMultiplier.ToString("0.###") + " heal=" + amount.ToString("0.##"));
                try { DariusPrototypeVfx.CreateQHeal(owner.transform); }
                catch (Exception e) { DariusLog.Exception("Q-VFX", e, "cast#" + castId + " heal VFX failed"); }
            }
            else if (instantQ)
            {
                DariusLog.Info("Q-HEAL", "cast#" + castId + " healing suppressed by Instant Decimate constellation.");
            }
            else
            {
                DariusLog.DebugInfo("Q-HEAL", "cast#" + castId + " no heal because outerTargets=0.");
            }

            if (!instantQ)
            {
                try { DariusPrototypeVfx.CreateQSwing(owner, center, InnerRadius, OuterRadius); }
                catch (Exception e) { DariusLog.Exception("Q-VFX", e, "cast#" + castId + " swing failed"); }
            }

            DariusLog.Info("Q", "cast#" + castId + " complete candidates=" + enemyCandidates +
                " innerHits=" + innerTargets + " outerHits=" + outerTargets + " resolvedUnique=" + resolved.Count);
        }
        catch (Exception e)
        {
            DariusLog.Exception("Q", e, "cast#" + castId + " server resolution crashed");
        }
    }
}
