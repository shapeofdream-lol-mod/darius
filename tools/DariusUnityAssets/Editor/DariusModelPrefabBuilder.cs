#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

internal static class DariusModelPrefabBuilder
{
    public static GameObject Build(
        string assetPath,
        string prefabPath,
        DariusNativeSkinProfile profile,
        List<string> bundleAssets)
    {
        if (profile == null) throw new ArgumentNullException(nameof(profile));
        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
        if (source == null) throw new InvalidOperationException("Missing imported model: " + assetPath);

        AnimationClip[] clips = LoadAnimationClips(assetPath);
        string generatedRoot = DariusNativeAssetContract.GeneratedAssetRoot;

        GameObject root = new GameObject("Darius_" + profile.Variant);
        GameObject model = UnityEngine.Object.Instantiate(source, root.transform, false);
        model.name = "Model";
        model.transform.localPosition = new Vector3(0f, profile.YOffset, 0f);
        model.transform.localRotation = Quaternion.Euler(0f, profile.Yaw, 0f);
        model.transform.localScale = Vector3.one * profile.Scale;

        DariusModelMaterialBinder.BindPrefabMaterials(model, assetPath, generatedRoot);
        DariusModelContractValidator.Validate(model, clips, profile);

        string controllerPath = generatedRoot + "/darius_" +
            DariusNativeAssetContract.NormalizeVariant(profile.Variant) + ".controller";
        AnimatorController controller = BuildController(controllerPath, clips, profile);
        ConfigureAnimator(model, controller);
        DariusModelMeshProcessor.ProcessPrefab(model, profile, generatedRoot, bundleAssets);

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        UnityEngine.Object.DestroyImmediate(root);
        if (prefab == null) throw new InvalidOperationException("Failed saving generated prefab: " + prefabPath);
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
        if (clips.Count == 0) throw new InvalidOperationException("Imported model has no animation clips: " + assetPath);
        return clips.ToArray();
    }

    private static AnimatorController BuildController(
        string path,
        AnimationClip[] clips,
        DariusNativeSkinProfile profile)
    {
        AssetDatabase.DeleteAsset(path);
        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(path);
        PopulateBaseStateMachine(controller.layers[0].stateMachine, clips, profile.Idle, profile.Variant);
        EditorUtility.SetDirty(controller);
        return controller;
    }

    private static void PopulateBaseStateMachine(
        AnimatorStateMachine stateMachine,
        AnimationClip[] clips,
        string idleClip,
        string variant)
    {
        HashSet<string> stateNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        AnimatorState defaultState = null;

        for (int i = 0; i < clips.Length; i++)
        {
            AnimationClip clip = clips[i];
            string stateName = DariusNativeAssetContract.AnimatorStateName(clip.name);
            if (!stateNames.Add(stateName))
                throw new InvalidOperationException("Animator state collision skin=" + variant +
                    " layer=base state=" + stateName);
            AnimatorState state = stateMachine.AddState(stateName);
            state.motion = clip;
            state.speed = 1f;
            if (string.Equals(clip.name, idleClip, StringComparison.OrdinalIgnoreCase)) defaultState = state;
        }

        if (defaultState == null)
            throw new InvalidOperationException("Required idle animation missing skin=" + variant + " clip=" + idleClip);
        stateMachine.defaultState = defaultState;
    }

    private static void ConfigureAnimator(GameObject model, RuntimeAnimatorController controller)
    {
        Animator animator = model.GetComponent<Animator>();
        if (animator == null) animator = model.AddComponent<Animator>();
        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
        animator.keepAnimatorStateOnDisable = true;
    }
}
#endif