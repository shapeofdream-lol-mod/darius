using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

// Presentation-only guards retained on this branch. Gameplay/runtime cadence cleanup belongs to the
// separate static-audit branch; this file is intentionally limited to model/skinning cost.
internal static class DariusRuntimePerformance
{
    public static void OptimizeSkinnedRenderers(GameObject root, bool disableAuthoredHidden)
    {
        if (root == null) return;
        SkinnedMeshRenderer[] renderers = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        int offscreenDisabled = 0;
        int hiddenDisabled = 0;
        for (int i = 0; i < renderers.Length; i++)
        {
            SkinnedMeshRenderer renderer = renderers[i];
            if (renderer == null) continue;
            if (renderer.updateWhenOffscreen)
            {
                renderer.updateWhenOffscreen = false;
                offscreenDisabled++;
            }

            if (disableAuthoredHidden && IsAuthoredHiddenRenderer(renderer) && renderer.enabled)
            {
                renderer.enabled = false;
                hiddenDisabled++;
            }
        }

        if (offscreenDisabled > 0 || hiddenDisabled > 0)
            DariusLog.DebugInfo("PERF-MODEL", "Optimized skinned renderers root=" + root.name +
                " offscreenDisabled=" + offscreenDisabled + " authoredHiddenDisabled=" + hiddenDisabled);
    }

    public static bool IsAuthoredHiddenRenderer(SkinnedMeshRenderer renderer)
    {
        if (renderer == null || renderer.sharedMesh == null) return false;
        string meshName = renderer.sharedMesh.name;
        return !string.IsNullOrEmpty(meshName) &&
            meshName.IndexOf("AuthoredHiddenOnly", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    public static void SetGodKingHiddenRendererEnabled(DariusGlbRuntimeModel model, bool enabled)
    {
        if (model == null || model.root == null) return;
        SkinnedMeshRenderer[] renderers = model.root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            SkinnedMeshRenderer renderer = renderers[i];
            if (!IsAuthoredHiddenRenderer(renderer)) continue;
            renderer.updateWhenOffscreen = false;
            renderer.enabled = enabled;
        }
    }
}

// Transitional fallback only: native AssetBundle models bypass the C# GLB interpreter completely.
// Until the local binary bundle is rebuilt, cap high-refresh displays from evaluating that legacy
// interpreter more than 90 times per second. This does not change gameplay state.
[HarmonyPatch(typeof(DariusGlbRuntimeModel), nameof(DariusGlbRuntimeModel.Tick))]
internal static class DariusLegacyGlbTickBudgetPatch
{
    private const float MinimumSampleInterval = 1f / 90f;
    private static readonly Dictionary<DariusGlbRuntimeModel, float> PendingDt =
        new Dictionary<DariusGlbRuntimeModel, float>();

    [HarmonyPrefix]
    private static bool Prefix(DariusGlbRuntimeModel __instance, ref float dt)
    {
        if (__instance == null) return true;
        float accumulated;
        PendingDt.TryGetValue(__instance, out accumulated);
        accumulated += Mathf.Max(0f, dt);
        if (accumulated + 0.0001f < MinimumSampleInterval)
        {
            PendingDt[__instance] = accumulated;
            return false;
        }

        dt = Mathf.Min(accumulated, 0.1f);
        PendingDt[__instance] = 0f;
        return true;
    }

    internal static void Forget(DariusGlbRuntimeModel model)
    {
        if (model != null) PendingDt.Remove(model);
    }
}

[HarmonyPatch(typeof(DariusGlbRuntimeModel), nameof(DariusGlbRuntimeModel.Dispose))]
internal static class DariusLegacyGlbDisposePerformancePatch
{
    [HarmonyPostfix]
    private static void Postfix(DariusGlbRuntimeModel __instance)
    {
        DariusLegacyGlbTickBudgetPatch.Forget(__instance);
    }
}

[HarmonyPatch(typeof(DariusTravelerModelInstance), "OnEnable")]
internal static class DariusLegacyModelRendererPerformancePatch
{
    [HarmonyPostfix]
    private static void Postfix(DariusTravelerModelInstance __instance)
    {
        if (__instance != null)
            DariusRuntimePerformance.OptimizeSkinnedRenderers(__instance.gameObject, true);
    }
}

[HarmonyPatch(typeof(DariusGlbRuntimeModel), nameof(DariusGlbRuntimeModel.CreateSubmeshOverlay))]
internal static class DariusLegacySubmeshOverlayPerformancePatch
{
    [HarmonyPostfix]
    private static void Postfix(GameObject __result)
    {
        if (__result != null)
            DariusRuntimePerformance.OptimizeSkinnedRenderers(__result, false);
    }
}

[HarmonyPatch(typeof(DariusGlbRuntimeModel), nameof(DariusGlbRuntimeModel.CreateFullMeshOverlay))]
internal static class DariusLegacyFullMeshOverlayPerformancePatch
{
    [HarmonyPostfix]
    private static void Postfix(GameObject __result)
    {
        if (__result != null)
            DariusRuntimePerformance.OptimizeSkinnedRenderers(__result, false);
    }
}

[HarmonyPatch(typeof(DariusGlbRuntimeModel), nameof(DariusGlbRuntimeModel.SetMaterialVisible))]
internal static class DariusGodKingHiddenRendererPerformancePatch
{
    [HarmonyPrefix]
    private static void Prefix(DariusGlbRuntimeModel __instance, string sourceName, bool visible)
    {
        if (visible && string.Equals(sourceName, "Wolf_Mat", StringComparison.OrdinalIgnoreCase))
            DariusRuntimePerformance.SetGodKingHiddenRendererEnabled(__instance, true);
    }

    [HarmonyPostfix]
    private static void Postfix(DariusGlbRuntimeModel __instance, string sourceName, bool visible)
    {
        if (!visible && string.Equals(sourceName, "Wolf_Mat", StringComparison.OrdinalIgnoreCase))
            DariusRuntimePerformance.SetGodKingHiddenRendererEnabled(__instance, false);
    }
}
