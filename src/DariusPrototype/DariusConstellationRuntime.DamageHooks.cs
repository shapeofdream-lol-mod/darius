public sealed partial class DariusConstellationRuntime : MonoBehaviour
{
    public static float GetRDamageMultiplier(Hero hero)
    {
        float result = 1f;
        int d = GetStarLevel(hero, DariusConstellationIds.DAxiomArcanist);
        if (d > 0) { float[] values = { 1.10f, 1.13f, 1.16f, 1.20f }; result *= values[Mathf.Clamp(d, 1, values.Length) - 1]; }
        int legacy = GetStarLevel(hero, DariusConstellationIds.ILegacyGuillotine);
        if (legacy > 0) { float[] values = { 1.15f, 1.20f, 1.25f, 1.30f }; result *= values[Mathf.Clamp(legacy, 1, values.Length) - 1]; }
        return result;
    }

    public static float GetHemorrhageDurationBonus(Hero hero)
    {
        int level = GetStarLevel(hero, DariusConstellationIds.FHandOfNoxus);
        if (level <= 0) return 0f;
        float[] values = { 1.0f, 1.5f, 2.0f };
        return values[Mathf.Clamp(level, 1, values.Length) - 1];
    }

    public static float GetNoxianMightAdBonus(Hero hero)
    {
        int level = GetStarLevel(hero, DariusConstellationIds.DNoxianMight);
        if (level <= 0) return 0f;
        float[] values = { 9f, 12f, 15f, 18f };
        return values[Mathf.Clamp(level, 1, values.Length) - 1];
    }

    public static float GetLegacyWSlowDurationBonus(Hero hero)
    {
        int level = GetStarLevel(hero, DariusConstellationIds.ILegacyCripplingStrike);
        if (level <= 0) return 0f;
        float[] values = { 0.25f, 0.35f, 0.45f, 0.55f };
        return values[Mathf.Clamp(level, 1, values.Length) - 1];
    }

    public static float GetLegacyWRefundPerHemorrhageStack(Hero hero)
    {
        int level = GetStarLevel(hero, DariusConstellationIds.ILegacyCripplingStrike);
        if (level <= 0) return 0f;
        float[] values = { 0.50f, 0.60f, 0.70f, 0.80f };
        return values[Mathf.Clamp(level, 1, values.Length) - 1];
    }

    public static void ApplyLegacyApprehendMark(Hero hero, Entity target)
    {
        if (!NetworkServer.active || hero == null || target == null) return;
        int level = GetStarLevel(hero, DariusConstellationIds.ILegacyApprehend);
        if (level <= 0) return;
        DariusLegacyApprehendMark mark = target.GetComponent<DariusLegacyApprehendMark>();
        if (mark == null) mark = target.gameObject.AddComponent<DariusLegacyApprehendMark>();
        mark.Activate(hero, 3f);
    }

    public static float GetLegacyApprehendPhysicalMultiplier(Hero hero, Entity target)
    {
        if (hero == null || target == null) return 1f;
        int level = GetStarLevel(hero, DariusConstellationIds.ILegacyApprehend);
        if (level <= 0) return 1f;
        DariusLegacyApprehendMark mark = target.GetComponent<DariusLegacyApprehendMark>();
        if (mark == null || !mark.IsActiveFor(hero)) return 1f;
        float[] values = { 1.10f, 1.13f, 1.16f, 1.20f };
        return values[Mathf.Clamp(level, 1, values.Length) - 1];
    }

    public static float GetNoxianMightDurationBonus(Hero hero)
    {
        int level = GetStarLevel(hero, DariusConstellationIds.DAxiomArcanist);
        if (level <= 0) return 0f;
        float[] values = { 1.0f, 1.25f, 1.5f, 2.0f };
        return values[Mathf.Clamp(level, 1, values.Length) - 1];
    }

    public static int GetWarFervorLevel(Hero hero) => GetStarLevel(hero, DariusConstellationIds.IWarFervor);

    public static float GetWarFervorWindow(Hero hero)
    {
        int level = GetWarFervorLevel(hero);
        if (level <= 0) return 0f;
        float[] windows = { 5f, 5.5f, 6f, 6.5f };
        return windows[Mathf.Clamp(level, 1, windows.Length) - 1];
    }

    public static void NotifyDamageHit(Hero hero, Entity target, string source)
    {
        if (!NetworkServer.active || hero == null || target == null) return;
        DariusConstellationRuntime runtime = hero.GetComponent<DariusConstellationRuntime>();
        if (runtime != null) runtime.OnDamageHit(source);
    }

    public static void NotifyKill(Hero hero, Entity victim, string source)
    {
        if (!NetworkServer.active || hero == null || victim == null) return;
        DariusConstellationRuntime runtime = hero.GetComponent<DariusConstellationRuntime>();
        if (runtime != null) runtime.OnKill(victim, source);
        DariusEquipmentRuntime.NotifyKill(hero, victim, source);
        DariusVoiceRuntime.NotifyKill(hero, victim);
    }

    public static void NotifyMovementSpell(Hero hero, AbilityTrigger trigger, string spell)
    {
        if (!NetworkServer.active || hero == null) return;
        DariusConstellationRuntime runtime = hero.GetComponent<DariusConstellationRuntime>();
        if (runtime != null) runtime.OnMovementSpell(trigger, spell);
    }

    private void Update()
    {
        if (!NetworkServer.active || _hero == null || _hero.Status == null) return;
        float hp = _hero.currentHealth;
        if (_lastHealth < 0f) _lastHealth = hp;
        if (hp + 0.01f < _lastHealth) OnOwnerDamaged(_lastHealth - hp);
        _lastHealth = hp;

        if (_conquerorStacks > 0 && Time.time >= _conquerorExpiresAt)
        {
            _conquerorStacks = 0;
            RefreshConquerorBonus();
            DariusLog.DebugInfo("STAR-CONQUEROR", "Stacks expired.");
        }

        RefreshLowHealthBonus(false);
        RefreshGatheringStorm(false);
        if (Time.time >= _nextBloodRushRefresh) { _nextBloodRushRefresh = Time.time + 0.20f; RefreshBloodRush(false); }
        if (Time.time >= _nextNoxianArenaRefresh) { _nextNoxianArenaRefresh = Time.time + 0.25f; RefreshNoxianArena(false); }
        TickSecondWind();
    }

    private void OnDamageHit(string source)
    {
        int level = GetLevel(DariusConstellationIds.DConqueror);
        if (level <= 0) return;
        _conquerorStacks = Mathf.Clamp(_conquerorStacks + 1, 0, 6);
        _conquerorExpiresAt = Time.time + 5f;
        RefreshConquerorBonus();
        DariusLog.DebugInfo("STAR-CONQUEROR", "source=" + source + " stacks=" + _conquerorStacks + "/6 level=" + level);
    }
}