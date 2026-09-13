using System;
using UnityEngine;

// Public animation bridge used by Hero_Darius skills and its native basic attack.
// Unity/Shape-of-Dreams native model assets are authoritative when present; the runtime GLB path
// remains only as a compatibility fallback until the local binary asset pack is rebuilt.
public static class DariusSkinAnimationHooks
{
    public static void PlayQ(Hero hero, bool instant = false)
    {
        DariusNativeModelBridge native = FindNative(hero);
        if (native != null && native.IsReady) { native.PlayQ(instant); return; }
        DariusTravelerModelInstance traveler = FindTraveler(hero);
        if (traveler != null) { traveler.PlayQ(instant); return; }
        DariusModelAnimationRuntime.DariusRetargetApi.Play(hero, instant ? "Spell1" : "Darius_Spell1_IN.anm", false, 1f);
    }

    public static void PlayW(Hero hero)
    {
        PlayW(hero, hero != null ? hero.transform.forward : Vector3.forward);
    }

    public static void PlayW(Hero hero, Vector3 direction)
    {
        DariusNativeModelBridge native = FindNative(hero);
        if (native != null && native.IsReady) { native.PlayWAttack(direction); return; }
        DariusTravelerModelInstance traveler = FindTraveler(hero);
        if (traveler != null) { traveler.PlayWAttack(direction); return; }
        DariusModelAnimationRuntime.DariusRetargetApi.Play(hero, "Spell2", false, 1f);
    }

    public static void SetWArmed(Hero hero, bool armed)
    {
        DariusNativeModelBridge native = FindNative(hero);
        if (native != null && native.IsReady) { native.SetWArmed(armed); return; }
        DariusTravelerModelInstance traveler = FindTraveler(hero);
        if (traveler != null) { traveler.SetWArmed(armed); return; }
        if (armed) DariusModelAnimationRuntime.DariusRetargetApi.Play(hero, "Spell2_Idle", true, 1f);
        else DariusModelAnimationRuntime.DariusRetargetApi.Stop(hero);
    }

    public static void PlayE(Hero hero)
    {
        DariusNativeModelBridge native = FindNative(hero);
        if (native != null && native.IsReady) { native.PlayOneShot("Spell3"); return; }
        DariusTravelerModelInstance traveler = FindTraveler(hero);
        if (traveler != null) { traveler.PlayOneShot("Spell3"); return; }
        DariusModelAnimationRuntime.DariusRetargetApi.Play(hero, "Spell3", false, 1f);
    }

    public static void PlayR(Hero hero)
    {
        DariusNativeModelBridge native = FindNative(hero);
        if (native != null && native.IsReady) { native.PlayOneShot("Spell4"); return; }
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
        DariusNativeModelBridge native = FindNative(hero);
        if (native != null && native.IsReady) { native.PlayAttack(alternate, critical, direction); return; }
        DariusTravelerModelInstance traveler = FindTraveler(hero);
        if (traveler != null) { traveler.PlayAttack(alternate, critical, direction); return; }
        DariusModelAnimationRuntime.DariusRetargetApi.Play(hero, clip, false, 1f);
    }

    public static bool IsGodKing(Hero hero)
    {
        DariusNativeModelBridge native = FindNative(hero);
        if (native != null && native.IsReady) return native.IsGodKingSkin;
        DariusTravelerModelInstance traveler = FindTraveler(hero);
        return traveler != null && traveler.IsGodKingSkin;
    }

    public static string GetVariantKey(Hero hero)
    {
        DariusNativeModelBridge native = FindNative(hero);
        if (native != null && native.IsReady) return native.VariantKey;
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
        DariusNativeModelBridge native = FindNative(hero);
        if (native != null && native.IsReady)
        {
            for (int i = 0; i < names.Length; i++)
            {
                Transform t = native.GetAnchor(names[i]);
                if (t != null) return t;
            }
        }
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
        DariusNativeModelBridge native = FindNative(hero);
        if (native != null && native.IsReady) return native.CreateSubmeshOverlay(sourceMaterialHash, overlayMaterial);
        DariusTravelerModelInstance traveler = FindTraveler(hero);
        return traveler != null ? traveler.CreateSubmeshOverlay(sourceMaterialHash, overlayMaterial) : null;
    }

    public static GameObject CreateFullMeshOverlay(Hero hero, Material overlayMaterial, string label)
    {
        DariusNativeModelBridge native = FindNative(hero);
        if (native != null && native.IsReady) return native.CreateFullMeshOverlay(overlayMaterial, label);
        DariusTravelerModelInstance traveler = FindTraveler(hero);
        return traveler != null ? traveler.CreateFullMeshOverlay(overlayMaterial, label) : null;
    }

    private static DariusNativeModelBridge FindNative(Hero hero)
    {
        if (hero == null) return null;
        return hero.GetComponentInChildren<DariusNativeModelBridge>(true);
    }

    private static DariusTravelerModelInstance FindTraveler(Hero hero)
    {
        if (hero == null) return null;
        return hero.GetComponentInChildren<DariusTravelerModelInstance>(true);
    }
}