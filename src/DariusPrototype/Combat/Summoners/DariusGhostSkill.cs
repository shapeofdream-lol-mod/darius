using System;
using System.Collections;
using Mirror;
using UnityEngine;

public sealed class St_Darius_Ghost : SkillTrigger
{
    protected override void OnPrepare()
    {
        base.OnPrepare();
        configs = new[] { DariusSummonerBalance.CreateGhostConfig(this), DariusSummonerBalance.CreateGhostConfig(this) };
        for (int i = 0; i < configs.Length; i++) configs[i].triggerIcon = DariusPrototypeIcons.Get("GHOST");
        DariusLog.Info("GHOST-CONFIG", "Prepared ghost from native movement template=" + DariusSummonerBalance.NativeSourceName);
    }

    protected override void OnLevelChange(int oldLevel, int newLevel)
    {
        if (newLevel < 1) return;
        if (owner == null) { ClientSkillEvent_OnLevelChange?.Invoke(oldLevel, newLevel); return; }
        base.OnLevelChange(oldLevel, newLevel);
    }

    public override AbilityInstance OnCastComplete(int configIndex, CastInfo info)
    {
        AbilityInstance result = null;
        try { result = base.OnCastComplete(configIndex, info); }
        catch (Exception e) { DariusLog.Exception("GHOST", e, "Stock movement cooldown bookkeeping failed"); }
        try
        {
            Hero ownerHero = info.caster as Hero;
            if (ownerHero != null)
            {
                DariusGhostRuntime runtime = ownerHero.GetComponent<DariusGhostRuntime>();
                if (runtime == null) runtime = ownerHero.gameObject.AddComponent<DariusGhostRuntime>();
                runtime.Activate(ownerHero);
                DariusConstellationRuntime.NotifyMovementSpell(ownerHero, this, "Ghost");
            }
        }
        catch (Exception e) { DariusLog.Exception("GHOST", e, "Ghost execution failed"); }
        return result;
    }
}

public sealed class DariusGhostRuntime : MonoBehaviour
{
    private Hero _owner;
    private StatBonus _bonus;
    private Coroutine _routine;
    private GameObject _vfx;

    public void Activate(Hero owner)
    {
        _owner = owner;
        if (_routine != null) StopCoroutine(_routine);
        RemoveBonus();
        ClearVfx();
        _routine = StartCoroutine(Run());
    }

    private IEnumerator Run()
    {
        RemoveBonus();
        float bonusPercent = DariusSummonerBalance.GetGhostBonusPercent(_owner);
        if (NetworkServer.active && _owner != null && _owner.Status != null)
        {
            _bonus = new StatBonus();
            _bonus.movementSpeedPercentage = bonusPercent;
            _owner.Status.AddStatBonus(_bonus);
        }
        _vfx = DariusPrototypeVfx.CreateSummonerGhost(_owner != null ? _owner.transform : null, DariusSummonerBalance.GhostDuration);
        DariusLog.Info("GHOST", "Activated owner=" + DariusLog.EntityLabel(_owner) + " bonus=" + bonusPercent.ToString("0.#") +
            "% duration=" + DariusSummonerBalance.GhostDuration.ToString("0.###"));
        yield return new WaitForSeconds(DariusSummonerBalance.GhostDuration);
        RemoveBonus();
        ClearVfx();
        _routine = null;
    }

    private void OnDestroy()
    {
        RemoveBonus();
        ClearVfx();
    }

    private void ClearVfx()
    {
        if (_vfx != null)
        {
            try { Destroy(_vfx); } catch { }
            _vfx = null;
        }
    }

    private void RemoveBonus()
    {
        if (_bonus != null && _owner != null && _owner.Status != null)
        {
            try { _owner.Status.RemoveStatBonus(_bonus); } catch { }
        }
        _bonus = null;
    }
}
