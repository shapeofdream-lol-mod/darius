#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;

internal static class DariusModelImportUtility
{
    public static void ConfigureModelImporter(string assetPath, string variant)
    {
        ModelImporter importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
        if (importer == null)
            throw new InvalidOperationException("Expected ModelImporter: " + assetPath);

        importer.globalScale = 1f;
        importer.importAnimation = true;
        importer.importBlendShapes = true;
        importer.importMaterials = true;
        importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        importer.animationType = ModelImporterAnimationType.Generic;
        importer.avatarSetup = ModelImporterAvatarSetup.NoAvatar;
        importer.optimizeGameObjects = false;
        importer.importCameras = false;
        importer.importLights = false;
        importer.isReadable = false;
        importer.clipAnimations = NormalizeClips(importer.defaultClipAnimations, variant);
        importer.SaveAndReimport();
    }

    private static ModelImporterClipAnimation[] NormalizeClips(
        ModelImporterClipAnimation[] clips,
        string variant)
    {
        if (clips == null) return Array.Empty<ModelImporterClipAnimation>();

        HashSet<string> names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < clips.Length; i++)
        {
            ModelImporterClipAnimation clip = clips[i];
            string normalized = NormalizeImportedClipName(clip.name);
            if (string.IsNullOrEmpty(normalized))
                throw new InvalidOperationException("Empty imported animation clip skin=" + variant);
            if (!names.Add(normalized))
                throw new InvalidOperationException("Duplicate imported animation clip skin=" + variant + " clip=" + normalized);

            clip.name = normalized;
            clip.loopTime = IsLoopClip(normalized);
            clip.loopPose = clip.loopTime;
            clip.keepOriginalOrientation = true;
            clip.keepOriginalPositionY = true;
            clip.keepOriginalPositionXZ = true;
            clips[i] = clip;
        }
        return clips;
    }

    private static bool IsLoopClip(string name)
    {
        if (string.IsNullOrEmpty(name)) return false;
        return name.IndexOf("idle", StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("run", StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("walk", StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("dance", StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("channel", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static string NormalizeImportedClipName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return string.Empty;
        string result = name.Trim();
        int separator = Math.Max(result.LastIndexOf('|'), result.LastIndexOf(':'));
        if (separator >= 0 && separator + 1 < result.Length)
            result = result.Substring(separator + 1).Trim();

        int dot = result.LastIndexOf('.');
        if (dot >= 0 && dot + 1 < result.Length)
        {
            string suffix = result.Substring(dot + 1);
            int numeric;
            if (suffix.Length == 3 && int.TryParse(suffix, out numeric))
                result = result.Substring(0, dot);
        }
        return result;
    }
}
#endif
