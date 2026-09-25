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
        _nextHeroDetailLogTime = 0f;
        _nextHeroDetailRefreshTime = 0f;
        _lastHeroDetailWindowId = 0;
    }

    public static void Install(Harmony harmony)
    {
        if (_installed || harmony == null) return;
        try
        {
            int requiredPatched = 0;
            int optionalPatched = 0;

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

            // Name/type/guid resolution is intentionally left to DewResources itself. Darius
            // registers the corresponding Dew DB indexes; only the final runtime-only load/network
            // boundary is intercepted because these objects have no Addressables locations.
            Type heroIconType = AccessTools.TypeByName("UI_HeroIcon");
            MethodInfo heroIconSetup = heroIconType != null
                ? heroIconType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                    .FirstOrDefault(m => m.Name == "Setup" && m.GetParameters().Length >= 1 &&
                        m.GetParameters()[0].ParameterType == typeof(string))
                : null;
            optionalPatched += PatchOne(harmony, heroIconSetup, null, nameof(HeroIconSetupPostfix),
                "UI_HeroIcon.Setup native tint neutralizer");

            Type heroDetailType = AccessTools.TypeByName("UI_InGame_HeroDetailWindow");
            MethodInfo heroDetailUpdate = heroDetailType != null
                ? heroDetailType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                    .FirstOrDefault(m => m.Name == "UpdateText" && m.GetParameters().Length == 0)
                : null;
            optionalPatched += PatchOne(harmony, heroDetailUpdate, nameof(HeroDetailUpdateTextPrefix), null,
                "UI_InGame_HeroDetailWindow.UpdateText");

            MethodInfo skinIncluded = typeof(Dew).GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                .FirstOrDefault(m => m.Name == "IsSkinIncludedInGame" && m.ReturnType == typeof(bool) &&
                    m.GetParameters().Length >= 1 && m.GetParameters()[0].ParameterType == typeof(string));
            optionalPatched += PatchOne(harmony, skinIncluded, null, nameof(IsSkinIncludedPostfix),
                "Dew.IsSkinIncludedInGame");

            if (requiredPatched != 3)
                throw new InvalidOperationException("Required runtime resource bridge incomplete: " + requiredPatched + "/3");

            _installed = true;
            DariusLog.Info("RESOURCE-COMPAT", "Runtime resource compatibility installed. required=3/3 optional=" +
                optionalPatched + " (Load/Preload/GetNetworkedPrefab only; Dew owns name/type/guid resolution).");
        }
        catch (Exception e)
        {
            _installed = false;
            DariusLog.Exception("RESOURCE-COMPAT", e, "Runtime resource compatibility install failed");
            throw;
        }
    }
}
