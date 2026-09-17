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
    private DariusNativeModelBridge _nativeModelBridge;

    internal DariusNativeModelBridge NativeModelBridge
    {
        get
        {
            if (_nativeModelBridge == null || !_nativeModelBridge.IsReady)
                _nativeModelBridge = GetComponentInChildren<DariusNativeModelBridge>(true);
            return _nativeModelBridge != null && _nativeModelBridge.IsReady ? _nativeModelBridge : null;
        }
    }

    public override void OnModelLoaded()
    {
        base.OnModelLoaded();
        try
        {
            DariusNativeModelBridge native = NativeModelBridge;
            if (native != null)
            {
                native.BindHero(this);
                if (!native.IsReady) native = null;
            }

            DariusTravelerModelInstance legacy = native == null
                ? GetComponentInChildren<DariusTravelerModelInstance>(true)
                : null;
            if (legacy != null) legacy.BindHero(this);

            DariusBasicAttackVisualRuntime attackVisual = GetComponent<DariusBasicAttackVisualRuntime>();
            if (attackVisual == null) attackVisual = gameObject.AddComponent<DariusBasicAttackVisualRuntime>();
            attackVisual.Bind(this);
            DariusLog.Info("TRAVELER-MODEL", "Hero_Darius.OnModelLoaded native=" + (native != null) +
                " legacy=" + (legacy != null) + " attackVisual=" + (attackVisual != null));
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
        Hero_Darius hero = FindHero(animation);
        return hero != null && hero.NativeModelBridge != null;
    }

    public static bool IsNative(EntityVisual visual)
    {
        Hero_Darius hero = FindHero(visual);
        return hero != null && hero.NativeModelBridge != null;
    }

    internal static Hero_Darius FindHero(UnityEngine.Component component)
    {
        if (component == null) return null;
        try
        {
            Hero_Darius hero = component.GetComponent<Hero_Darius>();
            return hero != null ? hero : component.GetComponentInParent<Hero_Darius>();
        }
        catch
        {
            return null;
        }
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

        Hero_Darius hero = DariusNativeAnimationMode.FindHero(__instance);
        if (hero == null || hero.NativeModelBridge != null) return __exception;

        DariusLog.DebugInfoThrottled("MODEL-NATIVE-GUARD", "legacy-load-model-tail",
            "Suppressed stock EntityVisual.LoadModelLocal tail NullReference for legacy GLB fallback.", 20.0);
        return null;
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
        Hero_Darius hero = DariusNativeAnimationMode.FindHero(__instance);
        if (hero == null || hero.NativeModelBridge != null) return true;

        DariusLog.DebugInfoThrottled("ANIM-NATIVE-GUARD", "legacy-replace-local",
            "Skipped stock ReplaceAnimationLocal only because Hero_Darius is using the legacy GLB fallback.", 20.0);
        return false;
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
        Hero_Darius hero = DariusNativeAnimationMode.FindHero(__instance);
        if (hero == null || hero.NativeModelBridge != null) return true;

        DariusLog.DebugInfoThrottled("ANIM-NATIVE-GUARD", "legacy-ability-rpc",
            "Skipped stock ability-animation RPC only because Hero_Darius is using the legacy GLB fallback.", 20.0);
        return false;
    }
}
