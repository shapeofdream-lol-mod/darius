using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEngine;

public static partial class DariusLolVfxRuntime
{
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
            return BuildAttachedMeshEmitter(owner, root, e, name, persistent, authoredTimeOffset, systemName);
        }

        // Riot Trails/Ribbons are path geometry. Converting them to ParticleSystem ribbons made
        // W/E/R collapse into bright blobs and polygon fans. For attached weapon/body effects,
        // follow the real animated attachment with a world-space TrailRenderer. World-static trail
        // systems need endpoint/target binding and are skipped until that binding is authored.
        if (primitive.IndexOf("Trail", StringComparison.OrdinalIgnoreCase) >= 0 ||
            string.Equals(primitive, "VfxPrimitiveRibbon", StringComparison.OrdinalIgnoreCase))
        {
            return BuildTrailEmitter(root, e, primitive, name, persistent, authoredTimeOffset, systemName);
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
        ApplyStartSizeAxes(ps, birthScale, size, uniformScale, qReadability,
            skin67NoxianAirflow, skin67WindupDisc, skin67WindupTeamRing, Skin67NoxianAirflowSpatialScale,
            primitive, name);

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

        ApplyRotationAndSpin(ps, go, e, primitive, name, qReadability, delay, systemLifetime, particleLifetime, linger);
        ConfigureTextureSheet(ps, e["texDiv"] as JArray);

        ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
        ConfigureRenderer(ps, renderer, e, primitive, meshRel, textureRel,
            ReadIntToken(e["blendMode"], 0), ReadIntToken(e["pass"], 0), systemName);
        ps.Play(true);
        return Mathf.Max(systemLifetime + particleLifetime + linger + delay, particleLifetime + delay);
    }
}
