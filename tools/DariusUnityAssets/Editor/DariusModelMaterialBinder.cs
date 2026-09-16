#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

// Build-time only. Restores external texture references after FBX import.
// Runtime should receive complete Material -> Texture dependencies through AssetBundle.
public static class DariusModelMaterialBinder
{
    public static void Bind(string assetPath)
    {
        string folder = Path.GetDirectoryName(assetPath);
        if (string.IsNullOrEmpty(folder)) return;

        UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
        int bound = 0;
        foreach (UnityEngine.Object asset in assets)
        {
            Material material = asset as Material;
            if (material == null) continue;

            Texture2D texture = FindTexture(folder, material.name);
            if (texture == null) continue;

            material.mainTexture = texture;
            EditorUtility.SetDirty(material);
            bound++;
            Debug.Log("[DariusNativeAssets] material-texture-bind material=" + material.name + " texture=" + texture.name);
        }

        AssetDatabase.SaveAssets();
        Debug.Log("[DariusNativeAssets] material-texture-bind complete asset=" + assetPath + " bound=" + bound);
    }

    private static Texture2D FindTexture(string folder, string materialName)
    {
        string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { folder });
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture == null) continue;

            string a = Normalize(materialName);
            string b = Normalize(texture.name);
            if (b.Contains(a) || a.Contains(b)) return texture;
        }
        return null;
    }

    private static string Normalize(string value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        return value.Replace("_mat", string.Empty)
            .Replace("_material", string.Empty)
            .Replace("_", string.Empty)
            .ToLowerInvariant();
    }
}
#endif
