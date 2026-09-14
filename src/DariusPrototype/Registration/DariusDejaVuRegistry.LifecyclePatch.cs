[HarmonyPatch]
public static class DariusDejaVuLifecyclePatch
{
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
        DariusLog.DebugInfo("DEJAVU", "DewProfile.Validate postfix -> re-injecting Darius candidates.");
        DariusDejaVuRegistry.RegisterProfile(__instance);
        DariusDejaVuRegistry.RegisterProfileStats(DewSave.profileStats);
        DariusDejaVuRegistry.RegisterCollectables();
        DariusConstellationPersistence.AfterValidate(__instance, __state);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(DewGameContentSettings), nameof(DewGameContentSettings.Init))]
    private static void ContentSettings_Init_Postfix(DewGameContentSettings __instance)
    {
        if (__instance == (DewBuildProfile.current != null ? DewBuildProfile.current.content : null))
        {
            DariusLog.DebugInfoThrottled("DEJAVU", "content-init", "DewGameContentSettings.Init postfix -> re-injecting Darius candidate names.", 20.0);
            DariusDejaVuRegistry.RegisterContentSettings(__instance);
        }
    }
}