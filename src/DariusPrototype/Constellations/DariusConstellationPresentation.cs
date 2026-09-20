using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;

// Keep the stock constellation browser state coherent when the list is rebuilt after a run,
// category switch, loadout migration, or star purchase. The stock UI keeps a hovered index
// separately from the rebuilt item list; a stale index is enough to make StarDetails repeatedly
// index past the end of the list until the menu is closed.
[HarmonyPatch(typeof(UI_Lobby_Constellations_StarList), "Refresh", new Type[] { })]
public static class DariusConstellationStarListRefreshPatch
{
    private static long _refreshStartedTicks;

    private static readonly MethodInfo GenericStarLookup = typeof(DewResources).GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
        .First(m => m.Name == "GetByType" && m.IsGenericMethodDefinition && m.GetParameters().Length == 2 && m.GetParameters()[0].ParameterType == typeof(Type));

    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        MethodInfo replacement = AccessTools.Method(typeof(DariusConstellationStarListRefreshPatch), nameof(GetStarForBrowser));
        foreach (CodeInstruction instruction in instructions)
        {
            MethodInfo called = instruction.operand as MethodInfo;
            if (called != null && called.IsGenericMethod && called.GetGenericMethodDefinition() == GenericStarLookup &&
                called.GetGenericArguments().Length == 1 && called.GetGenericArguments()[0] == typeof(StarEffect))
            {
                instruction.opcode = OpCodes.Call;
                instruction.operand = replacement;
            }
            yield return instruction;
        }
    }

    private static StarEffect GetStarForBrowser(Type type, ResourceLoadSettings settings)
    {
        if (type == null) return null;

        UnityEngine.Object runtimeObject;
        if (DariusFormalRegistry.TryGetResourceByExactType(type, out runtimeObject))
        {
            StarEffect runtimeStar = runtimeObject as StarEffect;
            if (runtimeStar != null) return runtimeStar;
        }

        return DewResources.GetByType<StarEffect>(type, settings);
    }

    [HarmonyPrefix]
    private static void Prefix(UI_Lobby_Constellations_StarList __instance)
    {
        if (__instance == null) return;
        _refreshStartedTicks = DateTime.UtcNow.Ticks;
        try
        {
            __instance.hoveredIndex = -1;
            if (__instance.listGroup != null) __instance.listGroup.currentIndex = -1;

            // Dew.ClearTypeReferences only resets _allHeroes. If another Mod interrupts the ensuing
            // rebuild, _allHeroes can be populated while _allStarTypes remains empty forever. Repair
            // that impossible partial-cache state once before the stock UI enumerates constellations.
            IReadOnlyList<Type> stars = Dew.allStarTypes;
            if (stars == null || stars.Count == 0)
            {
                Dew.ClearTypeReferences();
                Dew.InitAllTypeReferences();
                DariusConstellationRegistry.ReassertTypeCache();
                DariusLog.Warn("CONSTELLATION-UI", "Recovered an empty Dew star-type cache before constellation refresh.");
            }
        }
        catch (Exception e) { DariusLog.Exception("CONSTELLATION-UI", e, "Could not prepare global constellation list refresh"); }
    }

    [HarmonyPostfix]
    private static void Postfix(UI_Lobby_Constellations_StarList __instance)
    {
        if (__instance == null) return;
        try
        {
            int count = __instance.items != null ? __instance.items.Count : 0;
            double elapsedMs = _refreshStartedTicks > 0
                ? TimeSpan.FromTicks(DateTime.UtcNow.Ticks - _refreshStartedTicks).TotalMilliseconds
                : -1.0;
            if (__instance.hoveredIndex < 0 || __instance.hoveredIndex >= count) __instance.hoveredIndex = -1;
            if (__instance.listGroup != null && (__instance.listGroup.currentIndex < -1 || __instance.listGroup.currentIndex >= count))
                __instance.listGroup.currentIndex = -1;
            // Do not overwrite the shared constellation portrait/background widgets. The lobby reuses
            // them across travelers and does not reliably restore the previous Sprite, which can visually
            // contaminate stock heroes after visiting a custom traveler. Star icons remain isolated below.
            DariusLog.DebugInfo("CONSTELLATION-UI", "Global star list refresh count=" + count +
                " elapsedMs=" + elapsedMs.ToString("0.0"));
        }
        catch (Exception e)
        {
            DariusLog.Exception("CONSTELLATION-UI", e, "Could not normalize Darius constellation star-list selection");
        }
    }
}

