public static partial class DariusLolVfxRuntime
{
    private static Texture2D LoadTexture(string rel)
    {
        if(string.IsNullOrEmpty(rel)) return null; Texture2D t; if(Textures.TryGetValue(rel,out t))return t;
        try
        {
            string p=Path.Combine(DariusMedia.Root,"assets","lol_vfx",rel.Replace('/',Path.DirectorySeparatorChar));
            byte[] b=File.ReadAllBytes(p);
            if(rel.EndsWith(".tex",StringComparison.OrdinalIgnoreCase))
            {
                if(b.Length<12 || b[0]!=(byte)'T' || b[1]!=(byte)'E' || b[2]!=(byte)'X' || b[3]!=0)
                    throw new InvalidDataException("Riot TEX header missing");
                int w=b[4]|(b[5]<<8), h=b[6]|(b[7]<<8);
                int format=b[9];
                TextureFormat tf; int bytesPerBlock;
                if(format==10) { tf=TextureFormat.DXT1; bytesPerBlock=8; }
                else if(format==12) { tf=TextureFormat.DXT5; bytesPerBlock=16; }
                else throw new NotSupportedException("Riot TEX format="+format);
                // Riot TEX stores mip levels smallest -> largest; Unity raw texture loading expects
                // mip 0 first. Feeding the whole Riot payload directly therefore scrambled VFX
                // atlases. Render from the authoritative full-resolution mip (the final mip block).
                int blocksX=Mathf.Max(1,(w+3)/4), blocksY=Mathf.Max(1,(h+3)/4);
                int topMipBytes=blocksX*blocksY*bytesPerBlock;
                if(topMipBytes<=0 || b.Length<12+topMipBytes) throw new InvalidDataException("Riot TEX top mip truncated");
                byte[] raw=new byte[topMipBytes]; Buffer.BlockCopy(b,b.Length-topMipBytes,raw,0,topMipBytes);
                t=new Texture2D(w,h,tf,false); t.LoadRawTextureData(raw); t.Apply(false,false);
            }
            else
            {
                t=new Texture2D(2,2,TextureFormat.RGBA32,false);
                if(!ImageConversion.LoadImage(t,b,false)){UnityEngine.Object.Destroy(t);t=null;}
            }
            if(t!=null)
            {
                t.name="LoLVfx_"+Path.GetFileNameWithoutExtension(rel);
                t.wrapMode=TextureWrapMode.Repeat; t.filterMode=FilterMode.Bilinear;
            }
        }
        catch(Exception e){DariusLog.Exception("LOL-VFX-TEX",e,"rel="+rel);t=null;}
        Textures[rel]=t; return t;
    }

