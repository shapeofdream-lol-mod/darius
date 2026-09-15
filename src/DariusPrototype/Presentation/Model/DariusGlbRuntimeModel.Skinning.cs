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
}