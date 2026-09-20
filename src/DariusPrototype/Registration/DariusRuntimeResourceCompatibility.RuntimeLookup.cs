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
    private static Component FindComponentByTypeName(GameObject go, string typeName)
    {
        if (go == null || string.IsNullOrEmpty(typeName)) return null;
        try
        {
            Component[] components = go.GetComponents<Component>();
            for (int i = 0; i < components.Length; i++)
            {
                Component c = components[i];
                if (c != null && string.Equals(c.GetType().Name, typeName, StringComparison.Ordinal)) return c;
            }
        }
        catch { }
        return null;
    }

    private static float ReadFloatMember(object obj, string name, float fallback)
    {
        if (obj == null) return fallback;
        try
        {
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            Type t = obj.GetType();
            PropertyInfo p = t.GetProperty(name, flags);
            if (p != null) return Convert.ToSingle(p.GetValue(obj, null));
            FieldInfo f = t.GetField(name, flags);
            if (f != null) return Convert.ToSingle(f.GetValue(obj));
        }
        catch { }
        return fallback;
    }

    // DewResources.Load(string, ResourceLoadSettings): the working reference mod intercepts
    // this exact lookup path and returns its runtime prefab instead of asking Addressables.
    private static bool LoadPrefix(string __0, ref UnityEngine.Object __result)
    {
        UnityEngine.Object obj;
        bool travelerFound = DariusTravelerRegistry.TryLoad(__0, out obj);
        if (!travelerFound && DariusTravelerRegistry.IsRuntimeKey(__0))
        {
            // First-run teardown can destroy Hero_Darius before a scene callback fires. Never let a
            // known runtime GUID fall through to Addressables: repair/rebuild it synchronously and retry.
            DariusTravelerRegistry.RepairRuntimeRegistration("DewResources.Load self-heal key=" + __0);
            travelerFound = DariusTravelerRegistry.TryLoad(__0, out obj);
        }
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
        bool travelerAssetId = __0 == DariusTravelerRegistry.HeroAssetId ||
                               __0 == DariusTravelerRegistry.AttackAssetId ||
                               __0 == DariusTravelerRegistry.AttackInstanceAssetId ||
                               __0 == DariusTravelerRegistry.AttackCritInstanceAssetId;
        if (!travelerFound && travelerAssetId)
        {
            DariusTravelerRegistry.RepairRuntimeRegistration("DewResources.GetNetworkedPrefab self-heal assetId=" + __0);
            travelerFound = DariusTravelerRegistry.TryGetNetworkPrefab(__0, out prefab);
        }
        if (!travelerFound && !DariusFormalRegistry.TryGetNetworkPrefab(__0, out prefab)) return true;
        __result = prefab;
        DariusLog.DebugInfoThrottled("RES-NET", __0.ToString(), "GetNetworkedPrefab intercepted assetId=" + __0 + " prefab=" + prefab.name, 1.0);
        return false;
    }

    private static bool GetByTypePrefix(Type __0, ref UnityEngine.Object __result)
    {
        UnityEngine.Object obj;
        bool travelerFound = DariusTravelerRegistry.TryGetByType(__0, out obj);
        bool travelerType = __0 == typeof(Hero_Darius) || __0 == typeof(At_DariusAxe) ||
                            __0 == typeof(Ai_DariusAxe) || __0 == typeof(Ai_DariusAxe_Crit);
        if (!travelerFound && travelerType)
        {
            DariusTravelerRegistry.RepairRuntimeRegistration("DewResources.GetByType self-heal type=" + (__0 != null ? __0.FullName : "<null>"));
            travelerFound = DariusTravelerRegistry.TryGetByType(__0, out obj);
        }
        if (!travelerFound && !DariusFormalRegistry.TryGetResourceByExactType(__0, out obj)) return true;
        __result = obj;
        string typeKey = __0 != null ? __0.FullName : "<null>";
        DariusLog.DebugInfoThrottled("RES-TYPE", typeKey, "GetByType intercepted type=" + typeKey +
            " obj=" + (obj != null ? obj.name : "<null>"), 1.25);
        return false;
    }

}
