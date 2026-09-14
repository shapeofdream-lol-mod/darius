public static partial class DariusLolVfxRuntime
{
    private static void ConfigureVelocity(ParticleSystem ps, JObject e)
    {
        JObject vel = e["birthVelocity"] as JObject;
        if (vel != null)
        {
            Vector3 c = ReadConstantVector3(vel, Vector3.zero);
            ParticleSystem.VelocityOverLifetimeModule m = ps.velocityOverLifetime;
            m.enabled = true; m.space = ParticleSystemSimulationSpace.Local;
            ParticleSystem.MinMaxCurve x, y, z;
            BuildUniformAxisCurves(vel, c, out x, out y, out z);
            m.x = x; m.y = y; m.z = z;
        }
        JObject acc = e["birthAcceleration"] as JObject;
        if (acc != null)
        {
            Vector3 c = ReadConstantVector3(acc, Vector3.zero);
            ParticleSystem.ForceOverLifetimeModule f = ps.forceOverLifetime;
            f.enabled = true; f.space = ParticleSystemSimulationSpace.Local;
            ParticleSystem.MinMaxCurve x, y, z;
            BuildUniformAxisCurves(acc, c, out x, out y, out z);
            f.x = x; f.y = y; f.z = z;
        }
    }

    // Unity requires X/Y/Z velocity curves to use the same MinMaxCurve mode. Riot allows
    // per-axis probability tables, so promote all three axes to TwoConstants whenever any
    // axis is randomized. This preserves the authored ranges without generating per-frame
    // "Particle Velocity curves must all be in the same mode" errors.
    private static void BuildUniformAxisCurves(JObject d, Vector3 fallback,
        out ParticleSystem.MinMaxCurve x, out ParticleSystem.MinMaxCurve y, out ParticleSystem.MinMaxCurve z)
    {
        float xmin, xmax, ymin, ymax, zmin, zmax;
        bool xv = AxisRange(d, 0, fallback.x, out xmin, out xmax);
        bool yv = AxisRange(d, 1, fallback.y, out ymin, out ymax);
        bool zv = AxisRange(d, 2, fallback.z, out zmin, out zmax);
        if (xv || yv || zv)
        {
            x = new ParticleSystem.MinMaxCurve(xmin, xmax);
            y = new ParticleSystem.MinMaxCurve(ymin, ymax);
            z = new ParticleSystem.MinMaxCurve(zmin, zmax);
        }
        else
        {
            x = new ParticleSystem.MinMaxCurve(xmin);
            y = new ParticleSystem.MinMaxCurve(ymin);
            z = new ParticleSystem.MinMaxCurve(zmin);
        }
    }

    private static bool AxisRange(JObject d, int axis, float fallback, out float min, out float max)
    {
        Vector3 c = ReadConstantVector3(d, new Vector3(fallback, fallback, fallback));
        float cv = axis == 0 ? c.x : (axis == 1 ? c.y : c.z);
        min = max = cv;
        JArray pt = d != null ? d["probabilityTables"] as JArray : null;
        if (pt == null || pt.Count <= axis) return false;
        JObject pa = pt[axis] as JObject;
        JArray v = pa != null ? pa["values"] as JArray : null;
        if (v == null || v.Count < 2) return false;
        float a = Scalar(v[0], 1f) * cv;
        float b = Scalar(v[v.Count - 1], 1f) * cv;
        min = Mathf.Min(a, b); max = Mathf.Max(a, b);
        return Mathf.Abs(max - min) > 0.000001f;
    }

    private static void ConfigureSizeOverLifetime(ParticleSystem ps, JObject data, bool uniformScale)
    {
        if (data == null || data["times"] == null || data["values"] == null) return;
        ParticleSystem.SizeOverLifetimeModule s = ps.sizeOverLifetime;
        s.enabled = true; s.separateAxes = true;
        AnimationCurve x = BuildAnimationCurve(data, 0, 1f);
        s.x = new ParticleSystem.MinMaxCurve(1f, x);
        if (uniformScale)
        {
            s.y = new ParticleSystem.MinMaxCurve(1f, x);
            s.z = new ParticleSystem.MinMaxCurve(1f, x);
        }
        else
        {
            s.y = new ParticleSystem.MinMaxCurve(1f, BuildAnimationCurve(data,1,1f));
            s.z = new ParticleSystem.MinMaxCurve(1f, BuildAnimationCurve(data,2,1f));
        }
    }

    private static bool ShouldSkipSkin67Emitter(string systemName, string emitterName)
    {
        if (string.IsNullOrEmpty(systemName) || string.IsNullOrEmpty(emitterName)) return false;

        // These two are the green WindowPattern grid visible immediately when Q is pressed.
        if (string.Equals(systemName, "Darius_Skin67_Q_RingWindup", StringComparison.OrdinalIgnoreCase) &&
            (string.Equals(emitterName, "ant_dark1", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(emitterName, "ant_dark2", StringComparison.OrdinalIgnoreCase)))
            return true;

        // Ground_Lighting carries no texture, mesh or color at all.  It was one of the original
        // white card flashes during Noxian Might, so keep an explicit name guard in addition to
        // the generic empty-emitter test below.
        if (string.Equals(systemName, "Darius_Skin67_P_enraged", StringComparison.OrdinalIgnoreCase) &&
            (string.Equals(emitterName, "Ground_Lighting", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(emitterName, "Distort", StringComparison.OrdinalIgnoreCase)))
            return true;

        // The max-stack marker contains a special character/hologram mesh using the champion atlas
        // and a LoL-only shader contract.  On the generic shader it becomes an opaque character
        // card/rectangle.  The remaining max-stack emitters still provide a clear five-stack cue.
        if (string.Equals(systemName, "Darius_Skin67_DariusBasePassiveOverheadMaxStack", StringComparison.OrdinalIgnoreCase) &&
            (string.Equals(emitterName, "Temp_start", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(emitterName, "Temp_Mesh", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(emitterName, "Temp_Mesh1", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(emitterName, "FireCards", StringComparison.OrdinalIgnoreCase)))
            return true;

        return false;
    }

    private static bool IsQReadabilitySystem(Transform root)
    {
        return root != null && root.name.IndexOf("_Q_Ring", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool HasColorCurve(JObject data)
    {
        if (data == null) return false;
        JArray times = data["times"] as JArray;
        JArray values = data["values"] as JArray;
        return times != null && values != null && times.Count > 0 && values.Count > 0;
    }

    private static Color BoostQReadabilityColor(Color c)
    {
        float h, s, v;
        Color.RGBToHSV(c, out h, out s, out v);
        // Do not tint neutral whites/greys. Only strengthen already-authored chroma.
        if (s > 0.02f) s = Mathf.Clamp01(s * 1.15f + 0.06f);
        v = Mathf.Clamp01(v * 1.08f);
        Color result = Color.HSVToRGB(h, s, v);
        // Visibility compensation may strengthen RGB/chroma, but it must never resurrect pixels
        // that the authored texture/curve made transparent. Preserve alpha exactly.
        result.a = c.a;
        return result;
    }

    private static void ConfigureColorOverLifetime(ParticleSystem ps, JObject data, bool qReadability = false)
    {
        if (data == null || data["times"] == null || data["values"] == null) return;
        JArray times = data["times"] as JArray; JArray values = data["values"] as JArray;
        if (times == null || values == null || times.Count == 0) return;
        Gradient g = BuildGradient(data, Color.white, qReadability);
        ParticleSystem.ColorOverLifetimeModule m=ps.colorOverLifetime; m.enabled=true; m.color=new ParticleSystem.MinMaxGradient(g);
    }
}