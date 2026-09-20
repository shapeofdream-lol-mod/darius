using System;
using UnityEngine;

public sealed class St_Darius_CripplingStrike : SkillTrigger
{
    protected override void OnPrepare()
    {
        try
        {
            base.OnPrepare();
            if (configs == null || configs.Length < 2 || configs[0] == null || configs[1] == null)
                configs = new TriggerConfig[]
                {
                    DariusTriggerConfigRuntimeEditor.CreateBase(this, 5.0f),
                    DariusTriggerConfigRuntimeEditor.CreateBase(this, 5.0f)
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
                    DariusTriggerConfigRuntimeEditor.SetCooldownTime(configs[i], 5.0f);
                    DariusTriggerConfigRuntimeEditor.SetMinimumDelay(configs[i], 0.05f);
                }
            }

            for (int i = 0; i < configs.Length; i++)
            {
                if (configs[i] != null) configs[i].spawnedInstance = DariusFormalRegistry.CripplingStrikeAbility;
            }
            TriggerConfig cfg = configs[0];
            if (cfg.triggerIcon == null) cfg.triggerIcon = DariusPrototypeIcons.Get("W");
            cfg.isActive = true;
            cfg.canReceiveCooldownReduction = true;
            cfg.alwaysCastImmediately = true;
            cfg.castMethod = cfg.castMethod ?? new CastMethodData();
            cfg.castMethod.type = CastMethodType.None;
            cfg.castMethod._radius = 0f;
            DariusLog.Info("W-CONFIG", "OnPrepare configured cooldown=5 armDuration=4 icon=" + (cfg.triggerIcon != null));
        }
        catch (Exception e)
        {
            DariusLog.Exception("W-CONFIG", e, "OnPrepare failed");
            throw;
        }
    }

    public void RefundLegacyCooldown(float seconds, string reason)
    {
        if (!Mirror.NetworkServer.active || seconds <= 0f) return;
        try
        {
            int charge = currentConfigCurrentCharge;
            float before = currentConfigUnscaledCooldownTime;
            if (charge > 0 || before <= 0f) return;
            float after = Mathf.Max(0f, before - seconds);
            SetCooldownTimeAll(after, false);
            DariusLog.Info("STAR-LEGACY-W", "Cooldown refund " + before.ToString("0.##") + "->" + after.ToString("0.##") + " reason=" + reason);
        }
        catch (Exception e) { DariusLog.Exception("STAR-LEGACY-W", e, "Cooldown refund failed"); }
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
            DariusMemoryScaling.LogLevelChange(this, oldLevel, newLevel, 5.0f);
            return;
        }
        base.OnLevelChange(oldLevel, newLevel);
        DariusMemoryScaling.LogLevelChange(this, oldLevel, newLevel, 5.0f);
    }

    public override AbilityInstance OnCastComplete(int configIndex, CastInfo info)
    {
        AbilityInstance result = null;
        try
        {
            result = base.OnCastComplete(configIndex, info);
            if (result != null)
            {
                DariusLog.Info("W-TRIGGER", "Native AbilityInstance spawned=" + result.name +
                    " configIndex=" + configIndex + " caster=" + DariusLog.EntityLabel(info.caster));
                return result;
            }
            DariusLog.Warn("W-TRIGGER", "Native completion returned no AbilityInstance; using compatibility fallback execution.");
        }
        catch (Exception e)
        {
            DariusLog.Exception("W-TRIGGER", e, "Native AbilityInstance pipeline failed; using one-shot compatibility fallback");
        }

        // Compatibility safety net only. Normal gameplay must use the stock Trigger -> Instance ->
        // PrepareAndSpawn path so Gems receive a real EventInfoCast.instance and Actor damage/heal events.
        try
        {
            StartCoroutine(Ai_Darius_CripplingStrike.Execute(info, this, null));
            DariusLog.Warn("W-FALLBACK", "Direct execution started because native AbilityInstance creation failed.");
        }
        catch (Exception e)
        {
            DariusLog.Exception("W-FALLBACK", e, "Compatibility fallback failed to start");
        }
        return result;
    }
}
