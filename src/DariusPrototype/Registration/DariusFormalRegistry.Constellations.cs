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
    public static bool SpawnConstellationE(Hero hero, int level, DewPlayer nativeSpawnPlayer = null)
    {
        if (hero == null || Apprehend == null || !NetworkServer.active) return false;
        try
        {
            DewPlayer player = nativeSpawnPlayer != null && nativeSpawnPlayer.hero == hero ? nativeSpawnPlayer : FindPlayerForHero(hero);
            if (player == null)
            {
                DariusLog.Warn("STAR-STARTER-E", "Skipped Apprehend starter spawn because owning DewPlayer was unavailable.");
                return false;
            }
            string sceneName = SceneManager.GetActiveScene().name;
            if (string.IsNullOrEmpty(sceneName) || !sceneName.StartsWith("Room_", StringComparison.OrdinalIgnoreCase))
            {
                DariusLog.Warn("STAR-STARTER-E", "Rejected Apprehend starter spawn outside a gameplay Room_* scene: " + sceneName);
                return false;
            }
            if (!DariusConstellationRuntime.IsUsableStarterWorldPosition(hero.agentPosition))
            {
                DariusLog.Warn("STAR-STARTER-E", "Rejected Apprehend starter spawn at staging/invalid hero position " + DariusLog.Vec(hero.agentPosition));
                return false;
            }
            Vector3 pos = GoodDropPosition(hero.agentPosition, 1.9f);
            if (!DariusConstellationRuntime.IsUsableStarterWorldPosition(pos)) pos = hero.agentPosition;
            Dew.CreateSkillTrigger<St_Darius_Apprehend>(Apprehend, pos, Mathf.Max(1, level), player, null);
            DariusLog.Info("STAR-STARTER-E", "DejaVu-style CreateSkillTrigger completed level=" + level +
                " heroPos=" + DariusLog.Vec(hero.agentPosition) + " dropPos=" + DariusLog.Vec(pos) + " player=" + player.name);
            return true;
        }
        catch (Exception e)
        {
            DariusLog.Exception("STAR-STARTER-E", e, "Constellation E DejaVu-style spawn failed");
            return false;
        }
    }

    private static DewPlayer FindPlayerForHero(Hero hero)
    {
        if (hero == null) return null;
        try
        {
            DewPlayer[] players = UnityEngine.Object.FindObjectsByType<DewPlayer>(FindObjectsSortMode.None);
            for (int i = 0; i < players.Length; i++)
                if (players[i] != null && players[i].hero == hero) return players[i];
        }
        catch { }
        try
        {
            if (DewPlayer.local != null && DewPlayer.local.hero == hero) return DewPlayer.local;
        }
        catch { }
        return null;
    }

    private static T RegisterPrefab<T>(string name, string guid, Action<T> configure) where T : Component
    {
        GameObject go = new GameObject(name);
        bool warmAbilityInstance = typeof(AbilityInstance).IsAssignableFrom(typeof(T));

        // IMPORTANT for runtime AbilityInstance prefabs: keep the newly-created GameObject active
        // until after AddComponent<T>(). Actor/AbilityInstance initializes internal state from Awake.
        // If the prefab is inactive before AddComponent, Unity clones an un-awakened Actor and
        // Dew.InstantiateActor_Imp sets parentActor before activation, which crashes in
        // Actor.set_parentActor. SkillTrigger/Gem prefabs keep the older inactive construction path.
        if (!warmAbilityInstance) go.SetActive(false);
        go.hideFlags = HideFlags.HideAndDontSave;

        // Mirror NetworkBehaviour caches its NetworkIdentity during component initialization.
        // Current working SoD runtime-prefab mods add NetworkIdentity FIRST, then SkillTrigger/Gem.
        // Adding it afterwards leaves netIdentity null and crashes NetworkIdentity.OnStartServer.
        NetworkIdentity preIdentity = go.AddComponent<NetworkIdentity>();
        uint preAssetId = StableAssetId(guid) | 0x80000000u;
        DariusRuntimeNetworkBridge.ConfigureTemplateIdentity(preIdentity, preAssetId, name);

        // Match the working reference mod's exact order: the NetworkIdentity already has
        // its runtime assetId before the NetworkBehaviour (SkillTrigger/Gem/Ai) is added.
        T component = go.AddComponent<T>();
        component.name = name;
        if (configure != null) configure(component);

        // Every Formal template belongs to the current ModBehaviour generation. Warm Actor
        // templates stay activeSelf=true beneath the inactive root so Awake state is preserved
        // without creating a parallel persistent scene graph.
        GameObject root = GetRuntimeActorRoot();
        go.transform.SetParent(root.transform, false);
        if (warmAbilityInstance)
        {
            DariusLog.Info("AI-PREFAB", "Warm-initialized runtime Actor prefab name=" + name +
                " activeSelf=" + go.activeSelf + " activeInHierarchy=" + go.activeInHierarchy +
                " networkIdentity=" + (preIdentity != null));
        }

        OwnedPrefabs.Add(go);
        RegisterObject(component, name, guid);
        DariusLog.Info("REG", "Prefab registered name=" + name + " type=" + typeof(T).FullName + " guid=" + guid);
        return component;
    }

    private static GameObject GetRuntimeActorRoot()
    {
        if (_runtimeActorRoot != null) return _runtimeActorRoot;
        if (_modOwner == null)
            throw new InvalidOperationException("DariusFormalRegistry has no ModBehaviour owner.");

        _runtimeActorRoot = new GameObject("DariusPrototype_FormalRuntimeResources");
        _runtimeActorRoot.hideFlags = HideFlags.HideAndDontSave;
        _runtimeActorRoot.transform.SetParent(_modOwner, false);
        _runtimeActorRoot.SetActive(false);
        return _runtimeActorRoot;
    }

    private static void RegisterObject(UnityEngine.Object obj, string name, string guid)
    {
        var db = DewResources.database;
        Type type = obj.GetType();

        // Match the resource registration path used by current 2026 mods:
        // register the assembly-qualified type -> stable GUID, ensure the GUID is in
        // the runtime database, then rebuild the runtime lookup tables.
        string aqn = type.AssemblyQualifiedName;
        if (string.IsNullOrEmpty(aqn))
            throw new InvalidOperationException("Could not resolve AssemblyQualifiedName for " + name);

        uint assetId = StableAssetId(guid) | 0x80000000u;
        RegistrationsByGuid[guid] = new RuntimeRegistration(type, name, guid, assetId);

        db.typeAssemblyQualifiedNameToGuid[aqn] = guid;
        if (!db.allGuids.Contains(guid)) db.allGuids.Add(guid);
        DariusLog.DebugInfo("REG", "DB mapping AQN->GUID name=" + name + " aqn=" + aqn + " guid=" + guid);

        // The working Elemental Summon mod populates Dew's complete runtime index set directly.
        // This matters in heavily modded installs where InitForRuntime() may abort on another mod's
        // duplicate key before it finishes rebuilding these secondary dictionaries.
        SetDatabaseMap(db, "typeToGuid", type, guid);
        SetDatabaseMap(db, "guidToType", guid, type);
        SetDatabaseMap(db, "typeNameToGuid", type.Name, guid);
        SetDatabaseMap(db, "nameToGuid", name, guid);
        SetDatabaseMap(db, "guidToName", guid, name);
        SetDatabaseMap(db, "typeNameToType", type.Name, type);
        SetDatabaseMap(db, "typeNameToType", name, type);

        ResourcesByGuid[guid] = obj;
        GuidByObject[obj] = guid;
        RegisterNetworkIdentity(obj, guid);
    }

    private static void SetDatabaseMap(object database, string fieldName, object key, object value)
    {
        try
        {
            if (DariusRuntimeResourceDatabaseBridge.SetMap(database, fieldName, key, value))
                DariusLog.DebugInfo("REG-MAP", fieldName + "[" + key + "]=" + value);
        }
        catch (Exception e)
        {
            DariusLog.Exception("REG-MAP", e, "Failed runtime DB map " + fieldName + " key=" + key);
        }
    }
}