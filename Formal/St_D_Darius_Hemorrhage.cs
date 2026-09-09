using System;
using UnityEngine;

// Hero_Darius Identity Memory: Hemorrhage.
// This is intentionally a SkillTrigger instead of a Gem so it occupies the native Hero
// Identity slot. It no longer copies St_D_Resolve; the slot is assigned directly by the
// HeroSkillLocation contract and the runtime is enabled only while this Identity is owned.
public sealed class St_D_Darius_Hemorrhage : SkillTrigger
{
    private Hero _boundOwner;

    protected override void OnPrepare()
    {
        try
        {
            base.OnPrepare();
            if (configs == null || configs.Length < 2 || configs[0] == null || configs[1] == null)
            {
                configs = new TriggerConfig[]
                {
                    DariusTriggerConfigRuntimeEditor.CreateBase(this, 9999f),
                    DariusTriggerConfigRuntimeEditor.CreateBase(this, 9999f)
                };
            }

            DariusTriggerConfigRuntimeEditor.AttachAll(this);
            for (int i = 0; i < configs.Length; i++)
            {
                TriggerConfig cfg = configs[i];
                if (cfg == null) continue;
                cfg.triggerIcon = DariusPrototypeIcons.Get("H");
                cfg.spawnedInstance = null;
                cfg.isActive = false;
                cfg.canReceiveCooldownReduction = false;
                cfg.alwaysCastImmediately = false;
                cfg.castMethod = cfg.castMethod ?? new CastMethodData();
                cfg.castMethod.type = CastMethodType.None;
                DariusTriggerConfigRuntimeEditor.SetManaCost(cfg, 0f);
                DariusTriggerConfigRuntimeEditor.SetMaxCharges(cfg, 1);
                DariusTriggerConfigRuntimeEditor.SetAddedCharges(cfg, 1);
                DariusTriggerConfigRuntimeEditor.SetCooldownTime(cfg, 9999f);
            }

            SyncIdentityOwner();
            DariusLog.Info("HEM-IDENTITY", "OnPrepare complete owner=" + DariusLog.EntityLabel(owner));
        }
        catch (Exception e)
        {
            DariusLog.Exception("HEM-IDENTITY", e, "OnPrepare failed");
        }
    }

    protected override void OnLevelChange(int oldLevel, int newLevel)
    {
        if (newLevel < 1) return;
        if (owner == null)
        {
            ClientSkillEvent_OnLevelChange?.Invoke(oldLevel, newLevel);
            DariusMemoryScaling.LogLevelChange(this, oldLevel, newLevel, 0f);
            return;
        }
        base.OnLevelChange(oldLevel, newLevel);
        DariusMemoryScaling.LogLevelChange(this, oldLevel, newLevel, 0f);
        SyncIdentityLevel(newLevel, "level changed");
    }

    private void Update()
    {
        SyncIdentityOwner();
    }

    protected override void OnDisable()
    {
        UnbindIdentityOwner("OnDisable");
        base.OnDisable();
    }

    protected override void OnDestroy()
    {
        UnbindIdentityOwner("OnDestroy");
        base.OnDestroy();
    }

    private void SyncIdentityOwner()
    {
        Hero current = owner as Hero;
        if (current == _boundOwner) return;

        if (_boundOwner != null)
            SetIdentityState(_boundOwner, false, 1, "owner changed");

        _boundOwner = current;
        if (_boundOwner != null)
            SetIdentityState(_boundOwner, true, DariusMemoryScaling.GetLevel(this), "identity owned");
    }

    private void UnbindIdentityOwner(string reason)
    {
        if (_boundOwner == null) return;
        Hero old = _boundOwner;
        _boundOwner = null;
        SetIdentityState(old, false, 1, reason);
    }

    private void SyncIdentityLevel(int level, string reason)
    {
        Hero hero = _boundOwner != null ? _boundOwner : owner as Hero;
        if (hero == null) return;
        SetIdentityState(hero, true, DariusMemoryScaling.NormalizeLevel(level), reason);
    }

    private void SetIdentityState(Hero hero, bool equipped, int memoryLevel, string reason)
    {
        if (hero == null) return;
        try
        {
            DariusHemorrhageRuntime runtime = hero.GetComponent<DariusHemorrhageRuntime>();
            if (runtime == null && equipped)
                runtime = hero.gameObject.AddComponent<DariusHemorrhageRuntime>();
            if (runtime == null) return;

            runtime.SetIdentityEquipped(hero, equipped, memoryLevel, equipped ? this : null);
            DariusLog.Info("HEM-IDENTITY", (equipped ? "Enabled" : "Disabled") +
                " on " + DariusLog.EntityLabel(hero) + " reason=" + reason +
                " memoryLevel=" + runtime.identityMemoryLevel + " sourceCount=" + runtime.sourceCount);
        }
        catch (Exception e)
        {
            DariusLog.Exception("HEM-IDENTITY", e, "Failed to change identity state equipped=" + equipped);
        }
    }
}
