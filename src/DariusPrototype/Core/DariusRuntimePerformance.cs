using System;
using UnityEngine;

// Native-model presentation guard only. General gameplay cadence, diagnostics, HUD and legacy
// cleanup belong to their dedicated branches. Keeping this helper free of legacy GLB type references
// lets the static-audit branch remove old implementations without creating a merge dependency here.
internal static class DariusRuntimePerformance
{
    public static void OptimizeSkinnedRenderers(GameObject root, bool disableAuthoredHidden)
    {
        if (root == null) return;

        Animator[] animators = root.GetComponentsInChildren<Animator>(true);
        int alwaysAnimate = 0;
        for (int i = 0; i < animators.Length; i++)
        {
            Animator animator = animators[i];
            if (animator == null || animator.cullingMode == AnimatorCullingMode.AlwaysAnimate) continue;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            alwaysAnimate++;
        }

        SkinnedMeshRenderer[] renderers = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        int offscreenEnabled = 0;
        int hiddenDisabled = 0;
        int cullDisabled = 0;
        for (int i = 0; i < renderers.Length; i++)
        {
            SkinnedMeshRenderer renderer = renderers[i];
            if (renderer == null) continue;

            // Riot-derived rigs can arrive through Blender/FBX with skinned bounds that are not
            // reliable enough for Unity's visibility-driven Animator/SMR culling. The legacy GLB
            // path always evaluated these meshes offscreen, so keep the same correctness contract
            // until the native bundle owns validated per-skin bounds.
            if (!renderer.updateWhenOffscreen)
            {
                renderer.updateWhenOffscreen = true;
                offscreenEnabled++;
            }

            if (disableAuthoredHidden && IsAuthoredHiddenRenderer(renderer) && renderer.enabled)
            {
                renderer.enabled = false;
                hiddenDisabled++;
            }

            // The source LoL meshes use mirrored transforms/winding. The legacy GLB renderer
            // compensated by reversing triangle winding and disabling material culling. The FBX
            // path preserves the authored transform, so keep the equivalent native rendering
            // contract here without rewriting skinned mesh topology or bind poses.
            Material[] materials = renderer.sharedMaterials;
            for (int mi = 0; mi < materials.Length; mi++)
            {
                Material material = materials[mi];
                if (material == null) continue;
                bool changed = false;
                if (material.HasProperty("_Cull") && material.GetFloat("_Cull") != 0f)
                {
                    material.SetFloat("_Cull", 0f);
                    changed = true;
                }
                if (material.HasProperty("_CullMode") && material.GetFloat("_CullMode") != 0f)
                {
                    material.SetFloat("_CullMode", 0f);
                    changed = true;
                }
                if (material.HasProperty("_CullModeForward") && material.GetFloat("_CullModeForward") != 0f)
                {
                    material.SetFloat("_CullModeForward", 0f);
                    changed = true;
                }
                if (changed) cullDisabled++;
            }

            Mesh mesh = renderer.sharedMesh;
            Bounds local = renderer.localBounds;
            Bounds world = renderer.bounds;
            DariusLog.DebugInfo("NATIVE-RENDER", "root=" + root.name + " index=" + i +
                " renderer=" + renderer.name + " enabled=" + renderer.enabled +
                " active=" + renderer.gameObject.activeInHierarchy +
                " mesh=" + (mesh != null ? mesh.name : "<null>") +
                " verts=" + (mesh != null ? mesh.vertexCount.ToString() : "0") +
                " localCenter=" + local.center + " localSize=" + local.size +
                " worldCenter=" + world.center + " worldSize=" + world.size +
                " lossyScale=" + renderer.transform.lossyScale +
                " materials=" + materials.Length);
        }

        DariusLog.DebugInfo("PERF-MODEL", "Prepared native skinned renderers root=" + root.name +
            " renderers=" + renderers.Length + " animators=" + animators.Length +
            " alwaysAnimate=" + alwaysAnimate + " offscreenEnabled=" + offscreenEnabled +
            " authoredHiddenDisabled=" + hiddenDisabled + " doubleSidedMaterials=" + cullDisabled);
    }

    private static bool IsAuthoredHiddenRenderer(SkinnedMeshRenderer renderer)
    {
        if (renderer == null || renderer.sharedMesh == null) return false;
        string meshName = renderer.sharedMesh.name;
        return !string.IsNullOrEmpty(meshName) &&
            meshName.IndexOf("AuthoredHiddenOnly", StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
