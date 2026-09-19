using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;
using Mirror;
using UnityEngine;

public sealed partial class DariusGlbRuntimeModel
{
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
}