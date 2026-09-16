#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

internal static class DariusModelPrefabBuilder
{
    private const float RuntimeScale = 0.00921f;
    private const float RuntimeYOffset = 0.04f;

    public static GameObject Build(
        string assetPath,
        string prefabPath,
        string variant,
        float yaw,
        string idleClip)
    {
        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
        if (source == null)
            throw new InvalidOperationException("Missing imported model: " + assetPath);

        AnimationClip[] clips = LoadAnimationClips(assetPath);
        if (clips.Length == 0)
            throw new InvalidOperationException("Imported model has no animation clips: " + assetPath);

        string generatedRoot = (Path.GetDirectoryName(prefabPath) ?? "Assets").Replace('\\', '/');
        string controllerPath = generatedRoot + "/darius_" + variant.ToLowerInvariant() + ".controller";
        AnimatorController controller = BuildController(controllerPath, clips, idleClip, variant);

        GameObject root = new GameObject("Darius_" + variant);
        GameObject model = UnityEngine.Object.Instantiate(source, root.transform, false);
        model.name = "Model";
        model.transform.localPosition = new Vector3(0f, RuntimeYOffset, 0f);
        model.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        model.transform.localScale = Vector3.one * RuntimeScale;

        ConfigureAnimator(model, controller);
        DariusModelMeshProcessor.ProcessPrefab(model, variant, generatedRoot);

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        UnityEngine.Object.DestroyImmediate(root);
        if (prefab == null)
            throw new InvalidOperationException("Failed saving generated prefab: " + prefabPath);

        AssetDatabase.SaveAssets();
        return prefab;
    }

    private static AnimationClip[] LoadAnimationClips(string assetPath)
    {
        List<AnimationClip> clips = new List<AnimationClip>();
        foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(assetPath))
        {
            AnimationClip clip = asset as AnimationClip;
            if (clip == null || string.IsNullOrEmpty(clip.name)) continue;
            if (clip.name.StartsWith("__preview__", StringComparison.OrdinalIgnoreCase)) continue;
            clips.Add(clip);
        }
        return clips.ToArray();
    }

    private static AnimatorController BuildController(
        string path,
        AnimationClip[] clips,
        string idleClip,
        string variant)
    {
        AssetDatabase.DeleteAsset(path);
        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(path);
        AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
        HashSet<string> stateNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        AnimatorState defaultState = null;

        for (int i = 0; i < clips.Length; i++)
        {
            AnimationClip clip = clips[i];
            string stateName = ToAnimatorStateName(clip.name);
            if (!stateNames.Add(stateName))
                throw new InvalidOperationException("Animator state collision skin=" + variant + " state=" + stateName);

            AnimatorState state = stateMachine.AddState(stateName);
            state.motion = clip;
            state.speed = 1f;
            if (string.Equals(clip.name, idleClip, StringComparison.OrdinalIgnoreCase))
                defaultState = state;
        }

        if (defaultState == null)
            throw new InvalidOperationException("Required idle animation missing skin=" + variant + " clip=" + idleClip);
        stateMachine.defaultState = defaultState;
        EditorUtility.SetDirty(controller);
        return controller;
    }

    private static void ConfigureAnimator(GameObject model, RuntimeAnimatorController controller)
    {
        Animator animator = model.GetComponent<Animator>();
        if (animator == null) animator = model.AddComponent<Animator>();
        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
        animator.keepAnimatorControllerStateOnDisable = true;
    }

    private static string ToAnimatorStateName(string clipName)
    {
        return clipName.Replace('.', '_').Replace('/', '_').Replace('\\', '_');
    }
}
#endif
