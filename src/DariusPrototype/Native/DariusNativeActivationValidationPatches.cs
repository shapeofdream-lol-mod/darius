using HarmonyLib;

namespace DariusPrototype;

/// <summary>
/// Native model activation guard.
/// Keeps the native route from becoming authoritative unless the bridge reached a valid state.
/// The patch intentionally does not create a second loader or fallback path; it only validates the commit point.
/// </summary>
internal static class DariusNativeActivationValidationPatches
{
    [HarmonyPatch(typeof(DariusNativeModelAssets), "TryActivate")]
    private static class ValidateActivation
    {
        private static void Postfix(DariusTravelerModelInstance legacy, ref bool __result)
        {
            if (!__result || legacy == null)
                return;

            var bridge = legacy.GetComponent<DariusNativeModelBridge>();
            if (bridge == null || !bridge.IsReady)
            {
                __result = false;
            }
        }
    }
}
