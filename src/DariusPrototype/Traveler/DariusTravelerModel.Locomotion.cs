public sealed partial class DariusTravelerModelInstance : MonoBehaviour
{
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
        _homeguard = false;
        UpdateActionLocomotionLayer();
        if (_model.isPlayingFullBodyOneShot || _animationSequence != null) return;

        string desired = ResolveLocomotionClip();
        if (!string.Equals(_model.currentAnimation, desired, StringComparison.Ordinal))
            _model.PlayBase(desired, true, false, 1f, 0.12f);
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
}