using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;

public static partial class DariusConstellationRegistry
{
    private static readonly Dictionary<Type, string> IconKeyByType = new Dictionary<Type, string>();

    private static bool _registered;

    public static void Register()
    {
        if (_registered) return;

        RegisterStar<Se_Star_Darius_D_Conqueror>(DariusConstellationIds.DConqueror, "894ce37e-073e-5b17-89c8-3d6cd7c57928", StarType.Destruction, 4, 0, "RUNE_CONQUEROR");
        RegisterStar<Se_Star_Darius_D_Triumph>(DariusConstellationIds.DTriumph, "c3fce88d-744b-5ef8-8ca7-f37d7ed90bf5", StarType.Destruction, 4, 3, "RUNE_TRIUMPH");
        RegisterStar<Se_Star_Darius_D_Alacrity>(DariusConstellationIds.DAlacrity, "835c8764-0ec9-5e4f-b6e3-b2db267ff25a", StarType.Destruction, 4, 5, "RUNE_ALACRITY");
        RegisterStar<Se_Star_Darius_D_LastStand>(DariusConstellationIds.DLastStand, "0e878932-c89c-53fe-8b8d-c234c721e3d0", StarType.Destruction, 4, 10, "RUNE_LASTSTAND");
        RegisterStar<Se_Star_Darius_D_AxiomArcanist>(DariusConstellationIds.DAxiomArcanist, "116da196-1efa-5be7-bd37-cd6e17dfaa07", StarType.Flexible, 4, 20, "RUNE_AXIOM");
        RegisterStar<Se_Star_Darius_D_NoxianArena>(DariusConstellationIds.DNoxianArena, "43f38fd7-d6ff-4ee0-be5f-96fa01fc41df", StarType.Destruction, 4, 30, "RUNE_LASTSTAND");

        RegisterStar<Se_Star_Darius_L_SecondWind>(DariusConstellationIds.LSecondWind, "3cb0e5c7-2568-5dc4-9e83-635903c016bb", StarType.Life, 4, 0, "RUNE_SECONDWIND");
        RegisterStar<Se_Star_Darius_L_Overgrowth>(DariusConstellationIds.LOvergrowth, "aa078769-474e-503f-8cea-4a79aef40e94", StarType.Life, 4, 5, "RUNE_OVERGROWTH");
        RegisterStar<Se_Star_Darius_L_Revitalize>(DariusConstellationIds.LRevitalize, "bace7cd6-b89d-5677-b991-cb3451add034", StarType.Flexible, 4, 15, "RUNE_REVITALIZE");
        RegisterStar<Se_Star_Darius_L_Conditioning>(DariusConstellationIds.LConditioning, "b001793e-c937-5343-8d12-bf19ae1b6c80", StarType.Life, 4, 10, "RUNE_CONDITIONING");
        RegisterStar<Se_Star_Darius_L_Unflinching>(DariusConstellationIds.LUnflinching, "4167e69a-c3fb-5e58-a388-8e15d2fb3437", StarType.Life, 4, 15, "RUNE_UNFLINCHING");

        RegisterStar<Se_Star_Darius_I_CripplingStrike>(DariusConstellationIds.ICripplingStrike, "c967018d-e824-5ed9-992c-fffded3be39b", StarType.Flexible, 4, 0, "W");
        RegisterStar<Se_Star_Darius_I_Apprehend>(DariusConstellationIds.IApprehend, "b86b8872-d105-5770-9965-35f6ce53b365", StarType.Flexible, 4, 0, "E");
        RegisterStar<Se_Star_Darius_I_InstantDecimate>(DariusConstellationIds.IInstantDecimate, "28a6288e-c433-5f64-bb8e-b1c5347dd808", StarType.Flexible, 4, 25, "Q");
        RegisterStar<Se_Star_Darius_I_WarFervor>(DariusConstellationIds.IWarFervor, "945d0dde-705b-5d19-ae76-12f186c63b6d", StarType.Flexible, 4, 25, "RUNE_FERVOR");
        RegisterStar<Se_Star_Darius_I_LegacyCripplingStrike>(DariusConstellationIds.ILegacyCripplingStrike, "6b98a930-704c-527a-bec2-be6a2e5b8ffa", StarType.Flexible, 4, 10, "W");
        RegisterStar<Se_Star_Darius_I_LegacyApprehend>(DariusConstellationIds.ILegacyApprehend, "2ecf521e-bc1c-51d5-9d40-9a9bd8653bed", StarType.Flexible, 4, 15, "E");
        RegisterStar<Se_Star_Darius_I_LegacyGuillotine>(DariusConstellationIds.ILegacyGuillotine, "e47dcb4b-fd61-5b21-a642-cfda095f77ab", StarType.Flexible, 4, 25, "R");
        RegisterStar<Se_Star_Darius_D_BloodRush>(DariusConstellationIds.DBloodRush, "8ee908ba-b0e3-5d2b-a5a1-33f967b26585", StarType.Destruction, 4, 5, "RUNE_CELERITY");
        RegisterStar<Se_Star_Darius_D_NoxianMight>(DariusConstellationIds.DNoxianMight, "b2c4a927-911b-59ea-9768-13ae87b20dea", StarType.Destruction, 4, 20, "RUNE_CONQUEROR");
        RegisterStar<Se_Star_Darius_D_Dunkmaster>(DariusConstellationIds.DDunkmaster, "53c567a4-0e95-5017-b044-3c3d238cf999", StarType.Destruction, 4, 15, "R");
        RegisterStar<Se_Star_Darius_L_BloodPrice>(DariusConstellationIds.LBloodPrice, "77aba1d0-22cb-5ed2-a279-155dcf485387", StarType.Life, 4, 10, "RUNE_TRIUMPH");
        RegisterStar<Se_Star_Darius_F_HandOfNoxus>(DariusConstellationIds.FHandOfNoxus, "4a8537b1-2711-59db-8cac-c5bb5e08f287", StarType.Imagination, 3, 15, "H");

        RegisterStar<Se_Star_Darius_F_NimbusCloak>(DariusConstellationIds.FNimbusCloak, "0a1205ba-949f-538b-89de-650e8bf0cfe6", StarType.Imagination, 3, 3, "RUNE_NIMBUS");
        RegisterStar<Se_Star_Darius_F_Celerity>(DariusConstellationIds.FCelerity, "67dc3cdd-77fc-51a5-b556-9cdc4105a610", StarType.Imagination, 3, 5, "RUNE_CELERITY");
        RegisterStar<Se_Star_Darius_F_GatheringStorm>(DariusConstellationIds.FGatheringStorm, "adf3a77f-87a2-55cb-9eda-9a64a73c58b3", StarType.Imagination, 3, 15, "RUNE_GATHERING");
        RegisterStar<Se_Star_Darius_F_CosmicInsight>(DariusConstellationIds.FCosmicInsight, "9d2070b3-dcc1-5260-a19a-ebc37da299da", StarType.Flexible, 3, 10, "RUNE_COSMIC");

        // Pass 2: ten LoL equipment constellations plus the user-designed R-mechanic star.
        // Flexible remains strictly reserved for skill-mechanic rewrites.
        RegisterStar<Se_Star_Darius_D_ItemTrinityForce>(DariusEquipmentStarIds.TrinityForce, "948aaa47-89a7-510f-b903-185af94a63d8", StarType.Destruction, 4, 5, "ITEM_TRINITY");
        RegisterStar<Se_Star_Darius_D_ItemBlackCleaver>(DariusEquipmentStarIds.BlackCleaver, "dc43f66e-ad4d-5287-b766-a39b9937104c", StarType.Destruction, 4, 10, "ITEM_BLACK_CLEAVER");
        RegisterStar<Se_Star_Darius_D_ItemSpearOfShojin>(DariusEquipmentStarIds.SpearOfShojin, "30010cc5-ff6e-5f2c-a238-c838461d9af8", StarType.Destruction, 4, 20, "ITEM_SHOJIN");
        RegisterStar<Se_Star_Darius_L_ItemSteraksGage>(DariusEquipmentStarIds.SteraksGage, "99c89b92-0a78-515c-a33d-468f2d6a630d", StarType.Life, 4, 5, "ITEM_STERAK");
        RegisterStar<Se_Star_Darius_L_ItemDeathsDance>(DariusEquipmentStarIds.DeathsDance, "247a5dbf-e358-5318-938f-61a192e14613", StarType.Life, 4, 15, "ITEM_DEATHS_DANCE");
        RegisterStar<Se_Star_Darius_L_ItemOverlordsBloodmail>(DariusEquipmentStarIds.OverlordsBloodmail, "a65d70c4-9105-541e-ba11-e9c482d69e4f", StarType.Life, 4, 25, "ITEM_BLOODMAIL");
        RegisterStar<Se_Star_Darius_I_ItemSunderedSky>(DariusEquipmentStarIds.SunderedSky, "62de3a6e-8b96-5a3c-9456-7ec3aa1e0279", StarType.Imagination, 4, 10, "ITEM_SUNDERED_SKY");
        RegisterStar<Se_Star_Darius_I_ItemDeadMansPlate>(DariusEquipmentStarIds.DeadMansPlate, "1bfcb1f3-df0f-5991-9240-42ad1a0af969", StarType.Imagination, 4, 10, "ITEM_DEAD_MANS");
        RegisterStar<Se_Star_Darius_I_ItemYoumuusGhostblade>(DariusEquipmentStarIds.YoumuusGhostblade, "25ed20cd-9fde-53ce-8529-b05a14cdb8e3", StarType.Imagination, 4, 10, "ITEM_YOUMUU");
        RegisterStar<Se_Star_Darius_F_ItemStridebreaker>(DariusEquipmentStarIds.Stridebreaker, "cde73341-54dc-5949-bf65-153a4e29d140", StarType.Flexible, 4, 20, "ITEM_STRIDEBREAKER");
        RegisterStar<Se_Star_Darius_F_Awoo>(DariusEquipmentStarIds.Awoo, "88e91c5f-91cc-5f51-9749-86a3e18950f1", StarType.Flexible, 1, 30, "STAR_AWOO");

        _registered = true;
        DariusLog.Info("CONSTELLATION", "Registered 38 native StarEffect resources including Noxian Arena, 10 equipment stars and Awoo R-mechanic star.");
    }

    private static void RegisterStar<T>(string name, string guid, StarType type, int maxLevel, int requiredLevel, string iconKey) where T : DariusStarEffect
    {
        guid = "sod-darius-star-" + guid;
        IconKeyByType[typeof(T)] = iconKey;
        T star = DariusFormalRegistry.RegisterRuntimePrefab<T>(name, guid, s =>
        {
            s.type = type;
            DariusConstellationReflection.ConfigureProgression(s, maxLevel, requiredLevel, iconKey);
        });
        DariusLog.Info("CONSTELLATION-STAR", name + " type=" + type + " maxLevel=" + maxLevel + " requiredLevel=" + requiredLevel + " prefab=" + (star != null));
    }

    public static bool TryGetIconKey(StarEffect star, out string iconKey)
    {
        iconKey = null;
        return star != null && IconKeyByType.TryGetValue(star.GetType(), out iconKey) && !string.IsNullOrEmpty(iconKey);
    }

    public static bool TryGetIconKey(Type starType, out string iconKey)
    {
        iconKey = null;
        return starType != null && IconKeyByType.TryGetValue(starType, out iconKey) && !string.IsNullOrEmpty(iconKey);
    }

}
