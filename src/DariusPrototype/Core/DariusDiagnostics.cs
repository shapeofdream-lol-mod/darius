using System;
using System.Collections;
using System.Text;
using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;

// High-level flight recorder for first-pass in-game testing.
// Keeps the log useful even if a failure happens outside one of the skill try/catch blocks.
public sealed class DariusDiagnostics : MonoBehaviour
{
    private float _nextHeartbeat;
    private bool _lastServer;
    private bool _lastClient;
    private int _lastHeroId;
    private bool _unityHooked;
    private bool _sceneHooked;
    private int _sceneSnapshotToken;
    private bool _templateStateInitialized;
    private bool _lastHeroTemplateAlive;
    private bool _lastAttackTemplateAlive;
    private bool _lastAttackAiTemplateAlive;
    private bool _lastAttackCritTemplateAlive;

    public void Initialize()
    {
        if (!_unityHooked)
        {
            Application.logMessageReceivedThreaded += OnUnityLog;
            _unityHooked = true;
        }
        if (!_sceneHooked)
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
            SceneManager.activeSceneChanged += OnActiveSceneChanged;
            _sceneHooked = true;
        }

        _lastServer = SafeServerActive();
        _lastClient = SafeClientActive();
        _lastHeroId = GetHeroId();
        CaptureTemplateAliveState();
        _nextHeartbeat = Time.unscaledTime + 3f;
        DariusLog.Info("DIAG", "Diagnostics initialized. " + Snapshot());
        ScheduleSceneSnapshots("diagnostics initialize");
    }

    private void Update()
    {
        bool server = SafeServerActive();
        bool client = SafeClientActive();
        int heroId = GetHeroId();

        LogTemplateLifetimeTransitions();

        if (server != _lastServer || client != _lastClient)
        {
            DariusLog.Info("DIAG-NET", "Network state changed server=" + _lastServer + "->" + server +
                " client=" + _lastClient + "->" + client);
            _lastServer = server;
            _lastClient = client;
            DariusRuntimeAudit.LogSnapshot("network state changed", false);
        }

        if (heroId != _lastHeroId)
        {
            DariusLog.Info("DIAG-HERO", "Local hero changed instanceId=" + _lastHeroId + "->" + heroId +
                " hero=" + DariusLog.EntityLabel(DewPlayer.local != null ? DewPlayer.local.hero : null));
            _lastHeroId = heroId;
            DariusRuntimeAudit.LogSnapshot("local hero changed", true);
        }

        if (Time.unscaledTime >= _nextHeartbeat)
        {
            _nextHeartbeat = Time.unscaledTime + 30f;
            DariusLog.DebugInfo("HEARTBEAT", Snapshot());
            DariusRuntimeAudit.LogSnapshot("30s heartbeat", false);
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        DariusLog.Info("DIAG-SCENE", "sceneLoaded scene=" + DescribeScene(scene) + " mode=" + mode +
            " active=" + DescribeScene(SceneManager.GetActiveScene()));
        ScheduleSceneSnapshots("sceneLoaded " + scene.name + "#" + scene.handle);
    }

    private void OnSceneUnloaded(Scene scene)
    {
        DariusLog.Info("DIAG-SCENE", "sceneUnloaded scene=" + DescribeScene(scene) +
            " active=" + DescribeScene(SceneManager.GetActiveScene()));
        ScheduleSceneSnapshots("sceneUnloaded " + scene.name + "#" + scene.handle);
    }

    private void OnActiveSceneChanged(Scene oldScene, Scene newScene)
    {
        DariusLog.Info("DIAG-SCENE", "activeSceneChanged old=" + DescribeScene(oldScene) +
            " new=" + DescribeScene(newScene));
        ScheduleSceneSnapshots("activeSceneChanged " + oldScene.name + "->" + newScene.name);
    }

    private void ScheduleSceneSnapshots(string reason)
    {
        int token = ++_sceneSnapshotToken;
        StartCoroutine(SceneSnapshotRoutine(token, reason));
    }

    private IEnumerator SceneSnapshotRoutine(int token, string reason)
    {
        int[] beats = { 1, 10, 60 };
        int frame = 0;
        int beatIndex = 0;
        while (beatIndex < beats.Length)
        {
            if (token != _sceneSnapshotToken) yield break;
            if (frame >= beats[beatIndex])
            {
                DariusRuntimeAudit.LogSnapshot(reason + " +" + beats[beatIndex] + "f", true);
                beatIndex++;
            }
            frame++;
            yield return null;
        }
    }

    private void OnDestroy()
    {
        if (_unityHooked)
        {
            Application.logMessageReceivedThreaded -= OnUnityLog;
            _unityHooked = false;
        }
        if (_sceneHooked)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
            _sceneHooked = false;
        }
        _sceneSnapshotToken++;
        DariusLog.Info("DIAG", "Diagnostics destroyed generation=" + DariusPrototypeMod.ActiveGenerationId +
            " owner=" + DariusPrototypeMod.ActiveModInstanceId + ".");
    }

    private static void OnUnityLog(string condition, string stackTrace, LogType type)
    {
        // DariusLog writes directly to its own file and does not mirror ordinary diagnostics into
        // UnityEngine.Debug. Capture only genuine Unity errors/asserts/exceptions here.
        if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert)
            return;

        try
        {
            string msg = "Unity " + type + ": " + (condition ?? "<null>");
            if (!string.IsNullOrEmpty(stackTrace)) msg += Environment.NewLine + stackTrace;
            DariusLog.Error("UNITY", msg);
        }
        catch { }
    }

    public static string Snapshot()
    {
        StringBuilder sb = new StringBuilder();
        try
        {
            sb.Append("generation=").Append(DariusPrototypeMod.ActiveGenerationId)
              .Append(" modOwner=").Append(DariusPrototypeMod.ActiveModInstanceId)
              .Append(" scene=").Append(DescribeScene(SceneManager.GetActiveScene()))
              .Append(" server=").Append(NetworkServer.active)
              .Append(" client=").Append(NetworkClient.active)
              .Append(" frame=").Append(Time.frameCount)
              .Append(" t=").Append(Time.time.ToString("0.00"))
              .Append(" unscaled=").Append(Time.unscaledTime.ToString("0.00"));
        }
        catch (Exception e)
        {
            sb.Append(" network/time snapshot failed: ").Append(e.Message);
        }

        try
        {
            sb.Append(" player=").Append(DewPlayer.local != null ? DewPlayer.local.name : "<null>")
              .Append(" hero=").Append(DariusLog.EntityLabel(DewPlayer.local != null ? DewPlayer.local.hero : null));
        }
        catch (Exception e)
        {
            sb.Append(" player snapshot failed: ").Append(e.Message);
        }

        try
        {
            sb.Append(" resourceDB=").Append(DewResources.database != null ? "ready" : "null");
        }
        catch (Exception e)
        {
            sb.Append(" resourceDB snapshot failed: ").Append(e.Message);
        }

        return sb.ToString();
    }

    private static string DescribeScene(Scene scene)
    {
        try
        {
            return (scene.IsValid() ? scene.name : "<invalid>") + "#" + scene.handle + "(loaded=" + scene.isLoaded + ")";
        }
        catch { return "<scene-error>"; }
    }

    private void CaptureTemplateAliveState()
    {
        _lastHeroTemplateAlive = IsAlive(DariusTravelerRegistry.HeroPrefab);
        _lastAttackTemplateAlive = IsAlive(DariusTravelerRegistry.AttackPrefab);
        _lastAttackAiTemplateAlive = IsAlive(DariusTravelerRegistry.AttackInstancePrefab);
        _lastAttackCritTemplateAlive = IsAlive(DariusTravelerRegistry.AttackCritInstancePrefab);
        _templateStateInitialized = true;
    }

    private void LogTemplateLifetimeTransitions()
    {
        bool hero = IsAlive(DariusTravelerRegistry.HeroPrefab);
        bool attack = IsAlive(DariusTravelerRegistry.AttackPrefab);
        bool attackAi = IsAlive(DariusTravelerRegistry.AttackInstancePrefab);
        bool crit = IsAlive(DariusTravelerRegistry.AttackCritInstancePrefab);

        if (!_templateStateInitialized)
        {
            CaptureTemplateAliveState();
            return;
        }

        if (hero == _lastHeroTemplateAlive &&
            attack == _lastAttackTemplateAlive &&
            attackAi == _lastAttackAiTemplateAlive &&
            crit == _lastAttackCritTemplateAlive)
            return;

        DariusLog.Warn("TEMPLATE-LIFETIME",
            "Traveler template alive transition frame=" + Time.frameCount +
            " scene=" + DescribeScene(SceneManager.GetActiveScene()) +
            " server=" + SafeServerActive() +
            " client=" + SafeClientActive() +
            " hero=" + _lastHeroTemplateAlive + "->" + hero +
            " attack=" + _lastAttackTemplateAlive + "->" + attack +
            " attackAi=" + _lastAttackAiTemplateAlive + "->" + attackAi +
            " critAi=" + _lastAttackCritTemplateAlive + "->" + crit +
            " core={" + DariusTravelerRegistry.PipelineCoreState() + "}");

        _lastHeroTemplateAlive = hero;
        _lastAttackTemplateAlive = attack;
        _lastAttackAiTemplateAlive = attackAi;
        _lastAttackCritTemplateAlive = crit;
    }

    private static bool IsAlive(UnityEngine.Object obj)
    {
        return !ReferenceEquals(obj, null) && obj != null;
    }

    private static int GetHeroId()
    {
        try { return DewPlayer.local != null && DewPlayer.local.hero != null ? DewPlayer.local.hero.GetInstanceID() : 0; }
        catch { return 0; }
    }

    private static bool SafeServerActive()
    {
        try { return NetworkServer.active; }
        catch { return false; }
    }

    private static bool SafeClientActive()
    {
        try { return NetworkClient.active; }
        catch { return false; }
    }
}
