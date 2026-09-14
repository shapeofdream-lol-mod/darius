public sealed partial class DariusEquipmentRuntime : MonoBehaviour
{
    // W is a reinforced basic attack. The native attack-hit event already contributes Black Cleaver,
    // so W uses this Shojin-only entry to avoid applying two Cleaver stacks for one impact.
    public static void NotifyShojinSkillHit(Hero hero, string source)
    {
        if (!NetworkServer.active || hero == null) return;
        DariusEquipmentRuntime runtime = Get(hero, false);
        if (runtime != null) runtime.AddShojinStack(source);
    }

    public static float ModifyBasicSkillDamage(Hero hero, float amount)
    {
        DariusEquipmentRuntime runtime = Get(hero, false);
        return runtime != null ? runtime.ApplyShojinDamage(amount) : amount;
    }

    public static void NotifyKill(Hero hero, Entity victim, string source)
    {
        if (!NetworkServer.active || hero == null || victim == null) return;
        DariusEquipmentRuntime runtime = Get(hero, false);
        if (runtime != null) runtime.OnKill(victim, source);
    }

    public static void ApplyStridebreakerW(Hero hero, Entity primaryTarget, AbilityTrigger sourceTrigger, Actor sourceActor, string source)
    {
        if (!NetworkServer.active || hero == null || primaryTarget == null) return;
        DariusEquipmentRuntime runtime = Get(hero, false);
        if (runtime != null) runtime.OnStridebreakerW(primaryTarget, sourceTrigger, sourceActor, source);
    }

    public static bool TryGetAwooRRatio(Hero hero, out float ratio)
    {
        ratio = 0f;
        DariusEquipmentRuntime runtime = Get(hero, false);
        if (runtime == null || runtime.GetLevel(DariusEquipmentStarIds.Awoo) <= 0) return false;
        runtime.CheckAwooRoom();
        ratio = runtime._awooRAdRatio;
        return true;
    }

    public static bool NotifyAwooRCast(Hero hero)
    {
        if (!NetworkServer.active || hero == null) return false;
        DariusEquipmentRuntime runtime = Get(hero, false);
        if (runtime == null || runtime.GetLevel(DariusEquipmentStarIds.Awoo) <= 0) return false;
        runtime.CheckAwooRoom();
        runtime.RestartAnimals();
        DariusLog.Info("STAR-AWOO", "R cast started room=" + runtime._awooRoomToken +
            " ratio=" + runtime._awooRAdRatio.ToString("0.00") + " pitch=" + runtime._awooPitch.ToString("0.0"));
        return true;
    }

    public static void NotifyAwooRResult(Hero hero, bool killed, string source)
    {
        if (!NetworkServer.active || hero == null) return;
        DariusEquipmentRuntime runtime = Get(hero, false);
        if (runtime == null || runtime.GetLevel(DariusEquipmentStarIds.Awoo) <= 0) return;
        runtime.CheckAwooRoom();
        if (killed)
        {
            runtime._awooRAdRatio += 0.05f;
            runtime._awooPitch = Mathf.Clamp(runtime._awooPitch + 0.1f, 0.5f, 2.0f);
        }
        else
        {
            runtime._awooPitch = Mathf.Clamp(runtime._awooPitch - 0.1f, 0.5f, 2.0f);
        }
        runtime.RestartAnimals();
        DariusLog.Info("STAR-AWOO", "R result killed=" + killed + " source=" + source +
            " roomRatio=" + runtime._awooRAdRatio.ToString("0.00") + " pitch=" + runtime._awooPitch.ToString("0.0"));
    }

    private void OnSkillCast(string source)
    {
        int trinity = GetLevel(DariusEquipmentStarIds.TrinityForce);
        if (trinity > 0 && Time.time >= _trinityReadyAt)
        {
            _trinityArmedUntil = Time.time + 4f;
            DariusLog.DebugInfo("ITEM-TRINITY", "Spellblade armed source=" + source + " until=" + _trinityArmedUntil.ToString("0.00"));
        }
    }

    private float ApplyShojinDamage(float amount)
    {
        int level = GetLevel(DariusEquipmentStarIds.SpearOfShojin);
        if (level <= 0 || _shojinStacks <= 0 || Time.time >= _shojinExpiresAt) return amount;
        float[] perStack = { 0.05f, 0.06f, 0.07f, 0.085f };
        float mult = 1f + perStack[Mathf.Clamp(level, 1, perStack.Length) - 1] * _shojinStacks;
        return amount * mult;
    }

    private void OnPhysicalSkillHit(Entity target, string source, bool countsForShojin)
    {
        if (target == null) return;
        _lastCombatAt = Time.time;
        ApplyBlackCleaver(target, source);
        if (countsForShojin) AddShojinStack(source);
    }

    private void AddShojinStack(string source)
    {
        int level = GetLevel(DariusEquipmentStarIds.SpearOfShojin);
        if (level <= 0) return;
        // Q is one cast even when it catches ten enemies. The cast serial embedded in source keeps
        // AoE from instantly jumping from zero to four stacks.
        if (!string.IsNullOrEmpty(source) && string.Equals(source, _lastShojinSource, StringComparison.Ordinal) && Time.time - _lastShojinSourceAt < 0.35f)
            return;
        _lastShojinSource = source;
        _lastShojinSourceAt = Time.time;
        _shojinStacks = Mathf.Clamp(_shojinStacks + 1, 0, 4);
        _shojinExpiresAt = Time.time + 6f;
        DariusLog.DebugInfo("ITEM-SHOJIN", "source=" + source + " stacks=" + _shojinStacks + "/4 level=" + level);
    }

    private void OnAttackHit(EventInfoAttackHit info)
    {
        if (!NetworkServer.active || _hero == null || info.attacker != _hero || info.victim == null) return;
        if (!_hero.GetRelation(info.victim).HasFlag(EntityRelation.Enemy)) return;
        _lastCombatAt = Time.time;

        TriggerTrinity(info.victim);
        TriggerSunderedSky(info.victim);
        TriggerDeadMansPlate(info.victim);
        ApplyBlackCleaver(info.victim, "basic attack");
    }

    private void TriggerTrinity(Entity target)
    {
        int level = GetLevel(DariusEquipmentStarIds.TrinityForce);
        if (level <= 0 || Time.time > _trinityArmedUntil || Time.time < _trinityReadyAt || target == null) return;
        _trinityArmedUntil = 0f;
        _trinityReadyAt = Time.time + 1.5f;
        float[] ratios = { 1.30f, 1.50f, 1.70f, 1.95f };
        float damage = _hero.Status.finalStats.attackDamage * ratios[Mathf.Clamp(level, 1, ratios.Length) - 1];
        DariusMemoryEffectBridge.DealPhysical(_hero, _hero, null, damage, 0.35f, target, "Trinity Force Spellblade");
        DariusLog.Info("ITEM-TRINITY", "Spellblade consumed target=" + DariusLog.EntityLabel(target) + " damage=" + damage.ToString("0.##"));
        NotifyEquipmentKillIfNeeded(target, "Trinity Force");
    }
}