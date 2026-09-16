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
        if (root == null || profile == null || bundleAssets == null) return;
        SkinnedMeshRenderer[] renderers = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        for (int r = 0; r < renderers.Length; r++)
        {
            SkinnedMeshRenderer source = renderers[r];
            if (!IsOverlaySource(source)) continue;
            Material[] materials = source.sharedMaterials;
            HashSet<uint> emitted = new HashSet<uint>();
            int count = Math.Min(materials.Length, source.sharedMesh.subMeshCount);
            for (int i = 0; i < count; i++)
            {
                Material material = materials[i];
                if (material == null) continue;
                uint hash = RiotStringHash(material.name);
                if (!emitted.Add(hash)) continue;
                string path = CreateFilteredMesh(source.sharedMesh, materials, hash, profile, r, generatedRoot);
                bundleAssets.Add(path);
            }
        }
    }

    private static bool IsOverlaySource(SkinnedMeshRenderer renderer)
    {
        if (renderer == null || !renderer.enabled || renderer.sharedMesh == null) return false;
        string name = renderer.gameObject != null ? renderer.gameObject.name : string.Empty;
        return !name.StartsWith("DariusHidden_", StringComparison.OrdinalIgnoreCase);
    }

    private static string CreateFilteredMesh(
        Mesh source,
        Material[] materials,
        uint materialHash,
        DariusNativeSkinProfile profile,
        int rendererIndex,
        string generatedRoot)
    {
        Mesh copy = UnityEngine.Object.Instantiate(source);
        copy.name = source.name + "_Overlay_" + materialHash.ToString("x8");
        for (int i = 0; i < copy.subMeshCount; i++)
        {
            Material material = i < materials.Length ? materials[i] : null;
            if (material == null || RiotStringHash(material.name) != materialHash)
                copy.SetTriangles(Array.Empty<int>(), i, false);
        }
        string variant = profile.Variant.Replace(" ", string.Empty).ToLowerInvariant();
        string path = generatedRoot + "/overlay_" + variant + "_" + rendererIndex + "_" +
            materialHash.ToString("x8") + ".asset";
        AssetDatabase.CreateAsset(copy, path);
        return path;
    }

    private static uint RiotStringHash(string text)
    {
        uint h = 2166136261u;
        if (text == null) return h;
        for (int i = 0; i < text.Length; i++)
        {
            h ^= (byte)char.ToLowerInvariant(text[i]);
            h *= 16777619u;
        }
        return h;
    }
}
#endif
