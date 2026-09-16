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

    private static void SplitAuthoredHiddenSubmeshes(SkinnedMeshRenderer[] renderers, string generatedRoot)
    {
        for (int r = 0; r < renderers.Length; r++)
        {
            SkinnedMeshRenderer source = renderers[r];
            Mesh mesh = source != null ? source.sharedMesh : null;
            if (mesh == null) continue;
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
            if (!hasHidden) continue;

            string stem = SanitizeAssetName(source.name + "_" + r);
            if (!hasVisible)
            {
                Mesh hiddenOnly = SaveMeshCopy(mesh, generatedRoot, stem + "_hidden");
                hiddenOnly.name = mesh.name + "_Hidden";
                EditorUtility.SetDirty(hiddenOnly);
                source.sharedMesh = hiddenOnly;
                source.enabled = false;
                continue;
            }

            Mesh visibleMesh = UnityEngine.Object.Instantiate(mesh);
            Mesh hiddenMesh = UnityEngine.Object.Instantiate(mesh);
            visibleMesh.name = mesh.name + "_Visible";
            hiddenMesh.name = mesh.name + "_Hidden";
            for (int i = 0; i < hidden.Length; i++)
            {
                if (hidden[i]) visibleMesh.SetTriangles(Array.Empty<int>(), i, false);
                else hiddenMesh.SetTriangles(Array.Empty<int>(), i, false);
            }
            SaveMesh(visibleMesh, generatedRoot, stem + "_visible");
            SaveMesh(hiddenMesh, generatedRoot, stem + "_hidden");
            source.sharedMesh = visibleMesh;

            Material[] visibleMaterials = (Material[])materials.Clone();
            for (int i = 0; i < hidden.Length && i < visibleMaterials.Length; i++)
                if (hidden[i]) visibleMaterials[i] = null;
            source.sharedMaterials = visibleMaterials;

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

    private static Mesh SaveMeshCopy(Mesh source, string root, string stem)
    {
        Mesh copy = UnityEngine.Object.Instantiate(source);
        SaveMesh(copy, root, stem);
        return copy;
    }

    private static void SaveMesh(Mesh mesh, string root, string stem)
    {
        string path = AssetDatabase.GenerateUniqueAssetPath(root + "/" + stem + ".asset");
        AssetDatabase.CreateAsset(mesh, path);
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
        foreach (char c in System.IO.Path.GetInvalidFileNameChars()) value = value.Replace(c, '_');
        return value.Replace('/', '_').Replace('\\', '_');
    }
}
#endif