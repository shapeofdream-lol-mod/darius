using System;

internal static class DariusNativeAssetContract
{
    public const string GeneratedAssetRoot = "Assets/DariusGenerated";
    public const string LowerBodyAnimatorLayerName = "DariusLowerBody";

    private const string HiddenObjectPrefix = "DariusHidden_";
    private const string WolfHiddenObjectPrefix = "DariusHidden_Wolf_";
    private const string StaticHiddenObjectPrefix = "DariusHidden_Static_";

    public static readonly string[] HealthAnchorNames =
    {
        "C_BuffBone_Glb_Chest_Loc", "Chest", "Spine"
    };

    public static readonly string[] WeaponAnchorNames =
    {
        "BuffBone_Glb_Weapon_1", "Weapon", "R_Hand"
    };

    public static string NormalizeVariant(string variant)
    {
        return string.IsNullOrEmpty(variant)
            ? string.Empty
            : variant.Replace(" ", string.Empty).ToLowerInvariant();
    }

    public static string AnimatorStateName(string clipName)
    {
        return string.IsNullOrEmpty(clipName)
            ? string.Empty
            : clipName.Replace('.', '_').Replace('/', '_').Replace('\\', '_');
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

    public static bool IsLowerBodyLocomotionName(string name)
    {
        if (string.IsNullOrEmpty(name)) return false;
        string lower = name.ToLowerInvariant();
        if (lower.Contains("weapon") || lower.Contains("buffbone") || lower.Contains("ground_loc") ||
            lower.Contains("glb_foot_loc") || lower.Contains("snap_") || lower.Contains("doll")) return false;
        return lower.Contains("hip") || lower.Contains("knee") || lower.Contains("leg") ||
               lower.Contains("thigh") || lower.Contains("calf") || lower.Contains("foot") ||
               lower.Contains("toe");
    }

    public static string WolfHiddenObjectName(string sourceName)
    {
        return WolfHiddenObjectPrefix + (sourceName ?? string.Empty);
    }

    public static string StaticHiddenObjectName(string sourceName)
    {
        return StaticHiddenObjectPrefix + (sourceName ?? string.Empty);
    }

    public static string PrefabAssetPath(string variant)
    {
        return GeneratedAssetRoot + "/darius_" + NormalizeVariant(variant) + ".prefab";
    }

    public static string LowerBodyMaskAssetPath(string variant)
    {
        return GeneratedAssetRoot + "/darius_" + NormalizeVariant(variant) + "_lowerbody.mask";
    }

    public static string OverlayAssetFileName(string variant, int rendererIndex, uint materialHash)
    {
        return "overlay_" + NormalizeVariant(variant) + "_" + rendererIndex + "_" +
               materialHash.ToString("x8") + ".asset";
    }

    public static string OverlayAssetPath(string variant, int rendererIndex, uint materialHash)
    {
        return GeneratedAssetRoot + "/" + OverlayAssetFileName(variant, rendererIndex, materialHash);
    }
}
