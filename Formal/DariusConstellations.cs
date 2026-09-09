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

// Native Darius constellation set. Every entry is a real StarEffect bound to Hero_Darius and
// participates in Shape of Dreams' normal constellation loadout, profile, slot and level pipeline.
public static class DariusConstellationIds
{
    public const string DConqueror = "Se_Star_Darius_D_Conqueror";
    public const string DTriumph = "Se_Star_Darius_D_Triumph";
    public const string DAlacrity = "Se_Star_Darius_D_Alacrity";
    public const string DLastStand = "Se_Star_Darius_D_LastStand";
    public const string DAxiomArcanist = "Se_Star_Darius_D_AxiomArcanist";
    public const string DNoxianArena = "Se_Star_Darius_D_NoxianArena";

    public const string LSecondWind = "Se_Star_Darius_L_SecondWind";
    public const string LOvergrowth = "Se_Star_Darius_L_Overgrowth";
    public const string LRevitalize = "Se_Star_Darius_L_Revitalize";
    public const string LConditioning = "Se_Star_Darius_L_Conditioning";
    public const string LUnflinching = "Se_Star_Darius_L_Unflinching";

    public const string ICripplingStrike = "Se_Star_Darius_I_CripplingStrike";
    public const string IApprehend = "Se_Star_Darius_I_Apprehend";
    public const string IInstantDecimate = "Se_Star_Darius_I_InstantDecimate";
    public const string IWarFervor = "Se_Star_Darius_I_WarFervor";
    public const string ILegacyCripplingStrike = "Se_Star_Darius_I_LegacyCripplingStrike";
    public const string ILegacyApprehend = "Se_Star_Darius_I_LegacyApprehend";
    public const string ILegacyGuillotine = "Se_Star_Darius_I_LegacyGuillotine";

    public const string DBloodRush = "Se_Star_Darius_D_BloodRush";
    public const string DNoxianMight = "Se_Star_Darius_D_NoxianMight";
    public const string DDunkmaster = "Se_Star_Darius_D_Dunkmaster";
    public const string LBloodPrice = "Se_Star_Darius_L_BloodPrice";
    public const string FHandOfNoxus = "Se_Star_Darius_F_HandOfNoxus";

    public const string FNimbusCloak = "Se_Star_Darius_F_NimbusCloak";
    public const string FCelerity = "Se_Star_Darius_F_Celerity";
    public const string FGatheringStorm = "Se_Star_Darius_F_GatheringStorm";
    public const string FCosmicInsight = "Se_Star_Darius_F_CosmicInsight";
}

public abstract class DariusStarEffect : StarEffect
{
    public override Type heroType => typeof(Hero_Darius);
    protected abstract string DariusStarKey { get; }
    protected virtual int FallbackMaxLevel => 4;

    public override void OnStartServer()
    {
        base.OnStartServer();
        try
        {
            if (hero == null)
            {
                DariusLog.Warn("STAR", DariusStarKey + " OnStartServer has no hero.");
                return;
            }
            DariusConstellationRuntime runtime = hero.GetComponent<DariusConstellationRuntime>();
            if (runtime == null) runtime = hero.gameObject.AddComponent<DariusConstellationRuntime>();
            int level = DariusConstellationReflection.ReadRuntimeLevel(this, FallbackMaxLevel);
            runtime.SetStar(DariusStarKey, level, true);
            DariusLog.Info("STAR", "Enabled " + DariusStarKey + " level=" + level + " hero=" + DariusLog.EntityLabel(hero));
        }
        catch (Exception e)
        {
            DariusLog.Exception("STAR", e, "Starting star failed: " + DariusStarKey);
        }
    }

    public override void OnStopServer()
    {
        try
        {
            if (hero != null)
            {
                DariusConstellationRuntime runtime = hero.GetComponent<DariusConstellationRuntime>();
                if (runtime != null) runtime.SetStar(DariusStarKey, 0, false);
            }
        }
        catch (Exception e)
        {
            DariusLog.Exception("STAR", e, "Stopping star failed: " + DariusStarKey);
        }
        base.OnStopServer();
    }
}

public sealed class Se_Star_Darius_D_Conqueror : DariusStarEffect { protected override string DariusStarKey => DariusConstellationIds.DConqueror; }
public sealed class Se_Star_Darius_D_Triumph : DariusStarEffect { protected override string DariusStarKey => DariusConstellationIds.DTriumph; }
public sealed class Se_Star_Darius_D_Alacrity : DariusStarEffect { protected override string DariusStarKey => DariusConstellationIds.DAlacrity; }
public sealed class Se_Star_Darius_D_LastStand : DariusStarEffect { protected override string DariusStarKey => DariusConstellationIds.DLastStand; }
public sealed class Se_Star_Darius_D_AxiomArcanist : DariusStarEffect { protected override string DariusStarKey => DariusConstellationIds.DAxiomArcanist; }
public sealed class Se_Star_Darius_D_NoxianArena : DariusStarEffect { protected override string DariusStarKey => DariusConstellationIds.DNoxianArena; }

public sealed class Se_Star_Darius_L_SecondWind : DariusStarEffect { protected override string DariusStarKey => DariusConstellationIds.LSecondWind; }
public sealed class Se_Star_Darius_L_Overgrowth : DariusStarEffect { protected override string DariusStarKey => DariusConstellationIds.LOvergrowth; }
public sealed class Se_Star_Darius_L_Revitalize : DariusStarEffect { protected override string DariusStarKey => DariusConstellationIds.LRevitalize; }
public sealed class Se_Star_Darius_L_Conditioning : DariusStarEffect { protected override string DariusStarKey => DariusConstellationIds.LConditioning; }
public sealed class Se_Star_Darius_L_Unflinching : DariusStarEffect { protected override string DariusStarKey => DariusConstellationIds.LUnflinching; }

public sealed class Se_Star_Darius_I_CripplingStrike : DariusStarEffect { protected override string DariusStarKey => DariusConstellationIds.ICripplingStrike; }
public sealed class Se_Star_Darius_I_Apprehend : DariusStarEffect { protected override string DariusStarKey => DariusConstellationIds.IApprehend; }
public sealed class Se_Star_Darius_I_InstantDecimate : DariusStarEffect { protected override string DariusStarKey => DariusConstellationIds.IInstantDecimate; }
public sealed class Se_Star_Darius_I_WarFervor : DariusStarEffect { protected override string DariusStarKey => DariusConstellationIds.IWarFervor; }
public sealed class Se_Star_Darius_I_LegacyCripplingStrike : DariusStarEffect { protected override string DariusStarKey => DariusConstellationIds.ILegacyCripplingStrike; }
public sealed class Se_Star_Darius_I_LegacyApprehend : DariusStarEffect { protected override string DariusStarKey => DariusConstellationIds.ILegacyApprehend; }
public sealed class Se_Star_Darius_I_LegacyGuillotine : DariusStarEffect { protected override string DariusStarKey => DariusConstellationIds.ILegacyGuillotine; }
public sealed class Se_Star_Darius_D_BloodRush : DariusStarEffect { protected override string DariusStarKey => DariusConstellationIds.DBloodRush; }
public sealed class Se_Star_Darius_D_NoxianMight : DariusStarEffect { protected override string DariusStarKey => DariusConstellationIds.DNoxianMight; }
public sealed class Se_Star_Darius_D_Dunkmaster : DariusStarEffect { protected override string DariusStarKey => DariusConstellationIds.DDunkmaster; }
public sealed class Se_Star_Darius_L_BloodPrice : DariusStarEffect { protected override string DariusStarKey => DariusConstellationIds.LBloodPrice; }
public sealed class Se_Star_Darius_F_HandOfNoxus : DariusStarEffect { protected override string DariusStarKey => DariusConstellationIds.FHandOfNoxus; protected override int FallbackMaxLevel => 3; }

public sealed class Se_Star_Darius_F_NimbusCloak : DariusStarEffect { protected override string DariusStarKey => DariusConstellationIds.FNimbusCloak; protected override int FallbackMaxLevel => 3; }
public sealed class Se_Star_Darius_F_Celerity : DariusStarEffect { protected override string DariusStarKey => DariusConstellationIds.FCelerity; protected override int FallbackMaxLevel => 3; }
public sealed class Se_Star_Darius_F_GatheringStorm : DariusStarEffect { protected override string DariusStarKey => DariusConstellationIds.FGatheringStorm; protected override int FallbackMaxLevel => 3; }
public sealed class Se_Star_Darius_F_CosmicInsight : DariusStarEffect { protected override string DariusStarKey => DariusConstellationIds.FCosmicInsight; protected override int FallbackMaxLevel => 3; }

public static class DariusConstellationRegistry
{
    private static readonly List<Type> InjectedTypes = new List<Type>();
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

        RegisterStar<Se_Star_Darius_I_CripplingStrike>(DariusConstellationIds.ICripplingStrike, "c967018d-e824-5ed9-992c-fffded3be39b", StarType.Flexible, 4, 10, "W");
        RegisterStar<Se_Star_Darius_I_Apprehend>(DariusConstellationIds.IApprehend, "b86b8872-d105-5770-9965-35f6ce53b365", StarType.Flexible, 4, 10, "E");
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
        InjectStarType(typeof(T));
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

    private static bool IsOwnedStarType(Type type)
    {
        return type != null && !string.IsNullOrEmpty(type.Name) && type.Name.StartsWith("Se_Star_Darius_", StringComparison.Ordinal);
    }

    private static bool IsSameSemanticStarType(Type left, Type right)
    {
        if (left == null || right == null) return false;
        if (!IsOwnedStarType(left) || !IsOwnedStarType(right)) return false;
        return string.Equals(left.FullName, right.FullName, StringComparison.Ordinal) ||
               string.Equals(left.Name, right.Name, StringComparison.Ordinal);
    }

