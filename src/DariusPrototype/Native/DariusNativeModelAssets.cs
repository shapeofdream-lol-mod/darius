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

    internal static GameObject InstantiateFreshEntityModelTemplate(string variantKey, Transform parent)
    {
        GameObject prefab = GetPrefab(variantKey);
        if (prefab == null) return null;

        GameObject instance = UnityEngine.Object.Instantiate(prefab, parent, false);
        instance.name = "Darius_OfficialEntityModel_" + DariusNativeAssetContract.NormalizeVariant(variantKey);
        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = Quaternion.identity;
        instance.transform.localScale = Vector3.one;
        return instance;
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
            if (_bundle == null)
                throw new InvalidOperationException("AssetBundle.LoadFromFile returned null path=" + path);
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
}
