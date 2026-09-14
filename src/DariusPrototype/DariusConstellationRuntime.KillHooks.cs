public sealed partial class DariusConstellationRuntime : MonoBehaviour
{
    private void OnKill(Entity victim, string source)
    {
        int id;
        try { id = victim.GetInstanceID(); } catch { return; }
        float seen;
        if (_recentKills.TryGetValue(id, out seen) && Time.time - seen < 1.5f) return;
        _recentKills[id] = Time.time;
        _killCount++;

        int triumph = GetLevel(DariusConstellationIds.DTriumph);
        if (triumph > 0 && _hero != null && _hero.Status != null)
        {
            float[] fractions = { 0.06f, 0.07f, 0.08f, 0.09f };
            float missing = Mathf.Max(0f, _hero.Status.maxHealth - _hero.currentHealth);
            float amount = missing * fractions[Mathf.Clamp(triumph, 1, fractions.Length) - 1];
            if (amount > 0.01f)
            {
                HealData heal = new HealData(amount);
                heal.SetActor(_hero);
                _hero.DoHeal(heal, _hero);
                DariusLog.Info("STAR-TRIUMPH", "source=" + source + " healed=" + amount.ToString("0.##") + " missing=" + missing.ToString("0.##") + " level=" + triumph);
            }
        }

        int alacrity = GetLevel(DariusConstellationIds.DAlacrity);
        if (alacrity > 0)
        {
            int[] perStack = { 6, 5, 4, 3 };
            int needed = perStack[Mathf.Clamp(alacrity, 1, perStack.Length) - 1];
            int desired = Mathf.Clamp(_killCount / needed, 0, 5);
            if (desired != _alacrityStacks)
            {
                _alacrityStacks = desired;
                RefreshAlacrityBonus();
                DariusLog.Info("STAR-ALACRITY", "kills=" + _killCount + " stacks=" + _alacrityStacks + "/5 level=" + alacrity);
            }
        }

        int bloodPrice = GetLevel(DariusConstellationIds.LBloodPrice);
        if (bloodPrice > 0 && _hero != null && _hero.Status != null)
        {
            DariusHemorrhageRuntime hem = _hero.GetComponent<DariusHemorrhageRuntime>();
            int stacks = hem != null ? hem.GetStacks(victim) : 0;
            if (stacks > 0)
            {
                float[] fractions = { 0.025f, 0.035f, 0.045f, 0.055f };
                float amount = _hero.Status.maxHealth * fractions[Mathf.Clamp(bloodPrice,1,fractions.Length)-1];
                HealData heal = new HealData(amount); heal.SetActor(_hero); _hero.DoHeal(heal, _hero);
                DariusLog.Info("STAR-BLOOD-PRICE", "source=" + source + " stacks=" + stacks + " heal=" + amount.ToString("0.##"));
            }
        }

        int dunk = GetLevel(DariusConstellationIds.DDunkmaster);
        if (dunk > 0 && !string.IsNullOrEmpty(source) && source.IndexOf("R execute", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            if (_dunkmasterRoutine != null) StopCoroutine(_dunkmasterRoutine);
            _dunkmasterRoutine = StartCoroutine(DunkmasterRoutine(dunk));
        }
    }

    private void RefreshBloodRush(bool force)
    {
        int level = GetLevel(DariusConstellationIds.DBloodRush);
        int count = 0;
        if (level > 0 && _hero != null)
        {
            DariusHemorrhageRuntime hem = _hero.GetComponent<DariusHemorrhageRuntime>();
            if (hem != null) count = Mathf.Clamp(hem.GetActiveBleedingTargetCount(), 0, 5);
        }
        if (!force && count == _lastBloodRushCount) return;
        _lastBloodRushCount = count;
        RemoveBonus(ref _bloodRushBonus);
        if (level <= 0 || count <= 0 || _hero == null || _hero.Status == null) return;
        float[] perTarget = { 3f, 3.5f, 4f, 4.5f };
        _bloodRushBonus = new StatBonus();
        _bloodRushBonus.movementSpeedPercentage = perTarget[Mathf.Clamp(level,1,perTarget.Length)-1] * count;
        _hero.Status.AddStatBonus(_bloodRushBonus);
        DariusLog.DebugInfo("STAR-BLOOD-RUSH", "bleedingTargets=" + count + " move%=" + _bloodRushBonus.movementSpeedPercentage);
    }

    // Noxian Arena is intentionally a boss-counter constellation rather than a generic stat stick.
    // It remains modest while clearing packs, then ramps sharply as Darius is isolated with a
    // high-value target. Boss rank overrides Elite rank; enemy count still includes boss adds.
    private void RefreshNoxianArena(bool force)
    {
        int level = GetLevel(DariusConstellationIds.DNoxianArena);
        if (level <= 0 || _hero == null || _hero.Status == null)
        {
            RemoveBonus(ref _noxianArenaBonus);
            _lastNoxianArenaCount = -1;
            _lastNoxianArenaLevel = level;
            _lastNoxianArenaRank = DariusEnemyClass.Common;
            return;
        }

        const float radius = 10f;
        int rawCount = 0;
        DariusEnemyClass strongest = DariusEnemyClass.Common;
        HashSet<Entity> seen = new HashSet<Entity>();
        try
        {
            Collider[] hits = Physics.OverlapSphere(_hero.transform.position, radius);
            for (int i = 0; i < hits.Length; i++)
            {
                Collider hit = hits[i];
                if (hit == null) continue;
                Entity enemy = hit.GetComponentInParent<Entity>();
                if (enemy == null || enemy == _hero || !seen.Add(enemy) || enemy.IsNullInactiveDeadOrKnockedOut()) continue;
                bool hostile = false;
                try { hostile = _hero.GetRelation(enemy).HasFlag(EntityRelation.Enemy); } catch { }
                if (!hostile) continue;

                rawCount++;
                if (strongest != DariusEnemyClass.Boss)
                {
                    DariusEnemyClass kind = DariusEnemyClassifier.Classify(enemy);
                    if (kind == DariusEnemyClass.Boss) strongest = DariusEnemyClass.Boss;
                    else if (kind == DariusEnemyClass.Elite && strongest == DariusEnemyClass.Common) strongest = DariusEnemyClass.Elite;
                }
            }
        }
        catch (Exception e)
        {
            DariusLog.Exception("STAR-NOXIAN-ARENA", e, "Nearby-enemy scan failed");
        }

        int count = Mathf.Clamp(rawCount, 0, 10);
        if (!force && count == _lastNoxianArenaCount && level == _lastNoxianArenaLevel && strongest == _lastNoxianArenaRank) return;
        _lastNoxianArenaCount = count;
        _lastNoxianArenaLevel = level;
        _lastNoxianArenaRank = strongest;
        RemoveBonus(ref _noxianArenaBonus);
        if (count <= 0) return;

        // 10 enemies = x1.00; each missing enemy adds 10%. Elite/Boss rank multiplies
        // that scarcity bonus. Boss encounters cap at x3 so adds can never make the bonus
        // stronger than the explicitly requested solo-Boss result.
        float scarcityMultiplier = 1f + (10 - count) * 0.10f;
        float rankMultiplier = strongest == DariusEnemyClass.Boss ? 2f : strongest == DariusEnemyClass.Elite ? 1.5f : 1f;
        float multiplier = scarcityMultiplier * rankMultiplier;
        if (strongest == DariusEnemyClass.Boss) multiplier = Mathf.Min(3f, multiplier);

        float[] attackDamage = { 3f, 4f, 5f, 6f };
        float[] attackSpeed = { 8f, 10f, 12f, 15f };
        float[] moveSpeed = { 3f, 4f, 5f, 6f };
        float[] armor = { 0.5f, 0.75f, 1f, 1.25f };
        int index = Mathf.Clamp(level, 1, 4) - 1;

        _noxianArenaBonus = new StatBonus();
        _noxianArenaBonus.attackDamageFlat = attackDamage[index] * multiplier;
        _noxianArenaBonus.movementSpeedPercentage = moveSpeed[index] * multiplier;
        bool attackSpeedSet = DariusConstellationReflection.SetFloat(_noxianArenaBonus, attackSpeed[index] * multiplier, "attackSpeedPercentage", "attackSpeedPercent");
        _noxianArenaBonus.armorFlat = armor[index] * multiplier;
        _hero.Status.AddStatBonus(_noxianArenaBonus);

        DariusLog.Info("STAR-NOXIAN-ARENA", "level=" + level + " enemies=" + rawCount + " counted=" + count +
            " rank=" + strongest + " scarcityX=" + scarcityMultiplier.ToString("0.##") + " rankX=" + rankMultiplier.ToString("0.##") +
            " totalX=" + multiplier.ToString("0.##") + " flatAD=" + _noxianArenaBonus.attackDamageFlat.ToString("0.#") +
            " MS%=" + _noxianArenaBonus.movementSpeedPercentage.ToString("0.#") + " ASField=" + attackSpeedSet +
            " flatArmor=" + _noxianArenaBonus.armorFlat.ToString("0.##"));
    }
}