    private static void InjectStarType(Type starType)
    {
        if (starType == null) return;
        try
        {
            // Force Dew's lazy cache to initialize, but never manufacture an empty replacement list.
            PropertyInfo lazyProperty = typeof(Dew).GetProperty("allStarTypes", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (lazyProperty != null && lazyProperty.CanRead)
            {
                try { lazyProperty.GetValue(null, null); } catch { }
            }

            FieldInfo field = typeof(Dew).GetField("_allStarTypes", BindingFlags.Static | BindingFlags.NonPublic);
            if (field == null)
            {
                DariusLog.Warn("CONSTELLATION", "Dew._allStarTypes was not found; star type cache injection skipped for " + starType.Name);
                return;
            }
            List<Type> list = field.GetValue(null) as List<Type>;
            if (list == null)
            {
                DariusLog.Warn("CONSTELLATION", "Dew._allStarTypes is not initialized yet; deferred " + starType.Name);
                return;
            }

            // Dew hot reload loads a timestamped copy of the DLL. The old CLR Type and the new CLR
            // Type are therefore different objects even though they represent the same star resource.
            // Type-identity dedupe (`List.Contains`) is insufficient and caused one visible duplicate
            // constellation after every unrelated Mod add/remove. Collapse stale semantic duplicates
            // before the current type is added.
            int staleRemoved = 0;
            for (int i = list.Count - 1; i >= 0; i--)
            {
                Type existing = list[i];
                if (existing == starType) continue;
                if (!IsSameSemanticStarType(existing, starType)) continue;
                list.RemoveAt(i);
                staleRemoved++;
            }
            if (staleRemoved > 0)
                DariusLog.Warn("CONSTELLATION-RELOAD", "Removed " + staleRemoved + " stale hot-reload Type instance(s) for " + starType.Name + " before re-registration.");

            if (!list.Contains(starType)) list.Add(starType);
            if (!InjectedTypes.Contains(starType)) InjectedTypes.Add(starType);
        }
        catch (Exception e)
        {
            DariusLog.Exception("CONSTELLATION", e, "InjectStarType failed type=" + starType.Name);
        }
    }

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

public static class DariusConstellationReflection
{
    private static readonly HashSet<string> LoggedMissingLevel = new HashSet<string>();

    public static int ReadRuntimeLevel(StarEffect star, int fallbackMax)
    {
        if (star == null) return 1;
        foreach (string name in new[] { "level", "currentLevel", "starLevel", "_level", "<level>k__BackingField", "<currentLevel>k__BackingField" })
        {
            object value;
            if (TryReadMember(star, name, out value))
            {
                try
                {
                    int level = Convert.ToInt32(value);
                    if (level > 0) return Mathf.Clamp(level, 1, Mathf.Max(1, fallbackMax));
                }
                catch { }
            }
        }
        string typeName = star.GetType().Name;
        if (LoggedMissingLevel.Add(typeName))
            DariusLog.Warn("CONSTELLATION-LEVEL", "Could not discover runtime level member on " + typeName + "; using level 1 fallback. Field scan will be logged next.");
        LogLevelMembers(star);
        return 1;
    }

    public static void ConfigureProgression(StarEffect star, int maxLevel, int requiredLevel, string iconKey)
    {
        if (star == null) return;

        // A blank runtime StarEffect has enough fields to register, but the stock constellation UI
        // also expects native serialized per-level metadata arrays. A minimal runtime star that only
        // supplies maxStarLevel and icon can make UI_Lobby_Constellations_StarItem.Refresh index an empty array after a
        // purchase/drag/save. Hydrate the base StarEffect data contract from a real stock star of the
        // same category before applying Darius-owned progression and visuals.
        HydrateFromNativeStar(star, maxLevel);

        // Character-owned Stars must follow the same progression contract as stock Traveler Stars:
        // they unlock from this Traveler's own Mastery, not account-wide Total Mastery.  Runtime
        // hydration can copy either a global or character template, so always overwrite both fields.
        bool heroTypeSet = false;
        foreach (string name in new[] { "heroType", "_heroType", "<heroType>k__BackingField", "requiredHeroType" })
            heroTypeSet = TrySetMember(star, name, DariusTravelerRegistry.HeroName) || heroTypeSet;
        bool masteryScopeSet = false;
        foreach (string name in new[] { "isRequiredLevelTotalMastery", "_isRequiredLevelTotalMastery", "<isRequiredLevelTotalMastery>k__BackingField" })
            masteryScopeSet = TrySetMember(star, name, false) || masteryScopeSet;

        bool maxSet = TrySetMember(star, "maxStarLevel", maxLevel) || TrySetMember(star, "_maxStarLevel", maxLevel) || TrySetMember(star, "<maxStarLevel>k__BackingField", maxLevel) || TrySetMember(star, "maxLevel", maxLevel) || TrySetMember(star, "_maxLevel", maxLevel);
        bool reqSet = false;
        foreach (string name in new[] { "requiredLevel", "requiredHeroLevel", "unlockLevel", "requiredProfileLevel", "_requiredLevel", "<requiredLevel>k__BackingField" })
            reqSet = TrySetMember(star, name, requiredLevel) || reqSet;

        Sprite icon = DariusPrototypeIcons.Get(iconKey);
        bool iconSet = false;
        int presentationFields = 0;
        if (icon != null)
        {
            foreach (string name in new[] { "icon", "starIcon", "_icon", "<icon>k__BackingField" })
                iconSet = TrySetMember(star, name, icon) || iconSet;
            presentationFields = ApplyIconPresentationFields(star, icon, maxLevel);
            iconSet = iconSet || presentationFields > 0;
        }
        DariusLog.DebugInfo("CONSTELLATION-META", star.name + " maxSet=" + maxSet + " requiredSet=" + reqSet +
            " heroTypeSet=" + heroTypeSet + " perHeroMasterySet=" + masteryScopeSet +
            " iconSet=" + iconSet + " presentationFields=" + presentationFields);
    }



    private static int ApplyIconPresentationFields(StarEffect star, Sprite icon, int maxLevel)
    {
        if (star == null || icon == null) return 0;
        int changed = 0;
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        for (Type t = star.GetType(); t != null && t != typeof(UnityEngine.Object); t = t.BaseType)
        {
            foreach (FieldInfo field in t.GetFields(flags))
            {
                if (field.IsStatic || field.IsInitOnly) continue;
                try
                {
                    if (field.FieldType == typeof(Sprite))
                    {
                        field.SetValue(star, icon);
                        changed++;
                    }
                    else if (field.FieldType == typeof(Sprite[]))
                    {
                        Sprite[] current = field.GetValue(star) as Sprite[];
                        int len = current != null && current.Length > 0 ? current.Length : Math.Max(1, maxLevel + 1);
                        Sprite[] values = new Sprite[len];
                        for (int i = 0; i < len; i++) values[i] = icon;
                        field.SetValue(star, values);
                        changed++;
                    }
                    else if (typeof(IList<Sprite>).IsAssignableFrom(field.FieldType))
                    {
                        IList<Sprite> list = field.GetValue(star) as IList<Sprite>;
                        if (list != null)
                        {
                            int count = list.Count > 0 ? list.Count : Math.Max(1, maxLevel + 1);
                            list.Clear();
                            for (int i = 0; i < count; i++) list.Add(icon);
                            changed++;
                        }
                    }
                }
                catch { }
            }
        }
        return changed;
    }

    private static void HydrateFromNativeStar(StarEffect target, int maxLevel)
    {
        StarEffect source = FindNativeTemplate(target != null ? target.type : default(StarType), maxLevel);
        if (source == null || target == null)
        {
            DariusLog.Warn("CONSTELLATION-CONTRACT", "No stock StarEffect template found for " + (target != null ? target.name : "<null>"));
            return;
        }

        int copied = 0;
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        try
        {
            foreach (FieldInfo field in typeof(StarEffect).GetFields(flags))
            {
                if (field.IsStatic || field.IsInitOnly) continue;
                if (!field.IsPublic && !Attribute.IsDefined(field, typeof(SerializeField))) continue;
                try
                {
                    object value = CloneSerializedValue(field.GetValue(source));
                    field.SetValue(target, value);
                    copied++;
                }
                catch { }
            }

            // Some game builds keep upgrade/cost presentation arrays directly on StarEffect. Make
            // every serialized sequence long enough for both zero-based and one-based level indexing.
            EnsureSerializedSequenceCapacity(target, Mathf.Max(2, maxLevel + 1));
            DariusLog.DebugInfo("CONSTELLATION-CONTRACT", target.name + " hydrated from " + source.GetType().Name +
                " copiedFields=" + copied + " sourceMax=" + source.maxStarLevel + " requestedMax=" + maxLevel);
        }
        catch (Exception e)
        {
            DariusLog.Exception("CONSTELLATION-CONTRACT", e, "Could not hydrate native StarEffect contract for " + target.name);
        }
    }

    private static StarEffect FindNativeTemplate(StarType type, int requestedMax)
    {
        try
        {
            Type nativeType = type == StarType.Destruction ? typeof(Se_Star_D_CritChance) :
                type == StarType.Life ? typeof(Se_Star_L_Armor) :
                type == StarType.Imagination ? typeof(Se_Star_I_AdditionalChaosChance) :
                type == StarType.Flexible ? typeof(Se_Star_Aurena_F_CR_EarnGold) : null;
            return nativeType != null ? DewResources.GetByType<StarEffect>(nativeType) : null;
        }
        catch { return null; }
    }

    private static object CloneSerializedValue(object value)
    {
        if (value == null) return null;
        Array array = value as Array;
        if (array != null) return array.Clone();
        IList list = value as IList;
        if (list != null)
        {
            try
            {
                IList clone = Activator.CreateInstance(value.GetType()) as IList;
                if (clone != null)
                {
                    foreach (object item in list) clone.Add(item);
                    return clone;
                }
            }
            catch { }
        }
        return value;
    }

    private static void EnsureSerializedSequenceCapacity(StarEffect star, int count)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        foreach (FieldInfo field in typeof(StarEffect).GetFields(flags))
        {
            if (field.IsStatic || field.IsInitOnly) continue;
            if (!field.IsPublic && !Attribute.IsDefined(field, typeof(SerializeField))) continue;
            try
            {
                if (field.FieldType.IsArray)
                {
                    Array old = field.GetValue(star) as Array;
                    if (old == null || old.Length == 0 || old.Length >= count) continue;
                    Type element = field.FieldType.GetElementType();
                    Array expanded = Array.CreateInstance(element, count);
                    for (int i = 0; i < count; i++) expanded.SetValue(old.GetValue(Mathf.Min(i, old.Length - 1)), i);
                    field.SetValue(star, expanded);
                }
                else if (typeof(IList).IsAssignableFrom(field.FieldType))
                {
                    IList list = field.GetValue(star) as IList;
                    if (list == null || list.Count == 0 || list.Count >= count) continue;
                    object last = list[list.Count - 1];
                    while (list.Count < count) list.Add(last);
                }
            }
            catch { }
        }
    }

    public static bool SetFloat(object target, float value, params string[] names)
    {
        if (target == null || names == null) return false;
        for (int i = 0; i < names.Length; i++)
            if (TrySetMember(target, names[i], value)) return true;
        return false;
    }

    public static int ReadEntityLevel(object target)
    {
        if (target == null) return 1;
        foreach (string name in new[] { "level", "currentLevel", "_level", "<level>k__BackingField" })
        {
            object value;
            if (TryReadMember(target, name, out value))
            {
                try { return Mathf.Max(1, Convert.ToInt32(value)); } catch { }
            }
        }
        return 1;
    }

    private static bool TryReadMember(object target, string name, out object value)
    {
        value = null;
        if (target == null || string.IsNullOrEmpty(name)) return false;
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        for (Type t = target.GetType(); t != null; t = t.BaseType)
        {
            try
            {
                FieldInfo f = t.GetField(name, flags | BindingFlags.DeclaredOnly);
                if (f != null) { value = f.GetValue(target); return true; }
                PropertyInfo p = t.GetProperty(name, flags | BindingFlags.DeclaredOnly);
                if (p != null && p.CanRead && p.GetIndexParameters().Length == 0) { value = p.GetValue(target, null); return true; }
            }
            catch { }
        }
        return false;
    }

    private static bool TrySetMember(object target, string name, object value)
    {
        if (target == null || string.IsNullOrEmpty(name)) return false;
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        for (Type t = target.GetType(); t != null; t = t.BaseType)
        {
            try
            {
                FieldInfo f = t.GetField(name, flags | BindingFlags.DeclaredOnly);
                if (f != null && !f.IsInitOnly)
                {
                    f.SetValue(target, ConvertValue(value, f.FieldType));
                    return true;
                }
                PropertyInfo p = t.GetProperty(name, flags | BindingFlags.DeclaredOnly);
                if (p != null && p.CanWrite && p.GetIndexParameters().Length == 0)
                {
                    p.SetValue(target, ConvertValue(value, p.PropertyType), null);
                    return true;
                }
            }
            catch { }
        }
        return false;
    }

    private static object ConvertValue(object value, Type type)
    {
        if (value == null || type == null) return value;
        if (type.IsInstanceOfType(value)) return value;
        try { return Convert.ChangeType(value, type); } catch { return value; }
    }

    private static void LogLevelMembers(object target)
    {
        if (target == null) return;
        try
        {
            List<string> names = new List<string>();
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            for (Type t = target.GetType(); t != null; t = t.BaseType)
            {
                foreach (FieldInfo f in t.GetFields(flags))
                    if (f.Name.IndexOf("level", StringComparison.OrdinalIgnoreCase) >= 0) names.Add(t.Name + "." + f.Name + ":" + f.FieldType.Name);
                foreach (PropertyInfo p in t.GetProperties(flags))
                    if (p.Name.IndexOf("level", StringComparison.OrdinalIgnoreCase) >= 0) names.Add(t.Name + "." + p.Name + ":" + p.PropertyType.Name);
            }
            DariusLog.DebugInfo("CONSTELLATION-LEVEL", target.GetType().Name + " level members=" + string.Join(",", names.ToArray()));
        }
        catch { }
    }
}

public sealed class DariusConstellationRuntime : MonoBehaviour
{
    private Hero _hero;
    private readonly Dictionary<string, int> _levels = new Dictionary<string, int>(StringComparer.Ordinal);
    private readonly Dictionary<int, float> _recentKills = new Dictionary<int, float>();

