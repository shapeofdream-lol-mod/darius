#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class DariusModelMaterialBinder
{
    private const string NoTexture = "-";

    private sealed class MaterialBinding
    {
        public int Slot;
        public string Name;
        public string TextureFile;
    }

    public static void BindPrefabMaterials(GameObject model, string assetPath, string generatedRoot)
    {
        if (model == null) throw new ArgumentNullException(nameof(model));
        string folder = (Path.GetDirectoryName(assetPath) ?? string.Empty).Replace('\\', '/');
        string modelStem = Path.GetFileNameWithoutExtension(assetPath);
        if (string.IsNullOrEmpty(folder) || string.IsNullOrEmpty(modelStem))
            throw new InvalidOperationException("Invalid imported model path: " + assetPath);

        List<MaterialBinding> bindings = LoadBindings(folder, modelStem);
        Dictionary<string, Material> imported = LoadImportedMaterials(assetPath);
        SkinnedMeshRenderer[] renderers = model.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        int slotCount = 0;
        for (int r = 0; r < renderers.Length; r++)
            if (renderers[r] != null) slotCount += renderers[r].sharedMaterials.Length;
        if (slotCount != bindings.Count)
            throw new InvalidOperationException("Blender/Unity material slot count mismatch model=" + modelStem +
                " blender=" + bindings.Count + " unity=" + slotCount);

        int cursor = 0;
        for (int r = 0; r < renderers.Length; r++)
        {
            SkinnedMeshRenderer renderer = renderers[r];
            if (renderer == null) continue;
            Material[] materials = renderer.sharedMaterials;
            for (int m = 0; m < materials.Length; m++)
            {
                MaterialBinding binding = bindings[cursor];
                if (binding.Slot != cursor)
                    throw new InvalidOperationException("Native material slots are not contiguous model=" + modelStem +
                        " expected=" + cursor + " actual=" + binding.Slot);

                Material template;
                if (!imported.TryGetValue(binding.Name, out template) || template == null)
                    throw new InvalidOperationException("Imported material template missing model=" + modelStem +
                        " material=" + binding.Name);

                Material material = new Material(template) { name = binding.Name };
                if (!string.Equals(binding.TextureFile, NoTexture, StringComparison.Ordinal))
                {
                    string texturePath = folder + "/" + binding.TextureFile;
                    Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
                    if (texture == null)
                        throw new InvalidOperationException("Mapped color texture missing model=" + modelStem +
                            " material=" + binding.Name + " asset=" + texturePath);
                    material.mainTexture = texture;
                }

                string materialPath = generatedRoot + "/" + modelStem + "_mat_" +
                    cursor.ToString("D2") + "_" + SanitizeAssetName(binding.Name) + ".mat";
                AssetDatabase.DeleteAsset(materialPath);
                AssetDatabase.CreateAsset(material, materialPath);
                materials[m] = material;
                Debug.Log("[DariusNativeAssets] assigned model=" + modelStem +
                    " slot=" + cursor + " material=" + binding.Name +
                    " texture=" + binding.TextureFile);
                cursor++;
            }
            renderer.sharedMaterials = materials;
        }
        AssetDatabase.SaveAssets();
    }

    private static Dictionary<string, Material> LoadImportedMaterials(string assetPath)
    {
        Dictionary<string, Material> result = new Dictionary<string, Material>(StringComparer.OrdinalIgnoreCase);
        foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(assetPath))
        {
            Material material = asset as Material;
            if (material == null || string.IsNullOrEmpty(material.name) || result.ContainsKey(material.name)) continue;
            result.Add(material.name, material);
        }
        return result;
    }

    private static List<MaterialBinding> LoadBindings(string folder, string modelStem)
    {
        string manifestAsset = folder + "/" + modelStem + "__materials.tsv";
        string manifestPath = ToProjectAbsolute(manifestAsset);
        if (!File.Exists(manifestPath))
            throw new FileNotFoundException("Native material manifest missing", manifestPath);

        List<MaterialBinding> result = new List<MaterialBinding>();
        HashSet<string> names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string[] lines = File.ReadAllLines(manifestPath);
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i];
            if (string.IsNullOrWhiteSpace(line)) continue;
            string[] parts = line.Split('\t');
            int slot;
            string material;
            string textureFile;
            if (parts.Length == 2)
            {
                slot = result.Count;
                material = parts[0].Trim();
                textureFile = parts[1].Trim();
            }
            else if (parts.Length == 3 && int.TryParse(parts[0], out slot))
            {
                material = parts[1].Trim();
                textureFile = parts[2].Trim();
            }
            else
            {
                throw new InvalidDataException("Malformed native material manifest line=" + (i + 1) + " file=" + manifestPath);
            }

            if (slot != result.Count || string.IsNullOrWhiteSpace(material) || string.IsNullOrWhiteSpace(textureFile))
                throw new InvalidDataException("Invalid native material slot line=" + (i + 1) + " file=" + manifestPath);
            if (!string.Equals(textureFile, NoTexture, StringComparison.Ordinal) &&
                (!string.Equals(Path.GetFileName(textureFile), textureFile, StringComparison.Ordinal) ||
                 !textureFile.StartsWith(modelStem + "__tex_", StringComparison.OrdinalIgnoreCase)))
                throw new InvalidDataException("Invalid native texture mapping model=" + modelStem + " texture=" + textureFile);
            if (!names.Add(material))
                throw new InvalidDataException("Duplicate native material mapping model=" + modelStem + " material=" + material);

            result.Add(new MaterialBinding { Slot = slot, Name = material, TextureFile = textureFile });
        }
        if (result.Count == 0) throw new InvalidDataException("Native material manifest is empty: " + manifestPath);
        return result;
    }

    private static string SanitizeAssetName(string value)
    {
        if (string.IsNullOrEmpty(value)) return "material";
        foreach (char c in Path.GetInvalidFileNameChars()) value = value.Replace(c, '_');
        return value.Replace('/', '_').Replace('\\', '_');
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