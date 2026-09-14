public sealed partial class DariusGlbRuntimeModel
{
    private void CaptureBlendFromCurrentPose(float duration)
    {
        if (_nodes == null || duration <= 0.001f)
        {
            _blendDuration = 0f;
            _blendTime = 0f;
            return;
        }
        int count = _nodes.Length;
        if (_blendFromPos == null || _blendFromPos.Length != count)
        {
            _blendFromPos = new Vector3[count];
            _blendFromRot = new Quaternion[count];
            _blendFromScale = new Vector3[count];
        }
        for (int i = 0; i < count; i++)
        {
            Transform t = _nodes[i];
            if (t == null) continue;
            _blendFromPos[i] = t.localPosition;
            _blendFromRot[i] = t.localRotation;
            _blendFromScale[i] = t.localScale;
        }
        _blendTime = 0f;
        _blendDuration = Mathf.Max(0.01f, duration);
    }

    public void Tick(float dt)
    {
        float step = Mathf.Max(0f, dt);
        if (_lowerLocomotionActive && _lowerLocomotion != null && _lowerLocomotion.length > 0f)
        {
            float gaitDirection = Mathf.Abs(_lowerTargetYaw) > 125f ? -1f : 1f;
            _lowerLocomotionTime = Mathf.Repeat(_lowerLocomotionTime + step * _lowerLocomotionSpeed * gaitDirection,
                _lowerLocomotion.length);
            float directionalYaw = Mathf.Abs(_lowerTargetYaw) > 125f ? 0f : Mathf.Clamp(_lowerTargetYaw, -70f, 70f);
            _lowerAppliedYaw = Mathf.MoveTowardsAngle(_lowerAppliedYaw, directionalYaw, step * 540f);
        }
        else
        {
            _lowerAppliedYaw = Mathf.MoveTowardsAngle(_lowerAppliedYaw, 0f, step * 540f);
        }
        if (_blendDuration > 0f) _blendTime = Mathf.Min(_blendDuration, _blendTime + step);

        if (_current != null && _current.length > 0f)
        {
            _time += step * _basePlaybackSpeed;
            if (_loop)
            {
                _time = Mathf.Repeat(_time, _current.length);
            }
            else if (_time >= _current.length)
            {
                _time = _current.length;
                if (_holdBaseAtEnd)
                {
                    // Keep the terminal frame authoritative. This is used by the Darius Death clip;
                    // the hero lifecycle will explicitly switch back to Idle after a real revive.
                    AdvanceUpper(step, false);
                    ApplyCompositePose();
                    return;
                }

                // Apply the final base pose this frame, then release the clip. The owner will choose
                // the appropriate locomotion base on its following Update.
                RuntimeClip finished = _current;
                float finishedTime = _time;
                _current = null;
                _time = 0f;
                ApplyRestPose();
                ApplyClip(finished, finishedTime, false);
                if (_upper != null) ApplyClip(_upper, _upperTime, true, _upperUsesLocomotionMask);
                if (_lowerLocomotionActive && _lowerLocomotion != null)
                    ApplyClip(_lowerLocomotion, _lowerLocomotionTime, false, false, true);
                AdvanceUpper(step, false);
                return;
            }
        }

        AdvanceUpper(step, true);
        ApplyCompositePose();
    }

    private void AdvanceUpper(float dt, bool allowAdvance)
    {
        if (_upper == null || _upper.length <= 0f) return;
        if (allowAdvance) _upperTime += dt * _upperPlaybackSpeed;
        if (_upperTime >= _upper.length)
        {
            _upper = null;
            _upperTime = 0f;
            _upperUsesLocomotionMask = false;
        }
    }

    private void ApplyCompositePose()
    {
        ApplyRestPose();
        if (_current != null) ApplyClip(_current, _time, false);
        if (_upper != null) ApplyClip(_upper, _upperTime, true, _upperUsesLocomotionMask);
        if (_lowerLocomotionActive && _lowerLocomotion != null)
            ApplyClip(_lowerLocomotion, _lowerLocomotionTime, false, false, true);
        ApplyTransitionBlend();
        EnforcePoseSafety();
    }

    // Final invariant after animation sampling AND crossfade. A malformed source frame or a blend
    // captured from an unsafe pose must never be able to leave the skinned humanoid collapsed.
    private void EnforcePoseSafety()
    {
        if (_nodes == null) return;
        for (int i = 0; i < _nodes.Length; i++)
        {
            Transform n = _nodes[i];
            if (n == null) continue;
            if (IsCoreBodyScaleNode(i)) n.localScale = _baseScale[i];

            Vector3 p = n.localPosition;
            if (!IsFinite(p) || p.sqrMagnitude > 250000f) n.localPosition = _basePos[i];
            Quaternion q = n.localRotation;
            if (float.IsNaN(q.x) || float.IsNaN(q.y) || float.IsNaN(q.z) || float.IsNaN(q.w) ||
                float.IsInfinity(q.x) || float.IsInfinity(q.y) || float.IsInfinity(q.z) || float.IsInfinity(q.w))
                n.localRotation = _baseRot[i];
        }
    }

    private void ApplyTransitionBlend()
    {
        if (_blendDuration <= 0f || _nodes == null || _blendFromPos == null) return;
        float t = Mathf.Clamp01(_blendTime / _blendDuration);
        // SmoothStep reduces the visible knee/weapon snap at attack -> run and run -> attack boundaries.
        t = t * t * (3f - 2f * t);
        for (int i = 0; i < _nodes.Length; i++)
        {
            Transform n = _nodes[i];
            if (n == null) continue;
            n.localPosition = Vector3.Lerp(_blendFromPos[i], n.localPosition, t);
            n.localRotation = Quaternion.Slerp(_blendFromRot[i], n.localRotation, t);
            n.localScale = Vector3.Lerp(_blendFromScale[i], n.localScale, t);
        }
        if (t >= 0.999f)
        {
            _blendDuration = 0f;
            _blendTime = 0f;
        }
    }

    private void ApplyRestPose()
    {
        if (_nodes == null) return;
        for (int i = 0; i < _nodes.Length; i++)
        {
            if (_nodes[i] == null) continue;
            _nodes[i].localPosition = _basePos[i];
            _nodes[i].localRotation = _baseRot[i];
            _nodes[i].localScale = _baseScale[i];
        }
    }
}