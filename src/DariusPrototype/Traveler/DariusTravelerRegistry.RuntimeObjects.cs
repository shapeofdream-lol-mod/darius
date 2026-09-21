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
        DariusNativeModelAssets.Unload();
        DariusLog.Info("TRAVELER", "Runtime Hero_Darius resources unregistered; persistent profile data left untouched.");
    }
}