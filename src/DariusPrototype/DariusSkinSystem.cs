using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;
using Mirror;
using UnityEngine;

// Darius visual/animation layer for the independent Hero_Darius traveler.
// v0.18 removes the old F4 Vesper-hosted cosmetic path entirely: active gameplay and
// lobby presentation now resolve only the clean Skin_Darius_Default model.

// Public animation bridge used by Hero_Darius skills and its native basic attack.
// The clean traveler model is authoritative; retarget calls are defensive loading fallbacks only.
public static class DariusSkinAnimationHooks
{
    // Skills own the animation request. Hero_Darius first targets the clean traveler GLB;
    // the retarget API remains only as a defensive fallback if the model has not loaded yet.
    public static void PlayQ(Hero hero, bool instant = false)
    {
        DariusTravelerModelInstance traveler = FindTraveler(hero);
        if (traveler != null) { traveler.PlayQ(instant); return; }
        // Instant Decimate is a real animation-mechanic change: skip Spell1_IN entirely and start
        // the spinning Spell1 attack on the cast frame. Normal Q keeps the authored windup clip.
        DariusModelAnimationRuntime.DariusRetargetApi.Play(hero, instant ? "Spell1" : "Darius_Spell1_IN.anm", false, 1f);
    }

    public static void PlayW(Hero hero)
    {
        PlayW(hero, hero != null ? hero.transform.forward : Vector3.forward);
    }

    public static void PlayW(Hero hero, Vector3 direction)
    {
        DariusTravelerModelInstance traveler = FindTraveler(hero);
        if (traveler != null) { traveler.PlayWAttack(direction); return; }
        DariusModelAnimationRuntime.DariusRetargetApi.Play(hero, "Spell2", false, 1f);
    }

    public static void SetWArmed(Hero hero, bool armed)
    {
        DariusTravelerModelInstance traveler = FindTraveler(hero);
        if (traveler != null) { traveler.SetWArmed(armed); return; }
        if (armed) DariusModelAnimationRuntime.DariusRetargetApi.Play(hero, "Spell2_Idle", true, 1f);
        else DariusModelAnimationRuntime.DariusRetargetApi.Stop(hero);
    }

    public static void PlayE(Hero hero)
    {
        DariusTravelerModelInstance traveler = FindTraveler(hero);
        if (traveler != null) { traveler.PlayOneShot("Spell3"); return; }
        DariusModelAnimationRuntime.DariusRetargetApi.Play(hero, "Spell3", false, 1f);
    }

    public static void PlayR(Hero hero)
    {
        DariusTravelerModelInstance traveler = FindTraveler(hero);
        if (traveler != null) { traveler.PlayOneShot("Spell4"); return; }
        DariusModelAnimationRuntime.DariusRetargetApi.Play(hero, "Spell4", false, 1f);
    }

    public static void PlayAttack(Hero hero, bool alternate, bool critical = false)
    {
        PlayAttack(hero, alternate, critical, hero != null ? hero.transform.forward : Vector3.forward);
    }

    public static void PlayAttack(Hero hero, bool alternate, bool critical, Vector3 direction)
    {
        string clip = critical ? "Crit" : (alternate ? "Attack2" : "Attack1");
        DariusTravelerModelInstance traveler = FindTraveler(hero);
        if (traveler != null) { traveler.PlayAttack(alternate, critical, direction); return; }
        DariusModelAnimationRuntime.DariusRetargetApi.Play(hero, clip, false, 1f);
    }

    // VFX is attached directly to the animated Darius skeleton.
    // The supplied GLB exposes Riot-style buff bones, so effects can follow the axe throughout
    // Spell1/2/3/4 and the basic-attack tracks instead of looking detached from the animation.
    public static bool IsGodKing(Hero hero)
    {
        DariusTravelerModelInstance traveler = FindTraveler(hero);
        return traveler != null && traveler.IsGodKingSkin;
    }

    public static string GetVariantKey(Hero hero)
    {
        DariusTravelerModelInstance traveler = FindTraveler(hero);
        return traveler != null && !string.IsNullOrEmpty(traveler.VariantKey) ? traveler.VariantKey : "Classic";
    }

    public static Transform GetWeaponAnchor(Hero hero)
    {
        return GetAnchor(hero, "BuffBone_Glb_Weapon_1", "Weapon", "R_BuffBone_Glb_Hand_Loc", "R_Hand");
    }

    public static Transform GetChestAnchor(Hero hero)
    {
        return GetAnchor(hero, "C_BuffBone_Glb_Chest_Loc", "Chest", "Spine");
    }

    public static Transform GetAnchor(Hero hero, params string[] names)
    {
        if (hero == null || names == null) return null;
        DariusTravelerModelInstance traveler = FindTraveler(hero);
        if (traveler != null)
        {
            for (int i = 0; i < names.Length; i++)
            {
                Transform t = traveler.GetAnchor(names[i]);
                if (t != null) return t;
            }
        }
        return hero.transform;
    }

    public static GameObject CreateSubmeshOverlay(Hero hero, uint sourceMaterialHash, Material overlayMaterial)
    {
        DariusTravelerModelInstance traveler = FindTraveler(hero);
        return traveler != null ? traveler.CreateSubmeshOverlay(sourceMaterialHash, overlayMaterial) : null;
    }

    // Riot VfxPrimitiveAttachedMesh does not always serialize explicit submesh hashes. In that
    // form the primitive targets the traveler's visible avatar mesh as a whole. Treating an empty
    // hash list as unsupported dropped Mecha AvatarFlash/ScreenspaceLines/pauldron layers entirely.
    // Use the already split visible runtime mesh so authored-hidden recall/wolf/throne geometry is
    // never exposed by a full-avatar overlay.
    public static GameObject CreateFullMeshOverlay(Hero hero, Material overlayMaterial, string label)
    {
        DariusTravelerModelInstance traveler = FindTraveler(hero);
        return traveler != null ? traveler.CreateFullMeshOverlay(overlayMaterial, label) : null;
    }

    private static DariusTravelerModelInstance FindTraveler(Hero hero)
    {
        if (hero == null) return null;
        return hero.GetComponentInChildren<DariusTravelerModelInstance>(true);
    }

}

// Hero_Darius basic-attack visual adapter.
// v0.18 no longer watches a foreign hero attack event and never scans the scene to delete
// another traveler's effects. Hero_Darius owns At_DariusAxe; its native
// AttackTrigger.OnCastStart hook calls NotifyNativeAttackStarted at the same point the stock
// combat pipeline starts an attack.
public sealed class DariusBasicAttackVisualRuntime : MonoBehaviour
{
    private Hero _hero;
    private bool _alternate;
    private float _lastImpactFxAt;

    public void Bind(Hero hero)
    {
        if (_hero == hero) return;
        Unbind();
        _hero = hero;
        if (_hero == null) return;
        try { _hero.ActorEvent_OnAttackHit += OnAttackHit; }
        catch (Exception e) { DariusLog.Exception("ATK-NATIVE", e, "subscribe ActorEvent_OnAttackHit failed"); }
        DariusLog.Info("ATK-NATIVE", "Darius native basic-attack visual adapter bound to " + DariusLog.EntityLabel(_hero));
    }

