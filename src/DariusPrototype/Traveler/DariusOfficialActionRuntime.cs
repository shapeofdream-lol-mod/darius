using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Fresh EntityModel action presentation.
//
// SoD's stock AnimatorController remains authoritative for Idle/Run. Combat actions are sampled
// from the skin's own native Unity clips in LateUpdate, after stock locomotion has evaluated.
// Before sampling an action we capture the real leg-chain pose produced by stock locomotion and
// restore only those lower-body transforms afterwards. This preserves the main-branch invariant:
// Root/Pelvis/torso/weapon keep the authored combat action while movement continues to animate legs.
public sealed class DariusOfficialActionRuntime : MonoBehaviour
{
    [SerializeField] private Animator _animator;
    [SerializeField] private string[] _clipNames;
    [SerializeField] private AnimationClip[] _clips;

    private readonly Dictionary<string, AnimationClip> _clipMap =
        new Dictionary<string, AnimationClip>(StringComparer.OrdinalIgnoreCase);

    private Hero_Darius _hero;
    private DariusSkinModelBinding _binding;
    private Transform[] _lowerBodyNodes = Array.Empty<Transform>();
    private Vector3[] _lowerBodyPositions = Array.Empty<Vector3>();
    private Quaternion[] _lowerBodyRotations = Array.Empty<Quaternion>();
    private Vector3[] _lowerBodyScales = Array.Empty<Vector3>();

    private AnimationClip _actionClip;
    private float _actionTime;
    private float _actionSpeed = 1f;
    private Coroutine _sequence;

    public bool IsReady
    {
        get { return _animator != null && _binding != null && _clipMap.Count != 0; }
    }

