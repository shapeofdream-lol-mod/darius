using System;
using System.Collections;
using UnityEngine;

public sealed partial class DariusNativeModelBridge : MonoBehaviour
{
    private AnimationClip FindClip(string name)
    {
        AnimationClip clip;
        return !string.IsNullOrEmpty(name) && _clips.TryGetValue(name, out clip) ? clip : null;
    }

    private float ClipLength(string name, float fallback)
    {
        AnimationClip clip = FindClip(name);
        return clip != null ? Mathf.Max(0.01f, clip.length) : fallback;
    }

    private bool PlayState(string name, float fade, float speed)
    {
        if (_animator == null || string.IsNullOrEmpty(name)) return false;
        string stateName = DariusNativeAssetContract.AnimatorStateName(name);
        int hash = Animator.StringToHash(stateName);
        if (!_animator.HasState(0, hash))
        {
            DariusLog.Warn("NATIVE-ANIM", "Animator state missing skin=" + VariantKey +
                " state=" + stateName + " clip=" + name);
            return false;
        }
        _animator.speed = Mathf.Max(0.05f, speed);
        if (fade > 0.001f) _animator.CrossFadeInFixedTime(hash, fade, 0, 0f);
        else _animator.Play(hash, 0, 0f);
        return true;
    }

    public void PlayQ(bool instant)
    {
        StopSequence();
        _sequence = StartCoroutine(PlayQSequence(instant));
    }

    private IEnumerator PlayQSequence(bool instant)
    {
        BeginAnimatorAction();
        const float swingLead = 0.08f;
        const float swingSoundAt = 0.62f;
        float windupToSwing = Mathf.Max(0.05f, Ai_Darius_Decimate.Windup - swingLead);
        string action = _binding != null ? _binding.qClip : null;
        string intro = _binding != null ? _binding.qIntroClip : null;
        if (string.IsNullOrEmpty(intro) || FindClip(intro) == null) intro = null;

        if (instant)
        {
            float raw = ClipLength(action, 0.53f);
            float duration = Mathf.Clamp(raw, 0.42f, 0.70f);
            PlayState(action, 0.03f, raw / Mathf.Max(0.05f, duration));
            PlayQSwingAudio();
            yield return WaitForActionDuration(duration);
        }
        else if (!string.IsNullOrEmpty(intro))
        {
            PlayState(intro, 0.04f, 1f);
            float beforeSound = Mathf.Min(swingSoundAt, windupToSwing);
            yield return WaitForActionDuration(beforeSound);
            PlayQSwingAudio();
            yield return WaitForActionDuration(Mathf.Max(0f, windupToSwing - beforeSound));

            float raw = ClipLength(action, 0.53f);
            PlayState(action, 0.04f, 1f);
            yield return WaitForActionDuration(raw);
        }
        else
        {
            float raw = ClipLength(action, Ai_Darius_Decimate.Windup + 0.35f);
            float duration = Mathf.Max(0.55f, Ai_Darius_Decimate.Windup + 0.20f);
            PlayState(action, 0.08f, raw / duration);
            float beforeSound = Mathf.Min(swingSoundAt, duration);
            yield return WaitForActionDuration(beforeSound);
            PlayQSwingAudio();
            yield return WaitForActionDuration(Mathf.Max(0f, duration - beforeSound));
        }

        // Main plays God-King's authored Q-to-idle tail only when the cast ends stationary.
        if (IsGodKingSkin && !IsActionMovingNow() && FindClip("Spell1_ToIdle") != null)
        {
            if (PlayState("Spell1_ToIdle", 0.05f, 1f))
                yield return WaitForActionDuration(ClipLength("Spell1_ToIdle", 0.3f));
        }
        FinishAction();
    }

    private void PlayQSwingAudio()
    {
        try
        {
            if (_hero != null)
                DariusMedia.PlayForSkin("q_swing", _hero, _hero.transform.position, 0.98f);
        }
        catch { }
    }

    public void PlayWAttack(Vector3 direction)
    {
        StopSequence();
        _wSwingActive = true;
        ApplyActionFacing(direction);
        _sequence = StartCoroutine(PlayTimedAction(
            _binding != null ? _binding.wClip : null, null));
    }

