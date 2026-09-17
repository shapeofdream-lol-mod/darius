using System;
using System.Collections.Generic;
using UnityEngine;

internal static partial class DariusNativeModelAssets
{
    private static readonly Dictionary<string, Mesh> OverlayMeshes =
        new Dictionary<string, Mesh>(StringComparer.OrdinalIgnoreCase);

    internal static Mesh GetOverlayMesh(string variantKey, int rendererIndex, uint materialHash)
    {
        if (string.IsNullOrEmpty(variantKey) || rendererIndex < 0) return null;
        string key = DariusNativeOverlayContract.AssetFileName(variantKey, rendererIndex, materialHash);
        Mesh cached;
        if (OverlayMeshes.TryGetValue(key, out cached)) return cached;
        if (!EnsureBundle()) return null;

        string suffix = DariusNativeOverlayContract.AssetSuffix(variantKey, rendererIndex, materialHash);
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
            DariusLog.Error("NATIVE-OVERLAY", "AssetBundle has no filtered overlay mesh suffix=" + suffix);
            OverlayMeshes[key] = null;
            return null;
        }

        Mesh mesh = DariusUnityAssetBundleApi.LoadMesh(_bundle, assetName);
        if (mesh == null)
            DariusLog.Error("NATIVE-OVERLAY", "Filtered overlay mesh load returned null asset=" + assetName);
        OverlayMeshes[key] = mesh;
        return mesh;
    }

    private static void ClearOverlayMeshes()
    {
        OverlayMeshes.Clear();
    }
}