    private StatBonus _persistentBonus;
    private StatBonus _conquerorBonus;
    private StatBonus _lowHealthBonus;
    private StatBonus _alacrityBonus;
    private StatBonus _gatheringBonus;
    private StatBonus _nimbusBonus;
    private StatBonus _bloodRushBonus;
    private StatBonus _dunkmasterBonus;
    private StatBonus _noxianArenaBonus;

    private int _conquerorStacks;
    private float _conquerorExpiresAt;
    private int _killCount;
    private int _alacrityStacks;
    private int _lastGatheringStep = -1;
    private bool _lastLowHealth;
    private float _lastHealth = -1f;
    private float _secondWindPool;
    private float _nextSecondWindTick;
    private bool _spawnedW;
    private bool _spawnedE;
    private bool _nativeDejavuSpawnSeen;
    private DewPlayer _starterPlayer;
    private Coroutine _starterRoutine;
    private Coroutine _nimbusRoutine;
    private Coroutine _dunkmasterRoutine;
    private float _nextBloodRushRefresh;
    private int _lastBloodRushCount = -1;
    private float _nextNoxianArenaRefresh;
    private int _lastNoxianArenaCount = -1;
    private int _lastNoxianArenaLevel = -1;
    private DariusEnemyClass _lastNoxianArenaRank = DariusEnemyClass.Common;
    private readonly Dictionary<int, Coroutine> _cosmicRoutines = new Dictionary<int, Coroutine>();

    private void Awake()
    {
        _hero = GetComponent<Hero>();
        if (_hero != null) _lastHealth = _hero.currentHealth;
    }

    private void Start()
    {
        if (!NetworkServer.active) return;
        StartCoroutine(ApplySelectedLoadoutFallbackRoutine());
        StartCoroutine(StarterDejaVuFallbackReadyRoutine());
    }

    private IEnumerator StarterDejaVuFallbackReadyRoutine()
    {
        // Prefer PlayGameManager.DoDejavuSpawn because it is the game's own stable Memory-drop
        // lifecycle. Some runs do not execute a meaningful Deja Vu selection, though, so keep a
        // delayed fallback that still uses the exact same Dew.CreateSkillTrigger pickup path.
        //
        // This does not grant W/E directly to HeroSkill. It creates real world Memory pickups, so
        // ownership, UI refresh, level initialization and network synchronization remain native.
        yield return null;
        yield return new WaitForSeconds(1.25f);
        if (!NetworkServer.active || _hero == null || _nativeDejavuSpawnSeen) yield break;

        int wLevel = GetLevel(DariusConstellationIds.ICripplingStrike);
        int eLevel = GetLevel(DariusConstellationIds.IApprehend);
        if (wLevel <= 0 && eLevel <= 0) yield break;

        DewPlayer player = FindOwningPlayer();
        if (player == null)
        {
            // Player/hero binding can be a little later on a host transition. One retry is enough;
            // never spin every frame or create a permanent polling cost.
            yield return new WaitForSeconds(0.75f);
            player = FindOwningPlayer();
        }
        if (player == null || player.hero != _hero)
        {
            DariusLog.Warn("STAR-DEJAVU", "Fallback Memory-drop phase could not resolve the owning DewPlayer; W/E starter pickups were not spawned.");
            yield break;
        }

        _starterPlayer = player;
        _nativeDejavuSpawnSeen = true;
        DariusLog.Info("STAR-DEJAVU", "Native DoDejavuSpawn phase was not observed; using delayed DejaVu-style Memory pickup fallback. WLevel=" +
            wLevel + " ELevel=" + eLevel + " heroPos=" + DariusLog.Vec(_hero.agentPosition));
        ScheduleStarterDrops();
    }

    private DewPlayer FindOwningPlayer()
    {
        if (_hero == null) return null;
        try
        {
            if (DewPlayer.local != null && DewPlayer.local.hero == _hero) return DewPlayer.local;
        }
        catch { }
        try
        {
            DewPlayer[] players = UnityEngine.Object.FindObjectsByType<DewPlayer>(FindObjectsSortMode.None);
            for (int i = 0; i < players.Length; i++)
                if (players[i] != null && players[i].hero == _hero) return players[i];
        }
        catch { }
        return null;
    }

    private IEnumerator ApplySelectedLoadoutFallbackRoutine()
    {
        // Native StarEffect creation remains the primary path. This delayed reconciliation makes the
        // runtime robust if the game rebuilds HeroSkill constellation status effects during a scene
        // transition: the saved Darius loadout is authoritative and SetStar is idempotent.
        yield return null;
        yield return new WaitForSeconds(0.18f);
        if (!NetworkServer.active || _hero == null) yield break;

        // DewSave.profileMain and GetLocalPreferredGameSettings belong to this process' local player.
        // On a multiplayer host they must never be used to reconcile a remote player's hero, or the
        // host's saved constellation page can leak onto the guest. Remote heroes rely on the game's
        // normal networked StarEffect/loadout path instead.
        try
        {
            if (DewPlayer.local == null || DewPlayer.local.hero != _hero)
            {
                DariusLog.DebugInfo("MP-STAR", "Skipped local-profile constellation fallback for non-local Hero_Darius; native networked StarEffect state remains authoritative.");
                yield break;
            }
        }
        catch { yield break; }

        try
        {
            DewProfile profile = DewSave.profileMain;
            if (profile == null || profile.heroLoadouts == null) yield break;
            List<HeroLoadoutData> pages;
            if (!profile.heroLoadouts.TryGetValue(DariusTravelerRegistry.HeroName, out pages) || pages == null || pages.Count == 0) yield break;

            int page = 0;
            try
            {
                var settings = NetworkedManagerBase<GameSettingsManager>.instance != null
                    ? NetworkedManagerBase<GameSettingsManager>.instance.GetLocalPreferredGameSettings() : null;
                if (settings != null && settings.heroSelectedLoadoutIndex != null)
                {
                    int selected;
                    if (settings.heroSelectedLoadoutIndex.TryGetValue(DariusTravelerRegistry.HeroName, out selected))
                        page = Mathf.Clamp(selected, 0, pages.Count - 1);
                }
            }
            catch { page = 0; }

            HeroLoadoutData loadout = pages[Mathf.Clamp(page, 0, pages.Count - 1)];
            if (loadout == null) yield break;
            int applied = 0;
            applied += ApplySavedStars(loadout.cDestruction);
            applied += ApplySavedStars(loadout.cLife);
            applied += ApplySavedStars(loadout.cImagination);
            applied += ApplySavedStars(loadout.cFlexible);
            DariusLog.Info("STAR-RECONCILE", "Reconciled selected Hero_Darius loadout page=" + page + " activeDariusStars=" + applied);
        }
        catch (Exception e)
        {
            DariusLog.Exception("STAR-RECONCILE", e, "Could not reconcile selected Darius constellation loadout");
        }
    }

    private int ApplySavedStars(List<LoadoutStarItem> stars)
    {
        if (stars == null) return 0;
        int applied = 0;
        for (int i = 0; i < stars.Count; i++)
        {
            LoadoutStarItem item = stars[i];
            if (string.IsNullOrEmpty(item.name) || !DariusConstellationLocalization.IsDariusStarKey(item.name)) continue;
            SetStar(item.name, Mathf.Max(1, item.level), true);
            applied++;
        }
        return applied;
    }

    public void SetStar(string key, int level, bool active)
    {
        if (string.IsNullOrEmpty(key)) return;
        if (_hero == null) _hero = GetComponent<Hero>();
        if (active) _levels[key] = Mathf.Max(1, level);
        else _levels.Remove(key);
        // Equipment gameplay lives in a dedicated runtime but remains driven exclusively by the
        // same native StarEffect/Profile state as every other Darius constellation.
        if (DariusEquipmentConstellationLocalization.IsEquipmentKey(key))
            DariusEquipmentRuntime.SetStarForHero(_hero, key, Mathf.Max(1, level), active);
        RefreshPersistentBonus();
        RefreshLowHealthBonus(true);
        RefreshGatheringStorm(true);
        RefreshBloodRush(true);
        RefreshNoxianArena(true);
        // W/E unlock stars own their pickup timing. Schedule immediately and let the finite
        // room-ready routine wait for a real gameplay position; Deja Vu is only an optional
        // source of the owning DewPlayer reference, not a required lifecycle gate.
        if (active && (key == DariusConstellationIds.ICripplingStrike || key == DariusConstellationIds.IApprehend))
            ScheduleStarterDrops();
    }

    // Called from PlayGameManager.DoDejavuSpawn postfix. This is intentionally the same native
    // lifecycle phase that creates a normal Deja Vu Memory: the hero exists, the run reward-space
    // is initialized, and the owning DewPlayer is known. It avoids the old early-game (0,0,0) spawn.
    public static void NotifyNativeDejaVuSpawnPhase(DewPlayer player)
    {
        if (!NetworkServer.active || player == null) return;
        Hero_Darius hero = player.hero as Hero_Darius;
        if (hero == null) return;
        DariusConstellationRuntime runtime = hero.GetComponent<DariusConstellationRuntime>();
        if (runtime == null) runtime = hero.gameObject.AddComponent<DariusConstellationRuntime>();
        runtime.OnNativeDejaVuSpawnPhase(player);
    }

    private void OnNativeDejaVuSpawnPhase(DewPlayer player)
    {
        if (_hero == null) _hero = GetComponent<Hero>();
        if (_hero == null || player == null || player.hero != _hero) return;
        _starterPlayer = player;
        _nativeDejavuSpawnSeen = true;
        DariusLog.Info("STAR-DEJAVU", "Native Deja Vu spawn phase ready for Hero_Darius. WLevel=" +
            GetLevel(DariusConstellationIds.ICripplingStrike) + " ELevel=" + GetLevel(DariusConstellationIds.IApprehend) +
            " heroPos=" + DariusLog.Vec(_hero.agentPosition));
        ScheduleStarterDrops();
    }

    public int GetLevel(string key)
    {
        int value;
        return !string.IsNullOrEmpty(key) && _levels.TryGetValue(key, out value) ? value : 0;
    }

    public static int GetStarLevel(Hero hero, string key)
    {
        if (hero == null) return 0;
        DariusConstellationRuntime runtime = hero.GetComponent<DariusConstellationRuntime>();
        return runtime != null ? runtime.GetLevel(key) : 0;
    }

    public static float GetInstantQDamageMultiplier(Hero hero)
    {
        int level = GetStarLevel(hero, DariusConstellationIds.IInstantDecimate);
        if (level <= 0) return 1f;
        float[] values = { 1.12f, 1.16f, 1.20f, 1.24f };
        return values[Mathf.Clamp(level, 1, values.Length) - 1];
    }

