using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;

public static partial class DariusFormalRegistry
{
    public static bool IsDariusResourceKey(string key)
    {
        if (string.IsNullOrEmpty(key)) return false;
        if (ResourcesByGuid.ContainsKey(key)) return true;
        return key == "St_Darius_Decimate" || key == "St_Darius_CripplingStrike" ||
               key == "St_Darius_Apprehend" || key == "St_Darius_NoxianGuillotine" ||
               key == "St_Darius_Flash" || key == "St_Darius_Ghost" ||
               key == "St_D_Darius_Hemorrhage" || key == "Gem_Darius_Hemorrhage" || key == "Ai_Darius_Decimate" ||
               key == "Ai_Darius_CripplingStrike" || key == "Ai_Darius_Apprehend" ||
               key == "Ai_Darius_NoxianGuillotine" || key.StartsWith("Se_Star_Darius_", StringComparison.Ordinal);
    }

    public static bool IsDariusSkillKey(object value)
    {
        if (value == null) return false;
        string text = value as string;
        if (text != null) return IsDariusSkillName(text) || text == GuidQ || text == GuidW || text == GuidE || text == GuidR ||
                                 text == GuidFlash || text == GuidGhost || text == GuidIdentity;
        Type type = value as Type;
        if (type != null) return IsDariusSkillName(type.Name);
        UnityEngine.Object obj = value as UnityEngine.Object;
        return obj != null && (IsDariusSkillName(obj.name) || IsDariusSkillName(obj.GetType().Name));
    }

    public static bool IsDariusGemKey(object value)
    {
        if (value == null) return false;
        string text = value as string;
        if (text != null) return text == "Gem_Darius_Hemorrhage" || text == GuidGem;
        Type type = value as Type;
        if (type != null) return type == typeof(Gem_Darius_Hemorrhage);
        UnityEngine.Object obj = value as UnityEngine.Object;
        return obj != null && (obj.name == "Gem_Darius_Hemorrhage" || obj is Gem_Darius_Hemorrhage);
    }

    public static bool IsDariusRuntimeObject(UnityEngine.Object obj)
    {
        if (obj == null) return false;
        if (GuidByObject.ContainsKey(obj)) return true;
        return IsDariusSkillName(obj.name) || IsDariusSkillName(obj.GetType().Name) ||
               DariusConstellationLocalization.IsDariusStarKey(obj.name) ||
               DariusConstellationLocalization.IsDariusStarKey(obj.GetType().Name) ||
               obj.name == "St_D_Darius_Hemorrhage" || obj is St_D_Darius_Hemorrhage ||
               obj.name == "Gem_Darius_Hemorrhage" || obj is Gem_Darius_Hemorrhage ||
               obj is Ai_Darius_Decimate || obj is Ai_Darius_CripplingStrike ||
               obj is Ai_Darius_Apprehend || obj is Ai_Darius_NoxianGuillotine;
    }

    private static uint StableAssetId(string text)
    {
        unchecked
        {
            uint hash = 2166136261u;
            for (int i = 0; i < text.Length; i++)
            {
                hash ^= text[i];
                hash *= 16777619u;
            }
            return hash == 0u ? 1u : hash;
        }
    }
}