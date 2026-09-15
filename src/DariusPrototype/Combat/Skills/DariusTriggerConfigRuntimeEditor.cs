using System;
using System.Reflection;

// Current Shape of Dreams builds keep several TriggerConfig values in private backing fields.
// This helper mirrors the runtime-edit pattern used by current 2026 community mods so we do
// not depend on old public-field layouts.
public static class DariusTriggerConfigRuntimeEditor
{
    private static readonly FieldInfo ParentField = typeof(TriggerConfig).GetField("_parent", BindingFlags.NonPublic | BindingFlags.Instance);
    private static readonly FieldInfo ManaCostField = typeof(TriggerConfig).GetField("_manaCost", BindingFlags.NonPublic | BindingFlags.Instance);
    private static readonly FieldInfo MaxChargesField = typeof(TriggerConfig).GetField("_maxCharges", BindingFlags.NonPublic | BindingFlags.Instance);
    private static readonly FieldInfo AddedChargesField = typeof(TriggerConfig).GetField("_addedCharges", BindingFlags.NonPublic | BindingFlags.Instance);
    private static readonly FieldInfo CooldownTimeField = typeof(TriggerConfig).GetField("_cooldownTime", BindingFlags.NonPublic | BindingFlags.Instance);
    private static readonly FieldInfo MinimumDelayField = typeof(TriggerConfig).GetField("_minimumDelay", BindingFlags.NonPublic | BindingFlags.Instance);
    private static readonly FieldInfo IsConfigDirtyField = typeof(AbilityTrigger).GetField("_isConfigDirty", BindingFlags.NonPublic | BindingFlags.Instance);

    public static TriggerConfig CreateBase(AbilityTrigger parent, float cooldown)
    {
        TriggerConfig config = new TriggerConfig
        {
            isActive = true,
            startCharges = 1,
            spawnedInstance = null,
            appliedStatusEffect = null,
            destroyExistingEffect = false,
            startAnim = null,
            endAnim = null,
            castVoice = null,
            effectOnCast = null,
            victim = TriggerConfig.StatusEffectVictimType.Target,
            channel = new TriggerChannelData { duration = 0.05f },
            postDelay = 0.05f,
            selfValidator = AbilitySelfValidator.Default,
            ignoreBlock = false,
            ignoreAbilityLock = false,
            faceForward = true,
            overrideRotation = false,
            canReceiveCooldownReduction = true,
            postponeBasicCommand = false,
            moveTowardsCastDirection = false,
            canConsumeCastBonus = false,
            alwaysCastImmediately = false,
            castByMoveDirectionByDefault = false,
            castByMoveDirectionGamepad = false,
            ignoreAimDirectionGamepad = false,
            unstoppableWhileCasting = false,
            targetValidator = new AbilityTargetValidator(),
            castMethod = new CastMethodData(),
            predictionSettings = new AbilityTrigger.PredictionSettings
            {
                type = AbilityTrigger.PredictionSettings.ModelType.None
            }
        };

        Attach(parent, config);
        SetManaCost(config, 0f);
        SetMaxCharges(config, 1);
        SetAddedCharges(config, 1);
        SetCooldownTime(config, cooldown);
        SetMinimumDelay(config, 0.05f);
        return config;
    }

    public static void Attach(AbilityTrigger parent, TriggerConfig config)
    {
        try
        {
            if (config != null) ParentField?.SetValue(config, parent);
        }
        catch (Exception e)
        {
            DariusLog.Exception("CONFIG", e, "Attach TriggerConfig parent failed");
        }
    }

    public static void AttachAll(AbilityTrigger parent)
    {
        if (parent == null || parent.configs == null) return;
        for (int i = 0; i < parent.configs.Length; i++) Attach(parent, parent.configs[i]);
    }

    public static void SetManaCost(TriggerConfig config, float value) => SetField(config, ManaCostField, value, "manaCost");
    public static void SetMaxCharges(TriggerConfig config, int value) => SetField(config, MaxChargesField, value, "maxCharges");
    public static void SetAddedCharges(TriggerConfig config, int value) => SetField(config, AddedChargesField, value, "addedCharges");
    public static void SetCooldownTime(TriggerConfig config, float value) => SetField(config, CooldownTimeField, value, "cooldownTime");
    public static void SetMinimumDelay(TriggerConfig config, float value) => SetField(config, MinimumDelayField, value, "minimumDelay");

    public static float GetCooldownTime(TriggerConfig config, float fallback = 6f) => GetField(config, CooldownTimeField, fallback);
    public static int GetMaxCharges(TriggerConfig config, int fallback = 1) => GetField(config, MaxChargesField, fallback);
    public static int GetAddedCharges(TriggerConfig config, int fallback = 1) => GetField(config, AddedChargesField, fallback);
    public static float GetMinimumDelay(TriggerConfig config, float fallback = 0.05f) => GetField(config, MinimumDelayField, fallback);

    private static T GetField<T>(TriggerConfig config, FieldInfo field, T fallback)
    {
        if (config == null || field == null) return fallback;
        try
        {
            object value = field.GetValue(config);
            if (value is T typed) return typed;
            if (value != null) return (T)Convert.ChangeType(value, typeof(T));
        }
        catch { }
        return fallback;
    }

    private static void SetField(TriggerConfig config, FieldInfo field, object value, string label)
    {
        if (config == null) return;
        if (field == null)
        {
            DariusLog.Warn("CONFIG", "Backing field not found for " + label + "; value could not be applied.");
            return;
        }

        try
        {
            field.SetValue(config, value);
            MarkDirty(config);
        }
        catch (Exception e)
        {
            DariusLog.Exception("CONFIG", e, "Failed setting " + label + "=" + value);
        }
    }

    private static void MarkDirty(TriggerConfig config)
    {
        try
        {
            AbilityTrigger parent = ParentField?.GetValue(config) as AbilityTrigger;
            if (parent != null) IsConfigDirtyField?.SetValue(parent, true);
        }
        catch (Exception e)
        {
            DariusLog.Exception("CONFIG", e, "MarkDirty failed");
        }
    }
}
