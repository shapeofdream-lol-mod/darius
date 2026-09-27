using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

internal static partial class DariusNativeModelAssets
{
    public const string BundleFileName = "darius_models.bundle";

    private static object _bundle;
    private static string _bundlePath;
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
        // Loader readiness is not a load failure. Workshop ModItem.path can appear after Awake, so
        // never poison this process-lifetime cache merely because the mod root is temporarily null.
        string root = DariusMedia.Root;
        if (string.IsNullOrEmpty(root))
        {
            DariusLog.DebugInfoThrottled("NATIVE-MODEL", "root-not-ready",
                "Native model bundle load deferred because the Mod root is not ready.", 1.0);
            return false;
        }

        string path = Path.Combine(root, "assets", "models", BundleFileName);
        if (_bundle != null)
        {
            if (string.Equals(_bundlePath, path, StringComparison.OrdinalIgnoreCase))
                return true;

            // The authoritative Workshop/local root changed after an early fallback. Traveler core
            // health tears down the old generation first; this call occurs on the next-frame rebuild.
            DariusLog.Warn("NATIVE-MODEL", "Native bundle root changed; replacing process cache old=" +
                (_bundlePath ?? "<null>") + " new=" + path + ".");
            Prefabs.Clear();
            ClearOverlayMeshes();
            try { DariusUnityAssetBundleApi.Unload(_bundle, false); } catch { }
            _bundle = null;
            _bundlePath = null;
            _loadAttempted = false;
            DariusUnityAssetBundleApi.Reset();
        }

        if (_loadAttempted) return false;
        _loadAttempted = true;
        if (!File.Exists(path))
        {
            DariusLog.Error("NATIVE-MODEL", "Native model bundle is missing: " + path);
            return false;
        }

        try
        {
            _bundle = DariusUnityAssetBundleApi.LoadFromFile(path);
            if (_bundle == null)
                throw new InvalidOperationException("AssetBundle.LoadFromFile returned null path=" + path);
            _bundlePath = path;
            DariusLog.Info("NATIVE-MODEL", "Loaded process-lifetime native Unity model bundle path=" + path);
            return true;
        }
        catch (Exception e)
        {
            // A real LoadFromFile/reflection failure may still be transient during loader startup.
            // The generation-owned bootstrap has a finite retry budget, so allow its next attempt
            // instead of permanently caching one failed call.
            _bundle = null;
            _bundlePath = null;
            _loadAttempted = false;
            DariusUnityAssetBundleApi.Reset();
            DariusLog.Exception("NATIVE-MODEL", e, "Failed loading Unity model AssetBundle path=" + path);
            return false;
        }
    }

    public static void Unload()
    {
        DariusLog.Info("NATIVE-MODEL", "Process-lifetime unload requested bundle=" + (_bundle != null) +
            " loadAttempted=" + _loadAttempted +
            " prefabs=" + Prefabs.Count +
            " overlayMeshes=" + OverlayMeshes.Count + " unloadAllLoadedObjects=false.");
        Prefabs.Clear();
        ClearOverlayMeshes();
        _loadAttempted = false;
        if (_bundle != null)
        {
            try { DariusUnityAssetBundleApi.Unload(_bundle, false); } catch { }
        }
        _bundle = null;
        _bundlePath = null;
        DariusUnityAssetBundleApi.Reset();
    }
}
