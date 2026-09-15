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
}