    private static Material MaterialFor(string texRel,int blend)
    {
        string key=(texRel??"<none>")+":"+blend; Material m; if(Materials.TryGetValue(key,out m)&&m!=null)return m;
        Shader s=null;
        // Riot VfxEmitterDefinitionData blendMode is global across primitives:
        // 0 One/One additive (also the default when absent), 1 SrcAlpha/OneMinusSrcAlpha,
        // 2 Zero/OneMinusSrcColor multiply, 3 opaque/depth-writing, 4 SrcAlpha/One additive.
        if (blend==0 || blend==4) s=Shader.Find("Legacy Shaders/Particles/Additive");
        else if (blend==1) s=Shader.Find("Legacy Shaders/Particles/Alpha Blended");
        else if (blend==2) s=Shader.Find("Legacy Shaders/Particles/Multiply");
        else if (blend==3) s=Shader.Find("Unlit/Transparent Cutout");
        if(s==null) s=Shader.Find("Particles/Standard Unlit");
        if(s==null) s=Shader.Find("Sprites/Default");
        if(s==null) s=Shader.Find("Universal Render Pipeline/Unlit");
        if(s==null) return null;
        m=new Material(s); m.name="LoLVfxMat_"+blend+"_"+Path.GetFileNameWithoutExtension(texRel??"none");
        Texture2D t=LoadTexture(texRel); if(t!=null){m.mainTexture=t;if(m.HasProperty("_BaseMap"))m.SetTexture("_BaseMap",t);}
        int src=5, dst=10; bool transparent=true;
        if(blend==0) { src=1; dst=1; }
        else if(blend==1) { src=5; dst=10; }
        else if(blend==2) { src=0; dst=6; }
        else if(blend==3) { src=1; dst=0; transparent=false; }
        else if(blend==4) { src=5; dst=1; }
        if(m.HasProperty("_SrcBlend"))m.SetFloat("_SrcBlend",src);
        if(m.HasProperty("_DstBlend"))m.SetFloat("_DstBlend",dst);
        if(m.HasProperty("_ZWrite"))m.SetFloat("_ZWrite",transparent?0f:1f);
        if(m.HasProperty("_Surface"))m.SetFloat("_Surface",transparent?1f:0f);
        if(m.HasProperty("_Cull"))m.SetFloat("_Cull",0f);
        if(transparent) m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); else m.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
        if(m.HasProperty("_TintColor")) m.SetColor("_TintColor", Color.white);
        if(!transparent && m.HasProperty("_Cutoff")) m.SetFloat("_Cutoff", 0.01f);
        m.renderQueue=transparent?3000:2450; Materials[key]=m; return m;
    }

    private static Mesh LoadMesh(string rel)
    {
        if(string.IsNullOrEmpty(rel))return null; Mesh m; if(Meshes.TryGetValue(rel,out m))return m;
        try
        {
            string p=Path.Combine(DariusMedia.Root,"assets","lol_vfx",rel.Replace('/',Path.DirectorySeparatorChar)); JObject j=JObject.Parse(File.ReadAllText(p));
            JArray pos=j["positions"] as JArray, uv=j["uv"] as JArray, ix=j["indices"] as JArray;
            if(pos==null||ix==null)throw new InvalidDataException("mesh arrays missing");
            Vector3[] v=new Vector3[pos.Count]; Vector2[] tc=uv!=null?new Vector2[uv.Count]:null; int[] tri=new int[ix.Count];
            for(int i=0;i<pos.Count;i++){JArray a=pos[i] as JArray;v[i]=new Vector3((float)a[0],(float)a[1],(float)a[2]);}
            if(tc!=null)for(int i=0;i<uv.Count;i++){JArray a=uv[i] as JArray;tc[i]=new Vector2((float)a[0],1f-(float)a[1]);}
            for(int i=0;i<ix.Count;i++)tri[i]=(int)ix[i];
            m=new Mesh();m.name="LoLVfxMesh_"+((string)j["name"]??Path.GetFileNameWithoutExtension(rel));if(v.Length>65535)m.indexFormat=UnityEngine.Rendering.IndexFormat.UInt32;
            m.vertices=v;if(tc!=null&&tc.Length==v.Length)m.uv=tc;m.triangles=tri;m.RecalculateNormals();m.RecalculateBounds();
            DariusLog.DebugInfo("LOL-VFX-MESH","Loaded converted SCB rel="+rel+" vertices="+v.Length+" triangles="+(tri.Length/3));
        }
        catch(Exception e){DariusLog.Exception("LOL-VFX-MESH",e,"rel="+rel);m=null;}
        Meshes[rel]=m;return m;
    }

    private static Mesh QuadMesh()
    {
        if(_quad!=null)return _quad; _quad=new Mesh();_quad.name="LoLVfx_ArbitraryQuad";
        _quad.vertices=new[]{new Vector3(-0.5f,-0.5f,0f),new Vector3(0.5f,-0.5f,0f),new Vector3(0.5f,0.5f,0f),new Vector3(-0.5f,0.5f,0f)};
        _quad.uv=new[]{new Vector2(0,0),new Vector2(1,0),new Vector2(1,1),new Vector2(0,1)};_quad.triangles=new[]{0,2,1,0,3,2};_quad.RecalculateNormals();return _quad;
    }
}