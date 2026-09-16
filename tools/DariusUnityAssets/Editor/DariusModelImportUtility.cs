#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;

internal static class DariusModelImportUtility
{
    public static void ConfigureModelImporter(string assetPath, string variant)
    {
        ModelImporter modelImporter = AssetImporter.GetAtPath(assetPath) as ModelImporter;
        if (modelImporter == null)
            throw new InvalidOperationException("Expected ModelImporter: " + assetPath);

        modelImporter.importAnimation = true;
        modelImporter.importBlendShapes = true;
        modelImporter.importMaterials = true;
        modelImporter.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        modelImporter.animationType = ModelImporterAnimationType.Generic;
        modelImporter.clipAnimations = NormalizeClips(modelImporter.defaultClipAnimations);
        modelImporter.SaveAndReimport();
    }

    private static ModelImporterClipAnimation[] NormalizeClips(ModelImporterClipAnimation[] clips)
    {
        if (clips == null) return null;
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
        return !string.IsNullOrEmpty(name) &&
            (name.IndexOf("idle", StringComparison.OrdinalIgnoreCase) >= 0 ||
             name.IndexOf("run", StringComparison.OrdinalIgnoreCase) >= 0 ||
             name.IndexOf("walk", StringComparison.OrdinalIgnoreCase) >= 0);
    }

    private static string NormalizeClipName(string name)
    {
        return string.IsNullOrEmpty(name) ? name : name.Replace(" ", "_").Replace(".", "_");
    }

    public static string GetDirectory(string path)
    {
        return Path.GetDirectoryName(path)?.Replace("\\", "/");
    }
}
#endif
