using System;
using System.Collections.Generic;
using UnityEngine;
using Mirror;

public sealed partial class DariusHemorrhageRuntime : MonoBehaviour
{
    private void RefreshBleedMarkersForCurrentCap()
    {
        int maxStacks = currentMaxStacks;
        foreach (KeyValuePair<Entity, BleedState> pair in _states)
        {
            Entity target = pair.Key;
            BleedState state = pair.Value;
            if (target == null || state == null || target.IsNullInactiveDeadOrKnockedOut()) continue;
            try
            {
                if (state.stackVfx != null)
                {
                    UnityEngine.Object.Destroy(state.stackVfx);
                    state.stackVfx = null;
                }
                if (state.fiveStackVfx != null)
                {
                    UnityEngine.Object.Destroy(state.fiveStackVfx);
                    state.fiveStackVfx = null;
                }

                if (state.stacks >= maxStacks)
                    state.fiveStackVfx = DariusPrototypeVfx.CreateBleedFiveMark(_owner, target.transform);
                else if (state.stacks > 0)
                    state.stackVfx = DariusPrototypeVfx.CreateBleedStackMarker(_owner, target.transform, state.stacks);

                if (state.stacks > 0 && Time.time < state.expiresAt)
                    SyncHemorrhageStatus(target, state, maxStacks, Mathf.Max(0.01f, state.expiresAt - Time.time));
            }
            catch (Exception e)
            {
                DariusLog.Exception("HEM-VFX", e, "Refreshing bleed cap marker failed target=" + target.name);
            }
        }
        DariusLog.DebugInfo("HEM-VFX", "Refreshed active bleed markers for Identity cap=" + maxStacks);
    }

    private void RecordWarFervorApplications(int count, string source, int level)
    {
        float window = DariusConstellationRuntime.GetWarFervorWindow(_owner);
        if (window <= 0f) return;
        float now = Time.time;
        while (_warFervorApplications.Count > 0 && now - _warFervorApplications.Peek() > window)
            _warFervorApplications.Dequeue();
        for (int i = 0; i < count; i++) _warFervorApplications.Enqueue(now);
        DariusLog.Info("WAR-FERVOR", "source=" + source + " level=" + level + " applications=" + _warFervorApplications.Count +
            "/" + WarFervorRequiredApplications + " window=" + window.ToString("0.##") + "s");
        if (_warFervorApplications.Count >= WarFervorRequiredApplications)
        {
            _warFervorApplications.Clear();
            GrantNoxianMight("War Fervor global Hemorrhage threshold");
        }
    }

    // League rule: a successful Noxian Guillotine execute immediately grants Noxian Might.
    // Keep the state transition inside the passive runtime so stat bonus, duration and presentation
    // use exactly the same path as naturally reaching the current Hemorrhage stack cap.
    public bool GrantNoxianMightFromExecute(string reason)
    {
        if (!NetworkServer.active)
        {
            DariusLog.DebugInfo("R-EXECUTE-MIGHT", "Ignored execute grant on non-server instance.");
            return false;
        }
        if (!isEnabled || _owner == null)
        {
            DariusLog.Warn("R-EXECUTE-MIGHT", "Execute kill confirmed but Hemorrhage Identity is not active; owner=" +
                DariusLog.EntityLabel(_owner) + " sourceCount=" + sourceCount);
            return false;
        }
        GrantNoxianMight(string.IsNullOrEmpty(reason) ? "Noxian Guillotine execute" : reason);
        return _noxianMightBonus != null;
    }

