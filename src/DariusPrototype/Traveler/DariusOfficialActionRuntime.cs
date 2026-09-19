using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Fresh EntityModel action presentation.
//
// SoD's stock AnimatorController remains authoritative for Idle/Run. Darius combat actions are
// sampled from each skin's own native Unity clips in LateUpdate after stock locomotion evaluated.
// The stock leg-chain pose is captured before action sampling and restored afterwards, matching the
// main-branch ownership rule without restoring the raw GLB runtime player.
public sealed class DariusOfficialActionRuntime : MonoBehaviour
{
    private const float MovementThreshold = 0.12f;

    [SerializeField] private Animator _animator;
    [SerializeField] private string[] _clipNames;
    [SerializeField] private AnimationClip[] _clips;

    private readonly Dictionary<string, AnimationClip> _clipMap =
        new Dictionary<string, AnimationClip>(StringComparer.OrdinalIgnoreCase);

    private Hero_Darius _hero;
    private DariusSkinModelBinding _binding;
    private DariusNativeSkinProfile _profile;

    private Transform[] _lowerBodyNodes = Array.Empty<Transform>();
    private Vector3[] _lowerBodyPositions = Array.Empty<Vector3>();
    private Quaternion[] _lowerBodyRotations = Array.Empty<Quaternion>();
    private Vector3[] _lowerBodyScales = Array.Empty<Vector3>();
    private Renderer[] _godKingWolfRenderers = Array.Empty<Renderer>();

    private AnimationClip _actionClip;
    private float _actionTime;
    private float _actionSpeed = 1f;
    private AnimationClip _persistentClip;
    private float _persistentTime;
    private Coroutine _sequence;

    private Vector3 _lastMovementPosition;
    private float _lastMovementSampleAt;
    private bool _moving;

    private bool _wArmed;
    private bool _wSwingActive;
    private bool _wIdleInAlt;
    private bool _wDeactivateAlt;

    private Transform _facingPivot;
    private bool _attackFacingActive;
    private Vector3 _attackFacingDirection = Vector3.forward;
    private int _attackFacingSerial;

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

