using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

public sealed class DariusOfficialEntityModelMarker : MonoBehaviour
{
    public string variantKey;
}

public sealed class DariusOfficialEntityModelDiagnostics : MonoBehaviour
{
    private Hero_Darius _hero;
    private EntityVisual _visual;
    private EntityAnimation _animation;
    private EntityModel _model;
    private Vector3 _lastPosition;
    private float _lastSampleTime;
    private float _nextSampleTime;
    private float _stopAt;
    private int _sample;

    public void Bind(Hero_Darius hero)
    {
        _hero = hero;
        _visual = hero != null ? hero.Visual : null;
        _animation = hero != null ? hero.GetComponent<EntityAnimation>() : null;
        _model = _visual != null ? _visual.model : null;
        _lastPosition = hero != null ? hero.transform.position : Vector3.zero;
        _lastSampleTime = Time.unscaledTime;
        _nextSampleTime = Time.unscaledTime;
        _stopAt = Time.unscaledTime + 20f;
        _sample = 0;
        Dump("bind", true);
    }

    private void Update()
    {
        if (_hero == null || Time.unscaledTime > _stopAt)
        {
            enabled = false;
            return;
        }
        if (Time.unscaledTime < _nextSampleTime) return;
        _nextSampleTime = Time.unscaledTime + 1f;
        Dump("sample", true);
    }

