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
        bool hadRuntimeState = _registered || _registering || _resourceRoot != null ||
                               HeroPrefab != null || AttackPrefab != null ||
                               OwnedObjects.Count > 0 || ResourcesByGuid.Count > 0 ||
                               NetworkPrefabs.Count > 0;
        if (hadRuntimeState)
            DariusPrototypeMod.BlockBootstrapForCurrentFrame("Traveler runtime teardown");

        DariusLog.Info("TRAVELER-TEARDOWN", "UnregisterRuntimeOnly begin state=" + DiagnosticState());
        _registered = false;
        _registering = false;
        // Spawn-handler ownership is tracked separately from prefab lookup. Never unregister a
        // handler merely because a prefab map entry exists; only release handlers this generation
        // actually installed.
        foreach (uint id in RegisteredSpawnHandlerIds.ToArray())
        {
            try { NetworkClient.UnregisterSpawnHandler(id); } catch { }
        }
        RegisteredSpawnHandlerIds.Clear();

        foreach (uint id in NetworkPrefabs.Keys.ToArray())
        {
            try
            {
                string expected = RuntimeGuidForAssetId(id);
                if (!string.IsNullOrEmpty(expected))
                    DariusUnsupportedResourceBridge.RemoveNetworkGuidIfOwned(DewResources.database, id, expected);
            }
            catch { }
        }
        NetworkPrefabs.Clear();

        // Dew's object fallback index is keyed by the runtime Unity objects themselves. Remove our
        // entries before destroying a generation so variant clears cannot retain stale wrappers.
        foreach (KeyValuePair<string, UnityEngine.Object> pair in ResourcesByGuid.ToArray())
        {
            UnityEngine.Object resource = pair.Value;
            if (ReferenceEquals(resource, null)) continue;
            DariusUnsupportedResourceBridge.RemoveObjectGuidIfOwned(DewResources.database, resource, pair.Key);
            Component component = resource as Component;
            if (ReferenceEquals(component, null)) continue;
            try
            {
                GameObject go = component.gameObject;
                if (!ReferenceEquals(go, null))
                    DariusUnsupportedResourceBridge.RemoveObjectGuidIfOwned(DewResources.database, go, pair.Key);
            }
            catch { }
        }

        UnregisterContent();

        DariusUnsupportedResourceBridge.RemoveHeroTypes(
            new[] { typeof(Hero_Darius) }, "TRAVELER-TYPE");
        DariusUnsupportedResourceBridge.RemoveSkillTypes(
            DariusRegisteredSkillTypes, "TRAVELER-TYPE");
        DariusUnsupportedResourceBridge.RemoveHeroSkillTypes(
            DariusRegisteredHeroSkillTypes, "TRAVELER-TYPE");

        // Only remove maps where our exact value is still installed. User profile data is intentionally preserved.
        for (int si = 0; si < SkinSpecs.Length; si++)
        {
            DariusUnsupportedResourceBridge.RemoveNamedResourceIdentity(
                DewResources.database, SkinSpecs[si].name, SkinSpecs[si].guid);
        }
        DariusUnsupportedResourceBridge.RemoveTypedResourceIdentity(DewResources.database, typeof(Hero_Darius), HeroName, HeroGuid);
        DariusUnsupportedResourceBridge.RemoveTypedResourceIdentity(DewResources.database, typeof(At_DariusAxe), AttackName, AttackGuid);
        DariusUnsupportedResourceBridge.RemoveTypedResourceIdentity(DewResources.database, typeof(Ai_DariusAxe), AttackInstanceName, AttackInstanceGuid);
        DariusUnsupportedResourceBridge.RemoveTypedResourceIdentity(DewResources.database, typeof(Ai_DariusAxe_Crit), AttackCritInstanceName, AttackCritInstanceGuid);

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
        _registeredDatabase = null;
        _registeredModRoot = null;
        if (_resourceRoot != null) UnityEngine.Object.Destroy(_resourceRoot);
        _resourceRoot = null;
        DariusLog.Info("TRAVELER", "Runtime Hero_Darius resources unregistered; persistent profile data left untouched. state=" + DiagnosticState());
    }

    public static void ShutdownRuntimeResources()
    {
        UnregisterRuntimeOnly();

        // Native model bundle assets are process/mod-lifetime resources. Runtime skins are clones
        // owned by this Traveler generation, but unloading/reloading the bundle with Unload(false)
        // on every ModBehaviour replacement duplicates native assets across generations.
        _modOwner = null;
        _modOwnerInstanceId = 0;
        _modOwnerGenerationId = 0;
        _registeredDatabase = null;
        _registeredModRoot = null;
    }
}