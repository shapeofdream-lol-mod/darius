using System;
using System.Text;
using Mirror;
using UnityEngine;

// High-level flight recorder for first-pass in-game testing.
// Keeps the log useful even if a failure happens outside one of the skill try/catch blocks.
public sealed class DariusDiagnostics : MonoBehaviour
{
    private float _nextHeartbeat;
    private bool _lastServer;
    private bool _lastClient;
    private int _lastHeroId;
    private bool _unityHooked;

    public void Initialize()
    {
        if (!_unityHooked)
        {
            Application.logMessageReceivedThreaded += OnUnityLog;
            _unityHooked = true;
        }

        _lastServer = SafeServerActive();
        _lastClient = SafeClientActive();
        _lastHeroId = GetHeroId();
        _nextHeartbeat = Time.unscaledTime + 3f;
        DariusLog.Info("DIAG", "Diagnostics initialized. " + Snapshot());
    }

    private void Update()
    {
        bool server = SafeServerActive();
        bool client = SafeClientActive();
        int heroId = GetHeroId();

        if (server != _lastServer || client != _lastClient)
        {
            DariusLog.Info("DIAG-NET", "Network state changed server=" + _lastServer + "->" + server +
                " client=" + _lastClient + "->" + client);
            _lastServer = server;
            _lastClient = client;
        }

        if (heroId != _lastHeroId)
        {
            DariusLog.Info("DIAG-HERO", "Local hero changed instanceId=" + _lastHeroId + "->" + heroId +
                " hero=" + DariusLog.EntityLabel(DewPlayer.local != null ? DewPlayer.local.hero : null));
            _lastHeroId = heroId;
        }

        if (Time.unscaledTime >= _nextHeartbeat)
        {
            _nextHeartbeat = Time.unscaledTime + 30f;
            DariusLog.DebugInfo("HEARTBEAT", Snapshot());
        }
    }

    private void OnDestroy()
    {
        if (_unityHooked)
        {
            Application.logMessageReceivedThreaded -= OnUnityLog;
            _unityHooked = false;
        }
        DariusLog.Info("DIAG", "Diagnostics destroyed.");
    }

    private static void OnUnityLog(string condition, string stackTrace, LogType type)
    {
        // DariusLog itself writes through Unity's logger. Ignore those messages to prevent recursion/duplication.
        if (!string.IsNullOrEmpty(condition) && condition.StartsWith("[DariusPrototype]", StringComparison.Ordinal))
            return;

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
            sb.Append("server=").Append(NetworkServer.active)
              .Append(" client=").Append(NetworkClient.active)
              .Append(" frame=").Append(Time.frameCount)
              .Append(" t=").Append(Time.time.ToString("0.00"))
              .Append(" unscaled=").Append(Time.unscaledTime.ToString("0.00"));
        }
        catch (Exception e)
        {
            sb.Append("network/time snapshot failed: ").Append(e.Message);
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
