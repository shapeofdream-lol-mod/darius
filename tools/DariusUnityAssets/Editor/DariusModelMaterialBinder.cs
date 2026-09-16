#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class DariusModelMaterialBinder
{
    public static void BindImportedTextures(string assetPath)
    {
        Bind(assetPath);
    }

    public static void Bind(string assetPath)
    {
        string folder = Path.GetDirectoryName(assetPath);
        if (string.IsNullOrEmpty(folder)) return;

        foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(assetPath))
        {
            Material material = asset as Material;
            if (material == null) continue;

            Texture2D texture = FindTexture(folder, material.name);
            if (texture == null) continue;

            material.mainTexture = texture;
            EditorUtility.SetDirty(material);
        }

        AssetDatabase.SaveAssets();
    }

    private static Texture2D FindTexture(string folder, string materialName)
    {
        string exact = Normalize(materialName);
        string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { folder });
        Texture2D fallback = null;

        foreach (string guid in guids)
        {
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(guid));
            if (texture == null) continue;

            string candidate = Normalize(texture.name);
            if (candidate == exact) return texture;

            if (candidate == exact + "basecolor" || candidate == exact + "diffuse")
                fallback = texture;
            else if (candidate.Contains(exact) || exact.Contains(candidate))
                fallback ??= texture;
        }

        return fallback;
    }

    private static string Normalize(string value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        return value.Replace("_mat", string.Empty)
            .Replace("_material", string.Empty)
            .Replace("_basecolor", string.Empty)
            .Replace("_diffuse", string.Empty)
            .Replace("_", string.Empty)
            .ToLowerInvariant();
    }
}
#endif
