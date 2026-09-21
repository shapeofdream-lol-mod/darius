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
            // Scene/profile/content repair is allowed to restore lookup mappings only.
            // It must never create a new Hero/Skin/Attack generation: doing that while an old
            // lobby/title UI is still tearing down can mount a fresh presentation instance into
            // the outgoing UI hierarchy. Resource creation belongs exclusively to ModBehaviour
            // bootstrap (EnsureCoreRegisteredForBootstrap/Register).
            if (HeroPrefab == null || DefaultSkin == null || !AreSkinResourcesReady() ||
                AttackPrefab == null || AttackInstancePrefab == null || AttackCritInstancePrefab == null)
            {
                DariusLog.DebugInfoThrottled(
                    "TRAVELER-REPAIR",
                    "missing-generation",
                    "Skipped registration reassert because the current Traveler resource generation is incomplete; bootstrap owns recreation. reason=" +
                    (reason ?? "<unknown>"),
                    2.0);
                return;
            }

            // Dew/BuildProfile caches and parts of the runtime database can be rebuilt around
            // scene transitions. Reassert mappings for the existing generation only.
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


}