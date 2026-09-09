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
public static class DariusRuntimeResourceCompatibility
{
    // The stock browser resolves every known StarEffect before applying its hero/category filter.
    // Cache the final object, not a MethodInfo: category changes, purchases and hero changes all
    // rebuild the list and must not repeat reflection for every custom star.
    private static readonly Dictionary<Type, StarEffect> ResolvedStarByType = new Dictionary<Type, StarEffect>();

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

            // The mastery/reward window can open immediately after restart, before Workshop mods
            // have finished profile/content registration. Ensure the runtime Hero/Skin bridge exists
            // before that UI builds any HeroIcon children. This prefix does not alter reward logic.
            Type playRewardType = AccessTools.TypeByName("UI_PlayRewardAnnouncer");
            if (playRewardType != null)
            {
                string[] earlyRewardMethods = { "Awake", "OnEnable", "Start", "Refresh", "Setup" };
                for (int i = 0; i < earlyRewardMethods.Length; i++)
                {
                    MethodInfo rewardMethod = playRewardType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                        .FirstOrDefault(m => m.Name == earlyRewardMethods[i] && m.GetParameters().Length == 0);
                    if (rewardMethod != null)
                        patched += PatchOne(harmony, rewardMethod, nameof(PlayRewardUIPrefix), null, "UI_PlayRewardAnnouncer." + rewardMethod.Name);
                }
            }

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
                " (runtime Load/GetByName/GetByShortTypeName/GetByGuid/HeroIcon-native-tint/RewardGuard/HeroDetail/Preload/GetByType/GetNetworkedPrefab/skill-gem-star-skin inclusion/PrepareAndSpawn/EntityAbility/LootManager; shared generic DewResources hooks disabled).");
        }
        catch (Exception e)
        {
            DariusLog.Exception("RESOURCE-COMPAT", e, "Runtime resource compatibility install failed");
        }
    }

    private static int PatchSharedGenericObjectLookup(Harmony harmony, string methodName)
    {
        MethodInfo[] methods = typeof(DewResources).GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
        for (int i = 0; i < methods.Length; i++)
        {
            MethodInfo raw = methods[i];
            if (raw.Name != methodName || !raw.IsGenericMethodDefinition) continue;
            Type[] ga = raw.GetGenericArguments();
            ParameterInfo[] ps = raw.GetParameters();
            if (ga.Length != 1 || ps.Length < 1 || ps[0].ParameterType != typeof(string)) continue;
            try
            {
                MethodInfo closed = raw.MakeGenericMethod(typeof(UnityEngine.Object));
                if (closed.ReturnType != typeof(UnityEngine.Object)) continue;
                return PatchOne(harmony, closed, nameof(SharedGenericDariusLookupPrefix), null,
                    "DewResources." + methodName + "<UnityEngine.Object>[shared-body]");
            }
            catch (Exception e)
            {
                DariusLog.Exception("RESOURCE-COMPAT", e, "Could not close shared generic lookup " + methodName);
            }
        }
        return 0;
    }

    private static int PatchClosedGenericLookup(Harmony harmony, string methodName, Type resourceType, string prefixName)
    {
        int count = 0;
        MethodInfo[] methods = typeof(DewResources).GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
        for (int i = 0; i < methods.Length; i++)
        {
            MethodInfo raw = methods[i];
            if (raw.Name != methodName || !raw.IsGenericMethodDefinition) continue;
            Type[] ga = raw.GetGenericArguments();
            if (ga.Length != 1) continue;
            ParameterInfo[] ps = raw.GetParameters();
            if (ps.Length < 1 || ps[0].ParameterType != typeof(string)) continue;
            MethodInfo closed;
            try { closed = raw.MakeGenericMethod(resourceType); } catch { continue; }
            if (!resourceType.IsAssignableFrom(closed.ReturnType) && closed.ReturnType != resourceType) continue;
            count += PatchOne(harmony, closed, prefixName, null, "DewResources." + methodName + "<" + resourceType.Name + ">");
        }
        return count;
    }

    private static int PatchFinalizer(Harmony harmony, MethodInfo method, string finalizerName, string label)
    {
        if (method == null) return 0;
        try
        {
            HarmonyMethod finalizer = new HarmonyMethod(AccessTools.Method(typeof(DariusRuntimeResourceCompatibility), finalizerName));
            harmony.Patch(method, finalizer: finalizer);
            DariusLog.DebugInfo("RESOURCE-COMPAT", "Patched " + label + " -> " + method);
            return 1;
        }
        catch (Exception e)
        {
            DariusLog.Exception("RESOURCE-COMPAT", e, "Failed to patch " + label);
            return 0;
        }
    }

    private static int PatchOne(Harmony harmony, MethodInfo method, string prefixName, string postfixName, string label)
    {
        if (method == null)
        {
            DariusLog.Warn("RESOURCE-COMPAT", "Target not found: " + label);
            return 0;
        }
        try
        {
            HarmonyMethod prefix = string.IsNullOrEmpty(prefixName) ? null : new HarmonyMethod(AccessTools.Method(typeof(DariusRuntimeResourceCompatibility), prefixName));
            HarmonyMethod postfix = string.IsNullOrEmpty(postfixName) ? null : new HarmonyMethod(AccessTools.Method(typeof(DariusRuntimeResourceCompatibility), postfixName));
            harmony.Patch(method, prefix: prefix, postfix: postfix);
            DariusLog.DebugInfo("RESOURCE-COMPAT", "Patched " + label + " -> " + method);
            return 1;
        }
        catch (Exception e)
        {
            DariusLog.Exception("RESOURCE-COMPAT", e, "Failed to patch " + label);
            return 0;
        }
    }

    private static bool GetByNameObjectPrefix(string __0, ref UnityEngine.Object __result)
    {
        if (string.IsNullOrEmpty(__0)) return true;
        bool isHero = __0 == DariusTravelerRegistry.HeroName || __0 == DariusTravelerRegistry.HeroGuid;
        bool isSkin = DariusTravelerRegistry.IsRuntimeSkinKey(__0);
        if (!isHero && !isSkin) return true;

        DariusTravelerRegistry.EnsureCoreRegisteredForLookup("DewResources.GetByName early lookup: " + __0);
        if (isHero) __result = DariusTravelerRegistry.HeroPrefab;
        else
        {
            Skin skin;
            if (!DariusTravelerRegistry.TryResolveRuntimeSkin(__0, out skin)) return true;
            __result = skin;
        }
        if (__result == null) return true;
        DariusLog.DebugInfoThrottled("RES-NAME", __0, "Non-generic GetByName intercepted Workshop-safe key=" + __0 + " result=" + __result.name, 1.25);
        return false;
    }

    private static bool GetByShortTypeNameObjectPrefix(string __0, ref UnityEngine.Object __result)
    {
        if (string.IsNullOrEmpty(__0)) return true;
        if (__0 != DariusTravelerRegistry.HeroName && __0 != DariusTravelerRegistry.HeroGuid) return true;

        DariusTravelerRegistry.EnsureCoreRegisteredForLookup("DewResources.GetByShortTypeName early lookup: " + __0);
        __result = DariusTravelerRegistry.HeroPrefab;
        if (__result == null) return true;
        DariusLog.DebugInfoThrottled("RES-SHORTTYPE", __0, "Non-generic GetByShortTypeName intercepted Workshop-safe key=" + __0 + " result=" + __result.name, 1.25);
        return false;
    }

    private static bool GetByGuidObjectPrefix(string __0, ref UnityEngine.Object __result)
    {
        if (string.IsNullOrEmpty(__0)) return true;
        bool isHero = __0 == DariusTravelerRegistry.HeroGuid;
        bool isSkin = DariusTravelerRegistry.IsRuntimeSkinKey(__0);
        if (!isHero && !isSkin) return true;

        DariusTravelerRegistry.EnsureCoreRegisteredForLookup("DewResources.GetByGuid early lookup: " + __0);
        if (isHero) __result = DariusTravelerRegistry.HeroPrefab;
        else
        {
            Skin skin;
            if (!DariusTravelerRegistry.TryResolveRuntimeSkin(__0, out skin)) return true;
            __result = skin;
        }
        if (__result == null) return true;
        DariusLog.DebugInfoThrottled("RES-GUID", __0, "Non-generic GetByGuid intercepted Workshop-safe key=" + __0 + " result=" + __result.name, 1.25);
        return false;
    }

    private static bool SharedGenericDariusLookupPrefix(string __0, ref UnityEngine.Object __result)
    {
        if (string.IsNullOrEmpty(__0)) return true;
        bool isHero = __0 == DariusTravelerRegistry.HeroName || __0 == DariusTravelerRegistry.HeroGuid;
        bool isSkin = DariusTravelerRegistry.IsRuntimeSkinKey(__0);
        if (!isHero && !isSkin) return true;

        if (isHero)
        {
            if (DariusTravelerRegistry.HeroPrefab == null) DariusTravelerRegistry.RepairRuntimeRegistration("shared generic Hero lookup self-heal: " + __0);
            __result = DariusTravelerRegistry.HeroPrefab;
        }
        else
        {
            Skin skin;
            if (!DariusTravelerRegistry.TryResolveRuntimeSkin(__0, out skin))
            {
                DariusTravelerRegistry.RepairRuntimeRegistration("shared generic Skin lookup self-heal: " + __0);
                if (!DariusTravelerRegistry.TryResolveRuntimeSkin(__0, out skin)) return true;
            }
            __result = skin;
        }
        if (__result == null) return true;
        DariusLog.DebugInfoThrottled("RES-GENERIC", __0, "Shared generic runtime lookup intercepted exact " + (isHero ? "Hero" : "Skin") +
            " key=" + __0 + " result=" + __result.name, 1.25);
        return false;
    }

    private static bool HeroLookupPrefix(string __0, ref Hero __result)
    {
        if (string.IsNullOrEmpty(__0)) return true;
        if (__0 != DariusTravelerRegistry.HeroName && __0 != DariusTravelerRegistry.HeroGuid) return true;
        if (DariusTravelerRegistry.HeroPrefab == null)
            DariusTravelerRegistry.RepairRuntimeRegistration("generic Hero lookup self-heal: " + __0);
        __result = DariusTravelerRegistry.HeroPrefab;
        DariusLog.DebugInfoThrottled("RES-HERO", __0, "Closed generic Hero lookup intercepted key=" + __0 + " result=" + (__result != null ? __result.name : "<null>"), 1.25);
        return false;
    }

    private static bool SkinLookupPrefix(string __0, ref Skin __result)
    {
        if (string.IsNullOrEmpty(__0) || !DariusTravelerRegistry.IsRuntimeSkinKey(__0)) return true;
        if (!DariusTravelerRegistry.TryResolveRuntimeSkin(__0, out __result))
        {
            DariusTravelerRegistry.RepairRuntimeRegistration("generic Skin lookup self-heal: " + __0);
            if (!DariusTravelerRegistry.TryResolveRuntimeSkin(__0, out __result)) return true;
        }
        DariusLog.DebugInfoThrottled("RES-SKIN", __0, "Closed generic Skin lookup intercepted key=" + __0 + " result=" + (__result != null ? __result.name : "<null>"), 1.25);
        return false;
    }

    private static void HeroIconSetupPostfix(object __instance, string __0)
    {
        if (__instance == null || (__0 != DariusTravelerRegistry.HeroName && __0 != DariusTravelerRegistry.HeroGuid)) return;
        try
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            Type t = __instance.GetType();
            object iconTarget = null;
            FieldInfo field = t.GetField("icon", flags);
            if (field != null) iconTarget = field.GetValue(__instance);
            if (iconTarget == null)
            {
                PropertyInfo prop = t.GetProperty("icon", flags);
                if (prop != null && prop.CanRead && prop.GetIndexParameters().Length == 0) iconTarget = prop.GetValue(__instance, null);
            }
            if (iconTarget == null)
            {
                DariusLog.Warn("HERO-ICON-TINT", "UI_HeroIcon.Setup resolved Darius but serialized icon target was null; native Hero.icon remains untouched.");
                return;
            }

            // Verify that stock Setup actually installed the native Hero_Darius.icon before changing
            // only its multiplicative UI colour. Never force a sprite into arbitrary UI children.
            Sprite expected = DariusTravelerRegistry.HeroPrefab != null ? DariusTravelerRegistry.HeroPrefab.icon : null;
            PropertyInfo spriteProp = iconTarget.GetType().GetProperty("sprite", flags);
            Sprite actual = spriteProp != null && spriteProp.CanRead ? spriteProp.GetValue(iconTarget, null) as Sprite : null;
            if (expected == null || actual != expected)
            {
                DariusLog.Warn("HERO-ICON-TINT", "Stock UI_HeroIcon did not resolve the native Hero_Darius.icon; tint neutralizer skipped rather than replacing the sprite.");
                return;
            }

            PropertyInfo colorProp = iconTarget.GetType().GetProperty("color", flags);
            if (colorProp != null && colorProp.CanRead && colorProp.CanWrite && colorProp.PropertyType == typeof(Color))
            {
                Color current = (Color)colorProp.GetValue(iconTarget, null);
                colorProp.SetValue(iconTarget, new Color(1f, 1f, 1f, current.a), null);
                DariusLog.DebugInfoThrottled("HERO-ICON-TINT", "native", "Neutralized stock UI_HeroIcon RGB multiply while retaining Hero_Darius.icon as the sprite source.", 1.0);
            }
        }
        catch (Exception e)
        {
            DariusLog.Exception("HERO-ICON-TINT", e, "Native Darius hero-icon tint neutralizer failed; no sprite override was attempted.");
        }
    }

    private static void PlayRewardUIPrefix()
    {
        DariusTravelerRegistry.EnsureCoreRegisteredForLookup("UI_PlayRewardAnnouncer early mastery/reward guard");
    }

    private static float _nextHeroDetailLogTime;
    private static float _nextHeroDetailRefreshTime;
    private static int _lastHeroDetailWindowId;

    private static bool HeroDetailUpdateTextPrefix(object __instance)
    {
        if (__instance == null) return true;
        Hero_Darius hero = null;
        try
        {
            DewPlayer local = DewPlayer.local;
            hero = local != null ? local.hero as Hero_Darius : null;
        }
        catch { }
        if (hero == null) return true;

        // The stock detail window can call UpdateText many times per second while open. Darius'
        // reflection-based replacement only needs UI-rate refreshes, not frame-rate refreshes.
        int windowId = 0;
        try
        {
            UnityEngine.Object uo = __instance as UnityEngine.Object;
            windowId = uo != null ? uo.GetInstanceID() : __instance.GetHashCode();
        }
        catch { }
        if (windowId == _lastHeroDetailWindowId && Time.unscaledTime < _nextHeroDetailRefreshTime) return false;
        _lastHeroDetailWindowId = windowId;
        _nextHeroDetailRefreshTime = Time.unscaledTime + 0.20f;

        try
        {
            EntityStatus status = hero.Status;
            EntityAbility ability = hero.Ability;
            Component control = FindComponentByTypeName(hero.gameObject, "EntityControl");
            if (status == null || ability == null || control == null) return true;

            var final = status.finalStats;
            float currentHealth = hero.currentHealth;
            float attackSpeedMultiplier = ReadFloatMember(status, "attackSpeedMultiplier", 1f);
            float movementSpeedMultiplier = ReadFloatMember(status, "movementSpeedMultiplier", 1f);
            float baseAgentSpeed = ReadFloatMember(control, "baseAgentSpeed", 1f);
            float attackCooldown = 0f;
            try
            {
                AttackTrigger attack = ability.attackAbility as AttackTrigger;
                if (attack == null) attack = DariusTravelerRegistry.AttackPrefab;
                if (attack != null && attack.configs != null && attack.configs.Length > 0 && attack.configs[0] != null)
                    attackCooldown = attack.configs[0].cooldownTime;
            }
            catch { }
            float attackSpeed = attackCooldown > 0.0001f ? (1f / attackCooldown) * attackSpeedMultiplier : 0f;
            float moveSpeed = movementSpeedMultiplier * baseAgentSpeed * 100f;

            int written = 0;
            written += SetUiText(__instance, "healthText", currentHealth.ToString("0") + "/" + final.maxHealth.ToString("0"));
            written += SetUiText(__instance, "adText", final.attackDamage.ToString("0.##"));
            written += SetUiText(__instance, "apText", final.abilityPower.ToString("0.##"));
            written += SetUiText(__instance, "skillHasteText", final.abilityHaste.ToString("0.##"));
            written += SetUiText(__instance, "attackSpeedText", attackSpeed.ToString("0.00"));
            written += SetUiText(__instance, "critChanceText", (final.critChance * 100f).ToString("0.#") + "%");
            written += SetUiText(__instance, "armorText", final.armor.ToString("0.##"));
            written += SetUiText(__instance, "fireAmpText", "x" + (1f + final.fireEffectAmp).ToString("0.##"));
            written += SetUiText(__instance, "movementSpeedText", moveSpeed.ToString("0.##"));

            if (Time.unscaledTime >= _nextHeroDetailLogTime)
            {
                _nextHeroDetailLogTime = Time.unscaledTime + 20f;
                DariusLog.DebugInfo("STAT-PANEL", "Darius live panel changedFields=" + written +
                    " HP=" + currentHealth.ToString("0.##") + "/" + final.maxHealth.ToString("0.##") +
                    " AD=" + final.attackDamage.ToString("0.##") + " AP=" + final.abilityPower.ToString("0.##") +
                    " haste=" + final.abilityHaste.ToString("0.##") + " atkSpeed=" + attackSpeed.ToString("0.###") +
                    " crit=" + final.critChance.ToString("0.###") + " armor=" + final.armor.ToString("0.##") +
                    " move=" + moveSpeed.ToString("0.##") + " attackCd=" + attackCooldown.ToString("0.###"));
            }

            // Darius live values were resolved successfully. Always skip the stock method for this
            // refresh, even if every string was already identical. Returning true when changed=0
            // would let stock text overwrite the live values and then make the next Darius refresh
            // overwrite them again, producing visible text jitter.
            return false;
        }
        catch (Exception e)
        {
            DariusLog.Exception("STAT-PANEL", e, "Failed to supply live Hero_Darius detail values; falling back to stock UI method");
            return true;
        }
    }

    private static int SetUiText(object owner, string fieldName, string value)
    {
        try
        {
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            FieldInfo field = owner.GetType().GetField(fieldName, flags);
            object textObject = field != null ? field.GetValue(owner) : null;
            if (textObject == null) return 0;
            PropertyInfo text = textObject.GetType().GetProperty("text", flags);
            if (text == null || !text.CanWrite) return 0;
            if (text.CanRead)
            {
                string existing = text.GetValue(textObject, null) as string;
                if (string.Equals(existing, value, StringComparison.Ordinal)) return 0;
            }
            text.SetValue(textObject, value, null);
            return 1;
        }
        catch { return 0; }
    }

    private static Component FindComponentByTypeName(GameObject go, string typeName)
    {
        if (go == null || string.IsNullOrEmpty(typeName)) return null;
        try
        {
            Component[] components = go.GetComponents<Component>();
            for (int i = 0; i < components.Length; i++)
            {
                Component c = components[i];
                if (c != null && string.Equals(c.GetType().Name, typeName, StringComparison.Ordinal)) return c;
            }
        }
        catch { }
        return null;
    }

    private static float ReadFloatMember(object obj, string name, float fallback)
    {
        if (obj == null) return fallback;
        try
        {
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            Type t = obj.GetType();
            PropertyInfo p = t.GetProperty(name, flags);
            if (p != null) return Convert.ToSingle(p.GetValue(obj, null));
            FieldInfo f = t.GetField(name, flags);
            if (f != null) return Convert.ToSingle(f.GetValue(obj));
        }
        catch { }
        return fallback;
    }

    // DewResources.Load(string, ResourceLoadSettings): the working reference mod intercepts
    // this exact lookup path and returns its runtime prefab instead of asking Addressables.
    private static bool LoadPrefix(string __0, ref UnityEngine.Object __result)
    {
        UnityEngine.Object obj;
        bool travelerFound = DariusTravelerRegistry.TryLoad(__0, out obj);
        if (!travelerFound && DariusTravelerRegistry.IsRuntimeKey(__0))
        {
            // First-run teardown can destroy Hero_Darius before a scene callback fires. Never let a
            // known runtime GUID fall through to Addressables: repair/rebuild it synchronously and retry.
            DariusTravelerRegistry.RepairRuntimeRegistration("DewResources.Load self-heal key=" + __0);
            travelerFound = DariusTravelerRegistry.TryLoad(__0, out obj);
        }
        if (!travelerFound && !DariusFormalRegistry.TryLoad(__0, out obj)) return true;
        Component component = obj as Component;
        __result = component != null ? (UnityEngine.Object)component.gameObject : obj;
        if (string.IsNullOrEmpty(__0) || __0.IndexOf("-star-", StringComparison.Ordinal) < 0)
            DariusLog.DebugInfoThrottled("RES-LOAD", __0, "Runtime Load intercepted key=" + __0 + " result=" + (__result != null ? __result.name : "<null>"), 1.25);
        return false;
    }

    // DewResources.Preload goes straight to Addressables and therefore bypasses Load().
    // Runtime-only prefabs have no Addressables location; skipping the warm-up is safe because
    // Load/GetByType/GetNetworkedPrefab are intercepted below.
    private static bool PreloadPrefix(string __0)
    {
        if (!DariusTravelerRegistry.IsRuntimeKey(__0) && !DariusFormalRegistry.IsDariusResourceKey(__0)) return true;
        DariusLog.DebugInfoThrottled("RES-PRELOAD", __0, "Skipped Addressables preload for runtime Darius key=" + __0, 1.5);
        return false;
    }

    private static bool GetNetworkedPrefabPrefix(uint __0, ref GameObject __result)
    {
        GameObject prefab;
        bool travelerFound = DariusTravelerRegistry.TryGetNetworkPrefab(__0, out prefab);
        bool travelerAssetId = __0 == DariusTravelerRegistry.HeroAssetId ||
                               __0 == DariusTravelerRegistry.AttackAssetId ||
                               __0 == DariusTravelerRegistry.AttackInstanceAssetId ||
                               __0 == DariusTravelerRegistry.AttackCritInstanceAssetId;
        if (!travelerFound && travelerAssetId)
        {
            DariusTravelerRegistry.RepairRuntimeRegistration("DewResources.GetNetworkedPrefab self-heal assetId=" + __0);
            travelerFound = DariusTravelerRegistry.TryGetNetworkPrefab(__0, out prefab);
        }
        if (!travelerFound && !DariusFormalRegistry.TryGetNetworkPrefab(__0, out prefab)) return true;
        __result = prefab;
        DariusLog.DebugInfoThrottled("RES-NET", __0.ToString(), "GetNetworkedPrefab intercepted assetId=" + __0 + " prefab=" + prefab.name, 1.0);
        return false;
    }

    private static bool GetByTypePrefix(Type __0, ref UnityEngine.Object __result)
    {
        UnityEngine.Object obj;
        bool travelerFound = DariusTravelerRegistry.TryGetByType(__0, out obj);
        bool travelerType = __0 == typeof(Hero_Darius) || __0 == typeof(At_DariusAxe) ||
                            __0 == typeof(Ai_DariusAxe) || __0 == typeof(Ai_DariusAxe_Crit);
        if (!travelerFound && travelerType)
        {
            DariusTravelerRegistry.RepairRuntimeRegistration("DewResources.GetByType self-heal type=" + (__0 != null ? __0.FullName : "<null>"));
            travelerFound = DariusTravelerRegistry.TryGetByType(__0, out obj);
        }
        if (!travelerFound && !DariusFormalRegistry.TryGetResourceByExactType(__0, out obj)) return true;
        __result = obj;
        string typeKey = __0 != null ? __0.FullName : "<null>";
        DariusLog.DebugInfoThrottled("RES-TYPE", typeKey, "GetByType intercepted type=" + typeKey +
            " obj=" + (obj != null ? obj.name : "<null>"), 1.25);
        return false;
    }

    private static bool GenericStarEffectLookupPrefix(Type __0, ref StarEffect __result)
    {
        if (__0 == null || !typeof(StarEffect).IsAssignableFrom(__0)) return true;
        StarEffect star;
        if (TryResolveRuntimeStar(__0, out star))
        {
            __result = star;
            return false;
        }
        return true;
    }

    public static bool TryResolveRuntimeStar(Type type, out StarEffect star)
    {
        star = null;
        if (type == null || !typeof(StarEffect).IsAssignableFrom(type)) return false;

        StarEffect cached;
        if (ResolvedStarByType.TryGetValue(type, out cached))
        {
            if (cached != null)
            {
                star = cached;
                return true;
            }
            ResolvedStarByType.Remove(type);
        }

        // Native StarEffects have complete database and Addressables registration. Let the stock
        // method handle them immediately. The old resolver rebuilt an AppDomain-wide type catalog
        // after every native miss, which dominated every constellation refresh.
        string assemblyName;
        try { assemblyName = type.Assembly.GetName().Name; }
        catch { return false; }
        int marker = !string.IsNullOrEmpty(assemblyName)
            ? assemblyName.IndexOf("Prototype", StringComparison.Ordinal)
            : -1;
        if (marker <= 0) return false;

        try
        {
            // DewMod hot loading appends a timestamp to the assembly name
            // (for example YasuoPrototype_639238800...). The registry type itself keeps its
            // stable owner name, so derive it from the text before Prototype.
            string owner = assemblyName.Substring(0, marker);
            Type registryType = type.Assembly.GetType(owner + "FormalRegistry", false);
            MethodInfo resolver = registryType != null
                ? registryType.GetMethod("TryGetResourceByExactType", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static,
                    null, new[] { typeof(Type), typeof(UnityEngine.Object).MakeByRefType() }, null)
                : null;
            if (resolver == null) return false;

            object[] args = { type, null };
            if (!(bool)resolver.Invoke(null, args) || !(args[1] is StarEffect resolved)) return false;
            ResolvedStarByType[type] = resolved;
            star = resolved;
            return true;
        }
        catch (Exception e)
        {
            DariusLog.Exception("RES-STAR-GENERIC", e, "Exact runtime StarEffect resolution failed type=" + type.FullName);
            return false;
        }
    }

    private static void IsSkillIncludedPostfix(string __0, ref bool __result)
    {
        if (DariusFormalRegistry.IsDariusSkillKey(__0))
        {
            __result = true;
            DariusLog.DebugInfoThrottled("RESOURCE-COMPAT", "skill:" + __0, "Forced IsSkillIncludedInGame=true for " + __0, 2.0);
        }
    }

    private static void IsGemIncludedPostfix(string __0, ref bool __result)
    {
        if (DariusFormalRegistry.IsDariusGemKey(__0))
        {
            __result = true;
            DariusLog.DebugInfoThrottled("RESOURCE-COMPAT", "gem:" + __0, "Forced IsGemIncludedInGame=true for " + __0, 2.0);
        }
    }

    private static void IsStarIncludedPostfix(string __0, ref bool __result)
    {
        if (DariusConstellationLocalization.IsDariusStarKey(__0))
        {
            __result = true;
            DariusLog.DebugInfoThrottled("RESOURCE-COMPAT", "star:" + __0, "Forced IsStarIncludedInGame=true for " + __0, 2.0);
        }
    }

    private static void IsSkinIncludedPostfix(string __0, ref bool __result)
    {
        if (DariusTravelerRegistry.IsRuntimeSkinKey(__0))
        {
            __result = true;
            DariusLog.DebugInfoThrottled("RESOURCE-COMPAT", "skin:" + __0, "Forced IsSkinIncludedInGame=true for " + __0, 2.0);
        }
    }

    private static void EntityAbilityOnStartServerPrefix(EntityAbility __instance)
    {
        if (__instance == null) return;
        Hero_Darius hero = null;
        try { hero = __instance.GetComponent<Hero_Darius>(); } catch { }
        if (hero == null) return;
        try
        {
            DariusNativeAttackBinder binder = hero.GetComponent<DariusNativeAttackBinder>();
            if (binder == null) binder = hero.gameObject.AddComponent<DariusNativeAttackBinder>();
            binder.EnsureBound("EntityAbility.OnStartServer prefix");
            DariusLog.Info("ATK-NATIVE-BIND", "Verified Darius-owned attack preset immediately before native EntityAbility.OnStartServer.");
        }
        catch (Exception e)
        {
            DariusLog.Exception("ATK-NATIVE-BIND", e, "EntityAbility.OnStartServer binding failed");
        }
    }

    private static void ActorPrepareAndSpawnPrefix(Actor __instance)
    {
        if (__instance == null) return;
        bool isDariusRuntimeActor = (__instance is Hero_Darius) ||
                                    (__instance is At_DariusAxe) ||
                                    (__instance is Ai_DariusAxe) ||
                                    (__instance is Ai_DariusAxe_Crit) ||
                                    DariusFormalRegistry.IsDariusRuntimeObject(__instance);
        if (!isDariusRuntimeActor) return;
        try
        {
            GameObject go = __instance.gameObject;
            if (go != null && !go.activeSelf)
            {
                go.SetActive(true);
                DariusLog.DebugInfo("SPAWN-COMPAT", "Activated runtime Darius actor before PrepareAndSpawn: " + __instance.name);
            }

            // Do not rebuild NetworkIdentity.NetworkBehaviours on a live actor here. Mirror initializes
            // that cache during the normal Awake/spawn lifecycle. Re-running it immediately before
            // PrepareAndSpawn can invalidate Host-client serialization state and cause a local
            // disconnect when the basic-attack instance is spawned. Template-time initialization
            // remains in DariusTravelerRegistry.CreateAndRegisterNativeBasicAttack().
        }
        catch (Exception e)
        {
            DariusLog.Exception("SPAWN-COMPAT", e, "PrepareAndSpawn activation guard failed");
        }
    }

    // Add Darius to the same server-side pools used by normal Memory/Essence rewards.
    // This mirrors the reference mod's LootManager.OnStartServer patch, and handles gems symmetrically.
    private static void LootManagerOnStartServerPostfix(LootManager __instance)
    {
        if (__instance == null) return;
        try
        {
            AddPoolEntries(__instance, "poolSkills", "poolSkillsByRarity", DariusFormalRegistry.SkillPoolEntries);
            AddPoolEntries(__instance, "poolGems", "poolGemsByRarity", DariusFormalRegistry.GemPoolEntries);
            DariusLog.Info("LOOT-POOL", "Darius W/E added to normal Memory pool. Q/R/Identity stay in Hero_Darius loadout slots.");
        }
        catch (Exception e)
        {
            DariusLog.Exception("LOOT-POOL", e, "Failed to inject Darius into LootManager pools");
        }
    }

    private static void AddPoolEntries(object manager, string poolFieldName, string rarityFieldName, IEnumerable<KeyValuePair<string, Rarity>> entries)
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        Type t = manager.GetType();
        FieldInfo poolField = t.GetField(poolFieldName, flags);
        FieldInfo rarityField = t.GetField(rarityFieldName, flags);
        IList pool = poolField != null ? poolField.GetValue(manager) as IList : null;
        IDictionary byRarity = rarityField != null ? rarityField.GetValue(manager) as IDictionary : null;

        foreach (KeyValuePair<string, Rarity> entry in entries)
        {
            if (pool != null && !ContainsListValue(pool, entry.Key)) pool.Add(entry.Key);
            if (byRarity != null && byRarity.Contains(entry.Value))
            {
                IList rarityList = byRarity[entry.Value] as IList;
                if (rarityList != null && !ContainsListValue(rarityList, entry.Key)) rarityList.Add(entry.Key);
            }
        }
    }

    private static bool ContainsListValue(IList list, object value)
    {
        if (list == null) return false;
        foreach (object item in list)
            if (Equals(item, value)) return true;
        return false;
    }
}
