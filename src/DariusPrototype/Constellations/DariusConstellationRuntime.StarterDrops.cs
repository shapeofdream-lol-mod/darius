using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed partial class DariusConstellationRuntime : MonoBehaviour
{
    private IEnumerator StarterDropRoutine()
    {
        // DoDejavuSpawn / Hero.Start can run before the networked hero has reached its actual map
        // spawn. rc6 proved that a fixed delay is not enough: W/E were created at (0,0,0) and never
        // became usable world Memories. Wait for a real, active hero position instead.
        yield return null;
        yield return new WaitForSeconds(0.12f);
        if (!NetworkServer.active || _hero == null)
        {
            _starterRoutine = null;
            yield break;
        }

        // _starterPlayer is only a cached fast path. SpawnConstellationW/E can resolve the
        // owning player from the Hero if the Deja Vu phase has not supplied one yet.
        DewPlayer player = _starterPlayer;
        if (player != null && player.hero != _hero) player = null;

        float readyDeadline = Time.unscaledTime + 20f;
        bool worldReady = false;
        while (Time.unscaledTime < readyDeadline)
        {
            if (!NetworkServer.active || _hero == null)
            {
                _starterRoutine = null;
                yield break;
            }

            Vector3 agent = _hero.agentPosition;
            Vector3 transformPos = _hero.transform.position;
            bool finite = IsFiniteWorldPosition(agent) && IsFiniteWorldPosition(transformPos);
            bool realRoomScene = IsGameplayRoomScene(SceneManager.GetActiveScene().name);
            bool nonStagingPosition = IsUsableStarterWorldPosition(agent) && IsUsableStarterWorldPosition(transformPos);
            if (_hero.gameObject.activeInHierarchy && finite && realRoomScene && nonStagingPosition)
            {
                worldReady = true;
                break;
            }
            yield return new WaitForSecondsRealtime(0.10f);
        }

        if (!worldReady || _hero == null || !_hero.gameObject.activeInHierarchy)
        {
            string sceneName = SceneManager.GetActiveScene().name;
            DariusLog.Warn("STAR-DEJAVU", "Hero never reached a usable gameplay-room position; starter Memories were not spawned. scene=" +
                sceneName + " heroPos=" + (_hero != null ? DariusLog.Vec(_hero.agentPosition) : "<null>"));
            _starterRoutine = null;
            yield break;
        }
        DariusLog.Info("STAR-DEJAVU", "Starter-memory world state ready. scene=" + SceneManager.GetActiveScene().name +
            " heroPos=" + DariusLog.Vec(_hero.agentPosition));

        int wLevel = GetLevel(DariusConstellationIds.ICripplingStrike);
        if (wLevel > 0 && !_spawnedW)
        {
            _spawnedW = DariusFormalRegistry.SpawnConstellationW(_hero, wLevel, player);
            DariusLog.Info("STAR-STARTER", "W starter DejaVu-style spawn requested level=" + wLevel + " success=" + _spawnedW);
        }
        int eLevel = GetLevel(DariusConstellationIds.IApprehend);
        if (eLevel > 0 && !_spawnedE)
        {
            _spawnedE = DariusFormalRegistry.SpawnConstellationE(_hero, eLevel, player);
            DariusLog.Info("STAR-STARTER", "E starter DejaVu-style spawn requested level=" + eLevel + " success=" + _spawnedE);
        }
        _starterRoutine = null;
    }

    internal static bool IsUsableStarterWorldPosition(Vector3 pos)
    {
        if (!IsFiniteWorldPosition(pos)) return false;
        // Shape of Dreams parks the networked hero at (-5000,-5000,0) while PlayGame is
        // transitioning into the first Room_* scene. rc7 mistook that non-zero sentinel for a
        // valid location and spawned both starter Memories outside the playable world.
        if (Mathf.Abs(pos.x + 5000f) < 250f && Mathf.Abs(pos.y + 5000f) < 250f) return false;
        // Real gameplay navigation is close to ground level; this also rejects other far-off
        // staging/parking coordinates without imposing an arbitrary X/Z world-size limit.
        if (Mathf.Abs(pos.y) > 1000f) return false;
        return true;
    }

    private static bool IsFiniteWorldPosition(Vector3 pos)
    {
        return !float.IsNaN(pos.x) && !float.IsNaN(pos.y) && !float.IsNaN(pos.z) &&
               !float.IsInfinity(pos.x) && !float.IsInfinity(pos.y) && !float.IsInfinity(pos.z);
    }

    private static bool IsGameplayRoomScene(string sceneName)
    {
        return !string.IsNullOrEmpty(sceneName) && sceneName.StartsWith("Room_", StringComparison.OrdinalIgnoreCase);
    }

    private void OnMovementSpell(AbilityTrigger trigger, string spell)
    {
        int nimbus = GetLevel(DariusConstellationIds.FNimbusCloak);
        if (nimbus > 0)
        {
            if (_nimbusRoutine != null) StopCoroutine(_nimbusRoutine);
            _nimbusRoutine = StartCoroutine(NimbusRoutine(nimbus, spell));
        }

        int cosmic = GetLevel(DariusConstellationIds.FCosmicInsight);
        if (cosmic > 0 && trigger != null)
        {
            int id = trigger.GetInstanceID();
            Coroutine existing;
            if (_cosmicRoutines.TryGetValue(id, out existing) && existing != null) StopCoroutine(existing);
            _cosmicRoutines[id] = StartCoroutine(CosmicRoutine(trigger, cosmic, spell));
        }
    }

    private IEnumerator NimbusRoutine(int level, string spell)
    {
        RemoveBonus(ref _nimbusBonus);
        if (_hero != null && _hero.Status != null)
        {
            float[] values = { 15f, 20f, 25f };
            _nimbusBonus = new StatBonus();
            _nimbusBonus.movementSpeedPercentage = values[Mathf.Clamp(level, 1, values.Length) - 1];
            _hero.Status.AddStatBonus(_nimbusBonus);
            DariusLog.Info("STAR-NIMBUS", spell + " granted +" + _nimbusBonus.movementSpeedPercentage + "% move speed for 2s.");
        }
        yield return new WaitForSeconds(2f);
        RemoveBonus(ref _nimbusBonus);
        _nimbusRoutine = null;
    }

    private IEnumerator CosmicRoutine(AbilityTrigger trigger, int level, string spell)
    {
        float[] reductions = { 0.20f, 0.25f, 0.30f };
        float reduction = reductions[Mathf.Clamp(level, 1, reductions.Length) - 1];
        float wait = Mathf.Max(0.1f, DariusSummonerBalance.Cooldown * (1f - reduction));
        yield return new WaitForSeconds(wait);
        if (_hero != null && trigger != null)
        {
            try
            {
                _hero.ResetCooldown(trigger, false);
                DariusLog.Info("STAR-COSMIC", spell + " cooldown accelerated by " + (reduction * 100f).ToString("0") + "% after " + wait.ToString("0.##") + "s.");
            }
            catch (Exception e) { DariusLog.Exception("STAR-COSMIC", e, spell + " cooldown acceleration failed"); }
        }
        if (trigger != null) _cosmicRoutines.Remove(trigger.GetInstanceID());
    }

    private void RemoveBonus(ref StatBonus bonus)
    {
        if (bonus != null && _hero != null && _hero.Status != null)
        {
            try { _hero.Status.RemoveStatBonus(bonus); } catch { }
        }
        bonus = null;
    }

    private void OnDestroy()
    {
        RemoveBonus(ref _persistentBonus);
        RemoveBonus(ref _conquerorBonus);
        RemoveBonus(ref _lowHealthBonus);
        RemoveBonus(ref _alacrityBonus);
        RemoveBonus(ref _gatheringBonus);
        RemoveBonus(ref _nimbusBonus);
        RemoveBonus(ref _bloodRushBonus);
        RemoveBonus(ref _dunkmasterBonus);
        RemoveBonus(ref _noxianArenaBonus);
    }
}