    public void NotifyNativeAttackStarted(int configIndex, CastInfo info)
    {
        if (_hero == null) return;

        // The range guide must follow the exact live AttackTrigger configuration. Never hard-code
        // a second gameplay radius here: if balance changes At_DariusAxe, the visual updates with it.
        float range = At_DariusAxe.AttackRange;
        try
        {
            At_DariusAxe attack = DariusTravelerRegistry.AttackPrefab;
            if (attack != null && attack.configs != null && configIndex >= 0 && configIndex < attack.configs.Length)
            {
                TriggerConfig cfg = attack.configs[configIndex];
                if (cfg != null)
                {
                    float effective = cfg.effectiveRange;
                    if (effective > 0.05f) range = effective;
                    else if (cfg.castMethod != null && cfg.castMethod._range > 0.05f) range = cfg.castMethod._range;
                }
            }
        }
        catch (Exception e)
        {
            DariusLog.Exception("ATK-RANGE-VFX", e, "Could not read live attack range; using At_DariusAxe.AttackRange fallback.");
        }

        Vector3 forward = _hero.transform.forward;
        try
        {
            DariusDirectionalBasicAttackState aim = _hero.GetComponent<DariusDirectionalBasicAttackState>();
            if (aim != null) forward = aim.GetDirection(forward);
            else forward = DariusDirectionalBasicAttackGeometry.ResolveAimDirection(_hero, info);
        }
        catch { }
        // Range-guide VFX intentionally disabled: gameplay reach includes target/contact geometry,
        // so a hard radial arc is both visually abrupt and misleading. Keep only the weapon swing.

        DariusCripplingStrikeRuntime w = _hero.GetComponent<DariusCripplingStrikeRuntime>();
        bool empowered = w != null && w.IsArmed;
        if (empowered)
        {
            w.NotifyNativeAttackStarted(forward);
            DariusLog.DebugInfo("ATK-NATIVE", "At_DariusAxe cast start -> W/Spell2 owns the swing animation; configuredRange=" + range.ToString("0.###"));
            return;
        }

        _alternate = !_alternate;
        DariusSkinAnimationHooks.PlayAttack(_hero, _alternate, configIndex == 1, forward);
        DariusPrototypeVfx.CreateBasicAttackSwing(_hero, _alternate);
        NetworkIdentity identity = _hero.GetComponent<NetworkIdentity>();
        TravelerBasicAttackVfxReplication.Broadcast(identity, 0, (byte)(_alternate ? 1 : 0), configIndex == 1,
            _hero.transform.position, forward);
        DariusLog.DebugInfo("ATK-NATIVE", "At_DariusAxe cast start -> " + (configIndex == 1 ? "Crit" : (_alternate ? "Attack2" : "Attack1")) +
            " + Darius axe trail/SFX; configuredRange=" + range.ToString("0.###"));
    }

    private void OnAttackHit(EventInfoAttackHit info)
    {
        if (_hero == null || info.attacker != _hero || info.victim == null) return;

        Vector3 delta = info.victim.transform.position - _hero.transform.position;
        delta.y = 0f;
        float contactDist = delta.magnitude, sectorAngle = -1f; Vector3 contact = info.victim.transform.position;
        try { DariusDirectionalBasicAttackGeometry.IsInsideAttackSector(_hero as Hero_Darius, info.victim, out contactDist, out sectorAngle, out contact); } catch { }
        DariusDirectionalBasicAttackState attackState = _hero.GetComponent<DariusDirectionalBasicAttackState>();
        DariusLog.Info("ATK-HIT", "victim=" + DariusLog.EntityLabel(info.victim) +
            " attacker=" + DariusLog.EntityLabel(_hero) +
            " centerDist=" + delta.magnitude.ToString("0.###") +
            " contactDist=" + contactDist.ToString("0.###") + " angle=" + sectorAngle.ToString("0.##") +
            " swingSerial=" + (attackState != null ? attackState.Serial.ToString() : "0"));

        if (Time.unscaledTime - _lastImpactFxAt < 0.11f) return;
        _lastImpactFxAt = Time.unscaledTime;
        try { DariusPrototypeVfx.CreateBasicAttackImpact(_hero, info.victim.transform.position); }
        catch (Exception e) { DariusLog.Exception("ATK-NATIVE", e, "basic attack impact VFX failed"); }
        TravelerBasicAttackVfxReplication.Broadcast(_hero.GetComponent<NetworkIdentity>(), 1, 0, false,
            info.victim.transform.position, Vector3.zero);
    }

    private void OnReplicatedTravelerBasicAttackVfx(TravelerBasicAttackVfxMessage message)
    {
        if (_hero == null) Bind(GetComponent<Hero>());
        if (_hero == null) return;
        if (message.phase == 0)
        {
            _alternate = message.variant != 0;
            DariusSkinAnimationHooks.PlayAttack(_hero, _alternate, message.critical != 0, message.direction);
            DariusPrototypeVfx.CreateBasicAttackSwing(_hero, _alternate);
        }
        else if (message.phase == 1) DariusPrototypeVfx.CreateBasicAttackImpact(_hero, message.position);
    }

    private void OnDestroy() { Unbind(); }

    private void Unbind()
    {
        if (_hero != null)
        {
            try { _hero.ActorEvent_OnAttackHit -= OnAttackHit; } catch { }
        }
        _hero = null;
    }
}

// The pre-v0.18 DariusSkinInstance/Vesper rig-diagnostic implementation was removed.
// Skin_Darius_Default is now the sole Darius visual path.

// Purpose-built glTF 2.0/GLB reader for the supplied Darius model.
// It supports the subset present in the file: one skinned triangle mesh, PNG base-color texture,
// 4 influences per vertex, and LINEAR TRS animation tracks.
public sealed class DariusGlbRuntimeModel
{
    public GameObject root { get; private set; }
    public int vertexCount { get; private set; }
    public int triangleCount { get; private set; }
    public int primitiveCount { get; private set; }
    public int boneCount { get; private set; }
    public int animationCount => _clips.Count;
    public string currentAnimation => _current != null ? _current.name : null;
    public bool isPlayingOneShot => isPlayingFullBodyOneShot || _upper != null;
    public bool isPlayingFullBodyOneShot => _current != null && !_loop;
    public bool isPlayingUpperBody => _upper != null;

