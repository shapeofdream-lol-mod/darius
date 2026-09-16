using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

// Native Unity model path. AssetBundle calls are reflected only to keep the repository reference
// pack independent from UnityEngine.AssetBundleModule; the actual runtime implementation remains
// Unity's native AssetBundle API and is touched only during bundle/prefab load and teardown.
internal static class DariusNativeModelAssets
{
    public const string BundleFileName = "darius_models.bundle";

    private static object _bundle;
    private static Type _bundleType;
    private static bool _loadAttempted;
    private static string[] _assetNames;
    private static readonly Dictionary<string, GameObject> Prefabs =
        new Dictionary<string, GameObject>(StringComparer.OrdinalIgnoreCase);

    public static bool TryActivate(DariusTravelerModelInstance legacy)
    {
        if (legacy == null) return false;
        DariusNativeModelBridge existing = legacy.GetComponent<DariusNativeModelBridge>();
        if (existing != null && existing.IsReady) return true;

        DariusSkinModelBinding binding = legacy.GetComponent<DariusSkinModelBinding>();
        if (binding == null || string.IsNullOrEmpty(binding.variantKey)) return false;
        GameObject prefab = GetPrefab(binding.variantKey);
        if (prefab == null) return false;

        GameObject instance = null;
        DariusNativeModelBridge bridge = existing;
        bool bridgeAdded = false;
        try
        {
            RemoveStaleNativeRoots(legacy.transform);
            instance = UnityEngine.Object.Instantiate(prefab, legacy.transform, false);
            instance.name = "Darius_Native_Model";
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;

            if (bridge == null)
            {
                bridge = legacy.gameObject.AddComponent<DariusNativeModelBridge>();
                bridgeAdded = true;
            }

            bridge.Initialize(instance, binding);
            legacy.enabled = false;
            DariusLog.Info("NATIVE-MODEL", "Activated Unity AssetBundle model skin=" + binding.variantKey +
                " prefab=" + prefab.name + "; legacy GLB interpreter disabled.");
            return true;
        }
        catch (Exception e)
        {
            if (instance != null)
            {
                try { instance.SetActive(false); } catch { }
                try { UnityEngine.Object.Destroy(instance); } catch { }
            }
            if (bridgeAdded && bridge != null)
            {
                try { UnityEngine.Object.Destroy(bridge); } catch { }
            }
            DariusLog.Exception("NATIVE-MODEL", e,
                "Native model activation failed for skin=" + binding.variantKey + "; legacy GLB fallback remains available");
            return false;
        }
    }

    private static GameObject GetPrefab(string variantKey)
    {
        GameObject cached;
        if (Prefabs.TryGetValue(variantKey, out cached)) return cached;
        if (!EnsureBundle()) return null;

        string suffix = "/darius_" + variantKey.Replace(" ", string.Empty).ToLowerInvariant() + ".prefab";
        string assetName = null;
        for (int i = 0; i < _assetNames.Length; i++)
        {
            string candidate = _assetNames[i];
            if (candidate != null && candidate.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                assetName = candidate;
                break;
            }
        }
        if (assetName == null)
        {
            DariusLog.Error("NATIVE-MODEL", "AssetBundle has no prefab suffix=" + suffix);
            Prefabs[variantKey] = null;
            return null;
        }

        MethodInfo loadAsset = RequireBundleMethod(
            "LoadAsset",
            BindingFlags.Public | BindingFlags.Instance,
            new[] { typeof(string), typeof(Type) });
        GameObject prefab = loadAsset.Invoke(_bundle, new object[] { assetName, typeof(GameObject) }) as GameObject;
        if (prefab == null)
            DariusLog.Error("NATIVE-MODEL", "AssetBundle prefab load returned null asset=" + assetName);
        Prefabs[variantKey] = prefab;
        return prefab;
    }

    private static bool EnsureBundle()
    {
        if (_bundle != null) return true;
        if (_loadAttempted) return false;
        _loadAttempted = true;

        string root = DariusMedia.Root;
        string path = !string.IsNullOrEmpty(root) ? Path.Combine(root, "assets", "models", BundleFileName) : null;
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            DariusLog.Info("NATIVE-MODEL",
                "Native model bundle not present; using optimized legacy GLB fallback. expected=" + (path ?? "<null>"));
            return false;
        }

        try
        {
            _bundleType = Type.GetType("UnityEngine.AssetBundle, UnityEngine.AssetBundleModule", false);
            if (_bundleType == null)
                throw new TypeLoadException("UnityEngine.AssetBundle was not found in UnityEngine.AssetBundleModule.");

            MethodInfo loadFromFile = RequireBundleMethod(
                "LoadFromFile",
                BindingFlags.Public | BindingFlags.Static,
                new[] { typeof(string) });
            _bundle = loadFromFile.Invoke(null, new object[] { path });
            if (_bundle == null) throw new InvalidOperationException("AssetBundle.LoadFromFile returned null path=" + path);

            MethodInfo getNames = RequireBundleMethod("GetAllAssetNames", BindingFlags.Public | BindingFlags.Instance, Type.EmptyTypes);
            _assetNames = getNames.Invoke(_bundle, null) as string[] ?? Array.Empty<string>();
            DariusLog.Info("NATIVE-MODEL", "Loaded native Unity model bundle assets=" + _assetNames.Length + " path=" + path);
            return true;
        }
        catch (Exception e)
        {
            _bundle = null;
            _bundleType = null;
            DariusLog.Exception("NATIVE-MODEL", e, "Failed loading Unity model AssetBundle path=" + path);
            return false;
        }
    }

    public static void Unload()
    {
        DariusNativeEntityAnimationLease.Clear();
        Prefabs.Clear();
        _assetNames = null;
        _loadAttempted = false;
        if (_bundle != null)
        {
            try
            {
                MethodInfo unload = RequireBundleMethod(
                    "Unload",
                    BindingFlags.Public | BindingFlags.Instance,
                    new[] { typeof(bool) });
                unload.Invoke(_bundle, new object[] { false });
            }
            catch { }
        }
        _bundle = null;
        _bundleType = null;
    }

    private static MethodInfo RequireBundleMethod(string name, BindingFlags flags, Type[] parameterTypes)
    {
        if (_bundleType == null) throw new TypeLoadException("Unity AssetBundle type is unavailable.");
        MethodInfo method = _bundleType.GetMethod(name, flags, null, parameterTypes, null);
        if (method == null) throw new MissingMethodException(_bundleType.FullName, name);
        return method;
    }

    private static void RemoveStaleNativeRoots(Transform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            Transform child = parent.GetChild(i);
            if (child == null || !string.Equals(child.name, "Darius_Native_Model", StringComparison.Ordinal)) continue;
            try { child.gameObject.SetActive(false); } catch { }
            try { UnityEngine.Object.Destroy(child.gameObject); } catch { }
        }
    }
}
