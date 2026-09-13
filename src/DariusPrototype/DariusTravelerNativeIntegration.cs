using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;

// Independent traveler implementation for Hero_Darius.
// Runtime-created Hero/Skin resources remain necessary because the stock mod loader does not
// extend the game's Addressables catalog with custom Hero/Skin GUIDs. Model playback itself now
// prefers Unity-native assets and Shape of Dreams' EntityAnimation contract.
public sealed class Hero_Darius : Hero
{
    public override void OnModelLoaded()
    {
        base.OnModelLoaded();
        try
        {
            DariusNativeModelBridge native = GetComponentInChildren<DariusNativeModelBridge>(true);
            DariusTravelerModelInstance legacy = GetComponentInChildren<DariusTravelerModelInstance>(true);
            if (native != null && native.IsReady) native.BindHero(this);
            else if (legacy != null) legacy.BindHero(this);

            DariusBasicAttackVisualRuntime attackVisual = GetComponent<DariusBasicAttackVisualRuntime>();
            if (attackVisual == null) attackVisual = gameObject.AddComponent<DariusBasicAttackVisualRuntime>();
            attackVisual.Bind(this);
            DariusLog.Info("TRAVELER-MODEL", "Hero_Darius.OnModelLoaded native=" + (native != null && native.IsReady) +
                " legacy=" + (legacy != null && (native == null || !native.IsReady)) + " attackVisual=" + (attackVisual != null));
        }
        catch (Exception e)
        {
            DariusLog.Exception("TRAVELER-MODEL", e, "Hero_Darius.OnModelLoaded bridge bind failed");
        }
    }
}

internal static class DariusNativeAnimationMode
{
    public static bool IsNative(EntityAnimation animation)
    {
        if (animation == null) return false;
        try
        {
            Hero_Darius hero = animation.GetComponent<Hero_Darius>();
            if (hero == null) hero = animation.GetComponentInParent<Hero_Darius>();
            if (hero == null) return false;
            DariusNativeModelBridge bridge = hero.GetComponentInChildren<DariusNativeModelBridge>(true);
            return bridge != null && bridge.IsReady;
        }
        catch { return false; }
    }

    public static bool IsNative(EntityVisual visual)
    {
        if (visual == null) return false;
        try
        {
            Hero_Darius hero = visual.GetComponent<Hero_Darius>();
            if (hero == null) hero = visual.GetComponentInParent<Hero_Darius>();
            if (hero == null) return false;
            DariusNativeModelBridge bridge = hero.GetComponentInChildren<DariusNativeModelBridge>(true);
            return bridge != null && bridge.IsReady;
        }
        catch { return false; }
    }
}

// Legacy compatibility only. The raw runtime-GLB model is missing parts of the stock EntityModel
// hierarchy and historically triggered one benign tail NRE. Never suppress that exception when a
// native Unity model is active: native mode is expected to satisfy the real game contract.
[HarmonyPatch]
internal static class DariusEntityVisualLoadModelFinalizerPatch
{
    private static MethodBase TargetMethod()
    {
        return AccessTools.Method(typeof(EntityVisual), "LoadModelLocal", new[] { typeof(EntityModel) });
    }

    private static Exception Finalizer(EntityVisual __instance, Exception __exception)
    {
        if (__exception == null || __instance == null) return __exception;
        if (!(__exception is NullReferenceException)) return __exception;
        if (DariusNativeAnimationMode.IsNative(__instance)) return __exception;
        try
        {
            Hero_Darius hero = __instance.GetComponent<Hero_Darius>();
            if (hero == null) hero = __instance.GetComponentInParent<Hero_Darius>();
            if (hero == null) return __exception;
            DariusLog.DebugInfoThrottled("MODEL-NATIVE-GUARD", "legacy-load-model-tail",
                "Suppressed stock EntityVisual.LoadModelLocal tail NullReference for legacy GLB fallback.", 20.0);
            return null;
        }
        catch
        {
            return __exception;
        }
    }
}

// These two guards are retained only for the legacy GLB fallback. Native bundle models deliberately
// return true so Shape of Dreams can execute ReplaceAnimationLocal and its normal networked ability
// animation receiver again.
[HarmonyPatch]
internal static class DariusEntityAnimationReplaceAnimationLocalPatch
{
    private static MethodBase TargetMethod()
    {
        return typeof(EntityAnimation).GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .FirstOrDefault(m => m.Name == "ReplaceAnimationLocal" && m.GetParameters().Length == 2);
    }

    private static bool Prefix(EntityAnimation __instance)
    {
        if (__instance == null) return true;
        if (DariusNativeAnimationMode.IsNative(__instance)) return true;
        try
        {
            Hero_Darius hero = __instance.GetComponent<Hero_Darius>();
            if (hero == null) hero = __instance.GetComponentInParent<Hero_Darius>();
            if (hero == null) return true;
            DariusLog.DebugInfoThrottled("ANIM-NATIVE-GUARD", "legacy-replace-local",
                "Skipped stock ReplaceAnimationLocal only because Hero_Darius is using the legacy GLB fallback.", 20.0);
            return false;
        }
        catch { return true; }
    }
}

[HarmonyPatch]
internal static class DariusEntityAnimationAbilityRpcPatch
{
    private static MethodBase TargetMethod()
    {
        return typeof(EntityAnimation).GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .FirstOrDefault(m => m.Name.StartsWith("UserCode_RpcPlayAbilityAnimation", StringComparison.Ordinal) &&
                                 m.GetParameters().Length == 3);
    }

    private static bool Prefix(EntityAnimation __instance)
    {
        if (__instance == null) return true;
        if (DariusNativeAnimationMode.IsNative(__instance)) return true;
        try
        {
            Hero_Darius hero = __instance.GetComponent<Hero_Darius>();
            if (hero == null) hero = __instance.GetComponentInParent<Hero_Darius>();
            if (hero == null) return true;
            DariusLog.DebugInfoThrottled("ANIM-NATIVE-GUARD", "legacy-ability-rpc",
                "Skipped stock ability-animation RPC only because Hero_Darius is using the legacy GLB fallback.", 20.0);
            return false;
        }
        catch { return true; }
    }
}