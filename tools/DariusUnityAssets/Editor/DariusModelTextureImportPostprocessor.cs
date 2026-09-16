#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

// Keeps texture binding inside Unity import pipeline.
// AssetBundle builder only consumes completed prefab dependencies.
public sealed class DariusModelTextureImportPostprocessor : AssetPostprocessor
{
    private void OnPostprocessModel(GameObject root)
    {
        if (string.IsNullOrEmpty(assetPath) || !assetPath.StartsWith("Assets/DariusSource/", StringComparison.OrdinalIgnoreCase))
            return;

        string directory = Path.GetDirectoryName(assetPath) ?? string.Empty;
        string[] texturePaths = AssetDatabase.FindAssets("t:Texture2D", new[] { directory });
        Texture2D fallback = null;

        for (int i = 0; i < texturePaths.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(texturePaths[i]);
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture == null) continue;
            if (fallback == null) fallback = texture;

            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                Material[] materials = renderer.sharedMaterials;
                for (int m = 0; m < materials.Length; m++)
                {
                    Material material = materials[m];
                    if (material == null || material.mainTexture != null) continue;
                    if (Matches(material.name, texture.name))
                    {
                        material.mainTexture = texture;
                        EditorUtility.SetDirty(material);
                    }
                }
            }
        }

        if (fallback != null)
        {
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                Material[] materials = renderer.sharedMaterials;
                for (int m = 0; m < materials.Length; m++)
                {
                    if (materials[m] != null && materials[m].mainTexture == null)
                    {
                        materials[m].mainTexture = fallback;
                        EditorUtility.SetDirty(materials[m]);
                    }
                }
            }
        }
    }

    private static bool Matches(string materialName, string textureName)
    {
        if (string.IsNullOrEmpty(materialName) || string.IsNullOrEmpty(textureName)) return false;
        string a = materialName.Replace("_Mat", "").ToLowerInvariant();
        string b = textureName.ToLowerInvariant();
        return b.Contains(a) || a.Contains(b);
    }
}
#endif
