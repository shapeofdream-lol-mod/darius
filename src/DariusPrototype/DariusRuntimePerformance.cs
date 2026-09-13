using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

// Transitional performance guards for runtime systems that predate this audit.
// Presentation is moving to Unity/Shape-of-Dreams native Animator + EntityAnimation. Gameplay
// state that currently lives in MonoBehaviour.Update is bounded to the same 30 Hz cadence used by
// Shape of Dreams LogicBehaviour until those types can be migrated without changing persistence or
// network lifecycle contracts in the same patch.
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

// At 120/144/240 Hz the old C# animation interpreter previously sampled and rewrote the full
// skeleton every rendered frame. Keep 60 Hz gameplay visually unchanged while coalescing very
// high-refresh frames. Native Animator assets will remove this patch entirely after asset cutover.
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

// Apply Unity's normal off-screen culling contract after the legacy GLB has finished creating its
// SkinnedMeshRenderers. updateWhenOffscreen is deliberately not a permanent correctness mechanism;
// the native asset build must author conservative renderer bounds instead.
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

// God-King's Wolf_Mat lives on the authored-hidden mesh. Alpha-zero materials still leave a live
// skinned renderer, so disable that renderer outside Spell4 and only wake it for the real reveal.
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

// Hemorrhage state uses one-second damage ticks and time-based expiry, but its MonoBehaviour Update
// used to scan all bleeding targets at render frequency. Match Shape of Dreams' native logic cadence
// (30 Hz) until this runtime can be converted directly to LogicBehaviour without mixing that larger
// lifecycle change into the model migration.
[HarmonyPatch(typeof(DariusHemorrhageRuntime), "Update")]
internal static class DariusHemorrhageLogicCadencePatch
{
    private const float LogicInterval = 1f / 30f;
    private static readonly Dictionary<int, float> NextTickByInstance = new Dictionary<int, float>();

    [HarmonyPrefix]
    private static bool Prefix(DariusHemorrhageRuntime __instance)
    {
        if (__instance == null) return true;
        int id = __instance.GetInstanceID();
        float now = Time.time;
        float next;
        if (NextTickByInstance.TryGetValue(id, out next) && now + 0.0001f < next)
            return false;
        NextTickByInstance[id] = now + LogicInterval;
        return true;
    }

    internal static void Forget(DariusHemorrhageRuntime runtime)
    {
        if (runtime != null) NextTickByInstance.Remove(runtime.GetInstanceID());
    }
}

[HarmonyPatch(typeof(DariusHemorrhageRuntime), "OnDestroy")]
internal static class DariusHemorrhageLogicCadenceCleanupPatch
{
    [HarmonyPostfix]
    private static void Postfix(DariusHemorrhageRuntime __instance)
    {
        DariusHemorrhageLogicCadencePatch.Forget(__instance);
    }
}

// Equipment state performs timer expiry, movement accumulation, deferred-damage queues and status
// refreshes. None of those rules need render-frame frequency; 30 Hz also caps the cost of the
// Black-Cleaver dictionary walk and room/status probes at the game's normal logic cadence.
[HarmonyPatch(typeof(DariusEquipmentRuntime), "Update")]
internal static class DariusEquipmentLogicCadencePatch
{
    private const float LogicInterval = 1f / 30f;
    private static readonly Dictionary<int, float> NextTickByInstance = new Dictionary<int, float>();

    [HarmonyPrefix]
    private static bool Prefix(DariusEquipmentRuntime __instance)
    {
        if (__instance == null) return true;
        int id = __instance.GetInstanceID();
        float now = Time.time;
        float next;
        if (NextTickByInstance.TryGetValue(id, out next) && now + 0.0001f < next) return false;
        NextTickByInstance[id] = now + LogicInterval;
        return true;
    }

    internal static void Forget(DariusEquipmentRuntime runtime)
    {
        if (runtime != null) NextTickByInstance.Remove(runtime.GetInstanceID());
    }
}

[HarmonyPatch(typeof(DariusEquipmentRuntime), "OnDestroy")]
internal static class DariusEquipmentLogicCadenceCleanupPatch
{
    [HarmonyPostfix]
    private static void Postfix(DariusEquipmentRuntime __instance)
    {
        DariusEquipmentLogicCadencePatch.Forget(__instance);
    }
}

// Constellation maintenance contains periodic nearby-enemy physics scans plus health/bonus state.
// Keep that work on the same 30 Hz logic budget instead of letting a 240 Hz monitor multiply it.
[HarmonyPatch(typeof(DariusConstellationRuntime), "Update")]
internal static class DariusConstellationLogicCadencePatch
{
    private const float LogicInterval = 1f / 30f;
    private static readonly Dictionary<int, float> NextTickByInstance = new Dictionary<int, float>();

    [HarmonyPrefix]
    private static bool Prefix(DariusConstellationRuntime __instance)
    {
        if (__instance == null) return true;
        int id = __instance.GetInstanceID();
        float now = Time.time;
        float next;
        if (NextTickByInstance.TryGetValue(id, out next) && now + 0.0001f < next) return false;
        NextTickByInstance[id] = now + LogicInterval;
        return true;
    }

    internal static void Forget(DariusConstellationRuntime runtime)
    {
        if (runtime != null) NextTickByInstance.Remove(runtime.GetInstanceID());
    }
}

[HarmonyPatch(typeof(DariusConstellationRuntime), "OnDestroy")]
internal static class DariusConstellationLogicCadenceCleanupPatch
{
    [HarmonyPostfix]
    private static void Postfix(DariusConstellationRuntime __instance)
    {
        DariusConstellationLogicCadencePatch.Forget(__instance);
    }
}