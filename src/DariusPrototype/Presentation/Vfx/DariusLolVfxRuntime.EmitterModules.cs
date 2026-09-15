using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEngine;

public static partial class DariusLolVfxRuntime
{
    private static void ApplyStartSizeAxes(ParticleSystem ps, JObject birthScale, Vector3 size, bool uniformScale, bool qReadability,
        bool skin67NoxianAirflow, bool skin67WindupDisc, bool skin67WindupTeamRing, float skin67NoxianAirflowSpatialScale,
        string primitive, string name)
    {
        ParticleSystem.MainModule main = ps.main;
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
            if (skin67NoxianAirflow) u = ScaleCurve(u, skin67NoxianAirflowSpatialScale);
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
                sx = ScaleCurve(sx, skin67NoxianAirflowSpatialScale);
                sy = ScaleCurve(sy, skin67NoxianAirflowSpatialScale);
                sz = ScaleCurve(sz, skin67NoxianAirflowSpatialScale);
            }
            main.startSizeX = sx; main.startSizeY = sy; main.startSizeZ = sz;
        }
    }

    private static void ApplyRotationAndSpin(ParticleSystem ps, GameObject go, JObject e, string primitive, string name, bool qReadability, float delay, float systemLifetime, float particleLifetime, float linger)
    {
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
}
