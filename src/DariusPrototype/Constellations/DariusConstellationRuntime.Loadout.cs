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
    public void SetStar(string key, int level, bool active)
    {
        if (string.IsNullOrEmpty(key)) return;
        if (_hero == null) _hero = GetComponent<Hero>();
        if (active) _levels[key] = Mathf.Max(1, level);
        else _levels.Remove(key);
        // Equipment gameplay lives in a dedicated runtime but remains driven exclusively by the
        // same native StarEffect/Profile state as every other Darius constellation.
        if (DariusEquipmentConstellationLocalization.IsEquipmentKey(key))
            DariusEquipmentRuntime.SetStarForHero(_hero, key, Mathf.Max(1, level), active);
        RefreshPersistentBonus();
        RefreshLowHealthBonus(true);
        RefreshGatheringStorm(true);
        RefreshBloodRush(true);
        RefreshNoxianArena(true);
        // W/E unlock stars own their pickup timing. Schedule immediately and let the finite
        // room-ready routine wait for a real gameplay position; Deja Vu is only an optional
        // source of the owning DewPlayer reference, not a required lifecycle gate.
        if (active && (key == DariusConstellationIds.ICripplingStrike || key == DariusConstellationIds.IApprehend))
            ScheduleStarterDrops();
    }

    // Called from PlayGameManager.DoDejavuSpawn postfix. This is intentionally the same native
    // lifecycle phase that creates a normal Deja Vu Memory: the hero exists, the run reward-space
    // is initialized, and the owning DewPlayer is known. It avoids the old early-game (0,0,0) spawn.
    public static void NotifyNativeDejaVuSpawnPhase(DewPlayer player)
    {
        if (!NetworkServer.active || player == null) return;
        Hero_Darius hero = player.hero as Hero_Darius;
        if (hero == null) return;
        DariusConstellationRuntime runtime = hero.GetComponent<DariusConstellationRuntime>();
        if (runtime == null) runtime = hero.gameObject.AddComponent<DariusConstellationRuntime>();
        runtime.OnNativeDejaVuSpawnPhase(player);
    }

    private void OnNativeDejaVuSpawnPhase(DewPlayer player)
    {
        if (_hero == null) _hero = GetComponent<Hero>();
        if (_hero == null || player == null || player.hero != _hero) return;
        _starterPlayer = player;
        _nativeDejavuSpawnSeen = true;
        DariusLog.Info("STAR-DEJAVU", "Native Deja Vu spawn phase ready for Hero_Darius. WLevel=" +
            GetLevel(DariusConstellationIds.ICripplingStrike) + " ELevel=" + GetLevel(DariusConstellationIds.IApprehend) +
            " heroPos=" + DariusLog.Vec(_hero.agentPosition));
        ScheduleStarterDrops();
    }

    public int GetLevel(string key)
    {
        int value;
        return !string.IsNullOrEmpty(key) && _levels.TryGetValue(key, out value) ? value : 0;
    }

    public static int GetStarLevel(Hero hero, string key)
    {
        if (hero == null) return 0;
        DariusConstellationRuntime runtime = hero.GetComponent<DariusConstellationRuntime>();
        return runtime != null ? runtime.GetLevel(key) : 0;
    }

    public static float GetInstantQDamageMultiplier(Hero hero)
    {
        int level = GetStarLevel(hero, DariusConstellationIds.IInstantDecimate);
        if (level <= 0) return 1f;
        float[] values = { 1.12f, 1.16f, 1.20f, 1.24f };
        return values[Mathf.Clamp(level, 1, values.Length) - 1];
    }

    public static bool IsInstantQ(Hero hero) => GetStarLevel(hero, DariusConstellationIds.IInstantDecimate) > 0;

    public static float GetQHealMultiplier(Hero hero)
    {
        int level = GetStarLevel(hero, DariusConstellationIds.LRevitalize);
        if (level <= 0) return 1f;
        float[] values = { 1.20f, 1.25f, 1.30f, 1.35f };
        float result = values[Mathf.Clamp(level, 1, values.Length) - 1];
        try
        {
            if (hero != null && hero.Status != null && hero.Status.maxHealth > 0f && hero.currentHealth / hero.Status.maxHealth <= 0.40f)
                result += 0.06f;
        }
        catch { }
        return result;
    }
}