    public void ConfigureTemplate(Animator animator, AnimationClip[] clips)
    {
        _animator = animator;
        if (clips == null)
        {
            _clipNames = Array.Empty<string>();
            _clips = Array.Empty<AnimationClip>();
            return;
        }

        List<string> names = new List<string>(clips.Length);
        List<AnimationClip> values = new List<AnimationClip>(clips.Length);
        HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < clips.Length; i++)
        {
            AnimationClip clip = clips[i];
            if (clip == null || string.IsNullOrEmpty(clip.name) || !seen.Add(clip.name)) continue;
            names.Add(clip.name);
            values.Add(clip);
        }
        _clipNames = names.ToArray();
        _clips = values.ToArray();
    }

    public void Bind(Hero_Darius hero)
    {
        _hero = hero;
        _binding = GetComponent<DariusSkinModelBinding>();
        if (_animator == null) _animator = GetComponentInChildren<Animator>(true);
        BuildClipMap();
        BuildLowerBodyMap();

        DariusLog.Info("OFFICIAL-ACTION",
            "Bound native action overlay skin=" + (_binding != null ? _binding.variantKey : "<null>") +
            " animator=" + (_animator != null ? _animator.gameObject.name : "<null>") +
            " clips=" + _clipMap.Count +
            " lowerBodyNodes=" + _lowerBodyNodes.Length);
    }

    public bool PlayQ(bool instant)
    {
        if (!IsReady || _binding == null) return false;
        RestartSequence(PlayQSequence(instant));
        return true;
    }

    public bool PlayAttack(bool alternate, bool critical)
    {
        if (!IsReady || _binding == null) return false;
        string name = critical ? _binding.critClip : (alternate ? _binding.attack2Clip : _binding.attack1Clip);
        AnimationClip clip = FindClip(name);
        if (clip == null) return false;
        RestartSequence(PlayAttackSequence(clip));
        return true;
    }

    public void StopAction()
    {
        if (_sequence != null)
        {
            StopCoroutine(_sequence);
            _sequence = null;
        }
        ClearAction();
    }

    private IEnumerator PlayQSequence(bool instant)
    {
        AnimationClip q = FindClip(_binding.qClip);
        if (q == null)
        {
            DariusLog.Warn("OFFICIAL-ACTION", "Q clip missing skin=" + _binding.variantKey + " clip=" + _binding.qClip);
            _sequence = null;
            yield break;
        }

        if (instant)
        {
            float raw = Mathf.Max(0.05f, q.length);
            float duration = Mathf.Clamp(raw, 0.42f, 0.70f);
            StartAction(q, raw / Mathf.Max(0.05f, duration));
            yield return new WaitForSeconds(duration);
            ClearAction();
            _sequence = null;
            yield break;
        }

        AnimationClip intro = FindClip(_binding.qIntroClip);
        const float swingLead = 0.08f;
        float windupToSwing = Mathf.Max(0.05f, Ai_Darius_Decimate.Windup - swingLead);

        if (intro != null)
        {
            StartAction(intro, 1f);
            yield return new WaitForSeconds(windupToSwing);
            StartAction(q, 1f);
            yield return new WaitForSeconds(Mathf.Max(0.05f, q.length));
        }
        else
        {
            float raw = Mathf.Max(0.05f, q.length);
            float duration = Mathf.Max(0.55f, Ai_Darius_Decimate.Windup + 0.20f);
            StartAction(q, raw / duration);
            yield return new WaitForSeconds(duration);
        }

        ClearAction();
        _sequence = null;
    }

    private IEnumerator PlayAttackSequence(AnimationClip clip)
    {
        float raw = Mathf.Max(0.05f, clip.length);
        const float duration = 0.82f;
        StartAction(clip, raw / duration);
        yield return new WaitForSeconds(duration);
        ClearAction();
        _sequence = null;
    }

    private void RestartSequence(IEnumerator routine)
    {
        if (_sequence != null) StopCoroutine(_sequence);
        ClearAction();
        _sequence = StartCoroutine(routine);
    }

    private void StartAction(AnimationClip clip, float speed)
    {
        _actionClip = clip;
        _actionTime = 0f;
        _actionSpeed = Mathf.Max(0.05f, speed);
        DariusLog.DebugInfo("OFFICIAL-ACTION",
            "Start skin=" + (_binding != null ? _binding.variantKey : "<null>") +
            " clip=" + (clip != null ? clip.name : "<null>") +
            " speed=" + _actionSpeed.ToString("0.###"));
    }

    private void ClearAction()
    {
        _actionClip = null;
        _actionTime = 0f;
        _actionSpeed = 1f;
    }

    private void LateUpdate()
    {
        AnimationClip clip = _actionClip;
        if (clip == null || _animator == null || !_animator.gameObject.activeInHierarchy) return;

        _actionTime += Mathf.Max(0f, Time.deltaTime) * _actionSpeed;
        float sampleTime = Mathf.Clamp(_actionTime, 0f, Mathf.Max(0.001f, clip.length));

        CaptureLowerBodyPose();

        Transform animatorTransform = _animator.transform;
        Vector3 rootPosition = animatorTransform.localPosition;
        Quaternion rootRotation = animatorTransform.localRotation;
        Vector3 rootScale = animatorTransform.localScale;

        try
        {
            // These clips were imported for this exact native prefab hierarchy, so direct Unity
            // sampling keeps every authored action bone/weapon track without the generic retargeter.
            clip.SampleAnimation(_animator.gameObject, sampleTime);
        }
        catch (Exception e)
        {
            DariusLog.Exception("OFFICIAL-ACTION", e,
                "Native clip sampling failed skin=" + (_binding != null ? _binding.variantKey : "<null>") +
                " clip=" + clip.name);
            ClearAction();
            return;
        }

        // Never let authored clip root curves move the Model GameObject itself. Internal Root/Pelvis
        // bone curves remain untouched; only SoD owns world/model placement.
        animatorTransform.localPosition = rootPosition;
        animatorTransform.localRotation = rootRotation;
        animatorTransform.localScale = rootScale;

        RestoreLowerBodyPose();
    }

    private void BuildClipMap()
    {
        _clipMap.Clear();
        int count = Math.Min(_clipNames != null ? _clipNames.Length : 0, _clips != null ? _clips.Length : 0);
        for (int i = 0; i < count; i++)
        {
            string name = _clipNames[i];
            AnimationClip clip = _clips[i];
            if (string.IsNullOrEmpty(name) || clip == null || _clipMap.ContainsKey(name)) continue;
            _clipMap.Add(name, clip);
        }
    }

    private AnimationClip FindClip(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        AnimationClip clip;
        return _clipMap.TryGetValue(name, out clip) ? clip : null;
    }

    private void BuildLowerBodyMap()
    {
        if (_animator == null)
        {
            _lowerBodyNodes = Array.Empty<Transform>();
            ResizeLowerBodyBuffers(0);
            return;
        }

        Transform[] all = _animator.GetComponentsInChildren<Transform>(true);
        List<Transform> lower = new List<Transform>();
        for (int i = 0; i < all.Length; i++)
        {
            Transform t = all[i];
            if (t != null && IsLowerBodyLocomotionNode(t.name)) lower.Add(t);
        }
        _lowerBodyNodes = lower.ToArray();
        ResizeLowerBodyBuffers(_lowerBodyNodes.Length);
    }

    private void ResizeLowerBodyBuffers(int count)
    {
        _lowerBodyPositions = new Vector3[count];
        _lowerBodyRotations = new Quaternion[count];
        _lowerBodyScales = new Vector3[count];
    }

    private void CaptureLowerBodyPose()
    {
        for (int i = 0; i < _lowerBodyNodes.Length; i++)
        {
            Transform t = _lowerBodyNodes[i];
            if (t == null) continue;
            _lowerBodyPositions[i] = t.localPosition;
            _lowerBodyRotations[i] = t.localRotation;
            _lowerBodyScales[i] = t.localScale;
        }
    }

    private void RestoreLowerBodyPose()
    {
        for (int i = 0; i < _lowerBodyNodes.Length; i++)
        {
            Transform t = _lowerBodyNodes[i];
            if (t == null) continue;
            t.localPosition = _lowerBodyPositions[i];
            t.localRotation = _lowerBodyRotations[i];
            t.localScale = _lowerBodyScales[i];
        }
    }

    private static bool IsLowerBodyLocomotionNode(string nodeName)
    {
        string n = (nodeName ?? string.Empty).ToLowerInvariant();
        if (n.Contains("weapon") || n.Contains("buffbone") || n.Contains("ground_loc") ||
            n.Contains("glb_foot_loc") || n.Contains("snap_") || n.Contains("doll")) return false;

        return n.Contains("hip") || n.Contains("knee") || n.Contains("leg") ||
               n.Contains("thigh") || n.Contains("calf") || n.Contains("foot") ||
               n.Contains("toe");
    }

    private void OnDisable()
    {
        StopAction();
    }
}