    private JObject _json;
    private byte[] _bin;
    private Transform[] _nodes;
    private Vector3[] _basePos;
    private Quaternion[] _baseRot;
    private Vector3[] _baseScale;
    private readonly Dictionary<string, RuntimeClip> _clips = new Dictionary<string, RuntimeClip>(StringComparer.Ordinal);
    private readonly Dictionary<int, float[]> _floatAccessorCache = new Dictionary<int, float[]>();
    private RuntimeClip _current;
    private float _time;
    private bool _loop;
    private float _basePlaybackSpeed = 1f;
    private Vector3[] _blendFromPos;
    private Quaternion[] _blendFromRot;
    private Vector3[] _blendFromScale;
    private float _blendTime;
    private float _blendDuration;
    // Death is a one-shot that must hold its final authored pose until the Hero is revived or destroyed.
    private bool _holdBaseAtEnd;
    private RuntimeClip _upper;
    private float _upperTime;
    private float _upperPlaybackSpeed = 1f;
    // Moving basic attacks use an action overlay that keeps Root/Pelvis/upper-body/weapon
    // while Run/Idle continues to drive the leg branch.
    private bool _upperUsesLocomotionMask;
    // Full-body actions keep Root/Pelvis/torso/weapon authority. This independent sampler
    // replaces only real leg chains while the Hero translates during an action.
    private RuntimeClip _lowerLocomotion;
    private float _lowerLocomotionTime;
    private float _lowerLocomotionSpeed = 1f;
    private float _lowerTargetYaw;
    private float _lowerAppliedYaw;
    private bool _lowerLocomotionActive;
    private readonly List<Mesh> _meshes = new List<Mesh>();
    private readonly List<Material> _materials = new List<Material>();
    private readonly Dictionary<string, Material> _materialBySourceName = new Dictionary<string, Material>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Color> _materialBaseColor = new Dictionary<string, Color>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _materialPrimitiveIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    private SkinnedMeshRenderer _skinRenderer;
    // Authored-hidden LoL geometry (God-King wolf/throne, Mecha recall props, etc.) must not live
    // in EntityModel.bodyRenderers. The stock hover/highlight pass renders geometry regardless of
    // our alpha-zero material and would reveal these hidden submeshes as giant green ghost shapes.
    private SkinnedMeshRenderer _hiddenSkinRenderer;
    private Mesh _overlaySourceMesh;
    private Material _invisibleOverlayMaterial;
    private readonly List<Texture2D> _textures = new List<Texture2D>();
    private readonly Dictionary<int, Texture2D> _textureCache = new Dictionary<int, Texture2D>();

    private sealed class RuntimeClip
    {
        public string name;
        public float length;
        public readonly List<RuntimeTrack> tracks = new List<RuntimeTrack>();
    }

    private sealed class RuntimeTrack
    {
        public int node;
        public string path;
        public float[] times;
        public float[] values;
        public int components;
    }

    public static DariusGlbRuntimeModel Load(GameObject parent, string path)
    {
        DariusGlbRuntimeModel model = new DariusGlbRuntimeModel();
        model.ReadGlb(path);
        model.Build(parent);
        return model;
    }

    public bool HasClip(string name)
    {
        return !string.IsNullOrEmpty(name) && _clips.ContainsKey(name);
    }

    public float GetClipLength(string name, float fallback = 0.1f)
    {
        RuntimeClip clip;
        return !string.IsNullOrEmpty(name) && _clips.TryGetValue(name, out clip) ? Mathf.Max(0.01f, clip.length) : fallback;
    }

    public Transform FindNode(string nodeName)
    {
        if (_nodes == null || string.IsNullOrEmpty(nodeName)) return null;
        for (int i = 0; i < _nodes.Length; i++)
        {
            Transform t = _nodes[i];
            if (t != null && string.Equals(t.name, nodeName, StringComparison.OrdinalIgnoreCase)) return t;
        }
        return null;
    }

    public Renderer[] GetEntityBodyRenderers()
    {
        // Only the always-visible skinned body participates in native hover/hit-highlight.
        // Runtime-hidden LoL animation props remain on _hiddenSkinRenderer and can still be
        // revealed by SetMaterialVisible, but they are deliberately excluded from highlighting.
        return _skinRenderer != null ? new Renderer[] { _skinRenderer } : new Renderer[0];
    }

    private void ReadGlb(string path)
    {
        byte[] bytes = File.ReadAllBytes(path);
        if (bytes.Length < 20 || ReadUInt32(bytes, 0) != 0x46546C67u)
            throw new InvalidDataException("Not a GLB file: " + path);
        uint version = ReadUInt32(bytes, 4);
        if (version != 2u) throw new NotSupportedException("Only GLB 2.0 is supported; version=" + version);

        int offset = 12;
        string jsonText = null;
        while (offset + 8 <= bytes.Length)
        {
            int length = (int)ReadUInt32(bytes, offset);
            uint type = ReadUInt32(bytes, offset + 4);
            offset += 8;
            if (length < 0 || offset + length > bytes.Length) throw new InvalidDataException("Corrupt GLB chunk length.");
            if (type == 0x4E4F534Au)
                jsonText = System.Text.Encoding.UTF8.GetString(bytes, offset, length).TrimEnd('\0', ' ', '\t', '\r', '\n');
            else if (type == 0x004E4942u)
            {
                _bin = new byte[length];
                Buffer.BlockCopy(bytes, offset, _bin, 0, length);
            }
            offset += length;
        }
        if (jsonText == null || _bin == null) throw new InvalidDataException("GLB JSON or BIN chunk missing.");
        _json = JObject.Parse(jsonText);
    }

    private void Build(GameObject parent)
    {
        // A preview/skin reload can recreate the runtime bridge on the same EntityModel before the
        // previous Unity object reaches end-of-frame destruction. Remove stale GLB roots up front so
        // two skinned copies can never occupy the same model container.
        for (int i = parent.transform.childCount - 1; i >= 0; i--)
        {
            Transform child = parent.transform.GetChild(i);
            if (child != null && string.Equals(child.name, "Darius_GLB_Runtime", StringComparison.Ordinal))
            {
                try { UnityEngine.Object.DestroyImmediate(child.gameObject); } catch { UnityEngine.Object.Destroy(child.gameObject); }
                DariusLog.DebugInfo("TRAVELER-MODEL", "Removed stale runtime GLB root before rebuild: Darius_GLB_Runtime");
            }
        }

        root = new GameObject("Darius_GLB_Runtime");
        root.transform.SetParent(parent.transform, false);

        BuildNodes();
        BuildMeshAndMaterial();
        BuildAnimations();
    }

    public bool SetMaterialVisible(string sourceName, bool visible)
    {
        if (string.IsNullOrEmpty(sourceName)) return false;
        Material material;
        if (!_materialBySourceName.TryGetValue(sourceName, out material) || material == null) return false;
        Color baseColor;
        if (!_materialBaseColor.TryGetValue(sourceName, out baseColor)) baseColor = Color.white;
        Color c = baseColor; c.a = visible ? Mathf.Max(0.001f, baseColor.a) : 0f;
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", c);
        if (material.HasProperty("_Color")) material.SetColor("_Color", c);
        material.color = c;
        DariusLog.DebugInfo("SKIN-SUBMESH", "material=" + sourceName + " visible=" + visible + " alpha=" + c.a.ToString("0.###"));
        return true;
    }

