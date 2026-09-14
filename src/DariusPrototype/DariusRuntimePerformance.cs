// Native-model presentation guard only. General gameplay cadence, diagnostics, HUD and legacy
// cleanup belong to their dedicated branches. Keeping this helper free of legacy GLB type references
// lets the static-audit branch remove old implementations without creating a merge dependency here.
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
            DariusLog.DebugInfo("PERF-MODEL", "Optimized native skinned renderers root=" + root.name +
                " offscreenDisabled=" + offscreenDisabled + " authoredHiddenDisabled=" + hiddenDisabled);
    }

    private static bool IsAuthoredHiddenRenderer(SkinnedMeshRenderer renderer)
    {
        if (renderer == null || renderer.sharedMesh == null) return false;
        string meshName = renderer.sharedMesh.name;
        return !string.IsNullOrEmpty(meshName) &&
            meshName.IndexOf("AuthoredHiddenOnly", StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
