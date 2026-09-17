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

        string maskPath = DariusNativeAssetContract.LowerBodyMaskAssetPath(profile.Variant);
        AvatarMask lowerBodyMask = BuildLowerBodyMask(maskPath, model.transform, profile.Variant);
        bundleAssets.Add(maskPath);

        string controllerPath = generatedRoot + "/darius_" +
            DariusNativeAssetContract.NormalizeVariant(profile.Variant) + ".controller";
        AnimatorController controller = BuildController(controllerPath, clips, profile, lowerBodyMask);
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

    private static AvatarMask BuildLowerBodyMask(string path, Transform modelRoot, string variant)
    {
        if (modelRoot == null) throw new ArgumentNullException(nameof(modelRoot));
        AssetDatabase.DeleteAsset(path);

        AvatarMask mask = new AvatarMask();
        mask.name = "Darius_" + variant + "_LowerBody";
        mask.AddTransformPath(modelRoot, true);

        int active = 0;
        for (int i = 0; i < mask.transformCount; i++)
        {
            string transformPath = mask.GetTransformPath(i) ?? string.Empty;
            int slash = transformPath.LastIndexOf('/');
            string name = slash >= 0 ? transformPath.Substring(slash + 1) : transformPath;
            bool enabled = DariusNativeAssetContract.IsLowerBodyLocomotionName(name);
            mask.SetTransformActive(i, enabled);
            if (enabled) active++;
        }
        if (active == 0)
        {
            UnityEngine.Object.DestroyImmediate(mask);
            throw new InvalidOperationException("Lower-body AvatarMask found no leg transforms skin=" + variant);
        }

        AssetDatabase.CreateAsset(mask, path);
        EditorUtility.SetDirty(mask);
        return mask;
    }

    private static AnimatorController BuildController(
        string path,
        AnimationClip[] clips,
        DariusNativeSkinProfile profile,
        AvatarMask lowerBodyMask)
    {
        AssetDatabase.DeleteAsset(path);
        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(path);
        PopulateBaseStateMachine(controller.layers[0].stateMachine, clips, profile.Idle, profile.Variant);

        controller.AddLayer(DariusNativeAssetContract.LowerBodyAnimatorLayerName);
        AnimatorControllerLayer[] layers = controller.layers;
        AnimatorControllerLayer lower = layers[layers.Length - 1];
        lower.avatarMask = lowerBodyMask;
        lower.blendingMode = AnimatorLayerBlendingMode.Override;
        lower.defaultWeight = 0f;
        PopulateLowerBodyStateMachine(lower.stateMachine, clips, profile);
        layers[layers.Length - 1] = lower;
        controller.layers = layers;

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

    private static void PopulateLowerBodyStateMachine(
        AnimatorStateMachine stateMachine,
        AnimationClip[] clips,
        DariusNativeSkinProfile profile)
    {
        AnimatorState disabled = stateMachine.AddState("Disabled");
        stateMachine.defaultState = disabled;

        Dictionary<string, AnimationClip> byName = new Dictionary<string, AnimationClip>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < clips.Length; i++)
            if (clips[i] != null && !string.IsNullOrEmpty(clips[i].name) && !byName.ContainsKey(clips[i].name))
                byName.Add(clips[i].name, clips[i]);

        HashSet<string> added = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        AddLowerBodyState(stateMachine, byName, added, profile.Run, true, profile.Variant);
        AddLowerBodyState(stateMachine, byName, added, profile.WRun, false, profile.Variant);
        AddLowerBodyState(stateMachine, byName, added, "Spell2_Run", false, profile.Variant);
    }

    private static void AddLowerBodyState(
        AnimatorStateMachine stateMachine,
        Dictionary<string, AnimationClip> clips,
        HashSet<string> added,
        string clipName,
        bool required,
        string variant)
    {
        if (string.IsNullOrEmpty(clipName)) return;
        AnimationClip clip;
        if (!clips.TryGetValue(clipName, out clip))
        {
            if (required)
                throw new InvalidOperationException("Required lower-body locomotion clip missing skin=" + variant +
                    " clip=" + clipName);
            return;
        }

        string stateName = DariusNativeAssetContract.AnimatorStateName(clip.name);
        if (!added.Add(stateName)) return;
        AnimatorState state = stateMachine.AddState(stateName);
        state.motion = clip;
        state.speed = 1f;
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