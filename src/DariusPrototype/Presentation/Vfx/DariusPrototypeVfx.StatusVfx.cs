using System;
using UnityEngine;

public static partial class DariusPrototypeVfx
{
    public static void CreateBleedStack(Hero owner, Transform t, int stacks)
    {
        if (t == null || stacks <= 0) return;
        if (Mecha(owner))
        {
            DariusLolVfxRuntime.PlayWorld("Darius_Skin67_HemoBleedIndicatorHit", t.position + Vector3.up * 0.7f);
            return;
        }
        float s = 0.30f + Mathf.Clamp(stacks, 1, 10) * 0.035f;
        TexturedQuad(SkinFxName(owner, "BleedApply_" + stacks),"bleed_drop",t.position+Vector3.up*1.35f,
            new Vector3(s,s,1f),0.20f,SkinColor(owner,new Color(1f,0.45f,0.38f,0.90f),new Color(1f,0.68f,0.16f,0.94f)),true,t,0f);
        // Hemorrhage stack application is intentionally silent; bleed_apply.wav was an early
        // placeholder and produced the shared pop-like hit sound reported in v0.18.2b.
    }

    public static void CreateBleedStack(Transform t, int stacks) { CreateBleedStack(null, t, stacks); }

    public static GameObject CreateBleedStackMarker(Hero owner, Transform t, int stacks)
    {
        if (t == null || stacks <= 0) return null;
        if (Mecha(owner))
        {
            int n = Mathf.Clamp(stacks, 1, 5);
            return DariusLolVfxRuntime.PlayAttachedPersistent(owner, "Darius_Skin67_DariusBaseHemoCounter0" + n, t);
        }
        int count = Mathf.Clamp(stacks, 1, 12);
        GameObject root = new GameObject(SkinFxName(owner, "BleedStacks_" + stacks));
        root.transform.SetParent(t, false);
        root.transform.localPosition = Vector3.up * 1.56f;
        root.transform.localRotation = Quaternion.identity;
        root.transform.localScale = Vector3.one;
        const int perRow = 6;
        float spacing = 0.18f;
        Color pip = SkinColor(owner,new Color(1f,0.35f,0.30f,0.96f),new Color(1f,0.66f,0.14f,0.98f));
        for (int i = 0; i < count; i++)
        {
            int row = i / perRow;
            int indexInRow = i % perRow;
            int rowCount = Mathf.Min(perRow, count - row * perRow);
            float x = (indexInRow - (rowCount - 1) * 0.5f) * spacing;
            float y = row * 0.17f;
            PersistentTexturedQuad(SkinFxName(owner, "BleedStackDot_" + (i + 1)), "bleed_drop", root.transform,
                new Vector3(x, y, 0f), new Vector3(0.26f,0.26f,1f), pip, true, 0f, 0.025f, GodKing(owner) ? 5.5f : 4.0f);
        }
        return root;
    }

    public static GameObject CreateBleedStackMarker(Transform t, int stacks) { return CreateBleedStackMarker(null, t, stacks); }

    public static GameObject CreateBleedFiveMark(Hero owner, Transform t)
    {
        if (t == null) return null;
        if (Mecha(owner))
            return DariusLolVfxRuntime.PlayAttachedPersistent(owner, "Darius_Skin67_DariusBasePassiveOverheadMaxStack", t);
        return PersistentTexturedQuad(SkinFxName(owner, "BleedFiveMark"),"bleed_five_mark",t,Vector3.up*1.66f,
            new Vector3(GodKing(owner) ? 1.02f : 0.92f,GodKing(owner) ? 1.02f : 0.92f,1f),
            SkinColor(owner,new Color(1f,0.30f,0.25f,0.94f),new Color(1f,0.72f,0.18f,0.98f)),true,0f,0.055f,GodKing(owner) ? 7.0f : 5.8f);
    }

    public static GameObject CreateBleedFiveMark(Transform t) { return CreateBleedFiveMark(null, t); }

