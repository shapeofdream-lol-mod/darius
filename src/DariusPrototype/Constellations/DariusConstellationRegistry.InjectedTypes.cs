using System;

public static partial class DariusConstellationRegistry
{
    private static readonly Type[] DariusStarTypes =
    {
        typeof(Se_Star_Darius_D_Conqueror), typeof(Se_Star_Darius_D_Triumph), typeof(Se_Star_Darius_D_Alacrity),
        typeof(Se_Star_Darius_D_LastStand), typeof(Se_Star_Darius_D_AxiomArcanist), typeof(Se_Star_Darius_D_NoxianArena),
        typeof(Se_Star_Darius_L_SecondWind), typeof(Se_Star_Darius_L_Overgrowth), typeof(Se_Star_Darius_L_Revitalize),
        typeof(Se_Star_Darius_L_Conditioning), typeof(Se_Star_Darius_L_Unflinching),
        typeof(Se_Star_Darius_I_CripplingStrike), typeof(Se_Star_Darius_I_Apprehend),
        typeof(Se_Star_Darius_I_InstantDecimate), typeof(Se_Star_Darius_I_WarFervor),
        typeof(Se_Star_Darius_I_LegacyCripplingStrike), typeof(Se_Star_Darius_I_LegacyApprehend), typeof(Se_Star_Darius_I_LegacyGuillotine),
        typeof(Se_Star_Darius_D_BloodRush), typeof(Se_Star_Darius_D_NoxianMight), typeof(Se_Star_Darius_D_Dunkmaster),
        typeof(Se_Star_Darius_L_BloodPrice), typeof(Se_Star_Darius_F_HandOfNoxus),
        typeof(Se_Star_Darius_F_NimbusCloak), typeof(Se_Star_Darius_F_Celerity),
        typeof(Se_Star_Darius_F_GatheringStorm), typeof(Se_Star_Darius_F_CosmicInsight),
        typeof(Se_Star_Darius_D_ItemTrinityForce), typeof(Se_Star_Darius_D_ItemBlackCleaver), typeof(Se_Star_Darius_D_ItemSpearOfShojin),
        typeof(Se_Star_Darius_L_ItemSteraksGage), typeof(Se_Star_Darius_L_ItemDeathsDance), typeof(Se_Star_Darius_L_ItemOverlordsBloodmail),
        typeof(Se_Star_Darius_I_ItemSunderedSky), typeof(Se_Star_Darius_I_ItemDeadMansPlate), typeof(Se_Star_Darius_I_ItemYoumuusGhostblade),
        typeof(Se_Star_Darius_F_ItemStridebreaker), typeof(Se_Star_Darius_F_Awoo)
    };

    public static void RegisterTypeCache()
    {
        DariusUnsupportedResourceBridge.EnsureStarTypes(
            DariusStarTypes, "CONSTELLATION-TYPE");
    }

    public static void UnregisterTypeCache()
    {
        DariusUnsupportedResourceBridge.RemoveStarTypes(
            DariusStarTypes, "CONSTELLATION-TYPE");
        IconKeyByType.Clear();
        _registered = false;
    }
}
