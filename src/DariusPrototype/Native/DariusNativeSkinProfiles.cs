using System;

internal sealed class DariusNativeSkinProfile
{
    public string Variant;
    public string GlbFile;
    public string FbxFile;
    public float Scale;
    public float YOffset;
    public float Yaw;
    public bool GodKing;
    public int ExpectedPrimitives;
    public int ExpectedBones;
    public int ExpectedAnimations;
    public string ToggleHiddenMaterial;
    public string PermanentHiddenMaterial;
    public string Idle;
    public string IdleVariant;
    public string Run;
    public string Death;
    public string Attack1;
    public string Attack2;
    public string Crit;
    public string QIntro;
    public string Q;
    public string W;
    public string WIdle;
    public string WRun;
    public string E;
    public string R;
    public string Attack1ToIdle;
    public string Attack2ToIdle;
    public string CritToIdle;
    public string EToRun;
    public string EToIdle;
    public string RToRun;
    public string WActivateIdle;
    public string WActivateRun;
    public string WIdleIn;
    public string WIdleInAlt;
    public string WDeactivate;
    public string WDeactivateAlt;

    public string[] RequiredClips
    {
        get
        {
            return new[] {
                Idle, IdleVariant, Run, Death, Attack1, Attack2, Crit, QIntro, Q, W, WIdle, WRun, E, R,
                Attack1ToIdle, Attack2ToIdle, CritToIdle, EToRun, EToIdle, RToRun,
                WActivateIdle, WActivateRun, WIdleIn, WIdleInAlt, WDeactivate, WDeactivateAlt
            };
        }
    }
}

internal static class DariusNativeSkinProfiles
{
    public const float RuntimeScale = 0.00921f;
    public const float RuntimeYOffset = 0.04f;

    public static readonly DariusNativeSkinProfile Classic = new DariusNativeSkinProfile
    {
        Variant="Classic", GlbFile="darius.glb", FbxFile="darius.fbx", Scale=RuntimeScale,
        YOffset=RuntimeYOffset, Yaw=0f, ExpectedPrimitives=1, ExpectedBones=89, ExpectedAnimations=22,
        Idle="Idle1", IdleVariant="Idle2", Run="Run", Death="Death", Attack1="Attack1", Attack2="Attack2",
        Crit="Crit", QIntro="Darius_Spell1_IN.anm", Q="Spell1", W="Spell2", E="Spell3", R="Spell4"
    };

    public static readonly DariusNativeSkinProfile GodKing = new DariusNativeSkinProfile
    {
        Variant="GodKing", GlbFile="darius_godking.glb", FbxFile="darius_godking.fbx", Scale=RuntimeScale,
        YOffset=RuntimeYOffset, Yaw=180f, GodKing=true, ExpectedPrimitives=5, ExpectedBones=179, ExpectedAnimations=48,
        ToggleHiddenMaterial="Wolf_Mat", PermanentHiddenMaterial="Throne",
        Idle="Idle1_Base", IdleVariant="Idle2_Base", Run="Run_Normal", Death="Death", Attack1="Attack1",
        Attack2="Attack2", Crit="Crit", QIntro="Darius_Skin15_Spell1_IN.anm", Q="Spell1", W="Spell2",
        WIdle="Spell2_Idle", WRun="Spell2_Run", E="Spell3", R="Spell4",
        Attack1ToIdle="Attack1_ToIdle", Attack2ToIdle="Attack2_ToIdle", CritToIdle="Crit_ToIdle",
        EToRun="Spell3_ToRun", EToIdle="Spell3_ToIdle", RToRun="Spell4_ToRun",
        WActivateIdle="Spell2_ActivateIdle", WActivateRun="Spell2_ActivateRun",
        WIdleIn="Spell2_IdleIn", WIdleInAlt="Spell2_IdleIn2",
        WDeactivate="Spell2_Deactivate", WDeactivateAlt="Darius_Skin15_Spell2_Deactivate.anm"
    };

    public static readonly DariusNativeSkinProfile Dunkmaster = new DariusNativeSkinProfile
    {
        Variant="Dunkmaster", GlbFile="darius_dunkmaster.glb", FbxFile="darius_dunkmaster.fbx", Scale=RuntimeScale,
        YOffset=RuntimeYOffset, Yaw=0f, ExpectedPrimitives=1, ExpectedBones=99, ExpectedAnimations=36,
        Idle="Idle1_Base", IdleVariant="Idle2_Base", Run="Darius_Skin04_Run.anm", Death="Death", Attack1="Attack1",
        Attack2="Attack2", Crit="Crit", QIntro="Darius_Skin04_Spell1_IN.anm", Q="Spell1", W="Spell2", E="Spell3",
        R="Darius_Skin04_Spell4_A.anm"
    };

    public static readonly DariusNativeSkinProfile Mecha = new DariusNativeSkinProfile
    {
        Variant="Mecha", GlbFile="darius_mecha.glb", FbxFile="darius_mecha.fbx", Scale=RuntimeScale,
        YOffset=RuntimeYOffset, Yaw=180f, ExpectedPrimitives=13, ExpectedBones=246, ExpectedAnimations=59,
        Idle="Idle1_Base", IdleVariant="Idle2_legacy.SKINS_Darius_Skin67.anm", Run="Run_Normal", Death="Death",
        Attack1="Attack1", Attack2="Attack2", Crit="Crit", QIntro="Spell1_IN_Stand.SKINS_Darius_Skin67.anm",
        Q="Spell1", W="Spell2", E="Spell3", R="Spell4", Attack1ToIdle="Attack1_ToIdle",
        Attack2ToIdle="Attack2_ToIdle", CritToIdle="Crit_ToIdle", EToRun="Spell3_ToRun",
        EToIdle="Spell3_ToIdle", RToRun="Spell4_ToRun"
    };

    public static readonly DariusNativeSkinProfile[] All = { Classic, GodKing, Dunkmaster, Mecha };

    public static DariusNativeSkinProfile Find(string variant)
    {
        if (string.IsNullOrEmpty(variant)) return null;
        for (int i = 0; i < All.Length; i++)
            if (string.Equals(All[i].Variant, variant, StringComparison.OrdinalIgnoreCase)) return All[i];
        return null;
    }
}
