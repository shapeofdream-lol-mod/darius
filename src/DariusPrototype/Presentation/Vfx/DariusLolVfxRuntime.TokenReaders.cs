public static partial class DariusLolVfxRuntime
{
    private static bool ReadBoolToken(JToken t,bool fallback){try{return t!=null&&t.Type!=JTokenType.Null?(bool)t:fallback;}catch{return fallback;}}

    private static float ReadFloatToken(JToken t,float fallback){try{return t!=null&&t.Type!=JTokenType.Null?(float)t:fallback;}catch{return fallback;}}

    private static Vector3 ReadPointShapeOffset(JObject shape)
    {
        if (shape == null || !string.Equals((string)shape["type"], "VfxShapePointDoNotUse", StringComparison.OrdinalIgnoreCase)) return Vector3.zero;
        JObject fields = shape["fields"] as JObject;
        if (fields == null) return Vector3.zero;
        // 0xe5f268dd is the authored point offset in Riot's VfxShapePointDoNotUse.
        JArray a = fields["e5f268dd"] as JArray;
        if (a == null || a.Count < 3) return Vector3.zero;
        return new Vector3((float)a[0], (float)a[1], (float)a[2]);
    }

    private static int ReadIntToken(JToken t,int fallback){try{return t!=null&&t.Type!=JTokenType.Null?(int)t:fallback;}catch{return fallback;}}

    private static float ReadConstantFloat(JObject d,float fallback)
    {
        if(d==null)return fallback;JToken c=d["constant"];if(c==null)return fallback;JArray a=c as JArray;return a!=null&&a.Count>0?(float)a[0]:ReadFloatToken(c,fallback);
    }

    private static Vector3 ReadConstantVector3(JObject d,Vector3 fallback)
    {
        if(d==null)return fallback;JToken c=d["constant"];JArray a=c as JArray;if(a!=null){float x=a.Count>0?(float)a[0]:fallback.x;float y=a.Count>1?(float)a[1]:x;float z=a.Count>2?(float)a[2]:x;return new Vector3(x,y,z);}return fallback;
    }

    private static Vector2 ReadConstantVector2(JObject d, Vector2 fallback)
    {
        if (d == null) return fallback; JArray a = d["constant"] as JArray;
        if (a == null) return fallback;
        float x = a.Count > 0 ? (float)a[0] : fallback.x;
        float y = a.Count > 1 ? (float)a[1] : fallback.y;
        return new Vector2(x, y);
    }

    private static Color ReadConstantColor(JObject d,Color fallback)
    {
        if(d==null)return fallback;JArray a=d["constant"] as JArray;if(a==null||a.Count<3)return fallback;return new Color((float)a[0],(float)a[1],(float)a[2],a.Count>3?(float)a[3]:1f);
    }

    private static Color ReadInitialColor(JObject d, Color fallback)
    {
        if (d == null) return fallback;
        JArray c = d["constant"] as JArray;
        if (c != null && c.Count >= 3) return new Color((float)c[0], (float)c[1], (float)c[2], c.Count > 3 ? (float)c[3] : 1f);
        JArray values = d["values"] as JArray;
        JArray first = values != null && values.Count > 0 ? values[0] as JArray : null;
        if (first != null && first.Count >= 3) return new Color((float)first[0], (float)first[1], (float)first[2], first.Count > 3 ? (float)first[3] : 1f);
        return fallback;
    }

    private static Color MultiplyColor(Color a, Color b)
    {
        return new Color(a.r * b.r, a.g * b.g, a.b * b.b, a.a * b.a);
    }

    private static void ApplyMaterialColor(Material material, Color color)
    {
        if (material == null) return;
        if (material.HasProperty("_Color")) material.SetColor("_Color", color);
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_TintColor")) material.SetColor("_TintColor", color);
    }

    private static ParticleSystem.MinMaxCurve ToMinMaxCurve(JObject d,float fallback)
    {
        if(d==null)return new ParticleSystem.MinMaxCurve(fallback);float c=ReadConstantFloat(d,fallback);JArray pt=d["probabilityTables"] as JArray;
        if(pt!=null&&pt.Count>0){JObject p0=pt[0] as JObject; JArray v=p0!=null?p0["values"] as JArray:null;if(v!=null&&v.Count>=2){float a=Scalar(v[0],c),b=Scalar(v[v.Count-1],c);return new ParticleSystem.MinMaxCurve(Mathf.Min(a,b),Mathf.Max(a,b));}}
        return new ParticleSystem.MinMaxCurve(c);
    }

    private static ParticleSystem.MinMaxCurve ScaleCurve(ParticleSystem.MinMaxCurve curve, float factor)
    {
        if (Mathf.Approximately(factor, 1f)) return curve;
        if (curve.mode == ParticleSystemCurveMode.Constant) curve.constant *= factor;
        else if (curve.mode == ParticleSystemCurveMode.TwoConstants)
        {
            curve.constantMin *= factor; curve.constantMax *= factor;
        }
        else curve.curveMultiplier *= factor;
        return curve;
    }

    private static ParticleSystem.MinMaxCurve ToAxisCurve(JObject d,int axis,float fallback)
    {
        if(d==null)return new ParticleSystem.MinMaxCurve(fallback);Vector3 c=ReadConstantVector3(d,new Vector3(fallback,fallback,fallback));float cv=axis==0?c.x:(axis==1?c.y:c.z);
        JArray pt=d["probabilityTables"] as JArray;if(pt!=null&&pt.Count>axis){JObject pa=pt[axis] as JObject; JArray v=pa!=null?pa["values"] as JArray:null;if(v!=null&&v.Count>=2){float a=Scalar(v[0],1f)*cv,b=Scalar(v[v.Count-1],1f)*cv;return new ParticleSystem.MinMaxCurve(Mathf.Min(a,b),Mathf.Max(a,b));}}
        return new ParticleSystem.MinMaxCurve(cv);
    }

    private static float Scalar(JToken t,float fallback){try{JArray a=t as JArray;return a!=null&&a.Count>0?(float)a[0]:(float)t;}catch{return fallback;}}

    private static AnimationCurve BuildAnimationCurve(JObject d,int axis,float fallback)
    {
        JArray times=d!=null?d["times"] as JArray:null, vals=d!=null?d["values"] as JArray:null;if(times==null||vals==null||times.Count==0)return AnimationCurve.Linear(0f,fallback,1f,fallback);
        int n=Mathf.Min(times.Count,vals.Count);Keyframe[] keys=new Keyframe[n];for(int i=0;i<n;i++){JArray a=vals[i] as JArray;float v=a!=null&&a.Count>axis?(float)a[axis]:Scalar(vals[i],fallback);keys[i]=new Keyframe(Mathf.Clamp01((float)times[i]),v);}return new AnimationCurve(keys);
    }
}