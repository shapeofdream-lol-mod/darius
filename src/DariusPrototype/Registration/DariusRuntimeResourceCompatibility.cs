using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Mirror;
using UnityEngine;

// Compatibility layer based on a working runtime-skill mod (Elemental Summon).
// The important distinction from the early Darius prototype is that a runtime resource
// must participate in all of Dew's runtime lookup paths, not just the primary GUID table.
public static partial class DariusRuntimeResourceCompatibility
{
    private static bool _installed;

    public static void ResetInstallState()
    {
        _installed = false;
    }

    public static void Install(Harmony harmony)
    {
        if (_installed) return;
        if (harmony == null) throw new ArgumentNullException(nameof(harmony));
        try
        {
            int requiredPatched = 0;

            requiredPatched += PatchOne(harmony,
                typeof(DewResources).GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                    .FirstOrDefault(m => m.Name == "Load" && m.GetParameters().Length >= 1 &&
                        m.GetParameters()[0].ParameterType == typeof(string)),
                nameof(LoadPrefix), null, "DewResources.Load");

            requiredPatched += PatchOne(harmony,
                typeof(DewResources).GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                    .FirstOrDefault(m => m.Name == "Preload" && m.GetParameters().Length == 1 &&
                        m.GetParameters()[0].ParameterType == typeof(string)),
                nameof(PreloadPrefix), null, "DewResources.Preload");

            requiredPatched += PatchOne(harmony,
                typeof(DewResources).GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                    .FirstOrDefault(m => m.Name == "GetNetworkedPrefab" && m.GetParameters().Length >= 1 &&
                        m.GetParameters()[0].ParameterType == typeof(uint)),
                nameof(GetNetworkedPrefabPrefix), null, "DewResources.GetNetworkedPrefab");

            if (requiredPatched != 3)
                throw new InvalidOperationException("Required runtime resource bridge incomplete: " + requiredPatched + "/3");

            _installed = true;
            DariusLog.Info("RESOURCE-COMPAT",
                "Runtime resource compatibility installed. required=3/3 (Load/Preload/GetNetworkedPrefab only).");
        }
        catch (Exception e)
        {
            _installed = false;
            // Install() is always the first Harmony operation in a ModBehaviour generation.
            // If one required endpoint was patched before a later endpoint failed discovery,
            // remove that partial generation so Start() can either retry cleanly or fail closed.
            try { harmony.UnpatchAll(harmony.Id); } catch { }
            DariusLog.Exception("RESOURCE-COMPAT", e, "Runtime resource compatibility install failed; partial Harmony state was rolled back");
            throw;
        }
    }
}
