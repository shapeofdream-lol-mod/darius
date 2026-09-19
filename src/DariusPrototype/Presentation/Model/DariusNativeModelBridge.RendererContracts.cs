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
        string name = renderer != null && renderer.gameObject != null
            ? renderer.gameObject.name
            : string.Empty;
        return DariusNativeAssetContract.IsHiddenObjectName(name);
    }

    private static bool IsGodKingWolfRenderer(Renderer renderer)
    {
        string name = renderer != null && renderer.gameObject != null
            ? renderer.gameObject.name
            : string.Empty;
        return DariusNativeAssetContract.IsWolfHiddenObjectName(name);
    }

    private static bool IsNativeOverlayObject(GameObject go)
    {
        string name = go != null ? go.name : string.Empty;
        return name.StartsWith("Darius_NativeOverlay_", StringComparison.OrdinalIgnoreCase) ||
               name.StartsWith("Darius_NativeOverlayGroup_", StringComparison.OrdinalIgnoreCase);
    }
}