    public GameObject CreateSubmeshOverlay(uint sourceMaterialHash, Material overlayMaterial)
    {
        if (_skinRenderer == null || _overlaySourceMesh == null || overlayMaterial == null) return null;
        int primitiveIndex = -1;
        string sourceName = null;
        foreach (KeyValuePair<string, int> kv in _materialPrimitiveIndex)
        {
            if (RiotStringHash(kv.Key) == sourceMaterialHash)
            {
                sourceName = kv.Key; primitiveIndex = kv.Value; break;
            }
        }
        if (primitiveIndex < 0 || primitiveIndex >= _overlaySourceMesh.subMeshCount)
        {
            DariusLog.Warn("LOL-VFX-ATTACHED", "Unable to resolve submesh hash=0x" + sourceMaterialHash.ToString("x8"));
            return null;
        }

        GameObject go = new GameObject("LoL_AttachedMeshOverlay_" + sourceName);
        go.transform.SetParent(_skinRenderer.transform, false);
        SkinnedMeshRenderer r = go.AddComponent<SkinnedMeshRenderer>();
        r.updateWhenOffscreen = true;
        r.quality = SkinQuality.Bone4;
        r.sharedMesh = _overlaySourceMesh;
        r.bones = _skinRenderer.bones;
        r.rootBone = _skinRenderer.rootBone;
        Material[] mats = new Material[r.sharedMesh.subMeshCount];
        Material invisible = GetInvisibleOverlayMaterial(overlayMaterial);
        for (int i = 0; i < mats.Length; i++) mats[i] = invisible;
        mats[primitiveIndex] = overlayMaterial;
        r.sharedMaterials = mats;
        DariusLog.Info("LOL-VFX-ATTACHED", "Applied Riot AttachedMesh overlay material=" + sourceName +
            " hash=0x" + sourceMaterialHash.ToString("x8") + " primitiveIndex=" + primitiveIndex);
        return go;
    }

    public GameObject CreateFullMeshOverlay(Material overlayMaterial, string label)
    {
        if (_skinRenderer == null || _skinRenderer.sharedMesh == null || overlayMaterial == null) return null;
        Mesh visibleMesh = _skinRenderer.sharedMesh;
        GameObject go = new GameObject("LoL_AttachedMeshOverlay_Full_" + (string.IsNullOrEmpty(label) ? "Avatar" : label));
        go.transform.SetParent(_skinRenderer.transform, false);
        SkinnedMeshRenderer r = go.AddComponent<SkinnedMeshRenderer>();
        r.updateWhenOffscreen = true;
        r.quality = SkinQuality.Bone4;
        r.sharedMesh = visibleMesh;
        r.bones = _skinRenderer.bones;
        r.rootBone = _skinRenderer.rootBone;
        Material[] mats = new Material[Mathf.Max(1, visibleMesh.subMeshCount)];
        for (int i = 0; i < mats.Length; i++) mats[i] = overlayMaterial;
        r.sharedMaterials = mats;
        DariusLog.Info("LOL-VFX-ATTACHED", "Applied Riot full-avatar AttachedMesh overlay label=" +
            (string.IsNullOrEmpty(label) ? "Avatar" : label) + " submeshes=" + mats.Length);
        return go;
    }

    private Material GetInvisibleOverlayMaterial(Material template)
    {
        if (_invisibleOverlayMaterial != null) return _invisibleOverlayMaterial;
        Shader shader = Shader.Find("Unlit/Transparent");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        if (shader == null && template != null) shader = template.shader;
        if (shader == null) return template;
        _invisibleOverlayMaterial = new Material(shader);
        _invisibleOverlayMaterial.name = "Darius_InvisibleSubmeshOverlay";
        Color clear = new Color(0f,0f,0f,0f);
        if (_invisibleOverlayMaterial.HasProperty("_Color")) _invisibleOverlayMaterial.SetColor("_Color", clear);
        if (_invisibleOverlayMaterial.HasProperty("_BaseColor")) _invisibleOverlayMaterial.SetColor("_BaseColor", clear);
        if (_invisibleOverlayMaterial.HasProperty("_TintColor")) _invisibleOverlayMaterial.SetColor("_TintColor", clear);
        if (_invisibleOverlayMaterial.HasProperty("_ZWrite")) _invisibleOverlayMaterial.SetFloat("_ZWrite", 0f);
        _invisibleOverlayMaterial.renderQueue = 3000;
        return _invisibleOverlayMaterial;
    }

    private static uint RiotStringHash(string text)
    {
        uint h = 2166136261u;
        if (text == null) return h;
        for (int i = 0; i < text.Length; i++)
        {
            char c = char.ToLowerInvariant(text[i]);
            h ^= (byte)c;
            h *= 16777619u;
        }
        return h;
    }

    public bool HasMaterial(string sourceName)
    {
        Material m;
        return !string.IsNullOrEmpty(sourceName) && _materialBySourceName.TryGetValue(sourceName, out m) && m != null;
    }

    private void BuildNodes()
    {
        JArray nodes = (JArray)_json["nodes"];
        _nodes = new Transform[nodes.Count];
        _basePos = new Vector3[nodes.Count];
        _baseRot = new Quaternion[nodes.Count];
        _baseScale = new Vector3[nodes.Count];

        for (int i = 0; i < nodes.Count; i++)
        {
            JObject n = (JObject)nodes[i];
            GameObject go = new GameObject((string)n["name"] ?? ("Node_" + i));
            _nodes[i] = go.transform;
            _nodes[i].SetParent(root.transform, false);
            Vector3 p = ReadVec3(n["translation"], Vector3.zero);
            Quaternion r = ReadQuat(n["rotation"], Quaternion.identity);
            Vector3 s = ReadVec3(n["scale"], Vector3.one);
            _nodes[i].localPosition = p;
            _nodes[i].localRotation = r;
            _nodes[i].localScale = s;
            _basePos[i] = p;
            _baseRot[i] = r;
            _baseScale[i] = s;
        }

        for (int i = 0; i < nodes.Count; i++)
        {
            JArray children = nodes[i]["children"] as JArray;
            if (children == null) continue;
            for (int j = 0; j < children.Count; j++)
            {
                int child = (int)children[j];
                if (child >= 0 && child < _nodes.Length)
                    _nodes[child].SetParent(_nodes[i], false);
            }
        }
    }

