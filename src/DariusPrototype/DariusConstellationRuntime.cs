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

public sealed class DariusConstellationRuntime : MonoBehaviour
{
    private Hero _hero;
    private readonly Dictionary<string, int> _levels = new Dictionary<string, int>(StringComparer.Ordinal);
    private readonly Dictionary<int, float> _recentKills = new Dictionary<int, float>();

    private StatBonus _persistentBonus;
    private StatBonus _conquerorBonus;
    private StatBonus _lowHealthBonus;
    private StatBonus _alacrityBonus;
    private StatBonus _gatheringBonus;
    private StatBonus _nimbusBonus;
    private StatBonus _bloodRushBonus;
    private StatBonus _dunkmasterBonus;
    private StatBonus _noxianArenaBonus;

    private int _conquerorStacks;
    private float _conquerorExpiresAt;
    private int _killCount;
    private int _alacrityStacks;
    private int _lastGatheringStep = -1;
    private bool _lastLowHealth;
    private float _lastHealth = -1f;
    private float _secondWindPool;
    private float _nextSecondWindTick;
    private bool _spawnedW;
    private bool _spawnedE;
    private bool _nativeDejavuSpawnSeen;
    private DewPlayer _starterPlayer;
    private Coroutine _starterRoutine;
    private Coroutine _nimbusRoutine;
    private Coroutine _dunkmasterRoutine;
    private float _nextBloodRushRefresh;
    private int _lastBloodRushCount = -1;
    private float _nextNoxianArenaRefresh;
    private int _lastNoxianArenaCount = -1;
    private int _lastNoxianArenaLevel = -1;
    private DariusEnemyClass _lastNoxianArenaRank = DariusEnemyClass.Common;
    private readonly Dictionary<int, Coroutine> _cosmicRoutines = new Dictionary<int, Coroutine>();

    private void Awake()
    {
        _hero = GetComponent<Hero>();
        if (_hero != null) _lastHealth = _hero.currentHealth;
    }

    private void Start()
    {
        if (!NetworkServer.active) return;
        StartCoroutine(ApplySelectedLoadoutFallbackRoutine());
        StartCoroutine(StarterDejaVuFallbackReadyRoutine());
    }

    private IEnumerator StarterDejaVuFallbackReadyRoutine()
    {
        // Prefer PlayGameManager.DoDejavuSpawn because it is the game's own stable Memory-drop
        // lifecycle. Some runs do not execute a meaningful Deja Vu selection, though, so keep a
        // delayed fallback that still uses the exact same Dew.CreateSkillTrigger pickup path.
        //
        // This does not grant W/E directly to HeroSkill. It creates real world Memory pickups, so
        // ownership, UI refresh, level initialization and network synchronization remain native.
        yield return null;
        yield return new WaitForSeconds(1.25f);
        if (!NetworkServer.active || _hero == null || _nativeDejavuSpawnSeen) yield break;

        int wLevel = GetLevel(DariusConstellationIds.ICripplingStrike);
        int eLevel = GetLevel(DariusConstellationIds.IApprehend);
        if (wLevel <= 0 && eLevel <= 0) yield break;

        DewPlayer player = FindOwningPlayer();
        if (player == null)
        {
            // Player/hero binding can be a little later on a host transition. One retry is enough;
            // never spin every frame or create a permanent polling cost.
            yield return new WaitForSeconds(0.75f);
            player = FindOwningPlayer();
        }
        if (player == null || player.hero != _hero)
        {
            DariusLog.Warn("STAR-DEJAVU", "Fallback Memory-drop phase could not resolve the owning DewPlayer; W/E starter pickups were not spawned.");
            yield break;
        }

        _starterPlayer = player;
        _nativeDejavuSpawnSeen = true;
        DariusLog.Info("STAR-DEJAVU", "Native DoDejavuSpawn phase was not observed; using delayed DejaVu-style Memory pickup fallback. WLevel=" +
            wLevel + " ELevel=" + eLevel + " heroPos=" + DariusLog.Vec(_hero.agentPosition));
        ScheduleStarterDrops();
    }

