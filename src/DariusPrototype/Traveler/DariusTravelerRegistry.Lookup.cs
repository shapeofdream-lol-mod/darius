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
    private static void ValidateRegistration()
    {
        List<string> errors = new List<string>();
        if (HeroPrefab == null) errors.Add("HeroPrefab null");
        if (DefaultSkin == null) errors.Add("DefaultSkin null");
        if (AttackPrefab == null) errors.Add("AttackPrefab null");
        if (AttackInstancePrefab == null) errors.Add("AttackInstancePrefab null");
        if (AttackCritInstancePrefab == null) errors.Add("AttackCritInstancePrefab null");
        if (!ResourcesByGuid.ContainsKey(HeroGuid)) errors.Add("Hero GUID bridge missing");
        if (!ResourcesByGuid.ContainsKey(SkinGuid)) errors.Add("Skin GUID bridge missing");
        if (!ResourcesByGuid.ContainsKey(AttackGuid)) errors.Add("Darius attack GUID bridge missing");
        if (!ResourcesByGuid.ContainsKey(AttackInstanceGuid)) errors.Add("Darius attack instance GUID bridge missing");
        if (!ResourcesByGuid.ContainsKey(AttackCritInstanceGuid)) errors.Add("Darius crit attack instance GUID bridge missing");
        if (HeroPrefab != null)
        {
            GameObject go = HeroPrefab.gameObject;
            string[] required = { "EntityAI", "EntityAbility", "EntityStatus", "EntityAnimation", "EntityControl", "EntityVisual", "EntitySound", "HeroSkill", "NetworkIdentity" };
            foreach (string typeName in required)
            {
                bool found = go.GetComponents<Component>().Any(c => c != null && c.GetType().Name == typeName);
                if (!found) errors.Add("Hero component missing " + typeName);
            }
            EntityAbility ea = go.GetComponent<EntityAbility>();
            if (ea == null) errors.Add("EntityAbility missing");
            else
            {
                try
                {
                    AttackTrigger preset = ea.attackAbilityPreset.asset;
                    if (preset == null || preset.GetType() != typeof(At_DariusAxe))
                    {
                        DariusNativeAttackBinder binder = go.GetComponent<DariusNativeAttackBinder>();
                        if (binder != null) binder.EnsureBound("ValidateRegistration deferred AssetRef resolution");
                        DariusLog.Warn("TRAVELER-ASSERT", "attackAbilityPreset AssetRef is not resolvable yet; deferring until runtime resource hooks are installed.");
                    }
                }
                catch (Exception e) { errors.Add("attackAbilityPreset resolve threw " + e.GetType().Name); }
            }
            HeroSkill hs = go.GetComponent<HeroSkill>();
            if (hs == null || GetArrayLength(hs, "loadoutQ") < 1) errors.Add("loadoutQ empty");
            if (hs == null || GetArrayLength(hs, "loadoutR") < 1) errors.Add("loadoutR empty");
            if (hs == null || GetArrayLength(hs, "loadoutTrait") < 1) errors.Add("loadoutTrait empty");
            if (hs == null || GetArrayLength(hs, "loadoutMovement") < 2) errors.Add("loadoutMovement does not expose both Flash/Ghost");
        }
        for (int si = 0; si < SkinSpecs.Length; si++)
        {
            DariusSkinSpec spec = SkinSpecs[si]; Skin skin;
            if (!SkinsByName.TryGetValue(spec.name, out skin) || skin == null) { errors.Add(spec.name + " skin missing"); continue; }
            if (skin.GetComponent<EntityModel>() == null) errors.Add(spec.name + " EntityModel missing");
            ValidateSkinModelBinding(skin, spec, errors);
            try { if (!skin.IsValidFor(HeroName)) errors.Add(spec.name + ".IsValidFor(Hero_Darius)=false"); }
            catch (Exception e) { errors.Add(spec.name + ".IsValidFor threw " + e.GetType().Name); }
        }
        try
        {
            object database = DewResources.database;
            if (!DariusUnsupportedResourceBridge.IsNetworkGuidMapped(database, HeroAssetId, HeroGuid))
                errors.Add("Hero Mirror assetId map missing/wrong");
            if (!DariusUnsupportedResourceBridge.IsNetworkGuidMapped(database, AttackAssetId, AttackGuid))
                errors.Add("At_DariusAxe Mirror assetId map missing/wrong");
            if (!DariusUnsupportedResourceBridge.IsNetworkGuidMapped(database, AttackInstanceAssetId, AttackInstanceGuid))
                errors.Add("Ai_DariusAxe Mirror assetId map missing/wrong");
            if (!DariusUnsupportedResourceBridge.IsNetworkGuidMapped(database, AttackCritInstanceAssetId, AttackCritInstanceGuid))
                errors.Add("Ai_DariusAxe_Crit Mirror assetId map missing/wrong");
            if (!RegisteredSpawnHandlerIds.Contains(HeroAssetId))
                errors.Add("Hero spawn handler ownership missing");
            if (!RegisteredSpawnHandlerIds.Contains(AttackAssetId))
                errors.Add("At_DariusAxe spawn handler ownership missing");
            if (!RegisteredSpawnHandlerIds.Contains(AttackInstanceAssetId))
                errors.Add("Ai_DariusAxe spawn handler ownership missing");
            if (!RegisteredSpawnHandlerIds.Contains(AttackCritInstanceAssetId))
                errors.Add("Ai_DariusAxe_Crit spawn handler ownership missing");
        }
        catch { errors.Add("Hero/attack Mirror assetId maps unreadable"); }
        try
        {
            if (!Dew.allHeroes.Contains(typeof(Hero_Darius))) errors.Add("Dew.allHeroes missing Hero_Darius");
        }
        catch { errors.Add("Dew.allHeroes unavailable"); }
        DewProfile p = DewSave.profileMain;
        if (p != null)
        {
            if (p.heroes == null || !p.heroes.ContainsKey(HeroName)) errors.Add("profile.heroes missing Hero_Darius");
            if (p.heroLoadouts == null || !p.heroLoadouts.ContainsKey(HeroName)) errors.Add("profile.heroLoadouts missing Hero_Darius");
            if (p.heroSelectedSkins == null || !p.heroSelectedSkins.ContainsKey(HeroName)) errors.Add("profile.heroSelectedSkins missing Hero_Darius");
        }

        if (errors.Count > 0)
        {
            string message = string.Join("; ", errors.ToArray());
            DariusLog.Error("TRAVELER-ASSERT", "Startup assertions FAILED: " + message);
            throw new InvalidOperationException(message);
        }
        DariusLog.Info("TRAVELER-ASSERT", "Startup assertions passed for Hero/Skin/native-attack/Loadout/resource contract.");
    }

    public static bool TryLoad(string key, out UnityEngine.Object obj)
    {
        obj = null;
        if (string.IsNullOrEmpty(key)) return false;
        if (ResourcesByGuid.TryGetValue(key, out obj) && obj != null) return true;
        if (key == HeroName && HeroPrefab != null) { obj = HeroPrefab; return true; }
        if (key == DefaultSkinName && DefaultSkin != null) { obj = DefaultSkin; return true; }
        Skin skinByName;
        if (SkinsByName.TryGetValue(key, out skinByName) && skinByName != null) { obj = skinByName; return true; }
        if (key == AttackName && AttackPrefab != null) { obj = AttackPrefab; return true; }
        if (key == AttackInstanceName && AttackInstancePrefab != null) { obj = AttackInstancePrefab; return true; }
        if (key == AttackCritInstanceName && AttackCritInstancePrefab != null) { obj = AttackCritInstancePrefab; return true; }
        return false;
    }

    public static bool TryGetNetworkPrefab(uint assetId, out GameObject prefab)
    {
        return NetworkPrefabs.TryGetValue(assetId, out prefab) && prefab != null;
    }

    public static bool IsRuntimeKey(string key)
    {
        if (string.IsNullOrEmpty(key)) return false;
        if (key == HeroGuid || key == HeroName) return true;
        if (FindSkinSpec(key) != null) return true;
        return key == AttackGuid || key == AttackName ||
               key == AttackInstanceGuid || key == AttackInstanceName ||
               key == AttackCritInstanceGuid || key == AttackCritInstanceName;
    }

    public static void RefreshHeroPresentationFast()
    {
        if (!_registered || HeroPrefab == null || !AreSkinResourcesReady()) return;
        try
        {
            Sprite portrait = DariusPrototypeIcons.Get("HERO");
            if (portrait != null) TryAssignIfCompatible(HeroPrefab, "icon", portrait);
            TryAssignIfCompatible(HeroPrefab, "mainColor", new Color(0.36f, 0.075f, 0.055f, 1f));
            for (int i = 0; i < SkinSpecs.Length; i++)
            {
                Skin skin;
                if (!SkinsByName.TryGetValue(SkinSpecs[i].name, out skin) || skin == null) continue;
                Sprite preview = DariusPrototypeIcons.Get(SkinSpecs[i].previewIconKey);
                if (preview != null) TryAssignIfCompatible(skin, "previewImage", preview);
            }
            RepairHeroCosmeticContract(HeroPrefab);
            ValidateNativeCosmeticIconContract("fast-refresh");
        }
        catch (Exception e)
        {
            DariusLog.Exception("HERO-PRESENTATION", e, "Fast Hero_Darius presentation refresh failed");
        }
    }
}