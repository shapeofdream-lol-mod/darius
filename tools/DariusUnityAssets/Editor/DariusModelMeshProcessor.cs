using UnityEditor;
using UnityEngine;

internal static class DariusModelMeshProcessor
{
    public static void ProcessPrefab(GameObject root)
    {
        if (root == null)
            return;

        ProcessRenderers(root);
        ProcessGodKingPresentation(root);
    }

    private static void ProcessRenderers(GameObject root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        foreach (Renderer renderer in renderers)
        {
            if (renderer == null)
                continue;

            SkinnedMeshRenderer skinned = renderer as SkinnedMeshRenderer;
            if (skinned != null)
            {
                skinned.updateWhenOffscreen = true;
                skinned.localBounds = ExpandBounds(skinned.localBounds);
            }
        }
    }

    private static Bounds ExpandBounds(Bounds bounds)
    {
        bounds.Expand(2f);
        return bounds;
    }

    private static void ProcessGodKingPresentation(GameObject root)
    {
        if (!root.name.ToLowerInvariant().Contains("god"))
            return;

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        foreach (Renderer renderer in renderers)
        {
            if (renderer != null && renderer.name.ToLowerInvariant().Contains("wolf"))
                renderer.enabled = false;
        }
    }
}
