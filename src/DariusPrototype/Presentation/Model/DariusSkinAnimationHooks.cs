using UnityEngine;

// Animation entry point. Native Unity models use Shape of Dreams EntityAnimation as the
// animation authority. Legacy GLB animation remains a fallback until native assets are ready.
public static class DariusSkinAnimationHooks
{
    public static void PlayQ(Hero hero, bool instant = false)
    {
        if (DariusNativeAnimationRouter.PlayAbility(hero, "Spell1")) return;
        DariusTravelerModelInstance traveler = FindTraveler(hero);
        if (traveler != null) { traveler.PlayQ(instant); return; }
        DariusModelAnimationRuntime.DariusRetargetApi.Play(hero, "Spell1", false, 1f);
    }

    public static void PlayW(Hero hero)
    {
        PlayW(hero, hero != null ? hero.transform.forward : Vector3.forward);
    }

    public static void PlayW(Hero hero, Vector3 direction)
    {
        if (DariusNativeAnimationRouter.PlayAbility(hero, "Spell2")) return;
        DariusTravelerModelInstance traveler = FindTraveler(hero);
        if (traveler != null) { traveler.PlayWAttack(direction); return; }
        DariusModelAnimationRuntime.DariusRetargetApi.Play(hero, "Spell2", false, 1f);
    }

    public static void SetWArmed(Hero hero, bool armed)
    {
        if (!armed && DariusNativeAnimationRouter.StopAbility(hero)) return;
        if (armed && DariusNativeAnimationRouter.PlayAbility(hero, "Spell2_Idle", true)) return;
        DariusTravelerModelInstance traveler = FindTraveler(hero);
        if (traveler != null) { traveler.SetWArmed(armed); return; }
        if (armed) DariusModelAnimationRuntime.DariusRetargetApi.Play(hero, "Spell2_Idle", true, 1f);
        else DariusModelAnimationRuntime.DariusRetargetApi.Stop(hero);
    }

    public static void PlayE(Hero hero)
    {
        if (DariusNativeAnimationRouter.PlayAbility(hero, "Spell3")) return;
        DariusTravelerModelInstance traveler = FindTraveler(hero);
        if (traveler != null) { traveler.PlayOneShot("Spell3"); return; }
        DariusModelAnimationRuntime.DariusRetargetApi.Play(hero, "Spell3", false, 1f);
    }

    public static void PlayR(Hero hero)
    {
        if (DariusNativeAnimationRouter.PlayAbility(hero, "Spell4")) return;
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
        if (DariusNativeAnimationRouter.PlayAbility(hero, clip)) return;
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
        return traveler != null ? traveler.VariantKey : "Classic";
    }

    public static Transform GetWeaponAnchor(Hero hero) => GetAnchor(hero, "BuffBone_Glb_Weapon_1", "Weapon", "R_Hand");
    public static Transform GetChestAnchor(Hero hero) => GetAnchor(hero, "C_BuffBone_Glb_Chest_Loc", "Chest", "Spine");

    public static Transform GetAnchor(Hero hero, params string[] names)
    {
        DariusNativeModelBridge native = FindNative(hero);
        if (native != null && native.IsReady)
            for (int i = 0; i < names.Length; i++)
            {
                Transform t = native.GetAnchor(names[i]);
                if (t != null) return t;
            }
        DariusTravelerModelInstance traveler = FindTraveler(hero);
        if (traveler != null)
            for (int i = 0; i < names.Length; i++)
            {
                Transform t = traveler.GetAnchor(names[i]);
                if (t != null) return t;
            }
        return hero != null ? hero.transform : null;
    }

    public static GameObject CreateSubmeshOverlay(Hero hero, uint hash, Material material)
    {
        DariusNativeModelBridge native = FindNative(hero);
        if (native != null && native.IsReady) return native.CreateSubmeshOverlay(hash, material);
        DariusTravelerModelInstance traveler = FindTraveler(hero);
        return traveler != null ? traveler.CreateSubmeshOverlay(hash, material) : null;
    }

    public static GameObject CreateFullMeshOverlay(Hero hero, Material material, string label)
    {
        DariusNativeModelBridge native = FindNative(hero);
        if (native != null && native.IsReady) return native.CreateFullMeshOverlay(material, label);
        DariusTravelerModelInstance traveler = FindTraveler(hero);
        return traveler != null ? traveler.CreateFullMeshOverlay(material, label) : null;
    }

    private static DariusNativeModelBridge FindNative(Hero hero)
    {
        return hero == null ? null : hero.GetComponentInChildren<DariusNativeModelBridge>(true);
    }

    private static DariusTravelerModelInstance FindTraveler(Hero hero)
    {
        return hero == null ? null : hero.GetComponentInChildren<DariusTravelerModelInstance>(true);
    }
}
