using System;
using System.Collections.Generic;
using UnityEngine;
using Mirror;

public sealed class Gem_Darius_Hemorrhage : Gem
{
    public override void OnEquipGem(Hero newOwner)
    {
        base.OnEquipGem(newOwner);
        if (newOwner == null)
        {
            DariusLog.Warn("HEM-GEM", "OnEquipGem received null owner.");
            return;
        }

        try
        {
            DariusHemorrhageRuntime state = newOwner.GetComponent<DariusHemorrhageRuntime>();
            if (state == null)
            {
                state = newOwner.gameObject.AddComponent<DariusHemorrhageRuntime>();
                DariusLog.Info("HEM-GEM", "Created runtime on owner=" + DariusLog.EntityLabel(newOwner));
            }
            state.AddLegacyEssenceSource(newOwner);
            DariusLog.Info("HEM-GEM", "Equipped on owner=" + DariusLog.EntityLabel(newOwner) + " sources=" + state.sourceCount);
        }
        catch (Exception e)
        {
            DariusLog.Exception("HEM-GEM", e, "OnEquipGem failed");
        }
    }

    public override void OnUnequipGem(Hero oldOwner)
    {
        base.OnUnequipGem(oldOwner);
        if (oldOwner == null)
        {
            DariusLog.Warn("HEM-GEM", "OnUnequipGem received null owner.");
            return;
        }

        try
        {
            DariusHemorrhageRuntime state = oldOwner.GetComponent<DariusHemorrhageRuntime>();
            if (state != null)
            {
                state.RemoveLegacyEssenceSource();
                DariusLog.Info("HEM-GEM", "Unequipped from owner=" + DariusLog.EntityLabel(oldOwner) + " sources=" + state.sourceCount);
            }
            else
            {
                DariusLog.Warn("HEM-GEM", "Unequip could not find DariusHemorrhageRuntime on owner=" + oldOwner.name);
            }
        }
        catch (Exception e)
        {
            DariusLog.Exception("HEM-GEM", e, "OnUnequipGem failed");
        }
    }
}

public sealed class DariusHemorrhageRuntime : MonoBehaviour
{
    public const int MaxStacks = 5; // Base cap; runtime cap grows with the Identity Memory.
    public const int StackCapMilestoneEveryLevels = 5;
    public const int StackCapPerMilestone = 1;
    public const float NoxianMightAttackDamagePerExtraMaxStack = 20f;
    public const float Duration = 5f;
    public const float TickInterval = 1f;
    public const float AdRatioPerStackPerTick = 0.07f;
    public const float DamageGrowthPerAdditionalLevel = 0.20f;

    public static float AdRatioPerStackAtLevel(int level)
    {
        return DariusMemoryScaling.Linear(AdRatioPerStackPerTick, level, DamageGrowthPerAdditionalLevel);
    }

    public static int MaxStacksAtLevel(int level)
    {
        return MaxStacks + DariusMemoryScaling.MilestoneCount(level, StackCapMilestoneEveryLevels) * StackCapPerMilestone;
    }

    public static float NoxianMightAttackDamagePercentAtLevel(int level, bool warFervor)
    {
        float baseline = warFervor ? WarFervorAttackDamagePercent : NoxianMightAttackDamagePercent;
        int extraMaxStacks = Mathf.Max(0, MaxStacksAtLevel(level) - MaxStacks);
        return baseline + extraMaxStacks * NoxianMightAttackDamagePerExtraMaxStack;
    }

    public const float NoxianMightDuration = 5f;
    public const float NoxianMightAttackDamagePercent = 50f;
    public const float WarFervorAttackDamagePercent = 35f;
    public const int WarFervorRequiredApplications = 5;

    public bool isEnabled => _identityEquipped || _legacyEssenceSources > 0;
    public bool hasNoxianMight => isEnabled && Time.time < _noxianMightUntil;
    public int sourceCount => (_identityEquipped ? 1 : 0) + _legacyEssenceSources;
    public int essenceSourceCount => sourceCount; // compatibility for older callers/logs
    public int identityMemoryLevel => _identityEquipped ? _identityMemoryLevel : 1;
    public int currentMaxStacks => MaxStacksAtLevel(identityMemoryLevel);
    public float currentNoxianMightAttackDamagePercent => NoxianMightAttackDamagePercentAtLevel(identityMemoryLevel, DariusConstellationRuntime.GetWarFervorLevel(_owner) > 0);

    private Hero _owner;
    private bool _attackSubscribed;
    private bool _identityEquipped;
    private int _identityMemoryLevel = 1;
    private AbilityTrigger _identitySourceTrigger;
    private int _legacyEssenceSources;
    private float _noxianMightUntil;
    private StatBonus _noxianMightBonus;
    private GameObject _noxianMightVfx;
    private int _lastHudStackCount = -1;
    private int _lastHudDurationStep = -1;
    private readonly Dictionary<Entity, BleedState> _states = new Dictionary<Entity, BleedState>();
    private readonly List<Entity> _remove = new List<Entity>();
    private readonly Queue<float> _warFervorApplications = new Queue<float>();

