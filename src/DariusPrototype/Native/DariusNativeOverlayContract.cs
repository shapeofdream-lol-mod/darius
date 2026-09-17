using System;

internal static class DariusNativeOverlayContract
{
    private const string HiddenObjectPrefix = "DariusHidden_";
    private const string WolfHiddenObjectPrefix = "DariusHidden_Wolf_";
    private const string StaticHiddenObjectPrefix = "DariusHidden_Static_";

    public static string NormalizeVariant(string variant)
    {
        return string.IsNullOrEmpty(variant)
            ? string.Empty
            : variant.Replace(" ", string.Empty).ToLowerInvariant();
    }

    public static string NormalizeMaterialName(string name)
    {
        if (string.IsNullOrEmpty(name)) return string.Empty;
        const string instance = " (Instance)";
        if (name.EndsWith(instance, StringComparison.Ordinal))
            name = name.Substring(0, name.Length - instance.Length);
        const string runtime = "_Runtime";
        if (name.EndsWith(runtime, StringComparison.OrdinalIgnoreCase))
            name = name.Substring(0, name.Length - runtime.Length);
        return name;
    }

    public static uint MaterialHash(string materialName)
    {
        string text = NormalizeMaterialName(materialName);
        uint hash = 2166136261u;
        for (int i = 0; i < text.Length; i++)
        {
            hash ^= (byte)char.ToLowerInvariant(text[i]);
            hash *= 16777619u;
        }
        return hash;
    }

    public static bool IsHiddenObjectName(string name)
    {
        return !string.IsNullOrEmpty(name) &&
               name.StartsWith(HiddenObjectPrefix, StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsWolfHiddenObjectName(string name)
    {
        return !string.IsNullOrEmpty(name) &&
               name.StartsWith(WolfHiddenObjectPrefix, StringComparison.OrdinalIgnoreCase);
    }

    public static string WolfHiddenObjectName(string sourceName)
    {
        return WolfHiddenObjectPrefix + (sourceName ?? string.Empty);
    }

    public static string StaticHiddenObjectName(string sourceName)
    {
        return StaticHiddenObjectPrefix + (sourceName ?? string.Empty);
    }

    public static string AssetFileName(string variant, int rendererIndex, uint materialHash)
    {
        return "overlay_" + NormalizeVariant(variant) + "_" + rendererIndex + "_" +
               materialHash.ToString("x8") + ".asset";
    }

    public static string AssetSuffix(string variant, int rendererIndex, uint materialHash)
    {
        return "/" + AssetFileName(variant, rendererIndex, materialHash);
    }
}
