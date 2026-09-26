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
    public static void Unregister()
    {
        DariusLog.Info("REG", "Unregister started. resources=" + ResourcesByGuid.Count + " networkPrefabs=" + NetworkPrefabs.Count);
        _registered = false;
        _lastDroppedHeroInstanceId = 0;
        DariusConstellationRegistry.UnregisterTypeCache();

        object database = DewResources.database;
        if (database != null)
        {
            // Dew's fallback GUID index is keyed by the runtime Unity objects themselves. Remove
            // both the Component and its GameObject before destroying this Formal generation.
            foreach (KeyValuePair<string, UnityEngine.Object> pair in ResourcesByGuid.ToArray())
            {
                UnityEngine.Object resource = pair.Value;
                if (ReferenceEquals(resource, null)) continue;
                DariusUnsupportedResourceBridge.RemoveObjectGuidIfOwned(database, resource, pair.Key);
                Component component = resource as Component;
                if (ReferenceEquals(component, null)) continue;
                try
                {
                    GameObject go = component.gameObject;
                    if (!ReferenceEquals(go, null))
                        DariusUnsupportedResourceBridge.RemoveObjectGuidIfOwned(database, go, pair.Key);
                }
                catch { }
            }

            // Stable registration metadata survives Unity fake-null, so resource identity cleanup
            // stays symmetric even when the template object was already destroyed.
            foreach (RuntimeRegistration record in RegistrationsByGuid.Values.ToArray())
            {
                DariusUnsupportedResourceBridge.RemoveTypedResourceIdentity(
                    database, record.type, record.name, record.guid);
                DariusUnsupportedResourceBridge.RemoveNetworkGuidIfOwned(
                    database, record.assetId, record.guid);
            }
        }

        // Mirror handler ownership is independent from DewResources.database availability.
        // Always release handlers that this generation successfully installed.
        foreach (RuntimeRegistration record in RegistrationsByGuid.Values.ToArray())
        {
            if (!record.networkHandlerRegistered) continue;
            try { NetworkClient.UnregisterSpawnHandler(record.assetId); } catch { }
        }

        foreach (GameObject go in OwnedPrefabs)
        {
            if (go != null) UnityEngine.Object.Destroy(go);
        }

        OwnedPrefabs.Clear();
        if (_runtimeActorRoot != null) UnityEngine.Object.Destroy(_runtimeActorRoot);
        _runtimeActorRoot = null;
        ResourcesByGuid.Clear();
        GuidByObject.Clear();
        NetworkPrefabs.Clear();
        RegistrationsByGuid.Clear();
        Decimate = null;
        CripplingStrike = null;
        Apprehend = null;
        NoxianGuillotine = null;
        Flash = null;
        Ghost = null;
        Hemorrhage = null;
        NoxianMightStatus = null;
        HemorrhageStatus = null;
        LegacyHemorrhage = null;
        DecimateAbility = null;
        CripplingStrikeAbility = null;
        ApprehendAbility = null;
        NoxianGuillotineAbility = null;
        DariusLog.Info("REG", "Unregister complete.");
    }

    public static bool TryLoad(string key, out UnityEngine.Object obj)
    {
        obj = null;
        if (string.IsNullOrEmpty(key)) return false;
        if (ResourcesByGuid.TryGetValue(key, out obj) && obj != null) return true;

        // Some stock paths pass an internal resource name instead of the GUID.
        // Resolve both forms so custom runtime resources behave like native resources.
        foreach (UnityEngine.Object candidate in ResourcesByGuid.Values)
        {
            if (candidate == null) continue;
            if (string.Equals(candidate.name, key, StringComparison.Ordinal) ||
                string.Equals(candidate.GetType().Name, key, StringComparison.Ordinal))
            {
                obj = candidate;
                return true;
            }
        }
        return false;
    }

    public static bool TryGetGuid(UnityEngine.Object obj, out string guid)
    {
        guid = null;
        return obj != null && GuidByObject.TryGetValue(obj, out guid);
    }

    public static IEnumerable<UnityEngine.Object> GetObjectsAssignableTo(Type type)
    {
        foreach (UnityEngine.Object obj in ResourcesByGuid.Values)
            if (obj != null && type.IsAssignableFrom(obj.GetType())) yield return obj;
    }

    public static bool IsDariusSkillName(string name)
    {
        return name == "St_Darius_Decimate" ||
               name == "St_Darius_CripplingStrike" ||
               name == "St_Darius_Apprehend" ||
               name == "St_Darius_NoxianGuillotine" ||
               name == "St_Darius_Flash" ||
               name == "St_Darius_Ghost" ||
               name == "St_D_Darius_Hemorrhage";
    }

    internal static T RegisterRuntimePrefab<T>(string name, string guid, Action<T> configure) where T : Component
    {
        return RegisterPrefab<T>(name, guid, configure);
    }

    public static bool SpawnConstellationW(Hero hero, int level, DewPlayer nativeSpawnPlayer = null)
    {
        if (hero == null || CripplingStrike == null || !NetworkServer.active) return false;
        try
        {
            DewPlayer player = nativeSpawnPlayer != null && nativeSpawnPlayer.hero == hero ? nativeSpawnPlayer : FindPlayerForHero(hero);
            if (player == null)
            {
                DariusLog.Warn("STAR-STARTER-W", "Skipped Crippling Strike starter spawn because owning DewPlayer was unavailable.");
                return false;
            }
            string sceneName = SceneManager.GetActiveScene().name;
            if (string.IsNullOrEmpty(sceneName) || !sceneName.StartsWith("Room_", StringComparison.OrdinalIgnoreCase))
            {
                DariusLog.Warn("STAR-STARTER-W", "Rejected Crippling Strike starter spawn outside a gameplay Room_* scene: " + sceneName);
                return false;
            }
            if (!DariusConstellationRuntime.IsUsableStarterWorldPosition(hero.agentPosition))
            {
                DariusLog.Warn("STAR-STARTER-W", "Rejected Crippling Strike starter spawn at staging/invalid hero position " + DariusLog.Vec(hero.agentPosition));
                return false;
            }
            Vector3 pos = GoodDropPosition(hero.agentPosition, 1.6f);
            if (!DariusConstellationRuntime.IsUsableStarterWorldPosition(pos)) pos = hero.agentPosition;
            Dew.CreateSkillTrigger<St_Darius_CripplingStrike>(CripplingStrike, pos, Mathf.Max(1, level), player, null);
            DariusLog.Info("STAR-STARTER-W", "DejaVu-style CreateSkillTrigger completed level=" + level +
                " heroPos=" + DariusLog.Vec(hero.agentPosition) + " dropPos=" + DariusLog.Vec(pos) + " player=" + player.name);
            return true;
        }
        catch (Exception e)
        {
            DariusLog.Exception("STAR-STARTER-W", e, "Constellation W DejaVu-style spawn failed");
            return false;
        }
    }
}