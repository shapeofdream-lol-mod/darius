using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEngine;

public static partial class DariusLolVfxRuntime
{
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
}