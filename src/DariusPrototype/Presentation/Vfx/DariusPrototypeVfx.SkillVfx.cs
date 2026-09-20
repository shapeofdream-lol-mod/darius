using System;
using UnityEngine;

public static partial class DariusPrototypeVfx
{
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
}