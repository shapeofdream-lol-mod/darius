using System;
using System.IO;
using System.Reflection;
using System.Text;
using Mirror;
using UnityEngine;

public static partial class DariusLog
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
}