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
}

public sealed class DariusLegacyApprehendMark : MonoBehaviour
{
    private Hero _owner;
    private float _until;
    public void Activate(Hero owner, float duration) { _owner = owner; _until = Time.time + Mathf.Max(0.1f, duration); }
    public bool IsActiveFor(Hero owner) { return owner != null && _owner == owner && Time.time < _until; }
}