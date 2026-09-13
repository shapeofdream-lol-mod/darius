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

public sealed class DariusSkinModelBinding : MonoBehaviour
{
    public string skinResourceName;
    public string modelFile;
    public string displayName;
    public string variantKey;
    public bool isGodKingSkin;
    public float modelScale;
    public float modelYOffset;
    public float modelYaw;
    public int expectedPrimitives, expectedBones, expectedAnimations;
    public string idleClip, idleVariantClip, runClip, deathClip;
    public string attack1Clip, attack2Clip, critClip;
    public string qIntroClip, qClip, wClip, eClip, rClip;
    public string attack1ToIdleClip, attack2ToIdleClip, critToIdleClip;
    public string eToRunClip, eToIdleClip, rToRunClip;
}

public sealed class DariusTravelerModelInstance : MonoBehaviour
{
    private const float NativeModelScale = 0.00921f;
    private const float NativeModelYOffset = 0.04f;
    private const float FastRunThreshold = 6.0f;

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

    private void ValidateLoadedSkinProfile(DariusSkinModelBinding binding)
    {
        if (_model == null || binding == null) throw new InvalidOperationException("Darius skin profile validation received null model/binding.");
        if (binding.expectedPrimitives > 0 && _model.primitiveCount != binding.expectedPrimitives)
            throw new InvalidDataException("Skin " + binding.skinResourceName + " primitive mismatch expected=" + binding.expectedPrimitives + " actual=" + _model.primitiveCount);
        if (binding.expectedBones > 0 && _model.boneCount != binding.expectedBones)
            throw new InvalidDataException("Skin " + binding.skinResourceName + " bone mismatch expected=" + binding.expectedBones + " actual=" + _model.boneCount);
        if (binding.expectedAnimations > 0 && _model.animationCount != binding.expectedAnimations)
            throw new InvalidDataException("Skin " + binding.skinResourceName + " animation mismatch expected=" + binding.expectedAnimations + " actual=" + _model.animationCount);
        ValidateBoundClip("idleClip", binding.idleClip, true);
        ValidateBoundClip("runClip", binding.runClip, true);
        ValidateBoundClip("deathClip", binding.deathClip, true);
        ValidateBoundClip("attack1Clip", binding.attack1Clip, true);
        ValidateBoundClip("attack2Clip", binding.attack2Clip, true);
        ValidateBoundClip("critClip", binding.critClip, true);
        ValidateBoundClip("qIntroClip", binding.qIntroClip, true);
        ValidateBoundClip("qClip", binding.qClip, true);
        ValidateBoundClip("wClip", binding.wClip, true);
        ValidateBoundClip("eClip", binding.eClip, true);
        ValidateBoundClip("rClip", binding.rClip, true);
        ValidateBoundClip("attack1ToIdleClip", binding.attack1ToIdleClip, false);
        ValidateBoundClip("attack2ToIdleClip", binding.attack2ToIdleClip, false);
        ValidateBoundClip("critToIdleClip", binding.critToIdleClip, false);
        ValidateBoundClip("eToRunClip", binding.eToRunClip, false);
        ValidateBoundClip("eToIdleClip", binding.eToIdleClip, false);
        ValidateBoundClip("rToRunClip", binding.rToRunClip, false);
        DariusLog.Info("SKIN-PROFILE-ASSERT", "PASS skin=" + binding.skinResourceName + " profile=" + binding.variantKey +
            " primitives=" + _model.primitiveCount + " bones=" + _model.boneCount + " animations=" + _model.animationCount);
    }

    private void ValidateBoundClip(string slot, string clip, bool required)
    {
        if (string.IsNullOrEmpty(clip))
        {
            if (required) throw new InvalidDataException("Required skin animation binding is empty: " + slot);
            return;
        }
        if (!_model.HasClip(clip))
        {
            if (required) throw new InvalidDataException("Required skin animation is absent from GLB: " + slot + "=" + clip);
            DariusLog.Warn("SKIN-PROFILE-ASSERT", "Optional animation absent slot=" + slot + " clip=" + clip + " skin=" + (_binding != null ? _binding.variantKey : "<unknown>"));
        }
    }

    public void BindHero(Hero hero)
    {
        CancelAttackFacing("hero rebound");
        if (_facingPivot != null) _facingPivot.localRotation = Quaternion.identity;
        _hero = hero;
        if (_hero != null)
        {
            DariusVoiceRuntime.Ensure(_hero);
            _lastPosition = _hero.transform.position;
            _lastSample = Time.time;
            _isMoving = false;
            _wasMoving = false;
            _sampledSpeed = 0f;
            _recentMovementDirection = Vector3.zero;
            _recentMovementDirectionAt = -999f;
            _lastFacing = _hero.transform.forward;
            _lastFacing.y = 0f;
            if (_lastFacing.sqrMagnitude < 0.001f) _lastFacing = Vector3.forward;
            else _lastFacing.Normalize();
        }
    }

