using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed partial class DariusTravelerModelInstance : MonoBehaviour
{
    private const float NativeModelScale = 0.00921f;

    private const float NativeModelYOffset = 0.04f;

    private const float FastRunThreshold = 6.0f;

    private const float HomeguardThreshold = 7.6f;

    private DariusGlbRuntimeModel _model;

    private DariusSkinModelBinding _binding;

    private Hero _hero;

    private Vector3 _lastPosition;

    private float _lastSample;

    private bool _isMoving;

    private bool _wasMoving;

    private float _sampledSpeed;

    private Vector3 _recentMovementDirection;

    private float _recentMovementDirectionAt = -999f;

    private Vector3 _lastFacing = Vector3.forward;

    private float _lastTurnAt = -999f;

    private bool _wArmed;

    private bool _wSwingSequence;

    private bool _deathLatched;

    private bool _godKing;

    private bool _wIdleInAlt;

    private bool _wDeactivateAlt;

    private Coroutine _animationSequence;

    private Transform _facingPivot;

    private bool _attackFacingActive;

    private Vector3 _attackFacingDirection = Vector3.forward;

    private int _attackFacingSerial;

    private float _nextFacingDiagAt;

    private float _nextIdleVariantAt;

    private bool _lobbyPreview;

    private float _nextPreviewShowcaseAt;

    private int _previewShowcaseIndex;

    public bool IsGodKingSkin { get { return _godKing; } }

    public string VariantKey { get { return _binding != null ? _binding.variantKey : (_godKing ? "GodKing" : "Classic"); } }

    private string BoundClip(string preferred, string fallback)
    {
        if (_model != null && !string.IsNullOrEmpty(preferred) && _model.HasClip(preferred)) return preferred;
        if (_model != null && !string.IsNullOrEmpty(fallback) && _model.HasClip(fallback)) return fallback;
        return !string.IsNullOrEmpty(preferred) ? preferred : fallback;
    }

    private string IdleClip { get { return BoundClip(_binding != null ? _binding.idleClip : null, _godKing ? "Idle1_Base" : "Idle1"); } }

    private string IdleVariantClip { get { return BoundClip(_binding != null ? _binding.idleVariantClip : null, IdleClip); } }

    private string RunClip { get { return BoundClip(_binding != null ? _binding.runClip : null, _godKing ? "Run_Normal" : "Run"); } }

    private string DeathClip { get { return BoundClip(_binding != null ? _binding.deathClip : null, "Death"); } }

    private string Attack1Clip { get { return BoundClip(_binding != null ? _binding.attack1Clip : null, "Attack1"); } }

    private string Attack2Clip { get { return BoundClip(_binding != null ? _binding.attack2Clip : null, "Attack2"); } }

    private string CritClip { get { return BoundClip(_binding != null ? _binding.critClip : null, "Crit"); } }

    private string WClip { get { return BoundClip(_binding != null ? _binding.wClip : null, "Spell2"); } }

    private string EClip { get { return BoundClip(_binding != null ? _binding.eClip : null, "Spell3"); } }

    private string RClip { get { return BoundClip(_binding != null ? _binding.rClip : null, "Spell4"); } }

    private string WIdleClip { get { return BoundClip("Spell2_Idle", IdleClip); } }

    private string WRunClip { get { return BoundClip("Spell2_Run", RunClip); } }
}
