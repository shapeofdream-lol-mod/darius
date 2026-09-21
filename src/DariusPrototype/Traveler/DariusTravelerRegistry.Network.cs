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

    private static void ConfigureNetworkIdentity(NetworkIdentity identity, uint assetId)
    {
        if (identity == null) return;
        FieldInfo field = typeof(NetworkIdentity).GetField("_assetId", BindingFlags.Instance | BindingFlags.NonPublic)
                       ?? typeof(NetworkIdentity).GetField("assetId", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                       ?? typeof(NetworkIdentity).GetField("<assetId>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
        if (field != null) field.SetValue(identity, assetId);
        else
        {
            PropertyInfo p = typeof(NetworkIdentity).GetProperty("assetId", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (p != null && p.CanWrite) p.SetValue(identity, assetId, null);
        }
        identity.sceneId = 0UL;
        FieldInfo scene = typeof(NetworkIdentity).GetField("_isSceneObject", BindingFlags.Instance | BindingFlags.NonPublic);
        if (scene != null) scene.SetValue(identity, false);
        FieldInfo spawned = typeof(NetworkIdentity).GetField("hasSpawned", BindingFlags.Instance | BindingFlags.NonPublic);
        if (spawned != null) spawned.SetValue(identity, false);
    }

    internal static void ReinitializeNetworkBehaviours(NetworkIdentity identity)
    {
        if (identity == null) return;
        try
        {
            MethodInfo m = typeof(NetworkIdentity).GetMethod("InitializeNetworkBehaviours", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (m != null) m.Invoke(identity, null);
        }
        catch (Exception e) { DariusLog.Exception("TRAVELER-NET", e, "InitializeNetworkBehaviours failed for " + identity.name); }
    }

    private static T ResolveGenericResource<T>(string methodName, string key, bool loadLight) where T : UnityEngine.Object
    {
        MethodInfo[] methods = typeof(DewResources).GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
        foreach (MethodInfo raw in methods)
        {
            if (raw.Name != methodName || !raw.IsGenericMethodDefinition) continue;
            MethodInfo m;
            try { m = raw.MakeGenericMethod(typeof(T)); } catch { continue; }
            ParameterInfo[] ps = m.GetParameters();
            if (ps.Length < 1 || ps[0].ParameterType != typeof(string)) continue;
            object[] args = new object[ps.Length];
            args[0] = key;
            for (int i = 1; i < ps.Length; i++)
            {
                if (ps[i].ParameterType == typeof(bool) && ps[i].Name != null && ps[i].Name.IndexOf("light", StringComparison.OrdinalIgnoreCase) >= 0)
                    args[i] = loadLight;
                else if (ps[i].HasDefaultValue) args[i] = ps[i].DefaultValue;
                else args[i] = ps[i].ParameterType.IsValueType ? Activator.CreateInstance(ps[i].ParameterType) : null;
            }
            try
            {
                T result = m.Invoke(null, args) as T;
                if (result != null) return result;
            }
            catch { }
        }
        return null;
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