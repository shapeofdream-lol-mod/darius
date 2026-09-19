using System;
using System.Collections;
using UnityEngine;

public sealed partial class DariusNativeModelBridge : MonoBehaviour
{
    private const float WMovementThreshold = 0.12f;

    private bool _wArmedPresentation;
    private bool _wSwingActive;
    private bool _wIdleInAlt;
    private bool _wDeactivateAlt;
    private Vector3 _wMovementPosition;
    private float _wMovementSampleAt;
    private bool _wMoving;

    public void SetWArmed(bool armed)
    {
        if (_wArmedPresentation == armed) return;
        bool moving = IsRunningState();

        if (armed)
        {
            StopSequence();
            _wArmedPresentation = true;
            BeginWLocomotionTracking(moving);
            SyncAnimatorLease();

            if (IsGodKingSkin)
                _sequence = StartCoroutine(PlayGodKingWTransition(true, moving));
            else
                PlayWLocomotionState(moving, true);
            return;
        }

        // Main keeps the empowered swing authoritative until it finishes. Clearing the armed flag
        // here lets FinishAction return ownership to stock locomotion without interrupting the swing.
        if (_wSwingActive)
        {
            _wArmedPresentation = false;
            EndWLocomotionTracking();
            SyncAnimatorLease();
            return;
        }

        _wArmedPresentation = false;
        EndWLocomotionTracking();
        SyncAnimatorLease();

        if (IsGodKingSkin)
        {
            StopSequence();
            _sequence = StartCoroutine(PlayGodKingWTransition(false, moving));
        }
    }

    private IEnumerator PlayGodKingWTransition(bool armed, bool moving, bool alternateDeactivate = true)
    {
        DariusNativeSkinProfile profile = DariusNativeSkinProfiles.Find(VariantKey);
        if (profile == null) throw new InvalidOperationException("Native skin profile missing: " + VariantKey);
        BeginAnimatorAction();
        try
        {
            if (armed)
            {
                string activate = moving ? profile.WActivateRun : profile.WActivateIdle;
                if (PlayState(activate, 0.04f, 1f))
                    yield return WaitForActionDuration(ClipLength(activate, 0.25f));

                if (!moving && _wArmedPresentation)
                {
                    _wIdleInAlt = !_wIdleInAlt;
                    string entry = _wIdleInAlt ? profile.WIdleInAlt : profile.WIdleIn;
                    if (PlayState(entry, 0.04f, 1f))
                        yield return WaitForActionDuration(ClipLength(entry, 0.2f));
                }
            }
            else
            {
                string deactivate = profile.WDeactivate;
                if (alternateDeactivate)
                {
                    _wDeactivateAlt = !_wDeactivateAlt;
                    deactivate = _wDeactivateAlt ? profile.WDeactivateAlt : profile.WDeactivate;
                }
                if (PlayState(deactivate, 0.04f, 1f))
                    yield return WaitForActionDuration(ClipLength(deactivate, 0.2f));
            }
        }
        finally
        {
            if (_wArmedPresentation && !IsHeroInDeathState())
                PlayWLocomotionState(SampleWMovement(), true);
            EndAnimatorAction();
            _sequence = null;
        }
    }

    private void BeginWLocomotionTracking(bool moving)
    {
        _wMoving = moving;
        if (_hero == null || _hero.transform == null)
        {
            _wMovementPosition = Vector3.zero;
            _wMovementSampleAt = Time.time;
            return;
        }
        _wMovementPosition = _hero.transform.position;
        _wMovementSampleAt = Time.time;
    }

    private void EndWLocomotionTracking()
    {
        _wMoving = false;
        _wMovementPosition = Vector3.zero;
        _wMovementSampleAt = 0f;
    }

    private bool SampleWMovement()
    {
        if (_hero == null || _hero.transform == null) return _wMoving;
        float now = Time.time;
        float dt = Mathf.Max(0.001f, now - _wMovementSampleAt);
        Vector3 position = _hero.transform.position;
        Vector3 delta = position - _wMovementPosition;
        delta.y = 0f;
        _wMovementPosition = position;
        _wMovementSampleAt = now;
        return delta.magnitude / dt > WMovementThreshold;
    }

    private void UpdateWArmedLocomotion()
    {
        if (!_wArmedPresentation || _ownsAnimatorAction) return;
        bool moving = SampleWMovement();
        if (moving == _wMoving) return;
        PlayWLocomotionState(moving, false);
    }

    private void RestoreWLocomotionAfterAction()
    {
        if (!_wArmedPresentation || IsHeroInDeathState()) return;
        PlayWLocomotionState(SampleWMovement(), true);
    }

    private void PlayWLocomotionState(bool moving, bool force)
    {
        string clip = ResolveWLocomotionClip(moving);
        if (string.IsNullOrEmpty(clip)) return;
        if (!force && moving == _wMoving) return;
        _wMoving = moving;
        PlayState(clip, 0.12f, 1f);
    }

    private string ResolveWLocomotionClip(bool moving)
    {
        DariusNativeSkinProfile profile = DariusNativeSkinProfiles.Find(VariantKey);
        string preferred = profile != null ? (moving ? profile.WRun : profile.WIdle) : null;
        if (!string.IsNullOrEmpty(preferred) && FindClip(preferred) != null) return preferred;

        // Main probes these authored names for every skin and falls back only when the clip is absent.
        string authored = moving ? "Spell2_Run" : "Spell2_Idle";
        if (FindClip(authored) != null) return authored;
        return moving ? RunClipName : IdleClipName;
    }

    private void ResetWLocomotionOverrides()
    {
        _wArmedPresentation = false;
        EndWLocomotionTracking();
        SyncAnimatorLease();
    }
}
