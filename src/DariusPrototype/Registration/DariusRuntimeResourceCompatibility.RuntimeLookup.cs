using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Mirror;
using UnityEngine;

public static partial class DariusRuntimeResourceCompatibility
{
    // DewResources.Load(string, ResourceLoadSettings): the working reference mod intercepts
    // this exact lookup path and returns its runtime prefab instead of asking Addressables.
    private static bool LoadPrefix(string __0, ref UnityEngine.Object __result)
    {
        UnityEngine.Object obj;
        bool travelerFound = DariusTravelerRegistry.TryLoad(__0, out obj);
        if (!travelerFound && !DariusFormalRegistry.TryLoad(__0, out obj)) return true;
        Component component = obj as Component;
        __result = component != null ? (UnityEngine.Object)component.gameObject : obj;
        if (string.IsNullOrEmpty(__0) || __0.IndexOf("-star-", StringComparison.Ordinal) < 0)
            DariusLog.DebugInfoThrottled("RES-LOAD", __0, "Runtime Load intercepted key=" + __0 + " result=" + (__result != null ? __result.name : "<null>"), 1.25);
        return false;
    }

    // DewResources.Preload goes straight to Addressables and therefore bypasses Load().
    // Runtime-only prefabs have no Addressables location; skipping the warm-up is safe because
    // Load/GetByType/GetNetworkedPrefab are intercepted below.
    private static bool PreloadPrefix(string __0)
    {
        if (!DariusTravelerRegistry.IsRuntimeKey(__0) && !DariusFormalRegistry.IsDariusResourceKey(__0)) return true;
        DariusLog.DebugInfoThrottled("RES-PRELOAD", __0, "Skipped Addressables preload for runtime Darius key=" + __0, 1.5);
        return false;
    }

    private static bool GetNetworkedPrefabPrefix(uint __0, ref GameObject __result)
    {
        GameObject prefab;
        bool travelerFound = DariusTravelerRegistry.TryGetNetworkPrefab(__0, out prefab);
        if (!travelerFound && !DariusFormalRegistry.TryGetNetworkPrefab(__0, out prefab)) return true;
        __result = prefab;
        DariusLog.DebugInfoThrottled("RES-NET", __0.ToString(), "GetNetworkedPrefab intercepted assetId=" + __0 + " prefab=" + prefab.name, 1.0);
        return false;
    }


}
