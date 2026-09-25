using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;

public static partial class DariusFormalRegistry
{
    private static void RemoveDatabaseMapIfOwned(object database, string fieldName, object key, object expectedValue)
    {
        try { DariusRuntimeResourceDatabaseBridge.RemoveMapIfOwned(database, fieldName, key, expectedValue); }
        catch { }
    }

    private static void RegisterNetworkIdentity(UnityEngine.Object obj, string guid)
    {
        Component component = obj as Component;
        if (component == null) return;

        NetworkIdentity identity = component.GetComponent<NetworkIdentity>();
        if (identity == null)
        {
            try
            {
                identity = component.gameObject.AddComponent<NetworkIdentity>();
                DariusLog.DebugInfo("NET", "Added NetworkIdentity to " + obj.name);
            }
            catch (Exception e)
            {
                DariusLog.Exception("NET", e, "Could not add NetworkIdentity to " + obj.name);
                return;
            }
        }

        uint assetId = StableAssetId(guid) | 0x80000000u;
        DariusRuntimeNetworkBridge.ConfigureTemplateIdentity(identity, assetId, obj.name);

        string collision;
        if (DewResources.database.netObjectAssetIdToGuid.TryGetValue(assetId, out collision) && collision != guid)
            throw new InvalidOperationException("Mirror assetId collision: " + assetId + " already maps to " + collision);

        try
        {
            DewResources.database.netObjectAssetIdToGuid[assetId] = guid;
            DariusLog.Info("NET", "Registered network mapping name=" + obj.name + " assetId=" + assetId + " guid=" + guid);
        }
        catch (Exception e)
        {
            DariusLog.Exception("NET", e, "Failed DB network mapping for " + obj.name);
        }

        NetworkPrefabs[assetId] = component.gameObject;

        try
        {
            NetworkClient.RegisterSpawnHandler(assetId, SpawnHandler, UnspawnHandler);
            RuntimeRegistration registration;
            if (RegistrationsByGuid.TryGetValue(guid, out registration))
                registration.networkHandlerRegistered = true;
            DariusLog.Info("NET", "Registered spawn handler name=" + obj.name + " assetId=" + assetId);
        }
        catch (Exception e)
        {
            DariusLog.Exception("NET", e, "Could not register spawn handler for " + obj.name);
        }
    }

    private static GameObject SpawnHandler(SpawnMessage msg)
    {
        GameObject prefab;
        if (!NetworkPrefabs.TryGetValue(msg.assetId, out prefab) || prefab == null)
        {
            DariusLog.Error("NET-SPAWN", "Missing network prefab for assetId=" + msg.assetId + " pos=" + DariusLog.Vec(msg.position));
            return null;
        }

        GameObject spawned = null;
        try
        {
            spawned = UnityEngine.Object.Instantiate(prefab, msg.position, msg.rotation);
            if (spawned != null)
            {
                spawned.transform.localScale = msg.scale;
                spawned.name = prefab.name;
                // Mirror applies the SpawnMessage and owns the clone's live NetworkIdentity state.
                // Do not rewrite scene/private flags or rebuild NetworkBehaviours after Instantiate.
                if (!spawned.activeSelf) spawned.SetActive(true);
            }
            DariusLog.Info("NET-SPAWN", "Spawn handler assetId=" + msg.assetId + " prefab=" + prefab.name +
                " result=" + (spawned != null) + " pos=" + DariusLog.Vec(msg.position));
            return spawned;
        }
        catch (Exception e)
        {
            DariusLog.Exception("NET-SPAWN", e, "Spawn handler failed assetId=" + msg.assetId + " prefab=" + prefab.name);
            return null;
        }
    }

    private static void UnspawnHandler(GameObject spawned)
    {
        if (spawned != null)
        {
            DariusLog.DebugInfo("NET-SPAWN", "Unspawn " + spawned.name + "#" + spawned.GetInstanceID());
            SpawnManager.Destroy(spawned);
        }
    }

    public static bool TryGetNetworkPrefab(uint assetId, out GameObject prefab)
    {
        return NetworkPrefabs.TryGetValue(assetId, out prefab) && prefab != null;
    }

    public static bool TryGetResourceByExactType(Type type, out UnityEngine.Object obj)
    {
        obj = null;
        if (type == null) return false;
        foreach (UnityEngine.Object candidate in ResourcesByGuid.Values)
        {
            if (candidate != null && candidate.GetType() == type)
            {
                obj = candidate;
                return true;
            }
        }
        return false;
    }
}