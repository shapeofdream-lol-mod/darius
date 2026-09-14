using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

// Thin presentation bridge for Unity-native prefabs. It never samples bones itself.
public sealed partial class DariusNativeModelBridge : MonoBehaviour
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
}