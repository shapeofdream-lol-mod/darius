using UnityEngine;

// Darius-specific presentation entry points for the fresh EntityModel path.
// Gameplay remains owned by SoD; these helpers only request Darius action poses, anchors and
// LoL-specific overlay surfaces from the currently loaded native model.
public static class DariusSkinAnimationHooks
{
    public static void PlayQ(Hero hero, bool instant = false)
    {
        DariusNativeAnimationAdapter.PlayAbility(hero, "Spell1", instant);
    }

    public static void PlayW(Hero hero)
    {
        PlayW(hero, hero != null ? hero.transform.forward : Vector3.forward);
    }

    public static void PlayW(Hero hero, Vector3 direction)
    {
        DariusNativeAnimationAdapter.PlayAbility(hero, "Spell2", direction);
    }

    public static void SetWArmed(Hero hero, bool armed)
    {
        if (armed) DariusNativeAnimationAdapter.PlayAbility(hero, "Spell2_Idle", true);
        else DariusNativeAnimationAdapter.StopAbility(hero);
    }

    public static void PlayE(Hero hero)
    {
        DariusNativeAnimationAdapter.PlayAbility(hero, "Spell3");
    }

    public static void PlayR(Hero hero)
    {
        DariusNativeAnimationAdapter.PlayAbility(hero, "Spell4");
    }

    public static void PlayAttack(Hero hero, bool alternate, bool critical = false)
    {
        PlayAttack(hero, alternate, critical, hero != null ? hero.transform.forward : Vector3.forward);
    }

    public static void PlayAttack(Hero hero, bool alternate, bool critical, Vector3 direction)
    {
        string clip = critical ? "Crit" : (alternate ? "Attack2" : "Attack1");
        DariusNativeAnimationAdapter.PlayAbility(hero, clip, direction);
    }

    public static bool IsGodKing(Hero hero)
    {
        return string.Equals(GetVariantKey(hero), "GodKing", System.StringComparison.OrdinalIgnoreCase);
    }

    public static string GetVariantKey(Hero hero)
    {
        Hero_Darius darius = hero as Hero_Darius;
        EntityModel model = darius != null && darius.Visual != null ? darius.Visual.model : null;
        DariusOfficialEntityModelMarker marker =
            model != null ? model.GetComponent<DariusOfficialEntityModelMarker>() : null;
        return marker != null && !string.IsNullOrEmpty(marker.variantKey) ? marker.variantKey : "Classic";
    }

    public static Transform GetWeaponAnchor(Hero hero)
    {
        Transform anchor = DariusOfficialPresentationSurface.GetWeaponAnchor(hero);
        return anchor != null ? anchor : (hero != null ? hero.transform : null);
    }

    public static Transform GetChestAnchor(Hero hero)
    {
        Transform anchor = DariusOfficialPresentationSurface.FindAnchor(
            hero, "C_BuffBone_Glb_Chest_Loc", "Chest", "Spine");
        return anchor != null ? anchor : (hero != null ? hero.transform : null);
    }

    public static Transform GetAnchor(Hero hero, params string[] names)
    {
        Transform anchor = DariusOfficialPresentationSurface.FindAnchor(hero, names);
        return anchor != null ? anchor : (hero != null ? hero.transform : null);
    }

    public static GameObject CreateSubmeshOverlay(Hero hero, uint hash, Material material)
    {
        return DariusOfficialPresentationSurface.CreateSubmeshOverlay(hero, hash, material);
    }

    public static GameObject CreateFullMeshOverlay(Hero hero, Material material, string label)
    {
        return DariusOfficialPresentationSurface.CreateFullMeshOverlay(hero, material, label);
    }
}
