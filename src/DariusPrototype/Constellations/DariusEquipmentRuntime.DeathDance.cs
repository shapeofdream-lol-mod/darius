using System;
using System.Collections;
using System.Collections.Generic;
using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed partial class DariusEquipmentRuntime : MonoBehaviour
{
    private void TickDeathDance()
    {
        if (_deathDance.Count == 0 || _hero == null || _hero.IsNullOrInactive()) return;
        for (int i = _deathDance.Count - 1; i >= 0; i--)
        {
            DeferredDamage d = _deathDance[i];
            if (d == null || d.ticksRemaining <= 0) { _deathDance.RemoveAt(i); continue; }
            if (Time.time < d.nextTick) continue;
            d.nextTick += 0.5f;
            d.ticksRemaining--;
            try
            {
                DamageData damage = _hero.PureDamage(d.perTick, 0f).SetActor(_hero);
                damage.Dispatch(_hero);
            }
            catch (Exception e)
            {
                DariusLog.Exception("ITEM-DEATHDANCE", e, "Deferred self-damage dispatch failed");
            }
            if (d.ticksRemaining <= 0) _deathDance.RemoveAt(i);
        }
    }

    private void OnKill(Entity victim, string source)
    {
        int victimId;
        try { victimId = victim.GetInstanceID(); } catch { return; }
        float lastResolved;
        if (_recentKills.TryGetValue(victimId, out lastResolved) && Time.time - lastResolved < 1.5f) return;
        _recentKills[victimId] = Time.time;

        _lastCombatAt = Time.time;
        int deathDance = GetLevel(DariusEquipmentStarIds.DeathsDance);
        if (deathDance > 0 && _deathDance.Count > 0)
        {
            float outstanding = 0f;
            for (int i = 0; i < _deathDance.Count; i++)
                if (_deathDance[i] != null) outstanding += _deathDance[i].perTick * _deathDance[i].ticksRemaining;
            float[] clearFractions = { 0.10f, 0.20f, 0.30f, 0.40f };
            float clearFraction = clearFractions[Mathf.Clamp(deathDance, 1, clearFractions.Length) - 1];
            for (int i = _deathDance.Count - 1; i >= 0; i--)
            {
                DeferredDamage deferred = _deathDance[i];
                if (deferred == null || deferred.ticksRemaining <= 0)
                {
                    _deathDance.RemoveAt(i);
                    continue;
                }
                deferred.perTick *= 1f - clearFraction;
            }
            float cleared = outstanding * clearFraction;
            DariusLog.Info("ITEM-DEATHDANCE", "Kill cleared=" + cleared.ToString("0.##") + " of outstanding=" + outstanding.ToString("0.##") +
                " clear%=" + (clearFraction * 100f).ToString("0.#") + " source=" + source);
        }
    }

    private void RefreshBloodmail(bool force)
    {
        int level = GetLevel(DariusEquipmentStarIds.OverlordsBloodmail);
        if (!force && Time.time < _nextBloodmailRefresh) return;
        _nextBloodmailRefresh = Time.time + 0.25f;
        if (_bloodmailBonus != null && _hero != null && _hero.Status != null)
        {
            try { _hero.Status.RemoveStatBonus(_bloodmailBonus); } catch { }
            _bloodmailBonus = null;
        }
        if (level <= 0 || _hero == null || _hero.Status == null) return;

        float maxHealth = Mathf.Max(0f, _hero.Status.maxHealth);
        float hpRatio = maxHealth > 0f ? Mathf.Clamp01(_hero.currentHealth / maxHealth) : 1f;
        float[] hpConversion = { 0.010f, 0.0125f, 0.015f, 0.018f }; // flat AD per max-HP point
        float[] lowHealthCap = { 12f, 15f, 18f, 22f };
        int idx = Mathf.Clamp(level, 1, hpConversion.Length) - 1;
        float fromHealth = Mathf.Min(100f, maxHealth * hpConversion[idx]);
        float fromMissing = (1f - hpRatio) * lowHealthCap[idx];
        _bloodmailBonus = new StatBonus();
        _bloodmailBonus.attackDamageFlat = fromHealth + fromMissing;
        _hero.Status.AddStatBonus(_bloodmailBonus);
    }

    private void RefreshYoumuu(bool force)
    {
        int level = GetLevel(DariusEquipmentStarIds.YoumuusGhostblade);
        bool should = level > 0 && Time.time - _lastCombatAt >= 2f;
        if (!force && should == _youmuuActive) return;
        if (_youmuuBonus != null && _hero != null && _hero.Status != null)
        {
            try { _hero.Status.RemoveStatBonus(_youmuuBonus); } catch { }
            _youmuuBonus = null;
        }
        _youmuuActive = should;
        if (!should || _hero == null || _hero.Status == null) return;
        float[] speed = { 15f, 18f, 21f, 25f };
        _youmuuBonus = new StatBonus();
        _youmuuBonus.movementSpeedPercentage = speed[Mathf.Clamp(level, 1, speed.Length) - 1];
        _hero.Status.AddStatBonus(_youmuuBonus);
        DariusLog.DebugInfo("ITEM-YOUMUU", "Ghostwalk active move%=" + _youmuuBonus.movementSpeedPercentage);
    }

    private void TickMovement()
    {
        if (_hero == null) return;
        Vector3 p = _hero.transform.position;
        if (_hasLastPosition)
        {
            Vector3 delta = p - _lastPosition;
            delta.y = 0f;
            float dist = delta.magnitude;
            if (dist > 0f && dist < 10f && GetLevel(DariusEquipmentStarIds.DeadMansPlate) > 0)
                _deadMansMomentum = Mathf.Clamp(_deadMansMomentum + dist * 5f, 0f, 100f);
        }
        _lastPosition = p;
        _hasLastPosition = true;
    }

    private void TickBlackCleaver()
    {
        if (_blackCleaver.Count == 0) return;
        List<int> remove = null;
        foreach (KeyValuePair<int, ArmorShred> kv in _blackCleaver)
        {
            ArmorShred entry = kv.Value;
            if (entry == null || entry.target == null || entry.target.IsNullInactiveDeadOrKnockedOut() || Time.time >= entry.expiresAt)
            {
                if (entry != null && entry.bonus != null && entry.target != null && entry.target.Status != null)
                {
                    try { entry.target.Status.RemoveStatBonus(entry.bonus); } catch { }
                }
                if (remove == null) remove = new List<int>();
                remove.Add(kv.Key);
            }
        }
        if (remove != null) for (int i = 0; i < remove.Count; i++) _blackCleaver.Remove(remove[i]);
    }

    private void ClearBlackCleaver()
    {
        foreach (ArmorShred entry in _blackCleaver.Values)
        {
            if (entry != null && entry.bonus != null && entry.target != null && entry.target.Status != null)
            {
                try { entry.target.Status.RemoveStatBonus(entry.bonus); } catch { }
            }
        }
        _blackCleaver.Clear();
    }

    private void CheckAwooRoom()
    {
        if (GetLevel(DariusEquipmentStarIds.Awoo) <= 0) return;
        string scene = string.Empty;
        try { scene = SceneManager.GetActiveScene().name ?? string.Empty; } catch { }
        if (string.IsNullOrEmpty(scene) || scene.IndexOf("Room_", StringComparison.OrdinalIgnoreCase) < 0) return;
        if (!string.Equals(scene, _awooRoomToken, StringComparison.Ordinal)) ResetAwooForCurrentRoom("room-change:" + scene);
    }

    private void ResetAwooForCurrentRoom(string reason)
    {
        string scene = string.Empty;
        try { scene = SceneManager.GetActiveScene().name ?? string.Empty; } catch { }
        _awooRoomToken = scene;
        _awooRAdRatio = 0.25f;
        _awooPitch = 1f;
        if (_hero != null && GetLevel(DariusEquipmentStarIds.Awoo) > 0)
            St_Darius_NoxianGuillotine.ApplyAwooCooldownOverrideToHero(_hero, true);
        DariusAwooAudioRuntime audio = _hero != null ? _hero.GetComponent<DariusAwooAudioRuntime>() : null;
        if (audio != null) audio.StopPlayback();
        DariusLog.Info("STAR-AWOO", "Room state reset reason=" + reason + " room=" + scene + " Rratio=0.25 pitch=1.0");
    }
}