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
    private void PlayWSequenceStart()
    {
        StopAnimationSequence();
        _wSwingSequence = true;
        _animationSequence = StartCoroutine(PlayWSequence());
    }

    private IEnumerator PlayWSequence(int attackFacingSerial = 0)
    {
        string action = WClip;
        _model.Play(action, false, true);
        yield return new WaitForSeconds(_model.GetClipLength(action, 0.5f));
        _wSwingSequence = false;
        if (attackFacingSerial > 0) EndAttackFacing(attackFacingSerial, "W attack animation complete");
        if (_model != null && !_deathLatched)
        {
            if (_wArmed) _model.PlayBase(ResolveLocomotionClip(), true, true);
            else if (_godKing && _model.HasClip("Spell2_Deactivate"))
            {
                _model.Play("Spell2_Deactivate", false, true);
                yield return new WaitForSeconds(_model.GetClipLength("Spell2_Deactivate", 0.2f));
                if (_model != null) _model.PlayBase(ResolveLocomotionClip(), true, true);
            }
        }
        _animationSequence = null;
    }

    private void PlayESequenceStart()
    {
        StopAnimationSequence();
        string transition = _isMoving ? (_binding != null ? _binding.eToRunClip : null) : (_binding != null ? _binding.eToIdleClip : null);
        if (!string.IsNullOrEmpty(transition) && _model.HasClip(transition)) _animationSequence = StartCoroutine(PlayESequence());
        else _model.Play(EClip, false, true);
    }

    private IEnumerator PlayESequence()
    {
        string action = EClip;
        _model.Play(action, false, true);
        yield return new WaitForSeconds(_model.GetClipLength(action, 0.3f));
        string transition = _isMoving ? (_binding != null ? _binding.eToRunClip : null) : (_binding != null ? _binding.eToIdleClip : null);
        if (_model != null && _model.HasClip(transition))
        {
            _model.Play(transition, false, true);
            yield return new WaitForSeconds(_model.GetClipLength(transition, 0.25f));
        }
        if (_model != null && !_deathLatched) _model.PlayBase(ResolveLocomotionClip(), true, true);
        _animationSequence = null;
    }

    private void PlayRSequenceStart()
    {
        StopAnimationSequence();
        // God-King's original SKN contains a fourth Wolf_Mat submesh which is authored hidden at rest.
        // Always run R through the sequence coroutine so that exact submesh is enabled only while the
        // original Spell4 animation drives its 60-bone lion/wolf rig.
        _animationSequence = StartCoroutine(PlayRSequence());
    }

    private IEnumerator PlayRSequence()
    {
        string action = RClip;
        if (_godKing)
        {
            bool wolf = _model.SetMaterialVisible("Wolf_Mat", true);
            DariusLog.Info("GODKING-WOLF", "Spell4 begin: restored LoL Wolf_Mat submesh visible=" + wolf +
                " (the GLB Spell4 track drives the original Lion_* bone hierarchy). ");
        }
        _model.Play(action, false, true);
        yield return new WaitForSeconds(_model.GetClipLength(action, 0.5f));
        if (_godKing && _model != null) _model.SetMaterialVisible("Wolf_Mat", false);
        string transition = _binding != null ? _binding.rToRunClip : null;
        if (_model != null && _isMoving && !string.IsNullOrEmpty(transition) && _model.HasClip(transition))
        {
            _model.Play(transition, false, true);
            yield return new WaitForSeconds(_model.GetClipLength(transition, 0.2f));
        }
        if (_model != null && !_deathLatched) _model.PlayBase(ResolveLocomotionClip(), true, true);
        _animationSequence = null;
    }

    public void SetWArmed(bool armed)
    {
        if (_deathLatched)
        {
            _wArmed = false;
            return;
        }
        _wArmed = armed;
        if (_model == null) return;

        if (armed)
        {
            StopAnimationSequence();
            if (_godKing)
                _animationSequence = StartCoroutine(PlayWActivateSequence());
            else
                _model.Play(_isMoving ? WRunClip : WIdleClip, true, true);
            return;
        }

        // An empowered swing is still visually completing when the hit consumes W. Let that
        // authored action finish, then its coroutine performs the God-King deactivation transition.
        if (_wSwingSequence) return;
        StopAnimationSequence();
        if (_godKing && _model.HasClip("Spell2_Deactivate"))
            _animationSequence = StartCoroutine(PlayWDeactivateSequence());
        else
            _model.PlayBase(ResolveLocomotionClip(), true, true);
    }

    private IEnumerator PlayWActivateSequence()
    {
        string activate = _isMoving ? "Spell2_ActivateRun" : "Spell2_ActivateIdle";
        if (_model.HasClip(activate))
        {
            _model.Play(activate, false, true);
            yield return new WaitForSeconds(_model.GetClipLength(activate, 0.25f));
        }
        string inClip = null;
        if (!_isMoving)
        {
            _wIdleInAlt = !_wIdleInAlt;
            if (_wIdleInAlt && _model.HasClip("Spell2_IdleIn2")) inClip = "Spell2_IdleIn2";
            else if (_model.HasClip("Spell2_IdleIn")) inClip = "Spell2_IdleIn";
            else if (_model.HasClip("Spell2_IdleIn2")) inClip = "Spell2_IdleIn2";
        }
        if (_wArmed && !string.IsNullOrEmpty(inClip))
        {
            _model.Play(inClip, false, true);
            yield return new WaitForSeconds(_model.GetClipLength(inClip, 0.2f));
        }
        if (_model != null && _wArmed && !_deathLatched) _model.PlayBase(ResolveLocomotionClip(), true, true);
        _animationSequence = null;
    }

    private IEnumerator PlayWDeactivateSequence()
    {
        _wDeactivateAlt = !_wDeactivateAlt;
        string clip = _wDeactivateAlt && _model.HasClip("Darius_Skin15_Spell2_Deactivate.anm")
            ? "Darius_Skin15_Spell2_Deactivate.anm"
            : "Spell2_Deactivate";
        if (!_model.HasClip(clip)) clip = _model.HasClip("Spell2_Deactivate") ? "Spell2_Deactivate" : "Darius_Skin15_Spell2_Deactivate.anm";
        _model.Play(clip, false, true);
        yield return new WaitForSeconds(_model.GetClipLength(clip, 0.2f));
        if (_model != null && !_deathLatched) _model.PlayBase(ResolveLocomotionClip(), true, true);
        _animationSequence = null;
    }
}