    public static bool IsInstantQ(Hero hero) => GetStarLevel(hero, DariusConstellationIds.IInstantDecimate) > 0;

    public static float GetQHealMultiplier(Hero hero)
    {
        int level = GetStarLevel(hero, DariusConstellationIds.LRevitalize);
        if (level <= 0) return 1f;
        float[] values = { 1.20f, 1.25f, 1.30f, 1.35f };
        float result = values[Mathf.Clamp(level, 1, values.Length) - 1];
        try
        {
            if (hero != null && hero.Status != null && hero.Status.maxHealth > 0f && hero.currentHealth / hero.Status.maxHealth <= 0.40f)
                result += 0.06f;
        }
        catch { }
        return result;
    }

    public static float GetRDamageMultiplier(Hero hero)
    {
        float result = 1f;
        int d = GetStarLevel(hero, DariusConstellationIds.DAxiomArcanist);
        if (d > 0) { float[] values = { 1.10f, 1.13f, 1.16f, 1.20f }; result *= values[Mathf.Clamp(d, 1, values.Length) - 1]; }
        int legacy = GetStarLevel(hero, DariusConstellationIds.ILegacyGuillotine);
        if (legacy > 0) { float[] values = { 1.15f, 1.20f, 1.25f, 1.30f }; result *= values[Mathf.Clamp(legacy, 1, values.Length) - 1]; }
        return result;
    }

    public static float GetHemorrhageDurationBonus(Hero hero)
    {
        int level = GetStarLevel(hero, DariusConstellationIds.FHandOfNoxus);
        if (level <= 0) return 0f;
        float[] values = { 1.0f, 1.5f, 2.0f };
        return values[Mathf.Clamp(level, 1, values.Length) - 1];
    }

    public static float GetNoxianMightAdBonus(Hero hero)
    {
        int level = GetStarLevel(hero, DariusConstellationIds.DNoxianMight);
        if (level <= 0) return 0f;
        float[] values = { 9f, 12f, 15f, 18f };
        return values[Mathf.Clamp(level, 1, values.Length) - 1];
    }

    public static float GetLegacyWSlowDurationBonus(Hero hero)
    {
        int level = GetStarLevel(hero, DariusConstellationIds.ILegacyCripplingStrike);
        if (level <= 0) return 0f;
        float[] values = { 0.25f, 0.35f, 0.45f, 0.55f };
        return values[Mathf.Clamp(level, 1, values.Length) - 1];
    }

    public static float GetLegacyWRefundPerHemorrhageStack(Hero hero)
    {
        int level = GetStarLevel(hero, DariusConstellationIds.ILegacyCripplingStrike);
        if (level <= 0) return 0f;
        float[] values = { 0.50f, 0.60f, 0.70f, 0.80f };
        return values[Mathf.Clamp(level, 1, values.Length) - 1];
    }

    public static void ApplyLegacyApprehendMark(Hero hero, Entity target)
    {
        if (!NetworkServer.active || hero == null || target == null) return;
        int level = GetStarLevel(hero, DariusConstellationIds.ILegacyApprehend);
        if (level <= 0) return;
        DariusLegacyApprehendMark mark = target.GetComponent<DariusLegacyApprehendMark>();
        if (mark == null) mark = target.gameObject.AddComponent<DariusLegacyApprehendMark>();
        mark.Activate(hero, 3f);
    }

    public static float GetLegacyApprehendPhysicalMultiplier(Hero hero, Entity target)
    {
        if (hero == null || target == null) return 1f;
        int level = GetStarLevel(hero, DariusConstellationIds.ILegacyApprehend);
        if (level <= 0) return 1f;
        DariusLegacyApprehendMark mark = target.GetComponent<DariusLegacyApprehendMark>();
        if (mark == null || !mark.IsActiveFor(hero)) return 1f;
        float[] values = { 1.10f, 1.13f, 1.16f, 1.20f };
        return values[Mathf.Clamp(level, 1, values.Length) - 1];
    }

    public static float GetNoxianMightDurationBonus(Hero hero)
    {
        int level = GetStarLevel(hero, DariusConstellationIds.DAxiomArcanist);
        if (level <= 0) return 0f;
        float[] values = { 1.0f, 1.25f, 1.5f, 2.0f };
        return values[Mathf.Clamp(level, 1, values.Length) - 1];
    }

    public static int GetWarFervorLevel(Hero hero) => GetStarLevel(hero, DariusConstellationIds.IWarFervor);

    public static float GetWarFervorWindow(Hero hero)
    {
        int level = GetWarFervorLevel(hero);
        if (level <= 0) return 0f;
        float[] windows = { 5f, 5.5f, 6f, 6.5f };
        return windows[Mathf.Clamp(level, 1, windows.Length) - 1];
    }

    public static void NotifyDamageHit(Hero hero, Entity target, string source)
    {
        if (!NetworkServer.active || hero == null || target == null) return;
        DariusConstellationRuntime runtime = hero.GetComponent<DariusConstellationRuntime>();
        if (runtime != null) runtime.OnDamageHit(source);
    }

    public static void NotifyKill(Hero hero, Entity victim, string source)
    {
        if (!NetworkServer.active || hero == null || victim == null) return;
        DariusConstellationRuntime runtime = hero.GetComponent<DariusConstellationRuntime>();
        if (runtime != null) runtime.OnKill(victim, source);
        DariusEquipmentRuntime.NotifyKill(hero, victim, source);
        DariusVoiceRuntime.NotifyKill(hero, victim);
    }

    public static void NotifyMovementSpell(Hero hero, AbilityTrigger trigger, string spell)
    {
        if (!NetworkServer.active || hero == null) return;
        DariusConstellationRuntime runtime = hero.GetComponent<DariusConstellationRuntime>();
        if (runtime != null) runtime.OnMovementSpell(trigger, spell);
    }

    private void Update()
    {
        if (!NetworkServer.active || _hero == null || _hero.Status == null) return;
        float hp = _hero.currentHealth;
        if (_lastHealth < 0f) _lastHealth = hp;
        if (hp + 0.01f < _lastHealth) OnOwnerDamaged(_lastHealth - hp);
        _lastHealth = hp;

        if (_conquerorStacks > 0 && Time.time >= _conquerorExpiresAt)
        {
            _conquerorStacks = 0;
            RefreshConquerorBonus();
            DariusLog.DebugInfo("STAR-CONQUEROR", "Stacks expired.");
        }

        RefreshLowHealthBonus(false);
        RefreshGatheringStorm(false);
        if (Time.time >= _nextBloodRushRefresh) { _nextBloodRushRefresh = Time.time + 0.20f; RefreshBloodRush(false); }
        if (Time.time >= _nextNoxianArenaRefresh) { _nextNoxianArenaRefresh = Time.time + 0.25f; RefreshNoxianArena(false); }
        TickSecondWind();
    }

    private void OnDamageHit(string source)
    {
        int level = GetLevel(DariusConstellationIds.DConqueror);
        if (level <= 0) return;
        _conquerorStacks = Mathf.Clamp(_conquerorStacks + 1, 0, 6);
        _conquerorExpiresAt = Time.time + 5f;
        RefreshConquerorBonus();
        DariusLog.DebugInfo("STAR-CONQUEROR", "source=" + source + " stacks=" + _conquerorStacks + "/6 level=" + level);
    }

