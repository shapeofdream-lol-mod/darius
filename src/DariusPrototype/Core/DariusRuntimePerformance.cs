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

        Shader nativeBodyShader = Shader.Find("Universal Render Pipeline/Unlit");
        if (nativeBodyShader == null) nativeBodyShader = Shader.Find("Unlit/Texture");
        if (nativeBodyShader == null) nativeBodyShader = Shader.Find("Sprites/Default");
        if (nativeBodyShader == null)
            DariusLog.Error("NATIVE-MATERIAL", "No runtime body shader found; imported FBX materials cannot be rebound safely.");

        SkinnedMeshRenderer[] renderers = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        int offscreenEnabled = 0;
        int hiddenDisabled = 0;
        int reboundMaterials = 0;
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

            // FBX materials are authored in the throwaway Unity build project, but the bundle is
            // consumed by Shape of Dreams' URP player. Keep the imported textures/material names,
            // then bind them to a shader resolved from the actual running game. This avoids the
            // "correct shadow, invisible body" failure caused by an incompatible imported shader.
            Material[] materials = renderer.sharedMaterials;
            for (int mi = 0; mi < materials.Length; mi++)
            {
                Material material = materials[mi];
                if (material == null || nativeBodyShader == null) continue;
                if (RebindNativeBodyMaterial(material, nativeBodyShader)) reboundMaterials++;
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
            " authoredHiddenDisabled=" + hiddenDisabled + " reboundMaterials=" + reboundMaterials);
    }

    private static bool RebindNativeBodyMaterial(Material material, Shader shader)
    {
        if (material == null || shader == null) return false;

        string previousShader = material.shader != null ? material.shader.name : "<null>";
        Texture texture = null;
        Vector2 textureScale = Vector2.one;
        Vector2 textureOffset = Vector2.zero;
        string[] textureProperties = { "_BaseMap", "_BaseColorMap", "_MainTex", "_Albedo" };
        for (int i = 0; i < textureProperties.Length; i++)
        {
            string property = textureProperties[i];
            if (!material.HasProperty(property)) continue;
            Texture candidate = material.GetTexture(property);
            if (candidate == null) continue;
            texture = candidate;
            textureScale = material.GetTextureScale(property);
            textureOffset = material.GetTextureOffset(property);
            break;
        }
        if (texture == null)
        {
            try
            {
                texture = material.mainTexture;
                textureScale = material.mainTextureScale;
                textureOffset = material.mainTextureOffset;
            }
            catch { }
        }

        Color tint = Color.white;
        if (material.HasProperty("_BaseColor")) tint = material.GetColor("_BaseColor");
        else if (material.HasProperty("_Color")) tint = material.GetColor("_Color");
        tint.a = 1f;

        material.shader = shader;
        if (texture != null)
        {
            if (material.HasProperty("_BaseMap"))
            {
                material.SetTexture("_BaseMap", texture);
                material.SetTextureScale("_BaseMap", textureScale);
                material.SetTextureOffset("_BaseMap", textureOffset);
            }
            if (material.HasProperty("_MainTex"))
            {
                material.SetTexture("_MainTex", texture);
                material.SetTextureScale("_MainTex", textureScale);
                material.SetTextureOffset("_MainTex", textureOffset);
            }
        }
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", tint);
        if (material.HasProperty("_Color")) material.SetColor("_Color", tint);

        // Character body materials are opaque. Authored hidden God-King geometry is controlled by
        // its separate renderer, so it does not need transparent body materials either.
        material.SetOverrideTag("RenderType", "Opaque");
        if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 0f);
        if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 1f);
        if (material.HasProperty("_SrcBlend")) material.SetFloat("_SrcBlend", 1f);
        if (material.HasProperty("_DstBlend")) material.SetFloat("_DstBlend", 0f);
        if (material.HasProperty("_AlphaClip")) material.SetFloat("_AlphaClip", 0f);
        if (material.HasProperty("_Cull")) material.SetFloat("_Cull", 0f);
        if (material.HasProperty("_CullMode")) material.SetFloat("_CullMode", 0f);
        if (material.HasProperty("_CullModeForward")) material.SetFloat("_CullModeForward", 0f);
        material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.DisableKeyword("_ALPHATEST_ON");
        material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        material.renderQueue = 2000;

        DariusLog.DebugInfo("NATIVE-MATERIAL", "material=" + material.name +
            " shader=" + previousShader + " -> " + shader.name +
            " texture=" + (texture != null ? texture.name : "<null>") +
            " tint=" + tint);
        return !string.Equals(previousShader, shader.name, StringComparison.Ordinal);
    }

    private static bool IsAuthoredHiddenRenderer(SkinnedMeshRenderer renderer)
    {
        if (renderer == null || renderer.sharedMesh == null) return false;
        string meshName = renderer.sharedMesh.name;
        return !string.IsNullOrEmpty(meshName) &&
            meshName.IndexOf("AuthoredHiddenOnly", StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