    public static void CreateBleedTick(Hero owner, Vector3 p, int stacks)
    {
        if (Mecha(owner))
        {
            DariusLolVfxRuntime.PlayWorld("Darius_Skin67_HemoBleedIndicatorTalon", p + Vector3.up * 0.45f);
            return;
        }
        float visualStacks = Mathf.Min(stacks, 10);
        TexturedQuad(SkinFxName(owner, "BleedTick"),"hit_flash",p+Vector3.up*0.09f,
            new Vector3(0.36f+visualStacks*0.055f,1f,0.36f+visualStacks*0.055f),0.13f,
            SkinColor(owner,new Color(0.52f,0.05f,0.06f,0.56f),new Color(0.96f,0.34f,0.02f,0.64f)),false,null,GodKing(owner) ? 90f : 65f);
    }

    public static void CreateBleedTick(Vector3 p, int stacks) { CreateBleedTick(null, p, stacks); }

    public static GameObject CreateNoxianMight(Transform owner)
    {
        if (owner == null) return null;
        Hero heroOwner = null;
        try { heroOwner = owner.GetComponentInParent<Hero>(); } catch { }
        if (Dunkmaster(heroOwner))
            return DariusLolVfxRuntime.PlayAttachedPersistent(heroOwner, "Darius_Skin04_P_enraged", owner);
        if (Mecha(heroOwner))
        {
            GameObject rootFx = DariusLolVfxRuntime.PlayAttachedPersistent(heroOwner, "Darius_Skin67_P_enraged", owner);
            GameObject left = DariusLolVfxRuntime.PlayAttachedPersistent(heroOwner, "Darius_Skin67_P_EnragedShoulderL", owner);
            GameObject right = DariusLolVfxRuntime.PlayAttachedPersistent(heroOwner, "Darius_Skin67_P_EnragedShoulderR", owner);
            if (rootFx != null)
            {
                DariusLolVfxLinkedObjects links = rootFx.GetComponent<DariusLolVfxLinkedObjects>();
                if (links != null) { if (left != null) links.Add(left); if (right != null) links.Add(right); }
            }
            return rootFx ?? left ?? right;
        }
        bool godKing = GodKing(heroOwner);
        GameObject root = new GameObject(SkinFxName(heroOwner, "NoxianMight_State"));
        root.transform.SetParent(owner, false);
        root.transform.localPosition = Vector3.zero;

        Color ground = SkinColor(heroOwner, new Color(1f,0.18f,0.10f,0.76f), new Color(1f,0.58f,0.08f,0.84f));
        Color body = SkinColor(heroOwner, new Color(1f,0.08f,0.05f,0.46f), new Color(0.95f,0.24f,0.02f,0.52f));
        Color bloom = SkinColor(heroOwner, new Color(1f,0.16f,0.08f,0.30f), new Color(1f,0.72f,0.16f,0.38f));
        Color mark = SkinColor(heroOwner, new Color(1f,0.20f,0.14f,0.98f), new Color(1f,0.82f,0.24f,1.00f));

        // Presentation only: God-King has its own orange/gold profile and object namespace.
        PersistentTexturedQuad(SkinFxName(heroOwner, "NoxianMight_GroundAura"),SkinTexture(heroOwner, "noxian_aura", "gk_glow"),root.transform,Vector3.up*0.07f,
            new Vector3(godKing ? 2.80f : 2.55f,1f,godKing ? 2.80f : 2.55f),ground,false,godKing ? 62f : 46f,0.080f,godKing ? 7.2f : 6.2f);
        PersistentTexturedQuad(SkinFxName(heroOwner, "NoxianMight_BodyAura"),SkinTexture(heroOwner, "noxian_aura", "gk_wisps_red"),root.transform,Vector3.up*1.02f,
            new Vector3(godKing ? 2.12f : 1.95f,godKing ? 2.48f : 2.35f,1f),body,true,0f,0.105f,godKing ? 8.8f : 7.5f);
        PersistentTexturedQuad(SkinFxName(heroOwner, "NoxianMight_BodyBloom"),"hit_flash",root.transform,Vector3.up*1.02f,
            new Vector3(godKing ? 1.58f : 1.40f,godKing ? 2.10f : 1.95f,1f),bloom,true,0f,0.090f,godKing ? 9.6f : 8.3f);
        PersistentTexturedQuad(SkinFxName(heroOwner, "NoxianMight_Mark"),"bleed_five_mark",root.transform,Vector3.up*1.82f,
            new Vector3(godKing ? 0.98f : 0.88f,godKing ? 0.98f : 0.88f,1f),mark,true,0f,0.050f,godKing ? 6.6f : 5.2f);

        GameObject lightObject = new GameObject(SkinFxName(heroOwner, "NoxianMight_BodyLight"));
        lightObject.transform.SetParent(root.transform, false);
        lightObject.transform.localPosition = Vector3.up * 1.02f;
        Light bodyLight = lightObject.AddComponent<Light>();
        bodyLight.type = LightType.Point;
        bodyLight.color = SkinColor(heroOwner, new Color(1f,0.035f,0.018f,1f), new Color(1f,0.42f,0.035f,1f));
        bodyLight.range = godKing ? 3.8f : 3.4f;
        bodyLight.intensity = godKing ? 1.95f : 1.65f;
        bodyLight.shadows = LightShadows.None;

        DariusNoxianMightPresentation presentation = root.AddComponent<DariusNoxianMightPresentation>();
        presentation.owner = heroOwner;
        presentation.bodyLight = bodyLight;
        presentation.baseLightIntensity = bodyLight.intensity;
        presentation.edgeColor = SkinColor(heroOwner, new Color(0.92f,0.015f,0.01f,1f), new Color(1.00f,0.38f,0.025f,1f));

        DariusMedia.PlayForSkin("noxian_might", heroOwner, owner.position, 0.96f);
        DariusLog.Info("NOXIAN-VFX", "Created skin-specific body glow presentation skin=" +
            (godKing ? "GodKing" : "Classic") + " owner=" +
            (presentation.owner != null ? DariusLog.EntityLabel(presentation.owner) : owner.name));
        return root;
    }

