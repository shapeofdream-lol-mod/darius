using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

// Native Unity model path. The bundle is built offline from the local Riot-derived model pack.
// Runtime work is intentionally boring: load a prefab, let Unity Animator evaluate animation, and
// expose the resulting EntityModel/renderer/anchor contract to Shape of Dreams.
internal static class DariusNativeModelAssets
{
    public const string BundleFileName = "darius_models.bundle";
    private static AssetBundle _bundle;
    private static bool _loadAttempted;
    private static string[] _assetNames;
    private static readonly Dictionary<string, GameObject> Prefabs = new Dictionary<string, GameObject>(StringComparer.OrdinalIgnoreCase);

    public static bool TryActivate(DariusTravelerModelInstance legacy)
    {
        if (legacy == null) return false;
        DariusNativeModelBridge existing = legacy.GetComponent<DariusNativeModelBridge>();
        if (existing != null && existing.IsReady) return true;

        DariusSkinModelBinding binding = legacy.GetComponent<DariusSkinModelBinding>();
        if (binding == null || string.IsNullOrEmpty(binding.variantKey)) return false;
        GameObject prefab = GetPrefab(binding.variantKey);
        if (prefab == null) return false;

        try
        {
            RemoveStaleNativeRoots(legacy.transform);
            GameObject instance = UnityEngine.Object.Instantiate(prefab, legacy.transform, false);
            instance.name = "Darius_Native_Model";
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;

            DariusNativeModelBridge bridge = existing != null ? existing : legacy.gameObject.AddComponent<DariusNativeModelBridge>();
            bridge.Initialize(instance, binding);
            legacy.enabled = false;
            DariusLog.Info("NATIVE-MODEL", "Activated Unity AssetBundle model skin=" + binding.variantKey +
                " prefab=" + prefab.name + "; legacy GLB interpreter disabled.");
            return true;
        }
        catch (Exception e)
        {
            DariusLog.Exception("NATIVE-MODEL", e, "Native model activation failed for skin=" + binding.variantKey + "; legacy GLB fallback remains available");
            return false;
        }
    }

    private static GameObject GetPrefab(string variantKey)
    {
        GameObject cached;
        if (Prefabs.TryGetValue(variantKey, out cached)) return cached;
        if (!EnsureBundle()) return null;

        string suffix = "/darius_" + variantKey.Replace(" ", string.Empty).ToLowerInvariant() + ".prefab";
        string assetName = null;
        for (int i = 0; i < _assetNames.Length; i++)
        {
            string candidate = _assetNames[i];
            if (candidate != null && candidate.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                assetName = candidate;
                break;
            }
        }
        if (assetName == null)
        {
            DariusLog.Error("NATIVE-MODEL", "AssetBundle has no prefab suffix=" + suffix);
            Prefabs[variantKey] = null;
            return null;
        }

        GameObject prefab = _bundle.LoadAsset<GameObject>(assetName);
        if (prefab == null)
            DariusLog.Error("NATIVE-MODEL", "AssetBundle prefab load returned null asset=" + assetName);
        Prefabs[variantKey] = prefab;
        return prefab;
    }

    private static bool EnsureBundle()
    {
        if (_bundle != null) return true;
        if (_loadAttempted) return false;
        _loadAttempted = true;

        string root = DariusMedia.Root;
        string path = !string.IsNullOrEmpty(root) ? Path.Combine(root, "assets", "models", BundleFileName) : null;
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            DariusLog.Info("NATIVE-MODEL", "Native model bundle not present; using optimized legacy GLB fallback. expected=" + (path ?? "<null>"));
            return false;
        }

        try
        {
            _bundle = AssetBundle.LoadFromFile(path);
            if (_bundle == null)
            {
                DariusLog.Error("NATIVE-MODEL", "AssetBundle.LoadFromFile returned null path=" + path);
                return false;
            }
            _assetNames = _bundle.GetAllAssetNames() ?? Array.Empty<string>();
            DariusLog.Info("NATIVE-MODEL", "Loaded native Unity model bundle assets=" + _assetNames.Length + " path=" + path);
            return true;
        }
        catch (Exception e)
        {
            _bundle = null;
            DariusLog.Exception("NATIVE-MODEL", e, "Failed loading Unity model AssetBundle path=" + path);
            return false;
        }
    }

    public static void Unload()
    {
        Prefabs.Clear();
        _assetNames = null;
        _loadAttempted = false;
        if (_bundle != null)
        {
            try { _bundle.Unload(false); } catch { }
            _bundle = null;
        }
    }

    private static void RemoveStaleNativeRoots(Transform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            Transform child = parent.GetChild(i);
            if (child != null && string.Equals(child.name, "Darius_Native_Model", StringComparison.Ordinal))
            {
                try { UnityEngine.Object.DestroyImmediate(child.gameObject); }
                catch { UnityEngine.Object.Destroy(child.gameObject); }
            }
        }
    }
}

