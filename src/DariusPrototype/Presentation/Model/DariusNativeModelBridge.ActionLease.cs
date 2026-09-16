using UnityEngine;

public sealed partial class DariusNativeModelBridge : MonoBehaviour
{
    private bool _ownsAnimatorAction;

    internal bool OwnsAnimatorAction
    {
        get { return _ownsAnimatorAction && IsReady; }
    }

    private void BeginAnimatorAction()
    {
        if (_ownsAnimatorAction) return;
        _ownsAnimatorAction = true;
        if (_entityAnimation != null)
        {
            try { _entityAnimation.StopAbilityAnimation(); }
            catch { }
        }
    }

    private void EndAnimatorAction()
    {
        _ownsAnimatorAction = false;
        if (_animator != null) _animator.speed = 1f;
    }
}