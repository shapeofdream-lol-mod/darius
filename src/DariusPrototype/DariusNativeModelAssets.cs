// Native Unity model path. The bundle is built offline from the local Riot-derived model pack.
// Runtime work is intentionally boring: load a prefab, let Unity Animator evaluate animation, and
// expose the resulting EntityModel/renderer/anchor contract to Shape of Dreams.
internal static class DariusNativeModelAssets
{
    public const string BundleFileName = "darius_models.bundle";
    private static AssetBundle _bundle;
    private static bool _loadAttempted;
    private static string[] _assetNames;
    private static readonly Dictionary<string, GameObject> Prefabs = new Dictionary<string, GameObject>(StringComparer.OrdinalIgnoreCase);

    public static bool TryActivate(DariusTravelerModelInstance legacy)
    {
        if (legacy == null) return false;
        DariusNativeModelBridge existing = legacy.GetComponent<DariusNativeModelBridge>();
        if (existing != null && existing.IsReady) return true;

        DariusSkinModelBinding binding = legacy.GetComponent<DariusSkinModelBinding>();
        if (binding == null || string.IsNullOrEmpty(binding.variantKey)) return false;
        GameObject prefab = GetPrefab(binding.variantKey);
        if (prefab == null) return false;

        try
        {
            RemoveStaleNativeRoots(legacy.transform);
            GameObject instance = UnityEngine.Object.Instantiate(prefab, legacy.transform, false);
            instance.name = "Darius_Native_Model";
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;

            DariusNativeModelBridge bridge = existing != null ? existing : legacy.gameObject.AddComponent<DariusNativeModelBridge>();
            bridge.Initialize(instance, binding);
            legacy.enabled = false;
            DariusLog.Info("NATIVE-MODEL", "Activated Unity AssetBundle model skin=" + binding.variantKey +
                " prefab=" + prefab.name + "; legacy GLB interpreter disabled.");
            return true;
        }
        catch (Exception e)
        {
            DariusLog.Exception("NATIVE-MODEL", e, "Native model activation failed for skin=" + binding.variantKey + "; legacy GLB fallback remains available");
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

        GameObject prefab = _bundle.LoadAsset<GameObject>(assetName);
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
            DariusLog.Info("NATIVE-MODEL", "Native model bundle not present; using optimized legacy GLB fallback. expected=" + (path ?? "<null>"));
            return false;
        }

        try
        {
            _bundle = AssetBundle.LoadFromFile(path);
            if (_bundle == null)
            {
                DariusLog.Error("NATIVE-MODEL", "AssetBundle.LoadFromFile returned null path=" + path);
                return false;
            }
            _assetNames = _bundle.GetAllAssetNames() ?? Array.Empty<string>();
            DariusLog.Info("NATIVE-MODEL", "Loaded native Unity model bundle assets=" + _assetNames.Length + " path=" + path);
            return true;
        }
        catch (Exception e)
        {
            _bundle = null;
            DariusLog.Exception("NATIVE-MODEL", e, "Failed loading Unity model AssetBundle path=" + path);
            return false;
        }
    }

    public static void Unload()
    {
        Prefabs.Clear();
        _assetNames = null;
        _loadAttempted = false;
        if (_bundle != null)
        {
            try { _bundle.Unload(false); } catch { }
            _bundle = null;
        }
    }

    private static void RemoveStaleNativeRoots(Transform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            Transform child = parent.GetChild(i);
            if (child != null && string.Equals(child.name, "Darius_Native_Model", StringComparison.Ordinal))
            {
                try { UnityEngine.Object.DestroyImmediate(child.gameObject); }
                catch { UnityEngine.Object.Destroy(child.gameObject); }
            }
        }
    }
}