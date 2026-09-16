using System;
using UnityEngine;

public sealed partial class DariusNativeModelBridge : MonoBehaviour
{
    public GameObject CreateSubmeshOverlay(uint sourceMaterialHash, Material overlayMaterial)
    {
        if (_modelRoot == null || overlayMaterial == null) return null;
        GameObject root = CreateOverlayRoot("Submesh");
        DariusLolVfxLinkedObjects links = root.GetComponent<DariusLolVfxLinkedObjects>();
        int created = 0;
        SkinnedMeshRenderer[] renderers = _modelRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        for (int r = 0; r < renderers.Length; r++)
        {
            SkinnedMeshRenderer source = renderers[r];
            if (!IsVisibleOverlaySource(source)) continue;
            Material[] materials = source.sharedMaterials;
            for (int i = 0; i < materials.Length && i < source.sharedMesh.subMeshCount; i++)
            {
                Material material = materials[i];
                if (material == null || RiotStringHash(StripRuntimeSuffix(material.name)) != sourceMaterialHash) continue;
                GameObject overlay = CreateOverlayRenderer(source, overlayMaterial, i, "Submesh_" + r + "_" + i);
                links.Add(overlay);
                created++;
            }
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
            GameObject overlay = CreateOverlayRenderer(source, overlayMaterial, -1, resolvedLabel + "_" + i);
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
        return source != null && source.enabled && source.sharedMesh != null && !IsHiddenPresentationRenderer(source);
    }

    private static GameObject CreateOverlayRenderer(SkinnedMeshRenderer source, Material material, int selectedSubmesh, string label)
    {
        GameObject go = new GameObject("Darius_NativeOverlay_" + label);
        go.transform.SetParent(source.transform, false);
        SkinnedMeshRenderer renderer = go.AddComponent<SkinnedMeshRenderer>();
        Mesh mesh = source.sharedMesh;
        if (selectedSubmesh >= 0)
        {
            mesh = UnityEngine.Object.Instantiate(source.sharedMesh);
            mesh.name = source.sharedMesh.name + "_OverlaySubmesh" + selectedSubmesh;
            int[] triangles = source.sharedMesh.GetTriangles(selectedSubmesh);
            mesh.subMeshCount = 1;
            mesh.SetTriangles(triangles, 0, false);
            DariusNativeOverlayMeshLifetime lifetime = go.AddComponent<DariusNativeOverlayMeshLifetime>();
            lifetime.mesh = mesh;
        }
        renderer.sharedMesh = mesh;
        renderer.bones = source.bones;
        renderer.rootBone = source.rootBone;
        renderer.quality = source.quality;
        renderer.localBounds = source.localBounds;
        renderer.updateWhenOffscreen = false;
        if (selectedSubmesh >= 0)
        {
            renderer.sharedMaterials = new[] { material };
        }
        else
        {
            int count = Mathf.Max(1, mesh.subMeshCount);
            Material[] materials = new Material[count];
            for (int i = 0; i < count; i++) materials[i] = material;
            renderer.sharedMaterials = materials;
        }
        return go;
    }

    public bool SetGodKingWolfVisible(bool visible)
    {
        if (!IsGodKingSkin || _godKingWolfRenderers == null) return false;
        bool changed = false;
        for (int i = 0; i < _godKingWolfRenderers.Length; i++)
        {
            Renderer renderer = _godKingWolfRenderers[i];
            if (renderer == null) continue;
            renderer.enabled = visible;
            changed = true;
        }
        return changed;
    }

    private static bool IsHiddenPresentationRenderer(Renderer renderer)
    {
        if (renderer == null) return false;
        if (IsHiddenPresentationObject(renderer.gameObject)) return true;
        SkinnedMeshRenderer skinned = renderer as SkinnedMeshRenderer;
        string name = skinned != null && skinned.sharedMesh != null ? skinned.sharedMesh.name : null;
        return !string.IsNullOrEmpty(name) &&
            (name.EndsWith("_Hidden", StringComparison.OrdinalIgnoreCase) ||
             name.EndsWith("_WolfHidden", StringComparison.OrdinalIgnoreCase) ||
             name.EndsWith("_StaticHidden", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsGodKingWolfRenderer(Renderer renderer)
    {
        if (renderer == null) return false;
        string objectName = renderer.gameObject != null ? renderer.gameObject.name : string.Empty;
        if (objectName.StartsWith("DariusHidden_Wolf_", StringComparison.OrdinalIgnoreCase)) return true;
        SkinnedMeshRenderer skinned = renderer as SkinnedMeshRenderer;
        string meshName = skinned != null && skinned.sharedMesh != null ? skinned.sharedMesh.name : null;
        if (!string.IsNullOrEmpty(meshName) && meshName.EndsWith("_WolfHidden", StringComparison.OrdinalIgnoreCase)) return true;
        return IsHiddenPresentationRenderer(renderer) && RendererContainsMaterial(renderer, "Wolf_Mat") &&
               !RendererContainsMaterial(renderer, "Throne");
    }

    private static bool RendererContainsMaterial(Renderer renderer, string sourceName)
    {
        Material[] materials = renderer.sharedMaterials;
        for (int i = 0; i < materials.Length; i++)
        {
            Material material = materials[i];
            if (material != null && StripRuntimeSuffix(material.name).IndexOf(sourceName, StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
        }
        return false;
    }

    private static bool IsHiddenPresentationObject(GameObject go)
    {
        string name = go != null ? go.name : string.Empty;
        return name.StartsWith("DariusHidden_", StringComparison.OrdinalIgnoreCase);
    }

    private static string StripRuntimeSuffix(string name)
    {
        if (string.IsNullOrEmpty(name)) return string.Empty;
        const string instance = " (Instance)";
        if (name.EndsWith(instance, StringComparison.Ordinal)) name = name.Substring(0, name.Length - instance.Length);
        const string runtime = "_Runtime";
        if (name.EndsWith(runtime, StringComparison.OrdinalIgnoreCase)) name = name.Substring(0, name.Length - runtime.Length);
        return name;
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