    private void Dump(string reason, bool includeRenderers)
    {
        try
        {
            float now = Time.unscaledTime;
            Vector3 position = _hero != null ? _hero.transform.position : Vector3.zero;
            float dt = Mathf.Max(0.0001f, now - _lastSampleTime);
            float worldSpeed = (position - _lastPosition).magnitude / dt;
            _lastPosition = position;
            _lastSampleTime = now;

            Animator animator = _animation != null ? _animation.animator : null;
            string currentClip = "<none>";
            string nextClip = "<none>";
            int currentHash = 0;
            int nextHash = 0;
            float normalized = 0f;
            bool transition = false;
            if (animator != null && animator.layerCount > 0)
            {
                AnimatorStateInfo current = animator.GetCurrentAnimatorStateInfo(0);
                currentHash = current.shortNameHash;
                normalized = current.normalizedTime;
                transition = animator.IsInTransition(0);
                AnimatorClipInfo[] currentInfos = animator.GetCurrentAnimatorClipInfo(0);
                if (currentInfos != null && currentInfos.Length > 0 && currentInfos[0].clip != null)
                    currentClip = currentInfos[0].clip.name;
                if (transition)
                {
                    AnimatorStateInfo next = animator.GetNextAnimatorStateInfo(0);
                    nextHash = next.shortNameHash;
                    AnimatorClipInfo[] nextInfos = animator.GetNextAnimatorClipInfo(0);
                    if (nextInfos != null && nextInfos.Length > 0 && nextInfos[0].clip != null)
                        nextClip = nextInfos[0].clip.name;
                }
            }

            Transform modelTransform = _visual != null ? _visual.modelTransform : null;
            string modelTransformData = modelTransform != null
                ? " modelWorld=" + DariusLog.Vec(modelTransform.position) +
                  " modelLocal=" + DariusLog.Vec(modelTransform.localPosition) +
                  " modelScale=" + DariusLog.Vec(modelTransform.localScale) +
                  " modelLossyScale=" + DariusLog.Vec(modelTransform.lossyScale) +
                  " modelEuler=" + DariusLog.Vec(modelTransform.localEulerAngles)
                : string.Empty;
            DariusLog.Info("OFFICIAL-ENTITYMODEL-DIAG",
                "reason=" + reason + " sample=" + _sample +
                " pos=" + DariusLog.Vec(position) +
                " worldSpeed=" + worldSpeed.ToString("0.###") +
                " visualOff=" + (_visual != null && _visual.isRendererOff) +
                " visualRenderers=" + (_visual != null && _visual.renderers != null ? _visual.renderers.Count : -1) +
                " solidRenderers=" + (_visual != null && _visual.solidRenderers != null ? _visual.solidRenderers.Count : -1) +
                " model=" + (_model != null ? _model.name : "<null>") +
                " modelActive=" + (_model != null && _model.gameObject.activeInHierarchy) +
                " modelTransform=" + (modelTransform != null ? modelTransform.name : "<null>") +
                " modelLayer=" + (modelTransform != null ? modelTransform.gameObject.layer.ToString() : "<null>") +
                modelTransformData +
                " animatorActive=" + (animator != null && animator.gameObject.activeInHierarchy) +
                " animatorEnabled=" + (animator != null && animator.enabled) +
                " controller=" + (animator != null && animator.runtimeAnimatorController != null
                    ? animator.runtimeAnimatorController.name : "<null>") +
                " currentClip=" + currentClip +
                " currentHash=" + currentHash +
                " normalized=" + normalized.ToString("0.###") +
                " transition=" + transition +
                " nextClip=" + nextClip +
                " nextHash=" + nextHash);

            if (includeRenderers && _model != null)
            {
                Renderer[] renderers = _model.GetComponentsInChildren<Renderer>(true);
                for (int i = 0; i < renderers.Length; i++)
                {
                    Renderer renderer = renderers[i];
                    if (renderer == null) continue;
                    Material[] materials = renderer.sharedMaterials;
                    string materialSummary = string.Empty;
                    for (int mi = 0; mi < materials.Length; mi++)
                    {
                        Material material = materials[mi];
                        if (mi > 0) materialSummary += ",";
                        materialSummary += material != null
                            ? material.name + "/" + (material.shader != null ? material.shader.name : "<no-shader>")
                            : "<null>";
                    }
                    Bounds bounds = renderer.bounds;
                    string skinnedSummary = string.Empty;
                    SkinnedMeshRenderer skinned = renderer as SkinnedMeshRenderer;
                    if (skinned != null)
                    {
                        Bounds localBounds = skinned.localBounds;
                        skinnedSummary =
                            " updateOffscreen=" + skinned.updateWhenOffscreen +
                            " rootBone=" + (skinned.rootBone != null ? skinned.rootBone.name : "<null>") +
                            " rootBoneWorld=" + (skinned.rootBone != null ? DariusLog.Vec(skinned.rootBone.position) : "<null>") +
                            " localBoundsCenter=" + DariusLog.Vec(localBounds.center) +
                            " localBoundsSize=" + DariusLog.Vec(localBounds.size) +
                            " meshBoundsCenter=" + (skinned.sharedMesh != null ? DariusLog.Vec(skinned.sharedMesh.bounds.center) : "<null>") +
                            " meshBoundsSize=" + (skinned.sharedMesh != null ? DariusLog.Vec(skinned.sharedMesh.bounds.size) : "<null>");
                    }
                    DariusLog.Info("OFFICIAL-ENTITYMODEL-RENDER",
                        "sample=" + _sample + " index=" + i +
                        " name=" + renderer.name +
                        " enabled=" + renderer.enabled +
                        " active=" + renderer.gameObject.activeInHierarchy +
                        " visible=" + renderer.isVisible +
                        " layer=" + renderer.gameObject.layer +
                        " forceOff=" + renderer.forceRenderingOff +
                        " shadow=" + renderer.shadowCastingMode +
                        " world=" + DariusLog.Vec(renderer.transform.position) +
                        " local=" + DariusLog.Vec(renderer.transform.localPosition) +
                        " localScale=" + DariusLog.Vec(renderer.transform.localScale) +
                        " lossyScale=" + DariusLog.Vec(renderer.transform.lossyScale) +
                        " boundsCenter=" + DariusLog.Vec(bounds.center) +
                        " boundsSize=" + DariusLog.Vec(bounds.size) +
                        skinnedSummary +
                        " materials=" + materialSummary);

                    for (int mi = 0; mi < materials.Length; mi++)
                    {
                        Material material = materials[mi];
                        if (material == null) continue;
                        DariusLog.Info("OFFICIAL-ENTITYMODEL-MATERIAL",
                            "sample=" + _sample + " renderer=" + renderer.name + " slot=" + mi +
                            " " + DescribeMaterial(material));
                    }

                    bool hasPropertyBlock = renderer.HasPropertyBlock();
                    if (hasPropertyBlock)
                    {
                        MaterialPropertyBlock block = new MaterialPropertyBlock();
                        renderer.GetPropertyBlock(block);
                        DariusLog.Info("OFFICIAL-ENTITYMODEL-MPB",
                            "sample=" + _sample + " renderer=" + renderer.name +
                            " hasBlock=True" +
                            " color=" + DescribeColor(block.GetColor(Shader.PropertyToID("_Color"))) +
                            " baseColor=" + DescribeColor(block.GetColor(Shader.PropertyToID("_BaseColor"))) +
                            " alpha=" + block.GetFloat(Shader.PropertyToID("_Alpha")).ToString("0.###") +
                            " opacity=" + block.GetFloat(Shader.PropertyToID("_Opacity")).ToString("0.###") +
                            " fade=" + block.GetFloat(Shader.PropertyToID("_Fade")).ToString("0.###") +
                            " visibility=" + block.GetFloat(Shader.PropertyToID("_Visibility")).ToString("0.###"));
                    }
                    else
                    {
                        DariusLog.Info("OFFICIAL-ENTITYMODEL-MPB",
                            "sample=" + _sample + " renderer=" + renderer.name + " hasBlock=False");
                    }
                }
            }
            _sample++;
        }
        catch (Exception e)
        {
            DariusLog.Exception("OFFICIAL-ENTITYMODEL-DIAG", e, "Fresh EntityModel diagnostics failed");
            enabled = false;
        }
    }