    private sealed class BleedState
    {
        public int stacks;
        public float expiresAt;
        public float nextTickAt;
        public GameObject stackVfx;
        public GameObject fiveStackVfx;
    }

    public void SetIdentityEquipped(Hero owner, bool equipped, int memoryLevel = 1, AbilityTrigger sourceTrigger = null)
    {
        if (equipped && _owner != owner)
        {
            DariusLog.Info("HEM", "Owner changed from " + (_owner != null ? _owner.name : "<null>") + " to " + (owner != null ? owner.name : "<null>"));
            UnsubscribeAttack();
            _owner = owner;
            EnsureHud();
        }

        bool before = _identityEquipped;
        int beforeLevel = _identityMemoryLevel;
        _identityEquipped = equipped;
        _identityMemoryLevel = equipped ? DariusMemoryScaling.NormalizeLevel(memoryLevel) : 1;
        _identitySourceTrigger = equipped ? sourceTrigger : null;
        if (before != _identityEquipped || beforeLevel != _identityMemoryLevel)
        {
            DariusLog.Info("HEM", "Identity source " + before + " -> " + _identityEquipped +
                " memoryLevel=" + beforeLevel + " -> " + _identityMemoryLevel +
                " bleedRatioPerStack=" + AdRatioPerStackAtLevel(_identityMemoryLevel).ToString("0.###") +
                " maxStacks=" + MaxStacksAtLevel(_identityMemoryLevel) +
                " noxianAD=" + NoxianMightAttackDamagePercentAtLevel(_identityMemoryLevel, DariusConstellationRuntime.GetWarFervorLevel(_owner) > 0).ToString("0.#") + "%" +
                " owner=" + DariusLog.EntityLabel(_owner));
            if (_identityEquipped && beforeLevel != _identityMemoryLevel)
            {
                if (_noxianMightBonus != null)
                    RefreshNoxianMightStatBonus("Identity Memory level changed");
                RefreshBleedMarkersForCurrentCap();
            }
        }
        RefreshSourceState();
    }

    public void AddLegacyEssenceSource(Hero owner)
    {
        if (_owner != owner)
        {
            DariusLog.Info("HEM", "Legacy owner changed from " + (_owner != null ? _owner.name : "<null>") + " to " + (owner != null ? owner.name : "<null>"));
            UnsubscribeAttack();
            _owner = owner;
            EnsureHud();
        }
        _legacyEssenceSources++;
        DariusLog.Info("HEM", "Legacy Essence source added. count=" + _legacyEssenceSources + " owner=" + DariusLog.EntityLabel(_owner));
        RefreshSourceState();
    }

    public void RemoveLegacyEssenceSource()
    {
        int before = _legacyEssenceSources;
        _legacyEssenceSources = Mathf.Max(0, _legacyEssenceSources - 1);
        DariusLog.Info("HEM", "Legacy Essence source removed. count " + before + " -> " + _legacyEssenceSources);
        RefreshSourceState();
    }

    // Backward-compatible aliases for v0.11 callers/hot reloads.
    public void AddEssenceSource(Hero owner) { AddLegacyEssenceSource(owner); }
    public void RemoveEssenceSource() { RemoveLegacyEssenceSource(); }

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

        SyncHudState();
    }

    private void EnsureHud()
    {
        try { if (_owner != null && _owner.GetComponent<DariusHemorrhageHud>() == null) _owner.gameObject.AddComponent<DariusHemorrhageHud>(); }
        catch { }
    }

    private int GetHighestActiveStackCount()
    {
        int highest = 0;
        float now = Time.time;
        foreach (KeyValuePair<Entity, BleedState> pair in _states)
        {
            Entity target = pair.Key; BleedState state = pair.Value;
            if (target == null || state == null || now >= state.expiresAt || target.IsNullInactiveDeadOrKnockedOut()) continue;
            highest = Mathf.Max(highest, state.stacks);
        }
        return Mathf.Clamp(highest, 0, currentMaxStacks);
    }

    private void SyncHudState(bool force = false)
    {
        if (!NetworkServer.active || _owner == null || _owner.Skill == null || _owner.Skill.Identity == null) return;
        int stacks = GetHighestActiveStackCount();
        float duration = NoxianMightDuration + DariusConstellationRuntime.GetNoxianMightDurationBonus(_owner);
        int durationStep = hasNoxianMight ? Mathf.Clamp(Mathf.CeilToInt(((_noxianMightUntil - Time.time) / Mathf.Max(0.01f, duration)) * 100f), 0, 100) : 0;
        if (!force && stacks == _lastHudStackCount && durationStep == _lastHudDurationStep) return;
        _lastHudStackCount = stacks; _lastHudDurationStep = durationStep;
        _owner.Skill.Identity.specialOverlayColor = new Color(stacks / (float)Mathf.Max(1, currentMaxStacks), durationStep / 100f, 0.619f, 0f);
    }

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
        SyncHudState(true);
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
        SyncHudState(true);
        _warFervorApplications.Clear();
    }
}
