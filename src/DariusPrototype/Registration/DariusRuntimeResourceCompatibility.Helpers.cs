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

    private static void EntityAbilityOnStartServerPrefix(EntityAbility __instance)
    {
        if (__instance == null) return;
        Hero_Darius hero = null;
        try { hero = __instance.GetComponent<Hero_Darius>(); } catch { }
        if (hero == null) return;
        try
        {
            DariusNativeAttackBinder binder = hero.GetComponent<DariusNativeAttackBinder>();
            if (binder == null) binder = hero.gameObject.AddComponent<DariusNativeAttackBinder>();
            binder.EnsureBound("EntityAbility.OnStartServer prefix");
            DariusLog.Info("ATK-NATIVE-BIND", "Verified Darius-owned attack preset immediately before native EntityAbility.OnStartServer.");
        }
        catch (Exception e)
        {
            DariusLog.Exception("ATK-NATIVE-BIND", e, "EntityAbility.OnStartServer binding failed");
        }
    }

    private static void ActorPrepareAndSpawnPrefix(Actor __instance)
    {
        if (__instance == null) return;
        bool isDariusRuntimeActor = (__instance is Hero_Darius) ||
                                    (__instance is At_DariusAxe) ||
                                    (__instance is Ai_DariusAxe) ||
                                    (__instance is Ai_DariusAxe_Crit) ||
                                    DariusFormalRegistry.IsDariusRuntimeObject(__instance);
        if (!isDariusRuntimeActor) return;
        try
        {
            GameObject go = __instance.gameObject;
            if (go != null && !go.activeSelf)
            {
                go.SetActive(true);
                DariusLog.DebugInfo("SPAWN-COMPAT", "Activated runtime Darius actor before PrepareAndSpawn: " + __instance.name);
            }

            // Do not rebuild NetworkIdentity.NetworkBehaviours on a live actor here. Mirror initializes
            // that cache during the normal Awake/spawn lifecycle. Re-running it immediately before
            // PrepareAndSpawn can invalidate Host-client serialization state and cause a local
            // disconnect when the basic-attack instance is spawned. Template-time initialization
            // remains in DariusTravelerRegistry.CreateAndRegisterNativeBasicAttack().
        }
        catch (Exception e)
        {
            DariusLog.Exception("SPAWN-COMPAT", e, "PrepareAndSpawn activation guard failed");
        }
    }

}
