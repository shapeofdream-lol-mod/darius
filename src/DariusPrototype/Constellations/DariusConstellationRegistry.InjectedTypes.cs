public static partial class DariusConstellationRegistry
{
    public static void ReassertTypeCache()
    {
        Type[] types =
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
        for (int i = 0; i < types.Length; i++) InjectStarType(types[i]);
    }

    public static void RemoveInjectedTypes()
    {
        try
        {
            FieldInfo field = typeof(Dew).GetField("_allStarTypes", BindingFlags.Static | BindingFlags.NonPublic);
            List<Type> list = field != null ? field.GetValue(null) as List<Type> : null;
            if (list != null)
            {
                int removed = 0;
                for (int i = list.Count - 1; i >= 0; i--)
                {
                    Type candidate = list[i];
                    // Remove both current-assembly entries and stale timestamped-assembly entries
                    // owned by this Traveler. Stock/other-Mod StarEffects are never touched.
                    if (InjectedTypes.Contains(candidate) || IsOwnedStarType(candidate))
                    {
                        list.RemoveAt(i);
                        removed++;
                    }
                }
                if (removed > 0) DariusLog.Info("CONSTELLATION-RELOAD", "Unregistered " + removed + " owned/stale star Type cache entries.");
            }
        }
        catch (Exception e)
        {
            DariusLog.Exception("CONSTELLATION-RELOAD", e, "RemoveInjectedTypes semantic cleanup failed");
        }
        InjectedTypes.Clear();
        IconKeyByType.Clear();
        _registered = false;
    }
}