    private void OnKill(Entity victim, string source)
    {
        int id;
        try { id = victim.GetInstanceID(); } catch { return; }
        float seen;
        if (_recentKills.TryGetValue(id, out seen) && Time.time - seen < 1.5f) return;
        _recentKills[id] = Time.time;
        _killCount++;

        int triumph = GetLevel(DariusConstellationIds.DTriumph);
        if (triumph > 0 && _hero != null && _hero.Status != null)
        {
            float[] fractions = { 0.06f, 0.07f, 0.08f, 0.09f };
            float missing = Mathf.Max(0f, _hero.Status.maxHealth - _hero.currentHealth);
            float amount = missing * fractions[Mathf.Clamp(triumph, 1, fractions.Length) - 1];
            if (amount > 0.01f)
            {
                HealData heal = new HealData(amount);
                heal.SetActor(_hero);
                _hero.DoHeal(heal, _hero);
                DariusLog.Info("STAR-TRIUMPH", "source=" + source + " healed=" + amount.ToString("0.##") + " missing=" + missing.ToString("0.##") + " level=" + triumph);
            }
        }

        int alacrity = GetLevel(DariusConstellationIds.DAlacrity);
        if (alacrity > 0)
        {
            int[] perStack = { 6, 5, 4, 3 };
            int needed = perStack[Mathf.Clamp(alacrity, 1, perStack.Length) - 1];
            int desired = Mathf.Clamp(_killCount / needed, 0, 5);
            if (desired != _alacrityStacks)
            {
                _alacrityStacks = desired;
                RefreshAlacrityBonus();
                DariusLog.Info("STAR-ALACRITY", "kills=" + _killCount + " stacks=" + _alacrityStacks + "/5 level=" + alacrity);
            }
        }

        int bloodPrice = GetLevel(DariusConstellationIds.LBloodPrice);
        if (bloodPrice > 0 && _hero != null && _hero.Status != null)
        {
            DariusHemorrhageRuntime hem = _hero.GetComponent<DariusHemorrhageRuntime>();
            int stacks = hem != null ? hem.GetStacks(victim) : 0;
            if (stacks > 0)
            {
                float[] fractions = { 0.025f, 0.035f, 0.045f, 0.055f };
                float amount = _hero.Status.maxHealth * fractions[Mathf.Clamp(bloodPrice,1,fractions.Length)-1];
                HealData heal = new HealData(amount); heal.SetActor(_hero); _hero.DoHeal(heal, _hero);
                DariusLog.Info("STAR-BLOOD-PRICE", "source=" + source + " stacks=" + stacks + " heal=" + amount.ToString("0.##"));
            }
        }

        int dunk = GetLevel(DariusConstellationIds.DDunkmaster);
        if (dunk > 0 && !string.IsNullOrEmpty(source) && source.IndexOf("R execute", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            if (_dunkmasterRoutine != null) StopCoroutine(_dunkmasterRoutine);
            _dunkmasterRoutine = StartCoroutine(DunkmasterRoutine(dunk));
        }
    }

    private void RefreshBloodRush(bool force)
    {
        int level = GetLevel(DariusConstellationIds.DBloodRush);
        int count = 0;
        if (level > 0 && _hero != null)
        {
            DariusHemorrhageRuntime hem = _hero.GetComponent<DariusHemorrhageRuntime>();
            if (hem != null) count = Mathf.Clamp(hem.GetActiveBleedingTargetCount(), 0, 5);
        }
        if (!force && count == _lastBloodRushCount) return;
        _lastBloodRushCount = count;
        RemoveBonus(ref _bloodRushBonus);
        if (level <= 0 || count <= 0 || _hero == null || _hero.Status == null) return;
        float[] perTarget = { 3f, 3.5f, 4f, 4.5f };
        _bloodRushBonus = new StatBonus();
        _bloodRushBonus.movementSpeedPercentage = perTarget[Mathf.Clamp(level,1,perTarget.Length)-1] * count;
        _hero.Status.AddStatBonus(_bloodRushBonus);
        DariusLog.DebugInfo("STAR-BLOOD-RUSH", "bleedingTargets=" + count + " move%=" + _bloodRushBonus.movementSpeedPercentage);
    }


    // Noxian Arena is intentionally a boss-counter constellation rather than a generic stat stick.
    // It remains modest while clearing packs, then ramps sharply as Darius is isolated with a
    // high-value target. Boss rank overrides Elite rank; enemy count still includes boss adds.
    private void RefreshNoxianArena(bool force)
    {
        int level = GetLevel(DariusConstellationIds.DNoxianArena);
        if (level <= 0 || _hero == null || _hero.Status == null)
        {
            RemoveBonus(ref _noxianArenaBonus);
            _lastNoxianArenaCount = -1;
            _lastNoxianArenaLevel = level;
            _lastNoxianArenaRank = DariusEnemyClass.Common;
            return;
        }

        const float radius = 10f;
        int rawCount = 0;
        DariusEnemyClass strongest = DariusEnemyClass.Common;
        HashSet<Entity> seen = new HashSet<Entity>();
        try
        {
            Collider[] hits = Physics.OverlapSphere(_hero.transform.position, radius);
            for (int i = 0; i < hits.Length; i++)
            {
                Collider hit = hits[i];
                if (hit == null) continue;
                Entity enemy = hit.GetComponentInParent<Entity>();
                if (enemy == null || enemy == _hero || !seen.Add(enemy) || enemy.IsNullInactiveDeadOrKnockedOut()) continue;
                bool hostile = false;
                try { hostile = _hero.GetRelation(enemy).HasFlag(EntityRelation.Enemy); } catch { }
                if (!hostile) continue;

                rawCount++;
                if (strongest != DariusEnemyClass.Boss)
                {
                    DariusEnemyClass kind = DariusEnemyClassifier.Classify(enemy);
                    if (kind == DariusEnemyClass.Boss) strongest = DariusEnemyClass.Boss;
                    else if (kind == DariusEnemyClass.Elite && strongest == DariusEnemyClass.Common) strongest = DariusEnemyClass.Elite;
                }
            }
        }
        catch (Exception e)
        {
            DariusLog.Exception("STAR-NOXIAN-ARENA", e, "Nearby-enemy scan failed");
        }

        int count = Mathf.Clamp(rawCount, 0, 10);
        if (!force && count == _lastNoxianArenaCount && level == _lastNoxianArenaLevel && strongest == _lastNoxianArenaRank) return;
        _lastNoxianArenaCount = count;
        _lastNoxianArenaLevel = level;
        _lastNoxianArenaRank = strongest;
        RemoveBonus(ref _noxianArenaBonus);
        if (count <= 0) return;

        // 10 enemies = x1.00; each missing enemy adds 10%. Elite/Boss rank multiplies
        // that scarcity bonus. Boss encounters cap at x3 so adds can never make the bonus
        // stronger than the explicitly requested solo-Boss result.
        float scarcityMultiplier = 1f + (10 - count) * 0.10f;
        float rankMultiplier = strongest == DariusEnemyClass.Boss ? 2f : strongest == DariusEnemyClass.Elite ? 1.5f : 1f;
        float multiplier = scarcityMultiplier * rankMultiplier;
        if (strongest == DariusEnemyClass.Boss) multiplier = Mathf.Min(3f, multiplier);

        float[] attackDamage = { 3f, 4f, 5f, 6f };
        float[] attackSpeed = { 8f, 10f, 12f, 15f };
        float[] moveSpeed = { 3f, 4f, 5f, 6f };
        float[] armor = { 0.5f, 0.75f, 1f, 1.25f };
        int index = Mathf.Clamp(level, 1, 4) - 1;

        _noxianArenaBonus = new StatBonus();
        _noxianArenaBonus.attackDamageFlat = attackDamage[index] * multiplier;
        _noxianArenaBonus.movementSpeedPercentage = moveSpeed[index] * multiplier;
        bool attackSpeedSet = DariusConstellationReflection.SetFloat(_noxianArenaBonus, attackSpeed[index] * multiplier, "attackSpeedPercentage", "attackSpeedPercent");
        _noxianArenaBonus.armorFlat = armor[index] * multiplier;
        _hero.Status.AddStatBonus(_noxianArenaBonus);

        DariusLog.Info("STAR-NOXIAN-ARENA", "level=" + level + " enemies=" + rawCount + " counted=" + count +
            " rank=" + strongest + " scarcityX=" + scarcityMultiplier.ToString("0.##") + " rankX=" + rankMultiplier.ToString("0.##") +
            " totalX=" + multiplier.ToString("0.##") + " flatAD=" + _noxianArenaBonus.attackDamageFlat.ToString("0.#") +
            " MS%=" + _noxianArenaBonus.movementSpeedPercentage.ToString("0.#") + " ASField=" + attackSpeedSet +
            " flatArmor=" + _noxianArenaBonus.armorFlat.ToString("0.##"));
    }

    private IEnumerator DunkmasterRoutine(int level)
    {
        RemoveBonus(ref _dunkmasterBonus);
        if (_hero != null && _hero.Status != null)
        {
            float[] values = { 15f, 20f, 25f, 30f };
            _dunkmasterBonus = new StatBonus();
            _dunkmasterBonus.movementSpeedPercentage = values[Mathf.Clamp(level,1,values.Length)-1];
            _hero.Status.AddStatBonus(_dunkmasterBonus);
            DariusLog.Info("STAR-DUNKMASTER", "R execute granted +" + _dunkmasterBonus.movementSpeedPercentage + "% move speed for 3s.");
        }
        yield return new WaitForSeconds(3f);
        RemoveBonus(ref _dunkmasterBonus);
        _dunkmasterRoutine = null;
    }

    private void OnOwnerDamaged(float damage)
    {
        int level = GetLevel(DariusConstellationIds.LSecondWind);
        if (level <= 0 || _hero == null || _hero.Status == null) return;
        float[] fractions = { 0.04f, 0.05f, 0.06f, 0.07f };
        float missing = Mathf.Max(0f, _hero.Status.maxHealth - _hero.currentHealth);
        float pool = missing * fractions[Mathf.Clamp(level, 1, fractions.Length) - 1];
        _secondWindPool = Mathf.Max(_secondWindPool, pool);
        _nextSecondWindTick = Mathf.Min(_nextSecondWindTick <= 0f ? Time.time : _nextSecondWindTick, Time.time + 0.25f);
        DariusLog.DebugInfo("STAR-SECOND-WIND", "damage=" + damage.ToString("0.##") + " regenPool=" + _secondWindPool.ToString("0.##") + " level=" + level);
    }

    private void TickSecondWind()
    {
        if (_secondWindPool <= 0.01f || Time.time < _nextSecondWindTick || _hero == null || _hero.Status == null) return;
        float tick = Mathf.Min(_secondWindPool, Mathf.Max(0.25f, _secondWindPool * 0.25f));
        float missing = Mathf.Max(0f, _hero.Status.maxHealth - _hero.currentHealth);
        tick = Mathf.Min(tick, missing);
        if (tick > 0.01f)
        {
            HealData heal = new HealData(tick);
            heal.SetActor(_hero);
            _hero.DoHeal(heal, _hero);
        }
        _secondWindPool = Mathf.Max(0f, _secondWindPool - tick);
        _nextSecondWindTick = Time.time + 1f;
    }

    private void RefreshPersistentBonus()
    {
        RemoveBonus(ref _persistentBonus);
        if (_hero == null || _hero.Status == null) return;
        StatBonus bonus = new StatBonus();
        bool used = false;

        int overgrowth = GetLevel(DariusConstellationIds.LOvergrowth);
        if (overgrowth > 0)
        {
            float[] values = { 25f, 32f, 38f, 45f };
            bonus.maxHealthFlat = values[Mathf.Clamp(overgrowth, 1, values.Length) - 1];
            used = true;
        }

        int conditioning = GetLevel(DariusConstellationIds.LConditioning);
        if (conditioning > 0)
        {
            float[] values = { 0.5f, 0.75f, 1f, 1.25f };
            bonus.armorFlat = values[Mathf.Clamp(conditioning, 1, values.Length) - 1];
            used = true;
        }

        int celerity = GetLevel(DariusConstellationIds.FCelerity);
        if (celerity > 0)
        {
            float[] values = { 5f, 6.5f, 8f };
            bonus.movementSpeedPercentage = values[Mathf.Clamp(celerity, 1, values.Length) - 1];
            used = true;
        }

        if (used)
        {
            _persistentBonus = bonus;
            _hero.Status.AddStatBonus(_persistentBonus);
        }
    }

    private void RefreshConquerorBonus()
    {
        RemoveBonus(ref _conquerorBonus);
        int level = GetLevel(DariusConstellationIds.DConqueror);
        if (level <= 0 || _conquerorStacks <= 0 || _hero == null || _hero.Status == null) return;
        float[] perStack = { 1.5f, 2f, 2.5f, 3f };
        _conquerorBonus = new StatBonus();
        _conquerorBonus.attackDamageFlat = perStack[Mathf.Clamp(level, 1, perStack.Length) - 1] * _conquerorStacks;
        _hero.Status.AddStatBonus(_conquerorBonus);
    }

    private void RefreshAlacrityBonus()
    {
        RemoveBonus(ref _alacrityBonus);
        if (_alacrityStacks <= 0 || _hero == null || _hero.Status == null) return;
        _alacrityBonus = new StatBonus();
        bool set = DariusConstellationReflection.SetFloat(_alacrityBonus, 4f * _alacrityStacks, "attackSpeedPercentage", "attackSpeedPercent");
        if (set) _hero.Status.AddStatBonus(_alacrityBonus);
        else _alacrityBonus = null;
    }

    private void RefreshLowHealthBonus(bool force)
    {
        if (_hero == null || _hero.Status == null || _hero.Status.maxHealth <= 0f) return;
        bool low = _hero.currentHealth / _hero.Status.maxHealth <= 0.40f;
        if (!force && low == _lastLowHealth) return;
        _lastLowHealth = low;
        RemoveBonus(ref _lowHealthBonus);
        if (!low) return;

        int lastStand = GetLevel(DariusConstellationIds.DLastStand);
        int unflinching = GetLevel(DariusConstellationIds.LUnflinching);
        if (lastStand <= 0 && unflinching <= 0) return;
        _lowHealthBonus = new StatBonus();
        bool used = false;
        if (lastStand > 0)
        {
            float[] values = { 9f, 11f, 13f, 15f };
            _lowHealthBonus.attackDamageFlat = values[Mathf.Clamp(lastStand, 1, values.Length) - 1];
            used = true;
        }
        if (unflinching > 0)
        {
            float[] values = { 8f, 10f, 12f, 14f };
            _lowHealthBonus.movementSpeedPercentage = values[Mathf.Clamp(unflinching, 1, values.Length) - 1];
            used = true;
        }
        if (used) _hero.Status.AddStatBonus(_lowHealthBonus);
    }

    private void RefreshGatheringStorm(bool force)
    {
        int level = GetLevel(DariusConstellationIds.FGatheringStorm);
        if (level <= 0 || _hero == null || _hero.Status == null)
        {
            if (_gatheringBonus != null) RemoveBonus(ref _gatheringBonus);
            _lastGatheringStep = -1;
            return;
        }
        int heroLevel = DariusConstellationReflection.ReadEntityLevel(_hero);
        int step = Mathf.Max(0, heroLevel / 5);
        if (!force && step == _lastGatheringStep) return;
        _lastGatheringStep = step;
        RemoveBonus(ref _gatheringBonus);
        if (step <= 0) return;
        float[] perStep = { 2.5f, 3f, 3.5f };
        _gatheringBonus = new StatBonus();
        _gatheringBonus.attackDamageFlat = perStep[Mathf.Clamp(level, 1, perStep.Length) - 1] * step;
        _hero.Status.AddStatBonus(_gatheringBonus);
        DariusLog.Info("STAR-GATHERING", "heroLevel=" + heroLevel + " steps=" + step + " flatAD=" + _gatheringBonus.attackDamageFlat);
    }

    private void ScheduleStarterDrops()
    {
        if (_starterRoutine != null) StopCoroutine(_starterRoutine);
        _starterRoutine = StartCoroutine(StarterDropRoutine());
    }

    private IEnumerator StarterDropRoutine()
    {
        // DoDejavuSpawn / Hero.Start can run before the networked hero has reached its actual map
        // spawn. rc6 proved that a fixed delay is not enough: W/E were created at (0,0,0) and never
        // became usable world Memories. Wait for a real, active hero position instead.
        yield return null;
        yield return new WaitForSeconds(0.12f);
        if (!NetworkServer.active || _hero == null)
        {
            _starterRoutine = null;
            yield break;
        }

        // _starterPlayer is only a cached fast path. SpawnConstellationW/E can resolve the
        // owning player from the Hero if the Deja Vu phase has not supplied one yet.
        DewPlayer player = _starterPlayer;
        if (player != null && player.hero != _hero) player = null;

        float readyDeadline = Time.unscaledTime + 20f;
        bool worldReady = false;
        while (Time.unscaledTime < readyDeadline)
        {
            if (!NetworkServer.active || _hero == null)
            {
                _starterRoutine = null;
                yield break;
            }

            Vector3 agent = _hero.agentPosition;
            Vector3 transformPos = _hero.transform.position;
            bool finite = IsFiniteWorldPosition(agent) && IsFiniteWorldPosition(transformPos);
            bool realRoomScene = IsGameplayRoomScene(SceneManager.GetActiveScene().name);
            bool nonStagingPosition = IsUsableStarterWorldPosition(agent) && IsUsableStarterWorldPosition(transformPos);
            if (_hero.gameObject.activeInHierarchy && finite && realRoomScene && nonStagingPosition)
            {
                worldReady = true;
                break;
            }
            yield return new WaitForSecondsRealtime(0.10f);
        }

        if (!worldReady || _hero == null || !_hero.gameObject.activeInHierarchy)
        {
            string sceneName = SceneManager.GetActiveScene().name;
            DariusLog.Warn("STAR-DEJAVU", "Hero never reached a usable gameplay-room position; starter Memories were not spawned. scene=" +
                sceneName + " heroPos=" + (_hero != null ? DariusLog.Vec(_hero.agentPosition) : "<null>"));
            _starterRoutine = null;
            yield break;
        }
        DariusLog.Info("STAR-DEJAVU", "Starter-memory world state ready. scene=" + SceneManager.GetActiveScene().name +
            " heroPos=" + DariusLog.Vec(_hero.agentPosition));

        int wLevel = GetLevel(DariusConstellationIds.ICripplingStrike);
        if (wLevel > 0 && !_spawnedW)
        {
            _spawnedW = DariusFormalRegistry.SpawnConstellationW(_hero, wLevel, player);
            DariusLog.Info("STAR-STARTER", "W starter DejaVu-style spawn requested level=" + wLevel + " success=" + _spawnedW);
        }
        int eLevel = GetLevel(DariusConstellationIds.IApprehend);
        if (eLevel > 0 && !_spawnedE)
        {
            _spawnedE = DariusFormalRegistry.SpawnConstellationE(_hero, eLevel, player);
            DariusLog.Info("STAR-STARTER", "E starter DejaVu-style spawn requested level=" + eLevel + " success=" + _spawnedE);
        }
        _starterRoutine = null;
    }

    internal static bool IsUsableStarterWorldPosition(Vector3 pos)
    {
        if (!IsFiniteWorldPosition(pos)) return false;
        // Shape of Dreams parks the networked hero at (-5000,-5000,0) while PlayGame is
        // transitioning into the first Room_* scene. rc7 mistook that non-zero sentinel for a
        // valid location and spawned both starter Memories outside the playable world.
        if (Mathf.Abs(pos.x + 5000f) < 250f && Mathf.Abs(pos.y + 5000f) < 250f) return false;
        // Real gameplay navigation is close to ground level; this also rejects other far-off
        // staging/parking coordinates without imposing an arbitrary X/Z world-size limit.
        if (Mathf.Abs(pos.y) > 1000f) return false;
        return true;
    }

    private static bool IsFiniteWorldPosition(Vector3 pos)
    {
        return !float.IsNaN(pos.x) && !float.IsNaN(pos.y) && !float.IsNaN(pos.z) &&
               !float.IsInfinity(pos.x) && !float.IsInfinity(pos.y) && !float.IsInfinity(pos.z);
    }

    private static bool IsGameplayRoomScene(string sceneName)
    {
        return !string.IsNullOrEmpty(sceneName) && sceneName.StartsWith("Room_", StringComparison.OrdinalIgnoreCase);
    }

    private void OnMovementSpell(AbilityTrigger trigger, string spell)
    {
        int nimbus = GetLevel(DariusConstellationIds.FNimbusCloak);
        if (nimbus > 0)
        {
            if (_nimbusRoutine != null) StopCoroutine(_nimbusRoutine);
            _nimbusRoutine = StartCoroutine(NimbusRoutine(nimbus, spell));
        }

        int cosmic = GetLevel(DariusConstellationIds.FCosmicInsight);
        if (cosmic > 0 && trigger != null)
        {
            int id = trigger.GetInstanceID();
            Coroutine existing;
            if (_cosmicRoutines.TryGetValue(id, out existing) && existing != null) StopCoroutine(existing);
            _cosmicRoutines[id] = StartCoroutine(CosmicRoutine(trigger, cosmic, spell));
        }
    }

    private IEnumerator NimbusRoutine(int level, string spell)
    {
        RemoveBonus(ref _nimbusBonus);
        if (_hero != null && _hero.Status != null)
        {
            float[] values = { 15f, 20f, 25f };
            _nimbusBonus = new StatBonus();
            _nimbusBonus.movementSpeedPercentage = values[Mathf.Clamp(level, 1, values.Length) - 1];
            _hero.Status.AddStatBonus(_nimbusBonus);
            DariusLog.Info("STAR-NIMBUS", spell + " granted +" + _nimbusBonus.movementSpeedPercentage + "% move speed for 2s.");
        }
        yield return new WaitForSeconds(2f);
        RemoveBonus(ref _nimbusBonus);
        _nimbusRoutine = null;
    }

    private IEnumerator CosmicRoutine(AbilityTrigger trigger, int level, string spell)
    {
        float[] reductions = { 0.20f, 0.25f, 0.30f };
        float reduction = reductions[Mathf.Clamp(level, 1, reductions.Length) - 1];
        float wait = Mathf.Max(0.1f, DariusSummonerBalance.Cooldown * (1f - reduction));
        yield return new WaitForSeconds(wait);
        if (_hero != null && trigger != null)
        {
            try
            {
                _hero.ResetCooldown(trigger, false);
                DariusLog.Info("STAR-COSMIC", spell + " cooldown accelerated by " + (reduction * 100f).ToString("0") + "% after " + wait.ToString("0.##") + "s.");
            }
            catch (Exception e) { DariusLog.Exception("STAR-COSMIC", e, spell + " cooldown acceleration failed"); }
        }
        if (trigger != null) _cosmicRoutines.Remove(trigger.GetInstanceID());
    }

    private void RemoveBonus(ref StatBonus bonus)
    {
        if (bonus != null && _hero != null && _hero.Status != null)
        {
            try { _hero.Status.RemoveStatBonus(bonus); } catch { }
        }
        bonus = null;
    }

    private void OnDestroy()
    {
        RemoveBonus(ref _persistentBonus);
        RemoveBonus(ref _conquerorBonus);
        RemoveBonus(ref _lowHealthBonus);
        RemoveBonus(ref _alacrityBonus);
        RemoveBonus(ref _gatheringBonus);
        RemoveBonus(ref _nimbusBonus);
        RemoveBonus(ref _bloodRushBonus);
        RemoveBonus(ref _dunkmasterBonus);
        RemoveBonus(ref _noxianArenaBonus);
    }
}

public sealed class DariusLegacyApprehendMark : MonoBehaviour
{
    private Hero _owner;
    private float _until;
    public void Activate(Hero owner, float duration) { _owner = owner; _until = Time.time + Mathf.Max(0.1f, duration); }
    public bool IsActiveFor(Hero owner) { return owner != null && _owner == owner && Time.time < _until; }
}

public static class DariusConstellationLocalization
{
    private static readonly Dictionary<string, string> Names = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        { DariusConstellationIds.DConqueror, "征服者" },
        { DariusConstellationIds.DTriumph, "凯旋" },
        { DariusConstellationIds.DAlacrity, "传说：欢欣" },
        { DariusConstellationIds.DLastStand, "坚毅不倒" },
        { DariusConstellationIds.DAxiomArcanist, "公理秘术" },
        { DariusConstellationIds.DNoxianArena, "诺克萨斯竞技场" },
        { DariusConstellationIds.LSecondWind, "复苏之风" },
        { DariusConstellationIds.LOvergrowth, "过度生长" },
        { DariusConstellationIds.LRevitalize, "复苏" },
        { DariusConstellationIds.LConditioning, "调节" },
        { DariusConstellationIds.LUnflinching, "坚定" },
        { DariusConstellationIds.ICripplingStrike, "诺克萨斯武库：致残打击" },
        { DariusConstellationIds.IApprehend, "诺克萨斯武库：无情铁手" },
        { DariusConstellationIds.IInstantDecimate, "旋斧即决" },
        { DariusConstellationIds.IWarFervor, "战争热诚" },
        { DariusConstellationIds.ILegacyCripplingStrike, "斩断退路" },
        { DariusConstellationIds.ILegacyApprehend, "铁腕征服" },
        { DariusConstellationIds.ILegacyGuillotine, "断头台的律法" },
        { DariusConstellationIds.DBloodRush, "血路疾行" },
        { DariusConstellationIds.DNoxianMight, "真正的诺克萨斯之力" },
        { DariusConstellationIds.DDunkmaster, "扣篮王" },
        { DariusConstellationIds.LBloodPrice, "血债血偿" },
        { DariusConstellationIds.FHandOfNoxus, "诺克萨斯之手" },
        { DariusConstellationIds.FNimbusCloak, "灵光披风" },
        { DariusConstellationIds.FCelerity, "迅捷" },
        { DariusConstellationIds.FGatheringStorm, "风暴聚集" },
        { DariusConstellationIds.FCosmicInsight, "星界洞悉" }
    };

