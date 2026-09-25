using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Mirror;
using UnityEngine;
using DewInternal;

// Connects Darius Memories to the native Deja Vu start-loadout system.
// Public content/profile APIs are used directly; only the missing custom-resource type discovery
// remains behind the narrow Dew type-cache compatibility boundary.
public static partial class DariusDejaVuRegistry
{
    private static readonly string[] SkillNames =
    {
        "St_Darius_Decimate",
        "St_Darius_CripplingStrike",
        "St_Darius_Apprehend",
        "St_Darius_NoxianGuillotine",
        "St_D_Darius_Hemorrhage"
    };

    private static readonly Type[] SkillTypes =
    {
        typeof(St_Darius_Decimate),
        typeof(St_Darius_CripplingStrike),
        typeof(St_Darius_Apprehend),
        typeof(St_Darius_NoxianGuillotine),
        typeof(St_D_Darius_Hemorrhage)
    };

    private static bool _localizationPatched;

    // The Ctrl skill-drag UI calls ConvertDescriptionNodesToText several times per second, and
    // sometimes multiple times in one frame. Cache both equipped trigger lookup and the final
    // level-aware text so ordinary drag/hover refreshes stay allocation-free.
    private static readonly Dictionary<string, SkillTrigger> TooltipSkillCache = new Dictionary<string, SkillTrigger>(StringComparer.Ordinal);

    private static readonly Dictionary<string, string> TooltipTextCache = new Dictionary<string, string>(StringComparer.Ordinal);

    private static int _tooltipCacheHeroId;

    public static void RegisterAll()
    {
        try
        {
            RegisterContentSettings(DewBuildProfile.current != null ? DewBuildProfile.current.content : null);
            RegisterCollectables();
            RegisterProfileStats(DewSave.profileStats);
            DariusLog.Info("DEJAVU", "Native Deja Vu content/type/stats registration complete; profile unlock ownership remains in Traveler late registration.");
        }
        catch (Exception e)
        {
            DariusLog.Exception("DEJAVU", e, "RegisterAll failed");
        }
    }

    public static void ResetPatchInstallState()
    {
        _localizationPatched = false;
        TooltipSkillCache.Clear();
        TooltipTextCache.Clear();
        _tooltipCacheHeroId = 0;
    }

    public static void InstallLocalizationPatches(Harmony harmony)
    {
        if (_localizationPatched || harmony == null) return;
        try
        {
            MethodInfo stringPostfix = AccessTools.Method(typeof(DariusDejaVuRegistry), nameof(LocalizationStringPostfix));
            MethodInfo nodesPostfix = AccessTools.Method(typeof(DariusDejaVuRegistry), nameof(LocalizationNodesPostfix));
            MethodInfo convertedDescriptionPostfix = AccessTools.Method(typeof(DariusDejaVuRegistry), nameof(LocalizationConvertedDescriptionPostfix));
            int patched = 0;

            foreach (MethodInfo method in typeof(DewLocalization).GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance))
            {
                if (!method.Name.StartsWith("GetSkill", StringComparison.Ordinal) &&
                    !method.Name.StartsWith("GetGem", StringComparison.Ordinal) &&
                    !method.Name.StartsWith("GetStar", StringComparison.Ordinal) &&
                    !method.Name.StartsWith("GetHero", StringComparison.Ordinal) &&
                    !method.Name.StartsWith("GetSkin", StringComparison.Ordinal))
                    continue;

                ParameterInfo[] ps = method.GetParameters();
                if (ps.Length < 1 || ps[0].ParameterType != typeof(string)) continue;

                if (method.ReturnType == typeof(string))
                {
                    harmony.Patch(method, postfix: new HarmonyMethod(stringPostfix));
                    patched++;
                    DariusLog.DebugInfo("DEJAVU-I18N", "Patched string localization method " + MethodLabel(method));
                }
                else if (method.ReturnType == typeof(List<LocaleNode>))
                {
                    harmony.Patch(method, postfix: new HarmonyMethod(nodesPostfix));
                    patched++;
                    DariusLog.DebugInfo("DEJAVU-I18N", "Patched node localization method " + MethodLabel(method));
                }
            }

            // Skin/Hero labels in the current SoD UI use the generic UI table directly rather than
            // dedicated GetSkin*/GetHero* methods. Missing runtime keys surface as "ui!<key>".
            MethodInfo uiValue = typeof(DewLocalization).GetMethod(
                "GetUIValue",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static,
                null,
                new[] { typeof(string) },
                null);
            if (uiValue != null)
            {
                harmony.Patch(uiValue, postfix: new HarmonyMethod(stringPostfix));
                patched++;
                DariusLog.DebugInfo("DEJAVU-I18N", "Patched UI localization method " + MethodLabel(uiValue));
            }

            int converterPatched = 0;

            // The stock tooltip converts LocaleNodes using DescriptionSettings after GetSkillDescription.
            // Our Darius descriptions are runtime-generated text nodes, so intercept this final conversion
            // only for Darius SkillTrigger contexts and render current/previous Memory levels explicitly.
            foreach (MethodInfo method in typeof(DewLocalization).GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance))
            {
                // Do not guess the DescriptionSettings parameter position/type name.  The current
                // game build changed that signature and the old filter silently patched zero
                // converters.  Harmony's object[] __args is signature-agnostic, so patch every
                // string-returning overload and locate settings from the runtime arguments.
                if (method.Name != "ConvertDescriptionNodesToText" || method.ReturnType != typeof(string)) continue;
                harmony.Patch(method, postfix: new HarmonyMethod(convertedDescriptionPostfix));
                patched++;
                converterPatched++;
                DariusLog.DebugInfo("DEJAVU-I18N", "Patched native description conversion " + MethodLabel(method));
            }

            _localizationPatched = true;
            DariusLog.Info("DEJAVU-I18N", "Localization patch discovery complete. patchedMethods=" + patched + " converters=" + converterPatched);
            if (converterPatched == 0)
                DariusLog.Warn("DEJAVU-I18N", "No ConvertDescriptionNodesToText overload was patched; live native-style numeric rendering will not activate.");
        }
        catch (Exception e)
        {
            DariusLog.Exception("DEJAVU-I18N", e, "Dynamic localization patch install failed; raw internal names may be shown.");
        }
    }
}