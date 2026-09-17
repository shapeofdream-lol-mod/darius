using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

internal static partial class DariusNativeModelAssets
{
    public const string BundleFileName = "darius_models.bundle";

    private static object _bundle;
    private static bool _loadAttempted;
    private static readonly Dictionary<string, GameObject> Prefabs =
        new Dictionary<string, GameObject>(StringComparer.OrdinalIgnoreCase);

    public static bool TryActivate(GameObject owner)
    {
        if (owner == null) return false;
        DariusNativeModelBridge existing = owner.GetComponent<DariusNativeModelBridge>();
        if (existing != null && existing.IsReady) return true;

        DariusSkinModelBinding binding = owner.GetComponent<DariusSkinModelBinding>();
        if (binding == null || string.IsNullOrEmpty(binding.variantKey)) return false;
        GameObject prefab = GetPrefab(binding.variantKey);
        if (prefab == null) return false;

        GameObject instance = null;
        DariusNativeModelBridge bridge = existing;
        try
        {
            RemoveStaleNativeRoots(owner.transform);
            instance = UnityEngine.Object.Instantiate(prefab, owner.transform, false);
            instance.name = "Darius_Native_Model";
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;

            if (bridge == null) bridge = owner.AddComponent<DariusNativeModelBridge>();
            bridge.Initialize(instance, binding);
            DariusLog.Info("NATIVE-MODEL", "Activated Unity AssetBundle model skin=" + binding.variantKey +
                " prefab=" + prefab.name + ".");
            return true;
        }
        catch (Exception e)
        {
            if (instance != null)
            {
                try { instance.SetActive(false); } catch { }
                try { UnityEngine.Object.Destroy(instance); } catch { }
            }
            if (bridge != null)
            {
                try { UnityEngine.Object.Destroy(bridge); } catch { }
            }
            DariusLog.Exception("NATIVE-MODEL", e, "Native model activation failed skin=" + binding.variantKey);
            return false;
        }
    }

    public static bool IsActive(GameObject owner)
    {
        if (owner == null) return false;
        DariusNativeModelBridge bridge = owner.GetComponent<DariusNativeModelBridge>();
        return bridge != null && bridge.IsReady;
    }

    public static bool CanUseLegacyFallback(GameObject owner)
    {
        if (owner == null) return false;
        DariusSkinModelBinding binding = owner.GetComponent<DariusSkinModelBinding>();
        if (binding == null || string.IsNullOrEmpty(binding.modelFile) || string.IsNullOrEmpty(DariusMedia.Root))
            return false;
        string path = Path.Combine(DariusMedia.Root, "assets", "models", binding.modelFile);
        return File.Exists(path);
    }

    private static GameObject GetPrefab(string variantKey)
    {
        GameObject cached;
        if (Prefabs.TryGetValue(variantKey, out cached)) return cached;
        if (!EnsureBundle()) return null;

        string assetName = DariusNativeAssetContract.PrefabAssetPath(variantKey);
        GameObject prefab = DariusUnityAssetBundleApi.LoadGameObject(_bundle, assetName);
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

        string path = string.IsNullOrEmpty(DariusMedia.Root)
            ? null
            : Path.Combine(DariusMedia.Root, "assets", "models", BundleFileName);
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            DariusLog.Warn("NATIVE-MODEL", "Native model bundle is missing: " + (path ?? "<null>"));
            return false;
        }

        try
        {
            _bundle = DariusUnityAssetBundleApi.LoadFromFile(path);
            if (_bundle == null) throw new InvalidOperationException("AssetBundle.LoadFromFile returned null path=" + path);
            DariusLog.Info("NATIVE-MODEL", "Loaded native Unity model bundle path=" + path);
            return true;
        }
        catch (Exception e)
        {
            _bundle = null;
            DariusUnityAssetBundleApi.Reset();
            DariusLog.Exception("NATIVE-MODEL", e, "Failed loading Unity model AssetBundle path=" + path);
            return false;
        }
    }

    public static void Unload()
    {
        DariusNativeEntityAnimationLease.Clear();
        Prefabs.Clear();
        ClearOverlayMeshes();
        _loadAttempted = false;
        if (_bundle != null)
        {
            try { DariusUnityAssetBundleApi.Unload(_bundle, false); } catch { }
        }
        _bundle = null;
        DariusUnityAssetBundleApi.Reset();
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
