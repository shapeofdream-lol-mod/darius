using System;
using UnityEngine;

public sealed partial class DariusNativeModelBridge : MonoBehaviour
{
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

    private static bool IsNativeOverlayObject(GameObject go)
    {
        string name = go != null ? go.name : string.Empty;
        return name.StartsWith("Darius_NativeOverlay_", StringComparison.OrdinalIgnoreCase) ||
               name.StartsWith("Darius_NativeOverlayGroup_", StringComparison.OrdinalIgnoreCase);
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
