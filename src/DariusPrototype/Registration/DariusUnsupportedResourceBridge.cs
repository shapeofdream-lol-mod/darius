using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Mirror;
using UnityEngine;

// The only place in the Darius mod that may touch undocumented Dew resource indexes,
// Dew's private type-cache backing lists, or Mirror's runtime-template internals.
//
// Shape of Dreams currently exposes resource lookup and read-only type discovery, but no
// public API for registering a brand-new Hero/Skill/Star runtime resource. Keep this bridge
// narrow and fail loudly when those private contracts change.
internal static class DariusUnsupportedResourceBridge
{
    private static IDictionary GetInstanceMap(object owner, string fieldName)
    {
        if (owner == null) return null;
        FieldInfo field = owner.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        return field != null ? field.GetValue(owner) as IDictionary : null;
    }

    private static IList GetInstanceList(object owner, string fieldName)
    {
        if (owner == null) return null;
        FieldInfo field = owner.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        return field != null ? field.GetValue(owner) as IList : null;
    }

    private static IList GetStaticTypeList(string fieldName)
    {
        FieldInfo field = typeof(Dew).GetField(
            fieldName,
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        return field != null ? field.GetValue(null) as IList : null;
    }

    private static void EnsureDewTypeCacheInitialized(string fieldName)
    {
        if (fieldName == "_allHeroes") { int _ = Dew.allHeroes.Count; return; }
        if (fieldName == "_allSkills") { int _ = Dew.allSkills.Count; return; }
        if (fieldName == "_allHeroSkills") { int _ = Dew.allHeroSkills.Count; return; }
        if (fieldName == "_allStarTypes") { int _ = Dew.allStarTypes.Count; return; }
        throw new ArgumentOutOfRangeException(nameof(fieldName), fieldName, "Unsupported Dew type cache.");
    }

    private static void RequireMapValue(object database, string fieldName, object key, object value)
    {
        if (key == null) throw new ArgumentNullException(nameof(key));
        IDictionary map = GetInstanceMap(database, fieldName);
        if (map == null) throw new MissingMemberException(database != null ? database.GetType().FullName : "<null>", fieldName);
        map[key] = value;
    }

    private static void RequireListValue(object database, string fieldName, object value)
    {
        IList list = GetInstanceList(database, fieldName);
        if (list == null) throw new MissingMemberException(database != null ? database.GetType().FullName : "<null>", fieldName);
        if (!list.Contains(value)) list.Add(value);
    }

    internal static bool RemoveMapIfOwned(object database, string fieldName, object key, object expectedValue)
    {
        if (database == null || key == null) return false;
        IDictionary map = GetInstanceMap(database, fieldName);
        if (map == null || !map.Contains(key) || !Equals(map[key], expectedValue)) return false;
        map.Remove(key);
        return true;
    }

    private static bool RemoveListValue(object database, string fieldName, object value)
    {
        IList list = GetInstanceList(database, fieldName);
        if (list == null || !list.Contains(value)) return false;
        list.Remove(value);
        return true;
    }

    internal static void RegisterTypedResourceIdentity(
        object database,
        Type type,
        string name,
        string guid,
        UnityEngine.Object component = null,
        GameObject gameObject = null)
    {
        if (database == null) throw new InvalidOperationException("DewResources.database is null.");
        if (type == null) throw new ArgumentNullException(nameof(type));
        if (string.IsNullOrEmpty(name)) throw new ArgumentException("Resource name is required.", nameof(name));
        if (string.IsNullOrEmpty(guid)) throw new ArgumentException("Resource guid is required.", nameof(guid));

        string aqn = type.AssemblyQualifiedName;
        if (string.IsNullOrEmpty(aqn))
            throw new InvalidOperationException("Could not resolve AssemblyQualifiedName for " + name);

        RequireMapValue(database, "typeAssemblyQualifiedNameToGuid", aqn, guid);
        RequireListValue(database, "allGuids", guid);
        RequireMapValue(database, "typeToGuid", type, guid);
        RequireMapValue(database, "guidToType", guid, type);
        RequireMapValue(database, "typeNameToGuid", type.Name, guid);
        RequireMapValue(database, "typeNameToType", type.Name, type);
        RequireMapValue(database, "nameToGuid", name, guid);
        RequireMapValue(database, "guidToName", guid, name);

        if (component != null) RequireMapValue(database, "objectToGuidFallback", component, guid);
        if (gameObject != null) RequireMapValue(database, "objectToGuidFallback", gameObject, guid);
    }

    internal static void RegisterNamedResourceIdentity(
        object database,
        string name,
        string guid,
        UnityEngine.Object component = null,
        GameObject gameObject = null)
    {
        if (database == null) throw new InvalidOperationException("DewResources.database is null.");
        if (string.IsNullOrEmpty(name)) throw new ArgumentException("Resource name is required.", nameof(name));
        if (string.IsNullOrEmpty(guid)) throw new ArgumentException("Resource guid is required.", nameof(guid));

        RequireListValue(database, "allGuids", guid);
        RequireMapValue(database, "nameToGuid", name, guid);
        RequireMapValue(database, "guidToName", guid, name);
        if (component != null) RequireMapValue(database, "objectToGuidFallback", component, guid);
        if (gameObject != null) RequireMapValue(database, "objectToGuidFallback", gameObject, guid);
    }

    internal static void RemoveTypedResourceIdentity(object database, Type type, string name, string guid)
    {
        if (database == null || type == null || string.IsNullOrEmpty(guid)) return;
        string aqn = type.AssemblyQualifiedName;
        RemoveMapIfOwned(database, "typeAssemblyQualifiedNameToGuid", aqn, guid);
        RemoveMapIfOwned(database, "typeToGuid", type, guid);
        RemoveMapIfOwned(database, "guidToType", guid, type);
        RemoveMapIfOwned(database, "typeNameToGuid", type.Name, guid);
        RemoveMapIfOwned(database, "typeNameToType", type.Name, type);
        RemoveMapIfOwned(database, "nameToGuid", name, guid);
        RemoveMapIfOwned(database, "guidToName", guid, name);
        RemoveListValue(database, "allGuids", guid);
    }

    internal static void RemoveNamedResourceIdentity(object database, string name, string guid)
    {
        if (database == null || string.IsNullOrEmpty(guid)) return;
        RemoveMapIfOwned(database, "nameToGuid", name, guid);
        RemoveMapIfOwned(database, "guidToName", guid, name);
        RemoveListValue(database, "allGuids", guid);
    }

    internal static void RemoveObjectGuidIfOwned(object database, UnityEngine.Object obj, string guid)
    {
        if (ReferenceEquals(obj, null) || string.IsNullOrEmpty(guid)) return;
        RemoveMapIfOwned(database, "objectToGuidFallback", obj, guid);
    }

    internal static void RegisterNetworkGuid(object database, uint assetId, string guid)
    {
        IDictionary map = GetInstanceMap(database, "netObjectAssetIdToGuid");
        if (map == null) throw new MissingMemberException(database != null ? database.GetType().FullName : "<null>", "netObjectAssetIdToGuid");
        if (map.Contains(assetId))
        {
            object existing = map[assetId];
            if (!Equals(existing, guid))
                throw new InvalidOperationException("Mirror assetId collision: " + assetId + " already maps to " + existing);
        }
        map[assetId] = guid;
    }

    internal static void RemoveNetworkGuidIfOwned(object database, uint assetId, string guid)
    {
        RemoveMapIfOwned(database, "netObjectAssetIdToGuid", assetId, guid);
    }

    internal static bool IsNetworkGuidMapped(object database, uint assetId, string guid)
    {
        IDictionary map = GetInstanceMap(database, "netObjectAssetIdToGuid");
        return map != null && map.Contains(assetId) && Equals(map[assetId], guid);
    }

    private static void EnsureDewTypeCacheEntries(string fieldName, IEnumerable<Type> types, string logTag)
    {
        EnsureDewTypeCacheInitialized(fieldName);
        IList list = GetStaticTypeList(fieldName);
        if (list == null) throw new MissingMemberException(typeof(Dew).FullName, fieldName);

        Type[] desired = types != null ? types.Where(t => t != null).Distinct().ToArray() : Array.Empty<Type>();
        int staleRemoved = 0;
        int added = 0;

        foreach (Type type in desired)
        {
            bool haveCurrent = false;
            for (int i = list.Count - 1; i >= 0; i--)
            {
                Type existing = list[i] as Type;
                if (existing == null || !string.Equals(existing.FullName, type.FullName, StringComparison.Ordinal)) continue;
                if (existing == type && !haveCurrent)
                {
                    haveCurrent = true;
                    continue;
                }
                list.RemoveAt(i);
                staleRemoved++;
            }

            if (!haveCurrent)
            {
                list.Add(type);
                added++;
            }
        }

        if (added > 0 || staleRemoved > 0)
            DariusLog.Info(logTag ?? "UNSUPPORTED-TYPE",
                fieldName + " added=" + added + " staleRemoved=" + staleRemoved + " count=" + list.Count);
    }

    private static void RemoveDewTypeCacheEntries(string fieldName, IEnumerable<Type> types, string logTag)
    {
        try
        {
            EnsureDewTypeCacheInitialized(fieldName);
            IList list = GetStaticTypeList(fieldName);
            if (list == null) return;

            HashSet<string> fullNames = new HashSet<string>(
                (types ?? Array.Empty<Type>())
                    .Where(t => t != null && !string.IsNullOrEmpty(t.FullName))
                    .Select(t => t.FullName),
                StringComparer.Ordinal);

            int removed = 0;
            for (int i = list.Count - 1; i >= 0; i--)
            {
                Type existing = list[i] as Type;
                if (existing == null || string.IsNullOrEmpty(existing.FullName) || !fullNames.Contains(existing.FullName)) continue;
                list.RemoveAt(i);
                removed++;
            }

            if (removed > 0)
                DariusLog.Info(logTag ?? "UNSUPPORTED-TYPE", fieldName + " removed=" + removed + " count=" + list.Count);
        }
        catch (Exception e)
        {
            DariusLog.Exception(logTag ?? "UNSUPPORTED-TYPE", e, "Failed to remove Dew type-cache entries from " + fieldName);
        }
    }

    internal static void EnsureHeroTypes(IEnumerable<Type> types, string logTag)
    {
        EnsureDewTypeCacheEntries("_allHeroes", types, logTag);
    }

    internal static void EnsureSkillTypes(IEnumerable<Type> types, string logTag)
    {
        EnsureDewTypeCacheEntries("_allSkills", types, logTag);
    }

    internal static void EnsureHeroSkillTypes(IEnumerable<Type> types, string logTag)
    {
        EnsureDewTypeCacheEntries("_allHeroSkills", types, logTag);
    }

    internal static void EnsureStarTypes(IEnumerable<Type> types, string logTag)
    {
        EnsureDewTypeCacheEntries("_allStarTypes", types, logTag);
    }

    internal static void RemoveHeroTypes(IEnumerable<Type> types, string logTag)
    {
        RemoveDewTypeCacheEntries("_allHeroes", types, logTag);
    }

    internal static void RemoveSkillTypes(IEnumerable<Type> types, string logTag)
    {
        RemoveDewTypeCacheEntries("_allSkills", types, logTag);
    }

    internal static void RemoveHeroSkillTypes(IEnumerable<Type> types, string logTag)
    {
        RemoveDewTypeCacheEntries("_allHeroSkills", types, logTag);
    }

    internal static void RemoveStarTypes(IEnumerable<Type> types, string logTag)
    {
        RemoveDewTypeCacheEntries("_allStarTypes", types, logTag);
    }

    internal static void ConfigureTemplateIdentity(NetworkIdentity identity, uint assetId, string label)
    {
        if (identity == null) return;

        try
        {
            FieldInfo field = typeof(NetworkIdentity).GetField("_assetId", BindingFlags.Instance | BindingFlags.NonPublic)
                           ?? typeof(NetworkIdentity).GetField("assetId", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                           ?? typeof(NetworkIdentity).GetField("<assetId>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
            if (field != null)
            {
                field.SetValue(identity, assetId);
            }
            else
            {
                PropertyInfo property = typeof(NetworkIdentity).GetProperty(
                    "assetId",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (property != null && property.CanWrite) property.SetValue(identity, assetId, null);
                else throw new MissingMemberException(typeof(NetworkIdentity).FullName, "assetId");
            }

            identity.sceneId = 0UL;

            FieldInfo sceneField = typeof(NetworkIdentity).GetField(
                "_isSceneObject",
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (sceneField != null) sceneField.SetValue(identity, false);

            FieldInfo spawnedField = typeof(NetworkIdentity).GetField(
                "hasSpawned",
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (spawnedField != null) spawnedField.SetValue(identity, false);
        }
        catch (Exception e)
        {
            DariusLog.Exception("UNSUPPORTED-NET", e,
                "Could not configure runtime template identity label=" + label + " assetId=" + assetId);
            throw;
        }
    }

    internal static void RebuildNetworkBehaviours(NetworkIdentity identity, string label)
    {
        if (identity == null) return;
        try
        {
            MethodInfo method = typeof(NetworkIdentity).GetMethod(
                "InitializeNetworkBehaviours",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (method == null)
                throw new MissingMethodException(typeof(NetworkIdentity).FullName, "InitializeNetworkBehaviours");
            method.Invoke(identity, null);
        }
        catch (Exception e)
        {
            DariusLog.Exception("UNSUPPORTED-NET", e, "Could not rebuild NetworkBehaviour cache label=" + label);
            throw;
        }
    }
}