        // Fail during skin registration rather than silently degrading to full-body action
        // sampling at runtime if a skin's native skeleton no longer matches the lower-body contract.
        BuildLowerBodyMap();
        if (_lowerBodyNodes.Length == 0)
            throw new InvalidOperationException("Fresh action template has no lower-body locomotion nodes.");
    }

    public void Bind(Hero_Darius hero)
    {
        _hero = hero;
        _binding = GetComponent<DariusSkinModelBinding>();
        _profile = _binding != null ? DariusNativeSkinProfiles.Find(_binding.variantKey) : null;
        if (_animator == null) _animator = GetComponentInChildren<Animator>(true);
        BuildClipMap();
        BuildLowerBodyMap();
        BuildGodKingWolfMap();
        BuildFacingPivot();

        if (_hero != null)
        {
            _lastMovementPosition = _hero.transform.position;
            _lastMovementSampleAt = Time.time;
        }

        if (_lowerBodyNodes.Length == 0)
            DariusLog.Error("OFFICIAL-ACTION", "No lower-body locomotion nodes resolved skin=" +
                (_binding != null ? _binding.variantKey : "<null>"));

        DariusLog.Info("OFFICIAL-ACTION",
            "Bound native action overlay skin=" + (_binding != null ? _binding.variantKey : "<null>") +
            " animator=" + (_animator != null ? _animator.gameObject.name : "<null>") +
            " clips=" + _clipMap.Count +
            " lowerBodyNodes=" + _lowerBodyNodes.Length +
            " godKingWolves=" + _godKingWolfRenderers.Length);
    }

    public bool PlayQ(bool instant)
    {
        if (!IsReady || _binding == null) return false;
        RestartSequence(PlayQSequence(instant));
        return true;
    }

    public bool PlayAttack(bool alternate, bool critical, Vector3 direction)
    {
        if (!IsReady || _binding == null) return false;
        string actionName = critical ? _binding.critClip : (alternate ? _binding.attack2Clip : _binding.attack1Clip);
        string tailName = critical ? _binding.critToIdleClip :
            (alternate ? _binding.attack2ToIdleClip : _binding.attack1ToIdleClip);
        AnimationClip action = FindClip(actionName);
        if (action == null) return false;
        RestartSequence(PlayAttackSequence(action, tailName, direction));
        return true;
    }

    public bool PlayW(Vector3 direction)
    {
        if (!IsReady || _binding == null) return false;
        AnimationClip action = FindClip(_binding.wClip);
        if (action == null) return false;
        RestartSequence(PlayWAttackSequence(action, direction));
        return true;
    }

    public bool SetWArmed(bool armed)
    {
        if (!IsReady) return false;
        if (_wArmed == armed && (armed || !_wSwingActive)) return true;

        _wArmed = armed;
        if (!armed)
        {
            _persistentClip = null;
            _persistentTime = 0f;
            if (_wSwingActive) return true;

            if (IsGodKing)
            {
                RestartSequence(PlayGodKingWDeactivateSequence());
                return true;
            }
            return true;
        }

        if (IsGodKing)
        {
            RestartSequence(PlayGodKingWActivateSequence());
            return true;
        }

        RefreshWPersistentClip();
        return true;
    }

    public bool PlayE()
    {
        if (!IsReady || _binding == null) return false;
        AnimationClip action = FindClip(_binding.eClip);
        if (action == null) return false;
        RestartSequence(PlayLocomotionActionSequence(
            action, _binding.eToIdleClip, _binding.eToRunClip, false));
        return true;
    }

    public bool PlayR()
    {
        if (!IsReady || _binding == null) return false;
        AnimationClip action = FindClip(_binding.rClip);
        if (action == null) return false;
        RestartSequence(PlayLocomotionActionSequence(
            action, null, _binding.rToRunClip, IsGodKing));
        return true;
    }

    public void StopAction()
    {
        if (_sequence != null)
        {
            StopCoroutine(_sequence);
            _sequence = null;
        }
        _wSwingActive = false;
        CancelAttackFacing();
        SetGodKingWolfVisible(false);
        ClearAction();
        if (_wArmed) RefreshWPersistentClip();
        else
        {
            _persistentClip = null;
            _persistentTime = 0f;
        }
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
            RestorePersistentAfterAction();
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

        // Preserve the old God-King authored Q tail only when stationary.
        if (IsGodKing && !_moving)
        {
            AnimationClip tail = FindClip("Spell1_ToIdle");
            if (tail != null)
            {
                StartAction(tail, 1f);
                yield return new WaitForSeconds(Mathf.Max(0.05f, tail.length));
            }
        }

        ClearAction();
        RestorePersistentAfterAction();
        _sequence = null;
    }

    private IEnumerator PlayAttackSequence(AnimationClip action, string idleTailName, Vector3 direction)
    {
        int facingSerial = BeginAttackFacing(direction);
        bool movingAtStart = _moving;
        float duration = movingAtStart ? 0.76f : 0.82f;
        float raw = Mathf.Max(0.05f, action.length);
        StartAction(action, raw / duration);

        if (movingAtStart)
        {
            yield return new WaitForSeconds(Mathf.Max(0.08f, duration - 0.08f));
            ClearAction();
            EndAttackFacing(facingSerial);
            RestorePersistentAfterAction();
            _sequence = null;
            yield break;
        }

        yield return new WaitForSeconds(Mathf.Max(0.05f, duration - 0.10f));
        AnimationClip tail = FindClip(idleTailName);
        if (tail != null)
        {
            float tailRaw = Mathf.Max(0.05f, tail.length);
            StartAction(tail, tailRaw / 0.22f);
            yield return new WaitForSeconds(0.08f);
        }

        ClearAction();
        EndAttackFacing(facingSerial);
        RestorePersistentAfterAction();
        _sequence = null;
    }

    private IEnumerator PlayWAttackSequence(AnimationClip action, Vector3 direction)
    {
        int facingSerial = BeginAttackFacing(direction);
        _wSwingActive = true;
        _persistentClip = null;
        _persistentTime = 0f;

        StartAction(action, 1f);
        yield return new WaitForSeconds(Mathf.Max(0.05f, action.length));
        ClearAction();

        _wSwingActive = false;
        EndAttackFacing(facingSerial);
        if (_wArmed)
        {
            RefreshWPersistentClip();
        }
        else if (IsGodKing)
        {
            yield return PlayGodKingWDeactivateBody();
        }

        _sequence = null;
    }

    private IEnumerator PlayLocomotionActionSequence(
        AnimationClip action,
        string idleTailName,
        string runTailName,
        bool showGodKingWolf)
    {
        if (showGodKingWolf) SetGodKingWolfVisible(true);
        StartAction(action, 1f);
        yield return new WaitForSeconds(Mathf.Max(0.05f, action.length));
        if (showGodKingWolf) SetGodKingWolfVisible(false);

        string tailName = _moving ? runTailName : idleTailName;
        AnimationClip tail = FindClip(tailName);
        if (tail != null)
        {
            StartAction(tail, 1f);
            yield return new WaitForSeconds(Mathf.Max(0.05f, tail.length));
        }

        ClearAction();
        RestorePersistentAfterAction();
        _sequence = null;
    }

    private IEnumerator PlayGodKingWActivateSequence()
    {
        _persistentClip = null;
        _persistentTime = 0f;

        string activateName = _moving
            ? (_profile != null ? _profile.WActivateRun : null)
            : (_profile != null ? _profile.WActivateIdle : null);
        AnimationClip activate = FindClip(activateName);
        if (activate != null)
        {
            StartAction(activate, 1f);
            yield return new WaitForSeconds(Mathf.Max(0.05f, activate.length));
        }

        if (!_moving && _wArmed)
        {
            _wIdleInAlt = !_wIdleInAlt;
            string entryName = _profile != null
                ? (_wIdleInAlt ? _profile.WIdleInAlt : _profile.WIdleIn)
                : null;
            AnimationClip entry = FindClip(entryName);
            if (entry != null)
            {
                StartAction(entry, 1f);
                yield return new WaitForSeconds(Mathf.Max(0.05f, entry.length));
            }
        }

        ClearAction();
        if (_wArmed) RefreshWPersistentClip();
        _sequence = null;
    }

    private IEnumerator PlayGodKingWDeactivateSequence()
    {
        yield return PlayGodKingWDeactivateBody();
        _sequence = null;
    }

    private IEnumerator PlayGodKingWDeactivateBody()
    {
        _wDeactivateAlt = !_wDeactivateAlt;
        string name = _profile != null
            ? (_wDeactivateAlt && !string.IsNullOrEmpty(_profile.WDeactivateAlt)
                ? _profile.WDeactivateAlt
                : _profile.WDeactivate)
            : null;
        AnimationClip clip = FindClip(name);
        if (clip != null)
        {
            StartAction(clip, 1f);
            yield return new WaitForSeconds(Mathf.Max(0.05f, clip.length));
        }
        ClearAction();
        _persistentClip = null;
        _persistentTime = 0f;
    }

    private void RestorePersistentAfterAction()
    {
        if (_wArmed) RefreshWPersistentClip();
    }

    private void RefreshWPersistentClip()
    {
        if (!_wArmed)
        {
            _persistentClip = null;
            _persistentTime = 0f;
            return;
        }

        string preferred = _profile != null ? (_moving ? _profile.WRun : _profile.WIdle) : null;
        AnimationClip clip = FindClip(preferred);
        if (clip == null) clip = FindClip(_moving ? "Spell2_Run" : "Spell2_Idle");

        if (clip != _persistentClip)
        {
            _persistentClip = clip;
            _persistentTime = 0f;
            DariusLog.DebugInfo("OFFICIAL-ACTION",
                "W persistent skin=" + (_binding != null ? _binding.variantKey : "<null>") +
                " moving=" + _moving +
                " clip=" + (_persistentClip != null ? _persistentClip.name : "<stock-locomotion>"));
        }
    }

    private void RestartSequence(IEnumerator routine)
    {
        if (_sequence != null) StopCoroutine(_sequence);
        _sequence = null;
        _wSwingActive = false;
        CancelAttackFacing();
        SetGodKingWolfVisible(false);
        ClearAction();
        _persistentClip = null;
        _persistentTime = 0f;
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

    private void Update()
    {
        if (_hero == null || _hero.transform == null) return;

        float now = Time.time;
        float dt = Mathf.Max(0.001f, now - _lastMovementSampleAt);
        Vector3 position = _hero.transform.position;
        Vector3 delta = position - _lastMovementPosition;
        delta.y = 0f;
        bool moving = delta.magnitude / dt > MovementThreshold;
        _lastMovementPosition = position;
        _lastMovementSampleAt = now;

        if (moving == _moving) return;
        _moving = moving;
        if (_wArmed && _actionClip == null && !_wSwingActive)
            RefreshWPersistentClip();
    }

    private void LateUpdate()
    {
        if (_animator == null || !_animator.gameObject.activeInHierarchy) return;

        UpdateFacingPivot();

        AnimationClip clip = _actionClip;
        if (clip != null)
        {
            _actionTime += Mathf.Max(0f, Time.deltaTime) * _actionSpeed;
            float sampleTime = Mathf.Clamp(_actionTime, 0f, Mathf.Max(0.001f, clip.length));
            SampleOverlay(clip, sampleTime);
            return;
        }

        if (_persistentClip != null)
        {
            _persistentTime += Mathf.Max(0f, Time.deltaTime);
            float length = Mathf.Max(0.001f, _persistentClip.length);
            SampleOverlay(_persistentClip, Mathf.Repeat(_persistentTime, length));
        }
    }

    private void SampleOverlay(AnimationClip clip, float sampleTime)
    {
        bool preserveLowerBodyLocomotion = _moving;
        if (preserveLowerBodyLocomotion) CaptureLowerBodyPose();

        Transform animatorTransform = _animator.transform;
        Vector3 rootPosition = animatorTransform.localPosition;
        Quaternion rootRotation = animatorTransform.localRotation;
        Vector3 rootScale = animatorTransform.localScale;

        try
        {
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

        // SoD remains authoritative for model/world placement. Internal Root/Pelvis curves from the
        // sampled action are preserved; only the Animator GameObject transform itself is restored.
        animatorTransform.localPosition = rootPosition;
        animatorTransform.localRotation = rootRotation;
        animatorTransform.localScale = rootScale;

        if (preserveLowerBodyLocomotion) RestoreLowerBodyPose();
    }

    private void BuildFacingPivot()
    {
        if (_animator == null || _facingPivot != null) return;

        Transform model = _animator.transform;
        Transform parent = model.parent;
        Vector3 localPosition = model.localPosition;
        Quaternion localRotation = model.localRotation;
        Vector3 localScale = model.localScale;

        GameObject pivotObject = new GameObject("DariusOfficialFacingPivot");
        _facingPivot = pivotObject.transform;
        _facingPivot.SetParent(parent, false);
        _facingPivot.localPosition = Vector3.zero;
        _facingPivot.localRotation = Quaternion.identity;
        _facingPivot.localScale = Vector3.one;

        model.SetParent(_facingPivot, false);
        model.localPosition = localPosition;
        model.localRotation = localRotation;
        model.localScale = localScale;
    }

    private int BeginAttackFacing(Vector3 direction)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.001f && _hero != null) direction = _hero.transform.forward;
        if (direction.sqrMagnitude < 0.001f) direction = Vector3.forward;
        _attackFacingDirection = direction.normalized;
        _attackFacingActive = true;
        return ++_attackFacingSerial;
    }

    private void EndAttackFacing(int serial)
    {
        if (!_attackFacingActive || serial != _attackFacingSerial) return;
        _attackFacingActive = false;
    }

    private void CancelAttackFacing()
    {
        _attackFacingActive = false;
    }

    private void UpdateFacingPivot()
    {
        if (_facingPivot == null) return;

        Quaternion wantedLocal = Quaternion.identity;
        if (_attackFacingActive)
        {
            Vector3 desired = _attackFacingDirection;
            desired.y = 0f;
            if (desired.sqrMagnitude > 0.001f)
            {
                Transform parent = _facingPivot.parent;
                Vector3 localDirection = parent != null ? parent.InverseTransformDirection(desired.normalized) : desired.normalized;
                localDirection.y = 0f;
                if (localDirection.sqrMagnitude > 0.001f)
                    wantedLocal = Quaternion.LookRotation(localDirection.normalized, Vector3.up);
            }
        }

        float responsiveness = _attackFacingActive ? 48f : 22f;
        float t = 1f - Mathf.Exp(-responsiveness * Mathf.Max(Time.deltaTime, 0.0001f));
        _facingPivot.localRotation = Quaternion.Slerp(_facingPivot.localRotation, wantedLocal, t);
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

    private void BuildGodKingWolfMap()
    {
        if (!IsGodKing)
        {
            _godKingWolfRenderers = Array.Empty<Renderer>();
            return;
        }

        Renderer[] all = GetComponentsInChildren<Renderer>(true);
        List<Renderer> wolves = new List<Renderer>();
        for (int i = 0; i < all.Length; i++)
        {
            Renderer renderer = all[i];
            if (renderer == null ||
                !DariusNativeAssetContract.IsWolfHiddenObjectName(renderer.gameObject.name)) continue;
            renderer.enabled = false;
            renderer.gameObject.SetActive(false);
            wolves.Add(renderer);
        }
        _godKingWolfRenderers = wolves.ToArray();
    }

    private void SetGodKingWolfVisible(bool visible)
    {
        for (int i = 0; i < _godKingWolfRenderers.Length; i++)
        {
            Renderer renderer = _godKingWolfRenderers[i];
            if (renderer == null) continue;
            renderer.gameObject.SetActive(visible);
            renderer.enabled = visible;
        }
    }

    private bool IsGodKing
    {
        get { return _binding != null && _binding.isGodKingSkin; }
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
        CancelAttackFacing();
        if (_facingPivot != null) _facingPivot.localRotation = Quaternion.identity;
        _wArmed = false;
        _persistentClip = null;
        _persistentTime = 0f;
        SetGodKingWolfVisible(false);
    }
}
