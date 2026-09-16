#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

internal static class DariusModelMeshProcessor
{
    public static void ProcessPrefab(GameObject root, string variant, string generatedRoot)
    {
        if (root == null) return;

        SkinnedMeshRenderer[] renderers = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            SkinnedMeshRenderer renderer = renderers[i];
            if (renderer == null) continue;
            renderer.updateWhenOffscreen = false;
            renderer.localBounds = ExpandBounds(renderer, 1.20f);
        }

        if (string.Equals(variant, "GodKing", StringComparison.OrdinalIgnoreCase))
            SplitAuthoredHiddenSubmeshes(renderers, generatedRoot);
    }

    private static Bounds ExpandBounds(SkinnedMeshRenderer renderer, float factor)
    {
        Bounds bounds = renderer.localBounds;
        if (bounds.size.sqrMagnitude <= 0.000001f && renderer.sharedMesh != null)
            bounds = renderer.sharedMesh.bounds;
        bounds.extents *= Mathf.Max(1f, factor);
        return bounds;
    }

    private static void SplitAuthoredHiddenSubmeshes(
        SkinnedMeshRenderer[] renderers,
        string generatedRoot)
    {
        for (int r = 0; r < renderers.Length; r++)
        {
            SkinnedMeshRenderer source = renderers[r];
            Mesh mesh = source != null ? source.sharedMesh : null;
            if (mesh == null || mesh.subMeshCount <= 1) continue;

            Material[] materials = source.sharedMaterials;
            bool[] hidden = new bool[mesh.subMeshCount];
            bool hasHidden = false;
            bool hasVisible = false;
            for (int i = 0; i < hidden.Length; i++)
            {
                Material material = i < materials.Length ? materials[i] : null;
                hidden[i] = IsAuthoredHiddenMaterial(material);
                hasHidden |= hidden[i];
                hasVisible |= !hidden[i];
            }
            if (!hasHidden || !hasVisible) continue;

            Mesh visibleMesh = UnityEngine.Object.Instantiate(mesh);
            Mesh hiddenMesh = UnityEngine.Object.Instantiate(mesh);
            visibleMesh.name = mesh.name + "_Visible";
            hiddenMesh.name = mesh.name + "_Hidden";
            for (int i = 0; i < hidden.Length; i++)
            {
                if (hidden[i]) visibleMesh.SetTriangles(Array.Empty<int>(), i, false);
                else hiddenMesh.SetTriangles(Array.Empty<int>(), i, false);
            }

            string stem = SanitizeAssetName(source.name + "_" + r);
            string visiblePath = AssetDatabase.GenerateUniqueAssetPath(generatedRoot + "/" + stem + "_visible.asset");
            string hiddenPath = AssetDatabase.GenerateUniqueAssetPath(generatedRoot + "/" + stem + "_hidden.asset");
            AssetDatabase.CreateAsset(visibleMesh, visiblePath);
            AssetDatabase.CreateAsset(hiddenMesh, hiddenPath);
            source.sharedMesh = visibleMesh;

            GameObject hiddenObject = new GameObject("DariusHidden_" + source.name);
            hiddenObject.transform.SetParent(source.transform, false);
            SkinnedMeshRenderer hiddenRenderer = hiddenObject.AddComponent<SkinnedMeshRenderer>();
            hiddenRenderer.sharedMesh = hiddenMesh;
            hiddenRenderer.sharedMaterials = materials;
            hiddenRenderer.bones = source.bones;
            hiddenRenderer.rootBone = source.rootBone;
            hiddenRenderer.quality = source.quality;
            hiddenRenderer.localBounds = source.localBounds;
            hiddenRenderer.updateWhenOffscreen = false;
            hiddenRenderer.enabled = false;
        }
    }

    private static bool IsAuthoredHiddenMaterial(Material material)
    {
        if (material == null || string.IsNullOrEmpty(material.name)) return false;
        return material.name.IndexOf("Wolf_Mat", StringComparison.OrdinalIgnoreCase) >= 0 ||
               material.name.IndexOf("Throne", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static string SanitizeAssetName(string value)
    {
        if (string.IsNullOrEmpty(value)) return "mesh";
        foreach (char c in System.IO.Path.GetInvalidFileNameChars())
            value = value.Replace(c, '_');
        return value.Replace('/', '_').Replace('\\', '_');
    }
}
#endif
