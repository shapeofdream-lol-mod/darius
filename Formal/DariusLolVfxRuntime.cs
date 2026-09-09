using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEngine;

// Runtime interpreter for Riot's VfxSystemDefinitionData converted from the user's current
// Darius WAD. This deliberately maps authored LoL emitter parameters instead of redrawing the
// effect from screenshots. Unsupported Riot primitives are logged explicitly rather than being
// replaced by hand-authored imitation VFX.
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

public static class DariusLolVfxRuntime
{
    private const float DefaultCoordinateScale = 0.00921f;
    private static bool _loadAttempted;
    private static JObject _manifest;
    private static JObject _systems;
    private static float _coordinateScale = DefaultCoordinateScale;
    private static readonly Dictionary<string, Texture2D> Textures = new Dictionary<string, Texture2D>(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, Mesh> Meshes = new Dictionary<string, Mesh>(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, Material> Materials = new Dictionary<string, Material>(StringComparer.OrdinalIgnoreCase);
    private static Mesh _quad;

    public static bool Ready { get { EnsureLoaded(); return _systems != null; } }

    private static void EnsureLoaded()
    {
        if (_loadAttempted) return;
        _loadAttempted = true;
        try
        {
            string path = Path.Combine(DariusMedia.Root, "assets", "lol_vfx", "darius_lol_vfx.json");
            if (!File.Exists(path))
            {
                DariusLog.Error("LOL-VFX", "Converted League VFX manifest missing path=" + path);
                return;
            }
            _manifest = JObject.Parse(File.ReadAllText(path));
            _systems = _manifest["systems"] as JObject;
            if (_manifest["coordinateScale"] != null) _coordinateScale = (float)_manifest["coordinateScale"];
            int systemCount = _systems != null ? _systems.Count : 0;
            JObject assets = _manifest["assets"] as JObject;
            int textures = 0, meshes = 0, missing = 0;
            if (assets != null)
            {
                foreach (JProperty p in assets.Properties())
                {
                    JObject a = p.Value as JObject;
                    if (a == null) continue;
                    string kind = (string)a["kind"];
                    bool ok = a["file"] != null && a["file"].Type != JTokenType.Null;
                    if (!ok) { missing++; continue; }
                    if (string.Equals(kind, "texture", StringComparison.OrdinalIgnoreCase)) textures++;
                    else if (string.Equals(kind, "mesh", StringComparison.OrdinalIgnoreCase)) meshes++;
                }
            }
            DariusLog.Info("LOL-VFX", "Converted Riot VFX manifest loaded systems=" + systemCount +
                " textures=" + textures + " meshes=" + meshes + " missingAssets=" + missing +
                " coordinateScale=" + _coordinateScale.ToString("0.#####"));
        }
        catch (Exception e)
        {
            _systems = null;
            DariusLog.Exception("LOL-VFX", e, "Failed loading converted Riot VFX manifest");
        }
    }

    public static GameObject PlayAttached(Hero owner, string systemName, Transform parent)
    {
        Vector3 p = parent != null ? parent.position : (owner != null ? owner.transform.position : Vector3.zero);
        return PlayInternal(owner, systemName, p, Quaternion.identity, parent, true, false);
    }

    // Starts an authored Riot system as though `authoredTimeOffset` seconds of its timeline have
    // already elapsed. This is used by Instant Decimate: the normal Q ring has 0.75-0.85 s delays
    // baked into Riot emitters, but the no-windup constellation must enter the spin phase at once.
    public static GameObject PlayAttachedAtAuthoredTime(Hero owner, string systemName, Transform parent, float authoredTimeOffset)
    {
        Vector3 p = parent != null ? parent.position : (owner != null ? owner.transform.position : Vector3.zero);
        return PlayInternal(owner, systemName, p, Quaternion.identity, parent, true, false, Mathf.Max(0f, authoredTimeOffset));
    }

    public static GameObject PlayAttachedPersistent(Hero owner, string systemName, Transform parent)
    {
        Vector3 p = parent != null ? parent.position : (owner != null ? owner.transform.position : Vector3.zero);
        return PlayInternal(owner, systemName, p, Quaternion.identity, parent, true, true);
    }

    public static GameObject PlayWorld(string systemName, Vector3 position)
    {
        return PlayInternal(null, systemName, position, Quaternion.identity, null, false, false);
    }

    public static GameObject PlayWorld(string systemName, Vector3 position, Quaternion rotation)
    {
        return PlayInternal(null, systemName, position, rotation, null, false, false);
    }

    public static GameObject PlayWorldBetween(Hero owner, string systemName, Vector3 from, Vector3 to, float duration, Quaternion rotation)
    {
        GameObject mover = new GameObject("LOL_VFX_ENDPOINT_" + (string.IsNullOrEmpty(systemName) ? "Unknown" : systemName));
        mover.transform.position = from;
        mover.transform.rotation = Quaternion.identity;
        DariusLolVfxEndpointMover motion = mover.AddComponent<DariusLolVfxEndpointMover>();
        motion.from = from; motion.to = to; motion.duration = Mathf.Max(0.01f, duration); motion.destroyDelay = Mathf.Max(2.5f, duration + 2.0f);
        GameObject root = PlayInternal(owner, systemName, from, rotation, mover.transform, true, false);
        if (root == null) UnityEngine.Object.Destroy(mover);
        else DariusLog.DebugInfo("LOL-VFX-ENDPOINT", "Bound world Riot system to moving endpoint system=" + systemName +
            " from=" + DariusLog.Vec(from) + " to=" + DariusLog.Vec(to) + " duration=" + duration.ToString("0.###"));
        return root;
    }

    public static GameObject Play(string systemName, Vector3 position, Transform parent, bool attached)
    {
        return PlayInternal(null, systemName, position, Quaternion.identity, parent, attached, false);
    }

    private static GameObject PlayInternal(Hero owner, string systemName, Vector3 position, Quaternion rotation, Transform parent, bool attached, bool persistent, float authoredTimeOffset = 0f)
    {
        EnsureLoaded();
        if (_systems == null || string.IsNullOrEmpty(systemName)) return null;
        JObject system = _systems[systemName] as JObject;
        if (system == null)
        {
            foreach (JProperty p in _systems.Properties())
            {
                if (string.Equals(p.Name, systemName, StringComparison.OrdinalIgnoreCase)) { system = p.Value as JObject; systemName = p.Name; break; }
            }
        }
        if (system == null)
        {
            DariusLog.Warn("LOL-VFX", "Converted system not found: " + systemName);
            return null;
        }

        if (systemName.IndexOf("_Q_Ring", StringComparison.OrdinalIgnoreCase) >= 0)
            DariusLog.DebugInfo("LOL-VFX-Q-VISIBILITY", "Applying Q ring readability compositor to system=" + systemName + " (no duplicate lifetime-color multiplication; chroma lift enabled; authored alpha preserved).");

        GameObject root = new GameObject("LOL_VFX_" + systemName);
        root.AddComponent<DariusLolVfxLinkedObjects>();
        if (parent != null)
        {
            root.transform.SetParent(parent, false);
            root.transform.localPosition = Vector3.zero;
            root.transform.localRotation = rotation;
            // GLB bones already inherit the LoL->SoD model scale. Hero transforms do not.
            float lossy = Mathf.Abs(parent.lossyScale.x);
            root.transform.localScale = lossy > 0.00001f && lossy < 0.10f ? Vector3.one : Vector3.one * _coordinateScale;
        }
        else
        {
            root.transform.position = position;
            root.transform.rotation = rotation;
            root.transform.localScale = Vector3.one * _coordinateScale;
        }

        // Mecha Q exposes the small difference between Riot's authored visual radii and the
        // gameplay 4.25 m hit radius.  With coordinateScale=0.00921 the windup AOE quad lands at
        // ~4.283 m while the dominant impact team-ring mesh lands at ~4.080 m.  Normalize the two
        // complete systems to the same 4.25 m boundary instead of changing gameplay collision.
        if (string.Equals(systemName, "Darius_Skin67_Q_RingWindup", StringComparison.OrdinalIgnoreCase))
            root.transform.localScale *= 0.992376f;
        else if (string.Equals(systemName, "Darius_Skin67_Q_Ring", StringComparison.OrdinalIgnoreCase))
            root.transform.localScale *= 1.041622f;

        JArray emitters = system["emitters"] as JArray;
        int built = 0, skipped = 0, disabled = 0;
        float maxLife = 0.25f;
        if (emitters != null)
        {
            for (int i = 0; i < emitters.Count; i++)
            {
                JObject emitter = emitters[i] as JObject;
                if (emitter == null) continue;
                try
                {
                    float life = BuildEmitter(owner, root.transform, emitter, i, persistent, authoredTimeOffset, systemName);
                    if (float.IsNaN(life)) disabled++;
                    else if (life >= 0f) { built++; maxLife = Mathf.Max(maxLife, life); }
                    else skipped++;
                }
                catch (Exception e)
                {
                    skipped++;
                    DariusLog.Exception("LOL-VFX-EMITTER", e, "system=" + systemName + " emitter=" + ((string)emitter["name"] ?? i.ToString()));
                }
            }
        }
        if (!persistent)
        {
            float destroyAfter = Mathf.Clamp(maxLife + 1.0f, 0.5f, 20f);
            // Skin67 E uses several world-space slash/trail layers. Their authored curves can linger
            // for seconds, but after the 0.28 s hook beat that leaves a stale slash behind Darius
            // pointing along the old cast direction.  Keep the cast readable, then clean the old
            // world-space orientation promptly.
            if (string.Equals(systemName, "Darius_Skin67_E_Cast", StringComparison.OrdinalIgnoreCase)) destroyAfter = Mathf.Min(destroyAfter, 0.58f);
            else if (string.Equals(systemName, "Darius_Skin67_E_ClawMarks", StringComparison.OrdinalIgnoreCase)) destroyAfter = Mathf.Min(destroyAfter, 0.34f);
            else if (string.Equals(systemName, "Darius_Skin67_E_AxegrabCollision", StringComparison.OrdinalIgnoreCase)) destroyAfter = Mathf.Min(destroyAfter, 0.30f);
            else if (string.Equals(systemName, "Darius_Skin67_E_Tar02", StringComparison.OrdinalIgnoreCase)) destroyAfter = Mathf.Min(destroyAfter, 0.48f);
            UnityEngine.Object.Destroy(root, destroyAfter);
        }
        DariusLog.Info("LOL-VFX", "Play system=" + systemName + " authoredEmitters=" + (emitters != null ? emitters.Count : 0) +
            " built=" + built + " disabled=" + disabled + " unsupported=" + skipped + " attached=" + attached + " persistent=" + persistent +
            " authoredOffset=" + authoredTimeOffset.ToString("0.###") + " maxLife=" + maxLife.ToString("0.###"));
        return root;
    }

    private static float BuildEmitter(Hero owner, Transform root, JObject e, int index, bool persistent, float authoredTimeOffset, string systemName)
    {
        string name = (string)e["name"] ?? ("Emitter_" + index);
        if (ReadBoolToken(e["disabled"], false))
        {
            DariusLog.DebugInfo("LOL-VFX-DISABLED", "Respecting Riot disabled emitter=" + name);
            return float.NaN;
        }
        string primitive = (string)e["primitive"] ?? "VfxPrimitiveCameraQuad";
        if (ShouldSkipSkin67Emitter(systemName, name))
        {
            DariusLog.DebugInfo("LOL-VFX-SKIN67-FILTER", "Skipping visually-invalid Mecha emitter system=" + systemName + " emitter=" + name);
            return float.NaN;
        }
        bool single = ReadBoolToken(e["single"], false);
        float authoredParticleLifetime = ReadConstantFloat(e["particleLifetime"] as JObject, 0.5f);
        if (primitive.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            DariusLog.Warn("LOL-VFX-UNSUPPORTED", "Skipping unknown Riot primitive=" + primitive + " emitter=" + name);
            return -1f;
        }
        if (string.Equals(primitive, "VfxPrimitiveAttachedMesh", StringComparison.OrdinalIgnoreCase))
        {
            JObject pd = e["primitiveData"] as JObject;
            JObject ad = pd != null ? pd["attachedMesh"] as JObject : null;
            JArray hashes = ad != null ? ad["submeshHashes"] as JArray : null;
            int blend = ReadIntToken(e["blendMode"], 0);
            Material baseMaterial = MaterialFor((string)e["texture"], blend);
            if (baseMaterial == null) return -1f;
            Material overlayMaterial = new Material(baseMaterial);
            overlayMaterial.name = baseMaterial.name + "_Attached_" + name;

            bool skin67Attached = !string.IsNullOrEmpty(systemName) && systemName.StartsWith("Darius_Skin67_", StringComparison.OrdinalIgnoreCase);
            Color birthTint;
            Gradient lifetimeGradient;
            if (skin67Attached)
            {
                birthTint = ReadInitialColor(e["birthColor"] as JObject, Color.white);
                lifetimeGradient = BuildGradient(e["color"] as JObject, Color.white);
                ApplyMaterialColor(overlayMaterial, MultiplyColor(birthTint, lifetimeGradient.Evaluate(0f)));
            }
            else
            {
                // Preserve the already visually-approved pre-0.30.4 AttachedMesh behavior on
                // Classic/God-King/Dunkmaster. The richer birthColor contract is enabled only for
                // Skin67 layers that were previously missing altogether.
                Color legacyTint = ReadInitialColor(e["color"] as JObject, Color.white);
                ApplyMaterialColor(overlayMaterial, legacyTint);
                birthTint = Color.white;
                lifetimeGradient = BuildGradient(e["color"] as JObject, legacyTint);
            }

            // Skin67 AttachedMesh layers (passive flash, Q heal screen lines) also animate UVs.
            // They were previously absent altogether; when restored without these authored UV
            // transforms they can look like a static texture pasted over the avatar. Keep this
            // behavior scoped to Mecha so the already-approved other skins remain unchanged.
            if (skin67Attached)
            {
                Vector2 attachedOffset = ReadConstantVector2(e["birthUVOffset"] as JObject, Vector2.zero);
                Vector2 attachedScroll = ReadConstantVector2(e["birthUvScrollRate"] as JObject, Vector2.zero) +
                    ReadConstantVector2(e["emitterUvScrollRate"] as JObject, Vector2.zero);
                Vector2 attachedScale = Vector2.one;
                JArray attachedDiv = e["texDiv"] as JArray;
                if (attachedDiv != null && attachedDiv.Count >= 2)
                {
                    float dx = Mathf.Abs((float)attachedDiv[0]);
                    float dy = Mathf.Abs((float)attachedDiv[1]);
                    if (dx > 0.0001f && dy > 0.0001f &&
                        (dx < 0.999f || dy < 0.999f || Mathf.Abs(dx - Mathf.Round(dx)) > 0.001f || Mathf.Abs(dy - Mathf.Round(dy)) > 0.001f))
                        attachedScale = new Vector2(1f / dx, 1f / dy);
                }
                overlayMaterial.mainTextureScale = attachedScale;
                overlayMaterial.mainTextureOffset = attachedOffset;
                if (overlayMaterial.HasProperty("_BaseMap"))
                {
                    overlayMaterial.SetTextureScale("_BaseMap", attachedScale);
                    overlayMaterial.SetTextureOffset("_BaseMap", attachedOffset);
                }
                if (attachedScroll.sqrMagnitude > 0.0000001f)
                {
                    DariusLolVfxMaterialUvDriver uv = root.gameObject.AddComponent<DariusLolVfxMaterialUvDriver>();
                    uv.material = overlayMaterial; uv.initialOffset = attachedOffset; uv.scrollRate = attachedScroll;
                }
                if (attachedOffset.sqrMagnitude > 0.0000001f || attachedScroll.sqrMagnitude > 0.0000001f || (attachedScale - Vector2.one).sqrMagnitude > 0.0000001f)
                    DariusLog.DebugInfo("LOL-VFX-UV", "Applied Skin67 AttachedMesh UV semantics system=" + systemName +
                        " emitter=" + name + " scale=(" + attachedScale.x.ToString("0.###") + "," + attachedScale.y.ToString("0.###") +
                        ") offset=(" + attachedOffset.x.ToString("0.###") + "," + attachedOffset.y.ToString("0.###") +
                        ") scroll=(" + attachedScroll.x.ToString("0.###") + "," + attachedScroll.y.ToString("0.###") + ")");
            }

            bool any = false;
            int appliedSubmeshes = 0;
            DariusLolVfxLinkedObjects links = root.GetComponent<DariusLolVfxLinkedObjects>();
            if (owner != null && hashes != null && hashes.Count > 0)
            {
                for (int hi = 0; hi < hashes.Count; hi++)
                {
                    uint hash;
                    if (!uint.TryParse((string)hashes[hi], System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out hash)) continue;
                    GameObject overlay = DariusSkinAnimationHooks.CreateSubmeshOverlay(owner, hash, overlayMaterial);
                    if (overlay != null)
                    {
                        any = true; appliedSubmeshes++;
                        if (links != null) links.Add(overlay);
                    }
                }
            }
            else if (skin67Attached)
            {
                // Skin67 contains several hashless AttachedMesh nodes whose real LoL binding is
                // shader/submesh driven (AvatarFlash, ScreenspaceLines, Temp_Avatar, pauldrons).
                // Treating an empty hash list as "paint every visible character submesh" produced
                // the full-body green grid reported in-game.  Missing one decorative overlay is
                // preferable to corrupting the entire model, so only explicit submesh hashes are
                // rendered until their exact Riot binding contract is known.
                DariusLog.Warn("LOL-VFX-FIDELITY", "Skipping hashless Skin67 AttachedMesh instead of applying a full-avatar overlay emitter=" + name +
                    " system=" + systemName);
            }

            if (!any)
            {
                DariusLog.Warn("LOL-VFX-UNSUPPORTED", "AttachedMesh could not resolve visual target emitter=" + name +
                    " hashes=" + (hashes != null ? hashes.Count : 0));
                UnityEngine.Object.Destroy(overlayMaterial);
                return -1f;
            }
            if (links != null) links.Add(overlayMaterial);
            float attachedLife = EstimateEmitterLife(e, persistent, authoredTimeOffset);
            float authoredParticleLife = ReadConstantFloat(e["particleLifetime"] as JObject, -1f);
            bool driveFiniteAttachedColor = skin67Attached
                ? authoredParticleLife > 0f && (!persistent || single)
                : (!persistent && authoredParticleLife > 0f);
            if (driveFiniteAttachedColor)
            {
                DariusLolVfxMaterialColorDriver driver = root.gameObject.AddComponent<DariusLolVfxMaterialColorDriver>();
                driver.material = overlayMaterial;
                driver.gradient = lifetimeGradient;
                driver.multiplier = birthTint;
                driver.duration = Mathf.Max(0.05f, authoredParticleLife);
            }
            DariusLog.Info("LOL-VFX-ATTACHED", "Riot AttachedMesh converted emitter=" + name +
                " hashCount=" + (hashes != null ? hashes.Count : 0) + " overlays=" + appliedSubmeshes +
                " fullAvatar=" + (hashes == null || hashes.Count == 0) +
                " finiteColorDriver=" + driveFiniteAttachedColor);
            return attachedLife;
        }

        // Riot Trails/Ribbons are path geometry. Converting them to ParticleSystem ribbons made
        // W/E/R collapse into bright blobs and polygon fans. For attached weapon/body effects,
        // follow the real animated attachment with a world-space TrailRenderer. World-static trail
        // systems need endpoint/target binding and are skipped until that binding is authored.
        if (primitive.IndexOf("Trail", StringComparison.OrdinalIgnoreCase) >= 0 ||
            string.Equals(primitive, "VfxPrimitiveRibbon", StringComparison.OrdinalIgnoreCase))
        {
            // Long-lived attachment systems are animation/event driven in League. A permanently
            // emitting Unity trail accumulates idle weapon motion into a large ribbon cloud, so
            // persistent trails use attachment-motion gating below instead of being omitted.
            if (root.parent == null)
            {
                DariusLog.Warn("LOL-VFX-FIDELITY", "Skipping world-static Riot trail until target/endpoint binding is available primitive=" + primitive + " emitter=" + name);
                return -1f;
            }
            // Long-lived attachment trails are graph/event driven in League. Keep them alive with
            // the owning effect root, but gate emission by animated attachment motion so an idle
            // weapon cannot accumulate a multi-second ribbon cloud. This restores the authored W
            // trail instead of dropping it while retaining the anti-blob safeguard.
            return BuildAttachedTrail(root, e, primitive, name, persistent, authoredTimeOffset, systemName);
        }

        // Ray/Beam/Laser particles carry their strip dimensions in birthScale and their basis in
        // authored rotation. Render them as local mesh quads below instead of dropping the emitter.
        // The previous generic fallback artifacts were primarily caused by wrong blend/TEX decode.

        // Riot particleLifetime=-1 means "live until this emitter/effect stops", not a 999-second
        // particle. The generic path below turns it into a bounded one-shot on a persistent root;
        // destroying the owning effect root remains the authoritative visibility gate.

        // Directional ArbitraryQuad layers (including God-King BlastColumn) now use the authored
        // local basis and corrected material/texture pipeline instead of being omitted.

        string textureRel = (string)e["texture"];
        string meshRel = (string)e["mesh"];
        bool texturedSurfacePrimitive = string.Equals(primitive, "VfxPrimitiveCameraQuad", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(primitive, "VfxPrimitiveArbitraryQuad", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(primitive, "VfxPrimitiveRay", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(primitive, "VfxPrimitivePlanarProjection", StringComparison.OrdinalIgnoreCase) ||
            primitive.IndexOf("Beam", StringComparison.OrdinalIgnoreCase) >= 0 ||
            primitive.IndexOf("Laser", StringComparison.OrdinalIgnoreCase) >= 0;
        // Some Riot BIN nodes are graph placeholders with no texture, mesh, or authored color.
        // Rendering those through Unity's default particle material creates a literal white quad.
        // Mecha P_enraged/Ground_Lighting was the large white "card" visible in the recording.
        bool skin67System = !string.IsNullOrEmpty(systemName) && systemName.StartsWith("Darius_Skin67_", StringComparison.OrdinalIgnoreCase);
        if (skin67System && texturedSurfacePrimitive && string.IsNullOrEmpty(textureRel) && string.IsNullOrEmpty(meshRel) &&
            e["birthColor"] == null && e["color"] == null)
        {
            DariusLog.DebugInfo("LOL-VFX-EMPTY", "Skipping empty Riot visual emitter=" + name + " primitive=" + primitive +
                " system=" + systemName);
            return float.NaN;
        }

        GameObject go = new GameObject(name);
        go.transform.SetParent(root, false);
        Vector3 emitterPosition = ReadConstantVector3(e["position"] as JObject, Vector3.zero);
        Vector3 spawnPointOffset = ReadPointShapeOffset(e["shape"] as JObject);
        go.transform.localPosition = emitterPosition + spawnPointOffset;
        Vector3 emitterEuler = ReadConstantVector3(e["orientation"] as JObject, Vector3.zero);
        go.transform.localRotation = Quaternion.Euler(emitterEuler);
        go.transform.localScale = Vector3.one;

        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = ps.main;
        // A persistent root does not imply that every authored emitter loops. In particular,
        // isSingleParticle emitters are one-shot layers whose lifetime may be graph-controlled.
        main.loop = persistent && !single;
        main.playOnAwake = false;
        bool ribbonPrimitive = primitive.IndexOf("Trail", StringComparison.OrdinalIgnoreCase) >= 0 ||
                               primitive.IndexOf("Ribbon", StringComparison.OrdinalIgnoreCase) >= 0;
        // Weapon/character trails must leave their emitted points in world space as the attachment
        // moves. Local simulation made the whole ribbon follow the axe and collapse into the pink
        // blob seen in the test video.
        main.simulationSpace = ribbonPrimitive ? ParticleSystemSimulationSpace.World : ParticleSystemSimulationSpace.Local;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.maxParticles = 2048;

        float particleLifetime = authoredParticleLifetime;
        // Never synthesize a 999-second particle. Long-lived graph-controlled layers are either
        // explicitly handled above or represented by a bounded one-shot that dies with the root.
        if (particleLifetime <= 0f) particleLifetime = persistent ? (single ? 8f : 4f) : 0.5f;
        float systemLifetime = ReadFloatToken(e["systemLifetime"], persistent ? 1.0f : 0.25f);
        if (systemLifetime <= 0f) systemLifetime = persistent ? 1.0f : 0.25f;
        float linger = ReadFloatToken(e["particleLinger"], 0f);
        float delay = Mathf.Max(0f, ReadFloatToken(e["delay"], 0f) - Mathf.Max(0f, authoredTimeOffset));
        main.duration = Mathf.Clamp(Mathf.Max(0.05f, systemLifetime), 0.05f, persistent ? 60f : 10f);
        main.startDelay = Mathf.Max(0f, delay);
        JObject particleLifetimeData = e["particleLifetime"] as JObject;
        float authoredLifetimeConstant = ReadConstantFloat(particleLifetimeData, particleLifetime);
        main.startLifetime = authoredLifetimeConstant <= 0f
            ? new ParticleSystem.MinMaxCurve(particleLifetime)
            : ToMinMaxCurve(particleLifetimeData, particleLifetime);

        JObject birthColor = e["birthColor"] as JObject;
        JObject color = e["color"] as JObject;
        bool qReadability = IsQReadabilitySystem(root);
        bool colorHasCurve = HasColorCurve(color);
        // Riot Q ring emitters (especially God-King Ring_A) often author their visible tint entirely
        // in the lifetime color curve and leave birthColor unset. The generic converter previously
        // copied color.constant into startColor and then multiplied the same color again through
        // colorOverLifetime. For Ring_A that squared 0.27 alpha to about 0.073, which made the Q
        // ring almost disappear in Shape of Dreams. For Q ring systems only, let a lifetime curve
        // start from neutral white when no separate birthColor exists, then apply a modest
        // saturation lift (with authored alpha preserved) to compensate for the different LoL/Unity HDR bloom pipeline.
        Color startColor = qReadability && birthColor == null && colorHasCurve
            ? Color.white
            : ReadConstantColor(birthColor, ReadConstantColor(color, Color.white));
        if (qReadability && !(birthColor == null && colorHasCurve)) startColor = BoostQReadabilityColor(startColor);
        main.startColor = startColor;

        JObject birthScale = e["birthScale"] as JObject;
        Vector3 defaultSize = string.Equals(primitive, "VfxPrimitiveMesh", StringComparison.OrdinalIgnoreCase)
            ? Vector3.one : Vector3.one * 100f;
        Vector3 size = ReadConstantVector3(birthScale, defaultSize);
        bool uniformScale = ReadBoolToken(e["uniformScale"], false);
        bool skin67NoxianAirflow = string.Equals(systemName, "Darius_Skin67_P_enraged", StringComparison.OrdinalIgnoreCase) &&
            (string.Equals(name, "smoke", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(name, "smoke1", StringComparison.OrdinalIgnoreCase));
        const float Skin67NoxianAirflowSpatialScale = 0.52f;
        bool skin67WindupDisc = string.Equals(systemName, "Darius_Skin67_Q_RingWindup", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(primitive, "VfxPrimitiveArbitraryQuad", StringComparison.OrdinalIgnoreCase) &&
            (string.Equals(name, "AOE", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(name, "AOE_BG", StringComparison.OrdinalIgnoreCase));
        bool skin67WindupTeamRing = string.Equals(systemName, "Darius_Skin67_Q_RingWindup", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(primitive, "VfxPrimitiveMesh", StringComparison.OrdinalIgnoreCase) &&
            name.StartsWith("teamring_circle", StringComparison.OrdinalIgnoreCase);
        main.startSize3D = true;
        if (skin67WindupDisc)
        {
            // Skin67 authors the Q windup circle as [radius,1,1].  Treating those values as
            // literal Unity mesh XYZ produces a flattened/elliptical indicator.  The first
            // component is the Riot quad half-extent, so drive both in-plane axes from X.
            ParticleSystem.MinMaxCurve d = ScaleCurve(ToAxisCurve(birthScale, 0, size.x), 2f);
            main.startSizeX = d; main.startSizeY = d; main.startSizeZ = d;
            DariusLog.DebugInfo("LOL-VFX-Q-GEOMETRY", "Normalized Mecha Q windup disc to circular X/Y extents emitter=" + name);
        }
        else if (skin67WindupTeamRing)
        {
            // The converted Skin67 windup team-ring mesh is geometrically circular, but Riot's
            // source X/Z scale tuples are anisotropic. Preserve the smaller pair as a concentric
            // inner decoration while making the outer pair exactly match the 4.25 m release ring.
            bool outer = string.Equals(name, "teamring_circle2", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(name, "teamring_circle3", StringComparison.OrdinalIgnoreCase);
            float ringScale = outer ? 15.21956f : 12.2f;
            main.startSizeX = new ParticleSystem.MinMaxCurve(ringScale);
            main.startSizeY = new ParticleSystem.MinMaxCurve(1f);
            main.startSizeZ = new ParticleSystem.MinMaxCurve(ringScale);
            DariusLog.DebugInfo("LOL-VFX-Q-GEOMETRY", "Normalized Mecha Q windup team-ring emitter=" + name +
                " scale=" + ringScale.ToString("0.#####") + " outer=" + outer);
        }
        else if (uniformScale)
        {
            // Riot isUniformScale means the X scalar drives all three dimensions. v0.25 ignored
            // this and interpreted values such as Q/Slashes [8,600,150] as anisotropic XYZ,
            // creating the giant triangular sheets seen in the recording.
            ParticleSystem.MinMaxCurve u = ToAxisCurve(birthScale, 0, size.x);
            if (qReadability && string.Equals(primitive, "VfxPrimitiveArbitraryQuad", StringComparison.OrdinalIgnoreCase)) u = ScaleCurve(u, 2f);
            if (skin67NoxianAirflow) u = ScaleCurve(u, Skin67NoxianAirflowSpatialScale);
            main.startSizeX = u; main.startSizeY = u; main.startSizeZ = u;
        }
        else
        {
            ParticleSystem.MinMaxCurve sx = ToAxisCurve(birthScale, 0, size.x);
            ParticleSystem.MinMaxCurve sy = ToAxisCurve(birthScale, 1, size.y);
            ParticleSystem.MinMaxCurve sz = ToAxisCurve(birthScale, 2, Mathf.Abs(size.z) > 0.0001f ? size.z : size.x);
            // Riot ArbitraryQuad scale is authored as a half-extent. Unity ParticleSystem mesh
            // startSize is a full width. The old direct mapping made every Darius Q ring roughly
            // half the real 4.25m outer hit radius. Apply the semantic conversion to every Darius
            // Q ring system (Classic/God-King/Dunkmaster/Mecha), not to an individual skin.
            if (qReadability && string.Equals(primitive, "VfxPrimitiveArbitraryQuad", StringComparison.OrdinalIgnoreCase))
            {
                sx = ScaleCurve(sx, 2f); sy = ScaleCurve(sy, 2f); sz = ScaleCurve(sz, 2f);
            }
            if (skin67NoxianAirflow)
            {
                sx = ScaleCurve(sx, Skin67NoxianAirflowSpatialScale);
                sy = ScaleCurve(sy, Skin67NoxianAirflowSpatialScale);
                sz = ScaleCurve(sz, Skin67NoxianAirflowSpatialScale);
            }
            main.startSizeX = sx; main.startSizeY = sy; main.startSizeZ = sz;
        }

        JObject birthRotation = e["birthRotation"] as JObject;
        Vector3 rot = ReadConstantVector3(birthRotation, Vector3.zero) * Mathf.Deg2Rad;
        // Riot Ray uses a longitudinal Y axis. Unity's quad primitive starts in XY space, so
        // cancel the source Ray basis rotation instead of turning the ray into a ground fan.
        if (string.Equals(primitive, "VfxPrimitiveRay", StringComparison.OrdinalIgnoreCase))
            rot.x += Mathf.PI * 0.5f;
        main.startRotation3D = true;
        main.startRotationX = rot.x;
        main.startRotationY = rot.y;
        main.startRotationZ = rot.z;

        ConfigureEmission(ps, e);
        ConfigureShape(ps, e["shape"] as JObject);
        ConfigureVelocity(ps, e);
        if (skin67NoxianAirflow)
        {
            ScaleVelocityOverLifetime(ps, Skin67NoxianAirflowSpatialScale);
            DariusLog.DebugInfo("LOL-VFX-SKIN67-AIRFLOW", "Tightened Mecha Noxian Might airflow emitter=" + name +
                " spatialScale=" + Skin67NoxianAirflowSpatialScale.ToString("0.##"));
        }
        ConfigureSizeOverLifetime(ps, e["scale"] as JObject, uniformScale);
        ConfigureColorOverLifetime(ps, color, qReadability);

        // Q_Ring ArbitraryQuad rotation is authored around Riot's Y axis. Applying that vector
        // through Unity ParticleSystem.rotationOverLifetime leaves the bright axe sector fixed
        // because the quad has already been rotated -90 degrees onto the ground plane. For Q only,
        // rotate the emitter transform in local space instead. This preserves Riot's own texture,
        // authored angular speed and timing rather than drawing a replacement ring.
        bool qAuthoredGroundSpin = qReadability &&
            string.Equals(primitive, "VfxPrimitiveArbitraryQuad", StringComparison.OrdinalIgnoreCase);
        Vector3 authoredAngularVelocity = ReadConstantVector3(e["birthRotationalVelocity"] as JObject, Vector3.zero);
        if (qAuthoredGroundSpin && Mathf.Abs(authoredAngularVelocity.y) > 0.001f)
        {
            DariusLolVfxAuthoredTransformSpin spin = go.AddComponent<DariusLolVfxAuthoredTransformSpin>();
            spin.degreesPerSecondY = authoredAngularVelocity.y;
            spin.startDelay = delay;
            spin.activeDuration = Mathf.Max(0.05f, systemLifetime + particleLifetime + linger);
            DariusLog.DebugInfo("LOL-VFX-Q-SPIN", "Converted authored Q ring transform spin emitter=" + name +
                " degPerSecY=" + authoredAngularVelocity.y.ToString("0.##") +
                " startDelay=" + delay.ToString("0.###") + " duration=" + spin.activeDuration.ToString("0.###"));
        }
        else
        {
            ConfigureRotationOverLifetime(ps, e["birthRotationalVelocity"] as JObject);
        }
        ConfigureTextureSheet(ps, e["texDiv"] as JArray);

        ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
        ConfigureRenderer(ps, renderer, e, primitive, meshRel, textureRel,
            ReadIntToken(e["blendMode"], 0), ReadIntToken(e["pass"], 0), systemName);
        ps.Play(true);
        return Mathf.Max(systemLifetime + particleLifetime + linger + delay, particleLifetime + delay);
    }

    private static float EstimateEmitterLife(JObject e, bool persistent, float authoredTimeOffset = 0f)
    {
        float particleLifetime = ReadConstantFloat(e["particleLifetime"] as JObject, 0.5f);
        bool single = ReadBoolToken(e["single"], false);
        if (particleLifetime <= 0f) particleLifetime = persistent ? (single ? 8f : 4f) : 0.5f;
        float systemLifetime = ReadFloatToken(e["systemLifetime"], persistent ? 1.0f : 0.25f);
        if (systemLifetime <= 0f) systemLifetime = persistent ? 1.0f : 0.25f;
        float linger = ReadFloatToken(e["particleLinger"], 0f);
        float delay = Mathf.Max(0f, ReadFloatToken(e["delay"], 0f) - Mathf.Max(0f, authoredTimeOffset));
        return Mathf.Max(systemLifetime + particleLifetime + linger + delay, particleLifetime + delay);
    }

    private static void ConfigureEmission(ParticleSystem ps, JObject e)
    {
        ParticleSystem.EmissionModule emission = ps.emission;
        emission.enabled = true;
        bool single = e["single"] != null && (bool)e["single"];
        float rate = ReadConstantFloat(e["rate"] as JObject, single ? 1f : 0f);
        if (single)
        {
            emission.rateOverTime = 0f;
            emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, (short)Mathf.Clamp(Mathf.RoundToInt(Mathf.Max(1f, rate)), 1, 256)) });
        }
        else emission.rateOverTime = Mathf.Clamp(rate, 0f, 2000f);
    }

    private static void ConfigureShape(ParticleSystem ps, JObject shapeData)
    {
        if (shapeData == null) return;
        string type = (string)shapeData["type"];
        if (string.IsNullOrEmpty(type) || type.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(type, "VfxShapePointDoNotUse", StringComparison.OrdinalIgnoreCase)) return;
        ParticleSystem.ShapeModule shape = ps.shape;
        shape.enabled = true;
        JObject fields = shapeData["fields"] as JObject;
        if (string.Equals(type, "VfxShapeSphere", StringComparison.OrdinalIgnoreCase)) shape.shapeType = ParticleSystemShapeType.Sphere;
        else if (string.Equals(type, "VfxShapeBox", StringComparison.OrdinalIgnoreCase)) shape.shapeType = ParticleSystemShapeType.Box;
        else if (string.Equals(type, "VfxShapeCylinder", StringComparison.OrdinalIgnoreCase)) shape.shapeType = ParticleSystemShapeType.Cone;
        else if (string.Equals(type, "VfxShapeLegacy", StringComparison.OrdinalIgnoreCase))
        {
            // This Riot legacy structure contains basis/angle data, not a sphere radius. The old
            // sphere substitution scattered many particles into bogus polygons. Until its exact
            // basis semantics are mapped, keep the authored emitter origin rather than inventing
            // a random 3D volume.
            shape.enabled = false;
            return;
        }
        else { shape.enabled = false; return; }

        if (fields != null)
        {
            float firstPositive = 0f;
            foreach (JProperty p in fields.Properties())
            {
                if (p.Value.Type == JTokenType.Float || p.Value.Type == JTokenType.Integer)
                {
                    float v = Mathf.Abs((float)p.Value);
                    if (v > 0.0001f && firstPositive <= 0f) firstPositive = v;
                }
                JArray a = p.Value as JArray;
                if (a != null && a.Count >= 3 && shape.shapeType == ParticleSystemShapeType.Box)
                    shape.scale = new Vector3(Mathf.Abs((float)a[0]), Mathf.Abs((float)a[1]), Mathf.Abs((float)a[2]));
            }
            if (firstPositive > 0f && shape.shapeType != ParticleSystemShapeType.Box) shape.radius = firstPositive;
        }
    }

    private static void ScaleVelocityOverLifetime(ParticleSystem ps, float factor)
    {
        if (ps == null || Mathf.Approximately(factor, 1f)) return;
        ParticleSystem.VelocityOverLifetimeModule m = ps.velocityOverLifetime;
        if (!m.enabled) return;
        m.x = ScaleCurve(m.x, factor);
        m.y = ScaleCurve(m.y, factor);
        m.z = ScaleCurve(m.z, factor);
    }

    private static void ConfigureVelocity(ParticleSystem ps, JObject e)
    {
        JObject vel = e["birthVelocity"] as JObject;
        if (vel != null)
        {
            Vector3 c = ReadConstantVector3(vel, Vector3.zero);
            ParticleSystem.VelocityOverLifetimeModule m = ps.velocityOverLifetime;
            m.enabled = true; m.space = ParticleSystemSimulationSpace.Local;
            ParticleSystem.MinMaxCurve x, y, z;
            BuildUniformAxisCurves(vel, c, out x, out y, out z);
            m.x = x; m.y = y; m.z = z;
        }
        JObject acc = e["birthAcceleration"] as JObject;
        if (acc != null)
        {
            Vector3 c = ReadConstantVector3(acc, Vector3.zero);
            ParticleSystem.ForceOverLifetimeModule f = ps.forceOverLifetime;
            f.enabled = true; f.space = ParticleSystemSimulationSpace.Local;
            ParticleSystem.MinMaxCurve x, y, z;
            BuildUniformAxisCurves(acc, c, out x, out y, out z);
            f.x = x; f.y = y; f.z = z;
        }
    }

    // Unity requires X/Y/Z velocity curves to use the same MinMaxCurve mode. Riot allows
    // per-axis probability tables, so promote all three axes to TwoConstants whenever any
    // axis is randomized. This preserves the authored ranges without generating per-frame
    // "Particle Velocity curves must all be in the same mode" errors.
    private static void BuildUniformAxisCurves(JObject d, Vector3 fallback,
        out ParticleSystem.MinMaxCurve x, out ParticleSystem.MinMaxCurve y, out ParticleSystem.MinMaxCurve z)
    {
        float xmin, xmax, ymin, ymax, zmin, zmax;
        bool xv = AxisRange(d, 0, fallback.x, out xmin, out xmax);
        bool yv = AxisRange(d, 1, fallback.y, out ymin, out ymax);
        bool zv = AxisRange(d, 2, fallback.z, out zmin, out zmax);
        if (xv || yv || zv)
        {
            x = new ParticleSystem.MinMaxCurve(xmin, xmax);
            y = new ParticleSystem.MinMaxCurve(ymin, ymax);
            z = new ParticleSystem.MinMaxCurve(zmin, zmax);
        }
        else
        {
            x = new ParticleSystem.MinMaxCurve(xmin);
            y = new ParticleSystem.MinMaxCurve(ymin);
            z = new ParticleSystem.MinMaxCurve(zmin);
        }
    }

    private static bool AxisRange(JObject d, int axis, float fallback, out float min, out float max)
    {
        Vector3 c = ReadConstantVector3(d, new Vector3(fallback, fallback, fallback));
        float cv = axis == 0 ? c.x : (axis == 1 ? c.y : c.z);
        min = max = cv;
        JArray pt = d != null ? d["probabilityTables"] as JArray : null;
        if (pt == null || pt.Count <= axis) return false;
        JObject pa = pt[axis] as JObject;
        JArray v = pa != null ? pa["values"] as JArray : null;
        if (v == null || v.Count < 2) return false;
        float a = Scalar(v[0], 1f) * cv;
        float b = Scalar(v[v.Count - 1], 1f) * cv;
        min = Mathf.Min(a, b); max = Mathf.Max(a, b);
        return Mathf.Abs(max - min) > 0.000001f;
    }

    private static void ConfigureSizeOverLifetime(ParticleSystem ps, JObject data, bool uniformScale)
    {
        if (data == null || data["times"] == null || data["values"] == null) return;
        ParticleSystem.SizeOverLifetimeModule s = ps.sizeOverLifetime;
        s.enabled = true; s.separateAxes = true;
        AnimationCurve x = BuildAnimationCurve(data, 0, 1f);
        s.x = new ParticleSystem.MinMaxCurve(1f, x);
        if (uniformScale)
        {
            s.y = new ParticleSystem.MinMaxCurve(1f, x);
            s.z = new ParticleSystem.MinMaxCurve(1f, x);
        }
        else
        {
            s.y = new ParticleSystem.MinMaxCurve(1f, BuildAnimationCurve(data,1,1f));
            s.z = new ParticleSystem.MinMaxCurve(1f, BuildAnimationCurve(data,2,1f));
        }
    }

    private static bool ShouldSkipSkin67Emitter(string systemName, string emitterName)
    {
        if (string.IsNullOrEmpty(systemName) || string.IsNullOrEmpty(emitterName)) return false;

        // These two are the green WindowPattern grid visible immediately when Q is pressed.
        if (string.Equals(systemName, "Darius_Skin67_Q_RingWindup", StringComparison.OrdinalIgnoreCase) &&
            (string.Equals(emitterName, "ant_dark1", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(emitterName, "ant_dark2", StringComparison.OrdinalIgnoreCase)))
            return true;

        // Ground_Lighting carries no texture, mesh or color at all.  It was one of the original
        // white card flashes during Noxian Might, so keep an explicit name guard in addition to
        // the generic empty-emitter test below.
        if (string.Equals(systemName, "Darius_Skin67_P_enraged", StringComparison.OrdinalIgnoreCase) &&
            (string.Equals(emitterName, "Ground_Lighting", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(emitterName, "Distort", StringComparison.OrdinalIgnoreCase)))
            return true;

        // The max-stack marker contains a special character/hologram mesh using the champion atlas
        // and a LoL-only shader contract.  On the generic shader it becomes an opaque character
        // card/rectangle.  The remaining max-stack emitters still provide a clear five-stack cue.
        if (string.Equals(systemName, "Darius_Skin67_DariusBasePassiveOverheadMaxStack", StringComparison.OrdinalIgnoreCase) &&
            (string.Equals(emitterName, "Temp_start", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(emitterName, "Temp_Mesh", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(emitterName, "Temp_Mesh1", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(emitterName, "FireCards", StringComparison.OrdinalIgnoreCase)))
            return true;

        return false;
    }

    private static bool IsQReadabilitySystem(Transform root)
    {
        return root != null && root.name.IndexOf("_Q_Ring", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool HasColorCurve(JObject data)
    {
        if (data == null) return false;
        JArray times = data["times"] as JArray;
        JArray values = data["values"] as JArray;
        return times != null && values != null && times.Count > 0 && values.Count > 0;
    }

    private static Color BoostQReadabilityColor(Color c)
    {
        float h, s, v;
        Color.RGBToHSV(c, out h, out s, out v);
        // Do not tint neutral whites/greys. Only strengthen already-authored chroma.
        if (s > 0.02f) s = Mathf.Clamp01(s * 1.15f + 0.06f);
        v = Mathf.Clamp01(v * 1.08f);
        Color result = Color.HSVToRGB(h, s, v);
        // Visibility compensation may strengthen RGB/chroma, but it must never resurrect pixels
        // that the authored texture/curve made transparent. Preserve alpha exactly.
        result.a = c.a;
        return result;
    }

    private static void ConfigureColorOverLifetime(ParticleSystem ps, JObject data, bool qReadability = false)
    {
        if (data == null || data["times"] == null || data["values"] == null) return;
        JArray times = data["times"] as JArray; JArray values = data["values"] as JArray;
        if (times == null || values == null || times.Count == 0) return;
        Gradient g = BuildGradient(data, Color.white, qReadability);
        ParticleSystem.ColorOverLifetimeModule m=ps.colorOverLifetime; m.enabled=true; m.color=new ParticleSystem.MinMaxGradient(g);
    }

    private static Gradient BuildGradient(JObject data, Color fallback, bool qReadability = false)
    {
        Gradient g = new Gradient();
        JArray times = data != null ? data["times"] as JArray : null;
        JArray values = data != null ? data["values"] as JArray : null;
        if (times == null || values == null || times.Count == 0 || values.Count == 0)
        {
            Color c = ReadConstantColor(data, fallback);
            if (qReadability) c = BoostQReadabilityColor(c);
            g.SetKeys(new[] { new GradientColorKey(c, 0f), new GradientColorKey(c, 1f) },
                      new[] { new GradientAlphaKey(c.a, 0f), new GradientAlphaKey(c.a, 1f) });
            return g;
        }
        int n = Mathf.Min(8, Mathf.Min(times.Count, values.Count));
        GradientColorKey[] ck = new GradientColorKey[n];
        GradientAlphaKey[] ak = new GradientAlphaKey[n];
        for (int i = 0; i < n; i++)
        {
            JArray v = values[i] as JArray;
            float t = Mathf.Clamp01((float)times[i]);
            Color c = v != null && v.Count >= 3
                ? new Color((float)v[0], (float)v[1], (float)v[2], v.Count > 3 ? (float)v[3] : 1f)
                : fallback;
            if (qReadability) c = BoostQReadabilityColor(c);
            ck[i] = new GradientColorKey(c, t);
            ak[i] = new GradientAlphaKey(c.a, t);
        }
        g.SetKeys(ck, ak);
        return g;
    }

    private static void ConfigureRotationOverLifetime(ParticleSystem ps, JObject data)
    {
        if (data == null) return;
        Vector3 d = ReadConstantVector3(data, Vector3.zero) * Mathf.Deg2Rad;
        if (d.sqrMagnitude < 0.000001f) return;
        ParticleSystem.RotationOverLifetimeModule r=ps.rotationOverLifetime; r.enabled=true; r.separateAxes=true;
        r.x=d.x; r.y=d.y; r.z=d.z;
    }

    private static void ConfigureTextureSheet(ParticleSystem ps, JArray div)
    {
        if (div == null || div.Count < 2) return;
        float fx = (float)div[0], fy = (float)div[1];
        // Unity's grid animator requires integer tile counts. Riot also uses texDiv for continuous
        // UV scaling (for example Skin67 R_Trail [2, 0.5]); rounding 0.5 to one silently changes
        // the shader semantics. Non-integer divisions are handled by the per-emitter mesh material
        // path instead of being coerced into a fake sprite sheet.
        if (fx < 1f || fy < 1f || Mathf.Abs(fx - Mathf.Round(fx)) > 0.001f || Mathf.Abs(fy - Mathf.Round(fy)) > 0.001f) return;
        int x=Mathf.Max(1,Mathf.RoundToInt(fx)), y=Mathf.Max(1,Mathf.RoundToInt(fy));
        if (x<=1 && y<=1) return;
        ParticleSystem.TextureSheetAnimationModule a=ps.textureSheetAnimation; a.enabled=true; a.mode=ParticleSystemAnimationMode.Grid;
        a.numTilesX=x; a.numTilesY=y; a.animation=ParticleSystemAnimationType.WholeSheet;
    }

    private static void ConfigureRenderer(ParticleSystem ps, ParticleSystemRenderer r, JObject emitter, string primitive, string meshRel, string textureRel, int blend, int renderPass, string systemName)
    {
        if (r == null) return;
        Material material = MaterialFor(textureRel, blend);
        if (material == null) { r.enabled = false; return; }

        // Skin67 R_Trail uses mesh UV offsets/scrolling as part of the authored weapon-streak
        // shader. The previous converter ignored all three fields, so the broad white part of the
        // mask stayed fixed across the enormous slash mesh. Clone only these materials (leaving
        // already-approved Classic/God-King/Dunkmaster visuals untouched) and reproduce the UV
        // transform locally for each emitter.
        bool skin67RMeshUv = string.Equals(systemName, "Darius_Skin67_R_Trail", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(primitive, "VfxPrimitiveMesh", StringComparison.OrdinalIgnoreCase);
        if (skin67RMeshUv)
        {
            Vector2 initialOffset = ReadConstantVector2(emitter["birthUVOffset"] as JObject, Vector2.zero);
            Vector2 scroll = ReadConstantVector2(emitter["birthUvScrollRate"] as JObject, Vector2.zero) +
                ReadConstantVector2(emitter["emitterUvScrollRate"] as JObject, Vector2.zero);
            Vector2 uvScale = Vector2.one;
            JArray div = emitter["texDiv"] as JArray;
            if (div != null && div.Count >= 2)
            {
                float dx = Mathf.Abs((float)div[0]);
                float dy = Mathf.Abs((float)div[1]);
                if (dx > 0.0001f && dy > 0.0001f &&
                    (dx < 0.999f || dy < 0.999f || Mathf.Abs(dx - Mathf.Round(dx)) > 0.001f || Mathf.Abs(dy - Mathf.Round(dy)) > 0.001f))
                    uvScale = new Vector2(1f / dx, 1f / dy);
            }
            if (initialOffset.sqrMagnitude > 0.0000001f || scroll.sqrMagnitude > 0.0000001f || (uvScale - Vector2.one).sqrMagnitude > 0.0000001f)
            {
                Material localMaterial = new Material(material);
                localMaterial.name = material.name + "_UV_" + ((string)emitter["name"] ?? "Mesh");
                localMaterial.mainTextureScale = uvScale;
                localMaterial.mainTextureOffset = initialOffset;
                if (localMaterial.HasProperty("_BaseMap"))
                {
                    localMaterial.SetTextureScale("_BaseMap", uvScale);
                    localMaterial.SetTextureOffset("_BaseMap", initialOffset);
                }
                DariusLolVfxLinkedObjects links = r.transform.parent != null ? r.transform.parent.GetComponent<DariusLolVfxLinkedObjects>() : null;
                if (links != null) links.Add(localMaterial);
                if (scroll.sqrMagnitude > 0.0000001f)
                {
                    DariusLolVfxMaterialUvDriver uv = r.gameObject.AddComponent<DariusLolVfxMaterialUvDriver>();
                    uv.material = localMaterial; uv.initialOffset = initialOffset; uv.scrollRate = scroll;
                }
                material = localMaterial;
                DariusLog.DebugInfo("LOL-VFX-UV", "Applied Skin67 R mesh UV semantics emitter=" + ((string)emitter["name"] ?? "Mesh") +
                    " scale=(" + uvScale.x.ToString("0.###") + "," + uvScale.y.ToString("0.###") + ") offset=(" +
                    initialOffset.x.ToString("0.###") + "," + initialOffset.y.ToString("0.###") + ") scroll=(" +
                    scroll.x.ToString("0.###") + "," + scroll.y.ToString("0.###") + ")");
            }
        }
        if (string.Equals(primitive,"VfxPrimitiveMesh",StringComparison.OrdinalIgnoreCase))
        {
            Mesh m=LoadMesh(meshRel); if(m==null){r.enabled=false;return;} r.renderMode=ParticleSystemRenderMode.Mesh; r.mesh=m;
            r.alignment=ParticleSystemRenderSpace.Local;
            r.sharedMaterial=material;
        }
        else if (string.Equals(primitive,"VfxPrimitiveArbitraryQuad",StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(primitive,"VfxPrimitiveRay",StringComparison.OrdinalIgnoreCase) ||
                 primitive.IndexOf("Beam",StringComparison.OrdinalIgnoreCase)>=0 ||
                 primitive.IndexOf("Laser",StringComparison.OrdinalIgnoreCase)>=0 ||
                 string.Equals(primitive,"VfxPrimitivePlanarProjection",StringComparison.OrdinalIgnoreCase))
        {
            r.renderMode=ParticleSystemRenderMode.Mesh; r.mesh=QuadMesh(); r.alignment=ParticleSystemRenderSpace.Local;
            r.sharedMaterial=material;
        }
        else
        {
            r.renderMode=ParticleSystemRenderMode.Billboard;
            r.alignment=primitive.IndexOf("Camera",StringComparison.OrdinalIgnoreCase)>=0 ? ParticleSystemRenderSpace.View : ParticleSystemRenderSpace.Local;
            r.sharedMaterial=material;
        }
        r.sortMode=ParticleSystemSortMode.Distance;
        r.sortingOrder=Mathf.Clamp(renderPass,-200,200);
    }

    private static bool CreateGenericAttachedOverlay(Transform target, Material overlayMaterial, string label, DariusLolVfxLinkedObjects links)
    {
        if (target == null || overlayMaterial == null) return false;
        int created = 0;
        try
        {
            SkinnedMeshRenderer[] skinned = target.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            for (int i = 0; i < skinned.Length && created < 4; i++)
            {
                SkinnedMeshRenderer source = skinned[i];
                if (source == null || source.sharedMesh == null || IsVfxOverlayTransform(source.transform)) continue;
                GameObject go = new GameObject("LoL_TargetAttachedMesh_" + (string.IsNullOrEmpty(label) ? "Avatar" : label) + "_" + created);
                go.transform.SetParent(source.transform, false);
                SkinnedMeshRenderer r = go.AddComponent<SkinnedMeshRenderer>();
                r.updateWhenOffscreen = true;
                r.quality = source.quality;
                r.sharedMesh = source.sharedMesh;
                r.bones = source.bones;
                r.rootBone = source.rootBone;
                Material[] mats = new Material[Mathf.Max(1, source.sharedMesh.subMeshCount)];
                for (int mi = 0; mi < mats.Length; mi++) mats[mi] = overlayMaterial;
                r.sharedMaterials = mats;
                r.sortingLayerID = source.sortingLayerID;
                r.sortingOrder = source.sortingOrder + 1;
                if (links != null) links.Add(go);
                created++;
            }

            if (created == 0)
            {
                MeshRenderer[] meshes = target.GetComponentsInChildren<MeshRenderer>(true);
                for (int i = 0; i < meshes.Length && created < 4; i++)
                {
                    MeshRenderer source = meshes[i];
                    if (source == null || IsVfxOverlayTransform(source.transform)) continue;
                    MeshFilter sourceFilter = source.GetComponent<MeshFilter>();
                    if (sourceFilter == null || sourceFilter.sharedMesh == null) continue;
                    GameObject go = new GameObject("LoL_TargetAttachedMesh_" + (string.IsNullOrEmpty(label) ? "Avatar" : label) + "_" + created);
                    go.transform.SetParent(source.transform, false);
                    MeshFilter f = go.AddComponent<MeshFilter>(); f.sharedMesh = sourceFilter.sharedMesh;
                    MeshRenderer r = go.AddComponent<MeshRenderer>();
                    Material[] mats = new Material[Mathf.Max(1, sourceFilter.sharedMesh.subMeshCount)];
                    for (int mi = 0; mi < mats.Length; mi++) mats[mi] = overlayMaterial;
                    r.sharedMaterials = mats;
                    r.sortingLayerID = source.sortingLayerID;
                    r.sortingOrder = source.sortingOrder + 1;
                    if (links != null) links.Add(go);
                    created++;
                }
            }
        }
        catch (Exception e)
        {
            DariusLog.Exception("LOL-VFX-ATTACHED", e, "Generic target AttachedMesh overlay failed label=" + label);
        }
        if (created > 0)
            DariusLog.Info("LOL-VFX-ATTACHED", "Applied generic target AttachedMesh overlay label=" + label + " renderers=" + created);
        return created > 0;
    }

    private static bool IsVfxOverlayTransform(Transform t)
    {
        if (t == null) return false;
        string n = t.name ?? string.Empty;
        return n.StartsWith("LOL_VFX_", StringComparison.OrdinalIgnoreCase) ||
            n.StartsWith("LoL_AttachedMeshOverlay_", StringComparison.OrdinalIgnoreCase) ||
            n.StartsWith("LoL_TargetAttachedMesh_", StringComparison.OrdinalIgnoreCase);
    }

    private static float BuildAttachedTrail(Transform root, JObject emitter, string primitive, string name, bool persistent, float authoredTimeOffset, string systemName)
    {
        GameObject anchor = new GameObject(name + "_TrailAnchor");
        anchor.transform.SetParent(root, false);
        anchor.transform.localPosition = ReadConstantVector3(emitter["position"] as JObject, Vector3.zero) + ReadPointShapeOffset(emitter["shape"] as JObject);
        anchor.transform.localRotation = Quaternion.Euler(ReadConstantVector3(emitter["orientation"] as JObject, Vector3.zero));

        GameObject trailGo = new GameObject(name + "_RiotTrail");
        trailGo.transform.position = anchor.transform.position;
        trailGo.transform.rotation = anchor.transform.rotation;
        TrailRenderer tr = trailGo.AddComponent<TrailRenderer>();
        tr.autodestruct = false;
        tr.emitting = true;
        tr.textureMode = LineTextureMode.Tile;
        tr.alignment = primitive.IndexOf("Camera", StringComparison.OrdinalIgnoreCase) >= 0 ? LineAlignment.View : LineAlignment.TransformZ;
        tr.numCornerVertices = 0;
        tr.numCapVertices = 0;
        tr.generateLightingData = false;
        tr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        tr.receiveShadows = false;

        Vector3 bs = ReadConstantVector3(emitter["birthScale"] as JObject, new Vector3(50f, 50f, 0f));
        float width = Mathf.Clamp(Mathf.Abs(bs.x) * _coordinateScale, 0.015f, 2.5f);
        tr.widthMultiplier = width;
        tr.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
        float particleLifetime = ReadConstantFloat(emitter["particleLifetime"] as JObject, 0.35f);
        if (particleLifetime <= 0f) particleLifetime = persistent ? 4f : 0.35f;
        tr.time = Mathf.Clamp(particleLifetime, 0.05f, persistent ? 8f : 3f);

        JObject pd = emitter["primitiveData"] as JObject;
        JObject td = pd != null ? pd["trail"] as JObject : null;
        int smoothing = td != null ? ReadIntToken(td["smoothingMode"], 1) : 1;
        tr.minVertexDistance = smoothing >= 2 ? 0.015f : 0.03f;
        Material trailMaterial = MaterialFor((string)emitter["texture"], ReadIntToken(emitter["blendMode"], 0));
        if (trailMaterial != null && string.Equals(systemName, "Darius_Skin67_E_Tar02", StringComparison.OrdinalIgnoreCase))
        {
            Vector2 initialOffset = ReadConstantVector2(emitter["birthUVOffset"] as JObject, Vector2.zero);
            Vector2 scroll = ReadConstantVector2(emitter["birthUvScrollRate"] as JObject, Vector2.zero) +
                ReadConstantVector2(emitter["emitterUvScrollRate"] as JObject, Vector2.zero);
            if (initialOffset.sqrMagnitude > 0.0000001f || scroll.sqrMagnitude > 0.0000001f)
            {
                Material localTrailMaterial = new Material(trailMaterial);
                localTrailMaterial.name = trailMaterial.name + "_Skin67EndpointTrail_" + name;
                localTrailMaterial.mainTextureOffset = initialOffset;
                if (localTrailMaterial.HasProperty("_BaseMap")) localTrailMaterial.SetTextureOffset("_BaseMap", initialOffset);
                DariusLolVfxMaterialUvDriver uv = trailGo.AddComponent<DariusLolVfxMaterialUvDriver>();
                uv.material = localTrailMaterial; uv.initialOffset = initialOffset; uv.scrollRate = scroll;
                DariusLolVfxLinkedObjects rootLinks = root.GetComponent<DariusLolVfxLinkedObjects>();
                if (rootLinks != null) rootLinks.Add(localTrailMaterial);
                trailMaterial = localTrailMaterial;
                DariusLog.DebugInfo("LOL-VFX-UV", "Applied Skin67 E endpoint-trail UV scroll emitter=" + name +
                    " offset=(" + initialOffset.x.ToString("0.###") + "," + initialOffset.y.ToString("0.###") +
                    ") scroll=(" + scroll.x.ToString("0.###") + "," + scroll.y.ToString("0.###") + ")");
            }
        }
        tr.sharedMaterial = trailMaterial;
        tr.colorGradient = BuildGradient(emitter["color"] as JObject, ReadInitialColor(emitter["birthColor"] as JObject, Color.white));
        tr.sortingOrder = Mathf.Clamp(ReadIntToken(emitter["pass"], 0), -200, 200);

        DariusLolVfxTrailFollower follower = trailGo.AddComponent<DariusLolVfxTrailFollower>();
        follower.target = anchor.transform;
        follower.trail = tr;
        follower.startDelay = Mathf.Max(0f, ReadFloatToken(emitter["delay"], 0f) - Mathf.Max(0f, authoredTimeOffset));
        float systemLifetime = ReadFloatToken(emitter["systemLifetime"], persistent ? -1f : 0.25f);
        follower.emitDuration = persistent ? -1f : Mathf.Max(0.05f, systemLifetime);
        follower.motionGated = persistent;

        DariusLolVfxLinkedObjects links = root.GetComponent<DariusLolVfxLinkedObjects>();
        if (links != null) links.Add(trailGo);
        DariusLog.DebugInfo("LOL-VFX-TRAIL", "Converted attached Riot trail primitive=" + primitive + " emitter=" + name +
            " width=" + width.ToString("0.###") + " time=" + tr.time.ToString("0.###") + " smoothing=" + smoothing);
        return EstimateEmitterLife(emitter, persistent, authoredTimeOffset);
    }

    private static Texture2D LoadTexture(string rel)
    {
        if(string.IsNullOrEmpty(rel)) return null; Texture2D t; if(Textures.TryGetValue(rel,out t))return t;
        try
        {
            string p=Path.Combine(DariusMedia.Root,"assets","lol_vfx",rel.Replace('/',Path.DirectorySeparatorChar));
            byte[] b=File.ReadAllBytes(p);
            if(rel.EndsWith(".tex",StringComparison.OrdinalIgnoreCase))
            {
                if(b.Length<12 || b[0]!=(byte)'T' || b[1]!=(byte)'E' || b[2]!=(byte)'X' || b[3]!=0)
                    throw new InvalidDataException("Riot TEX header missing");
                int w=b[4]|(b[5]<<8), h=b[6]|(b[7]<<8);
                int format=b[9];
                TextureFormat tf; int bytesPerBlock;
                if(format==10) { tf=TextureFormat.DXT1; bytesPerBlock=8; }
                else if(format==12) { tf=TextureFormat.DXT5; bytesPerBlock=16; }
                else throw new NotSupportedException("Riot TEX format="+format);
                // Riot TEX stores mip levels smallest -> largest; Unity raw texture loading expects
                // mip 0 first. Feeding the whole Riot payload directly therefore scrambled VFX
                // atlases. Render from the authoritative full-resolution mip (the final mip block).
                int blocksX=Mathf.Max(1,(w+3)/4), blocksY=Mathf.Max(1,(h+3)/4);
                int topMipBytes=blocksX*blocksY*bytesPerBlock;
                if(topMipBytes<=0 || b.Length<12+topMipBytes) throw new InvalidDataException("Riot TEX top mip truncated");
                byte[] raw=new byte[topMipBytes]; Buffer.BlockCopy(b,b.Length-topMipBytes,raw,0,topMipBytes);
                t=new Texture2D(w,h,tf,false); t.LoadRawTextureData(raw); t.Apply(false,false);
            }
            else
            {
                t=new Texture2D(2,2,TextureFormat.RGBA32,false);
                if(!ImageConversion.LoadImage(t,b,false)){UnityEngine.Object.Destroy(t);t=null;}
            }
            if(t!=null)
            {
                t.name="LoLVfx_"+Path.GetFileNameWithoutExtension(rel);
                t.wrapMode=TextureWrapMode.Repeat; t.filterMode=FilterMode.Bilinear;
            }
        }
        catch(Exception e){DariusLog.Exception("LOL-VFX-TEX",e,"rel="+rel);t=null;}
        Textures[rel]=t; return t;
    }

    private static Material MaterialFor(string texRel,int blend)
    {
        string key=(texRel??"<none>")+":"+blend; Material m; if(Materials.TryGetValue(key,out m)&&m!=null)return m;
        Shader s=null;
        // Riot VfxEmitterDefinitionData blendMode is global across primitives:
        // 0 One/One additive (also the default when absent), 1 SrcAlpha/OneMinusSrcAlpha,
        // 2 Zero/OneMinusSrcColor multiply, 3 opaque/depth-writing, 4 SrcAlpha/One additive.
        if (blend==0 || blend==4) s=Shader.Find("Legacy Shaders/Particles/Additive");
        else if (blend==1) s=Shader.Find("Legacy Shaders/Particles/Alpha Blended");
        else if (blend==2) s=Shader.Find("Legacy Shaders/Particles/Multiply");
        else if (blend==3) s=Shader.Find("Unlit/Transparent Cutout");
        if(s==null) s=Shader.Find("Particles/Standard Unlit");
        if(s==null) s=Shader.Find("Sprites/Default");
        if(s==null) s=Shader.Find("Universal Render Pipeline/Unlit");
        if(s==null) return null;
        m=new Material(s); m.name="LoLVfxMat_"+blend+"_"+Path.GetFileNameWithoutExtension(texRel??"none");
        Texture2D t=LoadTexture(texRel); if(t!=null){m.mainTexture=t;if(m.HasProperty("_BaseMap"))m.SetTexture("_BaseMap",t);}
        int src=5, dst=10; bool transparent=true;
        if(blend==0) { src=1; dst=1; }
        else if(blend==1) { src=5; dst=10; }
        else if(blend==2) { src=0; dst=6; }
        else if(blend==3) { src=1; dst=0; transparent=false; }
        else if(blend==4) { src=5; dst=1; }
        if(m.HasProperty("_SrcBlend"))m.SetFloat("_SrcBlend",src);
        if(m.HasProperty("_DstBlend"))m.SetFloat("_DstBlend",dst);
        if(m.HasProperty("_ZWrite"))m.SetFloat("_ZWrite",transparent?0f:1f);
        if(m.HasProperty("_Surface"))m.SetFloat("_Surface",transparent?1f:0f);
        if(m.HasProperty("_Cull"))m.SetFloat("_Cull",0f);
        if(transparent) m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); else m.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
        if(m.HasProperty("_TintColor")) m.SetColor("_TintColor", Color.white);
        if(!transparent && m.HasProperty("_Cutoff")) m.SetFloat("_Cutoff", 0.01f);
        m.renderQueue=transparent?3000:2450; Materials[key]=m; return m;
    }

    private static Mesh LoadMesh(string rel)
    {
        if(string.IsNullOrEmpty(rel))return null; Mesh m; if(Meshes.TryGetValue(rel,out m))return m;
        try
        {
            string p=Path.Combine(DariusMedia.Root,"assets","lol_vfx",rel.Replace('/',Path.DirectorySeparatorChar)); JObject j=JObject.Parse(File.ReadAllText(p));
            JArray pos=j["positions"] as JArray, uv=j["uv"] as JArray, ix=j["indices"] as JArray;
            if(pos==null||ix==null)throw new InvalidDataException("mesh arrays missing");
            Vector3[] v=new Vector3[pos.Count]; Vector2[] tc=uv!=null?new Vector2[uv.Count]:null; int[] tri=new int[ix.Count];
            for(int i=0;i<pos.Count;i++){JArray a=pos[i] as JArray;v[i]=new Vector3((float)a[0],(float)a[1],(float)a[2]);}
            if(tc!=null)for(int i=0;i<uv.Count;i++){JArray a=uv[i] as JArray;tc[i]=new Vector2((float)a[0],1f-(float)a[1]);}
            for(int i=0;i<ix.Count;i++)tri[i]=(int)ix[i];
            m=new Mesh();m.name="LoLVfxMesh_"+((string)j["name"]??Path.GetFileNameWithoutExtension(rel));if(v.Length>65535)m.indexFormat=UnityEngine.Rendering.IndexFormat.UInt32;
            m.vertices=v;if(tc!=null&&tc.Length==v.Length)m.uv=tc;m.triangles=tri;m.RecalculateNormals();m.RecalculateBounds();
            DariusLog.DebugInfo("LOL-VFX-MESH","Loaded converted SCB rel="+rel+" vertices="+v.Length+" triangles="+(tri.Length/3));
        }
        catch(Exception e){DariusLog.Exception("LOL-VFX-MESH",e,"rel="+rel);m=null;}
        Meshes[rel]=m;return m;
    }

    private static Mesh QuadMesh()
    {
        if(_quad!=null)return _quad; _quad=new Mesh();_quad.name="LoLVfx_ArbitraryQuad";
        _quad.vertices=new[]{new Vector3(-0.5f,-0.5f,0f),new Vector3(0.5f,-0.5f,0f),new Vector3(0.5f,0.5f,0f),new Vector3(-0.5f,0.5f,0f)};
        _quad.uv=new[]{new Vector2(0,0),new Vector2(1,0),new Vector2(1,1),new Vector2(0,1)};_quad.triangles=new[]{0,2,1,0,3,2};_quad.RecalculateNormals();return _quad;
    }

    private static bool ReadBoolToken(JToken t,bool fallback){try{return t!=null&&t.Type!=JTokenType.Null?(bool)t:fallback;}catch{return fallback;}}
    private static float ReadFloatToken(JToken t,float fallback){try{return t!=null&&t.Type!=JTokenType.Null?(float)t:fallback;}catch{return fallback;}}
    private static Vector3 ReadPointShapeOffset(JObject shape)
    {
        if (shape == null || !string.Equals((string)shape["type"], "VfxShapePointDoNotUse", StringComparison.OrdinalIgnoreCase)) return Vector3.zero;
        JObject fields = shape["fields"] as JObject;
        if (fields == null) return Vector3.zero;
        // 0xe5f268dd is the authored point offset in Riot's VfxShapePointDoNotUse.
        JArray a = fields["e5f268dd"] as JArray;
        if (a == null || a.Count < 3) return Vector3.zero;
        return new Vector3((float)a[0], (float)a[1], (float)a[2]);
    }
    private static int ReadIntToken(JToken t,int fallback){try{return t!=null&&t.Type!=JTokenType.Null?(int)t:fallback;}catch{return fallback;}}
    private static float ReadConstantFloat(JObject d,float fallback)
    {
        if(d==null)return fallback;JToken c=d["constant"];if(c==null)return fallback;JArray a=c as JArray;return a!=null&&a.Count>0?(float)a[0]:ReadFloatToken(c,fallback);
    }
    private static Vector3 ReadConstantVector3(JObject d,Vector3 fallback)
    {
        if(d==null)return fallback;JToken c=d["constant"];JArray a=c as JArray;if(a!=null){float x=a.Count>0?(float)a[0]:fallback.x;float y=a.Count>1?(float)a[1]:x;float z=a.Count>2?(float)a[2]:x;return new Vector3(x,y,z);}return fallback;
    }
    private static Vector2 ReadConstantVector2(JObject d, Vector2 fallback)
    {
        if (d == null) return fallback; JArray a = d["constant"] as JArray;
        if (a == null) return fallback;
        float x = a.Count > 0 ? (float)a[0] : fallback.x;
        float y = a.Count > 1 ? (float)a[1] : fallback.y;
        return new Vector2(x, y);
    }
    private static Color ReadConstantColor(JObject d,Color fallback)
    {
        if(d==null)return fallback;JArray a=d["constant"] as JArray;if(a==null||a.Count<3)return fallback;return new Color((float)a[0],(float)a[1],(float)a[2],a.Count>3?(float)a[3]:1f);
    }
    private static Color ReadInitialColor(JObject d, Color fallback)
    {
        if (d == null) return fallback;
        JArray c = d["constant"] as JArray;
        if (c != null && c.Count >= 3) return new Color((float)c[0], (float)c[1], (float)c[2], c.Count > 3 ? (float)c[3] : 1f);
        JArray values = d["values"] as JArray;
        JArray first = values != null && values.Count > 0 ? values[0] as JArray : null;
        if (first != null && first.Count >= 3) return new Color((float)first[0], (float)first[1], (float)first[2], first.Count > 3 ? (float)first[3] : 1f);
        return fallback;
    }
    private static Color MultiplyColor(Color a, Color b)
    {
        return new Color(a.r * b.r, a.g * b.g, a.b * b.b, a.a * b.a);
    }
    private static void ApplyMaterialColor(Material material, Color color)
    {
        if (material == null) return;
        if (material.HasProperty("_Color")) material.SetColor("_Color", color);
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_TintColor")) material.SetColor("_TintColor", color);
    }
    private static ParticleSystem.MinMaxCurve ToMinMaxCurve(JObject d,float fallback)
    {
        if(d==null)return new ParticleSystem.MinMaxCurve(fallback);float c=ReadConstantFloat(d,fallback);JArray pt=d["probabilityTables"] as JArray;
        if(pt!=null&&pt.Count>0){JObject p0=pt[0] as JObject; JArray v=p0!=null?p0["values"] as JArray:null;if(v!=null&&v.Count>=2){float a=Scalar(v[0],c),b=Scalar(v[v.Count-1],c);return new ParticleSystem.MinMaxCurve(Mathf.Min(a,b),Mathf.Max(a,b));}}
        return new ParticleSystem.MinMaxCurve(c);
    }
    private static ParticleSystem.MinMaxCurve ScaleCurve(ParticleSystem.MinMaxCurve curve, float factor)
    {
        if (Mathf.Approximately(factor, 1f)) return curve;
        if (curve.mode == ParticleSystemCurveMode.Constant) curve.constant *= factor;
        else if (curve.mode == ParticleSystemCurveMode.TwoConstants)
        {
            curve.constantMin *= factor; curve.constantMax *= factor;
        }
        else curve.curveMultiplier *= factor;
        return curve;
    }
    private static ParticleSystem.MinMaxCurve ToAxisCurve(JObject d,int axis,float fallback)
    {
        if(d==null)return new ParticleSystem.MinMaxCurve(fallback);Vector3 c=ReadConstantVector3(d,new Vector3(fallback,fallback,fallback));float cv=axis==0?c.x:(axis==1?c.y:c.z);
        JArray pt=d["probabilityTables"] as JArray;if(pt!=null&&pt.Count>axis){JObject pa=pt[axis] as JObject; JArray v=pa!=null?pa["values"] as JArray:null;if(v!=null&&v.Count>=2){float a=Scalar(v[0],1f)*cv,b=Scalar(v[v.Count-1],1f)*cv;return new ParticleSystem.MinMaxCurve(Mathf.Min(a,b),Mathf.Max(a,b));}}
        return new ParticleSystem.MinMaxCurve(cv);
    }
    private static float Scalar(JToken t,float fallback){try{JArray a=t as JArray;return a!=null&&a.Count>0?(float)a[0]:(float)t;}catch{return fallback;}}
    private static AnimationCurve BuildAnimationCurve(JObject d,int axis,float fallback)
    {
        JArray times=d!=null?d["times"] as JArray:null, vals=d!=null?d["values"] as JArray:null;if(times==null||vals==null||times.Count==0)return AnimationCurve.Linear(0f,fallback,1f,fallback);
        int n=Mathf.Min(times.Count,vals.Count);Keyframe[] keys=new Keyframe[n];for(int i=0;i<n;i++){JArray a=vals[i] as JArray;float v=a!=null&&a.Count>axis?(float)a[axis]:Scalar(vals[i],fallback);keys[i]=new Keyframe(Mathf.Clamp01((float)times[i]),v);}return new AnimationCurve(keys);
    }
}
