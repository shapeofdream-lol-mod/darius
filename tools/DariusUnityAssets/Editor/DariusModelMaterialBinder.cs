#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class DariusModelMaterialBinder
{
    public static void BindImportedTextures(string assetPath)
    {
        string folder = Path.GetDirectoryName(assetPath);
        string modelStem = Path.GetFileNameWithoutExtension(assetPath);
        if (string.IsNullOrEmpty(folder) || string.IsNullOrEmpty(modelStem))
            throw new InvalidOperationException("Invalid imported model path: " + assetPath);

        int materialCount = 0;
        int boundCount = 0;
        foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(assetPath))
        {
            Material material = asset as Material;
            if (material == null) continue;
            materialCount++;
            Texture2D texture = FindTexture(folder, modelStem, material.name);
            if (texture == null)
                throw new InvalidOperationException("No color texture match model=" + modelStem + " material=" + material.name);
            material.mainTexture = texture;
            EditorUtility.SetDirty(material);
            boundCount++;
            Debug.Log("[DariusNativeAssets] bound model=" + modelStem +
                " material=" + material.name + " texture=" + texture.name);
        }

        if (materialCount == 0) throw new InvalidOperationException("Imported model has no materials: " + assetPath);
        if (boundCount != materialCount)
            throw new InvalidOperationException("Material binding incomplete model=" + modelStem +
                " materials=" + materialCount + " bound=" + boundCount);
        AssetDatabase.SaveAssets();
    }

    private static Texture2D FindTexture(string folder, string modelStem, string materialName)
    {
        string materialKey = NormalizeMaterialKey(materialName);
        if (string.IsNullOrEmpty(materialKey)) return null;

        string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { folder });
        Texture2D best = null;
        string bestPath = null;
        int bestScore = 0;
        int bestCount = 0;
        string requiredPrefix = modelStem + "__tex_";

        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            string stem = Path.GetFileNameWithoutExtension(path);
            if (string.IsNullOrEmpty(stem) || !stem.StartsWith(requiredPrefix, StringComparison.OrdinalIgnoreCase)) continue;
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture == null) continue;
            int score = ScoreTexture(materialKey, texture.name);
            if (score <= 0 || score < bestScore) continue;
            if (score > bestScore)
            {
                best = texture;
                bestPath = path;
                bestScore = score;
                bestCount = 1;
                continue;
            }
            if (!string.Equals(path, bestPath, StringComparison.OrdinalIgnoreCase)) bestCount++;
        }

        if (bestCount > 1)
            throw new InvalidOperationException("Ambiguous color texture model=" + modelStem +
                " material=" + materialName + " score=" + bestScore + " candidates=" + bestCount);
        return best;
    }

    private static int ScoreTexture(string materialKey, string textureName)
    {
        string sourceName = StripExportPrefix(textureName);
        string rawKey = NormalizeToken(sourceName);
        if (rawKey == materialKey) return 100;
        string colorKey = NormalizeToken(StripColorSuffix(sourceName));
        if (colorKey == materialKey) return 90;
        if (IsNonColorTexture(sourceName)) return -1;
        if (colorKey.EndsWith(materialKey, StringComparison.Ordinal)) return 70;
        if (colorKey.Contains(materialKey)) return 60;
        if (materialKey.Contains(colorKey) && colorKey.Length >= 5) return 40;
        return 0;
    }

    private static string StripExportPrefix(string value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        int marker = value.IndexOf("__tex_", StringComparison.OrdinalIgnoreCase);
        if (marker < 0) return value;
        int numberStart = marker + "__tex_".Length;
        int separator = value.IndexOf('_', numberStart);
        return separator >= 0 && separator + 1 < value.Length ? value.Substring(separator + 1) : value;
    }

    private static string StripColorSuffix(string value)
    {
        string[] suffixes = { "_basecolor", "_base_color", "_diffuse", "_albedo", "_color" };
        for (int i = 0; i < suffixes.Length; i++)
            if (value.EndsWith(suffixes[i], StringComparison.OrdinalIgnoreCase))
                return value.Substring(0, value.Length - suffixes[i].Length);
        return value;
    }

    private static bool IsNonColorTexture(string value)
    {
        if (string.IsNullOrEmpty(value)) return false;
        string lower = value.ToLowerInvariant();
        return lower.Contains("normal") || lower.Contains("_nrm") || lower.Contains("emiss") ||
               lower.Contains("specular") || lower.Contains("roughness") || lower.Contains("metallic") ||
               lower.Contains("_mask");
    }

    private static string NormalizeMaterialKey(string value)
    {
        string key = NormalizeToken(value);
        if (key.EndsWith("material", StringComparison.Ordinal)) key = key.Substring(0, key.Length - 8);
        else if (key.EndsWith("mat", StringComparison.Ordinal)) key = key.Substring(0, key.Length - 3);
        return key;
    }

    private static string NormalizeToken(string value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        char[] buffer = new char[value.Length];
        int count = 0;
        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];
            if (!char.IsLetterOrDigit(c)) continue;
            buffer[count++] = char.ToLowerInvariant(c);
        }
        return new string(buffer, 0, count);
    }
}
#endif