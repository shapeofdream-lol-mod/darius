using System;
using System.Reflection;
using HarmonyLib;

// Distinguishes a true DewMod hot-unload from ordinary scene-lifecycle destruction.
// Application.isPlaying is true in both cases, so it is not a sufficient discriminator.
public static class DariusModLifecycle
{
    private static bool _installed;
    private static bool _modManagerUnloading;

    public static bool IsModManagerUnloading => _modManagerUnloading;

    public static void Install(Harmony harmony)
    {
        if (_installed || harmony == null) return;
        try
        {
            Type dewModType = AccessTools.TypeByName("DewMod") ?? AccessTools.TypeByName("Dew.Core.DewMod");
            if (dewModType == null)
            {
                // One-time boot fallback for game builds that move DewMod into a different namespace.
                // This is not a frame/update scan.
                foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    Type candidate = null;
                    try
                    {
                        candidate = assembly.GetType("DewMod", false) ?? assembly.GetType("Dew.Core.DewMod", false);
                        if (candidate == null)
                        {
                            Type[] types = assembly.GetTypes();
                            for (int i = 0; i < types.Length; i++)
                                if (types[i] != null && string.Equals(types[i].Name, "DewMod", StringComparison.Ordinal)) { candidate = types[i]; break; }
                        }
                    }
                    catch { }
                    if (candidate != null) { dewModType = candidate; break; }
                }
            }
            MethodInfo unloadAll = dewModType != null
                ? (AccessTools.Method(dewModType, "UnloadAll", Type.EmptyTypes) ?? AccessTools.Method(dewModType, "UnloadAll"))
                : null;
            if (unloadAll == null)
            {
                DariusLog.Warn("MOD-LIFECYCLE", "DewMod.UnloadAll not found; semantic star dedupe remains active but true-unload detection is unavailable.");
                return;
            }

            HarmonyMethod prefix = new HarmonyMethod(typeof(DariusModLifecycle).GetMethod(nameof(BeforeDewModUnloadAll), BindingFlags.Static | BindingFlags.NonPublic));
            harmony.Patch(unloadAll, prefix: prefix);
            _installed = true;
            DariusLog.Info("MOD-LIFECYCLE", "Installed DewMod.UnloadAll guard; true mod reloads will now fully unregister owned runtime resources.");
        }
        catch (Exception e)
        {
            DariusLog.Exception("MOD-LIFECYCLE", e, "Failed installing DewMod.UnloadAll guard");
        }
    }

    private static void BeforeDewModUnloadAll()
    {
        if (_modManagerUnloading) return;
        _modManagerUnloading = true;
        try { DariusConstellationPersistence.SaveCurrent(DewSave.profileMain, "DewMod.UnloadAll prefix"); } catch { }
        DariusLog.Info("MOD-LIFECYCLE", "DewMod.UnloadAll detected. Current constellation state snapshotted before the old dynamic assembly is destroyed.");
    }
}
