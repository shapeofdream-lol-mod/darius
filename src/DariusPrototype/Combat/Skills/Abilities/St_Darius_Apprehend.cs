using System;
using UnityEngine;

public sealed class St_Darius_Apprehend : SkillTrigger
{
    protected override void OnPrepare()
    {
        try
        {
            base.OnPrepare();
            if (configs == null || configs.Length < 2 || configs[0] == null || configs[1] == null)
                configs = new TriggerConfig[]
                {
                    DariusTriggerConfigRuntimeEditor.CreateBase(this, 10.0f),
                    DariusTriggerConfigRuntimeEditor.CreateBase(this, 10.0f)
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
                    DariusTriggerConfigRuntimeEditor.SetCooldownTime(configs[i], 10.0f);
                    DariusTriggerConfigRuntimeEditor.SetMinimumDelay(configs[i], 0.05f);
                }
            }

            for (int i = 0; i < configs.Length; i++)
            {
                if (configs[i] != null) configs[i].spawnedInstance = DariusFormalRegistry.ApprehendAbility;
            }
            int level = DariusMemoryScaling.GetLevel(this);
            RefreshLevelRange(level);
            TriggerConfig cfg = configs[0];
            DariusLog.Info("E-CONFIG", "OnPrepare configured cooldown=10 range=" +
                Ai_Darius_Apprehend.RangeAtLevel(level).ToString("0.##") + " angle=60 icon=" +
                (cfg != null && cfg.triggerIcon != null));
        }
        catch (Exception e)
        {
            DariusLog.Exception("E-CONFIG", e, "OnPrepare failed");
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
            RefreshLevelRange(newLevel);
            DariusMemoryScaling.LogLevelChange(this, oldLevel, newLevel, 10.0f);
            return;
        }
        base.OnLevelChange(oldLevel, newLevel);
        RefreshLevelRange(newLevel);
        DariusMemoryScaling.LogLevelChange(this, oldLevel, newLevel, 10.0f);
    }

    private void RefreshLevelRange(int level)
    {
        if (configs == null) return;
        float range = Ai_Darius_Apprehend.RangeAtLevel(level);
        for (int i = 0; i < configs.Length; i++)
        {
            TriggerConfig cfg = configs[i];
            if (cfg == null) continue;
            if (cfg.triggerIcon == null) cfg.triggerIcon = DariusPrototypeIcons.Get("E");
            cfg.isActive = true;
            cfg.canReceiveCooldownReduction = true;
            cfg.alwaysCastImmediately = true;
            cfg.faceForward = true;
            cfg.castMethod = cfg.castMethod ?? new CastMethodData();
            cfg.castMethod.type = CastMethodType.Cone;
            cfg.castMethod._radius = range;
            cfg.castMethod._angle = 60.0f;
        }
        DariusLog.DebugInfo("E-RANGE", "Memory level=" + DariusMemoryScaling.NormalizeLevel(level) +
            " cast/physics range synchronized to " + range.ToString("0.##") + "m");
    }

    public override AbilityInstance OnCastComplete(int configIndex, CastInfo info)
    {
        AbilityInstance result = null;
        try
        {
            result = base.OnCastComplete(configIndex, info);
            if (result != null)
            {
                DariusLog.Info("E-TRIGGER", "Native AbilityInstance spawned=" + result.name +
                    " configIndex=" + configIndex + " caster=" + DariusLog.EntityLabel(info.caster));
                return result;
            }
            DariusLog.Warn("E-TRIGGER", "Native completion returned no AbilityInstance; using compatibility fallback execution.");
        }
        catch (Exception e)
        {
            DariusLog.Exception("E-TRIGGER", e, "Native AbilityInstance pipeline failed; using one-shot compatibility fallback");
        }

        // Compatibility safety net only. Normal gameplay must use the stock Trigger -> Instance ->
        // PrepareAndSpawn path so Gems receive a real EventInfoCast.instance and Actor damage/heal events.
        try
        {
            StartCoroutine(Ai_Darius_Apprehend.Execute(info, this, null));
            DariusLog.Warn("E-FALLBACK", "Direct execution started because native AbilityInstance creation failed.");
        }
        catch (Exception e)
        {
            DariusLog.Exception("E-FALLBACK", e, "Compatibility fallback failed to start");
        }
        return result;
    }
}