    private void BuildMeshAndMaterial()
    {
        JArray primitives = (JArray)_json["meshes"][0]["primitives"];
        if (primitives == null || primitives.Count == 0) throw new InvalidDataException("GLB mesh has no primitives.");
        primitiveCount = primitives.Count;

        JObject firstPrimitive = (JObject)primitives[0];
        JObject attrs = (JObject)firstPrimitive["attributes"];
        int posAccessor = (int)attrs["POSITION"];
        int uvAccessor = attrs["TEXCOORD_0"] != null ? (int)attrs["TEXCOORD_0"] : -1;
        int normalAccessor = attrs["NORMAL"] != null ? (int)attrs["NORMAL"] : -1;
        int jointsAccessor = (int)attrs["JOINTS_0"];
        int weightsAccessor = (int)attrs["WEIGHTS_0"];

        float[] pos = ReadFloatAccessor(posAccessor);
        float[] normal = normalAccessor >= 0 ? ReadFloatAccessor(normalAccessor) : null;
        float[] uv = uvAccessor >= 0 ? ReadFloatAccessor(uvAccessor) : null;
        ushort[] joints = ReadUShortAccessor(jointsAccessor);
        float[] weights = ReadFloatAccessor(weightsAccessor);

        vertexCount = pos.Length / 3;
        Vector3[] vertices = new Vector3[vertexCount];
        Vector3[] normals = normal != null && normal.Length >= vertexCount * 3 ? new Vector3[vertexCount] : null;
        Vector2[] uvs = uv != null && uv.Length >= vertexCount * 2 ? new Vector2[vertexCount] : null;
        BoneWeight[] boneWeights = new BoneWeight[vertexCount];

        for (int i = 0; i < vertexCount; i++)
        {
            vertices[i] = new Vector3(pos[i * 3], pos[i * 3 + 1], pos[i * 3 + 2]);
            if (normals != null) normals[i] = new Vector3(normal[i * 3], normal[i * 3 + 1], normal[i * 3 + 2]).normalized;
            if (uvs != null) uvs[i] = new Vector2(uv[i * 2], 1f - uv[i * 2 + 1]);
            int o = i * 4;
            boneWeights[i] = new BoneWeight
            {
                boneIndex0 = joints[o], boneIndex1 = joints[o + 1], boneIndex2 = joints[o + 2], boneIndex3 = joints[o + 3],
                weight0 = weights[o], weight1 = weights[o + 1], weight2 = weights[o + 2], weight3 = weights[o + 3]
            };
        }

        Mesh mesh = new Mesh();
        mesh.name = "Darius_RuntimeMesh";
        // Some dense LoL skin exports can exceed the 16-bit runtime Mesh vertex limit.
        // Unity still accepts the source's ushort index buffers, but the runtime Mesh itself should
        // be allowed to address more than 65535 vertices if a later skin needs it.
        if (vertexCount > 65535) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.vertices = vertices;
        if (normals != null) mesh.normals = normals;
        if (uvs != null) mesh.uv = uvs;
        mesh.boneWeights = boneWeights;
        mesh.subMeshCount = primitives.Count;

        triangleCount = 0;
        Material[] renderMaterials = new Material[primitives.Count];
        bool[] authoredVisiblePrimitives = new bool[primitives.Count];
        for (int pi = 0; pi < primitives.Count; pi++)
        {
            JObject primitive = (JObject)primitives[pi];
            ushort[] indices = ReadUShortAccessor((int)primitive["indices"]);
            int[] tris = new int[indices.Length];
            for (int i = 0; i < indices.Length; i += 3)
            {
                // The exported LoL model nodes use a mirrored scale. Reverse winding so every
                // submesh remains front-facing under URP and legacy shaders.
                tris[i] = indices[i];
                tris[i + 1] = indices[i + 2];
                tris[i + 2] = indices[i + 1];
            }
            triangleCount += tris.Length / 3;
            mesh.SetTriangles(tris, pi, false);
            int materialIndex = primitive["material"] != null ? (int)primitive["material"] : -1;
            bool authoredVisible;
            renderMaterials[pi] = CreateMaterial(materialIndex, pi, out authoredVisible);
            authoredVisiblePrimitives[pi] = authoredVisible;
        }

        if (normals == null) mesh.RecalculateNormals();

        JObject skin = (JObject)_json["skins"][0];
        JArray jointArray = (JArray)skin["joints"];
        Transform[] bones = new Transform[jointArray.Count];
        for (int i = 0; i < jointArray.Count; i++) bones[i] = _nodes[(int)jointArray[i]];
        boneCount = bones.Length;

        GameObject meshNode = _nodes[0].gameObject;
        SkinnedMeshRenderer smr = meshNode.AddComponent<SkinnedMeshRenderer>();
        smr.updateWhenOffscreen = true;
        // Preserve all four authored influences. Lower global skin-quality settings can otherwise
        // drop influences on dense LoL skin rigs and tear clothing/torso vertices apart.
        smr.quality = SkinQuality.Bone4;
        smr.bones = bones;
        smr.rootBone = _nodes[(int)jointArray[0]];

        Matrix4x4[] bindPoses = BuildBindPoses(skin, bones, meshNode.transform);
        mesh.bindposes = bindPoses;
        mesh.RecalculateBounds();
        _overlaySourceMesh = mesh;

        bool hasAuthoredHidden = false;
        for (int pi = 0; pi < authoredVisiblePrimitives.Length; pi++)
            if (!authoredVisiblePrimitives[pi]) { hasAuthoredHidden = true; break; }

        Mesh visibleMesh = mesh;
        if (hasAuthoredHidden)
        {
            // God-King is the only current Darius GLB with authored-hidden wolf/throne primitives.
            // If Unity rejects the runtime split for any reason, do not abort the entire model load:
            // the source materials are already authored-hidden, so falling back to the unsplit mesh
            // is visually safer than returning a null EntityModel to CharacterModelDisplay.
            try
            {
                Mesh visibleOnly = UnityEngine.Object.Instantiate(mesh);
                Mesh hiddenOnly = UnityEngine.Object.Instantiate(mesh);
                if (visibleOnly == null || hiddenOnly == null) throw new InvalidOperationException("Unity mesh clone returned null during authored-hidden split.");
                visibleOnly.name = "Darius_RuntimeMesh_VisibleOnly";
                hiddenOnly.name = "Darius_RuntimeMesh_AuthoredHiddenOnly";
                int hiddenCount = 0;
                for (int pi = 0; pi < authoredVisiblePrimitives.Length; pi++)
                {
                    if (authoredVisiblePrimitives[pi]) hiddenOnly.SetTriangles(Array.Empty<int>(), pi, false);
                    else { visibleOnly.SetTriangles(Array.Empty<int>(), pi, false); hiddenCount++; }
                }
                visibleOnly.RecalculateBounds(); hiddenOnly.RecalculateBounds();
                SkinnedMeshRenderer hiddenSmr = meshNode.AddComponent<SkinnedMeshRenderer>();
                if (hiddenSmr == null) throw new InvalidOperationException("Could not create authored-hidden SkinnedMeshRenderer.");
                hiddenSmr.updateWhenOffscreen = true; hiddenSmr.quality = SkinQuality.Bone4;
                hiddenSmr.bones = bones; hiddenSmr.rootBone = _nodes[(int)jointArray[0]];
                hiddenSmr.sharedMesh = hiddenOnly; hiddenSmr.sharedMaterials = renderMaterials;
                _hiddenSkinRenderer = hiddenSmr; visibleMesh = visibleOnly;
                _meshes.Add(visibleOnly); _meshes.Add(hiddenOnly);
                DariusLog.Info("SKIN-HIGHLIGHT", "Separated authored-hidden submeshes from native body renderer hidden=" + hiddenCount +
                    " total=" + authoredVisiblePrimitives.Length + "; hover/highlight can no longer reveal hidden LoL props.");
            }
            catch (Exception splitError)
            {
                visibleMesh = mesh; _hiddenSkinRenderer = null;
                SkinnedMeshRenderer[] renderers = meshNode.GetComponents<SkinnedMeshRenderer>();
                for (int ri = 0; ri < renderers.Length; ri++)
                    if (renderers[ri] != null && renderers[ri] != smr) { try { UnityEngine.Object.Destroy(renderers[ri]); } catch { } }
                DariusLog.Exception("SKIN-HIGHLIGHT", splitError, "Authored-hidden mesh split failed; continuing with source mesh/material visibility instead of failing the whole God-King model.");
            }
        }

        smr.sharedMesh = visibleMesh;
        smr.sharedMaterials = renderMaterials;
        _skinRenderer = smr;
        _meshes.Add(mesh);
    }

