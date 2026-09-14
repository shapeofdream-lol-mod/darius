using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Mirror;
using UnityEngine;

public static partial class DariusRuntimeResourceCompatibility
{
    private static int PatchClosedGenericLookup(Harmony harmony, string methodName, Type resourceType, string prefixName)
    {
        int count = 0;
        MethodInfo[] methods = typeof(DewResources).GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
        for (int i = 0; i < methods.Length; i++)
        {
            MethodInfo raw = methods[i];
            if (raw.Name != methodName || !raw.IsGenericMethodDefinition) continue;
            Type[] ga = raw.GetGenericArguments();
            if (ga.Length != 1) continue;
            ParameterInfo[] ps = raw.GetParameters();
            if (ps.Length < 1 || ps[0].ParameterType != typeof(string)) continue;
            MethodInfo closed;
            try { closed = raw.MakeGenericMethod(resourceType); } catch { continue; }
            if (!resourceType.IsAssignableFrom(closed.ReturnType) && closed.ReturnType != resourceType) continue;
            count += PatchOne(harmony, closed, prefixName, null, "DewResources." + methodName + "<" + resourceType.Name + ">");
        }
        return count;
    }

    private static int PatchFinalizer(Harmony harmony, MethodInfo method, string finalizerName, string label)
    {
        if (method == null) return 0;
        try
        {
            HarmonyMethod finalizer = new HarmonyMethod(AccessTools.Method(typeof(DariusRuntimeResourceCompatibility), finalizerName));
            harmony.Patch(method, finalizer: finalizer);
            DariusLog.DebugInfo("RESOURCE-COMPAT", "Patched " + label + " -> " + method);
            return 1;
        }
        catch (Exception e)
        {
            DariusLog.Exception("RESOURCE-COMPAT", e, "Failed to patch " + label);
            return 0;
        }
    }

