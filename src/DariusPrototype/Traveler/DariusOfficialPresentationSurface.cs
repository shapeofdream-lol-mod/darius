using System;
using UnityEngine;

// Presentation helpers for fresh EntityModel skins.
//
// The old Bridge exposed anchors and filtered mesh overlays. Fresh models own the same native
// hierarchy directly, so these helpers resolve that contract from EntityVisual.model without
// recreating a model host/bridge.
internal static class DariusOfficialPresentationSurface
{
    public static Transform FindAnchor(Hero hero, params string[] names)
    {
        EntityModel model;
        string variant;
        if (!TryGetFreshModel(hero, out model, out variant) || model == null || names == null) return null;

        Transform[] all = model.GetComponentsInChildren<Transform>(true);
        for (int ni = 0; ni < names.Length; ni++)
        {
            string name = names[ni];
            if (string.IsNullOrEmpty(name)) continue;
            for (int ti = 0; ti < all.Length; ti++)
            {
                Transform t = all[ti];
                if (t != null && string.Equals(t.name, name, StringComparison.OrdinalIgnoreCase))
                    return t;
            }
        }
        return null;
    }

    public static GameObject CreateSubmeshOverlay(Hero hero, uint sourceMaterialHash, Material overlayMaterial)
    {
        EntityModel model;
        string variant;
        if (!TryGetFreshModel(hero, out model, out variant) || model == null || overlayMaterial == null)
            return null;

        GameObject root = CreateOverlayRoot(model.transform, "Submesh");
        DariusLolVfxLinkedObjects links = root.GetComponent<DariusLolVfxLinkedObjects>();
        int created = 0;
        int sourceIndex = 0;

        SkinnedMeshRenderer[] renderers = model.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        for (int r = 0; r < renderers.Length; r++)
        {
            SkinnedMeshRenderer source = renderers[r];
            if (!IsOverlaySourceStructure(source)) continue;
            int stableIndex = sourceIndex++;
            if (!source.enabled || !source.gameObject.activeInHierarchy ||
                !RendererContainsMaterialHash(source, sourceMaterialHash)) continue;

            Mesh filtered = DariusNativeModelAssets.GetOverlayMesh(variant, stableIndex, sourceMaterialHash);
            if (filtered == null) continue;

            GameObject overlay = CreateOverlayRenderer(
                source, filtered, overlayMaterial, sourceMaterialHash, false, "Submesh_" + stableIndex);
            links.Add(overlay);
            created++;
        }

        return FinishOverlayRoot(root, created);
    }

    public static GameObject CreateFullMeshOverlay(Hero hero, Material overlayMaterial, string label)
    {
        EntityModel model;
        string variant;
        if (!TryGetFreshModel(hero, out model, out variant) || model == null || overlayMaterial == null)
            return null;

        string resolvedLabel = string.IsNullOrEmpty(label) ? "Full" : label;
        GameObject root = CreateOverlayRoot(model.transform, resolvedLabel);
        DariusLolVfxLinkedObjects links = root.GetComponent<DariusLolVfxLinkedObjects>();
        int created = 0;

        SkinnedMeshRenderer[] renderers = model.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            SkinnedMeshRenderer source = renderers[i];
            if (!IsOverlaySourceStructure(source) || !source.enabled || !source.gameObject.activeInHierarchy)
                continue;

            GameObject overlay = CreateOverlayRenderer(
                source, source.sharedMesh, overlayMaterial, 0u, true, resolvedLabel + "_" + i);
            links.Add(overlay);
            created++;
        }

        return FinishOverlayRoot(root, created);
    }

    private static bool TryGetFreshModel(Hero hero, out EntityModel model, out string variant)
    {
        model = null;
        variant = null;
        Hero_Darius darius = hero as Hero_Darius;
        if (darius == null || darius.Visual == null) return false;

        model = darius.Visual.model;
        if (model == null) return false;

        DariusOfficialEntityModelMarker marker = model.GetComponent<DariusOfficialEntityModelMarker>();
        if (marker == null) return false;

        variant = marker.variantKey;
        return !string.IsNullOrEmpty(variant);
    }

    private static GameObject CreateOverlayRoot(Transform parent, string label)
    {
        GameObject root = new GameObject("Darius_NativeOverlayGroup_" + label);
        root.transform.SetParent(parent, false);
        root.AddComponent<DariusLolVfxLinkedObjects>();
        return root;
    }

    private static GameObject FinishOverlayRoot(GameObject root, int created)
    {
        if (created > 0) return root;
        if (root != null) UnityEngine.Object.Destroy(root);
        return null;
    }

    private static bool IsOverlaySourceStructure(SkinnedMeshRenderer source)
    {
        if (source == null || source.sharedMesh == null) return false;
        string name = source.gameObject != null ? source.gameObject.name : string.Empty;
        if (DariusNativeAssetContract.IsHiddenObjectName(name)) return false;
        return !name.StartsWith("Darius_NativeOverlay_", StringComparison.OrdinalIgnoreCase) &&
               !name.StartsWith("Darius_NativeOverlayGroup_", StringComparison.OrdinalIgnoreCase);
    }

    private static bool RendererContainsMaterialHash(Renderer renderer, uint materialHash)
    {
        if (renderer == null) return false;
        Material[] materials = renderer.sharedMaterials;
        for (int i = 0; i < materials.Length; i++)
        {
            Material material = materials[i];
            if (material != null &&
                DariusNativeAssetContract.MaterialHash(material.name) == materialHash) return true;
        }
        return false;
    }

    private static GameObject CreateOverlayRenderer(
        SkinnedMeshRenderer source,
        Mesh mesh,
        Material material,
        uint selectedMaterialHash,
        bool fullMesh,
        string label)
    {
        GameObject go = new GameObject("Darius_NativeOverlay_" + label);
        go.transform.SetParent(source.transform, false);
        go.layer = source.gameObject.layer;

        SkinnedMeshRenderer renderer = go.AddComponent<SkinnedMeshRenderer>();
        renderer.sharedMesh = mesh;
        renderer.bones = source.bones;
        renderer.rootBone = source.rootBone;
        renderer.quality = source.quality;
        renderer.localBounds = source.localBounds;
        renderer.updateWhenOffscreen = false;

        int count = Mathf.Max(1, mesh.subMeshCount);
        Material[] materials = new Material[count];
        if (fullMesh)
        {
            for (int i = 0; i < count; i++) materials[i] = material;
        }
        else
        {
            Material[] sourceMaterials = source.sharedMaterials;
            for (int i = 0; i < count; i++)
            {
                Material sourceMaterial = i < sourceMaterials.Length ? sourceMaterials[i] : null;
                if (sourceMaterial != null &&
                    DariusNativeAssetContract.MaterialHash(sourceMaterial.name) == selectedMaterialHash)
                    materials[i] = material;
            }
        }
        renderer.sharedMaterials = materials;
        return go;
    }
}
