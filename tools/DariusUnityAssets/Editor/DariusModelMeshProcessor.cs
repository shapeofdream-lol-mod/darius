#if UNITY_EDITOR
using UnityEngine;

internal static class DariusModelMeshProcessor
{
    public static void ProcessPrefab(GameObject root)
    {
        if (root == null) return;
        ProcessRenderers(root);
    }

    private static void ProcessRenderers(GameObject root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        foreach (Renderer renderer in renderers)
        {
            if (renderer == null) continue;
            SkinnedMeshRenderer skinned = renderer as SkinnedMeshRenderer;
            if (skinned == null) continue;
            skinned.updateWhenOffscreen = true;
            skinned.localBounds = ExpandBounds(skinned.localBounds);
        }
    }

    private static Bounds ExpandBounds(Bounds bounds)
    {
        bounds.Expand(2f);
        return bounds;
    }
}
#endif