// Thin presentation bridge for Unity-native prefabs. It never samples bones itself.
public sealed class DariusNativeModelBridge : MonoBehaviour
{
    private DariusSkinModelBinding _binding;
    private GameObject _modelRoot;
    private Animator _animator;
    private EntityModel _entityModel;
    private Hero _hero;
    private EntityAnimation _entityAnimation;
    private readonly Dictionary<string, AnimationClip> _clips = new Dictionary<string, AnimationClip>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Transform> _anchors = new Dictionary<string, Transform>(StringComparer.OrdinalIgnoreCase);
    private Renderer[] _godKingWolfRenderers;
    private Coroutine _sequence;

    public bool IsReady { get { return _modelRoot != null && _animator != null; } }
    public bool IsGodKingSkin { get { return _binding != null && _binding.isGodKingSkin; } }
    public string VariantKey { get { return _binding != null && !string.IsNullOrEmpty(_binding.variantKey) ? _binding.variantKey : "Classic"; } }

    internal void Initialize(GameObject modelRoot, DariusSkinModelBinding binding)
    {
        _binding = binding;
        _modelRoot = modelRoot;
        _entityModel = GetComponent<EntityModel>();
        _animator = modelRoot != null ? modelRoot.GetComponentInChildren<Animator>(true) : null;
        if (_animator == null) throw new InvalidDataException("Native Darius prefab has no Animator: " + VariantKey);

        _animator.applyRootMotion = false;
        _animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
        CacheClips();
        CacheAnchors();
        ConfigureEntityModel();
        DariusRuntimePerformance.OptimizeSkinnedRenderers(modelRoot, true);

        Hero hostHero = GetComponentInParent<Hero>();
        if (hostHero != null) BindHero(hostHero);
        else PlayState(IdleClipName, 0f, 1f);
    }

    public void BindHero(Hero hero)
    {
        _hero = hero;
        if (_hero == null) return;
        DariusVoiceRuntime.Ensure(_hero);
        _entityAnimation = _hero.GetComponent<EntityAnimation>();
        if (_entityAnimation != null)
        {
            try
            {
                _entityAnimation.SetupModel();
                DariusLog.Info("NATIVE-MODEL", "EntityAnimation.SetupModel completed skin=" + VariantKey +
                    " animator=" + (_entityAnimation.animator != null));
            }
            catch (Exception e)
            {
                DariusLog.Exception("NATIVE-MODEL", e, "EntityAnimation.SetupModel failed skin=" + VariantKey + "; Unity Animator remains usable");
            }
        }
    }

    private string IdleClipName { get { return FirstExisting(_binding != null ? _binding.idleClip : null, "Idle1_Base", "Idle1"); } }
    private string RunClipName { get { return FirstExisting(_binding != null ? _binding.runClip : null, "Run_Normal", "Run"); } }
    private string DeathClipName { get { return FirstExisting(_binding != null ? _binding.deathClip : null, "Death"); } }
    private string Attack1ClipName { get { return FirstExisting(_binding != null ? _binding.attack1Clip : null, "Attack1"); } }
    private string Attack2ClipName { get { return FirstExisting(_binding != null ? _binding.attack2Clip : null, "Attack2"); } }
    private string CritClipName { get { return FirstExisting(_binding != null ? _binding.critClip : null, "Crit"); } }

    private void CacheClips()
    {
        _clips.Clear();
        RuntimeAnimatorController controller = _animator.runtimeAnimatorController;
        AnimationClip[] clips = controller != null ? controller.animationClips : Array.Empty<AnimationClip>();
        for (int i = 0; i < clips.Length; i++)
        {
            AnimationClip clip = clips[i];
            if (clip != null && !_clips.ContainsKey(clip.name)) _clips.Add(clip.name, clip);
        }
        if (_clips.Count == 0)
            throw new InvalidDataException("Native Darius AnimatorController exposes no AnimationClips: " + VariantKey);
    }

