using HarmonyLib;
using UnityEngine;

// Native activation is owned by DariusNativeModelHost. The legacy component only runs when an
// actual local GLB source exists, which keeps a developer fallback without pretending it ships.
[HarmonyPatch(typeof(DariusTravelerModelInstance), "OnEnable")]
internal static class DariusLegacyModelFallbackGuardPatch
{
    [HarmonyPrefix]
    private static bool Prefix(DariusTravelerModelInstance __instance)
    {
        if (__instance == null) return false;
        GameObject owner = __instance.gameObject;
        if (DariusNativeModelAssets.IsActive(owner)) return false;
        if (DariusNativeModelAssets.CanUseLegacyFallback(owner)) return true;

        DariusLog.Error("NATIVE-MODEL",
            "Skipped legacy GLB loader because no local GLB source exists; published runtime requires the native bundle.");
        return false;
    }
}