    private void GrantNoxianMight(string reason)
    {
        float oldUntil = _noxianMightUntil;
        float duration = NoxianMightDuration + DariusConstellationRuntime.GetNoxianMightDurationBonus(_owner);
        bool warFervor = DariusConstellationRuntime.GetWarFervorLevel(_owner) > 0;
        float adPercent = NoxianMightAttackDamagePercentAtLevel(identityMemoryLevel, warFervor);
        float flatAd = DariusConstellationRuntime.GetNoxianMightAdBonus(_owner);
        _noxianMightUntil = Time.time + duration;
        RefreshNoxianMightStatus(duration);
        if (_noxianMightBonus != null)
        {
            if (Mathf.Abs(_noxianMightBonus.attackDamagePercentage - adPercent) > 0.001f ||
                Mathf.Abs(_noxianMightBonus.attackDamageFlat - flatAd) > 0.001f)
                RefreshNoxianMightStatBonus("Noxian Might refresh");
            DariusLog.Info("NOXIAN-MIGHT", "Refreshed duration oldUntil=" + oldUntil.ToString("0.000") +
                " newUntil=" + _noxianMightUntil.ToString("0.000") + " AD=" + adPercent.ToString("0.#") +
                "% + " + flatAd.ToString("0.#") + " flat identityLevel=" + identityMemoryLevel + " maxStacks=" + currentMaxStacks + " reason=" + reason);
            return;
        }

        try
        {
            _noxianMightBonus = new StatBonus();
            _noxianMightBonus.attackDamagePercentage = adPercent;
            _noxianMightBonus.attackDamageFlat = flatAd;
            _owner.Status.AddStatBonus(_noxianMightBonus);
            _warFervorApplications.Clear();
            DariusLog.Info("NOXIAN-MIGHT", "Granted +" + adPercent + "% AD and +" + flatAd + " flat AD for " + duration.ToString("0.##") +
                "s owner=" + DariusLog.EntityLabel(_owner) + " warFervor=" + warFervor +
                " identityLevel=" + identityMemoryLevel + " maxStacks=" + currentMaxStacks + " reason=" + reason);
            try { _noxianMightVfx = DariusPrototypeVfx.CreateNoxianMight(_owner.transform); }
            catch (Exception e) { DariusLog.Exception("HEM-VFX", e, "Noxian Might VFX failed"); }
            DariusVoiceRuntime.NotifyPMax(_owner);
        }
        catch (Exception e)
        {
            DariusLog.Exception("NOXIAN-MIGHT", e, "Granting stat bonus failed");
            _noxianMightBonus = null;
        }
    }

    private void RefreshNoxianMightStatus(float duration)
    {
        if (!NetworkServer.active || _owner == null || DariusFormalRegistry.NoxianMightStatus == null) return;
        try
        {
            if (_noxianMightStatus == null || !_noxianMightStatus.isActive)
            {
                _noxianMightStatus = _owner.CreateStatusEffect<Se_Darius_NoxianMight>(
                    DariusFormalRegistry.NoxianMightStatus,
                    _owner,
                    default(CastInfo),
                    null);
            }
            if (_noxianMightStatus != null)
            {
                _noxianMightStatus.SetTimer(duration);
                _noxianMightStatus.ShowOnScreenTimer(
                    null,
                    new Color(0.85f, 0.08f, 0.08f, 1f),
                    false);
                DariusLog.Info("NOXIAN-MIGHT-BUFF", "Native on-screen timer set duration=" + duration.ToString("0.##") +
                    " victim=" + DariusLog.EntityLabel(_owner));
            }
        }
        catch (Exception e)
        {
            DariusLog.Exception("NOXIAN-MIGHT-BUFF", e, "Failed creating or refreshing native status effect");
            _noxianMightStatus = null;
        }
    }

    private void RefreshNoxianMightStatBonus(string reason)
    {
        if (_noxianMightBonus == null || _owner == null) return;
        try
        {
            bool warFervor = DariusConstellationRuntime.GetWarFervorLevel(_owner) > 0;
            float adPercent = NoxianMightAttackDamagePercentAtLevel(identityMemoryLevel, warFervor);
            float flatAd = DariusConstellationRuntime.GetNoxianMightAdBonus(_owner);
            _owner.Status.RemoveStatBonus(_noxianMightBonus);
            _noxianMightBonus.attackDamagePercentage = adPercent;
            _noxianMightBonus.attackDamageFlat = flatAd;
            _owner.Status.AddStatBonus(_noxianMightBonus);
            DariusLog.Info("NOXIAN-MIGHT", "Updated active stat bonus to +" + adPercent.ToString("0.#") +
                "% AD and +" + flatAd.ToString("0.#") + " flat AD identityLevel=" + identityMemoryLevel + " maxStacks=" + currentMaxStacks + " reason=" + reason);
        }
        catch (Exception e)
        {
            DariusLog.Exception("NOXIAN-MIGHT", e, "Updating active stat bonus failed reason=" + reason);
        }
    }

    private void RemoveNoxianMight()
    {
        if (_noxianMightBonus != null && _owner != null)
        {
            try
            {
                _owner.Status.RemoveStatBonus(_noxianMightBonus);
                DariusLog.Info("NOXIAN-MIGHT", "Removed from owner=" + DariusLog.EntityLabel(_owner));
            }
            catch (Exception e) { DariusLog.Exception("NOXIAN-MIGHT", e, "Removing stat bonus failed"); }
        }
        if (_noxianMightVfx != null)
        {
            try { UnityEngine.Object.Destroy(_noxianMightVfx); } catch { }
            _noxianMightVfx = null;
        }
        _noxianMightBonus = null;
        _noxianMightUntil = 0f;
        if (_noxianMightStatus != null)
        {
            try { _noxianMightStatus.HideOnScreenTimer(); } catch { }
            try { _noxianMightStatus.DestroyIfActive(); } catch { }
            _noxianMightStatus = null;
        }
        _warFervorApplications.Clear();
    }
}