    private void CacheAnchors()
    {
        _anchors.Clear();
        if (_modelRoot == null) return;
        Transform[] all = _modelRoot.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++)
        {
            Transform t = all[i];
            if (t != null && !string.IsNullOrEmpty(t.name) && !_anchors.ContainsKey(t.name))
                _anchors.Add(t.name, t);
        }
    }

    private void ConfigureEntityModel()
    {
        if (_entityModel == null || _modelRoot == null) return;
        Renderer[] allRenderers = _modelRoot.GetComponentsInChildren<Renderer>(true);
        List<Renderer> visibleRenderers = new List<Renderer>(allRenderers.Length);
        List<Renderer> godKingWolfRenderers = IsGodKingSkin ? new List<Renderer>() : null;
        for (int i = 0; i < allRenderers.Length; i++)
        {
            Renderer renderer = allRenderers[i];
            if (renderer == null) continue;
            if (renderer is SkinnedMeshRenderer)
                ((SkinnedMeshRenderer)renderer).updateWhenOffscreen = false;
            if (IsGodKingSkin && RendererContainsMaterial(renderer, "Wolf_Mat"))
                godKingWolfRenderers.Add(renderer);
            if (!IsHiddenPresentationObject(renderer.gameObject)) visibleRenderers.Add(renderer);
        }
        _godKingWolfRenderers = godKingWolfRenderers != null ? godKingWolfRenderers.ToArray() : null;
        _entityModel.bodyRenderers = visibleRenderers.ToArray();

        AnimationClip run = FindClip(RunClipName);
        if (run != null) _entityModel.runForwardClip = run;
        AssignClipWithSpeed(_entityModel, "idle", FindClip(IdleClipName), 1f);
        AssignClipWithSpeed(_entityModel, "lobby", FindClip(IdleClipName), 1f);
        AssignClipWithSpeed(_entityModel, "death", FindClip(DeathClipName), 1f);

        Transform health = GetAnchor("C_BuffBone_Glb_Chest_Loc") ?? GetAnchor("Chest") ?? GetAnchor("Spine");
        Transform weapon = GetAnchor("BuffBone_Glb_Weapon_1") ?? GetAnchor("Weapon") ?? GetAnchor("R_Hand");
        if (health != null) _entityModel.healthBarPosition = health;
        if (weapon != null) _entityModel.weapon = weapon;
    }

    private static void AssignClipWithSpeed(EntityModel model, string fieldName, AnimationClip clip, float speed)
    {
        if (model == null || clip == null) return;
        try
        {
            FieldInfo ownerField = typeof(EntityModel).GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (ownerField == null) return;
            object boxed = ownerField.GetValue(model);
            if (boxed == null) boxed = Activator.CreateInstance(ownerField.FieldType);
            FieldInfo[] fields = ownerField.FieldType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            for (int i = 0; i < fields.Length; i++)
            {
                FieldInfo field = fields[i];
                if (field.FieldType == typeof(AnimationClip)) field.SetValue(boxed, clip);
                else if (field.FieldType == typeof(float) && field.Name.IndexOf("speed", StringComparison.OrdinalIgnoreCase) >= 0) field.SetValue(boxed, speed);
            }
            ownerField.SetValue(model, boxed);
        }
        catch (Exception e)
        {
            DariusLog.Exception("NATIVE-MODEL", e, "Failed binding EntityModel." + fieldName + " to clip=" + clip.name);
        }
    }

    private string FirstExisting(params string[] candidates)
    {
        for (int i = 0; i < candidates.Length; i++)
            if (!string.IsNullOrEmpty(candidates[i]) && _clips.ContainsKey(candidates[i])) return candidates[i];
        return candidates.Length > 0 ? candidates[0] : null;
    }

    private AnimationClip FindClip(string name)
    {
        AnimationClip clip;
        return !string.IsNullOrEmpty(name) && _clips.TryGetValue(name, out clip) ? clip : null;
    }

    private float ClipLength(string name, float fallback)
    {
        AnimationClip clip = FindClip(name);
        return clip != null ? Mathf.Max(0.01f, clip.length) : fallback;
    }

    private bool PlayState(string name, float fade, float speed)
    {
        if (_animator == null || string.IsNullOrEmpty(name)) return false;
        int hash = Animator.StringToHash(name);
        if (!_animator.HasState(0, hash))
        {
            DariusLog.Warn("NATIVE-ANIM", "Animator state missing skin=" + VariantKey + " state=" + name);
            return false;
        }
        _animator.speed = Mathf.Max(0.05f, speed);
        if (fade > 0.001f) _animator.CrossFadeInFixedTime(hash, fade, 0, 0f);
        else _animator.Play(hash, 0, 0f);
        return true;
    }

    public void PlayQ(bool instant)
    {
        StopSequence();
        _sequence = StartCoroutine(PlayQSequence(instant));
    }

    private IEnumerator PlayQSequence(bool instant)
    {
        string action = FirstExisting(_binding != null ? _binding.qClip : null, "Spell1");
        string intro = _binding != null ? _binding.qIntroClip : null;
        if (!instant && !string.IsNullOrEmpty(intro) && FindClip(intro) != null)
        {
            PlayState(intro, 0.04f, 1f);
            yield return new WaitForSeconds(Mathf.Max(0.05f, Ai_Darius_Decimate.Windup - 0.08f));
        }
        float raw = ClipLength(action, 0.53f);
        float duration = instant ? Mathf.Clamp(raw, 0.42f, 0.70f) : raw;
        PlayState(action, 0.04f, raw / Mathf.Max(0.05f, duration));
        yield return new WaitForSeconds(duration);
        _animator.speed = 1f;
        _sequence = null;
    }

    public void PlayWAttack(Vector3 direction)
    {
        PlayAction(FirstExisting(_binding != null ? _binding.wClip : null, "Spell2"));
    }

    public void SetWArmed(bool armed)
    {
        if (!armed)
        {
            if (_entityAnimation != null) try { _entityAnimation.StopAbilityAnimation(); } catch { }
            return;
        }
        string armedState = FirstExisting("Spell2_Idle", _binding != null ? _binding.idleClip : null, IdleClipName);
        PlayState(armedState, 0.06f, 1f);
    }

    public void PlayOneShot(string name)
    {
        string resolved = name;
        if (string.Equals(name, "Spell3", StringComparison.Ordinal)) resolved = FirstExisting(_binding != null ? _binding.eClip : null, "Spell3");
        else if (string.Equals(name, "Spell4", StringComparison.Ordinal)) resolved = FirstExisting(_binding != null ? _binding.rClip : null, "Spell4");
        PlayAction(resolved);
    }

    public void PlayAttack(bool alternate, bool critical, Vector3 direction)
    {
        PlayAction(critical ? CritClipName : (alternate ? Attack2ClipName : Attack1ClipName));
    }

    private void PlayAction(string state)
    {
        StopSequence();
        if (IsGodKingSkin && string.Equals(state, FirstExisting(_binding != null ? _binding.rClip : null, "Spell4"), StringComparison.Ordinal))
            _sequence = StartCoroutine(PlayGodKingR(state));
        else
            PlayState(state, 0.05f, 1f);
    }

    private IEnumerator PlayGodKingR(string state)
    {
        SetGodKingWolfVisible(true);
        PlayState(state, 0.05f, 1f);
        yield return new WaitForSeconds(ClipLength(state, 0.6f));
        SetGodKingWolfVisible(false);
        _sequence = null;
    }

    public Transform GetAnchor(string name)
    {
        Transform result;
        return !string.IsNullOrEmpty(name) && _anchors.TryGetValue(name, out result) ? result : null;
    }

    public GameObject CreateSubmeshOverlay(uint sourceMaterialHash, Material overlayMaterial)
    {
        if (_modelRoot == null || overlayMaterial == null) return null;
        SkinnedMeshRenderer[] renderers = _modelRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        for (int r = 0; r < renderers.Length; r++)
        {
            SkinnedMeshRenderer source = renderers[r];
            if (source == null || source.sharedMesh == null || IsHiddenPresentationObject(source.gameObject)) continue;
            Material[] sourceMaterials = source.sharedMaterials;
            for (int i = 0; i < sourceMaterials.Length && i < source.sharedMesh.subMeshCount; i++)
            {
                Material material = sourceMaterials[i];
                if (material == null || RiotStringHash(StripRuntimeSuffix(material.name)) != sourceMaterialHash) continue;
                return CreateOverlayRenderer(source, overlayMaterial, i, "Submesh_" + i);
            }
        }
        return null;
    }

    public GameObject CreateFullMeshOverlay(Material overlayMaterial, string label)
    {
        if (_modelRoot == null || overlayMaterial == null) return null;
        SkinnedMeshRenderer[] renderers = _modelRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            SkinnedMeshRenderer source = renderers[i];
            if (source == null || source.sharedMesh == null || IsHiddenPresentationObject(source.gameObject)) continue;
            return CreateOverlayRenderer(source, overlayMaterial, -1, string.IsNullOrEmpty(label) ? "Full" : label);
        }
        return null;
    }

    private static GameObject CreateOverlayRenderer(SkinnedMeshRenderer source, Material material, int selectedSubmesh, string label)
    {
        GameObject go = new GameObject("Darius_NativeOverlay_" + label);
        go.transform.SetParent(source.transform, false);
        SkinnedMeshRenderer renderer = go.AddComponent<SkinnedMeshRenderer>();
        renderer.sharedMesh = source.sharedMesh;
        renderer.bones = source.bones;
        renderer.rootBone = source.rootBone;
        renderer.quality = source.quality;
        renderer.updateWhenOffscreen = false;
        int count = Mathf.Max(1, source.sharedMesh.subMeshCount);
        Material[] materials = new Material[count];
        if (selectedSubmesh < 0)
        {
            for (int i = 0; i < count; i++) materials[i] = material;
        }
        else
        {
            Material invisible = CreateInvisibleMaterial(material);
            for (int i = 0; i < count; i++) materials[i] = i == selectedSubmesh ? material : invisible;
            DariusLolVfxLinkedObjects links = go.AddComponent<DariusLolVfxLinkedObjects>();
            links.Add(invisible);
        }
        renderer.sharedMaterials = materials;
        return go;
    }

    private static Material CreateInvisibleMaterial(Material template)
    {
        Material material = new Material(template);
        material.name = "Darius_NativeOverlay_Invisible";
        Color clear = new Color(0f, 0f, 0f, 0f);
        if (material.HasProperty("_Color")) material.SetColor("_Color", clear);
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", clear);
        material.color = clear;
        return material;
    }

    public bool SetGodKingWolfVisible(bool visible)
    {
        if (!IsGodKingSkin || _godKingWolfRenderers == null) return false;
        bool changed = false;
        for (int i = 0; i < _godKingWolfRenderers.Length; i++)
        {
            Renderer renderer = _godKingWolfRenderers[i];
            if (renderer == null) continue;
            renderer.enabled = visible;
            changed = true;
        }
        return changed;
    }

    private static bool RendererContainsMaterial(Renderer renderer, string sourceName)
    {
        Material[] materials = renderer.sharedMaterials;
        for (int i = 0; i < materials.Length; i++)
        {
            Material material = materials[i];
            if (material != null && StripRuntimeSuffix(material.name).IndexOf(sourceName, StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
        }
        return false;
    }

    private static bool IsHiddenPresentationObject(GameObject go)
    {
        if (go == null) return false;
        string name = go.name ?? string.Empty;
        return name.IndexOf("hidden", StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("wolf", StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("throne", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static string StripRuntimeSuffix(string name)
    {
        if (string.IsNullOrEmpty(name)) return string.Empty;
        const string instance = " (Instance)";
        if (name.EndsWith(instance, StringComparison.Ordinal)) name = name.Substring(0, name.Length - instance.Length);
        const string runtime = "_Runtime";
        if (name.EndsWith(runtime, StringComparison.OrdinalIgnoreCase)) name = name.Substring(0, name.Length - runtime.Length);
        return name;
    }

    private static uint RiotStringHash(string text)
    {
        uint h = 2166136261u;
        if (text == null) return h;
        for (int i = 0; i < text.Length; i++)
        {
            char c = char.ToLowerInvariant(text[i]);
            h ^= (byte)c;
            h *= 16777619u;
        }
        return h;
    }

    private void StopSequence()
    {
        if (_sequence != null)
        {
            try { StopCoroutine(_sequence); } catch { }
            _sequence = null;
        }
        if (IsGodKingSkin) SetGodKingWolfVisible(false);
        if (_animator != null) _animator.speed = 1f;
    }

    private void OnDestroy()
    {
        StopSequence();
    }
}