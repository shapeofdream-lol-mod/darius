public sealed partial class Ai_Darius_Decimate : AbilityInstance
{
    private static void ResolveServerHit(Hero owner, int castId, bool instantQ, int memoryLevel, float memoryScale, float qDamageMultiplier, AbilityTrigger sourceTrigger, Actor sourceActor)
    {
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