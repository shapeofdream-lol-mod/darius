using System;
using System.Collections.Generic;
using UnityEngine;
using Mirror;

public sealed partial class DariusHemorrhageRuntime : MonoBehaviour
{
    public void Apply(Entity target, int stacks, string source = null)
    {
        if (!NetworkServer.active)
        {
            DariusLog.DebugInfo("HEM-APPLY", "Skipped on client. source=" + (source ?? "<unknown>"));
            return;
        }
        if (!isEnabled || _owner == null || target == null)
        {
            DariusLog.Warn("HEM-APPLY", "Skipped because disabled/owner/target invalid. enabled=" + isEnabled +
                " owner=" + (_owner != null) + " target=" + (target != null) + " source=" + (source ?? "<unknown>"));
            return;
        }
        if (!_owner.GetRelation(target).HasFlag(EntityRelation.Enemy))
        {
            DariusLog.DebugInfo("HEM-APPLY", "Ignored non-enemy target=" + DariusLog.EntityLabel(target));
            return;
        }

        try
        {
            BleedState state;
            bool created = false;
            if (!_states.TryGetValue(target, out state))
            {
                state = new BleedState { nextTickAt = Time.time + TickInterval };
                _states[target] = state;
                created = true;
            }

            int before = state.stacks;
            bool mightBefore = hasNoxianMight;
            int warFervorLevel = DariusConstellationRuntime.GetWarFervorLevel(_owner);
            bool warFervor = warFervorLevel > 0;
            int maxStacks = currentMaxStacks;
            state.stacks = mightBefore ? maxStacks : Mathf.Clamp(state.stacks + stacks, 1, maxStacks);
            state.expiresAt = Time.time + Duration + DariusConstellationRuntime.GetHemorrhageDurationBonus(_owner);

            DariusLog.Info("HEM-APPLY", "target=" + DariusLog.EntityLabel(target) +
                " source=" + (source ?? "<unknown>") + " created=" + created +
                " stacks=" + before + "->" + state.stacks + "/" + maxStacks + " requested=" + stacks +
                " mightBefore=" + mightBefore + " warFervor=" + warFervor + " identityLevel=" + identityMemoryLevel +
                " expiresAt=" + state.expiresAt.ToString("0.000"));

            try
            {
                DariusPrototypeVfx.CreateBleedStack(_owner, target.transform, state.stacks);
                if (state.stackVfx != null) UnityEngine.Object.Destroy(state.stackVfx);
                if (state.stacks < maxStacks && state.fiveStackVfx != null)
                {
                    UnityEngine.Object.Destroy(state.fiveStackVfx);
                    state.fiveStackVfx = null;
                }
                state.stackVfx = state.stacks < maxStacks ? DariusPrototypeVfx.CreateBleedStackMarker(_owner, target.transform, state.stacks) : null;
                DariusLog.DebugInfo("HEM-VFX", "Persistent bleed marker refreshed target=" + target.name +
                    " stacks=" + state.stacks + "/" + maxStacks);
            }
            catch (Exception e) { DariusLog.Exception("HEM-VFX", e, "Bleed stack VFX failed"); }

            if (state.stacks >= maxStacks)
            {
                if (state.fiveStackVfx == null)
                {
                    try { state.fiveStackVfx = DariusPrototypeVfx.CreateBleedFiveMark(_owner, target.transform); }
                    catch (Exception e) { DariusLog.Exception("HEM-VFX", e, "Five-stack mark VFX failed"); }
                }
            }

            if (!mightBefore && warFervor)
                RecordWarFervorApplications(Mathf.Max(1, stacks), source ?? "unknown", warFervorLevel);
            else if (!warFervor && state.stacks >= maxStacks)
                GrantNoxianMight("target reached current cap " + maxStacks + " stacks via " + (source ?? "unknown"));
        }
        catch (Exception e)
        {
            DariusLog.Exception("HEM-APPLY", e, "Apply failed source=" + (source ?? "<unknown>"));
        }
    }