    private void OnEnable()
    {
        if (_model != null) return;
        try
        {
            Hero hostHero = GetComponentInParent<Hero>();
            _lobbyPreview = hostHero == null;
            DariusSkinModelBinding skinBinding = GetComponent<DariusSkinModelBinding>();
            if (skinBinding == null || string.IsNullOrEmpty(skinBinding.modelFile))
                throw new InvalidOperationException("Darius Skin resource has no explicit model binding: " + gameObject.name);
            _binding = skinBinding;
            _godKing = skinBinding.isGodKingSkin;
            string file = skinBinding.modelFile;
            DariusLog.Info("SKIN-MODEL-BIND", "Resolved skinResource=" + skinBinding.skinResourceName +
                " display=" + skinBinding.displayName + " modelFile=" + file + " profile=" + skinBinding.variantKey);
            string path = System.IO.Path.Combine(DariusMedia.Root, "assets", "models", file);
            _model = DariusGlbRuntimeModel.Load(gameObject, path);
            if (_model == null) throw new InvalidOperationException("GLB runtime loader returned null.");
            ValidateLoadedSkinProfile(skinBinding);

            float boundScale = skinBinding.modelScale > 0.00001f ? skinBinding.modelScale : NativeModelScale;
            float boundYOffset = Mathf.Abs(skinBinding.modelYOffset) > 0.00001f ? skinBinding.modelYOffset : NativeModelYOffset;

            GameObject facingGo = new GameObject("DariusVisualFacingPivot");
            _facingPivot = facingGo.transform;
            _facingPivot.SetParent(_model.root.transform.parent, false);
            _facingPivot.localPosition = Vector3.zero;
            _facingPivot.localRotation = Quaternion.identity;
            _facingPivot.localScale = Vector3.one;
            _model.root.transform.SetParent(_facingPivot, false);

            _model.root.transform.localPosition = new Vector3(0f, boundYOffset, 0f);
            _model.root.transform.localRotation = Quaternion.Euler(0f, _binding.modelYaw, 0f);
            DariusLog.Info("SKIN-FACING-CALIBRATION", "Applied authored root yaw=" + _binding.modelYaw.ToString("0.#") + " skin=" + _binding.variantKey + " model=" + file);
            _model.root.transform.localScale = Vector3.one * boundScale;

            // Complete the stock EntityModel renderer contract after the runtime GLB exists.
            // Older builds left bodyRenderers empty and then suppressed EntityVisual tail NREs;
            // populating the real renderers lets lobby/preview/highlight consumers see the same model.
            try
            {
                EntityModel entityModel = GetComponent<EntityModel>();
                if (entityModel != null)
                {
                    Renderer[] renderers = _model.GetEntityBodyRenderers();
                    entityModel.bodyRenderers = renderers ?? new Renderer[0];
                    DariusLog.Info("TRAVELER-MODEL", "EntityModel bodyRenderers rebound visibleOnly count=" + entityModel.bodyRenderers.Length +
                        " skin=" + VariantKey + "; authored-hidden animation props excluded from native hover/highlight.");
                }
            }
            catch (Exception rendererError)
            {
                DariusLog.Exception("TRAVELER-MODEL", rendererError, "Failed binding runtime GLB renderers into EntityModel contract");
            }

            BindHero(hostHero);
            _nextIdleVariantAt = Time.time + 7.5f;
            _nextPreviewShowcaseAt = Time.time + 24.0f;
            // The lobby display may recreate EntityModel several times while the user changes
            // selection/skin. Replaying an entry clip on every rebuild made the preview look as if
            // actions were constantly interrupting each other. Lobby starts from the stable idle;
            // the authored intro remains available in the actual in-game model.
            if (_lobbyPreview)
                _model.Play(IdleClip, true, true, 1f, 0f);
            else if (_godKing && _model.HasClip("Idle_In"))
                _animationSequence = StartCoroutine(PlayIntroSequence());
            else
                _model.Play(IdleClip, true, true);

            DariusLog.Info("TRAVELER-MODEL", "Darius EntityModel bridge loaded skin=" + VariantKey +
                " file=" + file + " verts=" + _model.vertexCount + " tris=" + _model.triangleCount +
                " bones=" + _model.boneCount + " animations=" + _model.animationCount +
                " preview=" + _lobbyPreview + " scale=" + _model.root.transform.localScale.x.ToString("0.#####") +
                " localPos=" + DariusLog.Vec(_model.root.transform.localPosition));
        }
        catch (Exception e)
        {
            DariusLog.Exception("TRAVELER-MODEL", e, "Failed to load Darius GLB skin model");
        }
    }

