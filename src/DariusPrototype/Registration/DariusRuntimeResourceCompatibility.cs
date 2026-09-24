using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Mirror;
using UnityEngine;

// Compatibility layer based on a working runtime-skill mod (Elemental Summon).
// The important distinction from the early Darius prototype is that a runtime resource
// must participate in all of Dew's runtime lookup paths, not just the primary GUID table.
public static partial class DariusRuntimeResourceCompatibility
{
    private static bool _installed;

    public static void Install(Harmony harmony)
    {
        if (_installed || harmony == null) return;
        int patched = 0;
        try
        {
            patched += PatchOne(harmony,
                typeof(DewResources).GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                    .FirstOrDefault(m => m.Name == "Load" && m.GetParameters().Length >= 1 && m.GetParameters()[0].ParameterType == typeof(string)),
                nameof(LoadPrefix), null, "DewResources.Load");

            patched += PatchOne(harmony,
                typeof(DewResources).GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                    .FirstOrDefault(m => m.Name == "Preload" && m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType == typeof(string)),
                nameof(PreloadPrefix), null, "DewResources.Preload");

            patched += PatchOne(harmony,
                typeof(DewResources).GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                    .FirstOrDefault(m => m.Name == "GetNetworkedPrefab" && m.GetParameters().Length >= 1 && m.GetParameters()[0].ParameterType == typeof(uint)),
                nameof(GetNetworkedPrefabPrefix), null, "DewResources.GetNetworkedPrefab");

            patched += PatchOne(harmony,
                typeof(DewResources).GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                    .FirstOrDefault(m => m.Name == "GetByType" && !m.IsGenericMethod && m.GetParameters().Length >= 1 && m.GetParameters()[0].ParameterType == typeof(Type)),
                nameof(GetByTypePrefix), null, "DewResources.GetByType(Type,...)");

            // Do not patch a closed reference-type generic. Mono shares this method body with
            // GetByType<Actor>, used by the stock Obliteration menu; a StarEffect wrapper turns
            // native skills into null item resources. Exact non-generic registration and the
            // constellation icon mapping already resolve Darius runtime stars.

            // Workshop boot compatibility: the stock UI/profile path commonly resolves persisted
            // Hero/Skin references through GetByShortTypeName<T>/GetByName<T>. Do not patch the
            // shared generic bodies (Mono reference-type generic sharing can leak a prefix into
            // unrelated T). Instead patch the public non-generic base lookups for exact Darius keys.
            // The generic wrappers in current SoD builds can then receive the correct Object and
            // convert it to Hero/Skin without ever touching Addressables.
            patched += PatchOne(harmony,
                typeof(DewResources).GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                    .FirstOrDefault(m => m.Name == "GetByName" && !m.IsGenericMethod && m.ReturnType == typeof(UnityEngine.Object) &&
                        m.GetParameters().Length >= 1 && m.GetParameters()[0].ParameterType == typeof(string)),
                nameof(GetByNameObjectPrefix), null, "DewResources.GetByName(string,...)");

            patched += PatchOne(harmony,
                typeof(DewResources).GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                    .FirstOrDefault(m => m.Name == "GetByShortTypeName" && !m.IsGenericMethod && m.ReturnType == typeof(UnityEngine.Object) &&
                        m.GetParameters().Length >= 1 && m.GetParameters()[0].ParameterType == typeof(string)),
                nameof(GetByShortTypeNameObjectPrefix), null, "DewResources.GetByShortTypeName(string,...)");

            patched += PatchOne(harmony,
                typeof(DewResources).GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                    .FirstOrDefault(m => m.Name == "GetByGuid" && !m.IsGenericMethod && m.ReturnType == typeof(UnityEngine.Object) &&
                        m.GetParameters().Length >= 1 && m.GetParameters()[0].ParameterType == typeof(string)),
                nameof(GetByGuidObjectPrefix), null, "DewResources.GetByGuid(string,...)");

            // v0.18.2b: DO NOT patch any closed/shared generic DewResources lookup.
            // Mono shares reference-type generic method bodies, so even a UnityEngine.Object-typed
            // prefix leaks into GetByName<Acc/Emote/Skin/...> and corrupts unrelated native arrays.
            // Runtime Darius lookup is handled by the non-generic Load/GetByType/network paths and
            // by keeping the stock resource database maps/profile aliases valid.

            // Native cosmetic contract: stock UI_HeroIcon.Setup resolves Hero_Darius and reads
            // Hero.icon/mainColor. Keep that native sprite source. The only UI-side adjustment is a
            // narrow postfix that removes the stock Image RGB multiply from the exact serialized
            // `icon` target, matching the previously validated icon hotfix without scanning children
            // or replacing sprites.
            Type heroIconType = AccessTools.TypeByName("UI_HeroIcon");
            MethodInfo heroIconSetup = heroIconType != null
                ? heroIconType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                    .FirstOrDefault(m => m.Name == "Setup" && m.GetParameters().Length >= 1 && m.GetParameters()[0].ParameterType == typeof(string))
                : null;
            patched += PatchOne(harmony, heroIconSetup, null, nameof(HeroIconSetupPostfix), "UI_HeroIcon.Setup native tint neutralizer");

            // Temporary read-only diagnostic for the remaining Lobby -> Title miniature model issue.
            // The runtime log shows Title itself resolves Skin_Darius_Default after the transition;
            // capture only that exact display instance/transform without changing its presentation.
            Type characterModelDisplayType = AccessTools.TypeByName("CharacterModelDisplay");
            MethodInfo characterModelDisplaySetup = characterModelDisplayType != null
                ? characterModelDisplayType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                    .FirstOrDefault(m => m.Name == "Setup" && m.GetParameters().Length >= 1 &&
                        m.GetParameters()[0].ParameterType == typeof(string))
                : null;
            patched += PatchOne(harmony, characterModelDisplaySetup, null, nameof(CharacterModelDisplaySetupPostfix), "CharacterModelDisplay.Setup Title diagnostic");

            // v0.18.2: the stock in-run detail panel assumes its Hero/Status/attack references came
            // from an Addressables-backed native hero. Runtime Hero_Darius can otherwise leave this
            // panel reading stale/default values (observed as every field showing 500). For Darius
            // only, feed the exact live Hero_Darius EntityStatus/EntityAbility values into the same
            // UI text fields. Reflection is used only for TMP text assignment, keeping the mod free
            // of an extra TextMeshPro compile-time dependency.
            Type heroDetailType = AccessTools.TypeByName("UI_InGame_HeroDetailWindow");
            MethodInfo heroDetailUpdate = heroDetailType != null
                ? heroDetailType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                    .FirstOrDefault(m => m.Name == "UpdateText" && m.GetParameters().Length == 0)
                : null;
            patched += PatchOne(harmony, heroDetailUpdate, nameof(HeroDetailUpdateTextPrefix), null, "UI_InGame_HeroDetailWindow.UpdateText");

            MethodInfo skillIncluded = typeof(Dew).GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                .FirstOrDefault(m => m.Name == "IsSkillIncludedInGame" && m.ReturnType == typeof(bool) && m.GetParameters().Length >= 1);
            patched += PatchOne(harmony, skillIncluded, null, nameof(IsSkillIncludedPostfix), "Dew.IsSkillIncludedInGame");

            MethodInfo gemIncluded = typeof(Dew).GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                .FirstOrDefault(m => m.Name == "IsGemIncludedInGame" && m.ReturnType == typeof(bool) && m.GetParameters().Length >= 1);
            patched += PatchOne(harmony, gemIncluded, null, nameof(IsGemIncludedPostfix), "Dew.IsGemIncludedInGame");

            MethodInfo starIncluded = typeof(Dew).GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                .FirstOrDefault(m => m.Name == "IsStarIncludedInGame" && m.ReturnType == typeof(bool) &&
                    m.GetParameters().Length >= 1 && m.GetParameters()[0].ParameterType == typeof(string));
            patched += PatchOne(harmony, starIncluded, null, nameof(IsStarIncludedPostfix), "Dew.IsStarIncludedInGame");

            MethodInfo skinIncluded = typeof(Dew).GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                .FirstOrDefault(m => m.Name == "IsSkinIncludedInGame" && m.ReturnType == typeof(bool) &&
                    m.GetParameters().Length >= 1 && m.GetParameters()[0].ParameterType == typeof(string));
            patched += PatchOne(harmony, skinIncluded, null, nameof(IsSkinIncludedPostfix), "Dew.IsSkinIncludedInGame");

            MethodInfo[] networkServerSpawnMethods = typeof(NetworkServer)
                .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                .Where(m => m.Name == "Spawn" &&
                            m.GetParameters().Length >= 1 &&
                            m.GetParameters()[0].ParameterType == typeof(GameObject))
                .ToArray();
            for (int i = 0; i < networkServerSpawnMethods.Length; i++)
                patched += PatchOne(harmony, networkServerSpawnMethods[i], nameof(NetworkServerSpawnTemplatePrefix), null, "NetworkServer.Spawn Traveler-template diagnostic");

            MethodInfo networkServerShutdown = typeof(NetworkServer)
                .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                .FirstOrDefault(m => m.Name == "Shutdown" && m.GetParameters().Length == 0);
            patched += PatchOne(harmony, networkServerShutdown, nameof(NetworkServerShutdownPrefix), null, "NetworkServer.Shutdown Traveler-template diagnostic");

            MethodInfo[] networkServerDestroyMethods = typeof(NetworkServer)
                .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                .Where(m => m.Name == "Destroy" &&
                            m.GetParameters().Length >= 1 &&
                            m.GetParameters()[0].ParameterType == typeof(GameObject))
                .ToArray();
            for (int i = 0; i < networkServerDestroyMethods.Length; i++)
                patched += PatchOne(harmony, networkServerDestroyMethods[i], nameof(NetworkServerDestroyTemplatePrefix), null, "NetworkServer.Destroy Traveler-template diagnostic");

            MethodInfo[] networkClientDestroyMethods = typeof(NetworkClient)
                .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                .Where(m => (m.Name.IndexOf("Destroy", StringComparison.OrdinalIgnoreCase) >= 0 ||
                             m.Name.IndexOf("Unspawn", StringComparison.OrdinalIgnoreCase) >= 0) &&
                            m.GetParameters().Length >= 1 &&
                            (m.GetParameters()[0].ParameterType == typeof(GameObject) ||
                             m.GetParameters()[0].ParameterType == typeof(NetworkIdentity)))
                .ToArray();
            for (int i = 0; i < networkClientDestroyMethods.Length; i++)
                patched += PatchOne(harmony, networkClientDestroyMethods[i], nameof(NetworkClientDestroyTemplatePrefix), null, "NetworkClient destroy/unspawn Traveler-template diagnostic");

            MethodInfo[] spawnManagerDestroyMethods = typeof(SpawnManager)
                .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                .Where(m => m.Name == "Destroy" &&
                            m.GetParameters().Length >= 1 &&
                            m.GetParameters()[0].ParameterType == typeof(GameObject))
                .ToArray();
            for (int i = 0; i < spawnManagerDestroyMethods.Length; i++)
                patched += PatchOne(harmony, spawnManagerDestroyMethods[i], nameof(SpawnManagerDestroyTemplatePrefix), null, "SpawnManager.Destroy Traveler-template diagnostic");

            MethodInfo networkIdentityOnDestroy = typeof(NetworkIdentity)
                .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .FirstOrDefault(m => m.Name == "OnDestroy" && m.GetParameters().Length == 0);
            patched += PatchOne(harmony, networkIdentityOnDestroy, nameof(NetworkIdentityOnDestroyTemplatePrefix), null, "NetworkIdentity.OnDestroy Traveler-template diagnostic");

            MethodInfo actorPrepare = typeof(Actor).GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .FirstOrDefault(m => m.Name == "PrepareAndSpawn");
            patched += PatchOne(harmony, actorPrepare, nameof(ActorPrepareAndSpawnPrefix), null, "Actor.PrepareAndSpawn");

            MethodInfo entityAbilityStart = typeof(EntityAbility).GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .FirstOrDefault(m => m.Name == "OnStartServer" && m.GetParameters().Length == 0);
            patched += PatchOne(harmony, entityAbilityStart, nameof(EntityAbilityOnStartServerPrefix), null, "EntityAbility.OnStartServer");

            MethodInfo lootStart = typeof(LootManager).GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .FirstOrDefault(m => m.Name == "OnStartServer" && m.GetParameters().Length == 0);
            patched += PatchOne(harmony, lootStart, null, nameof(LootManagerOnStartServerPostfix), "LootManager.OnStartServer");

            _installed = true;
            DariusLog.Info("RESOURCE-COMPAT", "Runtime resource compatibility installed. patchedMethods=" + patched +
                " (runtime Load/GetByName/GetByShortTypeName/GetByGuid/HeroIcon-native-tint/Title-model-diagnostic/Network-template-diagnostic/Destroy-boundary-diagnostic/HeroDetail/Preload/GetByType/GetNetworkedPrefab/skill-gem-star-skin inclusion/PrepareAndSpawn/EntityAbility/LootManager; shared generic DewResources hooks disabled).");
        }
        catch (Exception e)
        {
            DariusLog.Exception("RESOURCE-COMPAT", e, "Runtime resource compatibility install failed");
        }
    }

}
