public sealed partial class DariusGlbRuntimeModel
{
    private void BuildAnimations()
    {
        JArray animations = (JArray)_json["animations"];
        if (animations == null) return;
        for (int ai = 0; ai < animations.Count; ai++)
        {
            JObject a = (JObject)animations[ai];
            RuntimeClip clip = new RuntimeClip { name = (string)a["name"] ?? ("Animation_" + ai) };
            JArray samplers = (JArray)a["samplers"];
            JArray channels = (JArray)a["channels"];

            for (int ci = 0; ci < channels.Count; ci++)
            {
                JObject channel = (JObject)channels[ci];
                int samplerIndex = (int)channel["sampler"];
                JObject sampler = (JObject)samplers[samplerIndex];
                JObject target = (JObject)channel["target"];
                int node = (int)target["node"];
                string path = (string)target["path"];
                int input = (int)sampler["input"];
                int output = (int)sampler["output"];
                float[] times = ReadFloatAccessor(input);
                float[] values = ReadFloatAccessor(output);
                int comps = string.Equals(path, "rotation", StringComparison.Ordinal) ? 4 : 3;
                if (times.Length > 0) clip.length = Mathf.Max(clip.length, times[times.Length - 1]);
                clip.tracks.Add(new RuntimeTrack { node = node, path = path, times = times, values = values, components = comps });
            }
            _clips[clip.name] = clip;
        }
    }

    // The supplied Darius GLB action clips contain authored Root/Pelvis/Spine AND the separate
    // Weapon hierarchy. They are therefore full-body authored clips, not safe upper-body masks.
    // Masking out Root/Pelvis while still applying Weapon makes the axe drift away from the hands.
    // Locomotion uses the base loop layer; attacks and skills temporarily take full-body authority.
    public void Play(string name, bool loop, bool force)
    {
        Play(name, loop, force, 1f, 0.10f);
    }

    public void Play(string name, bool loop, bool force, float playbackSpeed, float blendDuration)
    {
        _upper = null;
        _upperTime = 0f;
        _upperPlaybackSpeed = 1f;
        _upperUsesLocomotionMask = false;
        _holdBaseAtEnd = false;
        SetBase(name, loop, force, playbackSpeed, blendDuration);
    }

    // Death and similar terminal clips need to finish once and remain on their final frame instead
    // of snapping back to BasePose/Idle on the following Update.
    public void PlayAndHold(string name, bool force)
    {
        _lowerLocomotionActive = false;
        _upper = null;
        _upperTime = 0f;
        _upperPlaybackSpeed = 1f;
        _upperUsesLocomotionMask = false;
        _holdBaseAtEnd = true;
        SetBase(name, false, force, 1f, 0.10f);
        DariusLog.DebugInfo("SKIN-ANIM", "Play-and-hold " + name);
    }

    // Locomotion/base layer switch. A short pose crossfade is used instead of hard snapping.
    public void PlayBase(string name, bool loop, bool force)
    {
        PlayBase(name, loop, force, 1f, 0.10f);
    }

    public void PlayBase(string name, bool loop, bool force, float playbackSpeed, float blendDuration)
    {
        _holdBaseAtEnd = false;
        SetBase(name, loop, force, playbackSpeed, blendDuration);
    }

    public void SetLowerBodyLocomotion(string name, bool active, float playbackSpeed, float relativeYaw)
    {
        RuntimeClip clip;
        if (!active || string.IsNullOrEmpty(name) || !_clips.TryGetValue(name, out clip))
        {
            _lowerLocomotionActive = false;
            _lowerTargetYaw = 0f;
            return;
        }
        if (_lowerLocomotion != clip)
        {
            _lowerLocomotion = clip;
            _lowerLocomotionTime = 0f;
        }
        _lowerLocomotionSpeed = Mathf.Max(0.05f, playbackSpeed);
        _lowerTargetYaw = Mathf.DeltaAngle(0f, relativeYaw);
        _lowerLocomotionActive = true;
    }

    private void SetBase(string name, bool loop, bool force, float playbackSpeed, float blendDuration)
    {
        RuntimeClip clip;
        if (!_clips.TryGetValue(name, out clip))
        {
            DariusLog.Warn("SKIN-ANIM", "Animation not found: " + name);
            return;
        }
        float speed = Mathf.Max(0.05f, playbackSpeed);
        if (!force && _current == clip && _loop == loop && Mathf.Abs(_basePlaybackSpeed - speed) < 0.001f) return;
        CaptureBlendFromCurrentPose(blendDuration);
        _current = clip;
        _time = 0f;
        _loop = loop;
        _basePlaybackSpeed = speed;
        ApplyCompositePose();
        DariusLog.DebugInfo("SKIN-ANIM", "Play base " + name + " length=" + clip.length.ToString("0.000") +
            " loop=" + loop + " speed=" + speed.ToString("0.00") + " blend=" + Mathf.Max(0f, blendDuration).ToString("0.00") +
            " upperActive=" + (_upper != null));
    }

    public void PlayUpperBody(string name, bool force)
    {
        RuntimeClip clip;
        if (!_clips.TryGetValue(name, out clip))
        {
            DariusLog.Warn("SKIN-ANIM", "UpperBody animation not found: " + name);
            return;
        }
        if (!force && _upper == clip) return;
        CaptureBlendFromCurrentPose(0.08f);
        _upper = clip;
        _upperTime = 0f;
        _upperPlaybackSpeed = 1f;
        _upperUsesLocomotionMask = false;
        ApplyCompositePose();
        DariusLog.DebugInfo("SKIN-ANIM", "Play UpperBody " + name + " length=" + clip.length.ToString("0.000") + " overBase=" + (_current != null ? _current.name : "<rest>"));
    }

    // Retained only for compatibility with older callers. New character code no longer uses
    // partial-body attack overlays because the exported LoL clips are authored as full-body poses.
    public void PlayLocomotionAction(string name, bool force)
    {
        RuntimeClip clip;
        if (!_clips.TryGetValue(name, out clip))
        {
            DariusLog.Warn("SKIN-ANIM", "Locomotion action not found: " + name);
            return;
        }
        if (!force && _upper == clip && _upperUsesLocomotionMask) return;
        CaptureBlendFromCurrentPose(0.08f);
        _upper = clip;
        _upperTime = 0f;
        _upperPlaybackSpeed = 1f;
        _upperUsesLocomotionMask = true;
        ApplyCompositePose();
        DariusLog.DebugInfo("SKIN-ANIM", "Play locomotion action " + name + " length=" + clip.length.ToString("0.000") +
            " overBase=" + (_current != null ? _current.name : "<rest>"));
    }
}