#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

internal static class DariusModelOverlayMeshBuilder
{
    public static void Build(
        GameObject root,
        DariusNativeSkinProfile profile,
        string generatedRoot,
        List<string> bundleAssets)
    {
        if (root == null || profile == null || bundleAssets == null)
            throw new ArgumentNullException("Native overlay build contract received null input.");

        SkinnedMeshRenderer[] renderers = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        int sourceIndex = 0;
        int expected = 0;
        int generated = 0;
        for (int r = 0; r < renderers.Length; r++)
        {
            SkinnedMeshRenderer source = renderers[r];
            if (!IsOverlaySourceStructure(source)) continue;
            int stableIndex = sourceIndex++;
            Material[] materials = source.sharedMaterials;
            HashSet<uint> emitted = new HashSet<uint>();
            int count = Math.Min(materials.Length, source.sharedMesh.subMeshCount);
            for (int i = 0; i < count; i++)
            {
                Material material = materials[i];
                if (material == null) continue;
                uint hash = DariusNativeOverlayContract.MaterialHash(material.name);
                if (!emitted.Add(hash)) continue;
                expected++;
                string path = CreateFilteredMesh(
                    source.sharedMesh, materials, hash, profile, stableIndex, generatedRoot);
                Mesh asset = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if (asset == null)
                    throw new InvalidOperationException("Overlay mesh asset was not created skin=" + profile.Variant +
                        " renderer=" + stableIndex + " hash=" + hash.ToString("x8"));
                bundleAssets.Add(path);
                generated++;
            }
        }

        if (sourceIndex == 0 || expected == 0 || generated != expected)
            throw new InvalidOperationException("Overlay mesh contract failed skin=" + profile.Variant +
                " renderers=" + sourceIndex + " expected=" + expected + " generated=" + generated);
    }

    private static bool IsOverlaySourceStructure(SkinnedMeshRenderer renderer)
    {
        if (renderer == null || renderer.sharedMesh == null) return false;
        string name = renderer.gameObject != null ? renderer.gameObject.name : string.Empty;
        return !DariusNativeOverlayContract.IsHiddenObjectName(name);
    }

    private static string CreateFilteredMesh(
        Mesh source,
        Material[] materials,
        uint materialHash,
        DariusNativeSkinProfile profile,
        int sourceIndex,
        string generatedRoot)
    {
        Mesh copy = UnityEngine.Object.Instantiate(source);
        copy.name = source.name + "_Overlay_" + materialHash.ToString("x8");
        for (int i = 0; i < copy.subMeshCount; i++)
        {
            Material material = i < materials.Length ? materials[i] : null;
            if (material == null || DariusNativeOverlayContract.MaterialHash(material.name) != materialHash)
                copy.SetTriangles(Array.Empty<int>(), i, false);
        }
        string path = generatedRoot + "/" +
            DariusNativeOverlayContract.AssetFileName(profile.Variant, sourceIndex, materialHash);
        AssetDatabase.CreateAsset(copy, path);
        return path;
    }
}
#endif
