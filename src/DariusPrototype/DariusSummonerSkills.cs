using System;
using System.Reflection;
using UnityEngine;

// Darius replaces the stock dodge/movement slot with two League-style summoner spells.
// Their economy is anchored to Vesper's native movement Memory at runtime so balance changes in
// Shape of Dreams automatically carry over instead of hard-coding a separate cooldown model.
public static class DariusSummonerBalance
{
    public const float FlashCooldown = 3.0f;
    public static float Cooldown { get; private set; } = 6f;
    public static int MaxCharges { get; private set; } = 1;
    public static int AddedCharges { get; private set; } = 1;
    public static float MinimumDelay { get; private set; } = 0.05f;
    public static float FlashDistance { get; private set; } = 4.0f;
    public static float GhostDuration { get; private set; } = 3.0f;
    public static float GhostBonusPercentFallback { get; private set; } = 40f;
    public static string NativeSourceName { get; private set; } = "fallback";

    public static void InitializeFromNativeMovement(SkillTrigger nativeMovement)
    {
        try
        {
            if (nativeMovement == null || nativeMovement.configs == null || nativeMovement.configs.Length == 0 || nativeMovement.configs[0] == null)
            {
                DariusLog.Warn("SUMMONER-BALANCE", "Native Vesper movement skill unavailable; using conservative fallback values.");
                return;
            }

            TriggerConfig cfg = nativeMovement.configs[0];
            NativeSourceName = nativeMovement.name;
            Cooldown = Mathf.Max(0.1f, DariusTriggerConfigRuntimeEditor.GetCooldownTime(cfg, Cooldown));
            MaxCharges = Mathf.Max(1, DariusTriggerConfigRuntimeEditor.GetMaxCharges(cfg, MaxCharges));
            AddedCharges = Mathf.Max(1, DariusTriggerConfigRuntimeEditor.GetAddedCharges(cfg, AddedCharges));
            MinimumDelay = Mathf.Max(0.01f, DariusTriggerConfigRuntimeEditor.GetMinimumDelay(cfg, MinimumDelay));

            float range = 0f;
            try
            {
                if (cfg.castMethod != null)
                    range = Mathf.Max(Mathf.Abs(cfg.castMethod._radius), Mathf.Abs(cfg.castMethod._range));
            }
            catch { }

            // Some movement skills keep dash distance outside CastMethod. Probe numeric members with
            // movement/range/distance names and only accept sane world-space values.
            range = Mathf.Max(range, ProbeDistance(nativeMovement));
            range = Mathf.Max(range, ProbeDistance(cfg));
            if (cfg.spawnedInstance != null) range = Mathf.Max(range, ProbeDistance(cfg.spawnedInstance));
            if (cfg.appliedStatusEffect != null) range = Mathf.Max(range, ProbeDistance(cfg.appliedStatusEffect));
            if (range >= 1.0f && range <= 12.0f) FlashDistance = range;

            // Ghost spreads roughly one native-dodge worth of extra travel across a short window.
            // This keeps its mobility budget tied to the stock dodge without turning it into another dash.
            GhostDuration = Mathf.Clamp(Cooldown * 0.45f, 2.5f, 5.0f);
            GhostBonusPercentFallback = Mathf.Clamp(FlashDistance * 10f, 25f, 65f);

            DariusLog.Info("SUMMONER-BALANCE", "Inherited movement template=" + NativeSourceName +
                " ghostCooldown=" + Cooldown.ToString("0.###") + " flashCooldown=" + FlashCooldown.ToString("0.###") + " maxCharges=" + MaxCharges +
                " addedCharges=" + AddedCharges + " minDelay=" + MinimumDelay.ToString("0.###") +
                " flashDistance=" + FlashDistance.ToString("0.###") + " ghostDuration=" + GhostDuration.ToString("0.###") +
                " ghostFallbackBonus=" + GhostBonusPercentFallback.ToString("0.#") + "%");
        }
        catch (Exception e)
        {
            DariusLog.Exception("SUMMONER-BALANCE", e, "Failed reading native movement values; fallback retained");
        }
    }

    public static TriggerConfig CreateFlashConfig(AbilityTrigger parent)
    {
        return CreateConfig(parent, FlashCooldown);
    }

    public static TriggerConfig CreateGhostConfig(AbilityTrigger parent)
    {
        return CreateConfig(parent, Cooldown);
    }

    private static TriggerConfig CreateConfig(AbilityTrigger parent, float cooldown)
    {
        TriggerConfig cfg = DariusTriggerConfigRuntimeEditor.CreateBase(parent, cooldown);
        DariusTriggerConfigRuntimeEditor.SetMaxCharges(cfg, MaxCharges);
        DariusTriggerConfigRuntimeEditor.SetAddedCharges(cfg, AddedCharges);
        DariusTriggerConfigRuntimeEditor.SetMinimumDelay(cfg, MinimumDelay);
        cfg.startCharges = MaxCharges;
        cfg.spawnedInstance = null;
        cfg.appliedStatusEffect = null;
        cfg.isActive = true;
        cfg.canReceiveCooldownReduction = true;
        cfg.alwaysCastImmediately = true;
        cfg.castByMoveDirectionByDefault = true;
        cfg.castByMoveDirectionGamepad = true;
        cfg.faceForward = false;
        cfg.castMethod = cfg.castMethod ?? new CastMethodData();
        cfg.castMethod.type = CastMethodType.None;
        cfg.castMethod._range = FlashDistance;
        cfg.castMethod._radius = FlashDistance;
        return cfg;
    }

