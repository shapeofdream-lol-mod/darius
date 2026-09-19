using UnityEngine;

// Animation entry point. Native Unity models use the native presentation bridge while the
// legacy GLB animation path remains a fallback until native assets are ready.
public static class DariusSkinAnimationHooks
{
    public static void PlayQ(Hero hero, bool instant = false)
    {
        if (DariusNativeAnimationAdapter.PlayAbility(hero, "Spell1", instant)) return;
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
        if (DariusNativeAnimationAdapter.PlayAbility(hero, "Spell2", direction)) return;
        DariusTravelerModelInstance traveler = FindTraveler(hero);
        if (traveler != null) { traveler.PlayWAttack(direction); return; }
        DariusModelAnimationRuntime.DariusRetargetApi.Play(hero, "Spell2", false, 1f);
    }

    public static void SetWArmed(Hero hero, bool armed)
    {
        if (!armed && DariusNativeAnimationAdapter.StopAbility(hero)) return;
        if (armed && DariusNativeAnimationAdapter.PlayAbility(hero, "Spell2_Idle", true)) return;
        DariusTravelerModelInstance traveler = FindTraveler(hero);
        if (traveler != null) { traveler.SetWArmed(armed); return; }
        if (armed) DariusModelAnimationRuntime.DariusRetargetApi.Play(hero, "Spell2_Idle", true, 1f);
        else DariusModelAnimationRuntime.DariusRetargetApi.Stop(hero);
    }

    public static void PlayE(Hero hero)
    {
        if (DariusNativeAnimationAdapter.PlayAbility(hero, "Spell3")) return;
        DariusTravelerModelInstance traveler = FindTraveler(hero);
        if (traveler != null) { traveler.PlayOneShot("Spell3"); return; }
        DariusModelAnimationRuntime.DariusRetargetApi.Play(hero, "Spell3", false, 1f);
    }

    public static void PlayR(Hero hero)
    {
        if (DariusNativeAnimationAdapter.PlayAbility(hero, "Spell4")) return;
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
        if (DariusNativeAnimationAdapter.PlayAbility(hero, clip, direction)) return;
        DariusTravelerModelInstance traveler = FindTraveler(hero);
        if (traveler != null) { traveler.PlayAttack(alternate, critical, direction); return; }
        DariusModelAnimationRuntime.DariusRetargetApi.Play(hero, clip, false, 1f);
    }

    public static bool IsGodKing(Hero hero)
    {
        string freshVariant = GetOfficialFreshVariant(hero);
        if (!string.IsNullOrEmpty(freshVariant))
            return string.Equals(freshVariant, "GodKing", System.StringComparison.OrdinalIgnoreCase);

        DariusNativeModelBridge native = FindNative(hero);
        if (native != null && native.IsReady) return native.IsGodKingSkin;
        DariusTravelerModelInstance traveler = FindTraveler(hero);
        return traveler != null && traveler.IsGodKingSkin;
    }

    public static string GetVariantKey(Hero hero)
    {
        string freshVariant = GetOfficialFreshVariant(hero);
        if (!string.IsNullOrEmpty(freshVariant)) return freshVariant;

        DariusNativeModelBridge native = FindNative(hero);
        if (native != null && native.IsReady) return native.VariantKey;
        DariusTravelerModelInstance traveler = FindTraveler(hero);
        return traveler != null ? traveler.VariantKey : "Classic";
    }

    public static Transform GetWeaponAnchor(Hero hero) => GetAnchor(hero, DariusNativeAssetContract.WeaponAnchorNames);
    public static Transform GetChestAnchor(Hero hero) => GetAnchor(hero, DariusNativeAssetContract.HealthAnchorNames);

    public static Transform GetAnchor(Hero hero, params string[] names)
    {
        Transform fresh = DariusOfficialPresentationSurface.FindAnchor(hero, names);
        if (fresh != null) return fresh;

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
        GameObject fresh = DariusOfficialPresentationSurface.CreateSubmeshOverlay(hero, hash, material);
        if (fresh != null) return fresh;

        DariusNativeModelBridge native = FindNative(hero);
        if (native != null && native.IsReady) return native.CreateSubmeshOverlay(hash, material);
        DariusTravelerModelInstance traveler = FindTraveler(hero);
        return traveler != null ? traveler.CreateSubmeshOverlay(hash, material) : null;
    }

    public static GameObject CreateFullMeshOverlay(Hero hero, Material material, string label)
    {
        GameObject fresh = DariusOfficialPresentationSurface.CreateFullMeshOverlay(hero, material, label);
        if (fresh != null) return fresh;

        DariusNativeModelBridge native = FindNative(hero);
        if (native != null && native.IsReady) return native.CreateFullMeshOverlay(material, label);
        DariusTravelerModelInstance traveler = FindTraveler(hero);
        return traveler != null ? traveler.CreateFullMeshOverlay(material, label) : null;
    }

    private static string GetOfficialFreshVariant(Hero hero)
    {
        Hero_Darius darius = hero as Hero_Darius;
        if (darius == null || darius.Visual == null || darius.Visual.model == null) return null;
        DariusOfficialEntityModelMarker marker =
            darius.Visual.model.GetComponent<DariusOfficialEntityModelMarker>();
        return marker != null ? marker.variantKey : null;
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