    public int GetStacks(Entity target)
    {
        BleedState state;
        if (target != null && _states.TryGetValue(target, out state) && Time.time < state.expiresAt)
            return state.stacks;
        return 0;
    }

    public int GetActiveBleedingTargetCount()
    {
        int count = 0;
        float now = Time.time;
        foreach (KeyValuePair<Entity, BleedState> pair in _states)
        {
            Entity target = pair.Key; BleedState state = pair.Value;
            if (target == null || state == null || state.stacks <= 0 || now >= state.expiresAt) continue;
            if (target.IsNullInactiveDeadOrKnockedOut()) continue;
            count++;
        }
        return count;
    }

    private void Update()
    {
        if (!NetworkServer.active || _owner == null) return;

        _remove.Clear();
        foreach (KeyValuePair<Entity, BleedState> pair in _states)
        {
            Entity target = pair.Key;
            BleedState state = pair.Value;
            if (target == null || target.IsNullInactiveDeadOrKnockedOut() || Time.time >= state.expiresAt)
            {
                DariusLog.Info("HEM-EXPIRE", "Removing bleed target=" + DariusLog.EntityLabel(target) +
                    " reason=" + (target == null ? "null" : (target.IsNullInactiveDeadOrKnockedOut() ? "dead/inactive" : "duration")) +
                    " stacks=" + state.stacks);
                _remove.Add(target);
                continue;
            }

            if (Time.time >= state.nextTickAt)
            {
                state.nextTickAt += TickInterval;
                float ad = _owner.Status.finalStats.attackDamage;
                int memoryLevel = identityMemoryLevel;
                float scaledAdRatioPerStack = AdRatioPerStackAtLevel(memoryLevel);
                float damage = ad * scaledAdRatioPerStack * state.stacks;
                float hpBefore = target.currentHealth;
                try
                {
                    DariusMemoryEffectBridge.DealPhysical(_owner, _identitySourceTrigger, damage, 0f, target, "Hemorrhage tick");
                    DariusEquipmentRuntime.NotifyPhysicalSkillHit(_owner, target, "Hemorrhage tick", false);
                    DariusLog.Info("HEM-TICK", "target=" + DariusLog.EntityLabel(target) + " stacks=" + state.stacks +
                        " AD=" + ad.ToString("0.##") + " memoryLevel=" + memoryLevel +
                        " ratioPerStack=" + scaledAdRatioPerStack.ToString("0.###") + " damage=" + damage.ToString("0.##") +
                        " hpBefore=" + hpBefore.ToString("0.##") + " hpAfter=" + target.currentHealth.ToString("0.##"));
                    if (target.IsNullInactiveDeadOrKnockedOut() || target.currentHealth <= 0f)
                        DariusConstellationRuntime.NotifyKill(_owner, target, "Hemorrhage tick");
                }
                catch (Exception e)
                {
                    DariusLog.Exception("HEM-TICK", e, "Damage tick failed target=" + target.name);
                }
                try { DariusPrototypeVfx.CreateBleedTick(_owner, target.transform.position, state.stacks); }
                catch (Exception e) { DariusLog.Exception("HEM-VFX", e, "Bleed tick VFX failed"); }
            }
        }
        for (int i = 0; i < _remove.Count; i++)
        {
            BleedState state;
            if (_states.TryGetValue(_remove[i], out state) && state != null)
            {
                if (state.stackVfx != null) try { UnityEngine.Object.Destroy(state.stackVfx); } catch { }
                if (state.fiveStackVfx != null) try { UnityEngine.Object.Destroy(state.fiveStackVfx); } catch { }
            }
            _states.Remove(_remove[i]);
        }

        if (_noxianMightBonus != null && Time.time >= _noxianMightUntil)
            RemoveNoxianMight();
    }
}