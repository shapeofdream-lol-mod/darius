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