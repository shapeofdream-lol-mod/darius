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
    private static void RegisterTypedResource(Component component, GameObject go, string name, string guid, uint assetId)
    {
        Type type = component.GetType();
        object db = DewResources.database;
        if (db == null) throw new InvalidOperationException("database null");
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
        SetDatabaseMap("objectToGuidFallback", go, guid);
        ResourcesByGuid[guid] = component;
        ResourcesByType[type] = component;

        string collision;
        if (DewResources.database.netObjectAssetIdToGuid.TryGetValue(assetId, out collision) && collision != guid)
            throw new InvalidOperationException("Mirror assetId collision: " + assetId + " already maps to " + collision);
        DewResources.database.netObjectAssetIdToGuid[assetId] = guid;
        NetworkPrefabs[assetId] = go;
        try { NetworkClient.RegisterSpawnHandler(assetId, SpawnHandler, UnspawnHandler); }
        catch (Exception e) { DariusLog.Exception("TRAVELER-NET", e, "RegisterSpawnHandler failed assetId=" + assetId); }
    }

    private static void RegisterNamedResource(Component component, GameObject go, string name, string guid)
    {
        if (!DewResources.database.allGuids.Contains(guid)) DewResources.database.allGuids.Add(guid);
        SetDatabaseMap("nameToGuid", name, guid);
        SetDatabaseMap("guidToName", guid, name);
        SetDatabaseMap("objectToGuidFallback", component, guid);
        SetDatabaseMap("objectToGuidFallback", go, guid);
        ResourcesByGuid[guid] = component;
        // Deliberately do NOT write typeToGuid[typeof(Skin)]: Skin is a shared stock type.
    }

    private static GameObject SpawnHandler(SpawnMessage msg)
    {
        GameObject prefab;
        if (!NetworkPrefabs.TryGetValue(msg.assetId, out prefab) || prefab == null) return null;
        try
        {
            GameObject spawned = UnityEngine.Object.Instantiate(prefab, msg.position, msg.rotation);
            spawned.name = prefab.name;
            spawned.transform.localScale = msg.scale;
            // Mirror owns the live NetworkIdentity spawn state. The old handler rewrote assetId/scene state
            // and rebuilt NetworkBehaviours after Instantiate(), which can corrupt Host-client spawn
            // bookkeeping and disconnect the local client on a basic attack. A custom spawn handler
            // should only construct and return the object; Mirror applies the SpawnMessage afterwards.
            if (!spawned.activeSelf) spawned.SetActive(true);
            DariusLog.Info("TRAVELER-NET", "Client spawned " + prefab.name + " assetId=" + msg.assetId);
            return spawned;
        }
        catch (Exception e)
        {
            DariusLog.Exception("TRAVELER-NET", e, "Hero_Darius spawn handler failed assetId=" + msg.assetId);
            return null;
        }
    }

    private static void UnspawnHandler(GameObject spawned)
    {
        if (spawned != null) SpawnManager.Destroy(spawned);
    }

    private static Dictionary<FieldInfo, object> CaptureUnitySerializedFields(Component source, Type startType)
    {
        Dictionary<FieldInfo, object> values = new Dictionary<FieldInfo, object>();
        Type t = startType;
        while (t != null && typeof(Component).IsAssignableFrom(t))
        {
            foreach (FieldInfo f in t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            {
                if (f.IsStatic || f.IsInitOnly || f.IsNotSerialized) continue;
                bool serialized = f.IsPublic || f.GetCustomAttributes(typeof(SerializeField), true).Length > 0 || f.GetCustomAttributes(typeof(SerializeReference), true).Length > 0;
                if (!serialized) continue;
                try { values[f] = f.GetValue(source); } catch { }
            }
            t = t.BaseType;
        }
        return values;
    }

    private static void RestoreUnitySerializedFields(Component target, Dictionary<FieldInfo, object> values)
    {
        foreach (KeyValuePair<FieldInfo, object> kv in values)
        {
            try { kv.Key.SetValue(target, kv.Value); }
            catch (Exception e) { DariusLog.DebugInfo("TRAVELER-COPY", "Skip field " + kv.Key.DeclaringType.Name + "." + kv.Key.Name + ": " + e.Message); }
        }
    }
}