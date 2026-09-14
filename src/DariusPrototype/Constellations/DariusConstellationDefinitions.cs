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
