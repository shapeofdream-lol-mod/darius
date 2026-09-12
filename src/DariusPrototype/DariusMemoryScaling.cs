using System;
using System.Reflection;
using UnityEngine;

// Centralizes Darius' infinite in-run Memory growth.
// Shape of Dreams scales each description value independently: damage can grow every level while
// healing/control/range can advance only on milestones. Darius direct-execution skills bypass the
// serialized ScalingValue pipeline, so gameplay and tooltip code call these same helpers directly.
public static class DariusMemoryScaling
{
    // Native Memories commonly add a modest fraction of the base damage coefficient per additional level; this mod uses 20%.
    public const float DefaultDamagePerAdditionalLevel = 0.20f;

    private static MethodInfo _getCooldownTimeOnLevel;
    private static bool _cooldownMethodResolved;

    public static int NormalizeLevel(int level)
    {
        return Mathf.Max(1, level);
    }

    public static int AdditionalLevels(int level)
    {
        return NormalizeLevel(level) - 1;
    }

    public static int StepCount(int level, int additionalLevelsPerStep)
    {
        int interval = Mathf.Max(1, additionalLevelsPerStep);
        return AdditionalLevels(level) / interval;
    }

    // Milestones are anchored to absolute Memory levels: with interval=5 they trigger at
    // Lv5/Lv10/Lv15..., unlike StepCount which means every five *additional* levels.
    public static int MilestoneCount(int level, int levelsPerMilestone)
    {
        int interval = Mathf.Max(1, levelsPerMilestone);
        return NormalizeLevel(level) / interval;
    }

    public static float LinearMultiplier(int level, float baseFractionPerAdditionalLevel = DefaultDamagePerAdditionalLevel)
    {
        return 1f + AdditionalLevels(level) * Mathf.Max(0f, baseFractionPerAdditionalLevel);
    }

    public static float Linear(float baseValue, int level, float baseFractionPerAdditionalLevel = DefaultDamagePerAdditionalLevel)
    {
        return baseValue * LinearMultiplier(level, baseFractionPerAdditionalLevel);
    }

    public static float Stepped(float baseValue, float amountPerStep, int level, int additionalLevelsPerStep)
    {
        return baseValue + StepCount(level, additionalLevelsPerStep) * amountPerStep;
    }

    // Absolute Memory-level milestones: interval=5 means the first extra step happens exactly at
    // Lv5, then Lv10/Lv15/... . This is used for utility/mechanic upgrades that should sit on top
    // of the uninterrupted per-level damage curve rather than replacing it.
    public static float MilestoneStepped(float baseValue, float amountPerMilestone, int level, int levelsPerMilestone)
    {
        return baseValue + MilestoneCount(level, levelsPerMilestone) * amountPerMilestone;
    }

    // Backward-compatible aliases for callers/logs from rc5. They now mean damage-like linear growth,
    // not a universal multiplier that should be applied to every parameter.
    public static float Multiplier(int level) { return LinearMultiplier(level); }
    public static float Scale(float baseValue, int level) { return Linear(baseValue, level); }

    public static int GetLevel(AbilityTrigger trigger)
    {
        SkillTrigger skill = trigger as SkillTrigger;
        if (skill == null) return 1;
        return GetLevel(skill);
    }

    public static int GetLevel(SkillTrigger skill)
    {
        if (skill == null) return 1;
        int value;
        if (TryReadIntMember(skill, "effectiveLevel", out value)) return NormalizeLevel(value);
        if (TryReadIntMember(skill, "level", out value)) return NormalizeLevel(value);
        return 1;
    }

    private static bool TryReadIntMember(object target, string name, out int value)
    {
        value = 0;
        if (target == null) return false;
        try
        {
            Type type = target.GetType();
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            PropertyInfo property = type.GetProperty(name, flags) ?? typeof(SkillTrigger).GetProperty(name, flags);
            object raw = property != null && property.CanRead ? property.GetValue(target, null) : null;
            if (raw == null)
            {
                FieldInfo field = type.GetField(name, flags) ?? typeof(SkillTrigger).GetField(name, flags);
                if (field != null) raw = field.GetValue(target);
            }
            if (raw == null) return false;
            value = Convert.ToInt32(raw);
            return true;
        }
        catch { return false; }
    }

    // Ask the game's own SkillTrigger implementation for the level-specific cooldown so tooltips
    // and actual cooldown bookkeeping share the native Memory-Haste-per-level rule.
    public static float CooldownAtLevel(SkillTrigger skill, float baseCooldown, int level)
    {
        if (skill == null) return baseCooldown;
        try
        {
            if (!_cooldownMethodResolved)
            {
                _cooldownMethodResolved = true;
                _getCooldownTimeOnLevel = typeof(SkillTrigger).GetMethod(
                    "GetCooldownTimeOnLevel",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                    null,
                    new[] { typeof(int) },
                    null);
            }
            if (_getCooldownTimeOnLevel != null)
            {
                object value = _getCooldownTimeOnLevel.Invoke(skill, new object[] { NormalizeLevel(level) });
                if (value is float f && f > 0f && !float.IsNaN(f) && !float.IsInfinity(f)) return f;
                if (value is double d && d > 0d && !double.IsNaN(d) && !double.IsInfinity(d)) return (float)d;
            }
        }
        catch (Exception e)
        {
            DariusLog.DebugInfoThrottled("LEVEL", "cooldown-probe:" + skill.name,
                "GetCooldownTimeOnLevel probe failed for " + skill.name + ": " + e.GetType().Name, 10.0);
        }
        return baseCooldown;
    }

    public static void LogLevelChange(SkillTrigger skill, int oldLevel, int newLevel, float baseCooldown)
    {
        if (skill == null) return;
        int normalized = NormalizeLevel(newLevel);
        string cooldown = baseCooldown > 0f ? CooldownAtLevel(skill, baseCooldown, normalized).ToString("0.###") : "<passive>";
        DariusLog.Info("LEVEL", skill.name + " level " + oldLevel + " -> " + newLevel +
            " effective=" + GetLevel(skill) + " defaultLinear20Scale=" + LinearMultiplier(normalized).ToString("0.###") +
            " nativeCooldown=" + cooldown);
    }
}
