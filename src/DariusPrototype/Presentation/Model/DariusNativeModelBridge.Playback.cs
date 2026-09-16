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
        string stateName = ToAnimatorStateName(name);
        int hash = Animator.StringToHash(stateName);
        if (!_animator.HasState(0, hash))
        {
            DariusLog.Warn("NATIVE-ANIM", "Animator state missing skin=" + VariantKey+
                " state=" + stateName + " clip=" + name);
            return false;
        }
        _animator.speed = Mathf.Max(0.05f, speed);
        if (fade > 0.001f) _animator.CrossFadeInFixedTime(hash, fade, 0, 0f);
        else _animator.Play(hash, 0, 0f);
        return true;
    }

    private static string ToAnimatorStateName(string clipName)
    {
        return clipName.Replace('.', '_').Replace('/', '_').Replace('\\', '_');
    }

    private bool IsRunningState()
    {
        if (_animator == null) return false;
        AnimatorStateInfo state = _animator.GetCurrentAnimatorStateInfo(0);
        return state.IsName(ToAnimatorStateName(RunClipName));
    }

    private void ApplyActionFacing(Vector3 direction)
    {
        if (_modelRoot == null) return;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.001f && _hero != null) direction = _hero.transform.forward;
        if (direction.sqrMagnitude < 0.001f) return;
        direction.Normalize();
        Transform parent = _modelRoot.transform.parent;
        Vector3 localDirection = parent != null ? parent.InverseTransformDirection(direction) : direction;
        localDirection.y = 0f;
        if (localDirection.sqrMagnitude < 0.001f) return;
        _modelRoot.transform.localRotation = Quaternion.LookRotation(localDirection.normalized, Vector3.up);
    }

    private void ResetActionFacing()
    {
        if (_modelRoot != null) _modelRoot.transform.localRotation = Quaternion.identity;
    }

    public void PlayQ(bool instant)
    {
        StopSequence();
        _sequence = StartCoroutine(PlayQSequence(instant));
    }

    private IEnumerator PlayQSequence(bool instant)
    {
        BeginAnimatorAction();
        string action = FirstExisting(_binding != null ? _binding.qClip : null, "Spell1");
        string intro = _binding != null ? _binding.qIntroClip : null;
        if (!instant && !string.IsNullOrEmpty(intro) && FindClip(intro) != null)
        {
            PlayState(intro, 0.04f, 1f);
            yield return new WaitForSeconds(Mathf.Max(0.05f, Ai_Darius_Decimate.Windup - 0.08f));
        }
        float raw = ClipLength(action, 0.53f);
        float duration = instant ? Mathf.Clamp(raw, 0.42f, 0.70f) : raw;
        PlayState(action, 0.04f, raw / Mathf.Max(0.05f, duration));
        yield return new WaitForSeconds(duration);
        EndAnimatorAction();
        _sequence = null;
    }

    public void PlayWAttack(Vector3 direction)
    {
        StopSequence();
        _wSwingActive = true;
        ApplyActionFacing(direction);
        _sequence = StartCoroutine(PlayTimedAction(
            FirstExisting(_binding != null ? _binding.wClip : null, "Spell2"), null));
    }

    public void PlayOneShot(string name)
    {
        bool moving = IsRunningState();
        string resolved = name;
        string tail = null;
        if (string.Equals(name, "Spell3", StringComparison.Ordinal))
        {
            resolved = FirstExisting(_binding != null ? _binding.eClip : null, "Spell3");
            tail = moving ? (_binding != null ? _binding.eToRunClip : null) : (_binding != null ? _binding.eToIdleClip : null);
        }
        else if (string.Equals(name, "Spell4", StringComparison.Ordinal))
        {
            resolved = FirstExisting(_binding != null ? _binding.rClip : null, "Spell4");
            if (moving) tail = _binding != null ? _binding.rToRunClip : null;
        }
        PlayAction(resolved, tail);
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
        _sequence = StartCoroutine(PlayTimedAction(action, tail));
    }

    private void PlayAction(string state, string tail)
    {
        StopSequence();
        if (IsGodKingSkin && string.Equals(
                state, FirstExisting(_binding != null ? _binding.rClip : null, "Spell4"), StringComparison.Ordinal))
            _sequence = StartCoroutine(PlayGodKingR(state, tail));
        else
            _sequence = StartCoroutine(PlayTimedAction(state, tail));
    }

    private IEnumerator PlayTimedAction(string state, string tail)
    {
        BeginAnimatorAction();
        if (!PlayState(state, 0.05f, 1f))
        {
            FinishAction();
            yield break;
        }
        yield return new WaitForSeconds(ClipLength(state, 0.65f));
        if (!string.IsNullOrEmpty(tail) && FindClip(tail) != null && PlayState(tail, 0.05f, 1f))
            yield return new WaitForSeconds(ClipLength(tail, 0.22f));
        FinishAction();
    }

    private IEnumerator PlayGodKingR(string state, string tail)
    {
        BeginAnimatorAction();
        SetGodKingWolfVisible(true);
        if (!PlayState(state, 0.05f, 1f))
        {
            SetGodKingWolfVisible(false);
            FinishAction();
            yield break;
        }
        yield return new WaitForSeconds(ClipLength(state, 0.6f));
        SetGodKingWolfVisible(false);
        if (!string.IsNullOrEmpty(tail) && FindClip(tail) != null && PlayState(tail, 0.05f, 1f))
            yield return new WaitForSeconds(ClipLength(tail, 0.2f));
        FinishAction();
    }

    private void FinishAction()
    {
        bool finishW = _wSwingActive;
        _wSwingActive = false;
        ResetActionFacing();
        EndAnimatorAction();
        _sequence = null;
        if (finishW && !_wArmedPresentation && IsGodKingSkin)
            _sequence = StartCoroutine(PlayGodKingWTransition(false, false));
    }

    public Transform GetAnchor(string name)
    {
        Transform result;
        return !string.IsNullOrEmpty(name) && _anchors.TryGetValue(name, out result) ? result : null;
    }

    private void StopSequence()
    {
        if (_sequence != null)
        {
            try { StopCoroutine(_sequence); } catch { }
            _sequence = null;
        }
        _wSwingActive = false;
        if (IsGodKingSkin) SetGodKingWolfVisible(false);
        ResetActionFacing();
        EndAnimatorAction();
    }
}
