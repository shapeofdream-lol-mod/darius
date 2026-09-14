using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Mirror;
using UnityEngine;

public sealed partial class St_Darius_NoxianGuillotine : SkillTrigger
{
    // The gameplay execution still has one target, like League Darius R. The SoD native
    // CastMethodType.Target acquisition proved too brittle for a runtime-created Hero skill:
    // if the pointer was not directly on an enemy collider, the input could be rejected before
    // OnCastComplete and there was no Darius-side log at all. Use a point cast as the input layer,
    // then resolve the intended enemy in a tightly bounded aim-assist pass.
    public const float CastRange = 6.0f;

    public const float MaxResolvedCenterRange = 6.75f;

    public const float AimPointAssistRadius = 3.0f;

    public const float AimDirectionAssistAngle = 80.0f;

    public const float InputBufferWindow = 0.40f;

    public const float InputBufferPollInterval = 0.025f;

    public const float QueuedIntentWindow = 0.60f;

    public const float ReadyWatchdogDelay = 0.18f;

    public const float ExecutionWatchdogDelay = 1.20f;

    public const float ControlAttemptWatchdogDelay = 0.20f;

    private bool _bufferActive;

    private int _bufferToken;

    private int _bufferConfigIndex;

    private CastInfo _bufferInfo;

    private float _bufferUntil;

    private bool _executionActive;

    private int _executionToken;

    private bool _queuedDuringExecution;

    private int _queuedConfigIndex;

    private CastInfo _queuedInfo;

    private float _queuedUntil;

    private int _readyWatchdogToken;

    private int _controlAttemptToken;

    private int _lastControlAttemptFrame = -1;

    private int _lastControlRejectFrame = -1;

    private float _lastOnCastCompleteAt = -999f;

    private string _lastHardResetRoomToken;

    protected override void OnPrepare()
    {
        try
        {
            base.OnPrepare();
            if (configs == null || configs.Length < 2 || configs[0] == null || configs[1] == null)
                configs = new TriggerConfig[]
                {
                    DariusTriggerConfigRuntimeEditor.CreateBase(this, 24.0f),
                    DariusTriggerConfigRuntimeEditor.CreateBase(this, 24.0f)
                };
            else
            {
                DariusTriggerConfigRuntimeEditor.AttachAll(this);
                for (int i = 0; i < configs.Length; i++)
                {
                    if (configs[i] == null) continue;
                    DariusTriggerConfigRuntimeEditor.SetManaCost(configs[i], 0f);
                    DariusTriggerConfigRuntimeEditor.SetMaxCharges(configs[i], 1);
                    DariusTriggerConfigRuntimeEditor.SetAddedCharges(configs[i], 1);
                    configs[i].startCharges = 1;
                    // For R, cooldownTime is the recharge duration of the single ultimate charge.
                    // Availability is governed by currentCharges, not by a second ordinary-CD gate.
                    DariusTriggerConfigRuntimeEditor.SetCooldownTime(configs[i], 24.0f);
                    DariusTriggerConfigRuntimeEditor.SetMinimumDelay(configs[i], 0.02f);
                }
            }

            for (int i = 0; i < configs.Length; i++)
            {
                TriggerConfig cfg = configs[i];
                if (cfg == null) continue;
                // Standard SkillTrigger -> AbilityInstance -> PrepareAndSpawn is the authoritative path.
                // This gives Gems a real EventInfoCast.instance and preserves parentActor/firstTrigger.
                cfg.spawnedInstance = DariusFormalRegistry.NoxianGuillotineAbility;
                // R owns every visual/audio beat itself. Explicitly sever inherited template
                // presentation every time OnPrepare runs so no stock impact/cast sound can leak in.
                cfg.startAnim = null;
                cfg.endAnim = null;
                cfg.castVoice = null;
                cfg.effectOnCast = null;
                cfg.appliedStatusEffect = null;
                cfg.destroyExistingEffect = false;
                if (cfg.triggerIcon == null) cfg.triggerIcon = DariusPrototypeIcons.Get("R");
                cfg.isActive = true;
                cfg.canReceiveCooldownReduction = true;
                cfg.faceForward = true;
                // Runtime-created point casts can leave a remote client in the aim-confirm layer
                // without ever submitting the command to the host. R needs no second click: submit
                // the current facing/point immediately and let the server resolve the legal target.
                cfg.alwaysCastImmediately = true;
                cfg.castMethod = cfg.castMethod ?? new CastMethodData();

                // v0.18.5: collect an aim point first, then resolve the enemy ourselves. Native Target
                // mode could eat the button press before this trigger was reached at all.
                cfg.castMethod.type = CastMethodType.Point;
                cfg.castMethod._range = CastRange;
                cfg.castMethod._radius = AimPointAssistRadius;
                cfg.castMethod._isClamping = true;
                // Point input is only an aiming gesture. Native target validation can reject an
                // empty cursor point before Darius' own target resolver runs, which made R appear
                // to do nothing. The execute validates and resolves a living enemy itself.
                cfg.targetValidator = null;
            }

            Hero preparedCaster = owner as Hero;
            bool awooActive = preparedCaster != null && DariusEquipmentRuntime.Get(preparedCaster, false) != null &&
                DariusEquipmentRuntime.Get(preparedCaster, false).GetLevel(DariusEquipmentStarIds.Awoo) > 0;
            ApplyAwooCooldownOverride(awooActive);

            DariusLog.Info("R-CONFIG", "OnPrepare configured point-aim execute cooldown=" + (awooActive ? "12" : "24") + " castRange=" + CastRange +
                " assistRadius=" + AimPointAssistRadius + " assistAngle=" + AimDirectionAssistAngle +
                " spawnedInstance=" + (DariusFormalRegistry.NoxianGuillotineAbility != null ? DariusFormalRegistry.NoxianGuillotineAbility.name : "<null>") +
                " icon=" + (configs.Length > 0 && configs[0] != null && configs[0].triggerIcon != null));
        }
        catch (Exception e)
        {
            DariusLog.Exception("R-CONFIG", e, "OnPrepare failed");
            throw;
        }
    }

    protected override void OnLevelChange(int oldLevel, int newLevel)
    {
        // Current 1.3.x can set level before the spawned SkillTrigger has an owner.
        // Calling the vanilla implementation in that transient state dereferences null.
        if (newLevel < 1) return;
        if (owner == null)
        {
            DariusLog.DebugInfo("LEVEL", name + " deferred base OnLevelChange old=" + oldLevel + " new=" + newLevel + " owner=<null>");
            ClientSkillEvent_OnLevelChange?.Invoke(oldLevel, newLevel);
            DariusMemoryScaling.LogLevelChange(this, oldLevel, newLevel, 24.0f);
            return;
        }
        base.OnLevelChange(oldLevel, newLevel);
        DariusMemoryScaling.LogLevelChange(this, oldLevel, newLevel, 24.0f);
    }
}