// The stock property getters only check for -1; stale positive indices survive a list rebuild and
// then throw from every input poll. Return null for any index outside the current item list.
[HarmonyPatch(typeof(UI_Lobby_Constellations_StarList), "get_selectedStar")]
public static class DariusConstellationSelectedStarBoundsPatch
{
    [HarmonyPrefix]
    private static bool Prefix(UI_Lobby_Constellations_StarList __instance, ref StarEffect __result)
    {
        if (__instance != null && __instance.items != null && __instance.selectedIndex >= 0 && __instance.selectedIndex < __instance.items.Count)
            return true;
        __result = null;
        return false;
    }
}

[HarmonyPatch(typeof(UI_Lobby_Constellations_StarList), "get_hoveredStar")]
public static class DariusConstellationHoveredStarBoundsPatch
{
    [HarmonyPrefix]
    private static bool Prefix(UI_Lobby_Constellations_StarList __instance, ref StarEffect __result)
    {
        if (__instance != null && __instance.items != null && __instance.hoveredIndex >= 0 && __instance.hoveredIndex < __instance.items.Count)
            return true;
        __result = null;
        return false;
    }
}

// Preserve Riot's full-colour rune/ability artwork inside the stock star browser. The stock
// constellation UI normally treats star icons as monochrome masks and applies the branch colour;
// opaque League rune backgrounds therefore collapse into featureless red/green circles. For
// Darius-owned stars we keep the stock lock/dim behaviour, but neutralize branch tint on visible
// icon images and force the runtime Riot Sprite after both Setup and Refresh.
[HarmonyPatch(typeof(UI_Lobby_Constellations_StarItem), "Setup", new Type[] { typeof(StarEffect), typeof(int) })]
public static class DariusConstellationStarItemSetupPresentationPatch
{
    [HarmonyPostfix]
    private static void Postfix(UI_Lobby_Constellations_StarItem __instance, StarEffect __0)
    {
        DariusConstellationItemPresentation.RememberAndApply(__instance, __0);
    }
}

// Defensive release guard for stock constellation UI. Runtime-injected stars are hydrated from a
// native StarEffect contract above; if a game build still indexes a stock presentation array beyond
// its bounds, contain that UI-only exception for Hero_Darius instead of leaving the constellation
// menu in a permanently corrupted hover/detail state.
[HarmonyPatch(typeof(UI_Lobby_Constellations_StarItem), "Refresh", new Type[] { })]
public static class DariusConstellationStarItemRefreshGuard
{
    [HarmonyPostfix]
    private static void Postfix(UI_Lobby_Constellations_StarItem __instance)
    {
        DariusConstellationItemPresentation.ApplyRemembered(__instance);
    }

    [HarmonyFinalizer]
    private static Exception Finalizer(Exception __exception)
    {
        if (__exception == null) return null;
        try
        {
            bool darius = DewPlayer.local != null &&
                string.Equals(DewPlayer.local.selectedHeroType, DariusTravelerRegistry.HeroName, StringComparison.Ordinal);
            if (darius && (__exception is IndexOutOfRangeException || __exception is ArgumentOutOfRangeException))
            {
                DariusLog.Warn("CONSTELLATION-UI", "Contained stock StarItem.Refresh bounds exception for Hero_Darius: " + __exception.GetType().Name);
                return null;
            }
        }
        catch { }
        return __exception;
    }
}