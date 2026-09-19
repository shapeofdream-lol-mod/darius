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
    private static void EnsurePersistentRuntimeObjects(string reason)
    {
        // Remove Unity-destroyed entries from our ownership list so later application-quit cleanup
        // only walks live objects. UnityEngine.Object's overloaded == makes destroyed GameObjects
        // compare null even though the managed wrapper still exists.
        OwnedObjects.RemoveAll(go => go == null);
        CreateResourceRoot();
        ConfigureDariusSkillOwnership();

        bool rebuiltAttack = false;
        bool rebuiltSkin = false;
        bool rebuiltHero = false;

        // Hero construction depends on the Darius attack preset. Rebuild the complete attack chain
        // only if a teardown unexpectedly removed any of it; the normal first-run bug destroys only
        // Hero_Darius, so this branch should ordinarily remain untouched.
        if (AttackPrefab == null || AttackInstancePrefab == null || AttackCritInstancePrefab == null)
        {
            DariusLog.Warn("TRAVELER-SELFHEAL", "Darius native attack runtime prefab missing; rebuilding attack chain reason=" + reason);
            CreateAndRegisterNativeBasicAttack();
            rebuiltAttack = true;
        }

        bool heroPresentationWasDestroyed = HeroPrefab == null;
        if (heroPresentationWasDestroyed)
        {
            // The observed run teardown destroys Hero_* and invalidates the Skin -> EntityModel
            // presentation generation in the same transition even when Skin wrappers still compare
            // non-null. Rebuild the whole generation transactionally before constructing the Hero.
            DariusLog.Warn("TRAVELER-SELFHEAL", "Hero_Darius runtime prefab was destroyed; invalidating Skin presentation generation reason=" + reason);
            RebuildSkinPresentationGeneration(reason);
            rebuiltSkin = true;
        }
        else if (!AreSkinResourcesReady())
        {
            DariusLog.Warn("TRAVELER-SELFHEAL", "One or more Darius Skin/EntityModel presentation resources are stale; rebuilding full skin generation reason=" + reason);
            RebuildSkinPresentationGeneration(reason);
            rebuiltSkin = true;
            RepairHeroCosmeticContract(HeroPrefab);
        }

        if (HeroPrefab == null)
        {
            DariusLog.Warn("TRAVELER-SELFHEAL", "Hero_Darius runtime prefab was destroyed by run teardown; rebuilding from native structural template reason=" + reason);
            CreateAndRegisterHero();
            rebuiltHero = true;
        }

        if (rebuiltAttack || rebuiltSkin || rebuiltHero)
        {
            CreateLifecycleBridge();
            DariusLog.Info("TRAVELER-SELFHEAL", "Rebuilt runtime resources hero=" + rebuiltHero +
                " skin=" + rebuiltSkin + " attack=" + rebuiltAttack +
                " heroInstanceId=" + (HeroPrefab != null ? HeroPrefab.GetInstanceID().ToString() : "<null>") +
                " reason=" + reason);
        }
    }

    private static void ReassertTypedRuntimeResource(Component component, string name, string guid, uint assetId)
    {
        if (component == null || DewResources.database == null) return;
        Type type = component.GetType();
        string aqn = type.AssemblyQualifiedName;
        DewResources.database.typeAssemblyQualifiedNameToGuid[aqn] = guid;
        if (!DewResources.database.allGuids.Contains(guid)) DewResources.database.allGuids.Add(guid);
        SetDatabaseMap("typeToGuid", type, guid);
        SetDatabaseMap("guidToType", guid, type);
        SetDatabaseMap("typeNameToGuid", type.Name, guid);
        SetDatabaseMap("typeNameToType", type.Name, type);
        SetDatabaseMap("nameToGuid", name, guid);
        SetDatabaseMap("guidToName", guid, name);
        SetDatabaseMap("objectToGuidFallback", component, guid);
        SetDatabaseMap("objectToGuidFallback", component.gameObject, guid);
        ResourcesByGuid[guid] = component;
        ResourcesByType[type] = component;
        DewResources.database.netObjectAssetIdToGuid[assetId] = guid;
        NetworkPrefabs[assetId] = component.gameObject;
        try { NetworkClient.RegisterSpawnHandler(assetId, SpawnHandler, UnspawnHandler); } catch { }
    }

    private static string RuntimeGuidForAssetId(uint assetId)
    {
        if (assetId == HeroAssetId) return HeroGuid;
        if (assetId == AttackAssetId) return AttackGuid;
        if (assetId == AttackInstanceAssetId) return AttackInstanceGuid;
        if (assetId == AttackCritInstanceAssetId) return AttackCritInstanceGuid;
        return null;
    }

    public static void UnregisterRuntimeOnly()
    {
        _registered = false;
        _registering = false;
        foreach (uint id in NetworkPrefabs.Keys.ToArray())
        {
            try { NetworkClient.UnregisterSpawnHandler(id); } catch { }
            try
            {
                string expected = RuntimeGuidForAssetId(id);
                string existing;
                if (!string.IsNullOrEmpty(expected) && DewResources.database != null &&
                    DewResources.database.netObjectAssetIdToGuid.TryGetValue(id, out existing) && existing == expected)
                    DewResources.database.netObjectAssetIdToGuid.Remove(id);
            }
            catch { }
        }
        NetworkPrefabs.Clear();

        RemoveTypeFromDew("_allHeroes", typeof(Hero_Darius));
        RemoveTypeFromDew("_allSkills", typeof(St_Darius_Decimate));
        RemoveTypeFromDew("_allSkills", typeof(St_Darius_NoxianGuillotine));
        RemoveTypeFromDew("_allSkills", typeof(St_D_Darius_Hemorrhage));
        RemoveTypeFromDew("_allSkills", typeof(St_Darius_CripplingStrike));
        RemoveTypeFromDew("_allSkills", typeof(St_Darius_Apprehend));
        RemoveTypeFromDew("_allSkills", typeof(St_Darius_Flash));
        RemoveTypeFromDew("_allSkills", typeof(St_Darius_Ghost));
        RemoveTypeFromDew("_allHeroSkills", typeof(St_Darius_Decimate));
        RemoveTypeFromDew("_allHeroSkills", typeof(St_Darius_NoxianGuillotine));
        RemoveTypeFromDew("_allHeroSkills", typeof(St_D_Darius_Hemorrhage));
        RemoveTypeFromDew("_allHeroSkills", typeof(St_Darius_Flash));
        RemoveTypeFromDew("_allHeroSkills", typeof(St_Darius_Ghost));

        // Only remove maps where our exact value is still installed. User profile data is intentionally preserved.
        for (int si = 0; si < SkinSpecs.Length; si++)
        {
            RemoveDatabaseMappingIfOwned("nameToGuid", SkinSpecs[si].name, SkinSpecs[si].guid);
            RemoveDatabaseMappingIfOwned("guidToName", SkinSpecs[si].guid, SkinSpecs[si].name);
        }
        RemoveTypedMappings(typeof(Hero_Darius), HeroGuid, HeroName);
        RemoveTypedMappings(typeof(At_DariusAxe), AttackGuid, AttackName);
        RemoveTypedMappings(typeof(Ai_DariusAxe), AttackInstanceGuid, AttackInstanceName);
        RemoveTypedMappings(typeof(Ai_DariusAxe_Crit), AttackCritInstanceGuid, AttackCritInstanceName);
        List<string> runtimeGuids = new List<string> { HeroGuid, AttackGuid, AttackInstanceGuid, AttackCritInstanceGuid };
        for (int si = 0; si < SkinSpecs.Length; si++) runtimeGuids.Add(SkinSpecs[si].guid);
        for (int i = 0; i < runtimeGuids.Count; i++)
        {
            try { if (DewResources.database != null && DewResources.database.allGuids.Contains(runtimeGuids[i])) DewResources.database.allGuids.Remove(runtimeGuids[i]); } catch { }
        }

        foreach (GameObject go in OwnedObjects)
            if (go != null) UnityEngine.Object.Destroy(go);
        OwnedObjects.Clear();
        ResourcesByGuid.Clear();
        ResourcesByType.Clear();
        HeroPrefab = null;
        DefaultSkin = null;
        GodKingSkin = null;
        DunkmasterSkin = null;
        MechaSkin = null;
        SkinsByName.Clear();
        AttackPrefab = null;
        AttackInstancePrefab = null;
        AttackCritInstancePrefab = null;
        if (_resourceRoot != null) UnityEngine.Object.Destroy(_resourceRoot);
        _resourceRoot = null;
        DariusNativeEntityAnimationLease.Clear();
        DariusNativeModelAssets.Unload();
        DariusLog.Info("TRAVELER", "Runtime Hero_Darius resources unregistered; persistent profile data left untouched.");
    }
}