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
    private static void IsSkinIncludedPostfix(string __0, ref bool __result)
    {
        if (DariusTravelerRegistry.IsRuntimeSkinKey(__0))
        {
            __result = true;
            DariusLog.DebugInfoThrottled("RESOURCE-COMPAT", "skin:" + __0, "Forced IsSkinIncludedInGame=true for " + __0, 2.0);
        }
    }

}
