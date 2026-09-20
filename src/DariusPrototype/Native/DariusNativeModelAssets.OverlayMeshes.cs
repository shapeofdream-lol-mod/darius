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
        string assetName = DariusNativeAssetContract.OverlayAssetPath(variantKey, rendererIndex, materialHash);
        Mesh cached;
        if (OverlayMeshes.TryGetValue(assetName, out cached)) return cached;
        if (!EnsureBundle()) return null;

        Mesh mesh = DariusUnityAssetBundleApi.LoadMesh(_bundle, assetName);
        if (mesh == null)
            DariusLog.Error("NATIVE-OVERLAY", "Filtered overlay mesh load returned null asset=" + assetName);
        OverlayMeshes[assetName] = mesh;
        return mesh;
    }

    private static void ClearOverlayMeshes()
    {
        OverlayMeshes.Clear();
    }
}
