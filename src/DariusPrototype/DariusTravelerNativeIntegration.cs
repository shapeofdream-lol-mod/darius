using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;

// Independent traveler implementation for Hero_Darius.
// This deliberately uses runtime-created resources because the stock mod loader does not
// extend the game's Addressables catalog with custom Hero/Skin GUIDs.
public sealed class Hero_Darius : Hero
{
    public override void OnModelLoaded()
    {
        base.OnModelLoaded();
        try
        {
            DariusTravelerModelInstance model = GetComponentInChildren<DariusTravelerModelInstance>(true);
            if (model != null) model.BindHero(this);
            DariusBasicAttackVisualRuntime attackVisual = GetComponent<DariusBasicAttackVisualRuntime>();
            if (attackVisual == null) attackVisual = gameObject.AddComponent<DariusBasicAttackVisualRuntime>();
            attackVisual.Bind(this);
            DariusLog.Info("TRAVELER-MODEL", "Hero_Darius.OnModelLoaded modelBridge=" + (model != null) + " attackVisual=" + (attackVisual != null));
        }
        catch (Exception e)
        {
            DariusLog.Exception("TRAVELER-MODEL", e, "Hero_Darius.OnModelLoaded bridge bind failed");
        }
    }
}

// The clean runtime EntityModel intentionally has no stock body-renderer hierarchy. In the current
// game build, EntityVisual.LoadModelLocal completes the Darius skin/custom-model activation and then
// dereferences a stock renderer member that does not exist, throwing one benign NullReferenceException
// on spawn. Do not skip LoadModelLocal (the GLB is activated inside that lifecycle); suppress only that
// known tail exception for Hero_Darius after the custom model has already taken over.
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
        try
        {
            Hero_Darius hero = __instance.GetComponent<Hero_Darius>();
            if (hero == null) hero = __instance.GetComponentInParent<Hero_Darius>();
            if (hero == null) return __exception;
            DariusLog.DebugInfoThrottled("MODEL-NATIVE-GUARD", "load-model-tail",
                "Suppressed stock EntityVisual.LoadModelLocal tail NullReference after Darius GLB takeover.", 20.0);
            return null;
        }
        catch
        {
            return __exception;
        }
    }
}

// Hero_Darius renders and animates its independent GLB directly. The stock EntityAnimation RPC
// still tries to replace clips on the stripped generic model contract; in rc4 logs that path threw
// EntityAnimation.ReplaceAnimationLocal NullReferenceException repeatedly during combat. Skip only
// that stock replacement for Hero_Darius while leaving every other traveler untouched.
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
        try
        {
            Hero_Darius hero = __instance.GetComponent<Hero_Darius>();
            if (hero == null) hero = __instance.GetComponentInParent<Hero_Darius>();
            if (hero == null) return true;
            DariusLog.DebugInfoThrottled("ANIM-NATIVE-GUARD", "replace-local",
                "Skipped stock EntityAnimation.ReplaceAnimationLocal for Hero_Darius; custom GLB animation is authoritative.", 20.0);
            return false;
        }
        catch
        {
            return true;
        }
    }
}

// ReplaceAnimationLocal is not the only dereference inside the stock ability-animation RPC.
// rc6 skipped that helper but the generated UserCode_RpcPlayAbilityAnimation method continued and
// still threw at EntityAnimation.cs:228. Stop the whole receiver-side stock RPC for Hero_Darius;
// the custom GLB hooks above are the authoritative animation path.
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
        try
        {
            Hero_Darius hero = __instance.GetComponent<Hero_Darius>();
            if (hero == null) hero = __instance.GetComponentInParent<Hero_Darius>();
            if (hero == null) return true;
            DariusLog.DebugInfoThrottled("ANIM-NATIVE-GUARD", "ability-rpc",
                "Skipped stock RpcPlayAbilityAnimation receiver for Hero_Darius; custom GLB animation is authoritative.", 20.0);
            return false;
        }
        catch
        {
            return true;
        }
    }
}
