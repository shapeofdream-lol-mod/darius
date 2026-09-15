using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

public sealed partial class DariusNativeModelBridge : MonoBehaviour
{
    public GameObject CreateSubmeshOverlay(uint sourceMaterialHash, Material overlayMaterial)
    {
        if (_modelRoot == null || overlayMaterial == null) return null;
        SkinnedMeshRenderer[] renderers = _modelRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        for (int r = 0; r < renderers.Length; r++)
        {
            SkinnedMeshRenderer source = renderers[r];
            if (source == null || source.sharedMesh == null || IsHiddenPresentationObject(source.gameObject)) continue;
            Material[] sourceMaterials = source.sharedMaterials;
            for (int i = 0; i < sourceMaterials.Length && i < source.sharedMesh.subMeshCount; i++)
            {
                Material material = sourceMaterials[i];
                if (material == null || RiotStringHash(StripRuntimeSuffix(material.name)) != sourceMaterialHash) continue;
                return CreateOverlayRenderer(source, overlayMaterial, i, "Submesh_" + i);
            }
        }
        return null;
    }

    public GameObject CreateFullMeshOverlay(Material overlayMaterial, string label)
    {
        if (_modelRoot == null || overlayMaterial == null) return null;
        SkinnedMeshRenderer[] renderers = _modelRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            SkinnedMeshRenderer source = renderers[i];
            if (source == null || source.sharedMesh == null || IsHiddenPresentationObject(source.gameObject)) continue;
            return CreateOverlayRenderer(source, overlayMaterial, -1, string.IsNullOrEmpty(label) ? "Full" : label);
        }
        return null;
    }

    private static GameObject CreateOverlayRenderer(SkinnedMeshRenderer source, Material material, int selectedSubmesh, string label)
    {
        GameObject go = new GameObject("Darius_NativeOverlay_" + label);
        go.transform.SetParent(source.transform, false);
        SkinnedMeshRenderer renderer = go.AddComponent<SkinnedMeshRenderer>();
        renderer.sharedMesh = source.sharedMesh;
        renderer.bones = source.bones;
        renderer.rootBone = source.rootBone;
        renderer.quality = source.quality;
        renderer.updateWhenOffscreen = false;
        int count = Mathf.Max(1, source.sharedMesh.subMeshCount);
        Material[] materials = new Material[count];
        if (selectedSubmesh < 0)
        {
            for (int i = 0; i < count; i++) materials[i] = material;
        }
        else
        {
            Material invisible = CreateInvisibleMaterial(material);
            for (int i = 0; i < count; i++) materials[i] = i == selectedSubmesh ? material : invisible;
            DariusLolVfxLinkedObjects links = go.AddComponent<DariusLolVfxLinkedObjects>();
            links.Add(invisible);
        }
        renderer.sharedMaterials = materials;
        return go;
    }

    private static Material CreateInvisibleMaterial(Material template)
    {
        Material material = new Material(template);
        material.name = "Darius_NativeOverlay_Invisible";
        Color clear = new Color(0f, 0f, 0f, 0f);
        if (material.HasProperty("_Color")) material.SetColor("_Color", clear);
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", clear);
        material.color = clear;
        return material;
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
        if (go == null) return false;
        string name = go.name ?? string.Empty;
        return name.IndexOf("hidden", StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("wolf", StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("throne", StringComparison.OrdinalIgnoreCase) >= 0;
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
            char c = char.ToLowerInvariant(text[i]);
            h ^= (byte)c;
            h *= 16777619u;
        }
        return h;
    }
}