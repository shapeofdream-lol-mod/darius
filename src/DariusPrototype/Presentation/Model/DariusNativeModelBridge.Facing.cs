using System.Collections;
using UnityEngine;

public sealed partial class DariusNativeModelBridge : MonoBehaviour
{
    private bool _actionFacingActive;
    private Vector3 _actionFacingDirection;

    private bool IsRunningState()
    {
        if (_animator == null) return false;
        AnimatorStateInfo state = _animator.GetCurrentAnimatorStateInfo(0);
        return state.IsName(ToAnimatorStateName(RunClipName));
    }

    private void ApplyActionFacing(Vector3 direction)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.001f && _hero != null) direction = _hero.transform.forward;
        if (direction.sqrMagnitude < 0.001f) direction = Vector3.forward;
        direction.Normalize();
        _actionFacingDirection = direction;
        _actionFacingActive = true;
        RefreshActionFacing();
    }

    private void RefreshActionFacing()
    {
        if (!_actionFacingActive || _modelRoot == null) return;
        Transform parent = _modelRoot.transform.parent;
        Vector3 localDirection = parent != null
            ? parent.InverseTransformDirection(_actionFacingDirection)
            : _actionFacingDirection;
        localDirection.y = 0f;
        if (localDirection.sqrMagnitude < 0.001f) return;
        _modelRoot.transform.localRotation = Quaternion.LookRotation(localDirection.normalized, Vector3.up);
    }

    private void ResetActionFacing()
    {
        _actionFacingActive = false;
        _actionFacingDirection = Vector3.zero;
        if (_modelRoot != null) _modelRoot.transform.localRotation = Quaternion.identity;
    }

    private IEnumerator WaitForActionDuration(float duration)
    {
        duration = Mathf.Max(0f, duration);
        if (!_actionFacingActive)
        {
            if (duration > 0f) yield return new WaitForSeconds(duration);
            yield break;
        }

        float endAt = Time.time + duration;
        do
        {
            RefreshActionFacing();
            yield return null;
        }
        while (Time.time < endAt);
        RefreshActionFacing();
    }
}
