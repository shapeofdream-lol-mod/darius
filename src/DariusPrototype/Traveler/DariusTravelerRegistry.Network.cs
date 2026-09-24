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

    internal static bool TryGetRuntimeNetworkTemplateLabel(GameObject go, out string label)
    {
        label = null;
        if (go == null) return false;
        if (HeroPrefab != null && ReferenceEquals(go, HeroPrefab.gameObject)) { label = HeroName; return true; }
        if (AttackPrefab != null && ReferenceEquals(go, AttackPrefab.gameObject)) { label = AttackName; return true; }
        if (AttackInstancePrefab != null && ReferenceEquals(go, AttackInstancePrefab.gameObject)) { label = AttackInstanceName; return true; }
        if (AttackCritInstancePrefab != null && ReferenceEquals(go, AttackCritInstancePrefab.gameObject)) { label = AttackCritInstanceName; return true; }
        return false;
    }

    internal static void LogRuntimeNetworkTemplateStates(string reason)
    {
        LogRuntimeNetworkTemplateState(HeroPrefab != null ? HeroPrefab.gameObject : null, HeroName, reason);
        LogRuntimeNetworkTemplateState(AttackPrefab != null ? AttackPrefab.gameObject : null, AttackName, reason);
        LogRuntimeNetworkTemplateState(AttackInstancePrefab != null ? AttackInstancePrefab.gameObject : null, AttackInstanceName, reason);
        LogRuntimeNetworkTemplateState(AttackCritInstancePrefab != null ? AttackCritInstancePrefab.gameObject : null, AttackCritInstanceName, reason);
    }

    private static void LogRuntimeNetworkTemplateState(GameObject go, string label, string reason)
    {
        if (go == null)
        {
            DariusLog.Info("TRAVELER-NET-DIAG", reason + " template=" + label + " state=<missing>");
            return;
        }

        NetworkIdentity identity = go.GetComponent<NetworkIdentity>();
        if (identity == null)
        {
            DariusLog.Info("TRAVELER-NET-DIAG", reason + " template=" + label + " identity=<null>");
            return;
        }

        DariusLog.Info("TRAVELER-NET-DIAG",
            reason + " template=" + label +
            " id=" + go.GetInstanceID() +
            " netId=" + identity.netId +
            " assetId=" + identity.assetId +
            " isServer=" + identity.isServer +
            " isClient=" + identity.isClient +
            " activeSelf=" + go.activeSelf +
            " activeInHierarchy=" + go.activeInHierarchy +
            " scene=" + (go.scene.IsValid() ? go.scene.name : "<invalid>") +
            " parent=" + (go.transform.parent != null ? go.transform.parent.name : "<root>") +
            " parentScene=" + (go.transform.parent != null && go.transform.parent.gameObject.scene.IsValid() ? go.transform.parent.gameObject.scene.name : "<none>") +
            " serverSpawned=" + SpawnedContains(typeof(NetworkServer), identity) +
            " clientSpawned=" + SpawnedContains(typeof(NetworkClient), identity));
    }

    private static bool SpawnedContains(Type ownerType, NetworkIdentity identity)
    {
        if (ownerType == null || identity == null) return false;
        const BindingFlags flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        try
        {
            FieldInfo field = ownerType.GetField("spawned", flags);
            IDictionary dictionary = field != null ? field.GetValue(null) as IDictionary : null;
            if (dictionary == null)
            {
                PropertyInfo prop = ownerType.GetProperty("spawned", flags);
                dictionary = prop != null ? prop.GetValue(null, null) as IDictionary : null;
            }
            if (dictionary == null) return false;
            foreach (DictionaryEntry entry in dictionary)
                if (ReferenceEquals(entry.Value, identity)) return true;
        }
        catch { }
        return false;
    }

    internal static bool TryGetRuntimeNetworkTemplateLabel(UnityEngine.Object candidate, out string label)
    {
        label = null;
        if (candidate == null) return false;

        GameObject go = candidate as GameObject;
        if (go == null)
        {
            Component component = candidate as Component;
            if (component != null) go = component.gameObject;
        }
        return TryGetRuntimeNetworkTemplateLabel(go, out label);
    }

    internal static void LogRuntimeNetworkTemplateDestroyBoundary(UnityEngine.Object candidate, string boundary)
    {
        string label;
        if (!TryGetRuntimeNetworkTemplateLabel(candidate, out label)) return;

        GameObject go = candidate as GameObject;
        if (go == null)
        {
            Component component = candidate as Component;
            if (component != null) go = component.gameObject;
        }

        string scene = go != null && go.scene.IsValid() ? go.scene.name : "<invalid>";
        string parent = go != null && go.transform.parent != null ? go.transform.parent.name : "<root>";
        string parentScene = go != null && go.transform.parent != null && go.transform.parent.gameObject.scene.IsValid()
            ? go.transform.parent.gameObject.scene.name
            : "<none>";

        DariusLog.Warn("TRAVELER-DESTROY-DIAG",
            boundary + " template=" + label +
            " scene=" + scene +
            " parent=" + parent +
            " parentScene=" + parentScene +
            " stack=" + Environment.StackTrace);
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