// Drives short-lived textured quads without requiring an AssetBundle prefab.
public sealed class DariusVfxQuadMotion : MonoBehaviour
{
    public float lifetime = 0.4f;
    public float rotateDegreesPerSecond;
    public Vector3 startScale = Vector3.one;
    public Vector3 endScale = Vector3.one;
    public Transform follow;
    public Vector3 followOffset;
    public bool billboard;
    private float _born;
    private Renderer _renderer;
    private Color _initialColor = Color.white;

    private void Awake()
    {
        _born = Time.time;
        _renderer = GetComponent<Renderer>();
        if (_renderer != null && _renderer.material != null) _initialColor = _renderer.material.color;
    }

    private void Update()
    {
        float t = lifetime <= 0.001f ? 1f : Mathf.Clamp01((Time.time - _born) / lifetime);
        if (follow != null) transform.position = follow.position + followOffset;
        if (billboard && Camera.main != null)
        {
            Vector3 dir = Camera.main.transform.position - transform.position;
            if (dir.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(-dir.normalized, Vector3.up);
        }
        else if (Mathf.Abs(rotateDegreesPerSecond) > 0.01f)
        {
            transform.Rotate(0f, rotateDegreesPerSecond * Time.deltaTime, 0f, Space.World);
        }
        transform.localScale = Vector3.Lerp(startScale, endScale, t);
        if (_renderer != null && _renderer.material != null)
        {
            Color c = _initialColor;
            c.a *= 1f - t;
            _renderer.material.color = c;
        }
        if (t >= 1f) Destroy(gameObject);
    }
}

// Persistent follow/pulse driver used for W's armed weapon glow and Noxian Might.
// Lifetime is controlled by the gameplay runtime so the visual cannot disappear before the state does.
public sealed class DariusPersistentVfxPulse : MonoBehaviour
{
    public Transform follow;
    public Vector3 followOffset;
    public Vector3 baseScale = Vector3.one;
    public float pulseAmount = 0.06f;
    public float pulseSpeed = 6.0f;
    public float rotateDegreesPerSecond;
    public bool billboard;
    private Renderer _renderer;
    private Color _baseColor = Color.white;

    private void Awake()
    {
        _renderer = GetComponent<Renderer>();
        if (_renderer != null && _renderer.material != null) _baseColor = _renderer.material.color;
    }

    private void Update()
    {
        if (follow == null)
        {
            Destroy(gameObject);
            return;
        }
        transform.position = follow.position + followOffset;
        if (billboard && Camera.main != null)
        {
            Vector3 dir = Camera.main.transform.position - transform.position;
            if (dir.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(-dir.normalized, Vector3.up);
        }
        else if (Mathf.Abs(rotateDegreesPerSecond) > 0.01f)
        {
            transform.Rotate(0f, rotateDegreesPerSecond * Time.deltaTime, 0f, Space.World);
        }
        float pulse = 1f + Mathf.Sin(Time.time * pulseSpeed) * pulseAmount;
        transform.localScale = baseScale * pulse;
        if (_renderer != null && _renderer.material != null)
        {
            Color c = _baseColor;
            c.a *= 0.88f + 0.12f * Mathf.Sin(Time.time * pulseSpeed * 0.67f);
            _renderer.material.color = c;
        }
    }
}

// Presentation controller lives on the Noxian Might VFX root. It deliberately does not own the
// gameplay buff; destroying the root only removes presentation. Screen-edge feedback is local-only.
public sealed class DariusNoxianMightPresentation : MonoBehaviour
{
    public Hero owner;
    public Light bodyLight;
    public float baseLightIntensity = 1.65f;
    public Color edgeColor = new Color(0.92f, 0.015f, 0.01f, 1f);

    private void Update()
    {
        if (bodyLight != null)
        {
            float pulse = 0.88f + 0.12f * Mathf.Sin(Time.unscaledTime * 7.0f);
            bodyLight.intensity = baseLightIntensity * pulse;
        }
    }

    private bool IsLocalOwner()
    {
        if (owner == null) return false;
        try { return DewPlayer.local != null && DewPlayer.local.hero == owner; }
        catch { return false; }
    }

    private void OnGUI()
    {
        if (!IsLocalOwner() || Event.current == null || Event.current.type != EventType.Repaint) return;

        float pulse = 0.88f + 0.12f * Mathf.Sin(Time.unscaledTime * 5.3f);
        float depth = Mathf.Clamp(Mathf.Min(Screen.width, Screen.height) * 0.082f, 44f, 92f);
        const int bands = 12;
        float band = depth / bands;
        Color old = GUI.color;
        Texture2D white = Texture2D.whiteTexture;

        // Multiple translucent bands approximate a soft vignette without relying on a UI prefab,
        // Canvas hierarchy or post-processing stack that may be rebuilt between rooms.
        for (int i = 0; i < bands; i++)
        {
            float t = i / (float)(bands - 1);
            float alpha = (0.23f * pulse) * (1f - t) * (1f - t);
            GUI.color = new Color(edgeColor.r, edgeColor.g, edgeColor.b, alpha);
            float inset = i * band;
            GUI.DrawTexture(new Rect(0f, inset, Screen.width, band + 1f), white);
            GUI.DrawTexture(new Rect(0f, Screen.height - inset - band - 1f, Screen.width, band + 1f), white);
            GUI.DrawTexture(new Rect(inset, 0f, band + 1f, Screen.height), white);
            GUI.DrawTexture(new Rect(Screen.width - inset - band - 1f, 0f, band + 1f, Screen.height), white);
        }
        GUI.color = old;
    }
}