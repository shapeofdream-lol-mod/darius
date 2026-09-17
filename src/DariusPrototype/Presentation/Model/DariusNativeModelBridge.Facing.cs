using System;
using System.Collections;
using UnityEngine;

public sealed partial class DariusNativeModelBridge : MonoBehaviour
{
    private const float RecentMotionWindow = 0.12f;
    private const float MotionDeltaSqr = 0.000001f;

    private bool _actionFacingActive;
    private Vector3 _actionFacingDirection;
    private bool _actionMovementTracking;
    private Vector3 _actionMovementPosition;
    private float _actionLastMotionAt = -999f;

    private bool IsRunningState()
    {
        if (_animator == null) return false;
        if (IsRunState(_animator.GetCurrentAnimatorStateInfo(0))) return true;
        return _animator.IsInTransition(0) && IsRunState(_animator.GetNextAnimatorStateInfo(0));
    }

    private bool IsRunState(AnimatorStateInfo state)
    {
        if (state.IsName(ToAnimatorStateName(RunClipName))) return true;
        DariusNativeSkinProfile profile = DariusNativeSkinProfiles.Find(VariantKey);
        return profile != null && !string.IsNullOrEmpty(profile.WRun) &&
               state.IsName(ToAnimatorStateName(profile.WRun));
    }

    private void BeginActionMovementTracking()
    {
        _actionMovementTracking = _hero != null && _hero.transform != null;
        if (!_actionMovementTracking) return;
        _actionMovementPosition = _hero.transform.position;
        _actionLastMotionAt = IsRunningState() ? Time.time : -999f;
    }

    private void RefreshActionMovement()
    {
        if (!_actionMovementTracking || _hero == null || _hero.transform == null) return;
        Vector3 position = _hero.transform.position;
        Vector3 delta = position - _actionMovementPosition;
        delta.y = 0f;
        if (delta.sqrMagnitude > MotionDeltaSqr) _actionLastMotionAt = Time.time;
        _actionMovementPosition = position;
    }

    private bool IsActionMovingNow()
    {
        RefreshActionMovement();
        return _actionMovementTracking && Time.time - _actionLastMotionAt <= RecentMotionWindow;
    }

    private void EndActionMovementTracking()
    {
        _actionMovementTracking = false;
        _actionMovementPosition = Vector3.zero;
        _actionLastMotionAt = -999f;
    }

    private void ApplyActionFacing(Vector3 direction)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.001f && _hero != null) direction = _hero.transform.forward;
        if (direction.sqrMagnitude < 0.001f) direction = Vector3.forward;
        _actionFacingDirection = direction.normalized;
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
        float endAt = Time.time + Mathf.Max(0f, duration);
        do
        {
            RefreshActionMovement();
            RefreshActionFacing();
            yield return null;
        }
        while (Time.time < endAt);
        RefreshActionMovement();
        RefreshActionFacing();
    }
}
