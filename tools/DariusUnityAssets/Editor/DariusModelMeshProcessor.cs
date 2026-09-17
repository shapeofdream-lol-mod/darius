#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

internal static class DariusModelMeshProcessor
{
    private enum SubmeshKind { Visible, ToggleHidden, PermanentHidden }

    public static void ProcessPrefab(
        GameObject root,
        DariusNativeSkinProfile profile,
        string generatedRoot,
        List<string> bundleAssets)
    {
        if (root == null || profile == null) return;

        // Main's runtime GLB path only builds the skinned champion mesh. FBX import can additionally
        // surface unskinned helper/authoring meshes from the source scene; leaving their MeshRenderer
        // components in the native prefab changes presentation semantics and can expose texture cards
        // during action clips. Keep the hierarchy for animation/anchor paths, but remove rendering for
        // geometry that main never displayed.
        RemoveNonSkinnedRenderers(root);

        SkinnedMeshRenderer[] renderers = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            SkinnedMeshRenderer renderer = renderers[i];
            if (renderer == null) continue;
            renderer.updateWhenOffscreen = false;
            renderer.localBounds = ExpandBounds(renderer, 1.20f);
        }
        if (profile.GodKing) SplitAuthoredHiddenSubmeshes(renderers, profile, generatedRoot);
        DariusModelOverlayMeshBuilder.Build(root, profile, bundleAssets);
    }

    private static void RemoveNonSkinnedRenderers(GameObject root)
    {
        MeshRenderer[] renderers = root.GetComponentsInChildren<MeshRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            MeshRenderer renderer = renderers[i];
            if (renderer == null) continue;
            Debug.Log("[DariusNativeAssets] removed non-skinned source renderer object=" + renderer.gameObject.name);
            UnityEngine.Object.DestroyImmediate(renderer);
        }
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
        DariusNativeSkinProfile profile,
        string generatedRoot)
    {
        for (int r = 0; r < renderers.Length; r++)
        {
            SkinnedMeshRenderer source = renderers[r];
            Mesh mesh = source != null ? source.sharedMesh : null;
            if (mesh == null) continue;
            Material[] materials = source.sharedMaterials;
            SubmeshKind[] kinds = new SubmeshKind[mesh.subMeshCount];
            bool hasVisible = false, hasToggle = false, hasPermanent = false;
            for (int i = 0; i < kinds.Length; i++)
            {
                kinds[i] = ClassifyMaterial(i < materials.Length ? materials[i] : null, profile);
                hasVisible |= kinds[i] == SubmeshKind.Visible;
                hasToggle |= kinds[i] == SubmeshKind.ToggleHidden;
                hasPermanent |= kinds[i] == SubmeshKind.PermanentHidden;
            }
            if (!hasToggle && !hasPermanent) continue;

            string stem = SanitizeAssetName(source.name + "_" + r);
            Mesh visibleMesh = CreateFilteredMesh(mesh, kinds, SubmeshKind.Visible,
                generatedRoot, stem + "_visible", mesh.name + "_Visible");
            if (hasToggle)
                CreateHiddenRenderer(source, mesh, materials, kinds, SubmeshKind.ToggleHidden, generatedRoot,
                    stem + "_toggle", mesh.name + "_WolfHidden",
                    DariusNativeAssetContract.WolfHiddenObjectName(source.name));
            if (hasPermanent)
                CreateHiddenRenderer(source, mesh, materials, kinds, SubmeshKind.PermanentHidden, generatedRoot,
                    stem + "_static", mesh.name + "_StaticHidden",
                    DariusNativeAssetContract.StaticHiddenObjectName(source.name));

            source.sharedMesh = visibleMesh;
            source.sharedMaterials = FilterMaterials(materials, kinds, SubmeshKind.Visible);
            source.enabled = hasVisible;
        }
    }

    private static Mesh CreateFilteredMesh(
        Mesh source, SubmeshKind[] kinds, SubmeshKind keep, string root, string stem, string name)
    {
        Mesh copy = UnityEngine.Object.Instantiate(source);
        copy.name = name;
        for (int i = 0; i < kinds.Length; i++)
            if (kinds[i] != keep) copy.SetTriangles(Array.Empty<int>(), i, false);
        string path = AssetDatabase.GenerateUniqueAssetPath(root + "/" + stem + ".asset");
        AssetDatabase.CreateAsset(copy, path);
        return copy;
    }

    private static Material[] FilterMaterials(Material[] source, SubmeshKind[] kinds, SubmeshKind keep)
    {
        Material[] result = (Material[])source.Clone();
        for (int i = 0; i < result.Length; i++)
            if (i >= kinds.Length || kinds[i] != keep) result[i] = null;
        return result;
    }

    private static void CreateHiddenRenderer(
        SkinnedMeshRenderer source,
        Mesh sourceMesh,
        Material[] materials,
        SubmeshKind[] kinds,
        SubmeshKind keep,
        string generatedRoot,
        string stem,
        string meshName,
        string objectName)
    {
        Mesh hiddenMesh = CreateFilteredMesh(sourceMesh, kinds, keep, generatedRoot, stem, meshName);
        GameObject hiddenObject = new GameObject(objectName);
        hiddenObject.transform.SetParent(source.transform, false);
        SkinnedMeshRenderer renderer = hiddenObject.AddComponent<SkinnedMeshRenderer>();
        renderer.sharedMesh = hiddenMesh;
        renderer.sharedMaterials = FilterMaterials(materials, kinds, keep);
        renderer.bones = source.bones;
        renderer.rootBone = source.rootBone;
        renderer.quality = source.quality;
        renderer.localBounds = source.localBounds;
        renderer.updateWhenOffscreen = false;
        renderer.enabled = false;
    }

    private static SubmeshKind ClassifyMaterial(Material material, DariusNativeSkinProfile profile)
    {
        string name = material != null ? material.name : null;
        if (string.IsNullOrEmpty(name)) return SubmeshKind.Visible;
        if (!string.IsNullOrEmpty(profile.ToggleHiddenMaterial) &&
            name.IndexOf(profile.ToggleHiddenMaterial, StringComparison.OrdinalIgnoreCase) >= 0)
            return SubmeshKind.ToggleHidden;
        if (!string.IsNullOrEmpty(profile.PermanentHiddenMaterial) &&
            name.IndexOf(profile.PermanentHiddenMaterial, StringComparison.OrdinalIgnoreCase) >= 0)
            return SubmeshKind.PermanentHidden;
        return SubmeshKind.Visible;
    }

    private static string SanitizeAssetName(string value)
    {
        if (string.IsNullOrEmpty(value)) return "mesh";
        foreach (char c in System.IO.Path.GetInvalidFileNameChars()) value = value.Replace(c, '_');
        return value.Replace('/', '_').Replace('\\', '_');
    }
}
#endif
