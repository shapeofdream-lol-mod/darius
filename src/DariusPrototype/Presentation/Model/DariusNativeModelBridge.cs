using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

// Thin presentation bridge for Unity-native prefabs. Shape of Dreams EntityAnimation owns
// ordinary locomotion/death; this bridge owns Darius action presentation and W-armed presentation.
public sealed partial class DariusNativeModelBridge : MonoBehaviour
{
    private DariusSkinModelBinding _binding;
    private GameObject _modelRoot;
    private Animator _animator;
    private EntityModel _entityModel;
    private Hero _hero;
    private EntityAnimation _entityAnimation;
    private bool _entityAnimationModelSetup;
    private bool _setupFailed;
    private readonly Dictionary<string, AnimationClip> _clips =
        new Dictionary<string, AnimationClip>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Transform> _anchors =
        new Dictionary<string, Transform>(StringComparer.OrdinalIgnoreCase);
    private Renderer[] _godKingWolfRenderers;
    private Coroutine _sequence;

    public bool IsReady { get { return _modelRoot != null && _animator != null && !_setupFailed; } }
    public bool IsGodKingSkin { get { return _binding != null && _binding.isGodKingSkin; } }
    public string VariantKey
    {
        get { return _binding != null && !string.IsNullOrEmpty(_binding.variantKey) ? _binding.variantKey : "Classic"; }
    }

    internal void Initialize(GameObject modelRoot, DariusSkinModelBinding binding)
    {
        ResetWLocomotionOverrides();
        EndAnimatorAction();
        _binding = binding;
        _modelRoot = modelRoot;
        _entityModel = GetComponent<EntityModel>();
        _hero = null;
        _entityAnimation = null;
        _entityAnimationModelSetup = false;
        _setupFailed = false;
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
            ResetWLocomotionOverrides();
            StopSequence();
            _hero = null;
            _entityAnimation = null;
            _entityAnimationModelSetup = false;
            _setupFailed = false;
            SyncAnimatorLease();
            return;
        }

        if (!ReferenceEquals(_hero, hero))
        {
            ResetWLocomotionOverrides();
            StopSequence();
            _hero = hero;
            _entityAnimation = null;
            _entityAnimationModelSetup = false;
            _setupFailed = false;
        }

        DariusVoiceRuntime.Ensure(_hero);
        EntityAnimation animation = _hero.GetComponent<EntityAnimation>();
        if (!ReferenceEquals(_entityAnimation, animation))
        {
            _entityAnimation = animation;
            _entityAnimationModelSetup = false;
            _setupFailed = false;
            SyncAnimatorLease();
        }
        if (_entityAnimationModelSetup) return;
        if (_entityAnimation == null)
        {
            _setupFailed = true;
            if (_modelRoot != null) _modelRoot.SetActive(false);
            DariusLog.Error("NATIVE-MODEL", "Hero has no EntityAnimation; native gameplay presentation disabled skin=" + VariantKey);
            return;
        }

        try
        {
            _entityAnimation.SetupModel();
            if (_entityAnimation.animator == null)
                throw new InvalidOperationException("EntityAnimation.SetupModel produced no animator.");
            _entityAnimationModelSetup = true;
            _setupFailed = false;
            if (_wArmedPresentation)
            {
                bool moving = IsRunningState();
                BeginWLocomotionTracking(moving);
                SyncAnimatorLease();
                PlayWLocomotionState(moving, true);
            }
            else if (IsGodKingSkin && FindClip("Idle_In") != null)
            {
                StopSequence();
                _sequence = StartCoroutine(PlayTimedAction("Idle_In", null));
            }
            DariusLog.Info("NATIVE-MODEL", "EntityAnimation.SetupModel completed skin=" + VariantKey + " animator=true");
        }
        catch (Exception e)
        {
            _setupFailed = true;
            _entityAnimationModelSetup = false;
            ResetWLocomotionOverrides();
            StopSequence();
            if (_modelRoot != null) _modelRoot.SetActive(false);
            DariusLog.Exception("NATIVE-MODEL", e,
                "EntityAnimation.SetupModel failed skin=" + VariantKey + "; native gameplay presentation disabled");
        }
    }

    private string IdleClipName { get { return _binding != null ? _binding.idleClip : null; } }
    private string RunClipName { get { return _binding != null ? _binding.runClip : null; } }
    private string DeathClipName { get { return _binding != null ? _binding.deathClip : null; } }
    private string Attack1ClipName { get { return _binding != null ? _binding.attack1Clip : null; } }
    private string Attack2ClipName { get { return _binding != null ? _binding.attack2Clip : null; } }
    private string CritClipName { get { return _binding != null ? _binding.critClip : null; } }
}
