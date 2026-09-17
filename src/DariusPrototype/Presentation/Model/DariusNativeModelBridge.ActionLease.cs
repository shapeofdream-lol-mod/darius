using UnityEngine;

public sealed partial class DariusNativeModelBridge : MonoBehaviour
{
    private bool _ownsAnimatorAction;
    private EntityAnimation _leasedEntityAnimation;

    internal bool OwnsAnimatorAction
    {
        get { return _ownsAnimatorAction && IsReady && isActiveAndEnabled; }
    }

    private void BeginAnimatorAction()
    {
        if (_ownsAnimatorAction) return;
        _ownsAnimatorAction = true;
        BeginActionMovementTracking();
        SyncAnimatorLease();
        if (_entityAnimation != null)
        {
            try { _entityAnimation.StopAbilityAnimation(); }
            catch { }
        }
    }

    private void SyncAnimatorLease()
    {
        if (ReferenceEquals(_leasedEntityAnimation, _entityAnimation)) return;
        DariusNativeEntityAnimationLease.Release(_leasedEntityAnimation);
        _leasedEntityAnimation = null;
        if (_ownsAnimatorAction && _entityAnimation != null)
        {
            _leasedEntityAnimation = _entityAnimation;
            DariusNativeEntityAnimationLease.Acquire(_leasedEntityAnimation);
        }
    }

    private void EndAnimatorAction()
    {
        DariusNativeEntityAnimationLease.Release(_leasedEntityAnimation);
        _leasedEntityAnimation = null;
        _ownsAnimatorAction = false;
        EndActionMovementTracking();
        if (_animator != null) _animator.speed = 1f;
    }
}
