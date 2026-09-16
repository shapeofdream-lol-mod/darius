#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

internal static class DariusModelImportUtility
{
    public static void ConfigureModelImporter(string assetPath, string variant)
    {
        AssetImporter importer = AssetImporter.GetAtPath(assetPath);
        ModelImporter modelImporter = importer as ModelImporter;
        if (modelImporter == null)
            throw new InvalidOperationException("Expected ModelImporter: " + assetPath);

        modelImporter.importAnimation = true;
        modelImporter.importBlendShapes = true;
        modelImporter.importMaterials = true;
        modelImporter.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        modelImporter.animationType = ModelImporterAnimationType.Human;

        modelImporter.clipAnimations = NormalizeClips(
            modelImporter.defaultClipAnimations,
            variant);

        modelImporter.SaveAndReimport();
    }

    private static ModelImporterClipAnimation[] NormalizeClips(
        ModelImporterClipAnimation[] clips,
        string variant)
    {
        if (clips == null)
            return clips;

        for (int i = 0; i < clips.Length; i++)
        {
            ModelImporterClipAnimation clip = clips[i];
            clip.loopTime = IsLoopClip(clip.name);
            clip.name = NormalizeClipName(clip.name);
            clips[i] = clip;
        }

        return clips;
    }

    private static bool IsLoopClip(string name)
    {
        if (string.IsNullOrEmpty(name))
            return false;

        return name.IndexOf("idle", StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("run", StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("walk", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static string NormalizeClipName(string name)
    {
        if (string.IsNullOrEmpty(name))
            return name;

        return name.Replace(" ", "_").Replace(".", "_");
    }

    public static string GetDirectory(string path)
    {
        return Path.GetDirectoryName(path)?.Replace("\\", "/");
    }

    public static string GetFileName(string path)
    {
        return Path.GetFileNameWithoutExtension(path);
    }

    public static bool Exists(string path)
    {
        return !string.IsNullOrEmpty(path) && File.Exists(path);
    }
}
#endif