    public static string BuildFlashDescription()
    {
        if (DariusLanguage.IsEnglish)
            return "Blink <color=yellow>" + FlashDistance.ToString("0.##") + " m</color> in the movement or aim direction, passing through enemies and other units. " +
                   "Base cooldown is <color=yellow>" + FlashCooldown.ToString("0.##") + " seconds</color>; charges follow the native Shape of Dreams dodge system, up to <color=yellow>" + MaxCharges + "</color>.";
        return "朝移动/指向方向瞬间闪烁<color=yellow>" + FlashDistance.ToString("0.##") + "米</color>，可以直接越过敌人与其他单位。" +
               "基础冷却固定为<color=yellow>" + FlashCooldown.ToString("0.##") + "秒</color>；充能数继承《梦之形》原版闪避，最多<color=yellow>" +
               MaxCharges + "</color>次充能。";
    }

    public static string BuildGhostDescription(Hero hero = null)
    {
        float bonus = hero != null ? GetGhostBonusPercent(hero) : GhostBonusPercentFallback;
        if (DariusLanguage.IsEnglish)
            return "Enter Ghost for <color=yellow>" + GhostDuration.ToString("0.##") + " seconds</color>, currently granting about <color=yellow>" + bonus.ToString("0.#") + "%</color> Move Speed. " +
                   "The bonus is derived from the native dodge distance budget. Cooldown and charges follow the native dodge: <color=yellow>" + Cooldown.ToString("0.##") + " seconds</color>, up to <color=yellow>" + MaxCharges + "</color>.";
        return "进入疾跑状态<color=yellow>" + GhostDuration.ToString("0.##") + "秒</color>，当前额外移动速度约<color=yellow>" +
               bonus.ToString("0.#") + "%</color>。疾跑的额外移动距离预算按原版闪避距离实时换算；冷却与充能直接继承原版闪避：<color=yellow>" +
               Cooldown.ToString("0.##") + "秒</color>，最多<color=yellow>" + MaxCharges + "</color>次充能。";
    }

    public static float GetGhostBonusPercent(Hero hero)
    {
        float speed = ProbeMovementSpeed(hero);
        if (speed <= 0.05f) return GhostBonusPercentFallback;
        float extraMetersPerSecond = FlashDistance / Mathf.Max(0.1f, GhostDuration);
        return Mathf.Clamp(extraMetersPerSecond / speed * 100f, 20f, 80f);
    }

    private static float ProbeMovementSpeed(Hero hero)
    {
        if (hero == null || hero.Status == null) return 0f;
        try
        {
            object finalStats = hero.Status.finalStats;
            if (finalStats == null) return 0f;
            Type t = finalStats.GetType();
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            foreach (string name in new[] { "movementSpeed", "moveSpeed", "baseMovementSpeed" })
            {
                FieldInfo f = t.GetField(name, flags);
                if (f != null)
                {
                    float value = Convert.ToSingle(f.GetValue(finalStats));
                    if (value > 0f) return value;
                }
                PropertyInfo p = t.GetProperty(name, flags);
                if (p != null && p.GetIndexParameters().Length == 0)
                {
                    float value = Convert.ToSingle(p.GetValue(finalStats, null));
                    if (value > 0f) return value;
                }
            }
        }
        catch { }
        return 0f;
    }

    private static float ProbeDistance(object obj)
    {
        if (obj == null) return 0f;
        float best = 0f;
        try
        {
            Type t = obj.GetType();
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            foreach (FieldInfo f in t.GetFields(flags))
            {
                string n = f.Name.ToLowerInvariant();
                if (!(n.Contains("distance") || n.Contains("range") || n.Contains("dash"))) continue;
                if (f.FieldType != typeof(float) && f.FieldType != typeof(double) && f.FieldType != typeof(int)) continue;
                float v = Mathf.Abs(Convert.ToSingle(f.GetValue(obj)));
                if (v >= 1f && v <= 12f) best = Mathf.Max(best, v);
            }
            foreach (PropertyInfo p in t.GetProperties(flags))
            {
                string n = p.Name.ToLowerInvariant();
                if (!(n.Contains("distance") || n.Contains("range") || n.Contains("dash"))) continue;
                if (p.GetIndexParameters().Length != 0 || !p.CanRead) continue;
                if (p.PropertyType != typeof(float) && p.PropertyType != typeof(double) && p.PropertyType != typeof(int)) continue;
                float v = Mathf.Abs(Convert.ToSingle(p.GetValue(obj, null)));
                if (v >= 1f && v <= 12f) best = Mathf.Max(best, v);
            }
        }
        catch { }
        return best;
    }
}
