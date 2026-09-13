using System;
using UnityEngine;

// Public animation bridge used by Hero_Darius skills and its native basic attack.
// The clean traveler model is authoritative; retarget calls are defensive loading fallbacks only.
public static class DariusSkinAnimationHooks
{
    // Skills own the animation request. Hero_Darius first targets the clean traveler GLB;
    // the retarget API remains only as a defensive fallback if the model has not loaded yet.
    public static void PlayQ(Hero hero, bool instant = false)
    {
        DariusTravelerModelInstance traveler = FindTraveler(hero);
        if (traveler != null) { traveler.PlayQ(instant); return; }
        // Instant Decimate is a real animation-mechanic change: skip Spell1_IN entirely and start
        // the spinning Spell1 attack on the cast frame. Normal Q keeps the authored windup clip.
        DariusModelAnimationRuntime.DariusRetargetApi.Play(hero, instant ? "Spell1" : "Darius_Spell1_IN.anm", false, 1f);
    }

    public static void PlayW(Hero hero)
    {
        PlayW(hero, hero != null ? hero.transform.forward : Vector3.forward);
    }

    public static void PlayW(Hero hero, Vector3 direction)
    {
        DariusTravelerModelInstance traveler = FindTraveler(hero);
        if (traveler != null) { traveler.PlayWAttack(direction); return; }
        DariusModelAnimationRuntime.DariusRetargetApi.Play(hero, "Spell2", false, 1f);
    }

    public static void SetWArmed(Hero hero, bool armed)
    {
        DariusTravelerModelInstance traveler = FindTraveler(hero);
        if (traveler != null) { traveler.SetWArmed(armed); return; }
        if (armed) DariusModelAnimationRuntime.DariusRetargetApi.Play(hero, "Spell2_Idle", true, 1f);
        else DariusModelAnimationRuntime.DariusRetargetApi.Stop(hero);
    }

    public static void PlayE(Hero hero)
    {
        DariusTravelerModelInstance traveler = FindTraveler(hero);
        if (traveler != null) { traveler.PlayOneShot("Spell3"); return; }
        DariusModelAnimationRuntime.DariusRetargetApi.Play(hero, "Spell3", false, 1f);
    }

    public static void PlayR(Hero hero)
    {
        DariusTravelerModelInstance traveler = FindTraveler(hero);
        if (traveler != null) { traveler.PlayOneShot("Spell4"); return; }
        DariusModelAnimationRuntime.DariusRetargetApi.Play(hero, "Spell4", false, 1f);
    }

    public static void PlayAttack(Hero hero, bool alternate, bool critical = false)
    {
        PlayAttack(hero, alternate, critical, hero != null ? hero.transform.forward : Vector3.forward);
    }

    public static void PlayAttack(Hero hero, bool alternate, bool critical, Vector3 direction)
    {
        string clip = critical ? "Crit" : (alternate ? "Attack2" : "Attack1");
        DariusTravelerModelInstance traveler = FindTraveler(hero);
        if (traveler != null) { traveler.PlayAttack(alternate, critical, direction); return; }
        DariusModelAnimationRuntime.DariusRetargetApi.Play(hero, clip, false, 1f);
    }

    // VFX is attached directly to the animated Darius skeleton.
    // The supplied GLB exposes Riot-style buff bones, so effects can follow the axe throughout
    // Spell1/2/3/4 and the basic-attack tracks instead of looking detached from the animation.
    public static bool IsGodKing(Hero hero)
    {
        DariusTravelerModelInstance traveler = FindTraveler(hero);
        return traveler != null && traveler.IsGodKingSkin;
    }

    public static string GetVariantKey(Hero hero)
    {
        DariusTravelerModelInstance traveler = FindTraveler(hero);
        return traveler != null && !string.IsNullOrEmpty(traveler.VariantKey) ? traveler.VariantKey : "Classic";
    }

    public static Transform GetWeaponAnchor(Hero hero)
    {
        return GetAnchor(hero, "BuffBone_Glb_Weapon_1", "Weapon", "R_BuffBone_Glb_Hand_Loc", "R_Hand");
    }

    public static Transform GetChestAnchor(Hero hero)
    {
        return GetAnchor(hero, "C_BuffBone_Glb_Chest_Loc", "Chest", "Spine");
    }

    public static Transform GetAnchor(Hero hero, params string[] names)
    {
        if (hero == null || names == null) return null;
        DariusTravelerModelInstance traveler = FindTraveler(hero);
        if (traveler != null)
        {
            for (int i = 0; i < names.Length; i++)
            {
                Transform t = traveler.GetAnchor(names[i]);
                if (t != null) return t;
            }
        }
        return hero.transform;
    }

    public static GameObject CreateSubmeshOverlay(Hero hero, uint sourceMaterialHash, Material overlayMaterial)
    {
        DariusTravelerModelInstance traveler = FindTraveler(hero);
        return traveler != null ? traveler.CreateSubmeshOverlay(sourceMaterialHash, overlayMaterial) : null;
    }

    // Riot VfxPrimitiveAttachedMesh does not always serialize explicit submesh hashes. In that
    // form the primitive targets the traveler's visible avatar mesh as a whole. Treating an empty
    // hash list as unsupported dropped Mecha AvatarFlash/ScreenspaceLines/pauldron layers entirely.
    // Use the already split visible runtime mesh so authored-hidden recall/wolf/throne geometry is
    // never exposed by a full-avatar overlay.
    public static GameObject CreateFullMeshOverlay(Hero hero, Material overlayMaterial, string label)
    {
        DariusTravelerModelInstance traveler = FindTraveler(hero);
        return traveler != null ? traveler.CreateFullMeshOverlay(overlayMaterial, label) : null;
    }

    private static DariusTravelerModelInstance FindTraveler(Hero hero)
    {
        if (hero == null) return null;
        return hero.GetComponentInChildren<DariusTravelerModelInstance>(true);
    }
}