    private static int PatchOne(Harmony harmony, MethodInfo method, string prefixName, string postfixName, string label)
    {
        if (method == null)
        {
            DariusLog.Warn("RESOURCE-COMPAT", "Target not found: " + label);
            return 0;
        }
        try
        {
            HarmonyMethod prefix = string.IsNullOrEmpty(prefixName) ? null : new HarmonyMethod(AccessTools.Method(typeof(DariusRuntimeResourceCompatibility), prefixName));
            HarmonyMethod postfix = string.IsNullOrEmpty(postfixName) ? null : new HarmonyMethod(AccessTools.Method(typeof(DariusRuntimeResourceCompatibility), postfixName));
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

        DariusTravelerRegistry.EnsureCoreRegisteredForLookup("DewResources.GetByName early lookup: " + __0);
        if (isHero) __result = DariusTravelerRegistry.HeroPrefab;
        else
        {
            Skin skin;
            if (!DariusTravelerRegistry.TryResolveRuntimeSkin(__0, out skin)) return true;
            __result = skin;
        }
        if (__result == null) return true;
        DariusLog.DebugInfoThrottled("RES-NAME", __0, "Non-generic GetByName intercepted Workshop-safe key=" + __0 + " result=" + __result.name, 1.25);
        return false;
    }

    private static bool GetByShortTypeNameObjectPrefix(string __0, ref UnityEngine.Object __result)
    {
        if (string.IsNullOrEmpty(__0)) return true;
        if (__0 != DariusTravelerRegistry.HeroName && __0 != DariusTravelerRegistry.HeroGuid) return true;

        DariusTravelerRegistry.EnsureCoreRegisteredForLookup("DewResources.GetByShortTypeName early lookup: " + __0);
        __result = DariusTravelerRegistry.HeroPrefab;
        if (__result == null) return true;
        DariusLog.DebugInfoThrottled("RES-SHORTTYPE", __0, "Non-generic GetByShortTypeName intercepted Workshop-safe key=" + __0 + " result=" + __result.name, 1.25);
        return false;
    }

    private static bool GetByGuidObjectPrefix(string __0, ref UnityEngine.Object __result)
    {
        if (string.IsNullOrEmpty(__0)) return true;
        bool isHero = __0 == DariusTravelerRegistry.HeroGuid;
        bool isSkin = DariusTravelerRegistry.IsRuntimeSkinKey(__0);
        if (!isHero && !isSkin) return true;

        DariusTravelerRegistry.EnsureCoreRegisteredForLookup("DewResources.GetByGuid early lookup: " + __0);
        if (isHero) __result = DariusTravelerRegistry.HeroPrefab;
        else
        {
            Skin skin;
            if (!DariusTravelerRegistry.TryResolveRuntimeSkin(__0, out skin)) return true;
            __result = skin;
        }
        if (__result == null) return true;
        DariusLog.DebugInfoThrottled("RES-GUID", __0, "Non-generic GetByGuid intercepted Workshop-safe key=" + __0 + " result=" + __result.name, 1.25);
        return false;
    }

    private static bool SharedGenericDariusLookupPrefix(string __0, ref UnityEngine.Object __result)
    {
        if (string.IsNullOrEmpty(__0)) return true;
        bool isHero = __0 == DariusTravelerRegistry.HeroName || __0 == DariusTravelerRegistry.HeroGuid;
        bool isSkin = DariusTravelerRegistry.IsRuntimeSkinKey(__0);
        if (!isHero && !isSkin) return true;

        if (isHero)
        {
            if (DariusTravelerRegistry.HeroPrefab == null) DariusTravelerRegistry.RepairRuntimeRegistration("shared generic Hero lookup self-heal: " + __0);
            __result = DariusTravelerRegistry.HeroPrefab;
        }
        else
        {
            Skin skin;
            if (!DariusTravelerRegistry.TryResolveRuntimeSkin(__0, out skin))
            {
                DariusTravelerRegistry.RepairRuntimeRegistration("shared generic Skin lookup self-heal: " + __0);
                if (!DariusTravelerRegistry.TryResolveRuntimeSkin(__0, out skin)) return true;
            }
            __result = skin;
        }
        if (__result == null) return true;
        DariusLog.DebugInfoThrottled("RES-GENERIC", __0, "Shared generic runtime lookup intercepted exact " + (isHero ? "Hero" : "Skin") +
            " key=" + __0 + " result=" + __result.name, 1.25);
        return false;
    }

    private static bool HeroLookupPrefix(string __0, ref Hero __result)
    {
        if (string.IsNullOrEmpty(__0)) return true;
        if (__0 != DariusTravelerRegistry.HeroName && __0 != DariusTravelerRegistry.HeroGuid) return true;
        if (DariusTravelerRegistry.HeroPrefab == null)
            DariusTravelerRegistry.RepairRuntimeRegistration("generic Hero lookup self-heal: " + __0);
        __result = DariusTravelerRegistry.HeroPrefab;
        DariusLog.DebugInfoThrottled("RES-HERO", __0, "Closed generic Hero lookup intercepted key=" + __0 + " result=" + (__result != null ? __result.name : "<null>"), 1.25);
        return false;
    }

    private static bool SkinLookupPrefix(string __0, ref Skin __result)
    {
        if (string.IsNullOrEmpty(__0) || !DariusTravelerRegistry.IsRuntimeSkinKey(__0)) return true;
        if (!DariusTravelerRegistry.TryResolveRuntimeSkin(__0, out __result))
        {
            DariusTravelerRegistry.RepairRuntimeRegistration("generic Skin lookup self-heal: " + __0);
            if (!DariusTravelerRegistry.TryResolveRuntimeSkin(__0, out __result)) return true;
        }
        DariusLog.DebugInfoThrottled("RES-SKIN", __0, "Closed generic Skin lookup intercepted key=" + __0 + " result=" + (__result != null ? __result.name : "<null>"), 1.25);
        return false;
    }
}