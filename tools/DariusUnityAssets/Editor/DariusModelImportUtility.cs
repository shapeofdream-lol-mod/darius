#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;

internal static class DariusModelImportUtility
{
    public static void ConfigureModelImporter(string assetPath, DariusNativeSkinProfile profile)
    {
        if (profile == null) throw new ArgumentNullException(nameof(profile));
        ModelImporter importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
        if (importer == null)
            throw new InvalidOperationException("Expected ModelImporter: " + assetPath);

        importer.globalScale = 1f;
        importer.importAnimation = true;
        importer.importBlendShapes = true;
        importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        importer.animationType = ModelImporterAnimationType.Generic;
        importer.avatarSetup = ModelImporterAvatarSetup.NoAvatar;
        importer.optimizeGameObjects = false;
        importer.importCameras = false;
        importer.importLights = false;
        importer.isReadable = false;
        importer.clipAnimations = NormalizeClips(importer.defaultClipAnimations, profile);
        importer.SaveAndReimport();
    }

    private static ModelImporterClipAnimation[] NormalizeClips(
        ModelImporterClipAnimation[] clips,
        DariusNativeSkinProfile profile)
    {
        if (clips == null) return Array.Empty<ModelImporterClipAnimation>();

        HashSet<string> names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < clips.Length; i++)
        {
            ModelImporterClipAnimation clip = clips[i];
            string normalized = NormalizeImportedClipName(clip.name);
            if (string.IsNullOrEmpty(normalized))
                throw new InvalidOperationException("Empty imported animation clip skin=" + profile.Variant);
            if (!names.Add(normalized))
                throw new InvalidOperationException("Duplicate imported animation clip skin=" + profile.Variant + " clip=" + normalized);

            clip.name = normalized;
            clip.loopTime = IsLoopClip(normalized, profile);
            clip.loopPose = clip.loopTime;
            clip.keepOriginalOrientation = true;
            clip.keepOriginalPositionY = true;
            clip.keepOriginalPositionXZ = true;
            clips[i] = clip;
        }
        return clips;
    }

    private static bool IsLoopClip(string name, DariusNativeSkinProfile profile)
    {
        if (string.IsNullOrEmpty(name) || profile == null) return false;
        return string.Equals(name, profile.Idle, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(name, profile.IdleVariant, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(name, profile.Run, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(name, profile.WIdle, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(name, profile.WRun, StringComparison.OrdinalIgnoreCase);
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