    private DewPlayer FindOwningPlayer()
    {
        if (_hero == null) return null;
        try
        {
            if (DewPlayer.local != null && DewPlayer.local.hero == _hero) return DewPlayer.local;
        }
        catch { }
        try
        {
            DewPlayer[] players = UnityEngine.Object.FindObjectsByType<DewPlayer>(FindObjectsSortMode.None);
            for (int i = 0; i < players.Length; i++)
                if (players[i] != null && players[i].hero == _hero) return players[i];
        }
        catch { }
        return null;
    }

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

        DewProfile profile = null;
        List<HeroLoadoutData> pages = null;
        bool hasPages = false;
        Exception setupError = null;
        try
        {
            profile = DewSave.profileMain;
            if (profile != null && profile.heroLoadouts != null)
                hasPages = profile.heroLoadouts.TryGetValue(DariusTravelerRegistry.HeroName, out pages) && pages != null && pages.Count > 0;
        }
        catch (Exception e) { setupError = e; }

        if (setupError != null)
        {
            DariusLog.Exception("STAR-RECONCILE", setupError, "Could not read Hero_Darius saved loadout pages");
            yield break;
        }
        if (!hasPages) yield break;

        // Stale deactivation is destructive, unlike the old additive-only fallback. Never infer
        // page 0 when GameSettings is not ready. Require two consecutive identical, in-range
        // selected-page reads so a transient lobby/scene value cannot disable the native
        // StarEffect state that the game has already built for the actual page.
        int page = -1;
        int lastCandidate = -1;
        int stableReads = 0;
        for (int attempt = 0; attempt < 4; attempt++)
        {
            int candidate;
            if (TryResolveSelectedLoadoutPage(pages, out candidate))
            {
                if (candidate == lastCandidate) stableReads++;
                else
                {
                    lastCandidate = candidate;
                    stableReads = 1;
                }
                if (stableReads >= 2)
                {
                    page = candidate;
                    break;
                }
            }
            else
            {
                lastCandidate = -1;
                stableReads = 0;
            }

            if (attempt < 3) yield return new WaitForSecondsRealtime(0.10f);
        }

        if (page < 0)
        {
            DariusLog.DebugInfo("STAR-RECONCILE", "Selected Hero_Darius loadout page was not stable/available; skipped destructive fallback and kept native StarEffect state authoritative.");
            yield break;
        }

        HeroLoadoutData loadout = null;
        Exception loadoutError = null;
        try { loadout = pages[page]; }
        catch (Exception e) { loadoutError = e; }
        if (loadoutError != null)
        {
            DariusLog.Exception("STAR-RECONCILE", loadoutError, "Stable selected Hero_Darius loadout page could not be read");
            yield break;
        }
        if (loadout == null) yield break;