    private Matrix4x4[] BuildBindPoses(JObject skin, Transform[] bones, Transform meshTransform)
    {
        if (skin != null && skin["inverseBindMatrices"] != null)
        {
            try
            {
                int accessorIndex = (int)skin["inverseBindMatrices"];
                float[] authored = ReadFloatAccessor(accessorIndex);
                if (authored != null && authored.Length >= bones.Length * 16)
                {
                    Matrix4x4[] exact = new Matrix4x4[bones.Length];
                    for (int i = 0; i < bones.Length; i++)
                    {
                        int o = i * 16;
                        Matrix4x4 m = new Matrix4x4();
                        m.SetColumn(0, new Vector4(authored[o], authored[o + 1], authored[o + 2], authored[o + 3]));
                        m.SetColumn(1, new Vector4(authored[o + 4], authored[o + 5], authored[o + 6], authored[o + 7]));
                        m.SetColumn(2, new Vector4(authored[o + 8], authored[o + 9], authored[o + 10], authored[o + 11]));
                        m.SetColumn(3, new Vector4(authored[o + 12], authored[o + 13], authored[o + 14], authored[o + 15]));
                        exact[i] = m;
                    }
                    DariusLog.Info("GLB-BINDPOSE", "Using authored inverseBindMatrices count=" + bones.Length);
                    return exact;
                }
                DariusLog.Warn("GLB-BINDPOSE", "inverseBindMatrices accessor invalid; using derived fallback values=" + (authored != null ? authored.Length.ToString() : "<null>") + " bones=" + bones.Length);
            }
            catch (Exception e) { DariusLog.Exception("GLB-BINDPOSE", e, "Failed reading authored inverseBindMatrices; using derived fallback"); }
        }
        Matrix4x4[] fallback = new Matrix4x4[bones.Length];
        for (int i = 0; i < bones.Length; i++) fallback[i] = bones[i].worldToLocalMatrix * meshTransform.localToWorldMatrix;
        return fallback;
    }

    private Material CreateMaterial(int materialIndex, int primitiveIndex, out bool authoredVisible)
    {
        authoredVisible = true;
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Texture");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Standard");
        if (shader == null) throw new InvalidOperationException("No usable Unity shader was found for the runtime GLB model.");

        Material material = new Material(shader);
        JObject source = null;
        JArray allMaterials = _json["materials"] as JArray;
        if (allMaterials != null && materialIndex >= 0 && materialIndex < allMaterials.Count) source = allMaterials[materialIndex] as JObject;
        string sourceName = source != null && source["name"] != null ? (string)source["name"] : ("Darius_RuntimeMaterial_" + primitiveIndex);
        material.name = sourceName + "_Runtime";

        Texture2D texture = LoadBaseColorTexture(source);
        if (texture != null)
        {
            if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
            if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", texture);
        }

        Color tint = Color.white;
        try
        {
            JArray factor = source != null ? source["pbrMetallicRoughness"]?["baseColorFactor"] as JArray : null;
            if (factor != null && factor.Count >= 4) tint = new Color((float)factor[0], (float)factor[1], (float)factor[2], (float)factor[3]);
        }
        catch { }
        authoredVisible = true;
        try
        {
            if (source != null && source["extras"] != null && source["extras"]["visible"] != null)
                authoredVisible = (bool)source["extras"]["visible"];
        }
        catch { authoredVisible = true; }

