using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using HarmonyLib;
using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;

// Low-frequency runtime flight recorder used by lifecycle acceptance tests.
// It is deliberately read-only: diagnostics may inspect Darius-owned runtime state and public
// Dew lookups, but must never repair resources, mutate profile data, or rebuild scene objects.
internal static class DariusRuntimeAudit
{
    private static bool _hooksInstalled;
    private static readonly HashSet<int> DariusDisplayIds = new HashSet<int>();

    internal static void ResetHooks()
    {
        _hooksInstalled = false;
        DariusDisplayIds.Clear();
    }

    internal static void InstallHooks(Harmony harmony)
    {
        if (_hooksInstalled || harmony == null) return;

        int patched = 0;
        try
        {
            Type displayType = AccessTools.TypeByName("CharacterModelDisplay");
            MethodInfo displaySetup = displayType != null
                ? Array.Find(displayType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic),
                    m => m.Name == "Setup" && m.GetParameters().Length >= 1 &&
                         m.GetParameters()[0].ParameterType == typeof(string))
                : null;
            patched += PatchOptional(harmony, displaySetup, null, nameof(CharacterModelDisplaySetupPostfix),
                "CharacterModelDisplay.Setup");

            MethodInfo[] spawnMethods = typeof(NetworkServer).GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            for (int i = 0; i < spawnMethods.Length; i++)
            {
                MethodInfo method = spawnMethods[i];
                ParameterInfo[] ps = method.GetParameters();
                if (method.Name == "Spawn" && ps.Length >= 1 && ps[0].ParameterType == typeof(GameObject))
                    patched += PatchOptional(harmony, method, nameof(NetworkServerSpawnTemplatePrefix), null,
                        "NetworkServer.Spawn");
            }

            MethodInfo shutdown = Array.Find(
                typeof(NetworkServer).GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic),
                m => m.Name == "Shutdown" && m.GetParameters().Length == 0);
            patched += PatchOptional(harmony, shutdown, nameof(NetworkServerShutdownPrefix), null,
                "NetworkServer.Shutdown");

            MethodInfo[] destroyMethods = typeof(NetworkServer).GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            for (int i = 0; i < destroyMethods.Length; i++)
            {
                MethodInfo method = destroyMethods[i];
                ParameterInfo[] ps = method.GetParameters();
                if (method.Name == "Destroy" && ps.Length >= 1 && ps[0].ParameterType == typeof(GameObject))
                    patched += PatchOptional(harmony, method, nameof(NetworkServerDestroyTemplatePrefix), null,
                        "NetworkServer.Destroy");
            }

            MethodInfo[] spawnManagerMethods = typeof(SpawnManager).GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            for (int i = 0; i < spawnManagerMethods.Length; i++)
            {
                MethodInfo method = spawnManagerMethods[i];
                ParameterInfo[] ps = method.GetParameters();
                if (method.Name == "Destroy" && ps.Length >= 1 && ps[0].ParameterType == typeof(GameObject))
                    patched += PatchOptional(harmony, method, nameof(SpawnManagerDestroyTemplatePrefix), null,
                        "SpawnManager.Destroy");
            }

