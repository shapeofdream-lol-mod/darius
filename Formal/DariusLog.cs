using System;
using System.IO;
using System.Reflection;
using System.Text;
using Mirror;
using UnityEngine;

public static class DariusLog
{
    private static readonly object Sync = new object();
    private static string _logPath;
    private static bool _initialized;
    private static int _sequence;
    private static StreamWriter _writer;
    private static int _pendingLines;
    private static long _nextFlushTicks;
    private static readonly System.Collections.Generic.Dictionary<string, long> DebugThrottleTicks = new System.Collections.Generic.Dictionary<string, long>();

    public static string LogPath => _logPath;

    // Diagnostic verbosity. Debug is the default so existing behaviour is unchanged; testers can
    // raise the threshold without recompiling by setting the DARIUS_LOG_LEVEL environment
    // variable to debug | info | warn | error | off.
    public enum Level { Debug = 0, Info = 1, Warn = 2, Error = 3, Off = 4 }
    private static Level _minimum = Level.Debug;
    public static Level Minimum { get { return _minimum; } set { _minimum = value; } }

    public static void Initialize()
    {
        if (_initialized) return;

        string configuredLevel = Environment.GetEnvironmentVariable("DARIUS_LOG_LEVEL");
        if (!string.IsNullOrEmpty(configuredLevel))
        {
            try { _minimum = (Level)Enum.Parse(typeof(Level), configuredLevel.Trim(), true); }
            catch { }
        }

        try
        {
            string modsRoot = ResolveModsRoot();
            // Workshop-safe rule: runtime diagnostics belong in the game's shared Mods
            // directory, never inside this character mod's own folder. Keeping the live log
            // handle one level above the upload payload lets the Workshop publisher read the
            // character directory while the game is still running.
            if (string.IsNullOrEmpty(modsRoot)) return;
            Directory.CreateDirectory(modsRoot);
            _logPath = Path.Combine(modsRoot, "DariusPrototype_runtime.log");
            string previous = Path.Combine(modsRoot, "DariusPrototype_runtime.previous.log");
            try
            {
                if (File.Exists(previous)) File.Delete(previous);
                if (File.Exists(_logPath)) File.Move(_logPath, previous);
            }
            catch { }

            // rc6: keep one buffered file handle instead of File.AppendAllText for every combat/UI
            // line. The old path reopened the file synchronously thousands of times during a run.
            FileStream stream = new FileStream(_logPath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite, 65536, FileOptions.SequentialScan);
            _writer = new StreamWriter(stream, new UTF8Encoding(false), 65536);
            _writer.AutoFlush = false;
            _initialized = true;
            _writer.WriteLine("=== DariusPrototype runtime diagnostic log ===");
            _writer.WriteLine("Started: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"));
            _writer.WriteLine("Unity: " + Application.unityVersion);
            _writer.WriteLine("Game data path: " + Application.dataPath);
            _writer.WriteLine("Assembly: " + Assembly.GetExecutingAssembly().Location);
            _writer.WriteLine("Process: " + System.Diagnostics.Process.GetCurrentProcess().ProcessName + " pid=" + System.Diagnostics.Process.GetCurrentProcess().Id);
            _writer.WriteLine("================================================");
            _writer.Flush();
            _pendingLines = 0;
            _nextFlushTicks = DateTime.UtcNow.Ticks + TimeSpan.TicksPerSecond * 2;
        }
        catch (Exception e)
        {
            try { if (_writer != null) _writer.Dispose(); } catch { }
            _writer = null;
            _initialized = false;
            _logPath = null;
            return;
        }

        Info("BOOT", "Runtime logger initialized. File=" + (_logPath ?? "<unavailable>") + " mode=buffered");
    }

    public static int NextId()
    {
        return System.Threading.Interlocked.Increment(ref _sequence);
    }

    public static void DebugInfo(string category, string message) => Write("DEBUG", category, message, null);

    public static void DebugInfoThrottled(string category, string key, string message, double minSeconds = 1.0)
    {
        string token = (category ?? string.Empty) + "\n" + (key ?? string.Empty);
        long now = DateTime.UtcNow.Ticks;
        long interval = (long)(Math.Max(0.05, minSeconds) * TimeSpan.TicksPerSecond);
        bool write = false;
        lock (Sync)
        {
            long next;
            if (!DebugThrottleTicks.TryGetValue(token, out next) || now >= next)
            {
                DebugThrottleTicks[token] = now + interval;
                write = true;
            }
        }
        if (write) Write("DEBUG", category, message, null);
    }

    public static void Info(string category, string message) => Write("INFO", category, message, null);
    public static void Warn(string category, string message) => Write("WARN", category, message, null);
    public static void Error(string category, string message) => Write("ERROR", category, message, null);
    public static void Exception(string category, Exception exception, string context = null)
    {
        Write("EXCEPTION", category, (context ?? "Unhandled exception") + ": " + exception.Message, exception);
    }

    public static void Flush()
    {
        lock (Sync)
        {
            FlushLocked();
        }
    }

    public static void Shutdown()
    {
        lock (Sync)
        {
            try { FlushLocked(); } catch { }
            try { if (_writer != null) _writer.Dispose(); } catch { }
            _writer = null;
            _pendingLines = 0;
        }
    }

    public static string EntityLabel(object value)
    {
        if (value == null) return "<null>";
        Entity entity = value as Entity;
        if (entity != null)
        {
            try
            {
                return entity.name + "#" + entity.GetInstanceID() +
                       " hp=" + entity.currentHealth.ToString("0.##") +
                       " pos=" + Vec(entity.transform.position);
            }
            catch
            {
                return entity.name + "#" + entity.GetInstanceID();
            }
        }

        UnityEngine.Object unityObject = value as UnityEngine.Object;
        if (unityObject != null)
            return unityObject.name + "#" + unityObject.GetInstanceID() + " type=" + value.GetType().Name;

        return value.GetType().Name + ":" + value.ToString();
    }

    public static string Vec(Vector3 v)
    {
        return "(" + v.x.ToString("0.00") + "," + v.y.ToString("0.00") + "," + v.z.ToString("0.00") + ")";
    }

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
