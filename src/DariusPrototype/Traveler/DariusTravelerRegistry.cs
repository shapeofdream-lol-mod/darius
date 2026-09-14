public static partial class DariusTravelerRegistry
{
    public const string HeroName = "Hero_Darius";

    public const string DefaultSkinName = "Skin_Darius_Default";

    public const string GodKingSkinName = "Skin_Darius_GodKing";

    public const string DunkmasterSkinName = "Skin_Darius_Dunkmaster";

    public const string MechaSkinName = "Skin_Darius_Mecha";

    // v0.13-v0.15 used this Vesper-hosted skin name and it can remain in profile-selected-skin
    // data across upgrades. Treat it as a compatibility alias instead of letting GetByName fail.
    public const string LegacySkinAlias = "Skin_Vesper_Darius";

    public const string HeroGuid = "com.openai.sod.darius.hero.v1";

    public const string SkinGuid = "com.openai.sod.darius.skin.default.v1";

    public const string GodKingSkinGuid = "com.openai.sod.darius.skin.godking.v1";

    public const string DunkmasterSkinGuid = "com.openai.sod.darius.skin.dunkmaster.v1";

    public const string MechaSkinGuid = "com.openai.sod.darius.skin.mecha.v1";

    public const uint HeroAssetId = 2804836319u;

    private sealed class DariusSkinSpec
    {
        public string name, guid, modelFile, displayName, variantKey, previewIconKey;
        public float modelScale, modelYOffset, modelYaw;
        public int expectedPrimitives, expectedBones, expectedAnimations;
        public bool godKing;
        public string idle, idleVariant, run, death, attack1, attack2, crit, qIntro, q, w, e, r;
        public string attack1ToIdle, attack2ToIdle, critToIdle, eToRun, eToIdle, rToRun;
    }

    // Every skin has an explicit profile derived from its own GLB. Never infer animation names,
    // bone counts, primitive counts, or scale from another skin.
    private static readonly DariusSkinSpec[] SkinSpecs = new DariusSkinSpec[]
    {
        new DariusSkinSpec { name=DefaultSkinName, guid=SkinGuid, modelFile="darius.glb", displayName="经典德莱厄斯", variantKey="Classic", previewIconKey="SKIN_CLASSIC", modelScale=0.00921f, modelYOffset=0.04f, modelYaw=0f, expectedPrimitives=1, expectedBones=89, expectedAnimations=22,
            idle="Idle1", idleVariant="Idle2", run="Run", death="Death", attack1="Attack1", attack2="Attack2", crit="Crit", qIntro="Darius_Spell1_IN.anm", q="Spell1", w="Spell2", e="Spell3", r="Spell4" },
        new DariusSkinSpec { name=GodKingSkinName, guid=GodKingSkinGuid, modelFile="darius_godking.glb", displayName="神王德莱厄斯", variantKey="GodKing", previewIconKey="SKIN_GODKING", godKing=true, modelScale=0.00921f, modelYOffset=0.04f, modelYaw=180f, expectedPrimitives=5, expectedBones=179, expectedAnimations=48,
            idle="Idle1_Base", idleVariant="Idle2_Base", run="Run_Normal", death="Death", attack1="Attack1", attack2="Attack2", crit="Crit", qIntro="Darius_Skin15_Spell1_IN.anm", q="Spell1", w="Spell2", e="Spell3", r="Spell4",
            attack1ToIdle="Attack1_ToIdle", attack2ToIdle="Attack2_ToIdle", critToIdle="Crit_ToIdle", eToRun="Spell3_ToRun", eToIdle="Spell3_ToIdle", rToRun="Spell4_ToRun" },
        new DariusSkinSpec { name=DunkmasterSkinName, guid=DunkmasterSkinGuid, modelFile="darius_dunkmaster.glb", displayName="灌篮高手 德莱厄斯", variantKey="Dunkmaster", previewIconKey="SKIN_DUNKMASTER", modelScale=0.00921f, modelYOffset=0.04f, modelYaw=0f, expectedPrimitives=1, expectedBones=99, expectedAnimations=36,
            idle="Idle1_Base", idleVariant="Idle2_Base", run="Darius_Skin04_Run.anm", death="Death", attack1="Attack1", attack2="Attack2", crit="Crit", qIntro="Darius_Skin04_Spell1_IN.anm", q="Spell1", w="Spell2", e="Spell3", r="Darius_Skin04_Spell4_A.anm" },
        new DariusSkinSpec { name=MechaSkinName, guid=MechaSkinGuid, modelFile="darius_mecha.glb", displayName="机神 德莱厄斯", variantKey="Mecha", previewIconKey="SKIN_MECHA", modelScale=0.00921f, modelYOffset=0.04f, modelYaw=180f, expectedPrimitives=13, expectedBones=246, expectedAnimations=59,
            idle="Idle1_Base", idleVariant="Idle2_legacy.SKINS_Darius_Skin67.anm", run="Run_Normal", death="Death", attack1="Attack1", attack2="Attack2", crit="Crit", qIntro="Spell1_IN_Stand.SKINS_Darius_Skin67.anm", q="Spell1", w="Spell2", e="Spell3", r="Spell4",
            attack1ToIdle="Attack1_ToIdle", attack2ToIdle="Attack2_ToIdle", critToIdle="Crit_ToIdle", eToRun="Spell3_ToRun", eToIdle="Spell3_ToIdle", rToRun="Spell4_ToRun" }
    };

    private static readonly Dictionary<string, Skin> SkinsByName = new Dictionary<string, Skin>(StringComparer.Ordinal);

    public const string AttackName = "At_DariusAxe";

    public const string AttackGuid = "com.openai.sod.darius.attack.axe.v1";

    public const uint AttackAssetId = 2804836320u;

    public const string AttackInstanceName = "Ai_DariusAxe";

    public const string AttackInstanceGuid = "com.openai.sod.darius.attack.axe.instance.v1";

    public const uint AttackInstanceAssetId = 2804836321u;

    public const string AttackCritInstanceName = "Ai_DariusAxe_Crit";

    public const string AttackCritInstanceGuid = "com.openai.sod.darius.attack.axe.crit.v1";

    public const uint AttackCritInstanceAssetId = 2804836322u;

    private static readonly Dictionary<string, UnityEngine.Object> ResourcesByGuid = new Dictionary<string, UnityEngine.Object>(StringComparer.Ordinal);

    private static readonly Dictionary<Type, UnityEngine.Object> ResourcesByType = new Dictionary<Type, UnityEngine.Object>();

    private static readonly Dictionary<uint, GameObject> NetworkPrefabs = new Dictionary<uint, GameObject>();

    private static readonly List<GameObject> OwnedObjects = new List<GameObject>();

    private static GameObject _resourceRoot;

    private static bool _registered;

    private static bool _registering;

    private static bool _repairing;

    private static bool _presentationGenerationRebuilt;

    public static Hero_Darius HeroPrefab { get; private set; }

    public static Skin DefaultSkin { get; private set; }

    public static Skin GodKingSkin { get; private set; }

    public static Skin DunkmasterSkin { get; private set; }

    public static Skin MechaSkin { get; private set; }

    public static At_DariusAxe AttackPrefab { get; private set; }

    public static Ai_DariusAxe AttackInstancePrefab { get; private set; }

    public static Ai_DariusAxe_Crit AttackCritInstancePrefab { get; private set; }

    private static DariusSkinSpec FindSkinSpec(string key)
    {
        if (string.IsNullOrEmpty(key)) return null;
        for (int i = 0; i < SkinSpecs.Length; i++)
            if (string.Equals(SkinSpecs[i].name, key, StringComparison.Ordinal) || string.Equals(SkinSpecs[i].guid, key, StringComparison.Ordinal)) return SkinSpecs[i];
        return null;
    }

    public static bool IsRuntimeSkinKey(string key)
    {
        if (string.IsNullOrEmpty(key)) return false;
        return FindSkinSpec(key) != null;
    }

    public static bool TryResolveRuntimeSkin(string key, out Skin skin)
    {
        skin = null;
        if (string.IsNullOrEmpty(key)) return false;
        DariusSkinSpec spec = FindSkinSpec(key);
        if (spec == null) return false;
        return SkinsByName.TryGetValue(spec.name, out skin) && skin != null;
    }

    private static bool AreSkinResourcesReady()
    {
        if (SkinSpecs == null || SkinSpecs.Length == 0) return false;
        for (int i = 0; i < SkinSpecs.Length; i++)
        {
            Skin skin;
            if (!SkinsByName.TryGetValue(SkinSpecs[i].name, out skin) || skin == null || skin.gameObject == null) return false;
            // A run teardown can leave the managed Skin wrapper alive while destroying the Unity
            // presentation graph that CharacterModelDisplay later instantiates. Treat a partially
            // alive Skin as stale instead of trusting only Unity's wrapper-null test.
            if (skin.GetComponent<EntityModel>() == null || skin.GetComponent<DariusSkinModelBinding>() == null ||
                skin.GetComponent<DariusTravelerModelInstance>() == null) return false;
        }
        return true;
    }
}

// Lives on the clean Skin_Darius_Default EntityModel. The Skin object copies only the generic
// native EntityModel data contract; the visible hierarchy is Darius-only from the first frame.