    private static string DescribeMaterial(Material material)
    {
        if (material == null) return "<null>";
        string value =
            "name=" + material.name +
            " shader=" + (material.shader != null ? material.shader.name : "<null>") +
            " queue=" + material.renderQueue +
            " shaderQueue=" + (material.shader != null ? material.shader.renderQueue : -1) +
            " renderType=" + material.GetTag("RenderType", false, "<none>") +
            " mainTexture=" + (material.mainTexture != null ? material.mainTexture.name : "<null>");

        value += DescribeMaterialColor(material, "_Color");
        value += DescribeMaterialColor(material, "_BaseColor");
        value += DescribeMaterialFloat(material, "_Mode");
        value += DescribeMaterialFloat(material, "_Surface");
        value += DescribeMaterialFloat(material, "_SrcBlend");
        value += DescribeMaterialFloat(material, "_DstBlend");
        value += DescribeMaterialFloat(material, "_ZWrite");
        value += DescribeMaterialFloat(material, "_Cull");
        value += DescribeMaterialFloat(material, "_Cutoff");
        value += DescribeMaterialFloat(material, "_AlphaClip");
        value += DescribeMaterialFloat(material, "_Alpha");
        value += " keywords=" + string.Join(",", material.shaderKeywords ?? Array.Empty<string>());
        return value;
    }

    private static string DescribeMaterialColor(Material material, string property)
    {
        return material != null && material.HasProperty(property)
            ? " " + property + "=" + DescribeColor(material.GetColor(property))
            : " " + property + "=<none>";
    }

    private static string DescribeMaterialFloat(Material material, string property)
    {
        return material != null && material.HasProperty(property)
            ? " " + property + "=" + material.GetFloat(property).ToString("0.###")
            : " " + property + "=<none>";
    }

    private static string DescribeColor(Color color)
    {
        return "(" +
            color.r.ToString("0.###") + "," +
            color.g.ToString("0.###") + "," +
            color.b.ToString("0.###") + "," +
            color.a.ToString("0.###") + ")";
    }
}

// Independent traveler implementation for Hero_Darius.
// Runtime-created Hero/Skin resources remain necessary because the stock mod loader does not
// extend the game's Addressables catalog with custom Hero/Skin GUIDs. Model playback itself now
// prefers Unity-native assets and Shape of Dreams' EntityAnimation contract.
public sealed class Hero_Darius : Hero
{
    private DariusNativeModelBridge _nativeModelBridge;

    internal DariusNativeModelBridge NativeModelBridge
    {
        get
        {
            if (_nativeModelBridge == null || !_nativeModelBridge.IsReady)
                _nativeModelBridge = GetComponentInChildren<DariusNativeModelBridge>(true);
            return _nativeModelBridge != null && _nativeModelBridge.IsReady ? _nativeModelBridge : null;
        }
    }

    public override void OnModelLoaded()
    {
        base.OnModelLoaded();
        try
        {
            bool officialFresh = DariusNativeAnimationMode.IsOfficialFreshModel(this);
            DariusNativeModelBridge native = officialFresh ? null : NativeModelBridge;
            if (native != null)
            {
                native.BindHero(this);
                if (!native.IsReady) native = null;
            }

            DariusTravelerModelInstance legacy = native == null && !officialFresh
                ? GetComponentInChildren<DariusTravelerModelInstance>(true)
                : null;
            if (legacy != null)
            {
                if (legacy.enabled) legacy.enabled = false;
                legacy.enabled = true;
                legacy.BindHero(this);
            }

            DariusBasicAttackVisualRuntime attackVisual = GetComponent<DariusBasicAttackVisualRuntime>();
            if (attackVisual == null) attackVisual = gameObject.AddComponent<DariusBasicAttackVisualRuntime>();
            attackVisual.Bind(this);
            EntityAnimation animation = GetComponent<EntityAnimation>();
            EntityModel loadedModel = Visual != null ? Visual.model : null;
            DariusLog.Info("TRAVELER-MODEL", "Hero_Darius.OnModelLoaded officialFresh=" + officialFresh +
                " native=" + (native != null) + " legacy=" + (legacy != null) +
                " entityModel=" + (loadedModel != null ? loadedModel.name : "<null>") +
                " initialized=" + (loadedModel != null && loadedModel.isInitialized) +
                " animator=" + (animation != null && animation.animator != null ? animation.animator.gameObject.name : "<null>") +
                " attackVisual=" + (attackVisual != null));

            if (officialFresh)
            {
                DariusOfficialEntityModelDiagnostics diagnostics =
                    GetComponent<DariusOfficialEntityModelDiagnostics>();
                if (diagnostics == null) diagnostics = gameObject.AddComponent<DariusOfficialEntityModelDiagnostics>();
                diagnostics.Bind(this);
            }
        }
        catch (Exception e)
        {
            DariusLog.Exception("TRAVELER-MODEL", e, "Hero_Darius.OnModelLoaded bridge bind failed");
        }
    }
}

