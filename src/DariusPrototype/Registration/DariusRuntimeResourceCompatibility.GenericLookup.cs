using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

public static partial class DariusRuntimeResourceCompatibility
{
    private static int PatchOne(Harmony harmony, MethodInfo method, string prefixName, string postfixName, string label)
    {
        if (method == null)
        {
            DariusLog.Warn("RESOURCE-COMPAT", "Target not found: " + label);
            return 0;
        }
        try
        {
            HarmonyMethod prefix = string.IsNullOrEmpty(prefixName)
                ? null
                : new HarmonyMethod(AccessTools.Method(typeof(DariusRuntimeResourceCompatibility), prefixName));
            HarmonyMethod postfix = string.IsNullOrEmpty(postfixName)
                ? null
                : new HarmonyMethod(AccessTools.Method(typeof(DariusRuntimeResourceCompatibility), postfixName));
            harmony.Patch(method, prefix: prefix, postfix: postfix);
            DariusLog.DebugInfo("RESOURCE-COMPAT", "Patched " + label + " -> " + method);
            return 1;
        }
        catch (Exception e)
        {
            DariusLog.Exception("RESOURCE-COMPAT", e, "Failed to patch " + label);
            return 0;
        }
    }

    private static bool GetByNameObjectPrefix(string __0, ref UnityEngine.Object __result)
    {
        if (string.IsNullOrEmpty(__0)) return true;
        bool isHero = __0 == DariusTravelerRegistry.HeroName || __0 == DariusTravelerRegistry.HeroGuid;
        bool isSkin = DariusTravelerRegistry.IsRuntimeSkinKey(__0);
        if (!isHero && !isSkin) return true;

        if (isHero) __result = DariusTravelerRegistry.HeroPrefab;
        else
        {
            Skin skin;
            if (!DariusTravelerRegistry.TryResolveRuntimeSkin(__0, out skin)) return true;
            __result = skin;
        }
        if (__result == null) return true;
        DariusLog.DebugInfoThrottled("RES-NAME", __0,
            "Non-generic GetByName intercepted Workshop-safe key=" + __0 + " result=" + __result.name, 1.25);
        return false;
    }

    private static bool GetByShortTypeNameObjectPrefix(string __0, ref UnityEngine.Object __result)
    {
        if (string.IsNullOrEmpty(__0)) return true;
        if (__0 != DariusTravelerRegistry.HeroName && __0 != DariusTravelerRegistry.HeroGuid) return true;

        __result = DariusTravelerRegistry.HeroPrefab;
        if (__result == null) return true;
        DariusLog.DebugInfoThrottled("RES-SHORTTYPE", __0,
            "Non-generic GetByShortTypeName intercepted Workshop-safe key=" + __0 + " result=" + __result.name, 1.25);
        return false;
    }

    private static bool GetByGuidObjectPrefix(string __0, ref UnityEngine.Object __result)
    {
        if (string.IsNullOrEmpty(__0)) return true;
        bool isHero = __0 == DariusTravelerRegistry.HeroGuid;
        bool isSkin = DariusTravelerRegistry.IsRuntimeSkinKey(__0);
        if (!isHero && !isSkin) return true;

        if (isHero) __result = DariusTravelerRegistry.HeroPrefab;
        else
        {
            Skin skin;
            if (!DariusTravelerRegistry.TryResolveRuntimeSkin(__0, out skin)) return true;
            __result = skin;
        }
        if (__result == null) return true;
        DariusLog.DebugInfoThrottled("RES-GUID", __0,
            "Non-generic GetByGuid intercepted Workshop-safe key=" + __0 + " result=" + __result.name, 1.25);
        return false;
    }
}
