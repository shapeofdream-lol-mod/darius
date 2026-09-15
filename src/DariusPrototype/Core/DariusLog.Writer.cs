using System;
using System.IO;
using System.Reflection;
using System.Text;
using Mirror;
using UnityEngine;

public static partial class DariusLog
{
    private static bool ShouldSuppress(string level)
    {
        // C# does not define relational operators for enums, so compare the underlying values.
        int minimum = (int)_minimum;
        switch (level)
        {
            case "DEBUG": return minimum > (int)Level.Debug;
            case "INFO": return minimum > (int)Level.Info;
            case "WARN": return minimum > (int)Level.Warn;
            case "ERROR": return minimum > (int)Level.Error;
            default: return false;
        }
    }

    private static void Write(string level, string category, string message, Exception exception)
    {
        if (!_initialized && category != "BOOT") Initialize();
        if (!_initialized) return;

        // EXCEPTION always passes; every other level is filtered by the configured minimum.
        if (!string.Equals(level, "EXCEPTION", StringComparison.Ordinal) && ShouldSuppress(level)) return;

        string authority;
        try
        {
            authority = NetworkServer.active ? "SERVER" : (NetworkClient.active ? "CLIENT" : "LOCAL");
        }
        catch
        {
            authority = "UNKNOWN";
        }

        string line = DateTime.Now.ToString("HH:mm:ss.fff") +
                      " [" + level + "]" +
                      " [" + authority + "]" +
                      " [F" + Time.frameCount + "]" +
                      " [" + category + "] " + message;
        if (exception != null) line += Environment.NewLine + exception;

        // Do not mirror mod diagnostics into UnityEngine.Debug. Unity writes that stream to
        // Player.log. Darius/Gwen diagnostics stay exclusively in the shared game Mods folder,
        // outside each character mod's Workshop upload directory.

        if (_writer == null) return;
        try
        {
            lock (Sync)
            {
                _writer.WriteLine(line);
                _pendingLines++;
                long now = DateTime.UtcNow.Ticks;
                bool urgent = level == "ERROR" || level == "EXCEPTION" || level == "WARN";
                if (urgent || _pendingLines >= 96 || now >= _nextFlushTicks)
                {
                    FlushLocked();
                    _nextFlushTicks = now + TimeSpan.TicksPerSecond * 2;
                }
            }
        }
        catch { }
    }

    private static void FlushLocked()
    {
        if (_writer == null || _pendingLines <= 0) return;
        _writer.Flush();
        _pendingLines = 0;
    }

    private static string ResolveModsRoot()
    {
        // Primary source: Application.dataPath = <game>/Shape of Dreams_Data. The requested
        // destination is always the sibling <game>/Mods directory, not <game>/Mods/DariusPrototype.
        try
        {
            string dataPath = Application.dataPath;
            if (!string.IsNullOrEmpty(dataPath))
            {
                DirectoryInfo dataDir = new DirectoryInfo(dataPath);
                DirectoryInfo gameRoot = dataDir.Parent;
                if (gameRoot != null)
                    return Path.Combine(gameRoot.FullName, "Mods");
            }
        }
        catch { }

        // Conservative fallback for unusual loaders: accept only a resolved character mod
        // whose immediate parent is literally named Mods, then use that parent. Never fall
        // back to persistentDataPath, the game root, Player.log, or the character directory.
        try
        {
            string modRoot = DariusModEnvironment.ResolveRoot();
            if (!string.IsNullOrEmpty(modRoot))
            {
                DirectoryInfo modDir = new DirectoryInfo(modRoot);
                DirectoryInfo parent = modDir.Parent;
                if (parent != null && string.Equals(parent.Name, "Mods", StringComparison.OrdinalIgnoreCase))
                    return parent.FullName;
            }
        }
        catch { }

        return null;
    }
}