        string alphaMode = source != null && source["alphaMode"] != null ? (string)source["alphaMode"] : null;
        if (!authoredVisible)
        {
            // Hidden LoL submeshes such as God-King Wolf_Mat are revealed by runtime animation
            // events. URP/Unlit does not become a valid transparent material by only changing the
            // _Surface float at runtime; the wolf therefore remained invisible even after alpha=1.
            // Use a shader with an actual transparent pass for authored-hidden submeshes so the
            // existing SetMaterialVisible event can reliably reveal the original skinned geometry.
            Shader transparentShader = Shader.Find("Unlit/Transparent");
            if (transparentShader == null) transparentShader = Shader.Find("Sprites/Default");
            if (transparentShader != null && material.shader != transparentShader)
            {
                material.shader = transparentShader;
                if (texture != null)
                {
                    if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", texture);
                    if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
                }
            }
        }
        if (string.Equals(alphaMode, "BLEND", StringComparison.OrdinalIgnoreCase) || !authoredVisible)
        {
            if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
            if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0f);
            if (material.HasProperty("_SrcBlend")) material.SetFloat("_SrcBlend", 5f); // SrcAlpha
            if (material.HasProperty("_DstBlend")) material.SetFloat("_DstBlend", 10f); // OneMinusSrcAlpha
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = 3000;
        }

        _materialBySourceName[sourceName] = material;
        _materialBaseColor[sourceName] = tint;
        _materialPrimitiveIndex[sourceName] = primitiveIndex;
        Color applied = tint; if (!authoredVisible) applied.a = 0f;
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", applied);
        if (material.HasProperty("_Color")) material.SetColor("_Color", applied);
        material.color = applied;
        if (material.HasProperty("_Cull")) material.SetFloat("_Cull", 0f);
        if (!authoredVisible) DariusLog.Info("SKIN-SUBMESH", "Loaded authored-hidden material=" + sourceName + "; runtime visibility controlled by LoL animation event bridge; shader=" + material.shader.name);

        _materials.Add(material);
        return material;
    }

    private Texture2D LoadBaseColorTexture(JObject material)
    {
        try
        {
            if (material == null) return null;
            JObject pbr = material["pbrMetallicRoughness"] as JObject;
            JObject texInfo = pbr != null ? pbr["baseColorTexture"] as JObject : null;
            if (texInfo == null || texInfo["index"] == null) return null;
            int textureIndex = (int)texInfo["index"];
            Texture2D cached;
            if (_textureCache.TryGetValue(textureIndex, out cached)) return cached;

            JArray textures = _json["textures"] as JArray;
            if (textures == null || textureIndex < 0 || textureIndex >= textures.Count) return null;
            int imageIndex = (int)textures[textureIndex]["source"];
            JObject image = (JObject)_json["images"][imageIndex];
            int viewIndex = (int)image["bufferView"];
            JObject view = (JObject)_json["bufferViews"][viewIndex];
            int offset = view["byteOffset"] != null ? (int)view["byteOffset"] : 0;
            int length = (int)view["byteLength"];
            byte[] png = new byte[length];
            Buffer.BlockCopy(_bin, offset, png, 0, length);
            Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.name = image["name"] != null ? (string)image["name"] + "_Runtime" : "GLB_BaseColor_" + textureIndex;
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.filterMode = FilterMode.Bilinear;
            if (!ImageConversion.LoadImage(tex, png, false))
            {
                UnityEngine.Object.Destroy(tex);
                return null;
            }
            _textureCache[textureIndex] = tex;
            _textures.Add(tex);
            return tex;
        }
        catch (Exception e)
        {
            DariusLog.Exception("SKIN-GLB", e, "Base color texture decode failed");
            return null;
        }
    }

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

    private void ApplyClip(RuntimeClip clip, float time, bool upperBodyOnly, bool locomotionActionMask = false,
        bool lowerBodyOnly = false)
    {
        if (clip == null) return;
        for (int i = 0; i < clip.tracks.Count; i++)
        {
            RuntimeTrack t = clip.tracks[i];
            if (t.node < 0 || t.node >= _nodes.Length || _nodes[t.node] == null || t.times.Length == 0) continue;
            if (lowerBodyOnly && !IsLowerBodyLocomotionNode(t.node)) continue;
            if (upperBodyOnly)
            {
                if (locomotionActionMask)
                {
                    if (!IsLocomotionActionNode(t.node)) continue;
                }
                else if (!IsUpperBodyNode(t.node)) continue;
            }
            int k0, k1;
            float lerp;
            FindKeys(t.times, time, out k0, out k1, out lerp);
            int a = k0 * t.components;
            int b = k1 * t.components;

            if (string.Equals(t.path, "translation", StringComparison.Ordinal))
            {
                // The Darius combat clips animate Root and the separate Weapon node as a matched
                // authored pair. Suppressing Root translation while still applying Weapon is what
                // made the axe fly away from the body. Keep locomotion world movement owned by SoD,
                // but preserve the action clip's internal Root translation so the skeleton and axe
                // stay in the same source coordinate frame.
                string nodeName = _nodes[t.node] != null ? _nodes[t.node].name : null;
                bool isRootNode = string.Equals(nodeName, "Root", StringComparison.OrdinalIgnoreCase);
                if (isRootNode && ((upperBodyOnly && !locomotionActionMask) || !IsDariusCombatActionClip(clip.name))) continue;
                Vector3 v0 = new Vector3(t.values[a], t.values[a + 1], t.values[a + 2]);
                Vector3 v1 = new Vector3(t.values[b], t.values[b + 1], t.values[b + 2]);
                _nodes[t.node].localPosition = Vector3.Lerp(v0, v1, lerp);
            }
            else if (string.Equals(t.path, "scale", StringComparison.Ordinal))
            {
                // LoL exports contain scale tracks for smears, weapons and form-swap helpers.
                // Core humanoid bones must never inherit those tracks in the runtime GLB player:
                // a single bad Root/Pelvis/Spine scale is enough to fold the whole skinned model
                // into the compact "ball" seen in gameplay. Keep body scale at the bind pose.
                if (IsCoreBodyScaleNode(t.node))
                {
                    _nodes[t.node].localScale = _baseScale[t.node];
                    continue;
                }

                Vector3 v0 = new Vector3(t.values[a], t.values[a + 1], t.values[a + 2]);
                Vector3 v1 = new Vector3(t.values[b], t.values[b + 1], t.values[b + 2]);
                Vector3 scale = Vector3.Lerp(v0, v1, lerp);
                if (!IsFinite(scale) || Mathf.Abs(scale.x) > 50f || Mathf.Abs(scale.y) > 50f || Mathf.Abs(scale.z) > 50f)
                    scale = _baseScale[t.node];
                _nodes[t.node].localScale = scale;
            }
            else if (string.Equals(t.path, "rotation", StringComparison.Ordinal))
            {
                Quaternion q0 = new Quaternion(t.values[a], t.values[a + 1], t.values[a + 2], t.values[a + 3]);
                Quaternion q1 = new Quaternion(t.values[b], t.values[b + 1], t.values[b + 2], t.values[b + 3]);
                Quaternion sampled = Quaternion.Slerp(q0, q1, lerp);
                if (lowerBodyOnly && IsPrimaryLegRoot(t.node) && Mathf.Abs(_lowerAppliedYaw) > 0.1f)
                    sampled = Quaternion.AngleAxis(_lowerAppliedYaw * 0.62f, Vector3.up) * sampled;
                _nodes[t.node].localRotation = sampled;
            }
        }
    }

    private bool IsLowerBodyLocomotionNode(int node)
    {
        if (node < 0 || node >= _nodes.Length || _nodes[node] == null) return false;
        string n = (_nodes[node].name ?? string.Empty).ToLowerInvariant();
        if (n.Contains("weapon") || n.Contains("buffbone") || n.Contains("ground_loc") ||
            n.Contains("glb_foot_loc") || n.Contains("snap_") || n.Contains("doll")) return false;
        return n.Contains("hip") || n.Contains("knee") || n.Contains("leg") || n.Contains("thigh") ||
               n.Contains("calf") || n.Contains("foot") || n.Contains("toe");
    }

    private bool IsPrimaryLegRoot(int node)
    {
        if (node < 0 || node >= _nodes.Length || _nodes[node] == null) return false;
        string n = (_nodes[node].name ?? string.Empty).ToLowerInvariant();
        return n == "l_hip" || n == "r_hip" || n == "l_thigh" || n == "r_thigh";
    }


    private bool IsCoreBodyScaleNode(int node)
    {
        if (node < 0 || node >= _nodes.Length || _nodes[node] == null) return false;
        string n = (_nodes[node].name ?? string.Empty).ToLowerInvariant();
        // God-King Spell4 drives the restored beast with Lion_* scale tracks. Those names include
        // head/neck/spine and were accidentally caught by the humanoid anti-collapse guard, which
        // folded the authentic wolf/lion submesh into the red-black clump seen in the test video.
        if (n.StartsWith("lion_") || n.StartsWith("wolf_")) return false;
        if (n == "root" || n == "c_root" || n == "skeleton_root" || n == "doll_root" || n.Contains("pelvis") || n.Contains("spine") ||
            n.Contains("chest") || n.Contains("neck") || n.Contains("head") || n.Contains("clav") ||
            n.Contains("shoulder") || n.Contains("upperarm") || n.Contains("forearm") || n.Contains("hand") ||
            n.Contains("thigh") || n.Contains("knee") || n.Contains("calf") || n.Contains("ankle") ||
            n.Contains("foot") || n.Contains("toe")) return true;
        // Some Riot skeletons use L_Arm/R_Arm and L_Leg/R_Leg rather than upperarm/thigh names.
        if ((n.StartsWith("l_") || n.StartsWith("r_")) && (n.Contains("arm") || n.Contains("leg"))) return true;
        return false;
    }

    private static bool IsFinite(Vector3 v)
    {
        return !float.IsNaN(v.x) && !float.IsNaN(v.y) && !float.IsNaN(v.z) &&
               !float.IsInfinity(v.x) && !float.IsInfinity(v.y) && !float.IsInfinity(v.z);
    }

    private static bool IsDariusCombatActionClip(string name)
    {
        if (string.IsNullOrEmpty(name)) return false;
        return name.StartsWith("Attack", StringComparison.Ordinal) ||
               name.StartsWith("Crit", StringComparison.Ordinal) ||
               name.StartsWith("Spell", StringComparison.Ordinal) ||
               name.StartsWith("Darius_", StringComparison.Ordinal) ||
               string.Equals(name, "Death", StringComparison.Ordinal) ||
               string.Equals(name, "Channel_Wndup", StringComparison.Ordinal) ||
               string.Equals(name, "Channel", StringComparison.Ordinal);
    }

    private bool IsLocomotionActionNode(int node)
    {
        if (node < 0 || node >= _nodes.Length || _nodes[node] == null) return false;
        string n = _nodes[node].name ?? string.Empty;
        // Keep SoD locomotion on the actual leg chains. Everything else (Root/Pelvis, torso,
        // arms, weapon, cape and skin-specific auxiliary bones) follows the authored action.
        string lower = n.ToLowerInvariant();
        if (lower.Contains("hip") || lower.Contains("knee") || lower.Contains("foot") || lower.Contains("toe")) return false;
        if (lower.Contains("leg")) return false;
        if (lower.Contains("ground_loc")) return false;
        return true;
    }

    private bool IsUpperBodyNode(int node)
    {
        if (node < 0 || node >= _nodes.Length || _nodes[node] == null) return false;
        string n = (_nodes[node].name ?? string.Empty).ToLowerInvariant();
        if (n.Contains("hip") || n.Contains("knee") || n.Contains("foot") || n.Contains("toe") || n.Contains("leg")) return false;
        if (n.Contains("ground_loc")) return false;
        return true;
    }

    private static void FindKeys(float[] times, float time, out int k0, out int k1, out float t)
    {
        if (times.Length <= 1 || time <= times[0]) { k0 = k1 = 0; t = 0f; return; }
        int last = times.Length - 1;
        if (time >= times[last]) { k0 = k1 = last; t = 0f; return; }
        int lo = 0, hi = last;
        while (hi - lo > 1)
        {
            int mid = (lo + hi) >> 1;
            if (times[mid] <= time) lo = mid; else hi = mid;
        }
        k0 = lo; k1 = hi;
        float span = times[k1] - times[k0];
        t = span <= 0.000001f ? 0f : Mathf.Clamp01((time - times[k0]) / span);
    }

    private float[] ReadFloatAccessor(int accessorIndex)
    {
        float[] cached;
        if (_floatAccessorCache.TryGetValue(accessorIndex, out cached)) return cached;
        JObject a = (JObject)_json["accessors"][accessorIndex];
        int componentType = (int)a["componentType"];
        if (componentType != 5126) throw new NotSupportedException("Accessor " + accessorIndex + " is not FLOAT: " + componentType);
        int count = (int)a["count"];
        int comps = ComponentCount((string)a["type"]);
        JObject view = (JObject)_json["bufferViews"][(int)a["bufferView"]];
        int viewOffset = view["byteOffset"] != null ? (int)view["byteOffset"] : 0;
        int accessorOffset = a["byteOffset"] != null ? (int)a["byteOffset"] : 0;
        int stride = view["byteStride"] != null ? (int)view["byteStride"] : comps * 4;
        float[] result = new float[count * comps];
        for (int i = 0; i < count; i++)
        {
            int src = viewOffset + accessorOffset + i * stride;
            for (int c = 0; c < comps; c++) result[i * comps + c] = BitConverter.ToSingle(_bin, src + c * 4);
        }
        _floatAccessorCache[accessorIndex] = result;
        return result;
    }

    private ushort[] ReadUShortAccessor(int accessorIndex)
    {
        JObject a = (JObject)_json["accessors"][accessorIndex];
        int componentType = (int)a["componentType"];
        if (componentType != 5123) throw new NotSupportedException("Accessor " + accessorIndex + " is not UNSIGNED_SHORT: " + componentType);
        int count = (int)a["count"];
        int comps = ComponentCount((string)a["type"]);
        JObject view = (JObject)_json["bufferViews"][(int)a["bufferView"]];
        int viewOffset = view["byteOffset"] != null ? (int)view["byteOffset"] : 0;
        int accessorOffset = a["byteOffset"] != null ? (int)a["byteOffset"] : 0;
        int stride = view["byteStride"] != null ? (int)view["byteStride"] : comps * 2;
        ushort[] result = new ushort[count * comps];
        for (int i = 0; i < count; i++)
        {
            int src = viewOffset + accessorOffset + i * stride;
            for (int c = 0; c < comps; c++) result[i * comps + c] = BitConverter.ToUInt16(_bin, src + c * 2);
        }
        return result;
    }

    private static int ComponentCount(string type)
    {
        switch (type)
        {
            case "SCALAR": return 1;
            case "VEC2": return 2;
            case "VEC3": return 3;
            case "VEC4": return 4;
            case "MAT4": return 16;
            default: throw new NotSupportedException("Unsupported accessor type: " + type);
        }
    }

    private static Vector3 ReadVec3(JToken token, Vector3 fallback)
    {
        JArray a = token as JArray;
        if (a == null || a.Count < 3) return fallback;
        return new Vector3((float)a[0], (float)a[1], (float)a[2]);
    }

    private static Quaternion ReadQuat(JToken token, Quaternion fallback)
    {
        JArray a = token as JArray;
        if (a == null || a.Count < 4) return fallback;
        return new Quaternion((float)a[0], (float)a[1], (float)a[2], (float)a[3]);
    }

    private static uint ReadUInt32(byte[] bytes, int offset)
    {
        return BitConverter.ToUInt32(bytes, offset);
    }

    public void Dispose()
    {
        for (int i = 0; i < _meshes.Count; i++) if (_meshes[i] != null) UnityEngine.Object.Destroy(_meshes[i]);
        for (int i = 0; i < _materials.Count; i++) if (_materials[i] != null) UnityEngine.Object.Destroy(_materials[i]);
        for (int i = 0; i < _textures.Count; i++) if (_textures[i] != null) UnityEngine.Object.Destroy(_textures[i]);
        _meshes.Clear();
        _materials.Clear();
        _materialBySourceName.Clear();
        _materialBaseColor.Clear();
        _materialPrimitiveIndex.Clear();
        _skinRenderer = null;
        _hiddenSkinRenderer = null;
        _overlaySourceMesh = null;
        if (_invisibleOverlayMaterial != null) { try { UnityEngine.Object.Destroy(_invisibleOverlayMaterial); } catch { } _invisibleOverlayMaterial = null; }
        _textures.Clear();
        _textureCache.Clear();
        _clips.Clear();
        _floatAccessorCache.Clear();
        _current = null;
        _upper = null;
        _lowerLocomotion = null;
        _lowerLocomotionActive = false;
        _blendFromPos = null;
        _blendFromRot = null;
        _blendFromScale = null;
        _blendDuration = 0f;
        _blendTime = 0f;
        _json = null;
        _bin = null;
        _nodes = null;
        _basePos = null;
        _baseRot = null;
        _baseScale = null;
        if (root != null)
        {
            try { UnityEngine.Object.Destroy(root); } catch { }
            root = null;
        }
    }
}
