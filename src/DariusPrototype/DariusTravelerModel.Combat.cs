using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed partial class DariusTravelerModelInstance : MonoBehaviour
{
    public void PlayAttack(bool alternate, bool critical, Vector3 direction)
    {
        if (_model == null || _deathLatched) return;
        StopAnimationSequence();
        int facingSerial = BeginAttackFacing(direction);
        string clip = critical ? CritClip : (alternate ? Attack2Clip : Attack1Clip);

        // The exported LoL attacks are full-body authored poses. The previous implementation mixed
        // their Root/Pelvis/weapon tracks over a different Run leg pose, which is what produced
        // twisted limbs and the occasional God-King "ball" collapse. Keep SoD world movement,
        // but play one coherent full-body attack and blend back to locomotion.
        if (_isMoving)
        {
            _animationSequence = StartCoroutine(PlayActionToLocomotion(clip, 0.76f, 0.08f, facingSerial));
            DariusLog.DebugInfo("SKIN-ANIM", "Moving attack full-body " + clip + " -> " + ResolveLocomotionClip() +
                " skin=" + VariantKey);
            return;
        }

        string transition = critical ? (_binding != null ? _binding.critToIdleClip : null)
            : (alternate ? (_binding != null ? _binding.attack2ToIdleClip : null) : (_binding != null ? _binding.attack1ToIdleClip : null));
        if (!string.IsNullOrEmpty(transition) && !_model.HasClip(transition)) transition = null;
        _animationSequence = StartCoroutine(PlayActionToIdle(clip, transition, facingSerial));
    }

    public void PlayWAttack(Vector3 direction)
    {
        if (_model == null || _deathLatched) return;
        StopAnimationSequence();
        int facingSerial = BeginAttackFacing(direction);
        _wSwingSequence = true;
        _animationSequence = StartCoroutine(PlayWSequence(facingSerial));
    }

    private IEnumerator PlayActionToLocomotion(string action, float visualDuration)
    {
        return PlayActionToLocomotion(action, visualDuration, 0f);
    }

    private IEnumerator PlayActionToLocomotion(string action, float visualDuration, float tailTrim)
    {
        return PlayActionToLocomotion(action, visualDuration, tailTrim, 0);
    }

    private IEnumerator PlayActionToLocomotion(string action, float visualDuration, float tailTrim, int attackFacingSerial)
    {
        float raw = _model.GetClipLength(action, visualDuration);
        float duration = Mathf.Clamp(visualDuration, 0.45f, 0.95f);
        _model.Play(action, false, true, raw / duration, 0.08f);
        yield return new WaitForSeconds(Mathf.Max(0.08f, duration - Mathf.Max(0f, tailTrim)));
        if (_model != null && !_deathLatched) _model.PlayBase(ResolveLocomotionClip(), true, true, 1f, 0.12f);
        if (attackFacingSerial > 0) EndAttackFacing(attackFacingSerial, "attack animation complete");
        _animationSequence = null;
    }

    private IEnumerator PlayActionToIdle(string action, string transition, int attackFacingSerial = 0)
    {
        float rawAction = _model.GetClipLength(action, 0.8f);
        float actionDuration = 0.82f;
        _model.Play(action, false, true, rawAction / actionDuration, 0.08f);
        yield return new WaitForSeconds(actionDuration - 0.10f);
        if (_model == null || _deathLatched)
        {
            if (attackFacingSerial > 0) EndAttackFacing(attackFacingSerial, "model/death");
            _animationSequence = null;
            yield break;
        }
        if (!string.IsNullOrEmpty(transition) && _model.HasClip(transition))
        {
            float rawTransition = _model.GetClipLength(transition, 0.22f);
            const float transitionDuration = 0.22f;
            _model.Play(transition, false, true, rawTransition / transitionDuration, 0.06f);
            yield return new WaitForSeconds(0.08f);
        }
        if (_model != null && !_deathLatched) _model.PlayBase(IdleClip, true, true, 1f, 0.12f);
        if (attackFacingSerial > 0) EndAttackFacing(attackFacingSerial, "attack animation complete");
        _animationSequence = null;
    }

    public void PlayQ(bool instant = false)
    {
        if (_model == null || _deathLatched) return;
        StopAnimationSequence();
        _animationSequence = StartCoroutine(PlayQSequence(instant));
    }

    private IEnumerator PlayQSequence(bool instant)
    {
        const float swingLead = 0.08f;
        float windupToSwing = Mathf.Max(0.05f, Ai_Darius_Decimate.Windup - swingLead);
        string qClip = BoundClip(_binding != null ? _binding.qClip : null, "Spell1");
        string intro = _binding != null ? _binding.qIntroClip : null;
        if (!string.IsNullOrEmpty(intro) && !_model.HasClip(intro)) intro = null;
        DariusLog.DebugInfo("SKIN-ANIM", "Q graph profile=" + VariantKey + " instant=" + instant +
            " intro=" + (intro ?? "<none>") + " action=" + qClip);

        if (instant)
        {
            // No intro, no artificial 0.75 s wait. The attack action begins immediately so its
            // visual spin, Q hit snapshot and fast-forwarded Riot ring all share the same beat.
            float raw = _model.GetClipLength(qClip, 0.53f);
            if (raw <= 0.001f) raw = 0.53f;
            float duration = Mathf.Clamp(raw, 0.42f, 0.70f);
            _model.Play(qClip, false, true, raw / duration, 0.03f);
            try { if (_hero != null) DariusMedia.PlayForSkin("q_swing", _hero, _hero.transform.position, 0.98f); } catch { }
            yield return new WaitForSeconds(duration);
        }
        else
        {
            const float swingSoundAt = 0.62f;
            if (!string.IsNullOrEmpty(intro))
            {
                _model.Play(intro, false, true);
                yield return new WaitForSeconds(Mathf.Min(swingSoundAt, windupToSwing));
                try { if (_hero != null) DariusMedia.PlayForSkin("q_swing", _hero, _hero.transform.position, 0.98f); } catch { }
                yield return new WaitForSeconds(Mathf.Max(0f, windupToSwing - Mathf.Min(swingSoundAt, windupToSwing)));
                if (_model != null) _model.Play(qClip, false, true);
                yield return new WaitForSeconds(_model != null ? _model.GetClipLength(qClip, 0.53f) : 0.53f);
            }
            else
            {
                float raw = _model.GetClipLength(qClip, Ai_Darius_Decimate.Windup + 0.35f);
                float duration = Mathf.Max(0.55f, Ai_Darius_Decimate.Windup + 0.20f);
                _model.Play(qClip, false, true, raw / duration, 0.08f);
                yield return new WaitForSeconds(Mathf.Min(swingSoundAt, duration));
                try { if (_hero != null) DariusMedia.PlayForSkin("q_swing", _hero, _hero.transform.position, 0.98f); } catch { }
                yield return new WaitForSeconds(Mathf.Max(0f, duration - Mathf.Min(swingSoundAt, duration)));
            }
        }

        string qToIdle = null;
        if (_binding != null && string.Equals(_binding.variantKey, "GodKing", StringComparison.Ordinal) && _model.HasClip("Spell1_ToIdle")) qToIdle = "Spell1_ToIdle";
        if (_model != null && !_deathLatched && !_isMoving && !string.IsNullOrEmpty(qToIdle))
        {
            _model.Play(qToIdle, false, true);
            yield return new WaitForSeconds(_model.GetClipLength(qToIdle, 0.3f));
        }
        if (_model != null && !_deathLatched) _model.PlayBase(ResolveLocomotionClip(), true, true);
        _animationSequence = null;
    }

    public void PlayOneShot(string name)
    {
        if (_model == null || _deathLatched) return;
        if (string.Equals(name, "Spell2", StringComparison.Ordinal)) { PlayWSequenceStart(); return; }
        if (string.Equals(name, "Spell3", StringComparison.Ordinal)) { PlayESequenceStart(); return; }
        if (string.Equals(name, "Spell4", StringComparison.Ordinal)) { PlayRSequenceStart(); return; }

        StopAnimationSequence();
        bool movementAttack = (string.Equals(name, "Attack1", StringComparison.Ordinal) ||
                               string.Equals(name, "Attack2", StringComparison.Ordinal)) && _isMoving;
        if (movementAttack)
        {
            _animationSequence = StartCoroutine(PlayActionToLocomotion(name, 0.76f));
            return;
        }
        _model.Play(name, false, true, 1f, 0.10f);
    }
}