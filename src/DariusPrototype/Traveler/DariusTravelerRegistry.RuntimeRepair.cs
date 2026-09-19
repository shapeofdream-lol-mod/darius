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
    private static void ValidateNativeCosmeticIconContract(string reason)
    {
        try
        {
            object heroIcon = HeroPrefab != null ? ReadMemberValue(HeroPrefab, "icon") : null;
            object mainColor = HeroPrefab != null ? ReadMemberValue(HeroPrefab, "mainColor") : null;
            int ready = 0;
            for (int i = 0; i < SkinSpecs.Length; i++)
            {
                Skin skin;
                object preview = null;
                if (SkinsByName.TryGetValue(SkinSpecs[i].name, out skin) && skin != null)
                    preview = ReadMemberValue(skin, "previewImage");
                if (preview != null) ready++;
                else DariusLog.Error("SKIN-ICON", "Native previewImage missing skin=" + SkinSpecs[i].name + " reason=" + reason);
            }
            DariusLog.Info("HERO-ICON-CONTRACT", "reason=" + reason + " hero.icon=" + (heroIcon != null) +
                " mainColor=" + (mainColor != null) + " skin.previewImage=" + ready + "/" + SkinSpecs.Length);
        }
        catch (Exception e)
        {
            DariusLog.Exception("HERO-ICON-CONTRACT", e, "Native cosmetic icon validation failed reason=" + reason);
        }
    }

    public static void RepairRuntimeRegistration(string reason)
    {
        if (_repairing || !_registered || DewResources.database == null) return;
        _repairing = true;
        try
        {
            // A completed run can destroy the runtime Hero prefab itself while leaving the Skin,
            // formal Skills and this static registry alive. In that state merely re-inserting the
            // old reference into Dew maps cannot work because Unity destroyed objects compare null.
            // Recreate any missing runtime prefab first, then rebuild every lookup bridge.
            EnsurePersistentRuntimeObjects(reason ?? "runtime repair");
            if (HeroPrefab == null || DefaultSkin == null)
            {
                DariusLog.Error("TRAVELER-SELFHEAL", "Runtime repair could not restore required Hero/Skin resources reason=" + (reason ?? "<unknown>"));
                return;
            }

            // Dew/BuildProfile caches and parts of the runtime database are rebuilt when leaving a
            // run. Reassert Hero/Skin/type/content/network/profile paths after every rebuild beat.
            RegisterTypes();
            RegisterContent(DewBuildProfile.current != null ? DewBuildProfile.current.content : null);

            Type heroType = typeof(Hero_Darius);
            string aqn = heroType.AssemblyQualifiedName;
            DewResources.database.typeAssemblyQualifiedNameToGuid[aqn] = HeroGuid;
            if (!DewResources.database.allGuids.Contains(HeroGuid)) DewResources.database.allGuids.Add(HeroGuid);
            for (int si = 0; si < SkinSpecs.Length; si++)
                if (!DewResources.database.allGuids.Contains(SkinSpecs[si].guid)) DewResources.database.allGuids.Add(SkinSpecs[si].guid);
            SetDatabaseMap("typeToGuid", heroType, HeroGuid);
            SetDatabaseMap("guidToType", HeroGuid, heroType);
            SetDatabaseMap("typeNameToGuid", HeroName, HeroGuid);
            SetDatabaseMap("typeNameToType", HeroName, heroType);
            SetDatabaseMap("nameToGuid", HeroName, HeroGuid);
            SetDatabaseMap("guidToName", HeroGuid, HeroName);
            SetDatabaseMap("objectToGuidFallback", HeroPrefab, HeroGuid);
            SetDatabaseMap("objectToGuidFallback", HeroPrefab.gameObject, HeroGuid);

            for (int si = 0; si < SkinSpecs.Length; si++)
            {
                DariusSkinSpec spec = SkinSpecs[si]; Skin skin;
                if (!SkinsByName.TryGetValue(spec.name, out skin) || skin == null) continue;
                SetDatabaseMap("nameToGuid", spec.name, spec.guid);
                SetDatabaseMap("guidToName", spec.guid, spec.name);
                SetDatabaseMap("objectToGuidFallback", skin, spec.guid);
                SetDatabaseMap("objectToGuidFallback", skin.gameObject, spec.guid);
                ResourcesByGuid[spec.guid] = skin;
            }

            ResourcesByGuid[HeroGuid] = HeroPrefab;
            ResourcesByType[heroType] = HeroPrefab;
            DewResources.database.netObjectAssetIdToGuid[HeroAssetId] = HeroGuid;
            NetworkPrefabs[HeroAssetId] = HeroPrefab.gameObject;
            try { NetworkClient.RegisterSpawnHandler(HeroAssetId, SpawnHandler, UnspawnHandler); } catch { }

            ReassertTypedRuntimeResource(AttackPrefab, AttackName, AttackGuid, AttackAssetId);
            ReassertTypedRuntimeResource(AttackInstancePrefab, AttackInstanceName, AttackInstanceGuid, AttackInstanceAssetId);
            ReassertTypedRuntimeResource(AttackCritInstancePrefab, AttackCritInstanceName, AttackCritInstanceGuid, AttackCritInstanceAssetId);

            DewProfile profile = DewSave.profileMain;
            RepairLegacySelectedSkinAliases(profile, reason ?? "runtime repair");
            DariusNativeAttackBinder prefabBinder = HeroPrefab != null ? HeroPrefab.GetComponent<DariusNativeAttackBinder>() : null;
            if (prefabBinder != null) prefabBinder.EnsureBound("RepairRuntimeRegistration");

            if (_presentationGenerationRebuilt)
            {
                // Existing lobby/reward CharacterModelDisplay instances can already hold AssetRefs
                // to the destroyed generation. Re-run Dew's authoritative repair transaction only
                // when we actually replaced the presentation generation.
                NotifyDewMissingReferenceRepair();
                _presentationGenerationRebuilt = false;
            }

            DariusLog.DebugInfoThrottled("TRAVELER-REPAIR", reason ?? "<unknown>", "Reasserted Hero_Darius + Darius native attack resource/type/network/profile bridges reason=" + (reason ?? "<unknown>"), 15.0);
        }
        catch (Exception e)
        {
            DariusLog.Exception("TRAVELER-REPAIR", e, "Runtime registration repair failed reason=" + (reason ?? "<unknown>"));
        }
        finally
        {
            _repairing = false;
        }
    }

    private static void RebuildSkinPresentationGeneration(string reason)
    {
        List<GameObject> stale = new List<GameObject>();
        foreach (Skin skin in SkinsByName.Values)
        {
            if (skin != null && skin.gameObject != null && !stale.Contains(skin.gameObject)) stale.Add(skin.gameObject);
        }
        SkinsByName.Clear();
        DefaultSkin = null; GodKingSkin = null; DunkmasterSkin = null; MechaSkin = null;
        for (int i = 0; i < stale.Count; i++)
        {
            GameObject go = stale[i];
            OwnedObjects.Remove(go);
            if (go != null)
            {
                try { UnityEngine.Object.Destroy(go); } catch { }
            }
        }
        CreateAndRegisterSkin();
        _presentationGenerationRebuilt = true;
        DariusLog.Warn("TRAVELER-SELFHEAL", "Rebuilt complete Skin/EntityModel presentation generation after teardown reason=" + reason + " stale=" + stale.Count);
    }
}