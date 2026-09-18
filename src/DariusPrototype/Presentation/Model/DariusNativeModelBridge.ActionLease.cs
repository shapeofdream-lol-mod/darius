using UnityEngine;

public sealed partial class DariusNativeModelBridge : MonoBehaviour
{
    private bool _ownsAnimatorAction;
    private EntityAnimation _leasedEntityAnimation;
    private bool _animatorSpeedCaptured;
    private float _animatorSpeedBeforeAction = 1f;

    internal bool OwnsAnimatorAction
    {
        get { return _ownsAnimatorAction && IsReady && isActiveAndEnabled; }
    }

    private bool WantsAnimatorLease
    {
        get { return (_ownsAnimatorAction || _wArmedPresentation) && IsReady && isActiveAndEnabled; }
    }

    private void BeginAnimatorAction()
    {
        if (_ownsAnimatorAction) return;
        _ownsAnimatorAction = true;
        if (_animator != null)
        {
            _animatorSpeedBeforeAction = _animator.speed;
            _animatorSpeedCaptured = true;
        }
        BeginActionMovementTracking();
        SyncAnimatorLease();
    }

    private void SyncAnimatorLease()
    {
        bool wantsLease = WantsAnimatorLease && _entityAnimation != null;
        if (wantsLease && ReferenceEquals(_leasedEntityAnimation, _entityAnimation)) return;

        ReleaseAnimatorLease();
        if (wantsLease)
        {
            _leasedEntityAnimation = _entityAnimation;
            DariusNativeEntityAnimationLease.Acquire(_leasedEntityAnimation);
        }
    }

    private void ReleaseAnimatorLease()
    {
        EntityAnimation leased = _leasedEntityAnimation;
        _leasedEntityAnimation = null;
        if (!ReferenceEquals(leased, null))
            DariusNativeEntityAnimationLease.Release(leased);
    }

    private void EndAnimatorAction()
    {
        _ownsAnimatorAction = false;
        EndActionMovementTracking();
        if (_animatorSpeedCaptured && _animator != null)
            _animator.speed = _animatorSpeedBeforeAction;
        _animatorSpeedCaptured = false;
        _animatorSpeedBeforeAction = 1f;
        SyncAnimatorLease();
    }

    private void Update()
    {
        if (!IsReady || _hero == null) return;

        if ((_ownsAnimatorAction || _wArmedPresentation) && IsHeroInDeathState())
        {
            _wArmedPresentation = false;
            _wSwingActive = false;
            EndWLocomotionTracking();
            StopSequence();
            SyncAnimatorLease();
            return;
        }

        if (_wArmedPresentation && !_ownsAnimatorAction)
            UpdateWArmedLocomotion();
    }

    private bool IsHeroInDeathState()
    {
        if (_hero == null) return false;
        try { if (_hero.IsNullInactiveDeadOrKnockedOut()) return true; } catch { }
        try { if (_hero.currentHealth <= 0.011f) return true; } catch { }
        return false;
    }
}
