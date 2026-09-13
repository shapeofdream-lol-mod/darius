using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

// DariusMedia historically wrote `key -> null` into its dictionaries after a missing/failed asset,
// but its cache-hit condition required the cached Unity object to be non-null. Every later request
// therefore repeated File.Exists/IO/logging for the same missing key. Keep misses sticky for the
// lifetime of the dynamic assembly; a mod reload clears these sets and permits a newly installed
// local asset to be discovered.
[HarmonyPatch(typeof(DariusMedia), nameof(DariusMedia.Texture))]
internal static class DariusMissingTextureCachePatch
{
    private static readonly HashSet<string> Missing = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);

    [HarmonyPrefix]
    private static bool Prefix(string key, ref Texture2D __result)
    {
        if (string.IsNullOrEmpty(key) || !Missing.Contains(key)) return true;
        __result = null;
        return false;
    }

    [HarmonyPostfix]
    private static void Postfix(string key, Texture2D __result)
    {
        if (string.IsNullOrEmpty(key)) return;
        if (__result == null) Missing.Add(key);
        else Missing.Remove(key);
    }
}

[HarmonyPatch(typeof(DariusMedia), nameof(DariusMedia.Clip))]
internal static class DariusMissingClipCachePatch
{
    private static readonly HashSet<string> Missing = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);

    [HarmonyPrefix]
    private static bool Prefix(string key, ref AudioClip __result)
    {
        if (string.IsNullOrEmpty(key) || !Missing.Contains(key)) return true;
        __result = null;
        return false;
    }

    [HarmonyPostfix]
    private static void Postfix(string key, AudioClip __result)
    {
        if (string.IsNullOrEmpty(key)) return;
        if (__result == null) Missing.Add(key);
        else Missing.Remove(key);
    }
}