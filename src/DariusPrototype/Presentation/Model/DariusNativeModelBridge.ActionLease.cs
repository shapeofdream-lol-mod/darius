using UnityEngine;

public sealed partial class DariusNativeModelBridge : MonoBehaviour
{
    private bool _ownsAnimatorAction;
    private EntityAnimation _leasedEntityAnimation;

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
        BeginActionMovementTracking();
        SyncAnimatorLease();
        RefreshActionLowerBody();
    }

    private void SyncAnimatorLease()
    {
        bool wantsLease = WantsAnimatorLease && _entityAnimation != null;
        if (wantsLease && ReferenceEquals(_leasedEntityAnimation, _entityAnimation)) return;

        DariusNativeEntityAnimationLease.Release(_leasedEntityAnimation);
        _leasedEntityAnimation = null;
        if (wantsLease)
        {
            _leasedEntityAnimation = _entityAnimation;
            DariusNativeEntityAnimationLease.Acquire(_leasedEntityAnimation);
        }
    }

    private void EndAnimatorAction()
    {
        DisableActionLowerBody();
        _ownsAnimatorAction = false;
        EndActionMovementTracking();
        if (_animator != null) _animator.speed = 1f;
        SyncAnimatorLease();
    }

    private void Update()
    {
        if (!IsReady || _hero == null) return;

        // Main interrupts its custom animation sequence immediately on death. Native presentation
        // must do the same so EntityAnimation can take over Death instead of waiting for a Q/W/E/R
        // or attack coroutine to finish while FrameUpdate is leased.
        if ((_ownsAnimatorAction || _wArmedPresentation) && IsHeroInDeathState())
        {
            _wArmedPresentation = false;
            _wSwingActive = false;
            EndWLocomotionTracking();
            DisableActionLowerBody();
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
