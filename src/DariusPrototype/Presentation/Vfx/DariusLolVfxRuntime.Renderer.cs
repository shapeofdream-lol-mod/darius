using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEngine;

public static partial class DariusLolVfxRuntime
{
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

        // Base Darius Q uses dark-backed luminous ring textures. Riot's particle shader makes the
        // dark texels disappear, while Unity alpha/opaque fallbacks expose the source quad itself.
        // Keep this correction scoped to the reported base-Q quad/projection emitters.
        bool baseQRing = string.Equals(systemName, "Darius_Base_Q_Ring", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(systemName, "Darius_Base_Q_Ring_Windup", StringComparison.OrdinalIgnoreCase);
        bool qQuadLike = string.Equals(primitive, "VfxPrimitiveArbitraryQuad", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(primitive, "VfxPrimitiveCameraQuad", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(primitive, "VfxPrimitivePlanarProjection", StringComparison.OrdinalIgnoreCase);
        if (baseQRing && qQuadLike && (blend == 1 || blend == 3))
        {
            Material additive = MaterialFor(textureRel, 0);
            if (additive != null) material = additive;
        }

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
}