    private IEnumerator PlayIntroSequence()
    {
        _model.Play("Idle_In", false, true);
        yield return new WaitForSeconds(_model.GetClipLength("Idle_In", 0.5f));
        if (_model != null && !_deathLatched) _model.Play(IdleClip, true, true);
        _animationSequence = null;
    }

    private void Update()
    {
        if (_model == null) return;

        bool dead = IsHeroInDeathState();
        if (dead)
        {
            _model.SetLowerBodyLocomotion(null, false, 1f, 0f);
            if (!_deathLatched)
            {
                StopAnimationSequence();
                _wArmed = false;
                _isMoving = false;
                _deathLatched = true;
                _model.PlayAndHold(DeathClip, true);
                DariusLog.Info("SKIN-DEATH", "Hero_Darius death state entered -> Death clip latched. skin=" + VariantKey);
            }
            _model.Tick(Time.deltaTime);
            return;
        }

        if (_deathLatched)
        {
            _deathLatched = false;
            StopAnimationSequence();
            if (_hero != null)
            {
                _lastPosition = _hero.transform.position;
                _lastSample = Time.time;
            }
            // Respawn/Homeguard transition tracks are not needed for gameplay and some God-King
            // exports contain aggressive prop/root transforms. Resume from the stable idle instead.
            _model.PlayBase(IdleClip, true, true, 1f, 0.16f);
        }

        _model.Tick(Time.deltaTime);

        // Lobby is idle-only for now. Automatic showcases were firing while the previous action
        // was still settling and made the preview look permanently animated or malformed.
        if (_lobbyPreview)
        {
            if (_animationSequence == null && !_model.isPlayingFullBodyOneShot &&
                !string.Equals(_model.currentAnimation, IdleClip, StringComparison.Ordinal))
                _model.PlayBase(IdleClip, true, false, 1f, 0.12f);
            return;
        }

        _wasMoving = _isMoving;
        _isMoving = SampleMovement();
        UpdateActionLocomotionLayer();
        if (_model.isPlayingFullBodyOneShot || _animationSequence != null) return;

        string desired = ResolveLocomotionClip();
        if (!string.Equals(_model.currentAnimation, desired, StringComparison.Ordinal))
            _model.PlayBase(desired, true, false, 1f, 0.12f);
    }

    private void LateUpdate()
    {
        if (_facingPivot == null || _hero == null) return;

        Quaternion wantedLocal = Quaternion.identity;
        Vector3 desiredWorld = _attackFacingDirection;
        if (_attackFacingActive)
        {
            desiredWorld.y = 0f;
            if (desiredWorld.sqrMagnitude > 0.001f)
            {
                desiredWorld.Normalize();
                Transform parent = _facingPivot.parent;
                Vector3 localDirection = parent != null ? parent.InverseTransformDirection(desiredWorld) : desiredWorld;
                localDirection.y = 0f;
                if (localDirection.sqrMagnitude > 0.001f)
                    wantedLocal = Quaternion.LookRotation(localDirection.normalized, Vector3.up);
            }
        }

        float responsiveness = _attackFacingActive ? 48f : 22f;
        float t = 1f - Mathf.Exp(-responsiveness * Mathf.Max(Time.deltaTime, 0.0001f));
        _facingPivot.localRotation = Quaternion.Slerp(_facingPivot.localRotation, wantedLocal, t);

        if (_attackFacingActive && Time.unscaledTime >= _nextFacingDiagAt)
        {
            _nextFacingDiagAt = Time.unscaledTime + 0.18f;
            Vector3 heroForward = _hero.transform.forward; heroForward.y = 0f;
            Vector3 visualForward = _facingPivot.forward; visualForward.y = 0f;
            float dot = visualForward.sqrMagnitude > 0.001f && desiredWorld.sqrMagnitude > 0.001f
                ? Vector3.Dot(visualForward.normalized, desiredWorld.normalized) : -2f;
            DariusLog.DebugInfo("FACING-ARB", "attackSerial=" + _attackFacingSerial +
                " heroForward=" + DariusLog.Vec(heroForward) + " desired=" + DariusLog.Vec(desiredWorld) +
                " visualForward=" + DariusLog.Vec(visualForward) + " alignedDot=" + dot.ToString("0.###"));
        }
    }

