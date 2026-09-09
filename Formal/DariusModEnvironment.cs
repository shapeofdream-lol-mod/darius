using System;
using System.IO;
using System.Reflection;
using UnityEngine;

// Resolves the physical mod directory in a source-agnostic way.
// Local Mods use .../Mods/DariusPrototype, while Steam Workshop items live under a numeric
// published-file folder. The loader's ModItem.path is authoritative when available.
public static class DariusModEnvironment
{
    private static string _configuredRoot;
    private static string _sourceLabel = "unconfigured";

    public static string Root
    {
        get { return ResolveRoot(); }
    }

    public static string SourceLabel
    {
        get { return _sourceLabel; }
    }

    public static void Configure(ModBehaviour behaviour)
    {
        try
        {
            // Use reflection for the loader metadata contract so this early-Awake path does not take
            // a hard compile/runtime dependency on the concrete LoadedModInstance/ModItem classes.
            // Current SoD exposes ModItem.path/source/publishedFileId, and older/local loaders may
            // expose the ModItem directly as ModBehaviour.mod instead of instance.mod.
            object loaded = ReadMember(behaviour, "instance");
            object item = ReadMember(loaded, "mod") ?? ReadMember(behaviour, "mod");
            if (item != null)
            {
                string rawPath = Convert.ToString(ReadMember(item, "path"));
                string candidate = NormalizeRoot(rawPath);
                object source = ReadMember(item, "source");
                object published = ReadMember(item, "publishedFileId");
                string label = (source != null ? source.ToString() : "unknown-source") +
                               " publishedFileId=" + (published != null ? published.ToString() : "<none>");
                if (IsModRoot(candidate))
                {
                    _configuredRoot = candidate;
                    _sourceLabel = label;
                    return;
                }
                _sourceLabel = label + " path-invalid=" + (rawPath ?? "<null>");
            }
        }
        catch (Exception e)
        {
            _sourceLabel = "ModItem.path unavailable: " + e.GetType().Name;
        }

        // Do not fail the mod if Awake occurs before the loader has populated ModItem. ResolveRoot()
        // has conservative local/assembly fallbacks and Start() calls Configure() a second time.
        ResolveRoot();
    }

    public static string ResolveRoot()
    {
        if (IsModRoot(_configuredRoot)) return _configuredRoot;

        // Prefer the directory that physically contains the executing Workshop/local assembly. This
        // prevents a disabled/stale local Mods/DariusPrototype folder from stealing assets when the
        // enabled copy is actually running from a numeric Steam Workshop directory.
        try
        {
            string assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            DirectoryInfo current = !string.IsNullOrEmpty(assemblyDir) ? new DirectoryInfo(assemblyDir) : null;
            for (int i = 0; i < 8 && current != null; i++, current = current.Parent)
            {
                if (IsModRoot(current.FullName))
                {
                    _configuredRoot = current.FullName;
                    if (_sourceLabel == "unconfigured") _sourceLabel = "assembly ancestor fallback";
                    return _configuredRoot;
                }
            }
        }
        catch { }

        // Final compatibility fallback for a classic local install whose compiled assembly is loaded
        // from a cache outside its source folder.
        try
        {
            string direct = NormalizeRoot(Path.Combine(Application.dataPath, "..", "Mods", "DariusPrototype"));
            if (IsModRoot(direct))
            {
                _configuredRoot = direct;
                if (_sourceLabel == "unconfigured") _sourceLabel = "local Mods fallback";
                return _configuredRoot;
            }
        }
        catch { }

        return null;
    }

    private static object ReadMember(object owner, string name)
    {
        if (owner == null || string.IsNullOrEmpty(name)) return null;
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        try
        {
            Type t = owner.GetType();
            PropertyInfo p = t.GetProperty(name, flags);
            if (p != null && p.GetIndexParameters().Length == 0) return p.GetValue(owner, null);
            FieldInfo f = t.GetField(name, flags);
            if (f != null) return f.GetValue(owner);
        }
        catch { }
        return null;
    }

    private static bool IsModRoot(string path)
    {
        if (string.IsNullOrEmpty(path)) return false;
        try
        {
            if (!Directory.Exists(path) || !Directory.Exists(Path.Combine(path, "assets"))) return false;
            string about = Path.Combine(path, "about");
            bool hasAboutIdentity = File.Exists(Path.Combine(about, "publishedfileid.txt")) ||
                                    File.Exists(Path.Combine(about, "description.txt"));
            bool hasProjectIdentity = File.Exists(Path.Combine(path, "DariusPrototype.dll")) ||
                                      File.Exists(Path.Combine(path, "DariusPrototype.csproj"));
            return hasAboutIdentity && hasProjectIdentity;
        }
        catch { return false; }
    }

    private static string NormalizeRoot(string path)
    {
        if (string.IsNullOrEmpty(path)) return null;
        try { return Path.GetFullPath(path); }
        catch { return path; }
    }
}