internal static class DariusNativeAnimationMode
{
    public static bool IsNative(EntityAnimation animation)
    {
        Hero_Darius hero = FindHero(animation);
        return hero != null && hero.NativeModelBridge != null;
    }

    public static bool IsNative(EntityVisual visual)
    {
        Hero_Darius hero = FindHero(visual);
        return hero != null && hero.NativeModelBridge != null;
    }

    public static bool IsOfficialFreshModel(Hero_Darius hero)
    {
        if (hero == null) return false;
        try
        {
            EntityVisual visual = hero.Visual;
            EntityModel model = visual != null ? visual.model : null;
            if (model != null && model.GetComponent<DariusOfficialEntityModelMarker>() != null) return true;
            return hero.GetComponentInChildren<DariusOfficialEntityModelMarker>(true) != null;
        }
        catch
        {
            return false;
        }
    }

    internal static Hero_Darius FindHero(UnityEngine.Component component)
    {
        if (component == null) return null;
        try
        {
            Hero_Darius hero = component.GetComponent<Hero_Darius>();
            return hero != null ? hero : component.GetComponentInParent<Hero_Darius>();
        }
        catch
        {
            return null;
        }
    }
}

// Legacy compatibility only. The raw runtime-GLB model is missing parts of the stock EntityModel
// hierarchy and historically triggered one benign tail NRE. Never suppress that exception when a
// native Unity model is active: native mode is expected to satisfy the real game contract.
[HarmonyPatch]
internal static class DariusEntityVisualLoadModelFinalizerPatch
{
    private static MethodBase TargetMethod()
    {
        return AccessTools.Method(typeof(EntityVisual), "LoadModelLocal", new[] { typeof(EntityModel) });
    }

    private static Exception Finalizer(EntityVisual __instance, Exception __exception)
    {
        if (__exception == null || __instance == null) return __exception;
        if (!(__exception is NullReferenceException)) return __exception;

        Hero_Darius hero = DariusNativeAnimationMode.FindHero(__instance);
        if (hero == null || hero.NativeModelBridge != null ||
            DariusNativeAnimationMode.IsOfficialFreshModel(hero)) return __exception;

        DariusLog.DebugInfoThrottled("MODEL-NATIVE-GUARD", "legacy-load-model-tail",
            "Suppressed stock EntityVisual.LoadModelLocal tail NullReference for legacy GLB fallback.", 20.0);
        return null;
    }
}

// Native models keep stock ReplaceAnimationLocal available for generic locomotion/status changes.
// Only the legacy GLB fallback lacks the stock model contract and therefore still skips replacement.
[HarmonyPatch]
internal static class DariusEntityAnimationReplaceAnimationLocalPatch
{
    private static MethodBase TargetMethod()
    {
        return typeof(EntityAnimation).GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .FirstOrDefault(m => m.Name == "ReplaceAnimationLocal" && m.GetParameters().Length == 2);
    }

    private static bool Prefix(EntityAnimation __instance)
    {
        Hero_Darius hero = DariusNativeAnimationMode.FindHero(__instance);
        if (hero == null || hero.NativeModelBridge != null ||
            DariusNativeAnimationMode.IsOfficialFreshModel(hero)) return true;

        DariusLog.DebugInfoThrottled("ANIM-NATIVE-GUARD", "legacy-replace-local",
            "Skipped stock ReplaceAnimationLocal only because Hero_Darius is using the legacy GLB fallback.", 20.0);
        return false;
    }
}

// Main deliberately makes Darius' own presentation hooks authoritative for ability clips. Keep the
// same single-owner contract in native mode: stock logic/state still runs, but the receiver-side RPC
// must not also drive the same Q/W/E/R/basic-attack presentation on the Animator.
[HarmonyPatch]
internal static class DariusEntityAnimationAbilityRpcPatch
{
    private static MethodBase TargetMethod()
    {
        return typeof(EntityAnimation).GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .FirstOrDefault(m => m.Name.StartsWith("UserCode_RpcPlayAbilityAnimation", StringComparison.Ordinal) &&
                                 m.GetParameters().Length == 3);
    }

    private static bool Prefix(EntityAnimation __instance)
    {
        Hero_Darius hero = DariusNativeAnimationMode.FindHero(__instance);
        if (hero == null || DariusNativeAnimationMode.IsOfficialFreshModel(hero)) return true;

        DariusLog.DebugInfoThrottled("ANIM-NATIVE-GUARD", "darius-ability-rpc",
            "Skipped stock ability-animation RPC for Hero_Darius; Darius presentation hooks own ability clips.", 20.0);
        return false;
    }
}
