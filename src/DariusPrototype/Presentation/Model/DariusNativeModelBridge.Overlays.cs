using UnityEngine;

public sealed partial class DariusNativeModelBridge : MonoBehaviour
{
    public GameObject CreateSubmeshOverlay(uint sourceMaterialHash, Material overlayMaterial)
    {
        if (_modelRoot == null || overlayMaterial == null) return null;
        GameObject root = CreateOverlayRoot("Submesh");
        DariusLolVfxLinkedObjects links = root.GetComponent<DariusLolVfxLinkedObjects>();
        int created = 0;
        int sourceIndex = 0;
        SkinnedMeshRenderer[] renderers = _modelRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        for (int r = 0; r < renderers.Length; r++)
        {
            SkinnedMeshRenderer source = renderers[r];
            if (!IsVisibleOverlaySource(source)) continue;
            int stableIndex = sourceIndex++;
            if (!RendererContainsMaterialHash(source, sourceMaterialHash)) continue;
            Mesh filtered = DariusNativeModelAssets.GetOverlayMesh(VariantKey, stableIndex, sourceMaterialHash);
            if (filtered == null) continue;
            GameObject overlay = CreateOverlayRenderer(
                source, filtered, overlayMaterial, sourceMaterialHash, false, "Submesh_" + stableIndex);
            links.Add(overlay);
            created++;
        }
        return FinishOverlayRoot(root, created);
    }

    public GameObject CreateFullMeshOverlay(Material overlayMaterial, string label)
    {
        if (_modelRoot == null || overlayMaterial == null) return null;
        string resolvedLabel = string.IsNullOrEmpty(label) ? "Full" : label;
        GameObject root = CreateOverlayRoot(resolvedLabel);
        DariusLolVfxLinkedObjects links = root.GetComponent<DariusLolVfxLinkedObjects>();
        int created = 0;
        SkinnedMeshRenderer[] renderers = _modelRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            SkinnedMeshRenderer source = renderers[i];
            if (!IsVisibleOverlaySource(source)) continue;
            GameObject overlay = CreateOverlayRenderer(
                source, source.sharedMesh, overlayMaterial, 0u, true, resolvedLabel + "_" + i);
            links.Add(overlay);
            created++;
        }
        return FinishOverlayRoot(root, created);
    }

    private GameObject CreateOverlayRoot(string label)
    {
        GameObject root = new GameObject("Darius_NativeOverlayGroup_" + label);
        root.transform.SetParent(_modelRoot.transform, false);
        root.AddComponent<DariusLolVfxLinkedObjects>();
        return root;
    }

    private static GameObject FinishOverlayRoot(GameObject root, int created)
    {
        if (created > 0) return root;
        if (root != null) UnityEngine.Object.Destroy(root);
        return null;
    }

    private static bool IsVisibleOverlaySource(SkinnedMeshRenderer source)
    {
        return source != null && source.enabled && source.sharedMesh != null &&
               !IsHiddenPresentationRenderer(source) && !IsNativeOverlayObject(source.gameObject);
    }

    private static bool RendererContainsMaterialHash(Renderer renderer, uint materialHash)
    {
        if (renderer == null) return false;
        Material[] materials = renderer.sharedMaterials;
        for (int i = 0; i < materials.Length; i++)
        {
            Material material = materials[i];
            if (material != null && RiotStringHash(StripRuntimeSuffix(material.name)) == materialHash) return true;
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
                if (sourceMaterial != null && RiotStringHash(StripRuntimeSuffix(sourceMaterial.name)) == selectedMaterialHash)
                    materials[i] = material;
            }
        }
        renderer.sharedMaterials = materials;
        return go;
    }
}