    private static readonly Dictionary<string, string> Descriptions = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        { DariusConstellationIds.DConqueror, "对敌人造成直接命中会获得1层征服者，持续5秒，最多6层。每层固定提高1.5/2/2.5/3攻击力。" },
        { DariusConstellationIds.DTriumph, "击杀敌人时回复已损失生命值的6%/7%/8%/9%。同一次击杀只会结算一次。" },
        { DariusConstellationIds.DAlacrity, "每6/5/4/3次击杀获得1层欢欣，每层提高4%攻击速度，最多5层，本局持续。" },
        { DariusConstellationIds.DLastStand, "生命值低于40%时，固定提高9/11/13/15攻击力。" },
        { DariusConstellationIds.DAxiomArcanist, "诺克萨斯断头台伤害提高10%/13%/16%/20%，并使诺克萨斯之力持续时间延长1/1.25/1.5/2秒。" },
        { DariusConstellationIds.DNoxianArena, "10米内至少存在1个敌人时进入【诺克萨斯竞技场】：基础固定获得3/4/5/6攻击力与0.5/0.75/1/1.25护甲，并获得8%/10%/12%/15%攻击速度和3%/4%/5%/6%移动速度。附近敌人最多按10个计算，每少1个敌人，全部加成提高10%；存在精英时总效果×1.5，存在Boss时总效果×2（Boss优先），Boss场景最终倍率封顶×3，单独面对Boss时即为×3。" },
        { DariusConstellationIds.LSecondWind, "受到伤害后逐步回复当前已损失生命值的4%/5%/6%/7%，新的受击会刷新可回复量。" },
        { DariusConstellationIds.LOvergrowth, "固定提高25/32/38/45最大生命值。" },
        { DariusConstellationIds.LRevitalize, "大杀四方的治疗量提高12%/16%/20%/24%；生命值低于40%时额外提高6%。" },
        { DariusConstellationIds.LConditioning, "固定提高0.5/0.75/1/1.25护甲。" },
        { DariusConstellationIds.LUnflinching, "生命值低于40%时，移动速度提高8%/10%/12%/14%。" },
        { DariusConstellationIds.ICripplingStrike, "诺克萨斯从不教人给敌人第二次逃跑的机会。开战时，德莱厄斯附近出现1个【致残打击】记忆；记忆等级等于本星座等级。" },
        { DariusConstellationIds.IApprehend, "真正的强者不会等猎物自己走近。开战时，德莱厄斯附近出现1个【无情铁手】记忆；记忆等级等于本星座等级。" },
        { DariusConstellationIds.IInstantDecimate, "斧刃不必蓄势，杀意已经足够。Q【大杀四方】取消0.75秒前摇、蓄力动作与治疗，直接播放旋转攻击并立即结算；伤害提高20%/25%/30%/35%，所有命中均按外圈伤害处理；仅在已拥有【出血】身份时必定叠加1层【出血】。" },
        { DariusConstellationIds.IWarFervor, "战争不在乎鲜血来自谁。只要在5/5.5/6/6.5秒内跨目标累计施加5次【出血】，即可触发【诺克萨斯之力】，获得35%攻击力，并继续享受【出血】记忆每5级带来的成长。" },
        { DariusConstellationIds.ILegacyCripplingStrike, "逃跑只是把死亡拖得更久。W【致残打击】的减速额外延长0.25/0.35/0.45/0.55秒；命中时，目标每有1层【出血】，W剩余冷却额外减少0.5/0.6/0.7/0.8秒。" },
        { DariusConstellationIds.ILegacyApprehend, "被铁腕拖回来的敌人，连铠甲也失去了意义。E【无情铁手】拉中的敌人被标记3秒；期间德莱厄斯的技能与【出血】造成的物理伤害提高10%/13%/16%/20%。" },
        { DariusConstellationIds.ILegacyGuillotine, "断头台只承认力量，不承认侥幸。R【诺克萨斯断头台】伤害额外提高15%/20%/25%/30%，并与【公理秘术】乘算。" },
        { DariusConstellationIds.DBloodRush, "鲜血会替你指出逃兵的方向。每个正在流血的敌人使移动速度提高3%/3.5%/4%/4.5%，最多计算5个目标。" },
        { DariusConstellationIds.DNoxianMight, "君王以世袭的名义让你跪下，而诺克萨斯让你站起来。触发【诺克萨斯之力】时，额外固定获得9/12/15/18攻击力。" },
        { DariusConstellationIds.DDunkmaster, "高高跃起，然后让所有人记住这一斧。用【诺克萨斯断头台】完成击杀后，移动速度提高15%/20%/25%/30%，持续3秒。" },
        { DariusConstellationIds.LBloodPrice, "流下的血总要有人偿还。击杀仍带有【出血】的敌人时，回复最大生命值的2.5%/3.5%/4.5%/5.5%。" },
        { DariusConstellationIds.FHandOfNoxus, "伤口不会轻易闭合，德莱厄斯也不会停止追猎。【出血】持续时间延长1/1.5/2秒。" },
        { DariusConstellationIds.FNimbusCloak, "施放闪现或疾跑后，移动速度提高15%/20%/25%，持续2秒。" },
        { DariusConstellationIds.FCelerity, "移动速度提高5%/6.5%/8%。" },
        { DariusConstellationIds.FGatheringStorm, "每达到5个局内英雄等级获得一层风暴聚集；每层固定提高2.5/3/3.5攻击力。" },
        { DariusConstellationIds.FCosmicInsight, "闪现与疾跑的实际恢复时间缩短20%/25%/30%。" }
    };

