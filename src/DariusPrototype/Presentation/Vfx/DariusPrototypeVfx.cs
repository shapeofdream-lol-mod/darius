using System;
using UnityEngine;

public static partial class DariusPrototypeVfx
{
    private static bool GodKing(Hero owner)
    {
        try { return owner != null && DariusSkinAnimationHooks.IsGodKing(owner); } catch { return false; }
    }

    private static Color SkinColor(Hero owner, Color classicColor, Color godKingColor)
    {
        return GodKing(owner) ? godKingColor : classicColor;
    }

    private static string SkinTexture(Hero owner, string classicKey, string godKingKey)
    {
        return GodKing(owner) ? godKingKey : classicKey;
    }

    private static string SkinFxName(Hero owner, string suffix)
    {
        return (GodKing(owner) ? "Darius_GodKing_" : "Darius_Classic_") + suffix;
    }

    private static string VariantKey(Hero owner)
    {
        try { return DariusSkinAnimationHooks.GetVariantKey(owner) ?? "Classic"; }
        catch { return "Classic"; }
    }

    private static bool Dunkmaster(Hero owner)
    {
        return string.Equals(VariantKey(owner), "Dunkmaster", StringComparison.OrdinalIgnoreCase);
    }

    private static bool Mecha(Hero owner)
    {
        return string.Equals(VariantKey(owner), "Mecha", StringComparison.OrdinalIgnoreCase);
    }

    private static string LolSystem(Hero owner, string classicName, string godKingName, string dunkmasterName = null, string mechaName = null)
    {
        string variant = VariantKey(owner);
        if (string.Equals(variant, "GodKing", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(godKingName)) return godKingName;
        if (string.Equals(variant, "Dunkmaster", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(dunkmasterName)) return dunkmasterName;
        if (string.Equals(variant, "Mecha", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(mechaName)) return mechaName;
        return classicName;
    }

    private static Quaternion LolFacing(Vector3 direction)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f) return Quaternion.identity;
        return Quaternion.LookRotation(direction.normalized, Vector3.up);
    }

    private static Material NewMaterial(Color color, Texture texture = null)
    {
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Transparent");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        if (shader == null)
            throw new InvalidOperationException("No compatible unlit shader found for Darius VFX.");
        Material m = new Material(shader);
        m.name = "DariusVfxMaterial";
        m.color = color;
        if (texture != null) m.mainTexture = texture;
        m.renderQueue = 3000;
        return m;
    }

    private static Mesh MakeQuad(bool vertical)
    {
        Mesh mesh = new Mesh();
        mesh.name = vertical ? "DariusVfxVerticalQuad" : "DariusVfxGroundQuad";
        if (vertical)
        {
            mesh.vertices = new Vector3[]
            {
                new Vector3(-0.5f,-0.5f,0f), new Vector3(0.5f,-0.5f,0f),
                new Vector3(0.5f,0.5f,0f), new Vector3(-0.5f,0.5f,0f)
            };
        }
        else
        {
            mesh.vertices = new Vector3[]
            {
                new Vector3(-0.5f,0f,-0.5f), new Vector3(0.5f,0f,-0.5f),
                new Vector3(0.5f,0f,0.5f), new Vector3(-0.5f,0f,0.5f)
            };
        }
        mesh.uv = new Vector2[] { new Vector2(0,0), new Vector2(1,0), new Vector2(1,1), new Vector2(0,1) };
        mesh.triangles = new int[] { 0,2,1, 0,3,2 };
        mesh.RecalculateBounds();
        mesh.RecalculateNormals();
        return mesh;
    }

    private static GameObject TexturedQuad(string name, string textureKey, Vector3 position, Vector3 scale,
        float lifetime, Color color, bool vertical = false, Transform follow = null, float rotateSpeed = 0f, Quaternion? rotation = null)
    {
        Texture2D texture = DariusMedia.Texture(textureKey);
        if (texture == null) return null;
        GameObject go = new GameObject(name);
        go.transform.position = position;
        go.transform.rotation = rotation ?? Quaternion.identity;
        Mesh mesh = MakeQuad(vertical);
        Material material = NewMaterial(color, texture);
        MeshFilter mf = go.AddComponent<MeshFilter>();
        mf.sharedMesh = mesh;
        MeshRenderer mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = material;
        DariusOwnedRenderResources owned = go.AddComponent<DariusOwnedRenderResources>();
        owned.mesh = mesh;
        owned.material = material;
        DariusVfxQuadMotion motion = go.AddComponent<DariusVfxQuadMotion>();
        motion.BindMaterial(material);
        motion.lifetime = lifetime;
        motion.startScale = scale;
        motion.endScale = scale * (vertical ? 1.12f : 1.06f);
        motion.rotateDegreesPerSecond = rotateSpeed;
        motion.follow = follow;
        if (follow != null) motion.followOffset = position - follow.position;
        motion.billboard = vertical;
        go.transform.localScale = scale;
        return go;
    }

    private static GameObject PersistentTexturedQuad(string name, string textureKey, Transform follow, Vector3 offset,
        Vector3 scale, Color color, bool vertical, float rotateSpeed, float pulseAmount, float pulseSpeed)
    {
        Texture2D texture = DariusMedia.Texture(textureKey);
        if (texture == null || follow == null) return null;
        GameObject go = new GameObject(name);
        go.transform.position = follow.position + offset;
        Mesh mesh = MakeQuad(vertical);
        Material material = NewMaterial(color, texture);
        MeshFilter mf = go.AddComponent<MeshFilter>();
        mf.sharedMesh = mesh;
        MeshRenderer mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = material;
        DariusOwnedRenderResources owned = go.AddComponent<DariusOwnedRenderResources>();
        owned.mesh = mesh;
        owned.material = material;
        DariusPersistentVfxPulse pulse = go.AddComponent<DariusPersistentVfxPulse>();
        pulse.BindMaterial(material);
        pulse.follow = follow;
        pulse.followOffset = offset;
        pulse.baseScale = scale;
        pulse.rotateDegreesPerSecond = rotateSpeed;
        pulse.pulseAmount = pulseAmount;
        pulse.pulseSpeed = pulseSpeed;
        pulse.billboard = vertical;
        go.transform.localScale = scale;
        return go;
    }
}