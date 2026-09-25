using HarmonyLib;

[HarmonyPatch]
public static class DariusDejaVuLifecyclePatch
{
    // Temporary safety guard while custom StarEffect identity still relies on the unsupported
    // resource bridge. Do not re-register content/profile/type caches from Validate callbacks.
    [HarmonyPrefix]
    [HarmonyPatch(typeof(DewProfile), nameof(DewProfile.Validate))]
    private static void DewProfile_Validate_Prefix(DewProfile __instance, out DariusConstellationPersistence.GuardState __state)
    {
        __state = DariusConstellationPersistence.BeforeValidate(__instance);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(DewProfile), nameof(DewProfile.Validate))]
    private static void DewProfile_Validate_Postfix(DewProfile __instance, DariusConstellationPersistence.GuardState __state)
    {
        DariusConstellationPersistence.AfterValidate(__instance, __state);
    }
}
