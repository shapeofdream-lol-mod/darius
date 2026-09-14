// Prefer the Unity-native AssetBundle model before the legacy runtime GLB parser gets a chance to
// allocate meshes, decode textures, or start its C# animation interpreter. Returning false skips
// DariusTravelerModelInstance.OnEnable only when the native model was activated successfully.
[HarmonyPatch(typeof(DariusTravelerModelInstance), "OnEnable")]
internal static class DariusNativeModelActivationPatch
{
    [HarmonyPrefix]
    private static bool Prefix(DariusTravelerModelInstance __instance)
    {
        return !DariusNativeModelAssets.TryActivate(__instance);
    }
}

// Keep AssetBundle lifetime owned by the native-model feature rather than DariusPrototypeMod. This
// avoids overlapping the lifecycle-audit branch while still releasing bundle state during the
// registry's established runtime teardown transaction.
[HarmonyPatch(typeof(DariusTravelerRegistry), nameof(DariusTravelerRegistry.UnregisterRuntimeOnly))]
internal static class DariusNativeModelUnloadPatch
{
    [HarmonyPostfix]
    private static void Postfix()
    {
        DariusNativeModelAssets.Unload();
    }
}
