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
        public float Yaw;
        public string Idle;
    }

    private static readonly SkinSpec[] Skins =
    {
        new SkinSpec { Variant = "Classic", File = "darius.fbx", Yaw = 0f, Idle = "Idle1" },
        new SkinSpec { Variant = "GodKing", File = "darius_godking.fbx", Yaw = 180f, Idle = "Idle1_Base" },
        new SkinSpec { Variant = "Dunkmaster", File = "darius_dunkmaster.fbx", Yaw = 0f, Idle = "Idle1_Base" },
        new SkinSpec { Variant = "Mecha", File = "darius_mecha.fbx", Yaw = 180f, Idle = "Idle1_Base" },
    };

    public static void BuildAllBatch()
    {
        try
        {
            BuildAll();
            Debug.Log("[DariusNativeAssets] batch build completed successfully");
            EditorApplication.Exit(0);
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            Debug.LogError("[DariusNativeAssets] batch build failed: " + e.Message);
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
        if (AssetDatabase.IsValidFolder(GeneratedRoot)) AssetDatabase.DeleteAsset(GeneratedRoot);
        EnsureAssetFolder(GeneratedRoot);

        List<string> prefabs = new List<string>();
        foreach (SkinSpec skin in Skins)
        {
            string source = Path.Combine(fbxRoot, skin.File);
            if (!File.Exists(source)) throw new FileNotFoundException("Native FBX missing", source);

            string assetPath = SourceRoot + "/" + skin.File;
            File.Copy(source, ToProjectAbsolute(assetPath), true);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            DariusModelImportUtility.ConfigureModelImporter(assetPath, skin.Variant);
            DariusModelMaterialBinder.BindImportedTextures(assetPath);

            string prefabPath = GeneratedRoot + "/darius_" + skin.Variant.ToLowerInvariant() + ".prefab";
            GameObject prefab = DariusModelPrefabBuilder.Build(assetPath, prefabPath, skin.Variant, skin.Yaw, skin.Idle);
            if (prefab == null || AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null)
                throw new InvalidOperationException("Generated prefab missing skin=" + skin.Variant);
            prefabs.Add(prefabPath);
        }

        if (prefabs.Count != Skins.Length)
            throw new InvalidOperationException("Native prefab count mismatch expected=" + Skins.Length + " actual=" + prefabs.Count);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        string output = Path.Combine(workRoot, "bundle-output");
        Directory.CreateDirectory(output);
        AssetBundleBuild build = new AssetBundleBuild { assetBundleName = BundleName, assetNames = prefabs.ToArray() };
        AssetBundleManifest manifest = BuildPipeline.BuildAssetBundles(
            output,
            new[] { build },
            BuildAssetBundleOptions.ChunkBasedCompression,
            BuildTarget.StandaloneWindows64);
        if (manifest == null) throw new InvalidOperationException("Unity returned no AssetBundle manifest.");

        string builtBundle = Path.Combine(output, BundleName);
        if (!File.Exists(builtBundle)) throw new FileNotFoundException("Built AssetBundle missing", builtBundle);
        string destination = Path.Combine(repoRoot, "assets", "models", BundleName);
        Directory.CreateDirectory(Path.GetDirectoryName(destination));
        File.Copy(builtBundle, destination, true);
        Debug.Log("[DariusNativeAssets] wrote " + destination + " bytes=" + new FileInfo(destination).Length);
    }

    private static string RequireDirectoryEnvironment(string key)
    {
        string value = Environment.GetEnvironmentVariable(key);
        if (string.IsNullOrEmpty(value) || !Directory.Exists(value)) throw new DirectoryNotFoundException(key);
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
            AssetDatabase.CreateFolder(Path.GetDirectoryName(path), Path.GetFileName(path));
    }

    private static string ToProjectAbsolute(string assetPath)
    {
        return Path.Combine(Application.dataPath, assetPath.Substring("Assets/".Length));
    }
}
#endif
