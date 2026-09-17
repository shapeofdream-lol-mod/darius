#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class DariusModelMaterialBinder
{
    private const string NoTexture = "-";

    public static void BindImportedTextures(string assetPath)
    {
        string folder = (Path.GetDirectoryName(assetPath) ?? string.Empty).Replace('\\', '/');
        string modelStem = Path.GetFileNameWithoutExtension(assetPath);
        if (string.IsNullOrEmpty(folder) || string.IsNullOrEmpty(modelStem))
            throw new InvalidOperationException("Invalid imported model path: " + assetPath);

        Dictionary<string, string> bindings = LoadBindings(folder, modelStem);
        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
        if (model == null) throw new InvalidOperationException("Imported model asset missing: " + assetPath);

        HashSet<Material> referenced = new HashSet<Material>();
        Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true);
        for (int r = 0; r < renderers.Length; r++)
        {
            Material[] materials = renderers[r] != null ? renderers[r].sharedMaterials : null;
            if (materials == null) continue;
            for (int m = 0; m < materials.Length; m++)
                if (materials[m] != null) referenced.Add(materials[m]);
        }
        if (referenced.Count == 0) throw new InvalidOperationException("Imported model has no referenced materials: " + assetPath);

        HashSet<string> consumed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (Material material in referenced)
        {
            string textureFile;
            if (!bindings.TryGetValue(material.name, out textureFile))
                throw new InvalidOperationException("No Blender material binding model=" + modelStem + " material=" + material.name);

            consumed.Add(material.name);
            if (string.Equals(textureFile, NoTexture, StringComparison.Ordinal))
            {
                Debug.Log("[DariusNativeAssets] preserved untextured material model=" + modelStem +
                    " material=" + material.name);
                continue;
            }

            string texturePath = folder + "/" + textureFile;
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            if (texture == null)
                throw new InvalidOperationException("Mapped color texture missing model=" + modelStem +
                    " material=" + material.name + " asset=" + texturePath);
            material.mainTexture = texture;
            EditorUtility.SetDirty(material);
            Debug.Log("[DariusNativeAssets] bound model=" + modelStem +
                " material=" + material.name + " texture=" + texture.name);
        }

        if (consumed.Count != bindings.Count)
            throw new InvalidOperationException("Blender/Unity referenced material count mismatch model=" + modelStem +
                " blender=" + bindings.Count + " unity=" + consumed.Count);
        AssetDatabase.SaveAssets();
    }

    private static Dictionary<string, string> LoadBindings(string folder, string modelStem)
    {
        string manifestAsset = folder + "/" + modelStem + "__materials.tsv";
        string manifestPath = ToProjectAbsolute(manifestAsset);
        if (!File.Exists(manifestPath))
            throw new FileNotFoundException("Native material manifest missing", manifestPath);

        Dictionary<string, string> result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string[] lines = File.ReadAllLines(manifestPath);
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i];
            if (string.IsNullOrWhiteSpace(line)) continue;
            string[] parts = line.Split(new[] { '\t' }, 2);
            if (parts.Length != 2 || string.IsNullOrWhiteSpace(parts[0]) || string.IsNullOrWhiteSpace(parts[1]))
                throw new InvalidDataException("Malformed native material manifest line=" + (i + 1) + " file=" + manifestPath);
            string textureFile = parts[1].Trim();
            if (!string.Equals(textureFile, NoTexture, StringComparison.Ordinal) &&
                (!string.Equals(Path.GetFileName(textureFile), textureFile, StringComparison.Ordinal) ||
                 !textureFile.StartsWith(modelStem + "__tex_", StringComparison.OrdinalIgnoreCase)))
                throw new InvalidDataException("Invalid native texture mapping model=" + modelStem + " texture=" + textureFile);
            string material = parts[0].Trim();
            if (result.ContainsKey(material))
                throw new InvalidDataException("Duplicate native material mapping model=" + modelStem + " material=" + material);
            result.Add(material, textureFile);
        }
        if (result.Count == 0) throw new InvalidDataException("Native material manifest is empty: " + manifestPath);
        return result;
    }

    private static string ToProjectAbsolute(string assetPath)
    {
        const string prefix = "Assets/";
        if (!assetPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Expected Unity Assets path: " + assetPath);
        string relative = assetPath.Substring(prefix.Length).Replace('/', Path.DirectorySeparatorChar);
        return Path.Combine(Application.dataPath, relative);
    }
}
#endif