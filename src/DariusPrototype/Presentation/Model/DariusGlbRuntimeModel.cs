using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;
using Mirror;
using UnityEngine;

// The pre-v0.18 DariusSkinInstance/Vesper rig-diagnostic implementation was removed.
// Skin_Darius_Default is now the sole Darius visual path.

// Purpose-built glTF 2.0/GLB reader for the supplied Darius model.
// It supports the subset present in the file: one skinned triangle mesh, PNG base-color texture,
// 4 influences per vertex, and LINEAR TRS animation tracks.
public sealed partial class DariusGlbRuntimeModel
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
}