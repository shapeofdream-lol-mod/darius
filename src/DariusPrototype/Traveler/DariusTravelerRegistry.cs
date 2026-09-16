using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;

public static partial class DariusTravelerRegistry
{
    public const string HeroName = "Hero_Darius";
    public const string DefaultSkinName = "Skin_Darius_Default";
    public const string GodKingSkinName = "Skin_Darius_GodKing";
    public const string DunkmasterSkinName = "Skin_Darius_Dunkmaster";
    public const string MechaSkinName = "Skin_Darius_Mecha";
    public const string LegacySkinAlias = "Skin_Vesper_Darius";
    public const string HeroGuid = "com.openai.sod.darius.hero.v1";
    public const string SkinGuid = "com.openai.sod.darius.skin.default.v1";
    public const string GodKingSkinGuid = "com.openai.sod.darius.skin.godking.v1";
    public const string DunkmasterSkinGuid = "com.openai.sod.darius.skin.dunkmaster.v1";
    public const string MechaSkinGuid = "com.openai.sod.darius.skin.mecha.v1";
    public const uint HeroAssetId = 2804836319u;

    private sealed class DariusSkinSpec
    {
        public string name, guid, displayName, previewIconKey;
        public DariusNativeSkinProfile native;
    }

    private static readonly DariusSkinSpec[] SkinSpecs =
    {
        new DariusSkinSpec { name=DefaultSkinName, guid=SkinGuid, displayName="经典德莱厄斯", previewIconKey="SKIN_CLASSIC", native=DariusNativeSkinProfiles.Classic },
        new DariusSkinSpec { name=GodKingSkinName, guid=GodKingSkinGuid, displayName="神王德莱厄斯", previewIconKey="SKIN_GODKING", native=DariusNativeSkinProfiles.GodKing },
        new DariusSkinSpec { name=DunkmasterSkinName, guid=DunkmasterSkinGuid, displayName="灌篮高手 德莱厄斯", previewIconKey="SKIN_DUNKMASTER", native=DariusNativeSkinProfiles.Dunkmaster },
        new DariusSkinSpec { name=MechaSkinName, guid=MechaSkinGuid, displayName="机神 德莱厄斯", previewIconKey="SKIN_MECHA", native=DariusNativeSkinProfiles.Mecha }
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
            if (string.Equals(SkinSpecs[i].name, key, StringComparison.Ordinal) ||
                string.Equals(SkinSpecs[i].guid, key, StringComparison.Ordinal)) return SkinSpecs[i];
        return null;
    }

    public static bool IsRuntimeSkinKey(string key)
    {
        return !string.IsNullOrEmpty(key) && FindSkinSpec(key) != null;
    }

    public static bool TryResolveRuntimeSkin(string key, out Skin skin)
    {
        skin = null;
        DariusSkinSpec spec = FindSkinSpec(key);
        return spec != null && SkinsByName.TryGetValue(spec.name, out skin) && skin != null;
    }

    private static bool AreSkinResourcesReady()
    {
        if (SkinSpecs == null || SkinSpecs.Length == 0) return false;
        for (int i = 0; i < SkinSpecs.Length; i++)
        {
            Skin skin;
            if (!SkinsByName.TryGetValue(SkinSpecs[i].name, out skin) || skin == null || skin.gameObject == null) return false;
            if (skin.GetComponent<EntityModel>() == null || skin.GetComponent<DariusSkinModelBinding>() == null ||
                skin.GetComponent<DariusTravelerModelInstance>() == null) return false;
        }
        return true;
    }
}

// Lives on the clean Skin_Darius_Default EntityModel. The Skin object copies only the generic
// native EntityModel data contract; the visible hierarchy is Darius-only from the first frame.