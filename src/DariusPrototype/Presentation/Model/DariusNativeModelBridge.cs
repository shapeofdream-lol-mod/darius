using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

// Thin presentation bridge for Unity-native prefabs. Shape of Dreams EntityAnimation owns
// locomotion/death; this bridge only dispatches Darius-specific action clips and presentation props.
public sealed partial class DariusNativeModelBridge : MonoBehaviour
{
    private DariusSkinModelBinding _binding;
    private GameObject _modelRoot;
    private Animator _animator;
    private EntityModel _entityModel;
    private Hero _hero;
    private EntityAnimation _entityAnimation;
    private bool _entityAnimationModelSetup;
    private readonly Dictionary<string, AnimationClip> _clips =
        new Dictionary<string, AnimationClip>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Transform> _anchors =
        new Dictionary<string, Transform>(StringComparer.OrdinalIgnoreCase);
    private Renderer[] _godKingWolfRenderers;
    private Coroutine _sequence;

    public bool IsReady { get { return _modelRoot != null && _animator != null; } }
    public bool IsGodKingSkin { get { return _binding != null && _binding.isGodKingSkin; } }
    public string VariantKey
    {
        get { return _binding != null && !string.IsNullOrEmpty(_binding.variantKey) ? _binding.variantKey : "Classic"; }
    }

    internal void Initialize(GameObject modelRoot, DariusSkinModelBinding binding)
    {
        EndAnimatorAction();
        _binding = binding;
        _modelRoot = modelRoot;
        _entityModel = GetComponent<EntityModel>();
        _hero = null;
        _entityAnimation = null;
        _entityAnimationModelSetup = false;
        _animator = modelRoot != null ? modelRoot.GetComponentInChildren<Animator>(true) : null;
        if (_animator == null)
            throw new InvalidDataException("Native Darius prefab has no Animator: " + VariantKey);

        _animator.applyRootMotion = false;
        _animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
        CacheClips();
        CacheAnchors();
        ConfigureEntityModel();
        DariusRuntimePerformance.OptimizeSkinnedRenderers(modelRoot, true);

        if (GetComponentInParent<Hero>() == null)
            PlayState(IdleClipName, 0f, 1f);
    }

    public void BindHero(Hero hero)
    {
        if (hero == null)
        {
            StopSequence();
            _hero = null;
            _entityAnimation = null;
            _entityAnimationModelSetup = false;
            SyncAnimatorLease();
            return;
        }

        if (!ReferenceEquals(_hero, hero))
        {
            StopSequence();
            _hero = hero;
            _entityAnimation = null;
            _entityAnimationModelSetup = false;
        }

        DariusVoiceRuntime.Ensure(_hero);
        EntityAnimation animation = _hero.GetComponent<EntityAnimation>();
        if (!ReferenceEquals(_entityAnimation, animation))
        {
            _entityAnimation = animation;
            _entityAnimationModelSetup = false;
            SyncAnimatorLease();
        }
        if (_entityAnimation == null || _entityAnimationModelSetup) return;

        try
        {
            _entityAnimation.SetupModel();
            _entityAnimationModelSetup = true;
            DariusLog.Info("NATIVE-MODEL", "EntityAnimation.SetupModel completed skin=" + VariantKey +
                " animator=" + (_entityAnimation.animator != null));
        }
        catch (Exception e)
        {
            DariusLog.Exception("NATIVE-MODEL", e,
                "EntityAnimation.SetupModel failed skin=" + VariantKey + "; Unity Animator remains usable");
        }
    }

    private string IdleClipName { get { return FirstExisting(_binding != null ? _binding.idleClip : null, "Idle1_Base", "Idle1"); } }
    private string RunClipName { get { return FirstExisting(_binding != null ? _binding.runClip : null, "Run_Normal", "Run"); } }
    private string DeathClipName { get { return FirstExisting(_binding != null ? _binding.deathClip : null, "Death"); } }
    private string Attack1ClipName { get { return FirstExisting(_binding != null ? _binding.attack1Clip : null, "Attack1"); } }
    private string Attack2ClipName { get { return FirstExisting(_binding != null ? _binding.attack2Clip : null, "Attack2"); } }
    private string CritClipName { get { return FirstExisting(_binding != null ? _binding.critClip : null, "Crit"); } }

    private string FirstExisting(params string[] candidates)
    {
        for (int i = 0; i < candidates.Length; i++)
            if (!string.IsNullOrEmpty(candidates[i]) && _clips.ContainsKey(candidates[i])) return candidates[i];
        return candidates.Length > 0 ? candidates[0] : null;
    }
}