    public static bool TryName(string key, out string value)
    {
        value = null;
        string id = Normalize(key);
        if (id != null) return DariusLanguage.IsEnglish
            ? DariusEnglishLocalization.TryStarName(id, out value)
            : Names.TryGetValue(id, out value);
        return DariusEquipmentConstellationLocalization.TryName(key, out value);
    }

    public static bool TryDescription(string key, out string value)
    {
        value = null;
        string id = Normalize(key);
        if (id == null) return DariusEquipmentConstellationLocalization.TryDescription(key, out value);

        // The stock constellation detail widget asks for both a description and a separate lore row.
        // Darius used the same gameplay description for both, producing the duplicated grey line in
        // the lower panel. Keep the gameplay description in its native row and intentionally blank lore.
        if (IsLoreKey(key))
        {
            value = string.Empty;
            return true;
        }
        return DariusLanguage.IsEnglish
            ? DariusEnglishLocalization.TryStarDescription(id, out value)
            : Descriptions.TryGetValue(id, out value);
    }

    private static bool IsLoreKey(string key)
    {
        if (string.IsNullOrEmpty(key)) return false;
        return key.EndsWith(".lore", StringComparison.OrdinalIgnoreCase) ||
               key.EndsWith("_lore", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsDariusStarKey(string key) => Normalize(key) != null || DariusEquipmentConstellationLocalization.IsEquipmentKey(key);

    private static string Normalize(string key)
    {
        if (string.IsNullOrEmpty(key)) return null;
        foreach (string id in Names.Keys)
            if (key.IndexOf(id, StringComparison.OrdinalIgnoreCase) >= 0 || key.IndexOf(id.Replace("Se_", string.Empty), StringComparison.OrdinalIgnoreCase) >= 0)
                return id;
        return null;
    }
}


// Keep the stock constellation browser state coherent when the list is rebuilt after a run,
// category switch, loadout migration, or star purchase. The stock UI keeps a hovered index
// separately from the rebuilt item list; a stale index is enough to make StarDetails repeatedly
// index past the end of the list until the menu is closed.
[HarmonyPatch(typeof(UI_Lobby_Constellations_StarList), "Refresh", new Type[] { })]
public static class DariusConstellationStarListRefreshPatch
{
    private static long _refreshStartedTicks;

    private static readonly MethodInfo GenericStarLookup = typeof(DewResources).GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
        .First(m => m.Name == "GetByType" && m.IsGenericMethodDefinition && m.GetParameters().Length == 2 && m.GetParameters()[0].ParameterType == typeof(Type));

    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        MethodInfo replacement = AccessTools.Method(typeof(DariusConstellationStarListRefreshPatch), nameof(GetStarForBrowser));
        foreach (CodeInstruction instruction in instructions)
        {
            MethodInfo called = instruction.operand as MethodInfo;
            if (called != null && called.IsGenericMethod && called.GetGenericMethodDefinition() == GenericStarLookup &&
                called.GetGenericArguments().Length == 1 && called.GetGenericArguments()[0] == typeof(StarEffect))
            {
                instruction.opcode = OpCodes.Call;
                instruction.operand = replacement;
            }
            yield return instruction;
        }
    }

    private static StarEffect GetStarForBrowser(Type type, ResourceLoadSettings settings)
    {
        if (type == null) return null;
        StarEffect runtimeStar;
        if (DariusRuntimeResourceCompatibility.TryResolveRuntimeStar(type, out runtimeStar)) return runtimeStar;
        return DewResources.GetByType<StarEffect>(type, settings);
    }

    [HarmonyPrefix]
    private static void Prefix(UI_Lobby_Constellations_StarList __instance)
    {
        if (__instance == null) return;
        _refreshStartedTicks = DateTime.UtcNow.Ticks;
        try
        {
            __instance.hoveredIndex = -1;
            if (__instance.listGroup != null) __instance.listGroup.currentIndex = -1;

            // Dew.ClearTypeReferences only resets _allHeroes. If another Mod interrupts the ensuing
            // rebuild, _allHeroes can be populated while _allStarTypes remains empty forever. Repair
            // that impossible partial-cache state once before the stock UI enumerates constellations.
            IReadOnlyList<Type> stars = Dew.allStarTypes;
            if (stars == null || stars.Count == 0)
            {
                Dew.ClearTypeReferences();
                Dew.InitAllTypeReferences();
                DariusConstellationRegistry.ReassertTypeCache();
                DariusLog.Warn("CONSTELLATION-UI", "Recovered an empty Dew star-type cache before constellation refresh.");
            }
        }
        catch (Exception e) { DariusLog.Exception("CONSTELLATION-UI", e, "Could not prepare global constellation list refresh"); }
    }

    [HarmonyPostfix]
    private static void Postfix(UI_Lobby_Constellations_StarList __instance)
    {
        if (__instance == null) return;
        try
        {
            int count = __instance.items != null ? __instance.items.Count : 0;
            double elapsedMs = _refreshStartedTicks > 0
                ? TimeSpan.FromTicks(DateTime.UtcNow.Ticks - _refreshStartedTicks).TotalMilliseconds
                : -1.0;
            if (__instance.hoveredIndex < 0 || __instance.hoveredIndex >= count) __instance.hoveredIndex = -1;
            if (__instance.listGroup != null && (__instance.listGroup.currentIndex < -1 || __instance.listGroup.currentIndex >= count))
                __instance.listGroup.currentIndex = -1;
            // Do not overwrite the shared constellation portrait/background widgets. The lobby reuses
            // them across travelers and does not reliably restore the previous Sprite, which can visually
            // contaminate stock heroes after visiting a custom traveler. Star icons remain isolated below.
            DariusLog.DebugInfo("CONSTELLATION-UI", "Global star list refresh count=" + count +
                " elapsedMs=" + elapsedMs.ToString("0.0"));
        }
        catch (Exception e)
        {
            DariusLog.Exception("CONSTELLATION-UI", e, "Could not normalize Darius constellation star-list selection");
        }
    }
}

// The stock property getters only check for -1; stale positive indices survive a list rebuild and
// then throw from every input poll. Return null for any index outside the current item list.
[HarmonyPatch(typeof(UI_Lobby_Constellations_StarList), "get_selectedStar")]
public static class DariusConstellationSelectedStarBoundsPatch
{
    [HarmonyPrefix]
    private static bool Prefix(UI_Lobby_Constellations_StarList __instance, ref StarEffect __result)
    {
        if (__instance != null && __instance.items != null && __instance.selectedIndex >= 0 && __instance.selectedIndex < __instance.items.Count)
            return true;
        __result = null;
        return false;
    }
}

[HarmonyPatch(typeof(UI_Lobby_Constellations_StarList), "get_hoveredStar")]
public static class DariusConstellationHoveredStarBoundsPatch
{
    [HarmonyPrefix]
    private static bool Prefix(UI_Lobby_Constellations_StarList __instance, ref StarEffect __result)
    {
        if (__instance != null && __instance.items != null && __instance.hoveredIndex >= 0 && __instance.hoveredIndex < __instance.items.Count)
            return true;
        __result = null;
        return false;
    }
}


// Preserve Riot's full-colour rune/ability artwork inside the stock star browser. The stock
// constellation UI normally treats star icons as monochrome masks and applies the branch colour;
// opaque League rune backgrounds therefore collapse into featureless red/green circles. For
// Darius-owned stars we keep the stock lock/dim behaviour, but neutralize branch tint on visible
// icon images and force the runtime Riot Sprite after both Setup and Refresh.
[HarmonyPatch(typeof(UI_Lobby_Constellations_StarItem), "Setup", new Type[] { typeof(StarEffect), typeof(int) })]
public static class DariusConstellationStarItemSetupPresentationPatch
{
    [HarmonyPostfix]
    private static void Postfix(UI_Lobby_Constellations_StarItem __instance, StarEffect __0)
    {
        DariusConstellationItemPresentation.RememberAndApply(__instance, __0);
    }
}

public static class DariusConstellationItemPresentation
{
    private static readonly Dictionary<int, string> ItemIconKeys = new Dictionary<int, string>();

