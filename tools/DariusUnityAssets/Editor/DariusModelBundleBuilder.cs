#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class DariusModelBundleBuilder
{
    private const string SourceRoot = "Assets/DariusSource";
    private const string GeneratedRoot = "Assets/DariusGenerated";
    private const string BundleName = "darius_models.bundle";

    private sealed class SkinSpec
    {
        public string Variant;
        public string File;
    }

    private static readonly SkinSpec[] Skins =
    {
        new SkinSpec { Variant = "Classic", File = "darius.fbx" },
        new SkinSpec { Variant = "GodKing", File = "darius_godking.fbx" },
        new SkinSpec { Variant = "Dunkmaster", File = "darius_dunkmaster.fbx" },
        new SkinSpec { Variant = "Mecha", File = "darius_mecha.fbx" },
    };

    public static void BuildAllBatch()
    {
        try
        {
            BuildAll();
            EditorApplication.Exit(0);
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            EditorApplication.Exit(1);
        }
    }

    [MenuItem("Darius/Build Native Model Bundle")]
    public static void BuildAll()
    {
        string repoRoot = RequireDirectoryEnvironment("DARIUS_REPO_ROOT");
        string fbxRoot = RequireDirectoryEnvironment("DARIUS_MODEL_FBX_DIR");
        string workRoot = ResolveWorkRoot(repoRoot);

        EnsureAssetFolder(SourceRoot);
        if (AssetDatabase.IsValidFolder(GeneratedRoot))
            AssetDatabase.DeleteAsset(GeneratedRoot);
        EnsureAssetFolder(GeneratedRoot);

        List<string> prefabs = new List<string>();
        foreach (SkinSpec skin in Skins)
        {
            string source = Path.Combine(fbxRoot, skin.File);
            string assetPath = SourceRoot + "/" + skin.File;

            File.Copy(source, ToProjectAbsolute(assetPath), true);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
            DariusModelImportUtility.ConfigureModelImporter(assetPath, skin.Variant);
            DariusModelMaterialBinder.BindImportedTextures(assetPath);

            string prefab = DariusModelPrefabBuilder.Build(
                assetPath,
                GeneratedRoot + "/darius_" + skin.Variant.ToLowerInvariant() + ".prefab",
                skin.Variant);

            prefabs.Add(prefab);
        }

        AssetDatabase.SaveAssets();
        AssetBundleBuild build = new AssetBundleBuild
        {
            assetBundleName = BundleName,
            assetNames = prefabs.ToArray()
        };

        string output = Path.Combine(workRoot, "bundle-output");
        Directory.CreateDirectory(output);
        BuildPipeline.BuildAssetBundles(
            output,
            new[] { build },
            BuildAssetBundleOptions.ChunkBasedCompression,
            BuildTarget.StandaloneWindows64);

        File.Copy(
            Path.Combine(output, BundleName),
            Path.Combine(repoRoot, "assets", "models", BundleName),
            true);
    }

    private static string RequireDirectoryEnvironment(string key)
    {
        string value = Environment.GetEnvironmentVariable(key);
        if (string.IsNullOrEmpty(value) || !Directory.Exists(value))
            throw new DirectoryNotFoundException(key);
        return value;
    }

    private static string ResolveWorkRoot(string repoRoot)
    {
        string value = Environment.GetEnvironmentVariable("DARIUS_NATIVE_WORK_ROOT");
        return string.IsNullOrEmpty(value) ? Path.Combine(repoRoot, "native-assets-work") : value;
    }

    private static void EnsureAssetFolder(string path)
    {
        if (!AssetDatabase.IsValidFolder(path))
        {
            string parent = Path.GetDirectoryName(path);
            string name = Path.GetFileName(path);
            AssetDatabase.CreateFolder(parent, name);
        }
    }

    private static string ToProjectAbsolute(string assetPath)
    {
        return Path.Combine(Application.dataPath, assetPath.Substring("Assets/".Length));
    }
}
#endif
