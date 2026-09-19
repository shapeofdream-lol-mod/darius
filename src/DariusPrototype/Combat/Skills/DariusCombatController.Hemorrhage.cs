using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;

public sealed partial class DariusCombatController : MonoBehaviour
{
    // ==================== Hemorrhage Essence ====================
    private void ToggleHemorrhage()
    {
        _hemorrhageEnabled = !_hemorrhageEnabled;
        if (!_hemorrhageEnabled)
        {
            _bleeds.Clear();
            RemoveNoxianMight();
        }
        DariusLog.Info("LEGACY", "Hemorrhage Essence simulation toggled: " + (_hemorrhageEnabled ? "ON" : "OFF"));
        DariusPrototypeVfx.CreateEssenceToggle(hero.transform.position, _hemorrhageEnabled);
    }

    private void ApplyHemorrhage(Entity target, int normalStacks)
    {
        if (!_hemorrhageEnabled || !IsValidEnemy(target))
            return;

        int stacksToApply = HasNoxianMight() ? BLEED_MAX_STACKS : normalStacks;
        BleedState state;
        if (!_bleeds.TryGetValue(target, out state))
        {
            state = new BleedState { stacks = 0, nextTickAt = Time.time + BLEED_TICK_INTERVAL };
            _bleeds[target] = state;
        }

        state.stacks = Mathf.Clamp(state.stacks + stacksToApply, 1, BLEED_MAX_STACKS);
        if (HasNoxianMight()) state.stacks = BLEED_MAX_STACKS;
        state.expireAt = Time.time + BLEED_DURATION;

        if (state.stacks >= BLEED_MAX_STACKS)
            GrantNoxianMight();

        DariusPrototypeVfx.CreateBleedStack(target.transform, state.stacks);
    }

    private void UpdateBleeds()
    {
        if (_bleeds.Count == 0 || hero == null)
            return;

        _scratchEntities.Clear();
        foreach (KeyValuePair<Entity, BleedState> pair in _bleeds)
        {
            Entity target = pair.Key;
            BleedState state = pair.Value;
            if (target == null || target.IsNullInactiveDeadOrKnockedOut() || Time.time >= state.expireAt)
            {
                _scratchEntities.Add(target);
                continue;
            }

            if (Time.time >= state.nextTickAt)
            {
                state.nextTickAt += BLEED_TICK_INTERVAL;
                float ad = hero.Status.finalStats.attackDamage;
                DealPhysical(target, ad * BLEED_AD_RATIO_PER_STACK_PER_TICK * state.stacks);
                DariusPrototypeVfx.CreateBleedTick(target.transform.position, state.stacks);
            }
        }

        for (int i = 0; i < _scratchEntities.Count; i++)
            _bleeds.Remove(_scratchEntities[i]);
    }

    private int GetBleedStacks(Entity target)
    {
        BleedState state;
        if (target != null && _bleeds.TryGetValue(target, out state) && Time.time < state.expireAt)
            return state.stacks;
        return 0;
    }

    private void GrantNoxianMight()
    {
        _noxianMightUntil = Time.time + NOXIAN_MIGHT_DURATION;

        if (_noxianMightBonus == null)
        {
            _noxianMightBonus = new StatBonus();
            _noxianMightBonus.attackDamagePercentage = NOXIAN_MIGHT_AD_PERCENT;
            try { hero.Status.AddStatBonus(_noxianMightBonus); } catch { }
        }

        if (_legacyNoxianMightVfx == null)
            _legacyNoxianMightVfx = DariusPrototypeVfx.CreateNoxianMight(hero.transform);
    }

    private bool HasNoxianMight()
    {
        return _hemorrhageEnabled && Time.time < _noxianMightUntil;
    }

    private void UpdateNoxianMight()
    {
        if (_noxianMightBonus != null && Time.time >= _noxianMightUntil)
            RemoveNoxianMight();
    }

    private void RemoveNoxianMight()
    {
        if (_noxianMightBonus != null && hero != null)
        {
            try { hero.Status.RemoveStatBonus(_noxianMightBonus); } catch { }
        }
        if (_legacyNoxianMightVfx != null)
        {
            try { UnityEngine.Object.Destroy(_legacyNoxianMightVfx); } catch { }
            _legacyNoxianMightVfx = null;
        }
        _noxianMightBonus = null;
        _noxianMightUntil = 0f;
    }

    private void ClearLegacyWArmVfx()
    {
        if (_legacyWArmVfx != null)
        {
            try { UnityEngine.Object.Destroy(_legacyWArmVfx); } catch { }
            _legacyWArmVfx = null;
        }
    }
}