    // Flash/Ghost keep League's visual language while their actual mobility economy comes from
    // Shape of Dreams' native movement skill. These effects intentionally use the mod's existing
    // texture primitives so they work without an AssetBundle and survive both lobby/run scenes.
    public static void CreateSummonerFlash(Vector3 from, Vector3 to)
    {
        Color gold = new Color(1.00f, 0.90f, 0.36f, 0.96f);
        Color pale = new Color(1.00f, 0.98f, 0.72f, 0.88f);

        TexturedQuad("Darius_Flash_Origin", "hit_flash", from + Vector3.up * 0.14f,
            new Vector3(1.32f,1f,1.32f),0.20f,gold,false,null,220f);
        TexturedQuad("Darius_Flash_Destination", "hit_flash", to + Vector3.up * 0.14f,
            new Vector3(1.62f,1f,1.62f),0.25f,pale,false,null,-260f);
        TexturedQuad("Darius_Flash_Ring", "q_outer_ring", to + Vector3.up * 0.07f,
            new Vector3(1.85f,1f,1.85f),0.24f,new Color(1f,0.82f,0.22f,0.58f),false,null,420f);

        GameObject streak = new GameObject("Darius_Flash_Streak");
        LineRenderer line = streak.AddComponent<LineRenderer>();
        line.positionCount = 2;
        line.useWorldSpace = true;
        line.startWidth = 0.30f;
        line.endWidth = 0.06f;
        line.startColor = pale;
        line.endColor = new Color(1f,0.78f,0.20f,0.06f);
        Material streakMaterial = NewMaterial(pale);
        line.sharedMaterial = streakMaterial;
        DariusOwnedRenderResources owned = streak.AddComponent<DariusOwnedRenderResources>();
        owned.material = streakMaterial;
        line.SetPosition(0, from + Vector3.up * 0.45f);
        line.SetPosition(1, to + Vector3.up * 0.45f);
        UnityEngine.Object.Destroy(streak, 0.12f);

        DariusMedia.Play("flash", to, 0.94f);
    }
}