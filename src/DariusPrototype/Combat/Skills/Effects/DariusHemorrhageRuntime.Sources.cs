using System;
using System.Collections.Generic;
using UnityEngine;
using Mirror;

public sealed partial class DariusHemorrhageRuntime : MonoBehaviour
{
    private void RefreshSourceState()
    {
        if (isEnabled)
        {
            SubscribeAttackIfNeeded();
            return;
        }

        UnsubscribeAttack();
        int stateCount = _states.Count;
        foreach (KeyValuePair<Entity, BleedState> pair in _states)
        {
            if (pair.Value != null)
            {
                if (pair.Value.stackVfx != null) try { UnityEngine.Object.Destroy(pair.Value.stackVfx); } catch { }
                if (pair.Value.fiveStackVfx != null) try { UnityEngine.Object.Destroy(pair.Value.fiveStackVfx); } catch { }
            }
        }
        _states.Clear();
        _warFervorApplications.Clear();
        RemoveNoxianMight();
        DariusLog.Info("HEM", "No active Identity/legacy source; cleared " + stateCount + " bleed targets and Noxian Might.");
    }

    private void SubscribeAttackIfNeeded()
    {
        if (_attackSubscribed)
        {
            DariusLog.DebugInfo("HEM", "Attack event already subscribed.");
            return;
        }
        if (!NetworkServer.active)
        {
            DariusLog.DebugInfo("HEM", "Not subscribing attack event on non-server instance.");
            return;
        }
        if (_owner == null)
        {
            DariusLog.Warn("HEM", "Cannot subscribe attack event: owner is null.");
            return;
        }

        try
        {
            _owner.ActorEvent_OnAttackHit += OnOwnerAttackHit;
            _attackSubscribed = true;
            DariusLog.Info("HEM", "Subscribed ActorEvent_OnAttackHit on owner=" + DariusLog.EntityLabel(_owner));
        }
        catch (Exception e)
        {
            DariusLog.Exception("HEM", e, "Subscribing attack event failed");
        }
    }

    private void UnsubscribeAttack()
    {
        if (_attackSubscribed && _owner != null)
        {
            try
            {
                _owner.ActorEvent_OnAttackHit -= OnOwnerAttackHit;
                DariusLog.Info("HEM", "Unsubscribed ActorEvent_OnAttackHit from owner=" + _owner.name);
            }
            catch (Exception e) { DariusLog.Exception("HEM", e, "Unsubscribing attack event failed"); }
        }
        _attackSubscribed = false;
    }

    private void OnOwnerAttackHit(EventInfoAttackHit info)
    {
        if (!NetworkServer.active || !isEnabled || _owner == null) return;
        if (info.attacker != _owner) return;
        if (info.victim == null)
        {
            DariusLog.Warn("HEM-ATTACK", "Owner attack event had null victim.");
            return;
        }
        if (!_owner.GetRelation(info.victim).HasFlag(EntityRelation.Enemy))
        {
            DariusLog.DebugInfo("HEM-ATTACK", "Ignored non-enemy victim=" + DariusLog.EntityLabel(info.victim));
            return;
        }

        DariusConstellationRuntime.NotifyDamageHit(_owner, info.victim, "basic attack");
        if (info.victim.IsNullInactiveDeadOrKnockedOut() || info.victim.currentHealth <= 0f)
        {
            DariusConstellationRuntime.NotifyKill(_owner, info.victim, "basic attack");
            return;
        }
        DariusLog.Info("HEM-ATTACK", "Basic attack applying Hemorrhage to " + DariusLog.EntityLabel(info.victim));
        Apply(info.victim, 1, "basic attack");
    }

    private void OnDestroy()
    {
        DariusLog.Info("HEM", "Runtime OnDestroy owner=" + DariusLog.EntityLabel(_owner));
        UnsubscribeAttack();
        RemoveNoxianMight();
    }
}