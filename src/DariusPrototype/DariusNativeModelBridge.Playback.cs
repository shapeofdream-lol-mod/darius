using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
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
        int hash = Animator.StringToHash(name);
        if (!_animator.HasState(0, hash))
        {
            DariusLog.Warn("NATIVE-ANIM", "Animator state missing skin=" + VariantKey + " state=" + name);
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
        _animator.speed = 1f;
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
            if (_entityAnimation != null) try { _entityAnimation.StopAbilityAnimation(); } catch { }
            return;
        }
        string armedState = FirstExisting("Spell2_Idle", _binding != null ? _binding.idleClip : null, IdleClipName);
        PlayState(armedState, 0.06f, 1f);
    }

    public void PlayOneShot(string name)
    {
        string resolved = name;
        if (string.Equals(name, "Spell3", StringComparison.Ordinal)) resolved = FirstExisting(_binding != null ? _binding.eClip : null, "Spell3");
        else if (string.Equals(name, "Spell4", StringComparison.Ordinal)) resolved = FirstExisting(_binding != null ? _binding.rClip : null, "Spell4");
        PlayAction(resolved);
    }

    public void PlayAttack(bool alternate, bool critical, Vector3 direction)
    {
        PlayAction(critical ? CritClipName : (alternate ? Attack2ClipName : Attack1ClipName));
    }

    private void PlayAction(string state)
    {
        StopSequence();
        if (IsGodKingSkin && string.Equals(state, FirstExisting(_binding != null ? _binding.rClip : null, "Spell4"), StringComparison.Ordinal))
            _sequence = StartCoroutine(PlayGodKingR(state));
        else
            PlayState(state, 0.05f, 1f);
    }

    private IEnumerator PlayGodKingR(string state)
    {
        SetGodKingWolfVisible(true);
        PlayState(state, 0.05f, 1f);
        yield return new WaitForSeconds(ClipLength(state, 0.6f));
        SetGodKingWolfVisible(false);
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
        if (_animator != null) _animator.speed = 1f;
    }

    private void OnDestroy()
    {
        StopSequence();
    }
}