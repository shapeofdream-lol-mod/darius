using System;
using UnityEngine;

public static class DariusPrototypeVfx
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
        MeshFilter mf = go.AddComponent<MeshFilter>();
        mf.sharedMesh = MakeQuad(vertical);
        MeshRenderer mr = go.AddComponent<MeshRenderer>();
        mr.material = NewMaterial(color, texture);
        DariusVfxQuadMotion motion = go.AddComponent<DariusVfxQuadMotion>();
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
        MeshFilter mf = go.AddComponent<MeshFilter>();
        mf.sharedMesh = MakeQuad(vertical);
        MeshRenderer mr = go.AddComponent<MeshRenderer>();
        mr.material = NewMaterial(color, texture);
        DariusPersistentVfxPulse pulse = go.AddComponent<DariusPersistentVfxPulse>();
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

    public static void CreateBasicAttackRangeArc(Hero owner, Vector3 forward, float range)
    {
        if (owner == null || range <= 0.05f) return;
        forward.y = 0f;
        if (forward.sqrMagnitude <= 0.0001f) forward = owner.transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude <= 0.0001f) forward = Vector3.forward;
        forward.Normalize();

        // Shape of Dreams' melee readability uses a curved ground sector. The sector angle is purely
        // presentation; every outer point is exactly `range` metres from the Hero, so its radial edge
        // can never claim more or less reach than At_DariusAxe's live trigger configuration.
        const float halfAngle = 55f;
        const int arcSegments = 18;
        GameObject go = new GameObject(SkinFxName(owner, "BasicAttack_RangeArc"));
        LineRenderer line = go.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.loop = false;
        line.positionCount = arcSegments + 3;
        line.startWidth = 0.055f;
        line.endWidth = 0.055f;
        line.numCornerVertices = 3;
        line.numCapVertices = 2;
        Color c = SkinColor(owner, new Color(0.94f, 0.42f, 0.34f, 0.72f), new Color(1.00f, 0.63f, 0.16f, 0.82f));
        line.startColor = c;
        line.endColor = new Color(c.r, c.g, c.b, 0.42f);
        line.material = NewMaterial(c);

        Vector3 center = owner.transform.position + Vector3.up * 0.07f;
        line.SetPosition(0, center);
        for (int i = 0; i <= arcSegments; i++)
        {
            float t = i / (float)arcSegments;
            float angle = Mathf.Lerp(-halfAngle, halfAngle, t);
            Vector3 dir = Quaternion.Euler(0f, angle, 0f) * forward;
            line.SetPosition(i + 1, center + dir * range);
        }
        line.SetPosition(arcSegments + 2, center);
        UnityEngine.Object.Destroy(go, 0.30f);
        DariusLog.DebugInfoThrottled("ATK-RANGE-VFX", "arc",
            "Basic attack ground sector rendered with exact radial range=" + range.ToString("0.###") +
            "m halfAngle=" + halfAngle.ToString("0.#"), 1.0);
    }

    public static void CreateBasicAttackSwing(Hero owner, bool alternate)
    {
        if (owner == null) return;
        // Pass 5 removes the old hand-authored axe trail when an exact Riot skin system exists.
        // Classic/God-King have no separate champion BA system in the supplied current source, so
        // their model animation + exact Wwise attack SFX are left unmodified rather than fabricated.
        if (Dunkmaster(owner))
            DariusLolVfxRuntime.PlayAttached(owner, "Darius_Skin04_BA", owner.transform);
        else if (Mecha(owner))
            DariusLolVfxRuntime.PlayAttached(owner, alternate ? "Darius_Skin67_BA02" : "Darius_Skin67_BA01", owner.transform);
        DariusMedia.PlayForSkin("basic_attack", owner, DariusAudioChannel.BasicAttack, alternate ? 0.42f : 0.36f);
    }

    public static void CreateBasicAttackImpact(Hero owner, Vector3 p)
    {
        // No generic hand-made impact flash. The original LoL champion banks used by this package
        // do not expose a universal Darius basic-hit target system, so absence is preferred to a
        // fabricated substitute. Hit SFX still routes through the skin-specific audio bank.
    }

    public static void CreateBasicAttackImpact(Vector3 p) { CreateBasicAttackImpact(null, p); }

    public static void CreateQWindupTrail(Hero owner)
    {
        // Weapon-attached motion is authored inside the converted Riot system; do not layer a hand-made trail.
        if (owner == null) return;
        DariusLog.DebugInfo("LOL-VFX-BRIDGE", "Q windup uses converted Riot particle system; manual weapon trail suppressed.");
    }

    public static void CreateWAttackSwing(Hero owner)
    {
        if (owner == null) return;
        string system = LolSystem(owner, "darius_Base_W_weapon_02", "Darius_Skin15_W_Cast", "Darius_Skin04_W_Weapon_Child", "Darius_Skin67_W_Cast");
        Transform anchor = DariusSkinAnimationHooks.GetWeaponAnchor(owner);
        if (anchor == null) anchor = owner.transform;
        DariusLolVfxRuntime.PlayAttached(owner, system, anchor);
    }

    public static void CreateEWindup(Hero owner)
    {
        if (owner == null) return;
        Transform anchor = DariusSkinAnimationHooks.GetWeaponAnchor(owner);
        if (anchor == null) anchor = owner.transform;
        DariusLolVfxRuntime.PlayAttached(owner, LolSystem(owner, "darius_Base_E_weapon_trigger", "Darius_Skin15_E_Cast", null, "Darius_Skin67_E_Cast"), anchor);
        DariusMedia.PlayForSkin("e_cast", owner, 0.82f);
        DariusMedia.PlaySkillVoice(owner, "e", DariusAudioChannel.E, 0.78f);
    }

    public static void CreateRWindup(Hero owner)
    {
        if (owner == null) return;
        Transform weapon = DariusSkinAnimationHooks.GetWeaponAnchor(owner);
        if (weapon == null) weapon = owner.transform;
        DariusLolVfxRuntime.PlayAttached(owner, LolSystem(owner, "darius_Base_R_cast_axe", "Darius_Skin15_R_cast_axe", null, "Darius_Skin67_R_CastAxe"), weapon);
        if (GodKing(owner))
        {
            // Both are direct conversions from Skin15. The visible lunging beast itself is the restored
            // Wolf_Mat SKN submesh animated by the original Spell4 lion-bone animation.
            DariusLolVfxRuntime.PlayAttached(owner, "Darius_Skin15_R_Cast_Wolf", owner.transform);
            DariusLolVfxRuntime.PlayAttached(owner, "Darius_Skin15_R_Trail", weapon);
        }
        else if (Mecha(owner))
        {
            DariusLolVfxRuntime.PlayAttached(owner, "Darius_Skin67_R_CastWolf", owner.transform);
            DariusLolVfxRuntime.PlayAttached(owner, "Darius_Skin67_R_Trail", weapon);
        }
        DariusMedia.PlayForSkin("r_cast", owner, 0.96f);
        DariusMedia.PlaySkillVoice(owner, "r", DariusAudioChannel.R, 0.82f);
    }

    private static LineRenderer FallbackRing(string name, Vector3 center, float radius, Color color, float width, float lifetime)
    {
        GameObject go = new GameObject(name);
        go.transform.position = center + Vector3.up * 0.08f;
        LineRenderer line = go.AddComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.loop = true;
        line.positionCount = 64;
        line.startWidth = width;
        line.endWidth = width;
        line.startColor = color;
        line.endColor = color;
        line.material = NewMaterial(color);
        for (int i = 0; i < 64; i++)
        {
            float a = i * Mathf.PI * 2f / 64f;
            line.SetPosition(i, new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius));
        }
        UnityEngine.Object.Destroy(go, lifetime);
        return line;
    }

    // Q needs a readable pre-impact range telegraph. The previous 0.08-0.13 alpha texture was
    // effectively invisible in Shape of Dreams' blue combat rooms. Keep the actual impact sweep
    // separate, but make both the outer blade edge and inner dead-zone boundary unambiguous.
    public static void CreateQTelegraph(Transform owner, float inner, float outer, float duration)
    {
        if (owner == null) return;
        Hero heroOwner = owner.GetComponentInParent<Hero>();
        if (heroOwner == null) return;
        // The LoL system is authored in champion-space units (Q outer radius ~425), and the runtime
        // converter applies the same 0.00921 LoL->Shape-of-Dreams scale as the champion model.
        DariusLolVfxRuntime.PlayAttached(heroOwner, LolSystem(heroOwner, "Darius_Base_Q_Ring_Windup", "Darius_Skin15_Q_Ring_Windup", "Darius_Skin04_Q_Ring_Windup", "Darius_Skin67_Q_RingWindup"), owner);
        // Riot's actual Q_Ring emitters already contain ~0.75 s authored delays. Starting this
        // system only at the gameplay impact added the same delay a second time, which is why the
        // God-King spinning flash appeared roughly one second after Darius had finished Q. Start
        // the delayed ring now, alongside the windup, so its authored delay lands on the spin.
        DariusLolVfxRuntime.PlayAttached(heroOwner, LolSystem(heroOwner, "Darius_Base_Q_Ring", "Darius_Skin15_Q_Ring", "Darius_Skin04_Q_Ring", "Darius_Skin67_Q_Ring"), owner);
        DariusLog.DebugInfo("Q-TELEGRAPH", "Converted Riot Q windup + authored-delayed ring started together inner=" + inner.ToString("0.###") +
            " outer=" + outer.ToString("0.###") + " duration=" + duration.ToString("0.###"));
        DariusMedia.PlayForSkin("q_windup", heroOwner, owner.position, 0.84f);
        DariusMedia.PlaySkillVoice(heroOwner, "q", DariusAudioChannel.Q, 0.78f);
    }

    // One sweep only. Layer a pale steel axe arc over a short blood-red shock ring.
    public static void CreateQSwing(Hero owner, Vector3 center, float inner, float outer)
    {
        if (owner == null) return;
        // Q_Ring was started at cast-start because its Riot emitters contain the windup delay.
        // Do not spawn it again here. Skin15's zero-delay activation flash belongs exactly here.
        if (GodKing(owner)) DariusLolVfxRuntime.PlayAttached(owner, "Darius_Skin15_Q_Activate", owner.transform);
        else if (Mecha(owner)) DariusLolVfxRuntime.PlayAttached(owner, "Darius_Skin67_Q_Activate", owner.transform);
    }

    public static void CreateQInstantSwing(Hero owner)
    {
        if (owner == null) return;
        // Fast-forward only the authored emitter delay; do not globally speed up particle lifetimes.
        // Layers authored at 0.75 s start now, while 0.80/0.85 s accents retain their 0.05/0.10 s
        // spacing inside the spin. This preserves the Riot sequence when the constellation removes windup.
        DariusLolVfxRuntime.PlayAttachedAtAuthoredTime(owner,
            LolSystem(owner, "Darius_Base_Q_Ring", "Darius_Skin15_Q_Ring", "Darius_Skin04_Q_Ring", "Darius_Skin67_Q_Ring"), owner.transform, Ai_Darius_Decimate.Windup);
        if (GodKing(owner)) DariusLolVfxRuntime.PlayAttached(owner, "Darius_Skin15_Q_Activate", owner.transform);
        else if (Mecha(owner)) DariusLolVfxRuntime.PlayAttached(owner, "Darius_Skin67_Q_Activate", owner.transform);
        DariusMedia.PlaySkillVoice(owner, "q", DariusAudioChannel.Q, 0.78f);
    }

    public static void CreateHitFlash(Vector3 position, bool outer)
    {
        // Legacy compatibility path cannot know the active skin; use Classic authored target VFX.
        DariusLolVfxRuntime.PlayWorld(outer ? "darius_Base_Q_tar" : "darius_Base_Q_tar_inner", position);
    }

    public static void CreateHitFlash(Hero owner, Vector3 position, bool outer)
    {
        string classic = outer ? "darius_Base_Q_tar" : "darius_Base_Q_tar_inner";
        string godKing = outer ? "Darius_Skin15_Q_tar" : "Darius_Skin15_Q_tar_inner";
        string mecha = outer ? "Darius_Skin67_Q_tar" : "Darius_Skin67_Q_TarInner";
        DariusLolVfxRuntime.PlayWorld(LolSystem(owner, classic, godKing, null, mecha), position);
    }

    // Compatibility stub. q_hit.wav was an early recreated placeholder and is intentionally silent.
    public static void PlayQHit(Vector3 p) { }

    public static void CreateQHeal(Transform owner)
    {
        if (owner == null) return;
        Hero heroOwner = owner.GetComponentInParent<Hero>();
        if (heroOwner == null) return;
        DariusLolVfxRuntime.PlayAttached(heroOwner, LolSystem(heroOwner, "Darius_Base_Q_Heal", "Darius_Skin15_Q_Heal", null, "Darius_Skin67_Q_Heal"), owner);
    }

    // W should visibly stay armed until the empowered basic attack is spent/expired.
    public static GameObject CreateWArm(Hero owner)
    {
        if (owner == null) return null;
        Transform anchor = DariusSkinAnimationHooks.GetWeaponAnchor(owner);
        if (anchor == null) anchor = owner.transform;
        GameObject fx = DariusLolVfxRuntime.PlayAttachedPersistent(owner, LolSystem(owner, "darius_Base_W_weapon_01", "Darius_Skin15_W_weapon_01", "Darius_Skin04_W_Weapon_Child", "Darius_Skin67_W_Weapon01"), anchor);
        if (GodKing(owner))
        {
            // weapon_02 carries the real Skin15 Darius_Axe_Mat / body AttachedMesh overlays.
            // It is kept as a separate Riot system but tied to weapon_01's lifetime.
            GameObject overlayFx = DariusLolVfxRuntime.PlayAttachedPersistent(owner, "Darius_Skin15_W_weapon_02", anchor);
            if (fx != null && overlayFx != null)
            {
                DariusLolVfxLinkedObjects links = fx.GetComponent<DariusLolVfxLinkedObjects>();
                if (links != null) links.Add(overlayFx);
            }
            else if (fx == null) fx = overlayFx;
        }
        DariusMedia.PlayForSkin("w_arm", owner, 0.88f);
        DariusMedia.PlaySkillVoice(owner, "w", DariusAudioChannel.W, 0.78f);
        return fx;
    }

    public static void CreateWImpact(Hero owner, Vector3 p)
    {
        DariusLolVfxRuntime.PlayWorld(LolSystem(owner, "darius_Base_W_tar", "Darius_Skin15_W_tar", null, "Darius_Skin67_W_tar"), p);
        DariusMedia.PlayForSkin("w_hit", owner, p, 0.98f);
    }
    public static void CreateWImpact(Vector3 p) { CreateWImpact(null, p); }

    // E's green cone in the reference recording is only targeting UI. The cast itself is a quick
    // forward hook/scrape, so use a short crescent and then target-to-owner streaks.
    public static void CreateECone(Hero owner, Vector3 origin, Vector3 dir, float range, float halfAngle)
    {
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.001f) dir = owner != null ? owner.transform.forward : Vector3.forward;
        dir.Normalize();
        if (GodKing(owner))
        {
            DariusLolVfxRuntime.PlayWorld("Darius_Skin15_E_ClawMarks", origin, LolFacing(dir));
            DariusLolVfxRuntime.PlayWorld("Darius_Skin15_E_Axegrab_Collision", origin, LolFacing(dir));
        }
        else if (Dunkmaster(owner))
            DariusLolVfxRuntime.PlayWorld("Darius_Skin04_E_Axegrab_Collision", origin, LolFacing(dir));
        else if (Mecha(owner))
        {
            DariusLolVfxRuntime.PlayWorld("Darius_Skin67_E_ClawMarks", origin, LolFacing(dir));
            DariusLolVfxRuntime.PlayWorld("Darius_Skin67_E_AxegrabCollision", origin, LolFacing(dir));
        }
        else DariusLolVfxRuntime.PlayWorld("Darius_Base_E_Axegrab_Collision", origin, LolFacing(dir));
    }

    public static void CreateECone(Vector3 origin, Vector3 dir, float range, float halfAngle) { CreateECone(null, origin, dir, range, halfAngle); }

    public static void CreateEPull(Hero owner, Vector3 from, Vector3 to)
    {
        Vector3 dir = to - from;
        Quaternion facing = LolFacing(dir);
        DariusLolVfxRuntime.PlayWorld(LolSystem(owner, "darius_Base_E_tar_unit_trail", "Darius_Skin15_E_tar_unit_trail", "Darius_Skin04_E_Tar_Unit_Trail", "Darius_Skin67_E_TarUnitTrail"), from, facing);
        DariusLolVfxRuntime.PlayWorld(LolSystem(owner, "darius_Base_E_tar", "Darius_Skin15_E_tar", null, "Darius_Skin67_E_tar"), from);
        if (GodKing(owner)) DariusLolVfxRuntime.PlayWorld("Darius_Skin15_E_tar_02", from);
        else if (Mecha(owner))
        {
            // Skin67 E_Tar02 contains a target CameraTrail. Bind it to the same short movement
            // interval as the actual pull instead of spawning it on a static world root (which
            // forced the runtime to drop Trail_BLend as unsupported).
            DariusLolVfxRuntime.PlayWorldBetween(owner, "Darius_Skin67_E_Tar02", from, to, 0.22f, facing);
        }
    }
    public static void CreateEPull(Vector3 from, Vector3 to) { CreateEPull(null, from, to); }

    public static void PlayEPull(Hero owner, Vector3 p) { DariusMedia.PlayForSkin("e_pull", owner, p,0.94f); }
    public static void PlayEPull(Vector3 p) { DariusMedia.Play("e_pull", p,0.94f); }

    public static void CreateRLeap(Hero owner, Vector3 from, Vector3 to)
    {
        // R_Trail is started at cast time and follows the original weapon attachment.
        // No procedural LineRenderer replacement is added here.
    }
    public static void CreateRLeap(Vector3 from, Vector3 to) { CreateRLeap(null, from, to); }

    public static void CreateRImpact(Hero owner, Entity target, Vector3 p)
    {
        DariusLolVfxRuntime.PlayWorld(LolSystem(owner, "darius_Base_R_tar", "Darius_Skin15_R_tar", null, "Darius_Skin67_R_tar"), p);
        if (GodKing(owner))
        {
            // Skin15 ships an additional impact system. Its own disabled flags are now respected,
            // so only Riot-authored active flash/up-glow/BlastColumn layers are instantiated.
            DariusLolVfxRuntime.PlayWorld("Darius_Skin15_R_tar_02", p);
        }
        else if (Dunkmaster(owner))
            DariusLolVfxRuntime.PlayWorld("Darius_Skin04_R_Tar_Backboard", p);
        else if (Mecha(owner))
        {
            // Temp_Avatar in Skin67_R_TarStartVFX is an AttachedMesh on the victim. Preserve the
            // target transform so the generic target-overlay path can reproduce it instead of
            // logging "no owning traveler" and dropping the layer.
            if (target != null) DariusLolVfxRuntime.PlayAttached(null, "Darius_Skin67_R_TarStartVFX", target.transform);
            else DariusLolVfxRuntime.PlayWorld("Darius_Skin67_R_TarStartVFX", p);
            DariusLolVfxRuntime.PlayWorld("Darius_Skin67_R_Tar02", p);
        }
        DariusMedia.PlayForSkin("r_hit", owner, p, 1.0f);
    }
    public static void CreateRImpact(Hero owner, Vector3 p) { CreateRImpact(owner, null, p); }
    public static void CreateRImpact(Vector3 p) { CreateRImpact(null, null, p); }

    public static void CreateRReset(Hero owner, Vector3 p)
    {
        DariusLolVfxRuntime.PlayWorld(LolSystem(owner, "darius_Base_r_refresh_01", "Darius_Skin15_darius_Base_r_refresh_01", null, "Darius_Skin67_DariusBase_R_DariusBaseRefresh01"), p);
        DariusMedia.PlayForSkin("r_reset", owner, p, 0.74f);
    }
    public static void CreateRReset(Vector3 p) { CreateRReset(null, p); }

    public static void CreateEssenceToggle(Vector3 p, bool on)
    {
        if (on) TexturedQuad("Darius_Essence", "bleed_drop", p+Vector3.up*0.12f,new Vector3(0.72f,1f,0.72f),0.34f,Color.white,false,null,35f);
        else FallbackRing("Darius_EssenceOff",p,1.05f,new Color(0.3f,0.3f,0.3f,0.62f),0.08f,0.35f);
    }

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
        line.material = NewMaterial(pale);
        line.SetPosition(0, from + Vector3.up * 0.45f);
        line.SetPosition(1, to + Vector3.up * 0.45f);
        UnityEngine.Object.Destroy(streak, 0.12f);

        DariusMedia.Play("flash", to, 0.94f);
    }

    public static GameObject CreateSummonerGhost(Transform owner, float duration)
    {
        if (owner == null) return null;
        GameObject aura = PersistentTexturedQuad("Darius_Ghost_Aura", "noxian_aura", owner, Vector3.up * 0.06f,
            new Vector3(1.85f,1f,1.85f),new Color(0.34f,0.86f,1.00f,0.62f),false,92f,0.08f,8.4f);
        TexturedQuad("Darius_Ghost_Burst", "q_heal_wisps", owner.position + Vector3.up * 0.18f,
            new Vector3(1.65f,1f,1.65f),Mathf.Min(0.48f, Mathf.Max(0.18f, duration * 0.12f)),
            new Color(0.52f,0.94f,1.00f,0.72f),false,owner,90f);
        DariusMedia.Play("ghost", owner.position, 0.90f);
        return aura;
    }
}
