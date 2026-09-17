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
    private bool _lowerBodyActive;
    private string _lowerBodyClip;

    private bool IsRunningState()
    {
        if (_animator == null) return false;
        if (IsRunState(_animator.GetCurrentAnimatorStateInfo(0))) return true;
        return _animator.IsInTransition(0) && IsRunState(_animator.GetNextAnimatorStateInfo(0));
    }

    private bool IsRunState(AnimatorStateInfo state)
    {
        if (state.IsName(DariusNativeAssetContract.AnimatorStateName(RunClipName))) return true;
        DariusNativeSkinProfile profile = DariusNativeSkinProfiles.Find(VariantKey);
        if (profile != null && !string.IsNullOrEmpty(profile.WRun) &&
            state.IsName(DariusNativeAssetContract.AnimatorStateName(profile.WRun))) return true;
        return state.IsName(DariusNativeAssetContract.AnimatorStateName("Spell2_Run"));
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

    private void RefreshActionLowerBody()
    {
        if (_animator == null || _lowerBodyLayer < 0 || !_ownsAnimatorAction || IsHeroInDeathState())
        {
            DisableActionLowerBody();
            return;
        }

        if (!IsActionMovingNow())
        {
            DisableActionLowerBody();
            return;
        }

        string clip = _wArmedPresentation ? ResolveWLocomotionClip(true) : RunClipName;
        if (string.IsNullOrEmpty(clip) || FindClip(clip) == null)
        {
            DisableActionLowerBody();
            return;
        }

        int hash = Animator.StringToHash(DariusNativeAssetContract.AnimatorStateName(clip));
        if (!_animator.HasState(_lowerBodyLayer, hash))
        {
            DariusLog.Warn("NATIVE-ANIM", "Lower-body state missing skin=" + VariantKey + " clip=" + clip);
            DisableActionLowerBody();
            return;
        }

        if (!_lowerBodyActive || !string.Equals(_lowerBodyClip, clip, StringComparison.OrdinalIgnoreCase))
            _animator.Play(hash, _lowerBodyLayer, 0f);
        _animator.SetLayerWeight(_lowerBodyLayer, 1f);
        _lowerBodyActive = true;
        _lowerBodyClip = clip;
    }

    private void DisableActionLowerBody()
    {
        if (_animator != null && _lowerBodyLayer >= 0 && _lowerBodyActive)
            _animator.SetLayerWeight(_lowerBodyLayer, 0f);
        _lowerBodyActive = false;
        _lowerBodyClip = null;
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
            RefreshActionLowerBody();
            yield return null;
        }
        while (Time.time < endAt);
        RefreshActionMovement();
        RefreshActionFacing();
        RefreshActionLowerBody();
    }
}
