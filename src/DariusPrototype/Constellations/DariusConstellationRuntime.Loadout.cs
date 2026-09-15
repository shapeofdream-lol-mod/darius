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
    private IEnumerator ApplySelectedLoadoutFallbackRoutine()
    {
        // Native StarEffect creation remains the primary path. This delayed reconciliation makes the
        // runtime robust if the game rebuilds HeroSkill constellation status effects during a scene
        // transition: the saved Darius loadout is authoritative and SetStar is idempotent.
        yield return null;
        yield return new WaitForSeconds(0.18f);
        if (!NetworkServer.active || _hero == null) yield break;

        // DewSave.profileMain and GetLocalPreferredGameSettings belong to this process' local player.
        // On a multiplayer host they must never be used to reconcile a remote player's hero, or the
        // host's saved constellation page can leak onto the guest. Remote heroes rely on the game's
        // normal networked StarEffect/loadout path instead.
        try
        {
            if (DewPlayer.local == null || DewPlayer.local.hero != _hero)
            {
                DariusLog.DebugInfo("MP-STAR", "Skipped local-profile constellation fallback for non-local Hero_Darius; native networked StarEffect state remains authoritative.");
                yield break;
            }
        }
        catch { yield break; }

        try
        {
            DewProfile profile = DewSave.profileMain;
            if (profile == null || profile.heroLoadouts == null) yield break;
            List<HeroLoadoutData> pages;
            if (!profile.heroLoadouts.TryGetValue(DariusTravelerRegistry.HeroName, out pages) || pages == null || pages.Count == 0) yield break;

            int page = 0;
            try
            {
                var settings = NetworkedManagerBase<GameSettingsManager>.instance != null
                    ? NetworkedManagerBase<GameSettingsManager>.instance.GetLocalPreferredGameSettings() : null;
                if (settings != null && settings.heroSelectedLoadoutIndex != null)
                {
                    int selected;
                    if (settings.heroSelectedLoadoutIndex.TryGetValue(DariusTravelerRegistry.HeroName, out selected))
                        page = Mathf.Clamp(selected, 0, pages.Count - 1);
                }
            }
            catch { page = 0; }

            HeroLoadoutData loadout = pages[Mathf.Clamp(page, 0, pages.Count - 1)];
            if (loadout == null) yield break;
            int applied = 0;
            applied += ApplySavedStars(loadout.cDestruction);
            applied += ApplySavedStars(loadout.cLife);
            applied += ApplySavedStars(loadout.cImagination);
            applied += ApplySavedStars(loadout.cFlexible);
            DariusLog.Info("STAR-RECONCILE", "Reconciled selected Hero_Darius loadout page=" + page + " activeDariusStars=" + applied);
        }
        catch (Exception e)
        {
            DariusLog.Exception("STAR-RECONCILE", e, "Could not reconcile selected Darius constellation loadout");
        }
    }

    private int ApplySavedStars(List<LoadoutStarItem> stars)
    {
        if (stars == null) return 0;
        int applied = 0;
        for (int i = 0; i < stars.Count; i++)
        {
            LoadoutStarItem item = stars[i];
            if (string.IsNullOrEmpty(item.name) || !DariusConstellationLocalization.IsDariusStarKey(item.name)) continue;
            SetStar(item.name, Mathf.Max(1, item.level), true);
            applied++;
        }
        return applied;
    }

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