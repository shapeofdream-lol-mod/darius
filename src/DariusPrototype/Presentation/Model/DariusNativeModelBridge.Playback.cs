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
            DariusLog.Warn("NATIVE-ANIM", "Animator state missing skin=" + VariantKey +
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
        PlayAction(FirstExisting(_binding != null ? _binding.wClip : null, "Spell2"));
    }

    public void SetWArmed(bool armed)
    {
        if (!armed)
        {
            StopSequence();
            if (_entityAnimation != null) try { _entityAnimation.StopAbilityAnimation(); } catch { }
            return;
        }
        StopSequence();
        BeginAnimatorAction();
        string armedState = FirstExisting("Spell2_Idle", _binding != null ? _binding.idleClip : null, IdleClipName);
        if (!PlayState(armedState, 0.06f, 1f)) EndAnimatorAction();
    }

    public void PlayOneShot(string name)
    {
        string resolved = name;
        if (string.Equals(name, "Spell3", StringComparison.Ordinal))
            resolved = FirstExisting(_binding != null ? _binding.eClip : null, "Spell3");
        else if (string.Equals(name, "Spell4", StringComparison.Ordinal))
            resolved = FirstExisting(_binding != null ? _binding.rClip : null, "Spell4");
        PlayAction(resolved);
    }

    public void PlayAttack(bool alternate, bool critical, Vector3 direction)
    {
        PlayAction(critical ? CritClipName : (alternate ? Attack2ClipName : Attack1ClipName));
    }

    private void PlayAction(string state)
    {
        StopSequence();
        if (IsGodKingSkin && string.Equals(
                state, FirstExisting(_binding != null ? _binding.rClip : null, "Spell4"), StringComparison.Ordinal))
            _sequence = StartCoroutine(PlayGodKingR(state));
        else
            _sequence = StartCoroutine(PlayTimedAction(state));
    }

    private IEnumerator PlayTimedAction(string state)
    {
        BeginAnimatorAction();
        if (!PlayState(state, 0.05f, 1f))
        {
            EndAnimatorAction();
            _sequence = null;
            yield break;
        }
        yield return new WaitForSeconds(ClipLength(state, 0.65f));
        EndAnimatorAction();
        _sequence = null;
    }

    private IEnumerator PlayGodKingR(string state)
    {
        BeginAnimatorAction();
        SetGodKingWolfVisible(true);
        if (!PlayState(state, 0.05f, 1f))
        {
            SetGodKingWolfVisible(false);
            EndAnimatorAction();
            _sequence = null;
            yield break;
        }
        yield return new WaitForSeconds(ClipLength(state, 0.6f));
        SetGodKingWolfVisible(false);
        EndAnimatorAction();
        _sequence = null;
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
        if (IsGodKingSkin) SetGodKingWolfVisible(false);
        EndAnimatorAction();
    }

    private void OnDestroy()
    {
        StopSequence();
    }
}