        try
        {
            Dictionary<string, int> desired = new Dictionary<string, int>(StringComparer.Ordinal);
            CollectSavedStars(loadout.cDestruction, desired);
            CollectSavedStars(loadout.cLife, desired);
            CollectSavedStars(loadout.cImagination, desired);
            CollectSavedStars(loadout.cFlexible, desired);

            List<string> stale = new List<string>();
            foreach (string key in _levels.Keys)
                if (!desired.ContainsKey(key)) stale.Add(key);
            for (int i = 0; i < stale.Count; i++) SetStar(stale[i], 1, false);
            foreach (KeyValuePair<string, int> pair in desired) SetStar(pair.Key, pair.Value, true);

            DariusLog.Info("STAR-RECONCILE", "Reconciled selected Hero_Darius loadout page=" + page +
                " activeDariusStars=" + desired.Count + " deactivatedStale=" + stale.Count);
        }
        catch (Exception e)
        {
            DariusLog.Exception("STAR-RECONCILE", e, "Could not reconcile selected Darius constellation loadout");
        }
    }

    private static bool TryResolveSelectedLoadoutPage(List<HeroLoadoutData> pages, out int page)
    {
        page = -1;
        if (pages == null || pages.Count == 0) return false;
        try
        {
            GameSettingsManager manager = NetworkedManagerBase<GameSettingsManager>.instance;
            if (manager == null) return false;
            var settings = manager.GetLocalPreferredGameSettings();
            if (settings == null || settings.heroSelectedLoadoutIndex == null) return false;

            int selected;
            if (!settings.heroSelectedLoadoutIndex.TryGetValue(DariusTravelerRegistry.HeroName, out selected)) return false;
            if (selected < 0 || selected >= pages.Count) return false;
            page = selected;
            return true;
        }
        catch (Exception e)
        {
            DariusLog.DebugInfo("STAR-RECONCILE", "Selected loadout page read not ready: " + e.GetType().Name);
            return false;
        }
    }

    private static void CollectSavedStars(List<LoadoutStarItem> stars, Dictionary<string, int> desired)
    {
        if (stars == null || desired == null) return;
        for (int i = 0; i < stars.Count; i++)
        {
            LoadoutStarItem item = stars[i];
            if (string.IsNullOrEmpty(item.name) || !DariusConstellationLocalization.IsDariusStarKey(item.name)) continue;
            desired[item.name] = Mathf.Max(1, item.level);
        }
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

    public static float GetRDamageMultiplier(Hero hero)
    {
        float result = 1f;
        int d = GetStarLevel(hero, DariusConstellationIds.DAxiomArcanist);
        if (d > 0) { float[] values = { 1.10f, 1.13f, 1.16f, 1.20f }; result *= values[Mathf.Clamp(d, 1, values.Length) - 1]; }
        int legacy = GetStarLevel(hero, DariusConstellationIds.ILegacyGuillotine);
        if (legacy > 0) { float[] values = { 1.15f, 1.20f, 1.25f, 1.30f }; result *= values[Mathf.Clamp(legacy, 1, values.Length) - 1]; }
        return result;
    }

    public static float GetHemorrhageDurationBonus(Hero hero)
    {
        int level = GetStarLevel(hero, DariusConstellationIds.FHandOfNoxus);
        if (level <= 0) return 0f;
        float[] values = { 1.0f, 1.5f, 2.0f };
        return values[Mathf.Clamp(level, 1, values.Length) - 1];
    }

    public static float GetNoxianMightAdBonus(Hero hero)
    {
        int level = GetStarLevel(hero, DariusConstellationIds.DNoxianMight);
        if (level <= 0) return 0f;
        float[] values = { 9f, 12f, 15f, 18f };
        return values[Mathf.Clamp(level, 1, values.Length) - 1];
    }

    public static float GetLegacyWSlowDurationBonus(Hero hero)
    {
        int level = GetStarLevel(hero, DariusConstellationIds.ILegacyCripplingStrike);
        if (level <= 0) return 0f;
        float[] values = { 0.25f, 0.35f, 0.45f, 0.55f };
        return values[Mathf.Clamp(level, 1, values.Length) - 1];
    }

    public static float GetLegacyWRefundPerHemorrhageStack(Hero hero)
    {
        int level = GetStarLevel(hero, DariusConstellationIds.ILegacyCripplingStrike);
        if (level <= 0) return 0f;
        float[] values = { 0.50f, 0.60f, 0.70f, 0.80f };
        return values[Mathf.Clamp(level, 1, values.Length) - 1];
    }

    public static void ApplyLegacyApprehendMark(Hero hero, Entity target)
    {
        if (!NetworkServer.active || hero == null || target == null) return;
        int level = GetStarLevel(hero, DariusConstellationIds.ILegacyApprehend);
        if (level <= 0) return;
        DariusLegacyApprehendMark mark = target.GetComponent<DariusLegacyApprehendMark>();
        if (mark == null) mark = target.gameObject.AddComponent<DariusLegacyApprehendMark>();
        mark.Activate(hero, 3f);
    }

    public static float GetLegacyApprehendPhysicalMultiplier(Hero hero, Entity target)
    {
        if (hero == null || target == null) return 1f;
        int level = GetStarLevel(hero, DariusConstellationIds.ILegacyApprehend);
        if (level <= 0) return 1f;
        DariusLegacyApprehendMark mark = target.GetComponent<DariusLegacyApprehendMark>();
        if (mark == null || !mark.IsActiveFor(hero)) return 1f;
        float[] values = { 1.10f, 1.13f, 1.16f, 1.20f };
        return values[Mathf.Clamp(level, 1, values.Length) - 1];
    }

    public static float GetNoxianMightDurationBonus(Hero hero)
    {
        int level = GetStarLevel(hero, DariusConstellationIds.DAxiomArcanist);
        if (level <= 0) return 0f;
        float[] values = { 1.0f, 1.25f, 1.5f, 2.0f };
        return values[Mathf.Clamp(level, 1, values.Length) - 1];
    }

    public static int GetWarFervorLevel(Hero hero) => GetStarLevel(hero, DariusConstellationIds.IWarFervor);

    public static float GetWarFervorWindow(Hero hero)
    {
        int level = GetWarFervorLevel(hero);
        if (level <= 0) return 0f;
        float[] windows = { 5f, 5.5f, 6f, 6.5f };
        return windows[Mathf.Clamp(level, 1, windows.Length) - 1];
    }

    public static void NotifyDamageHit(Hero hero, Entity target, string source)
    {
        if (!NetworkServer.active || hero == null || target == null) return;
        DariusConstellationRuntime runtime = hero.GetComponent<DariusConstellationRuntime>();
        if (runtime != null) runtime.OnDamageHit(source);
    }

    public static void NotifyKill(Hero hero, Entity victim, string source)
    {
        if (!NetworkServer.active || hero == null || victim == null) return;
        DariusConstellationRuntime runtime = hero.GetComponent<DariusConstellationRuntime>();
        if (runtime != null) runtime.OnKill(victim, source);
        DariusEquipmentRuntime.NotifyKill(hero, victim, source);
        DariusVoiceRuntime.NotifyKill(hero, victim);
    }

    public static void NotifyMovementSpell(Hero hero, AbilityTrigger trigger, string spell)
    {
        if (!NetworkServer.active || hero == null) return;
        DariusConstellationRuntime runtime = hero.GetComponent<DariusConstellationRuntime>();
        if (runtime != null) runtime.OnMovementSpell(trigger, spell);
    }

    private void Update()
    {
        if (!NetworkServer.active || _hero == null || _hero.Status == null) return;
        float hp = _hero.currentHealth;
        if (_lastHealth < 0f) _lastHealth = hp;
        if (hp + 0.01f < _lastHealth) OnOwnerDamaged(_lastHealth - hp);
        _lastHealth = hp;

        if (_conquerorStacks > 0 && Time.time >= _conquerorExpiresAt)
        {
            _conquerorStacks = 0;
            RefreshConquerorBonus();
            DariusLog.DebugInfo("STAR-CONQUEROR", "Stacks expired.");
        }

        RefreshLowHealthBonus(false);
        RefreshGatheringStorm(false);
        if (Time.time >= _nextBloodRushRefresh) { _nextBloodRushRefresh = Time.time + 0.20f; RefreshBloodRush(false); }
        if (Time.time >= _nextNoxianArenaRefresh) { _nextNoxianArenaRefresh = Time.time + 0.25f; RefreshNoxianArena(false); }
        TickSecondWind();
    }

    private void OnDamageHit(string source)
    {
        int level = GetLevel(DariusConstellationIds.DConqueror);
        if (level <= 0) return;
        _conquerorStacks = Mathf.Clamp(_conquerorStacks + 1, 0, 6);
        _conquerorExpiresAt = Time.time + 5f;
        RefreshConquerorBonus();
        DariusLog.DebugInfo("STAR-CONQUEROR", "source=" + source + " stacks=" + _conquerorStacks + "/6 level=" + level);
    }

    private void OnKill(Entity victim, string source)
    {
        int id;
        try { id = victim.GetInstanceID(); } catch { return; }
        float seen;
        if (_recentKills.TryGetValue(id, out seen) && Time.time - seen < 1.5f) return;
        _recentKills[id] = Time.time;
        _killCount++;

        int triumph = GetLevel(DariusConstellationIds.DTriumph);
        if (triumph > 0 && _hero != null && _hero.Status != null)
        {
            float[] fractions = { 0.06f, 0.07f, 0.08f, 0.09f };
            float missing = Mathf.Max(0f, _hero.Status.maxHealth - _hero.currentHealth);
            float amount = missing * fractions[Mathf.Clamp(triumph, 1, fractions.Length) - 1];
            if (amount > 0.01f)
            {
                HealData heal = new HealData(amount);
                heal.SetActor(_hero);
                _hero.DoHeal(heal, _hero);
                DariusLog.Info("STAR-TRIUMPH", "source=" + source + " healed=" + amount.ToString("0.##") + " missing=" + missing.ToString("0.##") + " level=" + triumph);
            }
        }

        int alacrity = GetLevel(DariusConstellationIds.DAlacrity);
        if (alacrity > 0)
        {
            int[] perStack = { 6, 5, 4, 3 };
            int needed = perStack[Mathf.Clamp(alacrity, 1, perStack.Length) - 1];
            int desired = Mathf.Clamp(_killCount / needed, 0, 5);
            if (desired != _alacrityStacks)
            {
                _alacrityStacks = desired;
                RefreshAlacrityBonus();
                DariusLog.Info("STAR-ALACRITY", "kills=" + _killCount + " stacks=" + _alacrityStacks + "/5 level=" + alacrity);
            }
        }

        int bloodPrice = GetLevel(DariusConstellationIds.LBloodPrice);
        if (bloodPrice > 0 && _hero != null && _hero.Status != null)
        {
            DariusHemorrhageRuntime hem = _hero.GetComponent<DariusHemorrhageRuntime>();
            int stacks = hem != null ? hem.GetStacks(victim) : 0;
            if (stacks > 0)
            {
                float[] fractions = { 0.025f, 0.035f, 0.045f, 0.055f };
                float amount = _hero.Status.maxHealth * fractions[Mathf.Clamp(bloodPrice,1,fractions.Length)-1];
                HealData heal = new HealData(amount); heal.SetActor(_hero); _hero.DoHeal(heal, _hero);
                DariusLog.Info("STAR-BLOOD-PRICE", "source=" + source + " stacks=" + stacks + " heal=" + amount.ToString("0.##"));
            }
        }

        int dunk = GetLevel(DariusConstellationIds.DDunkmaster);
        if (dunk > 0 && !string.IsNullOrEmpty(source) && source.IndexOf("R execute", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            if (_dunkmasterRoutine != null) StopCoroutine(_dunkmasterRoutine);
            _dunkmasterRoutine = StartCoroutine(DunkmasterRoutine(dunk));
        }
    }

    private void RefreshBloodRush(bool force)
    {
        int level = GetLevel(DariusConstellationIds.DBloodRush);
        int count = 0;
        if (level > 0 && _hero != null)
        {
            DariusHemorrhageRuntime hem = _hero.GetComponent<DariusHemorrhageRuntime>();
            if (hem != null) count = Mathf.Clamp(hem.GetActiveBleedingTargetCount(), 0, 5);
        }
        if (!force && count == _lastBloodRushCount) return;
        _lastBloodRushCount = count;
        RemoveBonus(ref _bloodRushBonus);
        if (level <= 0 || count <= 0 || _hero == null || _hero.Status == null) return;
        float[] perTarget = { 3f, 3.5f, 4f, 4.5f };
        _bloodRushBonus = new StatBonus();
        _bloodRushBonus.movementSpeedPercentage = perTarget[Mathf.Clamp(level,1,perTarget.Length)-1] * count;
        _hero.Status.AddStatBonus(_bloodRushBonus);
        DariusLog.DebugInfo("STAR-BLOOD-RUSH", "bleedingTargets=" + count + " move%=" + _bloodRushBonus.movementSpeedPercentage);
    }


    // Noxian Arena is intentionally a boss-counter constellation rather than a generic stat stick.
    // It remains modest while clearing packs, then ramps sharply as Darius is isolated with a
    // high-value target. Boss rank overrides Elite rank; enemy count still includes boss adds.
    private void RefreshNoxianArena(bool force)
    {
        int level = GetLevel(DariusConstellationIds.DNoxianArena);
        if (level <= 0 || _hero == null || _hero.Status == null)
        {
            RemoveBonus(ref _noxianArenaBonus);
            _lastNoxianArenaCount = -1;
            _lastNoxianArenaLevel = level;
            _lastNoxianArenaRank = DariusEnemyClass.Common;
            return;
        }

        const float radius = 10f;
        int rawCount = 0;
        DariusEnemyClass strongest = DariusEnemyClass.Common;
        HashSet<Entity> seen = new HashSet<Entity>();
        try
        {
            Collider[] hits = Physics.OverlapSphere(_hero.transform.position, radius);
            for (int i = 0; i < hits.Length; i++)
            {
                Collider hit = hits[i];
                if (hit == null) continue;
                Entity enemy = hit.GetComponentInParent<Entity>();
                if (enemy == null || enemy == _hero || !seen.Add(enemy) || enemy.IsNullInactiveDeadOrKnockedOut()) continue;
                bool hostile = false;
                try { hostile = _hero.GetRelation(enemy).HasFlag(EntityRelation.Enemy); } catch { }
                if (!hostile) continue;

                rawCount++;
                if (strongest != DariusEnemyClass.Boss)
                {
                    DariusEnemyClass kind = DariusEnemyClassifier.Classify(enemy);
                    if (kind == DariusEnemyClass.Boss) strongest = DariusEnemyClass.Boss;
                    else if (kind == DariusEnemyClass.Elite && strongest == DariusEnemyClass.Common) strongest = DariusEnemyClass.Elite;
                }
            }
        }
        catch (Exception e)
        {
            DariusLog.Exception("STAR-NOXIAN-ARENA", e, "Nearby-enemy scan failed");
        }

        int count = Mathf.Clamp(rawCount, 0, 10);
        if (!force && count == _lastNoxianArenaCount && level == _lastNoxianArenaLevel && strongest == _lastNoxianArenaRank) return;
        _lastNoxianArenaCount = count;
        _lastNoxianArenaLevel = level;
        _lastNoxianArenaRank = strongest;
        RemoveBonus(ref _noxianArenaBonus);
        if (count <= 0) return;

        // 10 enemies = x1.00; each missing enemy adds 10%. Elite/Boss rank multiplies
        // that scarcity bonus. Boss encounters cap at x3 so adds can never make the bonus
        // stronger than the explicitly requested solo-Boss result.
        float scarcityMultiplier = 1f + (10 - count) * 0.10f;
        float rankMultiplier = strongest == DariusEnemyClass.Boss ? 2f : strongest == DariusEnemyClass.Elite ? 1.5f : 1f;
        float multiplier = scarcityMultiplier * rankMultiplier;
        if (strongest == DariusEnemyClass.Boss) multiplier = Mathf.Min(3f, multiplier);

        float[] attackDamage = { 3f, 4f, 5f, 6f };
        float[] attackSpeed = { 8f, 10f, 12f, 15f };
        float[] moveSpeed = { 3f, 4f, 5f, 6f };
        float[] armor = { 0.5f, 0.75f, 1f, 1.25f };
        int index = Mathf.Clamp(level, 1, 4) - 1;

        _noxianArenaBonus = new StatBonus();
        _noxianArenaBonus.attackDamageFlat = attackDamage[index] * multiplier;
        _noxianArenaBonus.movementSpeedPercentage = moveSpeed[index] * multiplier;
        bool attackSpeedSet = DariusConstellationReflection.SetFloat(_noxianArenaBonus, attackSpeed[index] * multiplier, "attackSpeedPercentage", "attackSpeedPercent");
        _noxianArenaBonus.armorFlat = armor[index] * multiplier;
        _hero.Status.AddStatBonus(_noxianArenaBonus);

        DariusLog.Info("STAR-NOXIAN-ARENA", "level=" + level + " enemies=" + rawCount + " counted=" + count +
            " rank=" + strongest + " scarcityX=" + scarcityMultiplier.ToString("0.##") + " rankX=" + rankMultiplier.ToString("0.##") +
            " totalX=" + multiplier.ToString("0.##") + " flatAD=" + _noxianArenaBonus.attackDamageFlat.ToString("0.#") +
            " MS%=" + _noxianArenaBonus.movementSpeedPercentage.ToString("0.#") + " ASField=" + attackSpeedSet +
            " flatArmor=" + _noxianArenaBonus.armorFlat.ToString("0.##"));
    }

    private IEnumerator DunkmasterRoutine(int level)
    {
        RemoveBonus(ref _dunkmasterBonus);
        if (_hero != null && _hero.Status != null)
        {
            float[] values = { 15f, 20f, 25f, 30f };
            _dunkmasterBonus = new StatBonus();
            _dunkmasterBonus.movementSpeedPercentage = values[Mathf.Clamp(level,1,values.Length)-1];
            _hero.Status.AddStatBonus(_dunkmasterBonus);
            DariusLog.Info("STAR-DUNKMASTER", "R execute granted +" + _dunkmasterBonus.movementSpeedPercentage + "% move speed for 3s.");
        }
        yield return new WaitForSeconds(3f);
        RemoveBonus(ref _dunkmasterBonus);
        _dunkmasterRoutine = null;
    }

    private void OnOwnerDamaged(float damage)
    {
        int level = GetLevel(DariusConstellationIds.LSecondWind);
        if (level <= 0 || _hero == null || _hero.Status == null) return;
        float[] fractions = { 0.04f, 0.05f, 0.06f, 0.07f };
        float missing = Mathf.Max(0f, _hero.Status.maxHealth - _hero.currentHealth);
        float pool = missing * fractions[Mathf.Clamp(level, 1, fractions.Length) - 1];
        _secondWindPool = Mathf.Max(_secondWindPool, pool);
        _nextSecondWindTick = Mathf.Min(_nextSecondWindTick <= 0f ? Time.time : _nextSecondWindTick, Time.time + 0.25f);
        DariusLog.DebugInfo("STAR-SECOND-WIND", "damage=" + damage.ToString("0.##") + " regenPool=" + _secondWindPool.ToString("0.##") + " level=" + level);
    }

    private void TickSecondWind()
    {
        if (_secondWindPool <= 0.01f || Time.time < _nextSecondWindTick || _hero == null || _hero.Status == null) return;
        float tick = Mathf.Min(_secondWindPool, Mathf.Max(0.25f, _secondWindPool * 0.25f));
        float missing = Mathf.Max(0f, _hero.Status.maxHealth - _hero.currentHealth);
        tick = Mathf.Min(tick, missing);
        if (tick > 0.01f)
        {
            HealData heal = new HealData(tick);
            heal.SetActor(_hero);
            _hero.DoHeal(heal, _hero);
        }
        _secondWindPool = Mathf.Max(0f, _secondWindPool - tick);
        _nextSecondWindTick = Time.time + 1f;
    }

    private void RefreshPersistentBonus()
    {
        RemoveBonus(ref _persistentBonus);
        if (_hero == null || _hero.Status == null) return;
        StatBonus bonus = new StatBonus();
        bool used = false;

        int overgrowth = GetLevel(DariusConstellationIds.LOvergrowth);
        if (overgrowth > 0)
        {
            float[] values = { 25f, 32f, 38f, 45f };
            bonus.maxHealthFlat = values[Mathf.Clamp(overgrowth, 1, values.Length) - 1];
            used = true;
        }

        int conditioning = GetLevel(DariusConstellationIds.LConditioning);
        if (conditioning > 0)
        {
            float[] values = { 0.5f, 0.75f, 1f, 1.25f };
            bonus.armorFlat = values[Mathf.Clamp(conditioning, 1, values.Length) - 1];
            used = true;
        }

        int celerity = GetLevel(DariusConstellationIds.FCelerity);
        if (celerity > 0)
        {
            float[] values = { 5f, 6.5f, 8f };
            bonus.movementSpeedPercentage = values[Mathf.Clamp(celerity, 1, values.Length) - 1];
            used = true;
        }

        if (used)
        {
            _persistentBonus = bonus;
            _hero.Status.AddStatBonus(_persistentBonus);
        }
    }

    private void RefreshConquerorBonus()
    {
        RemoveBonus(ref _conquerorBonus);
        int level = GetLevel(DariusConstellationIds.DConqueror);
        if (level <= 0 || _conquerorStacks <= 0 || _hero == null || _hero.Status == null) return;
        float[] perStack = { 1.5f, 2f, 2.5f, 3f };
        _conquerorBonus = new StatBonus();
        _conquerorBonus.attackDamageFlat = perStack[Mathf.Clamp(level, 1, perStack.Length) - 1] * _conquerorStacks;
        _hero.Status.AddStatBonus(_conquerorBonus);
    }

    private void RefreshAlacrityBonus()
    {
        RemoveBonus(ref _alacrityBonus);
        if (_alacrityStacks <= 0 || _hero == null || _hero.Status == null) return;
        _alacrityBonus = new StatBonus();
        bool set = DariusConstellationReflection.SetFloat(_alacrityBonus, 4f * _alacrityStacks, "attackSpeedPercentage", "attackSpeedPercent");
        if (set) _hero.Status.AddStatBonus(_alacrityBonus);
        else _alacrityBonus = null;
    }

    private void RefreshLowHealthBonus(bool force)
    {
        if (_hero == null || _hero.Status == null || _hero.Status.maxHealth <= 0f) return;
        bool low = _hero.currentHealth / _hero.Status.maxHealth <= 0.40f;
        if (!force && low == _lastLowHealth) return;
        _lastLowHealth = low;
        RemoveBonus(ref _lowHealthBonus);
        if (!low) return;

        int lastStand = GetLevel(DariusConstellationIds.DLastStand);
        int unflinching = GetLevel(DariusConstellationIds.LUnflinching);
        if (lastStand <= 0 && unflinching <= 0) return;
        _lowHealthBonus = new StatBonus();
        bool used = false;
        if (lastStand > 0)
        {
            float[] values = { 9f, 11f, 13f, 15f };
            _lowHealthBonus.attackDamageFlat = values[Mathf.Clamp(lastStand, 1, values.Length) - 1];
            used = true;
        }
        if (unflinching > 0)
        {
            float[] values = { 8f, 10f, 12f, 14f };
            _lowHealthBonus.movementSpeedPercentage = values[Mathf.Clamp(unflinching, 1, values.Length) - 1];
            used = true;
        }
        if (used) _hero.Status.AddStatBonus(_lowHealthBonus);
    }

    private void RefreshGatheringStorm(bool force)
    {
        int level = GetLevel(DariusConstellationIds.FGatheringStorm);
        if (level <= 0 || _hero == null || _hero.Status == null)
        {
            if (_gatheringBonus != null) RemoveBonus(ref _gatheringBonus);
            _lastGatheringStep = -1;
            return;
        }
        int heroLevel = DariusConstellationReflection.ReadEntityLevel(_hero);
        int step = Mathf.Max(0, heroLevel / 5);
        if (!force && step == _lastGatheringStep) return;
        _lastGatheringStep = step;
        RemoveBonus(ref _gatheringBonus);
        if (step <= 0) return;
        float[] perStep = { 2.5f, 3f, 3.5f };
        _gatheringBonus = new StatBonus();
        _gatheringBonus.attackDamageFlat = perStep[Mathf.Clamp(level, 1, perStep.Length) - 1] * step;
        _hero.Status.AddStatBonus(_gatheringBonus);
        DariusLog.Info("STAR-GATHERING", "heroLevel=" + heroLevel + " steps=" + step + " flatAD=" + _gatheringBonus.attackDamageFlat);
    }

    private void ScheduleStarterDrops()
    {
        if (_starterRoutine != null) StopCoroutine(_starterRoutine);
        _starterRoutine = StartCoroutine(StarterDropRoutine());
    }

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
            _cosmicRoutines[id] = StartCoroutine(CosmicRoutine(trigger, id, cosmic, spell));
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

    private IEnumerator CosmicRoutine(AbilityTrigger trigger, int triggerId, int level, string spell)
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
        // Unity-destroyed objects compare null, so never derive the cleanup key from trigger here.
        // The instance ID captured at schedule time remains valid for dictionary bookkeeping.
        _cosmicRoutines.Remove(triggerId);
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
        _cosmicRoutines.Clear();
    }
}

public sealed class DariusLegacyApprehendMark : MonoBehaviour
{
    private Hero _owner;
    private float _until;
    public void Activate(Hero owner, float duration) { _owner = owner; _until = Time.time + Mathf.Max(0.1f, duration); }
    public bool IsActiveFor(Hero owner) { return owner != null && _owner == owner && Time.time < _until; }
}
