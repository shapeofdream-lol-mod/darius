public sealed partial class DariusConstellationRuntime : MonoBehaviour
{
    private IEnumerator DunkmasterRoutine(int level)
    {
        RemoveBonus(ref _dunkmasterBonus);
        if (_hero != null && _hero.Status != null)
        {
            float[] values = { 15f, 20f, 25f, 30f };
            _dunkmasterBonus = new StatBonus();
            _dunkmasterBonus.movementSpeedPercentage = values[Mathf.Clamp(level,1,values.Length)-1];
            _hero.Status.AddStatBonus(_dunkmasterBonus);
            DariusLog.Info("STAR-DUNKMASTER", "R execute granted +" + _dunkmasterBonus.movementSpeedPercentage + "% move speed for 3s.");
        }
        yield return new WaitForSeconds(3f);
        RemoveBonus(ref _dunkmasterBonus);
        _dunkmasterRoutine = null;
    }

    private void OnOwnerDamaged(float damage)
    {
        int level = GetLevel(DariusConstellationIds.LSecondWind);
        if (level <= 0 || _hero == null || _hero.Status == null) return;
        float[] fractions = { 0.04f, 0.05f, 0.06f, 0.07f };
        float missing = Mathf.Max(0f, _hero.Status.maxHealth - _hero.currentHealth);
        float pool = missing * fractions[Mathf.Clamp(level, 1, fractions.Length) - 1];
        _secondWindPool = Mathf.Max(_secondWindPool, pool);
        _nextSecondWindTick = Mathf.Min(_nextSecondWindTick <= 0f ? Time.time : _nextSecondWindTick, Time.time + 0.25f);
        DariusLog.DebugInfo("STAR-SECOND-WIND", "damage=" + damage.ToString("0.##") + " regenPool=" + _secondWindPool.ToString("0.##") + " level=" + level);
    }

    private void TickSecondWind()
    {
        if (_secondWindPool <= 0.01f || Time.time < _nextSecondWindTick || _hero == null || _hero.Status == null) return;
        float tick = Mathf.Min(_secondWindPool, Mathf.Max(0.25f, _secondWindPool * 0.25f));
        float missing = Mathf.Max(0f, _hero.Status.maxHealth - _hero.currentHealth);
        tick = Mathf.Min(tick, missing);
        if (tick > 0.01f)
        {
            HealData heal = new HealData(tick);
            heal.SetActor(_hero);
            _hero.DoHeal(heal, _hero);
        }
        _secondWindPool = Mathf.Max(0f, _secondWindPool - tick);
        _nextSecondWindTick = Time.time + 1f;
    }

    private void RefreshPersistentBonus()
    {
        RemoveBonus(ref _persistentBonus);
        if (_hero == null || _hero.Status == null) return;
        StatBonus bonus = new StatBonus();
        bool used = false;

        int overgrowth = GetLevel(DariusConstellationIds.LOvergrowth);
        if (overgrowth > 0)
        {
            float[] values = { 25f, 32f, 38f, 45f };
            bonus.maxHealthFlat = values[Mathf.Clamp(overgrowth, 1, values.Length) - 1];
            used = true;
        }

        int conditioning = GetLevel(DariusConstellationIds.LConditioning);
        if (conditioning > 0)
        {
            float[] values = { 0.5f, 0.75f, 1f, 1.25f };
            bonus.armorFlat = values[Mathf.Clamp(conditioning, 1, values.Length) - 1];
            used = true;
        }

        int celerity = GetLevel(DariusConstellationIds.FCelerity);
        if (celerity > 0)
        {
            float[] values = { 5f, 6.5f, 8f };
            bonus.movementSpeedPercentage = values[Mathf.Clamp(celerity, 1, values.Length) - 1];
            used = true;
        }

        if (used)
        {
            _persistentBonus = bonus;
            _hero.Status.AddStatBonus(_persistentBonus);
        }
    }

    private void RefreshConquerorBonus()
    {
        RemoveBonus(ref _conquerorBonus);
        int level = GetLevel(DariusConstellationIds.DConqueror);
        if (level <= 0 || _conquerorStacks <= 0 || _hero == null || _hero.Status == null) return;
        float[] perStack = { 1.5f, 2f, 2.5f, 3f };
        _conquerorBonus = new StatBonus();
        _conquerorBonus.attackDamageFlat = perStack[Mathf.Clamp(level, 1, perStack.Length) - 1] * _conquerorStacks;
        _hero.Status.AddStatBonus(_conquerorBonus);
    }

    private void RefreshAlacrityBonus()
    {
        RemoveBonus(ref _alacrityBonus);
        if (_alacrityStacks <= 0 || _hero == null || _hero.Status == null) return;
        _alacrityBonus = new StatBonus();
        bool set = DariusConstellationReflection.SetFloat(_alacrityBonus, 4f * _alacrityStacks, "attackSpeedPercentage", "attackSpeedPercent");
        if (set) _hero.Status.AddStatBonus(_alacrityBonus);
        else _alacrityBonus = null;
    }

    private void RefreshLowHealthBonus(bool force)
    {
        if (_hero == null || _hero.Status == null || _hero.Status.maxHealth <= 0f) return;
        bool low = _hero.currentHealth / _hero.Status.maxHealth <= 0.40f;
        if (!force && low == _lastLowHealth) return;
        _lastLowHealth = low;
        RemoveBonus(ref _lowHealthBonus);
        if (!low) return;

        int lastStand = GetLevel(DariusConstellationIds.DLastStand);
        int unflinching = GetLevel(DariusConstellationIds.LUnflinching);
        if (lastStand <= 0 && unflinching <= 0) return;
        _lowHealthBonus = new StatBonus();
        bool used = false;
        if (lastStand > 0)
        {
            float[] values = { 9f, 11f, 13f, 15f };
            _lowHealthBonus.attackDamageFlat = values[Mathf.Clamp(lastStand, 1, values.Length) - 1];
            used = true;
        }
        if (unflinching > 0)
        {
            float[] values = { 8f, 10f, 12f, 14f };
            _lowHealthBonus.movementSpeedPercentage = values[Mathf.Clamp(unflinching, 1, values.Length) - 1];
            used = true;
        }
        if (used) _hero.Status.AddStatBonus(_lowHealthBonus);
    }

    private void RefreshGatheringStorm(bool force)
    {
        int level = GetLevel(DariusConstellationIds.FGatheringStorm);
        if (level <= 0 || _hero == null || _hero.Status == null)
        {
            if (_gatheringBonus != null) RemoveBonus(ref _gatheringBonus);
            _lastGatheringStep = -1;
            return;
        }
        int heroLevel = DariusConstellationReflection.ReadEntityLevel(_hero);
        int step = Mathf.Max(0, heroLevel / 5);
        if (!force && step == _lastGatheringStep) return;
        _lastGatheringStep = step;
        RemoveBonus(ref _gatheringBonus);
        if (step <= 0) return;
        float[] perStep = { 2.5f, 3f, 3.5f };
        _gatheringBonus = new StatBonus();
        _gatheringBonus.attackDamageFlat = perStep[Mathf.Clamp(level, 1, perStep.Length) - 1] * step;
        _hero.Status.AddStatBonus(_gatheringBonus);
        DariusLog.Info("STAR-GATHERING", "heroLevel=" + heroLevel + " steps=" + step + " flatAD=" + _gatheringBonus.attackDamageFlat);
    }

    private void ScheduleStarterDrops()
    {
        if (_starterRoutine != null) StopCoroutine(_starterRoutine);
        _starterRoutine = StartCoroutine(StarterDropRoutine());
    }
}