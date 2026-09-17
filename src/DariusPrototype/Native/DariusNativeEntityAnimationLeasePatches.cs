using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;

// Native actions only suppress SoD's per-frame presentation write and competing ability requests.
// LogicUpdate keeps running so death/status/gameplay animation state is never frozen by a visual lease.
[HarmonyPatch(typeof(EntityAnimation), nameof(EntityAnimation.FrameUpdate))]
internal static class DariusNativeEntityAnimationFramePatch
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
    private static readonly HashSet<EntityAnimation> Owned = new HashSet<EntityAnimation>();

    public static void Acquire(EntityAnimation animation)
    {
        if (animation != null) Owned.Add(animation);
    }

    public static void Release(EntityAnimation animation)
    {
        if (!object.ReferenceEquals(animation, null)) Owned.Remove(animation);
    }

    public static bool IsOwned(EntityAnimation animation)
    {
        return animation != null && Owned.Count != 0 && Owned.Contains(animation);
    }

    public static void Clear()
    {
        Owned.Clear();
    }
}
