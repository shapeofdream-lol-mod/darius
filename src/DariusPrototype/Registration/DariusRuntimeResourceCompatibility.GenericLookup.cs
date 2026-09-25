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

}
