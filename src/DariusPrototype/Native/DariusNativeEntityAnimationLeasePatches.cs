using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;

[HarmonyPatch(typeof(EntityAnimation), nameof(EntityAnimation.FrameUpdate))]
internal static class DariusNativeEntityAnimationFramePatch
{
    [HarmonyPrefix]
    private static bool Prefix(EntityAnimation __instance)
    {
        return !DariusNativeEntityAnimationLease.IsOwned(__instance);
    }
}

[HarmonyPatch(typeof(EntityAnimation), nameof(EntityAnimation.LogicUpdate))]
internal static class DariusNativeEntityAnimationLogicPatch
{
    [HarmonyPrefix]
    private static bool Prefix(EntityAnimation __instance)
    {
        return !DariusNativeEntityAnimationLease.IsOwned(__instance);
    }
}

[HarmonyPatch]
internal static class DariusNativeEntityAnimationAbilityPatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        return typeof(EntityAnimation).GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .Where(m => m.Name == "PlayAbilityAnimation");
    }

    [HarmonyPrefix]
    private static bool Prefix(EntityAnimation __instance)
    {
        return !DariusNativeEntityAnimationLease.IsOwned(__instance);
    }
}

internal static class DariusNativeEntityAnimationLease
{
    public static bool IsOwned(EntityAnimation animation)
    {
        Hero_Darius hero = DariusNativeAnimationMode.FindHero(animation);
        DariusNativeModelBridge bridge = hero != null ? hero.NativeModelBridge : null;
        return bridge != null && bridge.OwnsAnimatorAction;
    }
}