    public static void RememberAndApply(UI_Lobby_Constellations_StarItem item, StarEffect star)
    {
        if (item == null) return;
        int itemId = item.GetInstanceID();
        // StarItem objects are pooled and reused across travelers. Always forget the previous
        // mod icon before inspecting the newly assigned StarEffect, otherwise Refresh can repaint
        // another traveler's constellation with a stale League icon.
        ItemIconKeys.Remove(itemId);
        if (star == null) return;
        string key;
        if (!DariusConstellationRegistry.TryGetIconKey(star, out key)) return;
        ItemIconKeys[itemId] = key;
        Apply(item, key);
    }

    public static void ApplyRemembered(UI_Lobby_Constellations_StarItem item)
    {
        if (item == null) return;
        string key;
        if (ItemIconKeys.TryGetValue(item.GetInstanceID(), out key)) Apply(item, key);
    }

    public static bool HasRememberedItems(UI_Lobby_Constellations_StarList list)
    {
        if (list == null || list.items == null) return false;
        for (int i = 0; i < list.items.Count; i++)
        {
            UI_Lobby_Constellations_StarItem item = list.items[i];
            if (item != null && ItemIconKeys.ContainsKey(item.GetInstanceID())) return true;
        }
        return false;
    }

    private static void Apply(UI_Lobby_Constellations_StarItem item, string iconKey)
    {
        Sprite desired = DariusPrototypeIcons.Get(iconKey);
        if (desired == null) return;
        try
        {
            UI_StarIcon icon = item.GetComponentInChildren<UI_StarIcon>(true);
            if (icon == null) return;
            ApplyImage(icon.iconFillMask, desired);
            ApplyImage(icon.iconFill, desired);
            ApplyImage(icon.iconBg, desired);
        }
        catch { }
    }

    private static void ApplyImage(UnityEngine.UI.Image image, Sprite desired)
    {
        if (image == null) return;
        image.sprite = desired;
        Color color = image.color;
        if (Mathf.Max(color.r, Mathf.Max(color.g, color.b)) > 0.22f)
            image.color = new Color(1f, 1f, 1f, color.a);
    }
}

public static class DariusLobbyConstellationPresentation
{
    public static void ApplyHeroPortrait(Component origin)
    {
        if (origin == null) return;
        try
        {
            if (DewPlayer.local == null || !string.Equals(DewPlayer.local.selectedHeroType, DariusTravelerRegistry.HeroName, StringComparison.Ordinal)) return;
            Sprite portrait = DariusPrototypeIcons.Get("HERO");
            if (portrait == null) return;

            int applied = 0;
            Transform cursor = origin.transform;
            Transform highest = cursor;
            for (int i = 0; cursor != null && i < 10; i++, cursor = cursor.parent) highest = cursor;
            Component[] components = highest != null ? highest.GetComponentsInChildren<Component>(true) : origin.GetComponentsInChildren<Component>(true);
            foreach (Component component in components)
            {
                if (component == null || component.gameObject == null) continue;
                string objectName = component.gameObject.name.ToLowerInvariant();
                string typeName = component.GetType().Name.ToLowerInvariant();
                bool namedPortrait = objectName.Contains("hero") || objectName.Contains("portrait") || objectName.Contains("traveler") ||
                                    objectName.Contains("character") || objectName.Contains("profile") ||
                                    typeName.Contains("heroicon") || typeName.Contains("portrait");
                if (!namedPortrait || objectName.Contains("skill") || objectName.Contains("memory") || objectName.Contains("star") || objectName.Contains("lock")) continue;
                if (TrySetSpriteAndWhite(component, portrait)) applied++;
            }

            // Serialized controller references are more reliable than GameObject names on some UI prefabs.
            cursor = origin.transform;
            for (int i = 0; cursor != null && i < 10; i++, cursor = cursor.parent)
            {
                foreach (Component controller in cursor.GetComponents<Component>())
                {
                    if (controller == null) continue;
                    string tn = controller.GetType().Name;
                    if (tn.IndexOf("Constellation", StringComparison.OrdinalIgnoreCase) < 0 &&
                        tn.IndexOf("Hero", StringComparison.OrdinalIgnoreCase) < 0) continue;
                    applied += ApplyControllerPortraitReferences(controller, portrait);
                }
            }

            // Some 1.3.x constellation prefabs expose the large traveler portrait as one or more
            // generically named square Images instead of a serialized heroIcon field. Always sweep
            // the screen for the largest portrait-like square targets, because named/controller paths
            // may fix one preview widget yet still leave the top-left profile square on the stock fog.
            applied += ApplyLargeSquarePortraitCandidates(components, portrait);

            DariusLog.DebugInfoThrottled("HERO-PORTRAIT", "constellation-root",
                "Rebound Darius champion portrait in constellation UI targets=" + applied, 0.75);
        }
        catch (Exception e)
        {
            DariusLog.Exception("HERO-PORTRAIT", e, "Constellation hero portrait repair failed");
        }
    }

    private static int ApplyLargeSquarePortraitCandidates(Component[] components, Sprite portrait)
    {
        if (components == null || portrait == null) return 0;
        List<KeyValuePair<Component, float>> candidates = new List<KeyValuePair<Component, float>>();
        foreach (Component component in components)
        {
            if (component == null || component.gameObject == null) continue;
            string n = component.gameObject.name.ToLowerInvariant();
            if (n.Contains("background") || n.Contains("panel") || n.Contains("frame") || n.Contains("star") ||
                n.Contains("skill") || n.Contains("memory") || n.Contains("lock") || n.Contains("tab") ||
                n.Contains("branch") || n.Contains("line")) continue;
            PropertyInfo sp = component.GetType().GetProperty("sprite", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            PropertyInfo tp = component.GetType().GetProperty("texture", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            bool spriteTarget = sp != null && sp.CanWrite && sp.PropertyType == typeof(Sprite);
            bool textureTarget = tp != null && tp.CanWrite && typeof(Texture).IsAssignableFrom(tp.PropertyType);
            if (!spriteTarget && !textureTarget) continue;
            RectTransform rect = component.transform as RectTransform;
            if (rect == null) continue;
            float width = Mathf.Abs(rect.rect.width);
            float height = Mathf.Abs(rect.rect.height);
            if (width < 96f || height < 96f) continue;
            float ratio = height > 0.01f ? width / height : 0f;
            if (ratio < 0.72f || ratio > 1.38f) continue;
            float area = width * height;
            if (area < 9000f || area > 180000f) continue;
            candidates.Add(new KeyValuePair<Component, float>(component, area));
        }

        candidates.Sort((a, b) => b.Value.CompareTo(a.Value));
        int applied = 0;
        int limit = Mathf.Min(3, candidates.Count);
        for (int i = 0; i < limit; i++)
        {
            KeyValuePair<Component, float> kv = candidates[i];
            if (TrySetSpriteAndWhite(kv.Key, portrait))
            {
                applied++;
                DariusLog.DebugInfoThrottled("HERO-PORTRAIT", "square-target:" + kv.Key.GetInstanceID(),
                    "Applied Darius portrait to square constellation image name=" + kv.Key.gameObject.name +
                    " area=" + kv.Value.ToString("0"), 2.0);
            }
        }
        return applied;
    }

    private static int ApplyControllerPortraitReferences(Component controller, Sprite portrait)
    {
        int applied = 0;
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        foreach (FieldInfo field in controller.GetType().GetFields(flags))
        {
            string n = field.Name.ToLowerInvariant();
            if (!n.Contains("hero") && !n.Contains("portrait") && !n.Contains("traveler") && !n.Contains("character")) continue;
            object value = null;
            try { value = field.GetValue(controller); } catch { }
            Component component = value as Component;
            if (component != null && TrySetSpriteAndWhite(component, portrait)) applied++;
        }
        foreach (PropertyInfo property in controller.GetType().GetProperties(flags))
        {
            if (!property.CanRead || property.GetIndexParameters().Length != 0) continue;
            string n = property.Name.ToLowerInvariant();
            if (!n.Contains("hero") && !n.Contains("portrait") && !n.Contains("traveler") && !n.Contains("character")) continue;
            object value = null;
            try { value = property.GetValue(controller, null); } catch { }
            Component component = value as Component;
            if (component != null && TrySetSpriteAndWhite(component, portrait)) applied++;
        }
        return applied;
    }

    private static bool TrySetSpriteAndWhite(Component component, Sprite sprite)
    {
        if (component == null || sprite == null) return false;
        try
        {
            bool applied = false;
            PropertyInfo p = component.GetType().GetProperty("sprite", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (p != null && p.CanWrite && p.PropertyType == typeof(Sprite))
            {
                p.SetValue(component, sprite, null);
                applied = true;
            }
            PropertyInfo tp = component.GetType().GetProperty("texture", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (tp != null && tp.CanWrite && typeof(Texture).IsAssignableFrom(tp.PropertyType) && tp.PropertyType.IsAssignableFrom(sprite.texture.GetType()))
            {
                tp.SetValue(component, sprite.texture, null);
                applied = true;
            }
            if (!applied) return false;

            PropertyInfo cp = component.GetType().GetProperty("color", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (cp != null && cp.CanWrite && cp.PropertyType == typeof(Color))
            {
                Color old = Color.white;
                try { if (cp.CanRead) old = (Color)cp.GetValue(component, null); } catch { }
                cp.SetValue(component, new Color(1f, 1f, 1f, old.a), null);
            }
            return true;
        }
        catch { return false; }
    }
}


// Defensive release guard for stock constellation UI. Runtime-injected stars are hydrated from a
// native StarEffect contract above; if a game build still indexes a stock presentation array beyond
// its bounds, contain that UI-only exception for Hero_Darius instead of leaving the constellation
// menu in a permanently corrupted hover/detail state.
[HarmonyPatch(typeof(UI_Lobby_Constellations_StarItem), "Refresh", new Type[] { })]
public static class DariusConstellationStarItemRefreshGuard
{
    [HarmonyPostfix]
    private static void Postfix(UI_Lobby_Constellations_StarItem __instance)
    {
        DariusConstellationItemPresentation.ApplyRemembered(__instance);
    }

    [HarmonyFinalizer]
    private static Exception Finalizer(Exception __exception)
    {
        if (__exception == null) return null;
        try
        {
            bool darius = DewPlayer.local != null &&
                string.Equals(DewPlayer.local.selectedHeroType, DariusTravelerRegistry.HeroName, StringComparison.Ordinal);
            if (darius && (__exception is IndexOutOfRangeException || __exception is ArgumentOutOfRangeException))
            {
                DariusLog.Warn("CONSTELLATION-UI", "Contained stock StarItem.Refresh bounds exception for Hero_Darius: " + __exception.GetType().Name);
                return null;
            }
        }
        catch { }
        return __exception;
    }
}