    private void UpdateActionLocomotionLayer()
    {
        bool actionActive = _animationSequence != null || _model.isPlayingFullBodyOneShot;
        Vector3 movement = _recentMovementDirection; movement.y = 0f;
        string action = (_model.currentAnimation ?? string.Empty).ToLowerInvariant();
        bool excluded = action.Contains("dash") || action.Contains("death") || action.Contains("respawn") ||
            action.Contains("knock") || action.Contains("stun") || action.Contains("airborne");
        if (!_isMoving || !actionActive || excluded || movement.sqrMagnitude < 0.001f)
        {
            _model.SetLowerBodyLocomotion(null, false, 1f, 0f);
            return;
        }
        Vector3 visualForward = _model.root != null ? _model.root.transform.forward : _hero.transform.forward;
        visualForward.y = 0f;
        if (visualForward.sqrMagnitude < 0.001f) visualForward = _hero.transform.forward;
        float relativeYaw = Vector3.SignedAngle(visualForward.normalized, movement.normalized, Vector3.up);
        _model.SetLowerBodyLocomotion(ResolveLocomotionClip(), true, 1f, relativeYaw);
    }

    private int BeginAttackFacing(Vector3 direction)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.001f && _hero != null) direction = _hero.transform.forward;
        if (direction.sqrMagnitude < 0.001f) direction = Vector3.forward;
        direction.Normalize();
        _attackFacingDirection = direction;
        _attackFacingActive = true;
        int serial = ++_attackFacingSerial;
        DariusLog.DebugInfo("FACING-ARB", "begin attackSerial=" + serial + " desired=" + DariusLog.Vec(direction) +
            " moving=" + _isMoving);
        return serial;
    }

    private void EndAttackFacing(int serial, string reason)
    {
        if (serial != _attackFacingSerial || !_attackFacingActive) return;
        _attackFacingActive = false;
        DariusLog.DebugInfo("FACING-ARB", "end attackSerial=" + serial + " reason=" + reason);
    }

    private void CancelAttackFacing(string reason)
    {
        if (!_attackFacingActive) return;
        int serial = _attackFacingSerial;
        _attackFacingActive = false;
        DariusLog.DebugInfo("FACING-ARB", "cancel attackSerial=" + serial + " reason=" + reason);
    }

    private IEnumerator PlayRespawnSequence()
    {
        _model.Play("Respawn", false, true);
        yield return new WaitForSeconds(_model.GetClipLength("Respawn", 0.5f));
        if (_model != null && !_deathLatched) _model.Play(IdleClip, true, true);
        _animationSequence = null;
    }

    private IEnumerator PlayHomeguardTransition(string transition)
    {
        _model.Play(transition, false, true);
        yield return new WaitForSeconds(_model.GetClipLength(transition, 0.3f));
        if (_model != null && !_deathLatched) _model.PlayBase("Run_Homeguard", true, true);
        _animationSequence = null;
    }

    private IEnumerator PlayIdleVariant()
    {
        _model.Play(IdleVariantClip, false, true);
        yield return new WaitForSeconds(_model.GetClipLength(IdleVariantClip, 1f));
        if (_model != null && !_deathLatched) _model.Play(IdleClip, true, true);
        _animationSequence = null;
    }

    private IEnumerator PlayLobbyShowcase()
    {
        // Keep automatic lobby showcases conservative. Joke/Recall chains can depend on skin
        // props/VFX that are not present in the raw GLB, and long chained showcases made the lobby
        // appear permanently busy. Those clips remain packaged for explicit/manual use later.
        string[][] chains = _godKing
            ? new[]
            {
                new[] { "Taunt" },
                new[] { "Laugh" },
                new[] { "Dance_In", "Dance" },
                new[] { "Channel_Wndup", "Channel" },
            }
            : new[]
            {
                new[] { "Taunt" },
                new[] { "Laugh" },
                new[] { "Dance" },
                new[] { "Channel_Wndup", "Channel" },
            };

        string[] chain = chains[_previewShowcaseIndex++ % chains.Length];
        for (int i = 0; i < chain.Length; i++)
        {
            if (_model == null || !_lobbyPreview) break;
            string clip = chain[i];
            if (!_model.HasClip(clip)) continue;
            _model.Play(clip, false, true);
            yield return new WaitForSeconds(_model.GetClipLength(clip, 0.6f));
        }
        if (_model != null && _lobbyPreview) _model.PlayBase(IdleClip, true, true);
        _nextPreviewShowcaseAt = Time.time + 30.0f;
        _animationSequence = null;
    }

    private string ResolveLocomotionClip()
    {
        if (_wArmed) return _isMoving ? WRunClip : WIdleClip;
        return _isMoving ? RunClip : IdleClip;
    }

    private bool IsHeroInDeathState()
    {
        if (_hero == null) return false;
        try { if (_hero.IsNullInactiveDeadOrKnockedOut()) return true; } catch { }
        try { if (_hero.currentHealth <= 0.011f) return true; } catch { }
        return false;
    }

    private bool SampleMovement()
    {
        if (_hero == null) return false;
        float now = Time.time;
        float dt = Mathf.Max(0.001f, now - _lastSample);
        Vector3 p = _hero.transform.position;
        Vector3 delta = p - _lastPosition;
        delta.y = 0f;
        _sampledSpeed = delta.magnitude / dt;
        _lastPosition = p;
        _lastSample = now;
        bool moving = _sampledSpeed > 0.12f;
        if (moving && delta.sqrMagnitude > 0.000001f)
        {
            _recentMovementDirection = delta.normalized;
            _recentMovementDirectionAt = now;
        }
        return moving;
    }

    private void ObserveFacingTurn()
    {
        if (!_godKing || _hero == null || _isMoving || _wArmed || _animationSequence != null || _model.isPlayingOneShot) return;
        Vector3 current = _hero.transform.forward;
        current.y = 0f;
        if (current.sqrMagnitude < 0.001f) return;
        current.Normalize();
        float signed = Vector3.SignedAngle(_lastFacing, current, Vector3.up);
        _lastFacing = current;
        if (Mathf.Abs(signed) < 28f || Time.time - _lastTurnAt < 0.45f) return;
        _lastTurnAt = Time.time;
        string clip = signed > 0f ? "TurnR" : "TurnL";
        if (!_model.HasClip(clip)) return;
        _animationSequence = StartCoroutine(PlayTurnSequence(clip));
    }

    private IEnumerator PlayTurnSequence(string clip)
    {
        _model.Play(clip, false, true);
        yield return new WaitForSeconds(_model.GetClipLength(clip, 0.2f));
        if (_model != null && !_deathLatched && _model.HasClip("Turn0"))
        {
            _model.Play("Turn0", false, true);
            yield return new WaitForSeconds(_model.GetClipLength("Turn0", 0.18f));
        }
        if (_model != null && !_deathLatched) _model.Play(IdleClip, true, true);
        _animationSequence = null;
    }

    public bool TryGetRecentMovementDirection(out Vector3 direction)
    {
        direction = _recentMovementDirection;
        if (direction.sqrMagnitude <= 0.001f) return false;
        if (!_isMoving && Time.time - _recentMovementDirectionAt > 0.18f) return false;
        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.001f) return false;
        direction.Normalize();
        return true;
    }

    public Transform GetAnchor(string nodeName)
    {
        return _model != null ? _model.FindNode(nodeName) : null;
    }

    public GameObject CreateSubmeshOverlay(uint sourceMaterialHash, Material overlayMaterial)
    {
        return _model != null ? _model.CreateSubmeshOverlay(sourceMaterialHash, overlayMaterial) : null;
    }

    public GameObject CreateFullMeshOverlay(Material overlayMaterial, string label)
    {
        return _model != null ? _model.CreateFullMeshOverlay(overlayMaterial, label) : null;
    }

    public bool SetGodKingWolfVisible(bool visible)
    {
        if (!_godKing || _model == null) return false;
        bool ok = _model.SetMaterialVisible("Wolf_Mat", visible);
        DariusLog.DebugInfo("GODKING-WOLF", "Wolf_Mat visibility=" + visible + " ok=" + ok + " animation=" + (_model.currentAnimation ?? "<none>"));
        return ok;
    }

    public void PlayAttack(bool alternate, bool critical, Vector3 direction)
    {
        if (_model == null || _deathLatched) return;
        StopAnimationSequence();
        int facingSerial = BeginAttackFacing(direction);
        string clip = critical ? CritClip : (alternate ? Attack2Clip : Attack1Clip);

        // The exported LoL attacks are full-body authored poses. The previous implementation mixed
        // their Root/Pelvis/weapon tracks over a different Run leg pose, which is what produced
        // twisted limbs and the occasional God-King "ball" collapse. Keep SoD world movement,
        // but play one coherent full-body attack and blend back to locomotion.
        if (_isMoving)
        {
            _animationSequence = StartCoroutine(PlayActionToLocomotion(clip, 0.76f, 0.08f, facingSerial));
            DariusLog.DebugInfo("SKIN-ANIM", "Moving attack full-body " + clip + " -> " + ResolveLocomotionClip() +
                " skin=" + VariantKey);
            return;
        }

        string transition = critical ? (_binding != null ? _binding.critToIdleClip : null)
            : (alternate ? (_binding != null ? _binding.attack2ToIdleClip : null) : (_binding != null ? _binding.attack1ToIdleClip : null));
        if (!string.IsNullOrEmpty(transition) && !_model.HasClip(transition)) transition = null;
        _animationSequence = StartCoroutine(PlayActionToIdle(clip, transition, facingSerial));
    }

    public void PlayWAttack(Vector3 direction)
    {
        if (_model == null || _deathLatched) return;
        StopAnimationSequence();
        int facingSerial = BeginAttackFacing(direction);
        _wSwingSequence = true;
        _animationSequence = StartCoroutine(PlayWSequence(facingSerial));
    }

    private IEnumerator PlayActionToLocomotion(string action, float visualDuration)
    {
        return PlayActionToLocomotion(action, visualDuration, 0f);
    }

    private IEnumerator PlayActionToLocomotion(string action, float visualDuration, float tailTrim)
    {
        return PlayActionToLocomotion(action, visualDuration, tailTrim, 0);
    }

    private IEnumerator PlayActionToLocomotion(string action, float visualDuration, float tailTrim, int attackFacingSerial)
    {
        float raw = _model.GetClipLength(action, visualDuration);
        float duration = Mathf.Clamp(visualDuration, 0.45f, 0.95f);
        _model.Play(action, false, true, raw / duration, 0.08f);
        yield return new WaitForSeconds(Mathf.Max(0.08f, duration - Mathf.Max(0f, tailTrim)));
        if (_model != null && !_deathLatched) _model.PlayBase(ResolveLocomotionClip(), true, true, 1f, 0.12f);
        if (attackFacingSerial > 0) EndAttackFacing(attackFacingSerial, "attack animation complete");
        _animationSequence = null;
    }

    private IEnumerator PlayActionToIdle(string action, string transition, int attackFacingSerial = 0)
    {
        float rawAction = _model.GetClipLength(action, 0.8f);
        float actionDuration = 0.82f;
        _model.Play(action, false, true, rawAction / actionDuration, 0.08f);
        yield return new WaitForSeconds(actionDuration - 0.10f);
        if (_model == null || _deathLatched)
        {
            if (attackFacingSerial > 0) EndAttackFacing(attackFacingSerial, "model/death");
            _animationSequence = null;
            yield break;
        }
        if (!string.IsNullOrEmpty(transition) && _model.HasClip(transition))
        {
            float rawTransition = _model.GetClipLength(transition, 0.22f);
            const float transitionDuration = 0.22f;
            _model.Play(transition, false, true, rawTransition / transitionDuration, 0.06f);
            yield return new WaitForSeconds(0.08f);
        }
        if (_model != null && !_deathLatched) _model.PlayBase(IdleClip, true, true, 1f, 0.12f);
        if (attackFacingSerial > 0) EndAttackFacing(attackFacingSerial, "attack animation complete");
        _animationSequence = null;
    }

    public void PlayQ(bool instant = false)
    {
        if (_model == null || _deathLatched) return;
        StopAnimationSequence();
        _animationSequence = StartCoroutine(PlayQSequence(instant));
    }

    private IEnumerator PlayQSequence(bool instant)
    {
        const float swingLead = 0.08f;
        float windupToSwing = Mathf.Max(0.05f, Ai_Darius_Decimate.Windup - swingLead);
        string qClip = BoundClip(_binding != null ? _binding.qClip : null, "Spell1");
        string intro = _binding != null ? _binding.qIntroClip : null;
        if (!string.IsNullOrEmpty(intro) && !_model.HasClip(intro)) intro = null;
        DariusLog.DebugInfo("SKIN-ANIM", "Q graph profile=" + VariantKey + " instant=" + instant +
            " intro=" + (intro ?? "<none>") + " action=" + qClip);

        if (instant)
        {
            // No intro, no artificial 0.75 s wait. The attack action begins immediately so its
            // visual spin, Q hit snapshot and fast-forwarded Riot ring all share the same beat.
            float raw = _model.GetClipLength(qClip, 0.53f);
            if (raw <= 0.001f) raw = 0.53f;
            float duration = Mathf.Clamp(raw, 0.42f, 0.70f);
            _model.Play(qClip, false, true, raw / duration, 0.03f);
            try { if (_hero != null) DariusMedia.PlayForSkin("q_swing", _hero, _hero.transform.position, 0.98f); } catch { }
            yield return new WaitForSeconds(duration);
        }
        else
        {
            const float swingSoundAt = 0.62f;
            if (!string.IsNullOrEmpty(intro))
            {
                _model.Play(intro, false, true);
                yield return new WaitForSeconds(Mathf.Min(swingSoundAt, windupToSwing));
                try { if (_hero != null) DariusMedia.PlayForSkin("q_swing", _hero, _hero.transform.position, 0.98f); } catch { }
                yield return new WaitForSeconds(Mathf.Max(0f, windupToSwing - Mathf.Min(swingSoundAt, windupToSwing)));
                if (_model != null) _model.Play(qClip, false, true);
                yield return new WaitForSeconds(_model != null ? _model.GetClipLength(qClip, 0.53f) : 0.53f);
            }
            else
            {
                float raw = _model.GetClipLength(qClip, Ai_Darius_Decimate.Windup + 0.35f);
                float duration = Mathf.Max(0.55f, Ai_Darius_Decimate.Windup + 0.20f);
                _model.Play(qClip, false, true, raw / duration, 0.08f);
                yield return new WaitForSeconds(Mathf.Min(swingSoundAt, duration));
                try { if (_hero != null) DariusMedia.PlayForSkin("q_swing", _hero, _hero.transform.position, 0.98f); } catch { }
                yield return new WaitForSeconds(Mathf.Max(0f, duration - Mathf.Min(swingSoundAt, duration)));
            }
        }

        string qToIdle = null;
        if (_binding != null && string.Equals(_binding.variantKey, "GodKing", StringComparison.Ordinal) && _model.HasClip("Spell1_ToIdle")) qToIdle = "Spell1_ToIdle";
        if (_model != null && !_deathLatched && !_isMoving && !string.IsNullOrEmpty(qToIdle))
        {
            _model.Play(qToIdle, false, true);
            yield return new WaitForSeconds(_model.GetClipLength(qToIdle, 0.3f));
        }
        if (_model != null && !_deathLatched) _model.PlayBase(ResolveLocomotionClip(), true, true);
        _animationSequence = null;
    }


    public void PlayOneShot(string name)
    {
        if (_model == null || _deathLatched) return;
        if (string.Equals(name, "Spell2", StringComparison.Ordinal)) { PlayWSequenceStart(); return; }
        if (string.Equals(name, "Spell3", StringComparison.Ordinal)) { PlayESequenceStart(); return; }
        if (string.Equals(name, "Spell4", StringComparison.Ordinal)) { PlayRSequenceStart(); return; }

        StopAnimationSequence();
        bool movementAttack = (string.Equals(name, "Attack1", StringComparison.Ordinal) ||
                               string.Equals(name, "Attack2", StringComparison.Ordinal)) && _isMoving;
        if (movementAttack)
        {
            _animationSequence = StartCoroutine(PlayActionToLocomotion(name, 0.76f));
            return;
        }
        _model.Play(name, false, true, 1f, 0.10f);
    }

    private void PlayWSequenceStart()
    {
        StopAnimationSequence();
        _wSwingSequence = true;
        _animationSequence = StartCoroutine(PlayWSequence());
    }

    private IEnumerator PlayWSequence(int attackFacingSerial = 0)
    {
        string action = WClip;
        _model.Play(action, false, true);
        yield return new WaitForSeconds(_model.GetClipLength(action, 0.5f));
        _wSwingSequence = false;
        if (attackFacingSerial > 0) EndAttackFacing(attackFacingSerial, "W attack animation complete");
        if (_model != null && !_deathLatched)
        {
            if (_wArmed) _model.PlayBase(ResolveLocomotionClip(), true, true);
            else if (_godKing && _model.HasClip("Spell2_Deactivate"))
            {
                _model.Play("Spell2_Deactivate", false, true);
                yield return new WaitForSeconds(_model.GetClipLength("Spell2_Deactivate", 0.2f));
                if (_model != null) _model.PlayBase(ResolveLocomotionClip(), true, true);
            }
        }
        _animationSequence = null;
    }

    private void PlayESequenceStart()
    {
        StopAnimationSequence();
        string transition = _isMoving ? (_binding != null ? _binding.eToRunClip : null) : (_binding != null ? _binding.eToIdleClip : null);
        if (!string.IsNullOrEmpty(transition) && _model.HasClip(transition)) _animationSequence = StartCoroutine(PlayESequence());
        else _model.Play(EClip, false, true);
    }

    private IEnumerator PlayESequence()
    {
        string action = EClip;
        _model.Play(action, false, true);
        yield return new WaitForSeconds(_model.GetClipLength(action, 0.3f));
        string transition = _isMoving ? (_binding != null ? _binding.eToRunClip : null) : (_binding != null ? _binding.eToIdleClip : null);
        if (_model != null && _model.HasClip(transition))
        {
            _model.Play(transition, false, true);
            yield return new WaitForSeconds(_model.GetClipLength(transition, 0.25f));
        }
        if (_model != null && !_deathLatched) _model.PlayBase(ResolveLocomotionClip(), true, true);
        _animationSequence = null;
    }

    private void PlayRSequenceStart()
    {
        StopAnimationSequence();
        // God-King's original SKN contains a fourth Wolf_Mat submesh which is authored hidden at rest.
        // Always run R through the sequence coroutine so that exact submesh is enabled only while the
        // original Spell4 animation drives its 60-bone lion/wolf rig.
        _animationSequence = StartCoroutine(PlayRSequence());
    }

    private IEnumerator PlayRSequence()
    {
        string action = RClip;
        if (_godKing)
        {
            bool wolf = _model.SetMaterialVisible("Wolf_Mat", true);
            DariusLog.Info("GODKING-WOLF", "Spell4 begin: restored LoL Wolf_Mat submesh visible=" + wolf +
                " (the GLB Spell4 track drives the original Lion_* bone hierarchy). ");
        }
        _model.Play(action, false, true);
        yield return new WaitForSeconds(_model.GetClipLength(action, 0.5f));
        if (_godKing && _model != null) _model.SetMaterialVisible("Wolf_Mat", false);
        string transition = _binding != null ? _binding.rToRunClip : null;
        if (_model != null && _isMoving && !string.IsNullOrEmpty(transition) && _model.HasClip(transition))
        {
            _model.Play(transition, false, true);
            yield return new WaitForSeconds(_model.GetClipLength(transition, 0.2f));
        }
        if (_model != null && !_deathLatched) _model.PlayBase(ResolveLocomotionClip(), true, true);
        _animationSequence = null;
    }

    public void SetWArmed(bool armed)
    {
        if (_deathLatched)
        {
            _wArmed = false;
            return;
        }
        _wArmed = armed;
        if (_model == null) return;

        if (armed)
        {
            StopAnimationSequence();
            if (_godKing)
                _animationSequence = StartCoroutine(PlayWActivateSequence());
            else
                _model.Play(_isMoving ? WRunClip : WIdleClip, true, true);
            return;
        }

        // An empowered swing is still visually completing when the hit consumes W. Let that
        // authored action finish, then its coroutine performs the God-King deactivation transition.
        if (_wSwingSequence) return;
        StopAnimationSequence();
        if (_godKing && _model.HasClip("Spell2_Deactivate"))
            _animationSequence = StartCoroutine(PlayWDeactivateSequence());
        else
            _model.PlayBase(ResolveLocomotionClip(), true, true);
    }

    private IEnumerator PlayWActivateSequence()
    {
        string activate = _isMoving ? "Spell2_ActivateRun" : "Spell2_ActivateIdle";
        if (_model.HasClip(activate))
        {
            _model.Play(activate, false, true);
            yield return new WaitForSeconds(_model.GetClipLength(activate, 0.25f));
        }
        string inClip = null;
        if (!_isMoving)
        {
            _wIdleInAlt = !_wIdleInAlt;
            if (_wIdleInAlt && _model.HasClip("Spell2_IdleIn2")) inClip = "Spell2_IdleIn2";
            else if (_model.HasClip("Spell2_IdleIn")) inClip = "Spell2_IdleIn";
            else if (_model.HasClip("Spell2_IdleIn2")) inClip = "Spell2_IdleIn2";
        }
        if (_wArmed && !string.IsNullOrEmpty(inClip))
        {
            _model.Play(inClip, false, true);
            yield return new WaitForSeconds(_model.GetClipLength(inClip, 0.2f));
        }
        if (_model != null && _wArmed && !_deathLatched) _model.PlayBase(ResolveLocomotionClip(), true, true);
        _animationSequence = null;
    }

    private IEnumerator PlayWDeactivateSequence()
    {
        _wDeactivateAlt = !_wDeactivateAlt;
        string clip = _wDeactivateAlt && _model.HasClip("Darius_Skin15_Spell2_Deactivate.anm")
            ? "Darius_Skin15_Spell2_Deactivate.anm"
            : "Spell2_Deactivate";
        if (!_model.HasClip(clip)) clip = _model.HasClip("Spell2_Deactivate") ? "Spell2_Deactivate" : "Darius_Skin15_Spell2_Deactivate.anm";
        _model.Play(clip, false, true);
        yield return new WaitForSeconds(_model.GetClipLength(clip, 0.2f));
        if (_model != null && !_deathLatched) _model.PlayBase(ResolveLocomotionClip(), true, true);
        _animationSequence = null;
    }

    private void StopAnimationSequence()
    {
        if (_animationSequence != null)
        {
            try { StopCoroutine(_animationSequence); } catch { }
            _animationSequence = null;
        }
        _wSwingSequence = false;
        CancelAttackFacing("action interrupted");
        if (_godKing && _model != null)
        {
            try { _model.SetMaterialVisible("Wolf_Mat", false); } catch { }
        }
    }

    private void OnDestroy()
    {
        StopAnimationSequence();
        if (_model != null)
        {
            try { _model.Dispose(); } catch { }
            _model = null;
        }
    }
}
