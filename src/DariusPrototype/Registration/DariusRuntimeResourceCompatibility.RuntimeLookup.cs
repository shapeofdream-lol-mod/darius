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
    private static void NetworkServerSpawnTemplatePrefix(GameObject __0)
    {
        string label;
        if (!DariusTravelerRegistry.TryGetRuntimeNetworkTemplateLabel(__0, out label)) return;
        DariusLog.Warn("TRAVELER-NET-DIAG",
            "NetworkServer.Spawn received the registered Traveler template itself: " + label +
            " stack=" + Environment.StackTrace);
    }

    private static void NetworkServerShutdownPrefix()
    {
        DariusTravelerRegistry.LogRuntimeNetworkTemplateStates("NetworkServer.Shutdown prefix");
    }

    private static void NetworkServerDestroyTemplatePrefix(GameObject __0)
    {
        DariusTravelerRegistry.LogRuntimeNetworkTemplateDestroyBoundary(__0, "NetworkServer.Destroy");
    }

    private static void NetworkClientDestroyTemplatePrefix(object __0, MethodBase __originalMethod)
    {
        UnityEngine.Object candidate = __0 as UnityEngine.Object;
        string name = __originalMethod != null ? __originalMethod.DeclaringType.Name + "." + __originalMethod.Name : "NetworkClient destroy";
        DariusTravelerRegistry.LogRuntimeNetworkTemplateDestroyBoundary(candidate, name);
    }

    private static void SpawnManagerDestroyTemplatePrefix(GameObject __0)
    {
        DariusTravelerRegistry.LogRuntimeNetworkTemplateDestroyBoundary(__0, "SpawnManager.Destroy");
    }

    private static void NetworkIdentityOnDestroyTemplatePrefix(NetworkIdentity __instance)
    {
        DariusTravelerRegistry.LogRuntimeNetworkTemplateDestroyBoundary(__instance, "NetworkIdentity.OnDestroy");
    }

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

    private static bool GetByTypePrefix(Type __0, ref UnityEngine.Object __result)
    {
        UnityEngine.Object obj;
        bool travelerFound = DariusTravelerRegistry.TryGetByType(__0, out obj);
        if (!travelerFound && !DariusFormalRegistry.TryGetResourceByExactType(__0, out obj)) return true;
        __result = obj;
        string typeKey = __0 != null ? __0.FullName : "<null>";
        DariusLog.DebugInfoThrottled("RES-TYPE", typeKey, "GetByType intercepted type=" + typeKey +
            " obj=" + (obj != null ? obj.name : "<null>"), 1.25);
        return false;
    }

}
