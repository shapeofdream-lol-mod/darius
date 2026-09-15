#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// Build-time only. Run from a Unity 6000.0.x editor project with:
//   DARIUS_REPO_ROOT=<repo>
//   DARIUS_MODEL_FBX_DIR=<folder containing darius*.fbx>
//   DARIUS_NATIVE_WORK_ROOT=<build workspace>
//   Unity.exe -batchmode -projectPath <project> \
//     -executeMethod DariusModelBundleBuilder.BuildAllBatch
//
// The resulting AssetBundle is copied to <repo>/assets/models/darius_models.bundle.
// The shipped mod uses only Unity's AssetBundle/Animator APIs and has no Blender/glTF dependency.
public static class DariusModelBundleBuilder
{
    private const string SourceRoot = "Assets/DariusSource";
    private const string GeneratedRoot = "Assets/DariusGenerated";
    private const string BundleName = "darius_models.bundle";
    private const float RuntimeScale = 0.00921f;
    private const float RuntimeYOffset = 0.04f;

    private sealed class SkinSpec
    {
        public string Variant;
        public string File;
        public float Yaw;
        public string Idle;
        public string Run;
    }

    private static readonly SkinSpec[] Skins =
    {
        new SkinSpec { Variant = "Classic", File = "darius.fbx", Yaw = 0f, Idle = "Idle1", Run = "Run" },
        new SkinSpec { Variant = "GodKing", File = "darius_godking.fbx", Yaw = 180f, Idle = "Idle1_Base", Run = "Run_Normal" },
        new SkinSpec { Variant = "Dunkmaster", File = "darius_dunkmaster.fbx", Yaw = 0f, Idle = "Idle1_Base", Run = "Darius_Skin04_Run.anm" },
        new SkinSpec { Variant = "Mecha", File = "darius_mecha.fbx", Yaw = 180f, Idle = "Idle1_Base", Run = "Run_Normal" },
    };

    // Batch entry point deliberately owns the editor exit code. Relying on -quit around a long
    // synchronous import/build makes failures and hangs opaque to PowerShell/Agent callers.
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
        Debug.Log("[DariusNativeAssets] begin repo=" + repoRoot + " fbx=" + fbxRoot + " work=" + workRoot);

        EnsureAssetFolder(SourceRoot);
        if (AssetDatabase.IsValidFolder(GeneratedRoot)) AssetDatabase.DeleteAsset(GeneratedRoot);
        EnsureAssetFolder(GeneratedRoot);

        List<string> prefabPaths = new List<string>();
        foreach (SkinSpec skin in Skins)
        {
            string source = Path.Combine(fbxRoot, skin.File);
            if (!File.Exists(source)) throw new FileNotFoundException("Missing converted Darius FBX", source);
            string assetPath = SourceRoot + "/" + skin.File;

            Debug.Log("[DariusNativeAssets] skin=" + skin.Variant + " stage=copy-fbx");
            File.Copy(source, ToProjectAbsolute(assetPath), true);

            Debug.Log("[DariusNativeAssets] skin=" + skin.Variant + " stage=import-fbx");
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);

            Debug.Log("[DariusNativeAssets] skin=" + skin.Variant + " stage=configure-importer");
            ConfigureModelImporter(assetPath);

