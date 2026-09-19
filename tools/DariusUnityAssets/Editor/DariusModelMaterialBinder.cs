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
        public string Renderer;
        public int Slot;
        public string Name;
        public string TextureFile;
        public string AlphaMode;
        public float AlphaCutoff;
        public bool AuthoredVisible;
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
        Dictionary<string, SkinnedMeshRenderer> renderers = LoadRenderers(model, modelStem);
        Dictionary<string, List<MaterialBinding>> byRenderer = GroupBindings(bindings, modelStem);

        if (renderers.Count != byRenderer.Count)
            throw new InvalidOperationException("Blender/Unity skinned renderer count mismatch model=" + modelStem +
                " blender=" + byRenderer.Count + " unity=" + renderers.Count);

        int assetIndex = 0;
        foreach (KeyValuePair<string, List<MaterialBinding>> pair in byRenderer)
        {
            SkinnedMeshRenderer renderer;
            if (!renderers.TryGetValue(pair.Key, out renderer) || renderer == null)
                throw new InvalidOperationException("Unity skinned renderer missing model=" + modelStem +
                    " renderer=" + pair.Key);

            Material[] materials = renderer.sharedMaterials;
            List<MaterialBinding> rendererBindings = pair.Value;
            if (materials.Length != rendererBindings.Count)
                throw new InvalidOperationException("Blender/Unity material slot count mismatch model=" + modelStem +
                    " renderer=" + pair.Key + " blender=" + rendererBindings.Count + " unity=" + materials.Length);

            for (int slot = 0; slot < rendererBindings.Count; slot++)
            {
                MaterialBinding binding = rendererBindings[slot];
                if (binding.Slot != slot)
                    throw new InvalidOperationException("Native material slots are not contiguous model=" + modelStem +
                        " renderer=" + pair.Key + " expected=" + slot + " actual=" + binding.Slot);

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

                material.SetOverrideTag("DariusAuthoredVisible", binding.AuthoredVisible ? "1" : "0");
                if (string.Equals(binding.AlphaMode, "BLEND", StringComparison.OrdinalIgnoreCase) ||
                    !binding.AuthoredVisible)
                {
                    material.SetOverrideTag("RenderType", "Transparent");
                    if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
                    if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0f);
                    if (material.HasProperty("_SrcBlend")) material.SetFloat("_SrcBlend", 5f);
                    if (material.HasProperty("_DstBlend")) material.SetFloat("_DstBlend", 10f);
                    if (material.HasProperty("_AlphaClip")) material.SetFloat("_AlphaClip", 0f);
                    material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    material.DisableKeyword("_ALPHATEST_ON");
                    material.renderQueue = 3000;
                }
                else if (string.Equals(binding.AlphaMode, "MASK", StringComparison.OrdinalIgnoreCase))
                {
                    material.SetOverrideTag("RenderType", "TransparentCutout");
                    if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 0f);
                    if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 1f);
                    if (material.HasProperty("_AlphaClip")) material.SetFloat("_AlphaClip", 1f);
                    if (material.HasProperty("_Cutoff")) material.SetFloat("_Cutoff", binding.AlphaCutoff);
                    material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    material.EnableKeyword("_ALPHATEST_ON");
                    material.renderQueue = 2450;
                }

                string materialPath = generatedRoot + "/" + modelStem + "_mat_" +
                    assetIndex.ToString("D2") + "_" + SanitizeAssetName(binding.Name) + ".mat";
                AssetDatabase.DeleteAsset(materialPath);
                AssetDatabase.CreateAsset(material, materialPath);
                materials[slot] = material;
                Debug.Log("[DariusNativeAssets] assigned model=" + modelStem +
                    " renderer=" + pair.Key + " slot=" + slot + " material=" + binding.Name +
                    " texture=" + binding.TextureFile);
                assetIndex++;
            }
            renderer.sharedMaterials = materials;
        }
        AssetDatabase.SaveAssets();
    }

    private static Dictionary<string, SkinnedMeshRenderer> LoadRenderers(GameObject model, string modelStem)
    {
        Dictionary<string, SkinnedMeshRenderer> result =
            new Dictionary<string, SkinnedMeshRenderer>(StringComparer.OrdinalIgnoreCase);
        SkinnedMeshRenderer[] renderers = model.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            SkinnedMeshRenderer renderer = renderers[i];
            if (renderer == null || renderer.sharedMesh == null) continue;
            string name = renderer.gameObject != null ? renderer.gameObject.name : null;
            if (string.IsNullOrEmpty(name))
                throw new InvalidOperationException("Unity skinned renderer has no name model=" + modelStem);
            if (result.ContainsKey(name))
                throw new InvalidOperationException("Duplicate Unity skinned renderer name model=" + modelStem +
                    " renderer=" + name);
            result.Add(name, renderer);
        }
        if (result.Count == 0)
            throw new InvalidOperationException("Imported model has no skinned renderers: " + modelStem);
        return result;
    }

    private static Dictionary<string, List<MaterialBinding>> GroupBindings(
        List<MaterialBinding> bindings,
        string modelStem)
    {
        Dictionary<string, List<MaterialBinding>> result =
            new Dictionary<string, List<MaterialBinding>>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < bindings.Count; i++)
        {
            MaterialBinding binding = bindings[i];
            List<MaterialBinding> list;
            if (!result.TryGetValue(binding.Renderer, out list))
            {
                list = new List<MaterialBinding>();
                result.Add(binding.Renderer, list);
            }
            if (binding.Slot != list.Count)
                throw new InvalidDataException("Non-contiguous native material slot model=" + modelStem +
                    " renderer=" + binding.Renderer + " expected=" + list.Count + " actual=" + binding.Slot);
            list.Add(binding);
        }
        return result;
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
        string[] lines = File.ReadAllLines(manifestPath);
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i];
            if (string.IsNullOrWhiteSpace(line)) continue;
            string[] parts = line.Split('\t');
            int slot;
            float alphaCutoff;
            int authoredVisibleInt;
            if (parts.Length != 7 || string.IsNullOrWhiteSpace(parts[0]) ||
                !int.TryParse(parts[1], out slot) || slot < 0 ||
                string.IsNullOrWhiteSpace(parts[2]) || string.IsNullOrWhiteSpace(parts[3]) ||
                !float.TryParse(parts[5], System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out alphaCutoff) ||
                !int.TryParse(parts[6], out authoredVisibleInt) ||
                (authoredVisibleInt != 0 && authoredVisibleInt != 1))
                throw new InvalidDataException("Malformed native material manifest line=" + (i + 1) +
                    " file=" + manifestPath);

            string renderer = parts[0].Trim();
            string material = parts[2].Trim();
            string textureFile = parts[3].Trim();
            string alphaMode = parts[4].Trim();
            if (!string.Equals(alphaMode, "OPAQUE", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(alphaMode, "MASK", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(alphaMode, "BLEND", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Invalid alpha mode model=" + modelStem + " alphaMode=" + alphaMode);
            if (!string.Equals(textureFile, NoTexture, StringComparison.Ordinal) &&
                (!string.Equals(Path.GetFileName(textureFile), textureFile, StringComparison.Ordinal) ||
                 !textureFile.StartsWith(modelStem + "__tex_", StringComparison.OrdinalIgnoreCase)))
                throw new InvalidDataException("Invalid native texture mapping model=" + modelStem +
                    " texture=" + textureFile);

            result.Add(new MaterialBinding {
                Renderer = renderer,
                Slot = slot,
                Name = material,
                TextureFile = textureFile,
                AlphaMode = alphaMode,
                AlphaCutoff = alphaCutoff,
                AuthoredVisible = authoredVisibleInt != 0
            });
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