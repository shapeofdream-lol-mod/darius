public sealed partial class DariusEquipmentRuntime : MonoBehaviour
{
    private void ApplyBlackCleaver(Entity target, string source)
    {
        int level = GetLevel(DariusEquipmentStarIds.BlackCleaver);
        if (level <= 0 || target == null || target.Status == null) return;
        int id;
        try { id = target.GetInstanceID(); } catch { return; }
        ArmorShred entry;
        if (!_blackCleaver.TryGetValue(id, out entry) || entry == null || entry.target != target)
        {
            entry = new ArmorShred { target = target, stacks = 0, expiresAt = 0f, bonus = null };
            _blackCleaver[id] = entry;
        }
        entry.stacks = Mathf.Clamp(entry.stacks + 1, 1, 5);
        entry.expiresAt = Time.time + 6f;
        if (entry.bonus != null)
        {
            try { target.Status.RemoveStatBonus(entry.bonus); } catch { }
        }
        float[] shredPerStack = { 5f, 5.5f, 6f, 6.5f };
        entry.bonus = new StatBonus();
        float amount = -shredPerStack[Mathf.Clamp(level, 1, shredPerStack.Length) - 1] * entry.stacks;
        if (!DariusConstellationReflection.SetFloat(entry.bonus, amount, "armorPercentage", "armorPercent"))
        {
            DariusLog.Warn("ITEM-CLEAVER", "Could not resolve StatBonus armor-percent field; skipping this shred refresh instead of hard-binding a private game field.");
            entry.bonus = null;
            return;
        }
        target.Status.AddStatBonus(entry.bonus);
        DariusLog.DebugInfo("ITEM-CLEAVER", "source=" + source + " target=" + DariusLog.EntityLabel(target) +
            " stacks=" + entry.stacks + "/5 armor%=" + amount.ToString("0.#"));
    }

    private void TriggerSunderedSky(Entity target)
    {
        int level = GetLevel(DariusEquipmentStarIds.SunderedSky);
        if (level <= 0 || target == null) return;
        int id;
        try { id = target.GetInstanceID(); } catch { return; }
        float ready;
        if (_sunderedReady.TryGetValue(id, out ready) && Time.time < ready) return;
        float[] cds = { 6f, 5.7f, 5.4f, 5.0f };
        _sunderedReady[id] = Time.time + cds[Mathf.Clamp(level, 1, cds.Length) - 1];

        float[] ratios = { 1.00f, 1.15f, 1.30f, 1.50f };
        float damage = _hero.Status.finalStats.attackDamage * ratios[Mathf.Clamp(level, 1, ratios.Length) - 1];
        DariusMemoryEffectBridge.DealPhysical(_hero, _hero, null, damage, 0.35f, target, "Sundered Sky");

        if (_hero.Status != null)
        {
            float missing = Mathf.Max(0f, _hero.Status.maxHealth - _hero.currentHealth);
            float[] healFractions = { 0.05f, 0.06f, 0.07f, 0.08f };
            float heal = missing * healFractions[Mathf.Clamp(level, 1, healFractions.Length) - 1];
            if (heal > 0.01f) DariusMemoryEffectBridge.HealSelf(_hero, _hero, null, heal, "Sundered Sky");
        }
        DariusLog.Info("ITEM-SUNDERED", "Primed hit target=" + DariusLog.EntityLabel(target) + " damage=" + damage.ToString("0.##"));
        NotifyEquipmentKillIfNeeded(target, "Sundered Sky");
    }

    private void TriggerDeadMansPlate(Entity target)
    {
        int level = GetLevel(DariusEquipmentStarIds.DeadMansPlate);
        if (level <= 0 || target == null || _deadMansMomentum <= 0.01f) return;
        float normalized = Mathf.Clamp01(_deadMansMomentum / 100f);
        float[] ratios = { 0.50f, 0.60f, 0.70f, 0.85f };
        float damage = _hero.Status.finalStats.attackDamage * ratios[Mathf.Clamp(level, 1, ratios.Length) - 1] * normalized;
        bool wasFull = _deadMansMomentum >= 99.5f;
        _deadMansMomentum = 0f;
        DariusMemoryEffectBridge.DealPhysical(_hero, _hero, null, damage, 0.25f, target, "Dead Man's Plate Momentum");
        if (wasFull)
        {
            float[] slows = { 0.40f, 0.45f, 0.50f, 0.55f };
            DariusSlowHelper.Apply(target, slows[Mathf.Clamp(level, 1, slows.Length) - 1], 1.25f, "Dead Man's Plate full Momentum");
        }
        DariusLog.Info("ITEM-DEADMAN", "Momentum consumed ratio=" + normalized.ToString("0.00") + " full=" + wasFull + " damage=" + damage.ToString("0.##"));
        NotifyEquipmentKillIfNeeded(target, "Dead Man's Plate");
    }

    private void OnStridebreakerW(Entity primaryTarget, AbilityTrigger sourceTrigger, Actor sourceActor, string source)
    {
        int level = GetLevel(DariusEquipmentStarIds.Stridebreaker);
        if (level <= 0 || primaryTarget == null || _hero == null) return;
        float[] ratios = { 0.60f, 0.70f, 0.80f, 0.95f };
        float[] slows = { 0.35f, 0.40f, 0.45f, 0.50f };
        float ratio = ratios[Mathf.Clamp(level, 1, ratios.Length) - 1];
        float slow = slows[Mathf.Clamp(level, 1, slows.Length) - 1];
        float damage = _hero.Status.finalStats.attackDamage * ratio;
        Vector3 center = primaryTarget.transform.position;
        Collider[] hits = Physics.OverlapSphere(center, 3.0f);
        HashSet<int> seen = new HashSet<int>();
        int hitCount = 0;
        for (int i = 0; i < hits.Length; i++)
        {
            Entity enemy = hits[i] != null ? hits[i].GetComponentInParent<Entity>() : null;
            if (enemy == null || enemy.IsNullInactiveDeadOrKnockedOut()) continue;
            int id;
            try { id = enemy.GetInstanceID(); } catch { continue; }
            if (!seen.Add(id) || !_hero.GetRelation(enemy).HasFlag(EntityRelation.Enemy)) continue;
            DariusMemoryEffectBridge.DealPhysical(_hero, sourceActor, sourceTrigger, damage, 0.25f, enemy, "Stridebreaker W pulse");
            DariusSlowHelper.Apply(enemy, slow, 1.5f, "Stridebreaker W pulse");
            hitCount++;
            NotifyEquipmentKillIfNeeded(enemy, "Stridebreaker W");
        }
        DariusLog.Info("ITEM-STRIDE", "W mechanism pulse source=" + source + " targets=" + hitCount + " radius=3 damageEach=" + damage.ToString("0.##"));
    }

    private void OnOwnerDamaged(float rawDamage)
    {
        if (rawDamage <= 0.01f || _hero == null || _hero.Status == null) return;
        _lastCombatAt = Time.time;

        int deathDance = GetLevel(DariusEquipmentStarIds.DeathsDance);
        if (deathDance > 0)
        {
            float[] fractions = { 0.05f, 0.10f, 0.15f, 0.20f };
            float deferred = rawDamage * fractions[Mathf.Clamp(deathDance, 1, fractions.Length) - 1];
            if (deferred > 0.01f)
            {
                // Damage already arrived through the native chain. Refund only the deferred slice now,
                // then feed that exact slice back through DamageData over six half-second ticks.
                DariusMemoryEffectBridge.HealSelf(_hero, _hero, null, deferred, "Death's Dance defer");
                _deathDance.Add(new DeferredDamage { perTick = deferred / 6f, ticksRemaining = 6, nextTick = Time.time + 0.5f });
                DariusLog.DebugInfo("ITEM-DEATHDANCE", "Deferred=" + deferred.ToString("0.##") + " raw=" + rawDamage.ToString("0.##") + " activeQueues=" + _deathDance.Count);
            }
        }

        TryTriggerSterak();
    }

    private void TryTriggerSterak()
    {
        int level = GetLevel(DariusEquipmentStarIds.SteraksGage);
        if (level <= 0 || _hero == null || _hero.Status == null || Time.time < _sterakReadyAt) return;
        float max = Mathf.Max(1f, _hero.Status.maxHealth);
        if (_hero.currentHealth / max > 0.35f) return;

        float[] barrier = { 0.25f, 0.30f, 0.35f, 0.40f };
        float[] cooldown = { 30f, 28f, 26f, 24f };
        float fraction = barrier[Mathf.Clamp(level, 1, barrier.Length) - 1];
        _sterakReadyAt = Time.time + cooldown[Mathf.Clamp(level, 1, cooldown.Length) - 1];
        RemoveSterakBarrier();

        _sterakBarrierBonus = new StatBonus();
        DariusConstellationReflection.SetFloat(_sterakBarrierBonus, fraction * 100f, "maxHealthPercentage", "maxHealthPercent", "healthPercentage");
        _hero.Status.AddStatBonus(_sterakBarrierBonus);
        float temporaryHealth = max * fraction;
        DariusMemoryEffectBridge.HealSelf(_hero, _hero, null, temporaryHealth, "Sterak Lifeline temporary barrier");
        _sterakBarrierRoutine = StartCoroutine(RemoveSterakAfter(5f));
        DariusLog.Info("ITEM-STERAK", "Lifeline triggered temporaryHealth=" + temporaryHealth.ToString("0.##") + " duration=5 cooldown=" + cooldown[Mathf.Clamp(level,1,cooldown.Length)-1].ToString("0.#"));
    }

    private IEnumerator RemoveSterakAfter(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        RemoveSterakBarrier();
        _sterakBarrierRoutine = null;
    }

    private void RemoveSterakBarrier()
    {
        if (_sterakBarrierBonus != null && _hero != null && _hero.Status != null)
        {
            try { _hero.Status.RemoveStatBonus(_sterakBarrierBonus); } catch { }
        }
        _sterakBarrierBonus = null;
    }
}