            Debug.Log("[DariusNativeAssets] skin=" + skin.Variant + " stage=build-prefab");
            string prefabPath = BuildSkinPrefab(skin, assetPath);
            prefabPaths.Add(prefabPath);
            Debug.Log("[DariusNativeAssets] skin=" + skin.Variant + " stage=done prefab=" + prefabPath);
        }

        Debug.Log("[DariusNativeAssets] stage=save-generated-assets");
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        string bundleOutput = Path.Combine(workRoot, "bundle-output");
        if (Directory.Exists(bundleOutput)) Directory.Delete(bundleOutput, true);
        Directory.CreateDirectory(bundleOutput);

        AssetBundleBuild build = new AssetBundleBuild
        {
            assetBundleName = BundleName,
            assetNames = prefabPaths.ToArray(),
        };

        Debug.Log("[DariusNativeAssets] stage=build-assetbundle prefabs=" + prefabPaths.Count + " output=" + bundleOutput);
        AssetBundleManifest manifest = BuildPipeline.BuildAssetBundles(
            bundleOutput,
            new[] { build },
            BuildAssetBundleOptions.ChunkBasedCompression | BuildAssetBundleOptions.DeterministicAssetBundle,
            BuildTarget.StandaloneWindows64);
        if (manifest == null) throw new InvalidOperationException("BuildPipeline.BuildAssetBundles returned null manifest.");

        string built = Path.Combine(bundleOutput, BundleName);
        if (!File.Exists(built)) throw new FileNotFoundException("Expected AssetBundle output missing", built);
        string destination = Path.Combine(repoRoot, "assets", "models", BundleName);
        Directory.CreateDirectory(Path.GetDirectoryName(destination));

        Debug.Log("[DariusNativeAssets] stage=copy-bundle destination=" + destination);
        File.Copy(built, destination, true);

        Debug.Log("[DariusNativeAssets] Built " + destination + " bytes=" + new FileInfo(destination).Length +
                  " prefabs=" + prefabPaths.Count + " compression=LZ4");
    }

    private static void ConfigureModelImporter(string assetPath)
    {
        ModelImporter importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
        if (importer == null) throw new InvalidOperationException("Not a ModelImporter: " + assetPath);
        importer.importAnimation = true;
        importer.animationType = ModelImporterAnimationType.Generic;
        importer.avatarSetup = ModelImporterAvatarSetup.NoAvatar;
        importer.importCameras = false;
        importer.importLights = false;
        importer.isReadable = false;

        ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;
        if (clips != null && clips.Length > 0)
        {
            for (int i = 0; i < clips.Length; i++)
            {
                string name = clips[i].name ?? string.Empty;
                bool loop = IsLoopingClip(name);
                clips[i].loopTime = loop;
                clips[i].loopPose = loop;
                clips[i].keepOriginalOrientation = true;
                clips[i].keepOriginalPositionY = true;
                clips[i].keepOriginalPositionXZ = true;
            }
            importer.clipAnimations = clips;
        }
        importer.SaveAndReimport();
    }

    private static string BuildSkinPrefab(SkinSpec skin, string fbxPath)
    {
        AnimationClip[] clips = AssetDatabase.LoadAllAssetsAtPath(fbxPath)
            .OfType<AnimationClip>()
            .Where(c => c != null && !c.name.StartsWith("__preview__", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (clips.Length == 0) throw new InvalidDataException("FBX has no animation clips: " + fbxPath);

        Debug.Log("[DariusNativeAssets] skin=" + skin.Variant + " clips=" + clips.Length);
        string controllerPath = GeneratedRoot + "/darius_" + skin.Variant.ToLowerInvariant() + ".controller";
        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
        AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
        AnimatorState defaultState = null;
        foreach (AnimationClip clip in clips)
        {
            AnimatorState state = stateMachine.AddState(clip.name);
            state.motion = clip;
            state.speed = 1f;
            if (string.Equals(clip.name, skin.Idle, StringComparison.OrdinalIgnoreCase)) defaultState = state;
        }
        if (defaultState == null) throw new InvalidDataException("Required idle clip missing for " + skin.Variant + ": " + skin.Idle);
        stateMachine.defaultState = defaultState;

        GameObject imported = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
        if (imported == null) throw new InvalidDataException("FBX root prefab missing: " + fbxPath);
        GameObject container = new GameObject("Darius_" + skin.Variant);
        try
        {
            GameObject model = PrefabUtility.InstantiatePrefab(imported) as GameObject;
            if (model == null) throw new InvalidOperationException("PrefabUtility.InstantiatePrefab failed: " + fbxPath);
            model.name = "Model";
            model.transform.SetParent(container.transform, false);
            model.transform.localPosition = new Vector3(0f, RuntimeYOffset, 0f);
            model.transform.localRotation = Quaternion.Euler(0f, skin.Yaw, 0f);
            model.transform.localScale = Vector3.one * RuntimeScale;

            Animator animator = model.GetComponent<Animator>();
            if (animator == null) animator = model.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

            SkinnedMeshRenderer[] renderers = model.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            Debug.Log("[DariusNativeAssets] skin=" + skin.Variant + " skinnedRenderers=" + renderers.Length);
            for (int i = 0; i < renderers.Length; i++)
            {
                renderers[i].updateWhenOffscreen = false;
                ExpandBounds(renderers[i], 1.20f);
            }
            if (string.Equals(skin.Variant, "GodKing", StringComparison.OrdinalIgnoreCase))
                SplitAuthoredHiddenSubmeshes(model, renderers, skin.Variant);

            string prefabPath = GeneratedRoot + "/darius_" + skin.Variant.ToLowerInvariant() + ".prefab";
            PrefabUtility.SaveAsPrefabAsset(container, prefabPath);
            return prefabPath;
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(container);
        }
    }

    private static void SplitAuthoredHiddenSubmeshes(GameObject model, SkinnedMeshRenderer[] renderers, string variant)
    {
        for (int ri = 0; ri < renderers.Length; ri++)
        {
            SkinnedMeshRenderer source = renderers[ri];
            if (source == null || source.sharedMesh == null) continue;
            Material[] materials = source.sharedMaterials;
            List<int> hidden = new List<int>();
            for (int i = 0; i < materials.Length && i < source.sharedMesh.subMeshCount; i++)
            {
                string materialName = materials[i] != null ? materials[i].name : string.Empty;
                if (IsAuthoredHiddenMaterial(materialName)) hidden.Add(i);
            }
            if (hidden.Count == 0) continue;

            Debug.Log("[DariusNativeAssets] skin=" + variant + " hiddenSubmeshes=" + hidden.Count + " renderer=" + source.name);
            Mesh visibleMesh = UnityEngine.Object.Instantiate(source.sharedMesh);
            Mesh hiddenMesh = UnityEngine.Object.Instantiate(source.sharedMesh);
            visibleMesh.name = source.sharedMesh.name + "_VisibleOnly";
            hiddenMesh.name = source.sharedMesh.name + "_AuthoredHiddenOnly";
            for (int i = 0; i < source.sharedMesh.subMeshCount; i++)
            {
                if (hidden.Contains(i)) visibleMesh.SetTriangles(Array.Empty<int>(), i, false);
                else hiddenMesh.SetTriangles(Array.Empty<int>(), i, false);
            }
            visibleMesh.RecalculateBounds();
            hiddenMesh.RecalculateBounds();

            string safeName = SanitizeAssetName(variant + "_" + source.name + "_" + ri);
            string visiblePath = GeneratedRoot + "/" + safeName + "_visible.asset";
            string hiddenPath = GeneratedRoot + "/" + safeName + "_hidden.asset";
            AssetDatabase.CreateAsset(visibleMesh, visiblePath);
            AssetDatabase.CreateAsset(hiddenMesh, hiddenPath);
            source.sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(visiblePath);

            GameObject hiddenGo = new GameObject("DariusHidden_" + source.name);
            hiddenGo.transform.SetParent(source.transform, false);
            SkinnedMeshRenderer hiddenRenderer = hiddenGo.AddComponent<SkinnedMeshRenderer>();
            hiddenRenderer.sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(hiddenPath);
            hiddenRenderer.sharedMaterials = materials;
            hiddenRenderer.bones = source.bones;
            hiddenRenderer.rootBone = source.rootBone;
            hiddenRenderer.quality = source.quality;
            hiddenRenderer.updateWhenOffscreen = false;
            hiddenRenderer.localBounds = source.localBounds;
            hiddenRenderer.enabled = false;
        }
    }

    private static bool IsAuthoredHiddenMaterial(string name)
    {
        if (string.IsNullOrEmpty(name)) return false;
        return name.IndexOf("Wolf_Mat", StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("Throne", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool IsLoopingClip(string name)
    {
        if (string.IsNullOrEmpty(name)) return false;
        return name.IndexOf("Idle", StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("Run", StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("Walk", StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("Dance", StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("Channel", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static void ExpandBounds(SkinnedMeshRenderer renderer, float factor)
    {
        Bounds bounds = renderer.localBounds;
        if (bounds.size.sqrMagnitude <= 0.000001f && renderer.sharedMesh != null) bounds = renderer.sharedMesh.bounds;
        bounds.extents *= Mathf.Max(1f, factor);
        renderer.localBounds = bounds;
    }

    private static string RequireDirectoryEnvironment(string key)
    {
        string value = Environment.GetEnvironmentVariable(key);
        if (string.IsNullOrWhiteSpace(value)) throw new InvalidOperationException("Environment variable is required: " + key);
        value = Path.GetFullPath(value);
        if (!Directory.Exists(value)) throw new DirectoryNotFoundException(key + "=" + value);
        return value;
    }

    private static string ResolveWorkRoot(string repoRoot)
    {
        string value = Environment.GetEnvironmentVariable("DARIUS_NATIVE_WORK_ROOT");
        if (string.IsNullOrWhiteSpace(value)) value = Path.Combine(repoRoot, "build", "native-assets-work", "editor-manual");
        value = Path.GetFullPath(value);
        Directory.CreateDirectory(value);
        return value;
    }

    private static string ToProjectAbsolute(string assetPath)
    {
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        return Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar));
    }

    private static void EnsureAssetFolder(string assetPath)
    {
        string[] parts = assetPath.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }

    private static string SanitizeAssetName(string value)
    {
        foreach (char c in Path.GetInvalidFileNameChars()) value = value.Replace(c, '_');
        return value.Replace(' ', '_');
    }
}
#endif
