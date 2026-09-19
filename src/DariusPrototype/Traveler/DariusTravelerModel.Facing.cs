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
    private void LateUpdate()
    {
        if (_facingPivot == null || _hero == null) return;

        Quaternion wantedLocal = Quaternion.identity;
        Vector3 desiredWorld = _attackFacingDirection;
        if (_attackFacingActive)
        {
            desiredWorld.y = 0f;
            if (desiredWorld.sqrMagnitude > 0.001f)
            {
                desiredWorld.Normalize();
                Transform parent = _facingPivot.parent;
                Vector3 localDirection = parent != null ? parent.InverseTransformDirection(desiredWorld) : desiredWorld;
                localDirection.y = 0f;
                if (localDirection.sqrMagnitude > 0.001f)
                    wantedLocal = Quaternion.LookRotation(localDirection.normalized, Vector3.up);
            }
        }

        float responsiveness = _attackFacingActive ? 48f : 22f;
        float t = 1f - Mathf.Exp(-responsiveness * Mathf.Max(Time.deltaTime, 0.0001f));
        _facingPivot.localRotation = Quaternion.Slerp(_facingPivot.localRotation, wantedLocal, t);

        if (_attackFacingActive && Time.unscaledTime >= _nextFacingDiagAt)
        {
            _nextFacingDiagAt = Time.unscaledTime + 0.18f;
            Vector3 heroForward = _hero.transform.forward; heroForward.y = 0f;
            Vector3 visualForward = _facingPivot.forward; visualForward.y = 0f;
            float dot = visualForward.sqrMagnitude > 0.001f && desiredWorld.sqrMagnitude > 0.001f
                ? Vector3.Dot(visualForward.normalized, desiredWorld.normalized) : -2f;
            DariusLog.DebugInfo("FACING-ARB", "attackSerial=" + _attackFacingSerial +
                " heroForward=" + DariusLog.Vec(heroForward) + " desired=" + DariusLog.Vec(desiredWorld) +
                " visualForward=" + DariusLog.Vec(visualForward) + " alignedDot=" + dot.ToString("0.###"));
        }
    }

    private int BeginAttackFacing(Vector3 direction)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.001f && _hero != null) direction = _hero.transform.forward;
        if (direction.sqrMagnitude < 0.001f) direction = Vector3.forward;
        direction.Normalize();
        _attackFacingDirection = direction;
        _attackFacingActive = true;
        int serial = ++_attackFacingSerial;
        DariusLog.DebugInfo("FACING-ARB", "begin attackSerial=" + serial + " desired=" + DariusLog.Vec(direction) +
            " moving=" + _isMoving);
        return serial;
    }

    private void EndAttackFacing(int serial, string reason)
    {
        if (serial != _attackFacingSerial || !_attackFacingActive) return;
        _attackFacingActive = false;
        DariusLog.DebugInfo("FACING-ARB", "end attackSerial=" + serial + " reason=" + reason);
    }

    private void CancelAttackFacing(string reason)
    {
        if (!_attackFacingActive) return;
        int serial = _attackFacingSerial;
        _attackFacingActive = false;
        DariusLog.DebugInfo("FACING-ARB", "cancel attackSerial=" + serial + " reason=" + reason);
    }

    private void ObserveFacingTurn()
    {
        if (!_godKing || _hero == null || _isMoving || _wArmed || _animationSequence != null || _model.isPlayingOneShot) return;
        Vector3 current = _hero.transform.forward;
        current.y = 0f;
        if (current.sqrMagnitude < 0.001f) return;
        current.Normalize();
        float signed = Vector3.SignedAngle(_lastFacing, current, Vector3.up);
        _lastFacing = current;
        if (Mathf.Abs(signed) < 28f || Time.time - _lastTurnAt < 0.45f) return;
        _lastTurnAt = Time.time;
        string clip = signed > 0f ? "TurnR" : "TurnL";
        if (!_model.HasClip(clip)) return;
        _animationSequence = StartCoroutine(PlayTurnSequence(clip));
    }

    private IEnumerator PlayTurnSequence(string clip)
    {
        _model.Play(clip, false, true);
        yield return new WaitForSeconds(_model.GetClipLength(clip, 0.2f));
        if (_model != null && !_deathLatched && _model.HasClip("Turn0"))
        {
            _model.Play("Turn0", false, true);
            yield return new WaitForSeconds(_model.GetClipLength("Turn0", 0.18f));
        }
        if (_model != null && !_deathLatched) _model.Play(IdleClip, true, true);
        _animationSequence = null;
    }

    public bool TryGetRecentMovementDirection(out Vector3 direction)
    {
        direction = _recentMovementDirection;
        if (direction.sqrMagnitude <= 0.001f) return false;
        if (!_isMoving && Time.time - _recentMovementDirectionAt > 0.18f) return false;
        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.001f) return false;
        direction.Normalize();
        return true;
    }
}