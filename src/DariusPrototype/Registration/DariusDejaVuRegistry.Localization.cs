using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Mirror;
using UnityEngine;
using DewInternal;

public static partial class DariusDejaVuRegistry
{
    private static bool LooksLikeDescriptionRequest(MethodBase method, string key)
    {
        string methodName = method != null ? method.Name : string.Empty;
        if (!string.IsNullOrEmpty(methodName) &&
            (methodName.IndexOf("Story", StringComparison.OrdinalIgnoreCase) >= 0 ||
             methodName.IndexOf("Lore", StringComparison.OrdinalIgnoreCase) >= 0 ||
             methodName.IndexOf("Description", StringComparison.OrdinalIgnoreCase) >= 0 ||
             methodName.IndexOf("Desc", StringComparison.OrdinalIgnoreCase) >= 0 ||
             methodName.IndexOf("Short", StringComparison.OrdinalIgnoreCase) >= 0))
            return true;
        if (string.IsNullOrEmpty(key)) return false;
        return key.EndsWith(".lore", StringComparison.OrdinalIgnoreCase) ||
               key.EndsWith(".desc", StringComparison.OrdinalIgnoreCase) ||
               key.EndsWith(".description", StringComparison.OrdinalIgnoreCase) ||
               key.EndsWith("_lore", StringComparison.OrdinalIgnoreCase) ||
               key.EndsWith("_desc", StringComparison.OrdinalIgnoreCase) ||
               key.EndsWith("_description", StringComparison.OrdinalIgnoreCase) ||
               key.IndexOf(".short", StringComparison.OrdinalIgnoreCase) >= 0 ||
               key.IndexOf("shortdesc", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static void LocalizationStringPostfix(MethodBase __originalMethod, string __0, ref string __result)
    {
        string key = __0;
        if (string.IsNullOrEmpty(key) || __originalMethod == null) return;
        string value;
        string starValue;
        bool wantsName = LooksLikeNameRequest(__originalMethod, key);
        bool wantsDescription = LooksLikeDescriptionRequest(__originalMethod, key);
        if (DariusLanguage.IsJapanese && DariusJapaneseLocalization.TryGet(key, wantsDescription, out value)) { __result = value; return; }

        if (key == DariusTravelerRegistry.HeroName || key == DariusTravelerRegistry.HeroGuid)
        {
            if (wantsName)
                __result = DariusLanguage.IsEnglish ? "The Hand of Noxus" : "诺克萨斯之手";
            else if (wantsDescription)
                __result = DariusLanguage.IsEnglish ? "Darius is one of Noxus's most feared and battle-hardened commanders. He tears through enemy lines with his great axe, turning every wound into the prelude to an execution." : "德莱厄斯是诺克萨斯最令人畏惧、久经沙场的统帅之一。他以巨斧和绝不退让的意志撕开敌阵，让每一道伤口都成为下一次处决的前奏。";
            return;
        }
        if (key == DariusTravelerRegistry.DefaultSkinName || key == DariusTravelerRegistry.SkinGuid || key == DariusTravelerRegistry.LegacySkinAlias)
        {
            if (wantsName)
                __result = DariusLanguage.IsEnglish ? "Classic Darius" : "经典德莱厄斯";
            else if (wantsDescription)
                __result = DariusLanguage.IsEnglish ? "The Hand of Noxus in his classic armor, wielding his great axe." : "诺克萨斯之手的经典战甲与巨斧。";
            return;
        }
        if (key == DariusTravelerRegistry.GodKingSkinName || key == DariusTravelerRegistry.GodKingSkinGuid)
        {
            if (wantsName) __result = DariusLanguage.IsEnglish ? "God-King Darius" : "神王 德莱厄斯";
            else if (wantsDescription) __result = DariusLanguage.IsEnglish ? "God-King Darius. Changes presentation only, not gameplay." : "神王德莱厄斯造型。仅改变模型、动画与表现，不改变角色玩法。";
            return;
        }
        if (key == DariusTravelerRegistry.DunkmasterSkinName || key == DariusTravelerRegistry.DunkmasterSkinGuid)
        {
            if (wantsName) __result = DariusLanguage.IsEnglish ? "Dunkmaster Darius" : "灌篮高手 德莱厄斯";
            else if (wantsDescription) __result = DariusLanguage.IsEnglish ? "Dunkmaster Darius, with its own model, rig, and animation profile." : "灌篮高手德莱厄斯造型。使用该皮肤独立的模型、骨骼与动画档案。";
            return;
        }
        if (key == DariusTravelerRegistry.MechaSkinName || key == DariusTravelerRegistry.MechaSkinGuid)
        {
            if (wantsName) __result = DariusLanguage.IsEnglish ? "Mecha Kingdoms Darius" : "机神 德莱厄斯";
            else if (wantsDescription) __result = DariusLanguage.IsEnglish ? "Mecha Kingdoms Darius, with its own model, rig, and animation profile." : "机神德莱厄斯造型。使用该皮肤独立的模型、骨骼与动画档案。";
            return;
        }
        if (DariusConstellationLocalization.TryName(key, out starValue))
        {
            if (wantsName)
                __result = starValue;
            else if (wantsDescription && DariusConstellationLocalization.TryDescription(key, out starValue))
                __result = starValue;
            return;
        }
        if (DariusFormalLocalization.TrySkillName(key, out value))
        {
            if (wantsName)
                __result = value;
            else if (!string.IsNullOrEmpty(__originalMethod.Name) && __originalMethod.Name.IndexOf("Memory", StringComparison.OrdinalIgnoreCase) >= 0)
                __result = DariusLanguage.IsEnglish ? "A combat Memory from Darius, the Hand of Noxus." : "来自诺克萨斯之手·德莱厄斯的战斗记忆。";
            else if (wantsDescription && DariusFormalLocalization.TrySkillDescription(key, out value))
                __result = value;
        }
        else if (DariusFormalLocalization.IsHemorrhageKey(key))
        {
            if (wantsName)
                __result = DariusLanguage.IsEnglish ? "Hemorrhage" : "出血";
            else if (!string.IsNullOrEmpty(__originalMethod.Name) && __originalMethod.Name.IndexOf("Memory", StringComparison.OrdinalIgnoreCase) >= 0)
                __result = DariusLanguage.IsEnglish ? "Axe wounds keep tearing open until spilled blood awakens the conqueror's strength." : "斧刃留下的伤口会不断撕裂，直到鲜血唤起征服者的力量。";
            else if (wantsDescription)
                __result = DariusFormalLocalization.HemorrhageDescription;
        }
    }

    private static void LocalizationConvertedDescriptionPostfix(object[] __args, ref string __result)
    {
        if (__args == null || __args.Length < 2) return;
        try
        {
            object settings = FindDescriptionSettings(__args);
            if (settings == null) return;
            SkillTrigger skill = ResolveDariusTooltipSkill(settings, __result);
            string key = skill != null ? skill.GetType().Name : DetectDariusMemoryKey(__result);
            if (!DariusFormalLocalization.IsDariusMemoryKey(key)) return;

            int liveLevel = skill != null ? DariusMemoryScaling.GetLevel(skill) : 1;
            int? currentFromSettings = ReadNullableIntMember(settings, "currentLevel");
            int? previousFromSettings = ReadNullableIntMember(settings, "previousLevel");
            bool showScaling = ReadBoolMember(settings, "showLevelScaling", false);

            int currentLevel = DariusMemoryScaling.NormalizeLevel(currentFromSettings ?? liveLevel);
            int? previousLevel = previousFromSettings;
            if (showScaling)
            {
                // Some UI paths supply both levels (old -> target); others only set the current
                // level and request showLevelScaling. In the latter case explicitly render the
                // next enhancement so Darius gets the same useful preview in every Memory UI.
                if (!previousLevel.HasValue)
                {
                    previousLevel = liveLevel;
                    if (currentLevel == liveLevel) currentLevel = liveLevel + 1;
                }
            }
            else
            {
                previousLevel = null;
            }

            // Do not cache the final rendered tooltip.  Native descriptions evaluate their
            // expressions against DescriptionSettings every time they are converted; keeping a
            // finished custom string here made AP/AD changes appear frozen.  Skill lookup remains
            // cached, but the numerical text is rebuilt from the live context entity.
            Hero contextHero = ResolveTooltipHero(settings, skill);
            string rebuilt;
            if (DariusFormalLocalization.TryLevelAwareDescription(key, skill, currentLevel, previousLevel, showScaling, contextHero, out rebuilt))
            {
                __result = rebuilt;
                // Ordinary Ctrl edit/drag UI can rebuild multiple tooltips on open/close. Never log
                // those normal renders; only keep diagnostics for the actual level-up preview path.
                if (showScaling)
                {
                    DariusLog.DebugInfoThrottled("TOOLTIP-LEVEL", key + ":scaling",
                        "Rebuilt upgrade preview " + key + " live=" + liveLevel + " current=" + currentLevel +
                        " previous=" + (previousLevel.HasValue ? previousLevel.Value.ToString() : "<none>"), 8.0);
                }
            }
        }
        catch (Exception e)
        {
            DariusLog.DebugInfo("TOOLTIP-LEVEL", "Level-aware description conversion fallback: " + e.GetType().Name + " " + e.Message);
        }
    }
}