    public void PlayOneShot(string name)
    {
        if (string.Equals(name, "Spell3", StringComparison.Ordinal))
        {
            string state = _binding != null ? _binding.eClip : null;
            PlayLocomotionAction(state,
                _binding != null ? _binding.eToIdleClip : null,
                _binding != null ? _binding.eToRunClip : null,
                false);
            return;
        }
        if (string.Equals(name, "Spell4", StringComparison.Ordinal))
        {
            string state = _binding != null ? _binding.rClip : null;
            PlayLocomotionAction(state, null, _binding != null ? _binding.rToRunClip : null, IsGodKingSkin);
            return;
        }
        PlayAction(name, null);
    }

    public void PlayAttack(bool alternate, bool critical, Vector3 direction)
    {
        bool moving = IsRunningState();
        string action = critical ? CritClipName : (alternate ? Attack2ClipName : Attack1ClipName);
        string tail = null;
        if (!moving && _binding != null)
            tail = critical ? _binding.critToIdleClip : (alternate ? _binding.attack2ToIdleClip : _binding.attack1ToIdleClip);
        StopSequence();
        ApplyActionFacing(direction);
        _sequence = StartCoroutine(PlayAttackSequence(action, tail, moving));
    }

    private IEnumerator PlayAttackSequence(string state, string tail, bool moving)
    {
        BeginAnimatorAction();
        float duration = moving ? 0.76f : 0.82f;
        float raw = ClipLength(state, duration);
        if (!PlayState(state, 0.08f, raw / duration))
        {
            FinishAction();
            yield break;
        }
        if (moving)
        {
            yield return WaitForActionDuration(Mathf.Max(0.08f, duration - 0.08f));
            FinishAction();
            yield break;
        }
        yield return WaitForActionDuration(duration - 0.10f);
        if (!string.IsNullOrEmpty(tail) && FindClip(tail) != null)
        {
            float rawTail = ClipLength(tail, 0.22f);
            if (PlayState(tail, 0.06f, rawTail / 0.22f))
                yield return WaitForActionDuration(0.08f);
        }
        FinishAction();
    }

    private void PlayAction(string state, string tail)
    {
        StopSequence();
        _sequence = StartCoroutine(PlayTimedAction(state, tail));
    }

    private void PlayLocomotionAction(string state, string idleTail, string runTail, bool godKingWolf)
    {
        StopSequence();
        _sequence = StartCoroutine(PlayLocomotionActionSequence(state, idleTail, runTail, godKingWolf));
    }

    private IEnumerator PlayTimedAction(string state, string tail)
    {
        BeginAnimatorAction();
        if (!PlayState(state, 0.05f, 1f))
        {
            FinishAction();
            yield break;
        }
        yield return WaitForActionDuration(ClipLength(state, 0.65f));
        if (!string.IsNullOrEmpty(tail) && FindClip(tail) != null && PlayState(tail, 0.05f, 1f))
            yield return WaitForActionDuration(ClipLength(tail, 0.22f));
        FinishAction();
    }

    private IEnumerator PlayLocomotionActionSequence(string state, string idleTail, string runTail, bool godKingWolf)
    {
        BeginAnimatorAction();
        if (godKingWolf) SetGodKingWolfVisible(true);
        if (!PlayState(state, 0.05f, 1f))
        {
            if (godKingWolf) SetGodKingWolfVisible(false);
            FinishAction();
            yield break;
        }
        yield return WaitForActionDuration(ClipLength(state, godKingWolf ? 0.6f : 0.65f));
        if (godKingWolf) SetGodKingWolfVisible(false);
        string tail = IsActionMovingNow() ? runTail : idleTail;
        if (!string.IsNullOrEmpty(tail) && FindClip(tail) != null && PlayState(tail, 0.05f, 1f))
            yield return WaitForActionDuration(ClipLength(tail, 0.22f));
        FinishAction();
    }

    private void FinishAction()
    {
        bool finishW = _wSwingActive;
        _wSwingActive = false;
        ResetActionFacing();
        EndAnimatorAction();
        _sequence = null;

        if (_wArmedPresentation)
            RestoreWLocomotionAfterAction();
        else if (finishW && IsGodKingSkin)
            _sequence = StartCoroutine(PlayGodKingWTransition(false, false));
    }
}