            MethodInfo identityDestroy = Array.Find(
                typeof(NetworkIdentity).GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic),
                m => m.Name == "OnDestroy" && m.GetParameters().Length == 0);
            patched += PatchOptional(harmony, identityDestroy, nameof(NetworkIdentityOnDestroyTemplatePrefix), null,
                "NetworkIdentity.OnDestroy");

            _hooksInstalled = true;
            DariusLog.Info("AUDIT-HOOK", "Read-only runtime audit hooks installed count=" + patched + ".");
        }
        catch (Exception e)
        {
            _hooksInstalled = false;
            DariusLog.Exception("AUDIT-HOOK", e, "Installing read-only runtime audit hooks failed");
        }
    }

    private static int PatchOptional(Harmony harmony, MethodInfo target, string prefixName, string postfixName, string label)
    {
        if (target == null)
        {
            DariusLog.DebugInfo("AUDIT-HOOK", "Optional diagnostic target unavailable: " + label);
            return 0;
        }

        try
        {
            HarmonyMethod prefix = string.IsNullOrEmpty(prefixName)
                ? null
                : new HarmonyMethod(AccessTools.Method(typeof(DariusRuntimeAudit), prefixName));
            HarmonyMethod postfix = string.IsNullOrEmpty(postfixName)
                ? null
                : new HarmonyMethod(AccessTools.Method(typeof(DariusRuntimeAudit), postfixName));
            harmony.Patch(target, prefix: prefix, postfix: postfix);
            return 1;
        }
        catch (Exception e)
        {
            DariusLog.Exception("AUDIT-HOOK", e, "Optional diagnostic patch failed: " + label);
            return 0;
        }
    }

    internal static void LogSnapshot(string reason, bool includeObjectCensus)
    {
        string token = string.IsNullOrEmpty(reason) ? "<unspecified>" : reason;
        try
        {
            DariusLog.Info("AUDIT", "BEGIN reason=" + token +
                " generation=" + DariusPrototypeMod.ActiveGenerationId +
                " activeModOwner=" + DariusPrototypeMod.ActiveModInstanceId);
        }
        catch { }

        LogSection("AUDIT-SCENE", token, BuildSceneState);
        LogSection("AUDIT-MEMORY", token, BuildMemoryState);
        LogSection("AUDIT-TRAVELER", token, DariusTravelerRegistry.DiagnosticState);
        LogSection("AUDIT-FORMAL", token, DariusFormalRegistry.DiagnosticState);
        LogSection("AUDIT-DEWDB", token, () => DariusUnsupportedResourceBridge.DiagnosticDatabaseState(DewResources.database));
        LogSection("AUDIT-CONTENT", token, BuildContentState);
        LogSection("AUDIT-PROFILE", token, BuildProfileState);
        LogSection("AUDIT-LOOKUP", token, BuildLookupState);
        LogSection("AUDIT-ASSETS", token, BuildAssetState);

        if (includeObjectCensus)
        {
            LogCensus<DariusPrototypeMod>("ModBehaviour", null, 8, token);
            LogCensus<DariusTravelerLifecycleBridge>("LifecycleBridge", null, 8, token);
            LogCensus<Hero_Darius>("Hero_Darius", null, 12, token);
            LogCensus<Skin>("DariusSkin", s => s != null && !string.IsNullOrEmpty(s.name) &&
                s.name.StartsWith("Skin_Darius_", StringComparison.Ordinal), 12, token);
            LogCensus<DariusOfficialEntityModelMarker>("EntityModelMarker", null, 16, token);
            LogCensus<At_DariusAxe>("At_DariusAxe", null, 10, token);
            LogCensus<St_Darius_NoxianGuillotine>("RTrigger", null, 10, token);
        }

        DariusLog.Info("AUDIT", "END reason=" + token);
    }

    private static void LogSection(string category, string reason, Func<string> builder)
    {
        try
        {
            DariusLog.Info(category, "reason=" + reason + " " + builder());
        }
        catch (Exception e)
        {
            DariusLog.Exception(category, e, "Snapshot section failed reason=" + reason);
        }
    }

    private static string BuildSceneState()
    {
        StringBuilder sb = new StringBuilder();
        Scene active = SceneManager.GetActiveScene();
        sb.Append("active=").Append(DescribeScene(active))
          .Append(" loadedSceneCount=").Append(SceneManager.sceneCount)
          .Append(" loaded=[");
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            if (i > 0) sb.Append(";");
            sb.Append(DescribeScene(SceneManager.GetSceneAt(i)));
        }
        sb.Append("] server=").Append(SafeBool(() => NetworkServer.active))
          .Append(" client=").Append(SafeBool(() => NetworkClient.active))
          .Append(" frame=").Append(Time.frameCount)
          .Append(" t=").Append(Time.time.ToString("0.000"))
          .Append(" unscaled=").Append(Time.unscaledTime.ToString("0.000"));
        return sb.ToString();
    }

    private static string BuildMemoryState()
    {
        long managed = 0;
        long workingSet = 0;
        try { managed = GC.GetTotalMemory(false); } catch { }
        try { workingSet = System.Diagnostics.Process.GetCurrentProcess().WorkingSet64; } catch { }
        return "managedMB=" + (managed / 1048576.0).ToString("0.0") +
               " workingSetMB=" + (workingSet / 1048576.0).ToString("0.0") +
               " gc0=" + SafeGcCount(0) +
               " gc1=" + SafeGcCount(1) +
               " gc2=" + SafeGcCount(2);
    }

    private static string BuildContentState()
    {
        DewGameContentSettings content = null;
        try { content = DewBuildProfile.current != null ? DewBuildProfile.current.content : null; } catch { }

        StringBuilder sb = new StringBuilder();
        sb.Append("buildProfile=").Append(Identity(DewBuildProfile.current))
          .Append(" content=").Append(Identity(content));
        if (content != null)
        {
            sb.Append(" serializedHeroes=").Append(content._availableHeroes != null ? content._availableHeroes.Length : -1)
              .Append(" runtimeHeroes=").Append(content.availableHeroes != null ? content.availableHeroes.Count : -1)
              .Append(" dariusHero(serialized/runtime)=")
              .Append(Contains(content._availableHeroes, DariusTravelerRegistry.HeroName)).Append("/")
              .Append(content.availableHeroes != null && content.availableHeroes.Contains(DariusTravelerRegistry.HeroName))
              .Append(" dariusSkills(serialized/runtime)=")
              .Append(CountPrefix(content._availableSkills, "St_Darius_", "St_D_Darius_")).Append("/")
              .Append(CountPrefix(content.availableSkills, "St_Darius_", "St_D_Darius_"))
              .Append(" dariusStars(serialized/runtime)=")
              .Append(CountPrefix(content._availableStars, "Se_Star_Darius_")).Append("/")
              .Append(CountPrefix(content.availableStars, "Se_Star_Darius_"));
        }

        try
        {
            sb.Append(" DewTypes heroes=").Append(Dew.allHeroes.Count)
              .Append(" skills=").Append(Dew.allSkills.Count)
              .Append(" heroSkills=").Append(Dew.allHeroSkills.Count)
              .Append(" stars=").Append(Dew.allStarTypes.Count)
              .Append(" containsDarius hero=").Append(ContainsType(Dew.allHeroes, typeof(Hero_Darius)))
              .Append(" Q=").Append(ContainsType(Dew.allSkills, typeof(St_Darius_Decimate)))
              .Append(" R=").Append(ContainsType(Dew.allSkills, typeof(St_Darius_NoxianGuillotine)))
              .Append(" identity=").Append(ContainsType(Dew.allSkills, typeof(St_D_Darius_Hemorrhage)));
        }
        catch (Exception e)
        {
            sb.Append(" DewTypesError=").Append(e.GetType().Name);
        }
        return sb.ToString();
    }

    private static string BuildProfileState()
    {
        DewProfile profile = null;
        DewProfileStats stats = null;
        try { profile = DewSave.profileMain; } catch { }
        try { stats = DewSave.profileStats; } catch { }

        StringBuilder sb = new StringBuilder();
        sb.Append("profile=").Append(Identity(profile))
          .Append(" stats=").Append(Identity(stats));

        if (profile != null)
        {
            bool heroEntry = profile.heroes != null && profile.heroes.ContainsKey(DariusTravelerRegistry.HeroName);
            string selectedSkin = null;
            if (profile.heroSelectedSkins != null)
                profile.heroSelectedSkins.TryGetValue(DariusTravelerRegistry.HeroName, out selectedSkin);
            List<HeroLoadoutData> pages = null;
            if (profile.heroLoadouts != null)
                profile.heroLoadouts.TryGetValue(DariusTravelerRegistry.HeroName, out pages);

            int purchasedStars = 0;
            if (profile.newStars != null)
            {
                foreach (string key in profile.newStars.Keys)
                    if (!string.IsNullOrEmpty(key) && key.StartsWith("Se_Star_Darius_", StringComparison.Ordinal))
                        purchasedStars++;
            }

            sb.Append(" heroEntry=").Append(heroEntry)
              .Append(" selectedSkin=").Append(selectedSkin ?? "<null>")
              .Append(" loadoutPages=").Append(pages != null ? pages.Count : -1)
              .Append(" equippedDariusStars=").Append(CountEquippedDariusStars(pages))
              .Append(" purchasedDariusStars=").Append(purchasedStars);
        }

        if (stats != null)
        {
            sb.Append(" statsHero=").Append(stats.heroes != null && stats.heroes.ContainsKey(DariusTravelerRegistry.HeroName))
              .Append(" statsSkills=").Append(CountPrefix(stats.skills != null ? stats.skills.Keys : null, "St_Darius_", "St_D_Darius_"));
        }

        try
        {
            GameSettingsManager manager = NetworkedManagerBase<GameSettingsManager>.instance;
            object settings = manager != null ? manager.GetLocalPreferredGameSettings() : null;
            sb.Append(" localSettings=").Append(Identity(settings));
            if (settings != null)
            {
                PreferredGameSettings preferred = settings as PreferredGameSettings;
                if (preferred != null && preferred.heroSelectedLoadoutIndex != null)
                {
                    int selected;
                    sb.Append(" DariusLoadoutIndex=")
                      .Append(preferred.heroSelectedLoadoutIndex.TryGetValue(DariusTravelerRegistry.HeroName, out selected)
                          ? selected.ToString()
                          : "<missing>");
                }
                sb.Append(" heroMembers={").Append(DescribeInterestingMembers(settings)).Append("}");
            }
        }
        catch (Exception e)
        {
            sb.Append(" localSettingsError=").Append(e.GetType().Name);
        }

        return sb.ToString();
    }

    private static string BuildLookupState()
    {
        if (DewResources.database == null) return "resourceDB=<null>";
        if (!DariusRuntimeResourceCompatibility.IsInstalled)
            return "bridgeInstalled=false; active lookup probes skipped to avoid Addressables side effects";

        StringBuilder sb = new StringBuilder();
        try
        {
            Hero hero = DewResources.GetByShortTypeName<Hero>(DariusTravelerRegistry.HeroName);
            sb.Append("HeroByShort=").Append(DescribeUnityObject(hero))
              .Append(" matchRegistry=").Append(ReferenceEquals(hero, DariusTravelerRegistry.HeroPrefab));
        }
        catch (Exception e) { sb.Append(" HeroByShortError=").Append(e.GetType().Name).Append(":").Append(e.Message); }

        try
        {
            Skin skin = DewResources.GetByName<Skin>(DariusTravelerRegistry.DefaultSkinName);
            sb.Append(" SkinByName=").Append(DescribeUnityObject(skin))
              .Append(" matchRegistry=").Append(ReferenceEquals(skin, DariusTravelerRegistry.DefaultSkin));
        }
        catch (Exception e) { sb.Append(" SkinByNameError=").Append(e.GetType().Name).Append(":").Append(e.Message); }

        AppendSkillLookup<AttackTrigger>(sb, DariusTravelerRegistry.AttackName, DariusTravelerRegistry.AttackPrefab, "Attack");
        AppendSkillLookup<SkillTrigger>(sb, "St_Darius_Decimate", DariusFormalRegistry.Decimate, "Q");
        AppendSkillLookup<SkillTrigger>(sb, "St_Darius_NoxianGuillotine", DariusFormalRegistry.NoxianGuillotine, "R");
        AppendSkillLookup<SkillTrigger>(sb, "St_D_Darius_Hemorrhage", DariusFormalRegistry.Hemorrhage, "Identity");
        return sb.ToString();
    }

    private static void AppendSkillLookup<T>(StringBuilder sb, string name, UnityEngine.Object expected, string label)
        where T : UnityEngine.Object
    {
        try
        {
            T value = DewResources.GetByShortTypeName<T>(name);
            sb.Append(" ").Append(label).Append("ByShort=").Append(DescribeUnityObject(value))
              .Append(" matchRegistry=").Append(ReferenceEquals(value, expected));
        }
        catch (Exception e)
        {
            sb.Append(" ").Append(label).Append("ByShortError=").Append(e.GetType().Name).Append(":").Append(e.Message);
        }
    }

    private static string BuildAssetState()
    {
        return DariusNativeModelAssets.DiagnosticState() + " | " +
               DariusMedia.DiagnosticState() + " | " +
               DariusPrototypeIcons.DiagnosticState() + " | " +
               DariusLolVfxRuntime.DiagnosticState();
    }

    internal static string DescribeUnityObject(UnityEngine.Object obj)
    {
        if (ReferenceEquals(obj, null)) return "<managed-null>";

        StringBuilder sb = new StringBuilder();
        bool fakeNull = false;
        try { fakeNull = obj == null; } catch { }
        sb.Append(obj.GetType().Name)
          .Append(":").Append(SafeName(obj))
          .Append("#").Append(SafeInstanceId(obj))
          .Append(" fakeNull=").Append(fakeNull)
          .Append(" hide=").Append(SafeHideFlags(obj));

        Component component = obj as Component;
        GameObject go = obj as GameObject;
        if (!ReferenceEquals(component, null))
        {
            try { go = component.gameObject; } catch { }
        }

        if (!ReferenceEquals(go, null))
        {
            try
            {
                sb.Append(" scene=").Append(DescribeScene(go.scene))
                  .Append(" activeSelf=").Append(go.activeSelf)
                  .Append(" activeHierarchy=").Append(go.activeInHierarchy)
                  .Append(" parent=").Append(go.transform.parent != null ? go.transform.parent.name + "#" + go.transform.parent.GetInstanceID() : "<root>");
                NetworkIdentity identity = go.GetComponent<NetworkIdentity>();
                if (identity != null)
                {
                    sb.Append(" netId=").Append(identity.netId)
                      .Append(" assetId=").Append(identity.assetId)
                      .Append(" isServer=").Append(identity.isServer)
                      .Append(" isClient=").Append(identity.isClient);
                }
            }
            catch (Exception e)
            {
                sb.Append(" goStateError=").Append(e.GetType().Name);
            }
        }

        return sb.ToString();
    }

    private static void LogCensus<T>(string label, Predicate<T> predicate, int limit, string reason)
        where T : UnityEngine.Object
    {
        try
        {
            T[] all = Resources.FindObjectsOfTypeAll<T>();
            List<string> entries = new List<string>();
            int count = 0;
            for (int i = 0; i < all.Length; i++)
            {
                T item = all[i];
                if (ReferenceEquals(item, null)) continue;
                if (predicate != null && !predicate(item)) continue;
                count++;
                if (entries.Count < limit) entries.Add(DescribeUnityObject(item));
            }
            DariusLog.Info("AUDIT-OBJECTS", "reason=" + reason + " type=" + label +
                " count=" + count + " sample=[" + string.Join(" | ", entries.ToArray()) + "]");
        }
        catch (Exception e)
        {
            DariusLog.Exception("AUDIT-OBJECTS", e, "Object census failed type=" + label + " reason=" + reason);
        }
    }

    private static void CharacterModelDisplaySetupPostfix(object __instance, string __0)
    {
        if (__instance == null) return;

        try
        {
            Component component = __instance as Component;
            if (component == null)
            {
                DariusLog.Warn("AUDIT-DISPLAY", "CharacterModelDisplay.Setup instance is not a Component. skin=" + (__0 ?? "<null>"));
                return;
            }

            int displayId = component.GetInstanceID();
            bool isDarius = string.Equals(__0, DariusTravelerRegistry.DefaultSkinName, StringComparison.Ordinal);
            bool wasDarius = DariusDisplayIds.Contains(displayId);
            if (!isDarius)
            {
                if (wasDarius)
                {
                    DariusLog.Info("AUDIT-DISPLAY", "CharacterModelDisplay reused away from Darius display=" +
                        DescribeUnityObject(component) + " newSkin=" + (__0 ?? "<null>"));
                    DariusDisplayIds.Remove(displayId);
                }
                return;
            }
            DariusDisplayIds.Add(displayId);

            Transform root = component.transform;
            Transform[] all = root.GetComponentsInChildren<Transform>(true);
            List<string> children = new List<string>();
            for (int i = 0; i < all.Length && children.Count < 20; i++)
            {
                Transform child = all[i];
                if (child == null || child == root) continue;
                children.Add(child.name + "#" + child.GetInstanceID() +
                    "@scene=" + (child.gameObject.scene.IsValid() ? child.gameObject.scene.name : "<invalid>") +
                    "@pos=" + DariusLog.Vec(child.localPosition) +
                    "@scale=" + DariusLog.Vec(child.localScale) +
                    "@activeSelf=" + child.gameObject.activeSelf +
                    "@activeHierarchy=" + child.gameObject.activeInHierarchy);
            }

            DariusLog.Info("AUDIT-DISPLAY",
                "CharacterModelDisplay.Setup skin=" + __0 +
                " display=" + DescribeUnityObject(component) +
                " worldPos=" + DariusLog.Vec(root.position) +
                " localPos=" + DariusLog.Vec(root.localPosition) +
                " localScale=" + DariusLog.Vec(root.localScale) +
                " descendants=" + Math.Max(0, all.Length - 1) +
                " childSample=[" + string.Join(";", children.ToArray()) + "]" +
                " stack=" + Environment.StackTrace);
        }
        catch (Exception e)
        {
            DariusLog.Exception("AUDIT-DISPLAY", e, "Failed inspecting Darius CharacterModelDisplay.Setup");
        }
    }

    private static void CharacterModelDisplayLifecyclePrefix(object __instance, MethodBase __originalMethod)
    {
        Component component = __instance as Component;
        if (component == null) return;
        int id;
        try { id = component.GetInstanceID(); } catch { return; }
        if (!DariusDisplayIds.Contains(id)) return;

        string boundary = __originalMethod != null ? __originalMethod.Name : "lifecycle";
        DariusLog.Info("AUDIT-DISPLAY", "Darius CharacterModelDisplay " + boundary +
            " display=" + DescribeUnityObject(component) +
            " stack=" + Environment.StackTrace);
        if (string.Equals(boundary, "OnDestroy", StringComparison.Ordinal))
            DariusDisplayIds.Remove(id);
    }

    private static void NetworkServerSpawnTemplatePrefix(GameObject __0)
    {
        string label;
        if (!DariusTravelerRegistry.TryGetRuntimeNetworkTemplateLabel(__0, out label)) return;
        DariusLog.Warn("AUDIT-NET",
            "NetworkServer.Spawn received a registered Darius template itself label=" + label +
            " object=" + DescribeUnityObject(__0) + " stack=" + Environment.StackTrace);
    }

    private static void NetworkServerShutdownPrefix()
    {
        LogSnapshot("NetworkServer.Shutdown prefix", true);
    }

    private static void NetworkServerDestroyTemplatePrefix(GameObject __0)
    {
        LogTemplateDestroy(__0, "NetworkServer.Destroy");
    }

    private static void SpawnManagerDestroyTemplatePrefix(GameObject __0)
    {
        LogTemplateDestroy(__0, "SpawnManager.Destroy");
    }

    private static void NetworkIdentityOnDestroyTemplatePrefix(NetworkIdentity __instance)
    {
        LogTemplateDestroy(__instance, "NetworkIdentity.OnDestroy");
    }

    private static void LogTemplateDestroy(UnityEngine.Object candidate, string boundary)
    {
        string label;
        if (!DariusTravelerRegistry.TryGetRuntimeNetworkTemplateLabel(candidate, out label)) return;
        DariusLog.Warn("AUDIT-DESTROY",
            "Darius runtime template reached " + boundary +
            " label=" + label +
            " object=" + DescribeUnityObject(candidate) +
            " stack=" + Environment.StackTrace);
    }

    internal static string DescribeAbilityState(object trigger, int requestedConfigIndex)
    {
        if (trigger == null) return "<trigger-null>";
        StringBuilder sb = new StringBuilder();
        UnityEngine.Object unity = trigger as UnityEngine.Object;
        if (!ReferenceEquals(unity, null)) sb.Append("trigger=").Append(DescribeUnityObject(unity));
        else sb.Append("triggerType=").Append(trigger.GetType().FullName);

        string[] triggerMembers =
        {
            "owner",
            "abilityIndex",
            "currentConfigIndex",
            "currentConfigCurrentCharge",
            "currentConfigCooldownTime",
            "currentConfigUnscaledCooldownTime",
            "currentConfigCurrentMinimumDelay",
            "currentConfigMaxCooldownTime",
            "level"
        };
        for (int i = 0; i < triggerMembers.Length; i++)
            AppendReflectedMember(sb, trigger, triggerMembers[i]);

        object configs;
        if (TryReadMemberRecursive(trigger, "configs", out configs))
        {
            Array array = configs as Array;
            sb.Append(" configs=").Append(array != null ? array.Length.ToString() : SummarizeValue(configs));
            if (array != null && requestedConfigIndex >= 0 && requestedConfigIndex < array.Length)
            {
                object cfg = array.GetValue(requestedConfigIndex);
                sb.Append(" requestedConfig=").Append(requestedConfigIndex);
                if (cfg == null) sb.Append("(<null>)");
                else
                {
                    string[] configMembers =
                    {
                        "spawnedInstance",
                        "isActive",
                        "alwaysCastImmediately",
                        "faceForward",
                        "canReceiveCooldownReduction",
                        "castMethod"
                    };
                    for (int i = 0; i < configMembers.Length; i++)
                        AppendReflectedMember(sb, cfg, configMembers[i]);

                    object castMethod;
                    if (TryReadMemberRecursive(cfg, "castMethod", out castMethod) && castMethod != null)
                    {
                        string[] castMembers = { "type", "_range", "_radius", "_angle", "_isClamping" };
                        for (int i = 0; i < castMembers.Length; i++)
                            AppendReflectedMember(sb, castMethod, castMembers[i]);
                    }
                }
            }
        }

        try
        {
            MethodInfo canCast = trigger.GetType().GetMethod(
                "CanBeCast",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                Type.EmptyTypes,
                null);
            if (canCast != null) sb.Append(" canCast=").Append(canCast.Invoke(trigger, null));
        }
        catch (Exception e)
        {
            sb.Append(" canCastError=").Append(e.GetType().Name);
        }
        return sb.ToString();
    }

    private static void AppendReflectedMember(StringBuilder sb, object target, string name)
    {
        object value;
        if (!TryReadMemberRecursive(target, name, out value)) return;
        sb.Append(" ").Append(name).Append("=").Append(SummarizeValue(value));
    }

    private static bool TryReadMemberRecursive(object target, string name, out object value)
    {
        value = null;
        if (target == null || string.IsNullOrEmpty(name)) return false;
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        for (Type type = target.GetType(); type != null; type = type.BaseType)
        {
            try
            {
                FieldInfo field = type.GetField(name, flags) ??
                                  type.GetField("<" + name + ">k__BackingField", flags);
                if (field != null)
                {
                    value = field.GetValue(target);
                    return true;
                }

                PropertyInfo property = type.GetProperty(name, flags);
                if (property != null && property.CanRead && property.GetIndexParameters().Length == 0)
                {
                    value = property.GetValue(target, null);
                    return true;
                }
            }
            catch { }
        }
        return false;
    }

    private static string DescribeInterestingMembers(object target)
    {
        if (target == null) return "<null>";
        List<string> values = new List<string>();
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        for (Type type = target.GetType(); type != null && values.Count < 24; type = type.BaseType)
        {
            FieldInfo[] fields = null;
            PropertyInfo[] props = null;
            try { fields = type.GetFields(flags); } catch { }
            if (fields != null)
            {
                for (int i = 0; i < fields.Length && values.Count < 24; i++)
                {
                    FieldInfo field = fields[i];
                    if (field == null || !InterestingName(field.Name)) continue;
                    try { values.Add(type.Name + "." + field.Name + "=" + SummarizeValue(field.GetValue(target))); } catch { }
                }
            }

            try { props = type.GetProperties(flags); } catch { }
            if (props != null)
            {
                for (int i = 0; i < props.Length && values.Count < 24; i++)
                {
                    PropertyInfo prop = props[i];
                    if (prop == null || !prop.CanRead || prop.GetIndexParameters().Length != 0 || !InterestingName(prop.Name)) continue;
                    try { values.Add(type.Name + "." + prop.Name + "=" + SummarizeValue(prop.GetValue(target, null))); } catch { }
                }
            }
        }
        return string.Join(",", values.ToArray());
    }

    private static bool InterestingName(string name)
    {
        if (string.IsNullOrEmpty(name)) return false;
        return name.IndexOf("hero", StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("skin", StringComparison.OrdinalIgnoreCase) >= 0 ||
               name.IndexOf("loadout", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static string SummarizeValue(object value)
    {
        if (value == null) return "<null>";
        string text = value as string;
        if (text != null) return text;

        IDictionary dictionary = value as IDictionary;
        if (dictionary != null)
        {
            object dariusValue = null;
            try
            {
                if (dictionary.Contains(DariusTravelerRegistry.HeroName))
                    dariusValue = dictionary[DariusTravelerRegistry.HeroName];
            }
            catch { }
            return "Dictionary(count=" + dictionary.Count +
                   (dariusValue != null ? ",Hero_Darius=" + dariusValue : "") + ")";
        }

        ICollection collection = value as ICollection;
        if (collection != null) return value.GetType().Name + "(count=" + collection.Count + ")";

        Type type = value.GetType();
        if (type.IsPrimitive || type.IsEnum || value is decimal)
            return Convert.ToString(value);

        UnityEngine.Object unity = value as UnityEngine.Object;
        if (!ReferenceEquals(unity, null)) return DescribeUnityObject(unity);

        try
        {
            string result = value.ToString();
            return !string.IsNullOrEmpty(result) && result.Length <= 160 ? result : type.Name;
        }
        catch { return type.Name; }
    }

    private static int CountEquippedDariusStars(List<HeroLoadoutData> pages)
    {
        if (pages == null) return -1;
        int count = 0;
        for (int i = 0; i < pages.Count; i++)
        {
            HeroLoadoutData loadout = pages[i];
            if (loadout == null) continue;
            count += CountLoadoutStars(loadout.cDestruction);
            count += CountLoadoutStars(loadout.cLife);
            count += CountLoadoutStars(loadout.cImagination);
            count += CountLoadoutStars(loadout.cFlexible);
        }
        return count;
    }

    private static int CountLoadoutStars(List<LoadoutStarItem> items)
    {
        if (items == null) return 0;
        int count = 0;
        for (int i = 0; i < items.Count; i++)
        {
            string name = items[i].name;
            if (!string.IsNullOrEmpty(name) && name.StartsWith("Se_Star_Darius_", StringComparison.Ordinal))
                count++;
        }
        return count;
    }

    private static bool Contains(string[] values, string target)
    {
        if (values == null || string.IsNullOrEmpty(target)) return false;
        for (int i = 0; i < values.Length; i++)
            if (string.Equals(values[i], target, StringComparison.Ordinal)) return true;
        return false;
    }

    private static int CountPrefix(IEnumerable<string> values, params string[] prefixes)
    {
        if (values == null || prefixes == null) return 0;
        int count = 0;
        foreach (string value in values)
        {
            if (string.IsNullOrEmpty(value)) continue;
            for (int i = 0; i < prefixes.Length; i++)
            {
                string prefix = prefixes[i];
                if (!string.IsNullOrEmpty(prefix) && value.StartsWith(prefix, StringComparison.Ordinal))
                {
                    count++;
                    break;
                }
            }
        }
        return count;
    }

    private static bool ContainsType(IEnumerable<Type> values, Type desired)
    {
        if (values == null || desired == null) return false;
        foreach (Type value in values)
            if (value == desired) return true;
        return false;
    }

    private static string Identity(object value)
    {
        if (value == null) return "<null>";
        try { return value.GetType().Name + "@" + RuntimeHelpers.GetHashCode(value); }
        catch { return value.GetType().Name; }
    }

    private static string SafeName(UnityEngine.Object obj)
    {
        try { return obj != null ? obj.name : "<unity-null>"; } catch { return "<name-error>"; }
    }

    private static int SafeInstanceId(UnityEngine.Object obj)
    {
        try { return obj.GetInstanceID(); } catch { return 0; }
    }

    private static HideFlags SafeHideFlags(UnityEngine.Object obj)
    {
        try { return obj.hideFlags; } catch { return HideFlags.None; }
    }

    private static string DescribeScene(Scene scene)
    {
        try
        {
            return (scene.IsValid() ? scene.name : "<invalid>") +
                   "#" + scene.handle +
                   "(loaded=" + scene.isLoaded + ")";
        }
        catch { return "<scene-error>"; }
    }

    private static bool SafeBool(Func<bool> value)
    {
        try { return value(); } catch { return false; }
    }

    private static int SafeGcCount(int generation)
    {
        try { return GC.CollectionCount(generation); } catch { return -1; }
    }
}

public static partial class DariusTravelerRegistry
{
    internal static string DiagnosticState()
    {
        StringBuilder sb = new StringBuilder();
        sb.Append("registered=").Append(_registered)
          .Append(" registering=").Append(_registering)
          .Append(" ownerId=").Append(_modOwnerInstanceId)
          .Append(" owner=").Append(DariusRuntimeAudit.DescribeUnityObject(_modOwner))
          .Append(" root=").Append(DariusRuntimeAudit.DescribeUnityObject(_resourceRoot))
          .Append(" lifecycle=").Append(DariusRuntimeAudit.DescribeUnityObject(_lifecycleBridgeObject))
          .Append(" hero=").Append(DariusRuntimeAudit.DescribeUnityObject(HeroPrefab))
          .Append(" skin=").Append(DariusRuntimeAudit.DescribeUnityObject(DefaultSkin))
          .Append(" attack=").Append(DariusRuntimeAudit.DescribeUnityObject(AttackPrefab))
          .Append(" attackAi=").Append(DariusRuntimeAudit.DescribeUnityObject(AttackInstancePrefab))
          .Append(" critAi=").Append(DariusRuntimeAudit.DescribeUnityObject(AttackCritInstancePrefab))
          .Append(" resourcesGuid=").Append(ResourcesByGuid.Count)
          .Append(" resourcesType=").Append(ResourcesByType.Count)
          .Append(" skins=").Append(SkinsByName.Count)
          .Append(" networkPrefabs=").Append(NetworkPrefabs.Count)
          .Append(" spawnHandlers=").Append(RegisteredSpawnHandlerIds.Count)
          .Append(" ownedObjects=").Append(OwnedObjects.Count)
          .Append(" contentOwner=").Append(_contentOwner != null ? RuntimeHelpers.GetHashCode(_contentOwner) : 0);

        object database = DewResources.database;
        if (database != null)
        {
            sb.Append(" netMap(hero/atk/ai/crit)=")
              .Append(DariusUnsupportedResourceBridge.IsNetworkGuidMapped(database, HeroAssetId, HeroGuid)).Append("/")
              .Append(DariusUnsupportedResourceBridge.IsNetworkGuidMapped(database, AttackAssetId, AttackGuid)).Append("/")
              .Append(DariusUnsupportedResourceBridge.IsNetworkGuidMapped(database, AttackInstanceAssetId, AttackInstanceGuid)).Append("/")
              .Append(DariusUnsupportedResourceBridge.IsNetworkGuidMapped(database, AttackCritInstanceAssetId, AttackCritInstanceGuid));
        }
        return sb.ToString();
    }

    internal static bool TryGetRuntimeNetworkTemplateLabel(UnityEngine.Object candidate, out string label)
    {
        label = null;
        if (ReferenceEquals(candidate, null)) return false;

        GameObject go = candidate as GameObject;
        if (ReferenceEquals(go, null))
        {
            Component component = candidate as Component;
            if (!ReferenceEquals(component, null))
            {
                try { go = component.gameObject; } catch { }
            }
        }
        if (ReferenceEquals(go, null)) return false;

        try
        {
            if (HeroPrefab != null && ReferenceEquals(go, HeroPrefab.gameObject)) { label = HeroName; return true; }
            if (AttackPrefab != null && ReferenceEquals(go, AttackPrefab.gameObject)) { label = AttackName; return true; }
            if (AttackInstancePrefab != null && ReferenceEquals(go, AttackInstancePrefab.gameObject)) { label = AttackInstanceName; return true; }
            if (AttackCritInstancePrefab != null && ReferenceEquals(go, AttackCritInstancePrefab.gameObject)) { label = AttackCritInstanceName; return true; }
        }
        catch { }
        return false;
    }
}

public static partial class DariusFormalRegistry
{
    internal static string DiagnosticState()
    {
        bool healthy = false;
        string healthError = null;
        try { healthy = IsRegistrationHealthy(); }
        catch (Exception e) { healthError = e.GetType().Name + ":" + e.Message; }

        StringBuilder sb = new StringBuilder();
        sb.Append("registered=").Append(_registered)
          .Append(" healthy=").Append(healthy)
          .Append(" ownerId=").Append(_modOwnerInstanceId)
          .Append(" owner=").Append(DariusRuntimeAudit.DescribeUnityObject(_modOwner))
          .Append(" root=").Append(DariusRuntimeAudit.DescribeUnityObject(_runtimeActorRoot))
          .Append(" resources=").Append(ResourcesByGuid.Count)
          .Append(" registrations=").Append(RegistrationsByGuid.Count)
          .Append(" networkPrefabs=").Append(NetworkPrefabs.Count)
          .Append(" ownedPrefabs=").Append(OwnedPrefabs.Count)
          .Append(" Q=").Append(DariusRuntimeAudit.DescribeUnityObject(Decimate))
          .Append(" R=").Append(DariusRuntimeAudit.DescribeUnityObject(NoxianGuillotine))
          .Append(" Identity=").Append(DariusRuntimeAudit.DescribeUnityObject(Hemorrhage));
        if (!string.IsNullOrEmpty(healthError)) sb.Append(" healthError=").Append(healthError);

        if (_registered && !healthy)
        {
            List<string> bad = new List<string>();
            object database = DewResources.database;
            foreach (RuntimeRegistration record in RegistrationsByGuid.Values)
            {
                if (bad.Count >= 10) break;
                UnityEngine.Object resource;
                bool resourceOk = ResourcesByGuid.TryGetValue(record.guid, out resource) && resource != null && resource.GetType() == record.type;
                bool typedOk = database != null && DariusUnsupportedResourceBridge.IsTypedResourceIdentityMapped(
                    database, record.type, record.name, record.guid);
                GameObject prefab;
                bool prefabOk = NetworkPrefabs.TryGetValue(record.assetId, out prefab) && prefab != null;
                bool netOk = database != null && DariusUnsupportedResourceBridge.IsNetworkGuidMapped(database, record.assetId, record.guid);
                if (!resourceOk || !typedOk || !record.networkHandlerRegistered || !prefabOk || !netOk)
                    bad.Add(record.name + "(res=" + resourceOk + ",typed=" + typedOk + ",handler=" +
                        record.networkHandlerRegistered + ",prefab=" + prefabOk + ",net=" + netOk + ")");
            }
            if (bad.Count > 0) sb.Append(" bad=[").Append(string.Join(";", bad.ToArray())).Append("]");
        }
        return sb.ToString();
    }
}

internal static partial class DariusNativeModelAssets
{
    internal static string DiagnosticState()
    {
        return "nativeModel(bundle=" + (_bundle != null) +
               ",loadAttempted=" + _loadAttempted +
               ",prefabs=" + Prefabs.Count +
               ",overlayMeshes=" + OverlayMeshes.Count + ")";
    }
}

public static partial class DariusMedia
{
    internal static string DiagnosticState()
    {
        return "media(generation=" + _lifecycleGeneration +
               ",rootCached=" + (!string.IsNullOrEmpty(_root)) +
               ",textures=" + Textures.Count +
               ",clips=" + Clips.Count +
               ",sfxSkins=" + Pass2SfxBySkin.Count +
               ",voiceSkins=" + Pass2VoiceBySkin.Count +
               ",poolsLoaded=" + _pass2PoolsLoaded + ")";
    }
}

public static partial class DariusPrototypeIcons
{
    internal static string DiagnosticState()
    {
        return "icons(cache=" + Cache.Count + ")";
    }
}

public static partial class DariusLolVfxRuntime
{
    internal static string DiagnosticState()
    {
        return "lolVfx(loadAttempted=" + _loadAttempted +
               ",manifest=" + (_manifest != null) +
               ",systems=" + (_systems != null ? _systems.Count : 0) +
               ",textures=" + Textures.Count +
               ",meshes=" + Meshes.Count +
               ",materials=" + Materials.Count +
               ",quad=" + (_quad != null) + ")";
    }
}
