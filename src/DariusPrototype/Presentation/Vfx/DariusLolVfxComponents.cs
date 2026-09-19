using System.Collections.Generic;
using UnityEngine;

// Small MonoBehaviour components used by DariusLolVfxRuntime to host per-effect lifetime behavior.
public sealed class DariusLolVfxLinkedObjects : MonoBehaviour
{
    private readonly List<GameObject> _linked = new List<GameObject>();
    private readonly List<Material> _materials = new List<Material>();
    public void Add(GameObject go) { if (go != null && !_linked.Contains(go)) _linked.Add(go); }
    public void Add(Material material) { if (material != null && !_materials.Contains(material)) _materials.Add(material); }
    private void OnDestroy()
    {
        for (int i = 0; i < _linked.Count; i++)
        {
            GameObject go = _linked[i];
            if (go != null) { try { UnityEngine.Object.Destroy(go); } catch { } }
        }
        _linked.Clear();
        for (int i = 0; i < _materials.Count; i++)
        {
            Material material = _materials[i];
            if (material != null) { try { UnityEngine.Object.Destroy(material); } catch { } }
        }
        _materials.Clear();
    }
}

public sealed class DariusLolVfxTrailFollower : MonoBehaviour
{
    public Transform target;
    public TrailRenderer trail;
    public float startDelay;
    public float emitDuration = -1f;
    public bool motionGated;
    public float motionThreshold = 0.0025f;
    private float _startedAt;
    private Vector3 _lastTargetPosition;
    private Quaternion _lastTargetRotation;
    private bool _haveLastPose;

    private void Awake() { _startedAt = Time.time; }
    private void OnEnable() { _startedAt = Time.time; _haveLastPose = false; if (trail != null) trail.Clear(); }
    private void LateUpdate()
    {
        if (target == null) { if (trail != null) trail.emitting = false; return; }
        Vector3 targetPosition = target.position;
        Quaternion targetRotation = target.rotation;
        bool moved = !_haveLastPose || (targetPosition - _lastTargetPosition).sqrMagnitude >= motionThreshold * motionThreshold ||
            Quaternion.Angle(targetRotation, _lastTargetRotation) >= 0.35f;
        transform.position = targetPosition;
        transform.rotation = targetRotation;
        _lastTargetPosition = targetPosition;
        _lastTargetRotation = targetRotation;
        _haveLastPose = true;
        if (trail == null) return;
        float elapsed = Time.time - _startedAt;
        bool withinWindow = elapsed >= startDelay && (emitDuration < 0f || elapsed <= startDelay + emitDuration);
        trail.emitting = withinWindow && (!motionGated || moved);
    }
}

public sealed class DariusLolVfxMaterialColorDriver : MonoBehaviour
{
    public Material material;
    public Gradient gradient;
    public Color multiplier = Color.white;
    public float duration = 1f;
    private float _startedAt;

    private void OnEnable() { _startedAt = Time.time; }
    private void Update()
    {
        if (material == null || gradient == null || duration <= 0f) return;
        float t = Mathf.Clamp01((Time.time - _startedAt) / duration);
        Color c = gradient.Evaluate(t);
        c = new Color(c.r * multiplier.r, c.g * multiplier.g, c.b * multiplier.b, c.a * multiplier.a);
        if (material.HasProperty("_Color")) material.SetColor("_Color", c);
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", c);
        if (material.HasProperty("_TintColor")) material.SetColor("_TintColor", c);
        if (t >= 1f) enabled = false;
    }
}

// Riot mesh emitters frequently animate UVs directly instead of using a discrete sprite sheet.
// Mecha Darius' R weapon streaks are a prominent example: ignoring birthUVOffset/scroll caused
// the broad white region of the source mask to cover the entire long slash mesh. Each emitter
// receives its own material clone only when these UV semantics are present.
public sealed class DariusLolVfxMaterialUvDriver : MonoBehaviour
{
    public Material material;
    public Vector2 initialOffset;
    public Vector2 scrollRate;
    private float _startedAt;

    private void OnEnable() { _startedAt = Time.time; Apply(initialOffset); }
    private void Update()
    {
        if (material == null) { enabled = false; return; }
        float elapsed = Mathf.Max(0f, Time.time - _startedAt);
        Apply(initialOffset + scrollRate * elapsed);
    }
    private void Apply(Vector2 offset)
    {
        if (material == null) return;
        material.mainTextureOffset = offset;
        if (material.HasProperty("_BaseMap")) material.SetTextureOffset("_BaseMap", offset);
    }
}

// Short world-space target trails (for example Mecha E's Trail_BLend) are authored against a
// moving endpoint. A static world root cannot produce that path, so move a temporary attachment
// root along the same displacement interval and let the normal Riot TrailRenderer conversion
// record the path.
public sealed class DariusLolVfxEndpointMover : MonoBehaviour
{
    public Vector3 from;
    public Vector3 to;
    public float duration = 0.22f;
    public float destroyDelay = 2.5f;
    private float _startedAt;
    private void OnEnable() { _startedAt = Time.time; }
    private void Update()
    {
        float d = Mathf.Max(0.01f, duration);
        float t = Mathf.Clamp01((Time.time - _startedAt) / d);
        transform.position = Vector3.LerpUnclamped(from, to, t);
        if (t >= 1f) enabled = false;
    }
    private void Start() { UnityEngine.Object.Destroy(gameObject, Mathf.Max(duration + 0.5f, destroyDelay)); }
}

// Riot Q ring ArbitraryQuad layers author their sweep as Y-axis rotational velocity on the
// emitter. Unity's ParticleSystem mesh rotation interprets that basis differently after the
// source quad is laid onto the XZ ground plane, so the bright quarter of the ring stayed at a
// fixed angle. Drive the converted emitter transform itself with the authored Riot angular
// velocity. Because the particles simulate in Local space, the whole original Riot texture
// rotates around Darius and remains synchronized with the axe-spin beat.
public sealed class DariusLolVfxAuthoredTransformSpin : MonoBehaviour
{
    public float degreesPerSecondY;
    public float startDelay;
    public float activeDuration = -1f;
    private float _startedAt;

    private void OnEnable() { _startedAt = Time.time; }

    private void LateUpdate()
    {
        float elapsed = Time.time - _startedAt;
        if (elapsed < startDelay) return;
        if (activeDuration >= 0f && elapsed > startDelay + activeDuration) { enabled = false; return; }
        if (Mathf.Abs(degreesPerSecondY) <= 0.001f) { enabled = false; return; }
        transform.Rotate(0f, degreesPerSecondY * Time.deltaTime, 0f, Space.Self);
    }
}
