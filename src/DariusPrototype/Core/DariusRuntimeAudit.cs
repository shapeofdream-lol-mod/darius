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
    private static readonly HashSet<string> RuntimeConsumerStackKeys = new HashSet<string>(StringComparer.Ordinal);
    private static Type[] _lobbyConsumerTypes;
    private static bool _suppressConsumerLookupTrace;

    internal static void ResetHooks()
    {
        _hooksInstalled = false;
        DariusDisplayIds.Clear();
        RuntimeConsumerStackKeys.Clear();
        _lobbyConsumerTypes = null;
        _suppressConsumerLookupTrace = false;
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
            patched += PatchOptional(harmony, displaySetup, nameof(CharacterModelDisplaySetupPrefix), nameof(CharacterModelDisplaySetupPostfix),
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

    internal static void LogPipelineCheckpoint(string correlation, string stage, string detail)
    {
        try
        {
            Scene scene = SceneManager.GetActiveScene();
            DariusLog.Info("PIPELINE",
                "corr=" + (string.IsNullOrEmpty(correlation) ? "<none>" : correlation) +
                " stage=" + (string.IsNullOrEmpty(stage) ? "<unknown>" : stage) +
                " generation=" + DariusPrototypeMod.ActiveGenerationId +
                " owner=" + DariusPrototypeMod.ActiveModInstanceId +
                " frame=" + Time.frameCount +
                " scene=" + (scene.IsValid() ? scene.name + "#" + scene.handle : "<invalid>") +
                (string.IsNullOrEmpty(detail) ? string.Empty : " " + detail));
        }
        catch (Exception e)
        {
            DariusLog.Exception("PIPELINE", e, "Pipeline checkpoint failed stage=" + (stage ?? "<unknown>"));
        }
    }

    internal static void LogRuntimeConsumerLookup(string key, UnityEngine.Object result)
    {
        if (_suppressConsumerLookupTrace) return;
        if (string.IsNullOrEmpty(key) || !DariusTravelerRegistry.IsRuntimeKey(key)) return;

        string sceneName = string.Empty;
        try { sceneName = SceneManager.GetActiveScene().name ?? string.Empty; } catch { }
        if (sceneName.IndexOf("Lobby", StringComparison.OrdinalIgnoreCase) < 0 &&
            sceneName.IndexOf("Title", StringComparison.OrdinalIgnoreCase) < 0)
            return;

        string throttleKey = sceneName + ":" + key;
        bool captureCaller = RuntimeConsumerStackKeys.Add(throttleKey);
        string caller = captureCaller ? CompactExternalStack() : "<captured-earlier>";
        DariusLog.DebugInfoThrottled(
            "PIPELINE-CONSUMER",
            throttleKey,
            "corr=g" + DariusPrototypeMod.ActiveGenerationId + "-o" + DariusPrototypeMod.ActiveModInstanceId +
            " stage=runtime-load key=" + key +
            " result=" + DescribeUnityObject(result) +
            " caller=" + caller,
            0.75);
    }

    private static string CompactExternalStack()
    {
        try
        {
            string[] lines = Environment.StackTrace.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            List<string> kept = new List<string>();
            for (int i = 0; i < lines.Length && kept.Count < 6; i++)
            {
                string line = lines[i].Trim();
                if (line.Length == 0) continue;
                if (line.IndexOf("DariusRuntimeAudit", StringComparison.Ordinal) >= 0 ||
                    line.IndexOf("DariusRuntimeResourceCompatibility", StringComparison.Ordinal) >= 0 ||
                    line.IndexOf("System.Environment", StringComparison.Ordinal) >= 0)
                    continue;
                kept.Add(line);
            }
            return string.Join(" <- ", kept.ToArray());
        }
        catch { return "<stack-unavailable>"; }
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
            Scene activeScene = SceneManager.GetActiveScene();
            if (activeScene.IsValid() &&
                activeScene.name.IndexOf("Lobby", StringComparison.OrdinalIgnoreCase) >= 0)
                LogLobbyConsumerState(token);

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
            int serializedDariusIndex = content._availableHeroes != null
                ? Array.IndexOf(content._availableHeroes, DariusTravelerRegistry.HeroName)
                : -1;
            int runtimeDariusIndex = content.availableHeroes != null
                ? content.availableHeroes.IndexOf(DariusTravelerRegistry.HeroName)
                : -1;

            sb.Append(" serializedHeroes=").Append(content._availableHeroes != null ? content._availableHeroes.Length : -1)
              .Append(" runtimeHeroes=").Append(content.availableHeroes != null ? content.availableHeroes.Count : -1)
              .Append(" dariusHero(serialized/runtime)=")
              .Append(Contains(content._availableHeroes, DariusTravelerRegistry.HeroName)).Append("/")
              .Append(content.availableHeroes != null && content.availableHeroes.Contains(DariusTravelerRegistry.HeroName))
              .Append(" dariusIndex(serialized/runtime)=").Append(serializedDariusIndex).Append("/").Append(runtimeDariusIndex)
              .Append(" heroNames(serialized)=").Append(JoinNames(content._availableHeroes))
              .Append(" heroNames(runtime)=").Append(JoinNames(content.availableHeroes))
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

    internal static void LogPublicLookupCheckpoint(string correlation, string stage)
    {
        LogPipelineCheckpoint(
            correlation,
            stage,
            "lookup={" + BuildLookupState() + "}");
    }

    private static string BuildLookupState()
    {
        object database = DewResources.database;
        if (database == null) return "resourceDB=<null>";
        if (!DariusRuntimeResourceCompatibility.IsInstalled)
            return "bridgeInstalled=false; active lookup probes skipped to avoid Addressables side effects";

        bool heroMap = DariusUnsupportedResourceBridge.IsTypedResourceIdentityMapped(
            database, typeof(Hero_Darius), DariusTravelerRegistry.HeroName, DariusTravelerRegistry.HeroGuid);
        bool skinMap = DariusUnsupportedResourceBridge.IsNamedResourceIdentityMapped(
            database, DariusTravelerRegistry.DefaultSkinName, DariusTravelerRegistry.SkinGuid);
        bool attackMap = DariusUnsupportedResourceBridge.IsTypedResourceIdentityMapped(
            database, typeof(At_DariusAxe), DariusTravelerRegistry.AttackName, DariusTravelerRegistry.AttackGuid);

        if (!heroMap || !skinMap || !attackMap ||
            DariusTravelerRegistry.HeroPrefab == null ||
            DariusTravelerRegistry.DefaultSkin == null ||
            DariusTravelerRegistry.AttackPrefab == null)
        {
            return "directMaps(hero/skin/attack)=" + heroMap + "/" + skinMap + "/" + attackMap +
                   " templates(hero/skin/attack)=" +
                   (DariusTravelerRegistry.HeroPrefab != null) + "/" +
                   (DariusTravelerRegistry.DefaultSkin != null) + "/" +
                   (DariusTravelerRegistry.AttackPrefab != null) +
                   "; active public lookup probes skipped because prerequisite shape is incomplete";
        }

        StringBuilder sb = new StringBuilder();
        _suppressConsumerLookupTrace = true;
        try
        {
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
        finally
        {
            _suppressConsumerLookupTrace = false;
        }
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

    private static void LogLobbyConsumerState(string reason)
    {
        try
        {
            Type[] candidates = GetLobbyConsumerTypes();
            int loggedObjects = 0;
            List<string> typeSummary = new List<string>();

            for (int ti = 0; ti < candidates.Length && loggedObjects < 24; ti++)
            {
                Type type = candidates[ti];
                UnityEngine.Object[] objects = null;
                try { objects = Resources.FindObjectsOfTypeAll(type); } catch { }
                int count = objects != null ? objects.Length : 0;
                if (count == 0) continue;

                typeSummary.Add(type.FullName + "=" + count);
                for (int oi = 0; oi < count && loggedObjects < 24; oi++)
                {
                    UnityEngine.Object obj = objects[oi];
                    Component component = obj as Component;
                    if (component == null) continue;

                    bool loadedSceneObject = false;
                    try
                    {
                        Scene scene = component.gameObject.scene;
                        loadedSceneObject = scene.IsValid() && scene.isLoaded;
                    }
                    catch { }
                    if (!loadedSceneObject) continue;

                    string members = DescribeInterestingFields(component);
                    if (string.IsNullOrEmpty(members) &&
                        type.Name.IndexOf("Hero", StringComparison.OrdinalIgnoreCase) < 0 &&
                        type.Name.IndexOf("Character", StringComparison.OrdinalIgnoreCase) < 0)
                        continue;

                    DariusLog.Info(
                        "PIPELINE-LOBBY",
                        "reason=" + reason +
                        " stage=lobby-consumer-state type=" + type.FullName +
                        " object=" + DescribeUnityObject(component) +
                        " members={" + (string.IsNullOrEmpty(members) ? "<none>" : members) + "}");
                    loggedObjects++;
                }
            }

            DariusLog.Info(
                "PIPELINE-LOBBY",
                "reason=" + reason +
                " stage=lobby-consumer-types candidates=" + candidates.Length +
                " objectsLogged=" + loggedObjects +
                " types=[" + string.Join(";", typeSummary.ToArray()) + "]");
        }
        catch (Exception e)
        {
            DariusLog.Exception("PIPELINE-LOBBY", e, "Lobby consumer-state inspection failed reason=" + reason);
        }
    }

    private static Type[] GetLobbyConsumerTypes()
    {
        if (_lobbyConsumerTypes != null) return _lobbyConsumerTypes;

        List<Type> result = new List<Type>();
        Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
        for (int ai = 0; ai < assemblies.Length; ai++)
        {
            Type[] types = null;
            try { types = assemblies[ai].GetTypes(); }
            catch (ReflectionTypeLoadException e) { types = e.Types; }
            catch { }
            if (types == null) continue;

            for (int ti = 0; ti < types.Length && result.Count < 64; ti++)
            {
                Type type = types[ti];
                if (type == null || !typeof(Component).IsAssignableFrom(type)) continue;
                string fullName = type.FullName ?? type.Name;
                if (fullName.IndexOf("Lobby", StringComparison.OrdinalIgnoreCase) < 0) continue;

                bool relevant =
                    fullName.IndexOf("Hero", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    fullName.IndexOf("Character", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    HasInterestingHeroField(type);
                if (relevant) result.Add(type);
            }
        }

        _lobbyConsumerTypes = result.ToArray();
        DariusLog.Info("PIPELINE-LOBBY",
            "Discovered read-only Lobby consumer types count=" + _lobbyConsumerTypes.Length +
            " types=[" + string.Join(";", Array.ConvertAll(_lobbyConsumerTypes, t => t.FullName ?? t.Name)) + "]");
        return _lobbyConsumerTypes;
    }

    private static bool HasInterestingHeroField(Type type)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        try
        {
            FieldInfo[] fields = type.GetFields(flags);
            for (int i = 0; i < fields.Length; i++)
                if (InterestingName(fields[i].Name)) return true;
        }
        catch { }
        return false;
    }

    private static string DescribeInterestingFields(object target)
    {
        if (target == null) return "<null>";
        List<string> values = new List<string>();
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        for (Type type = target.GetType(); type != null && values.Count < 24; type = type.BaseType)
        {
            FieldInfo[] fields = null;
            try { fields = type.GetFields(flags); } catch { }
            if (fields == null) continue;

            for (int i = 0; i < fields.Length && values.Count < 24; i++)
            {
                FieldInfo field = fields[i];
                if (field == null || !InterestingName(field.Name)) continue;
                try { values.Add(type.Name + "." + field.Name + "=" + SummarizeValue(field.GetValue(target))); } catch { }
            }
        }
        return string.Join(",", values.ToArray());
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

    private static void CharacterModelDisplaySetupPrefix(object __instance, string __0)
    {
        if (__instance == null || string.IsNullOrEmpty(__0) ||
            !DariusTravelerRegistry.IsRuntimeSkinKey(__0))
            return;

        try
        {
            Component component = __instance as Component;
            Skin registrySkin;
            bool registryHasSkin = DariusTravelerRegistry.TryResolveRuntimeSkin(__0, out registrySkin);
            bool dewMap = false;
            if (string.Equals(__0, DariusTravelerRegistry.DefaultSkinName, StringComparison.Ordinal))
            {
                dewMap = DariusUnsupportedResourceBridge.IsNamedResourceIdentityMapped(
                    DewResources.database,
                    DariusTravelerRegistry.DefaultSkinName,
                    DariusTravelerRegistry.SkinGuid);
            }

            LogPipelineCheckpoint(
                "g" + DariusPrototypeMod.ActiveGenerationId + "-o" + DariusPrototypeMod.ActiveModInstanceId,
                "lobby-display-input",
                "skin=" + __0 +
                " display=" + DescribeUnityObject(component) +
                " registryHasSkin=" + registryHasSkin +
                " registrySkin=" + DescribeUnityObject(registrySkin) +
                " dewNamedMap=" + dewMap +
                " core={" + DariusTravelerRegistry.PipelineCoreState() + "}" +
                " caller=" + CompactExternalStack());
        }
        catch (Exception e)
        {
            DariusLog.Exception("PIPELINE", e, "CharacterModelDisplay.Setup input checkpoint failed skin=" + __0);
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

            LogPipelineCheckpoint(
                "g" + DariusPrototypeMod.ActiveGenerationId + "-o" + DariusPrototypeMod.ActiveModInstanceId,
                "lobby-display-output",
                "skin=" + __0 +
                " display=" + DescribeUnityObject(component) +
                " descendants=" + Math.Max(0, all.Length - 1) +
                " worldPos=" + DariusLog.Vec(root.position) +
                " localScale=" + DariusLog.Vec(root.localScale));

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

        IList list = value as IList;
        if (list != null)
        {
            List<string> sample = new List<string>();
            int limit = Math.Min(list.Count, 16);
            for (int i = 0; i < limit; i++)
            {
                object item = null;
                try { item = list[i]; } catch { }
                if (item == null) { sample.Add("<null>"); continue; }

                string itemText = item as string;
                if (itemText != null) { sample.Add(itemText); continue; }

                Type itemType = item as Type;
                if (itemType != null) { sample.Add(itemType.Name); continue; }

                UnityEngine.Object itemObject = item as UnityEngine.Object;
                if (!ReferenceEquals(itemObject, null))
                {
                    sample.Add(SafeName(itemObject));
                    continue;
                }

                sample.Add(item.GetType().Name);
            }
            if (list.Count > limit) sample.Add("...");
            return value.GetType().Name + "(count=" + list.Count + ",sample=[" +
                   string.Join(",", sample.ToArray()) + "])";
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

    private static string JoinNames(IEnumerable<string> values)
    {
        if (values == null) return "<null>";
        try
        {
            List<string> names = new List<string>();
            foreach (string value in values)
            {
                if (names.Count >= 32) { names.Add("..."); break; }
                names.Add(value ?? "<null>");
            }
            return "[" + string.Join(",", names.ToArray()) + "]";
        }
        catch { return "<error>"; }
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
          .Append(" ownerGeneration=").Append(_modOwnerGenerationId)
          .Append(" ownerId=").Append(_modOwnerInstanceId)
          .Append(" registeredModRoot=").Append(_registeredModRoot ?? "<null>")
          .Append(" currentModRoot=").Append(DariusModEnvironment.Root ?? "<null>")
          .Append(" activeGeneration=").Append(DariusPrototypeMod.ActiveGenerationId)
          .Append(" activeOwner=").Append(DariusPrototypeMod.ActiveModInstanceId)
          .Append(" barrier=").Append(DariusPrototypeMod.IsBootstrapBlockedThisFrame)
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
          .Append(" mirrorHero={").Append(DariusMirrorRuntimeHealth.Describe(HeroAssetId)).Append("}")
          .Append(" mirrorAttack={").Append(DariusMirrorRuntimeHealth.Describe(AttackAssetId)).Append("}")
          .Append(" mirrorAttackAi={").Append(DariusMirrorRuntimeHealth.Describe(AttackInstanceAssetId)).Append("}")
          .Append(" mirrorCritAi={").Append(DariusMirrorRuntimeHealth.Describe(AttackCritInstanceAssetId)).Append("}")
          .Append(" ownedObjects=").Append(OwnedObjects.Count)
          .Append(" contentOwner=").Append(_contentOwner != null ? RuntimeHelpers.GetHashCode(_contentOwner) : 0)
          .Append(" registeredDb=").Append(_registeredDatabase != null ? RuntimeHelpers.GetHashCode(_registeredDatabase) : 0)
          .Append(" currentDb=").Append(DewResources.database != null ? RuntimeHelpers.GetHashCode(DewResources.database) : 0);

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

    internal static string PipelineCoreState()
    {
        object database = DewResources.database;
        DewGameContentSettings content = null;
        try { content = DewBuildProfile.current != null ? DewBuildProfile.current.content : null; } catch { }

        bool contentSerialized = content != null && content._availableHeroes != null &&
                                 Array.IndexOf(content._availableHeroes, HeroName) >= 0;
        bool contentRuntime = content != null && content.availableHeroes != null &&
                              content.availableHeroes.Contains(HeroName);

        bool heroType = false;
        try
        {
            IReadOnlyList<Type> heroTypes = Dew.allHeroes;
            for (int i = 0; i < heroTypes.Count; i++)
            {
                if (heroTypes[i] == typeof(Hero_Darius))
                {
                    heroType = true;
                    break;
                }
            }
        }
        catch { }

        return "registered=" + _registered +
               " registering=" + _registering +
               " db=" + (database != null ? RuntimeHelpers.GetHashCode(database).ToString() : "<null>") +
               " dbMatchesRegistered=" + (_registeredDatabase != null && ReferenceEquals(database, _registeredDatabase)) +
               " rootMatchesRegistered=" + (!string.IsNullOrEmpty(_registeredModRoot) &&
                   string.Equals(_registeredModRoot, DariusModEnvironment.Root, StringComparison.OrdinalIgnoreCase)) +
               " hero=" + (HeroPrefab != null ? HeroPrefab.GetInstanceID().ToString() : "<null>") +
               " skin=" + (DefaultSkin != null ? DefaultSkin.GetInstanceID().ToString() : "<null>") +
               " attack=" + (AttackPrefab != null ? AttackPrefab.GetInstanceID().ToString() : "<null>") +
               " resGuid=" + ResourcesByGuid.Count +
               " heroMap=" + DariusUnsupportedResourceBridge.IsTypedResourceIdentityMapped(database, typeof(Hero_Darius), HeroName, HeroGuid) +
               " skinMap=" + DariusUnsupportedResourceBridge.IsNamedResourceIdentityMapped(database, DefaultSkinName, SkinGuid) +
               " attackMap=" + DariusUnsupportedResourceBridge.IsTypedResourceIdentityMapped(database, typeof(At_DariusAxe), AttackName, AttackGuid) +
               " netMap=" + DariusUnsupportedResourceBridge.IsNetworkGuidMapped(database, HeroAssetId, HeroGuid) + "/" +
                              DariusUnsupportedResourceBridge.IsNetworkGuidMapped(database, AttackAssetId, AttackGuid) + "/" +
                              DariusUnsupportedResourceBridge.IsNetworkGuidMapped(database, AttackInstanceAssetId, AttackInstanceGuid) + "/" +
                              DariusUnsupportedResourceBridge.IsNetworkGuidMapped(database, AttackCritInstanceAssetId, AttackCritInstanceGuid) +
               " mirror=" + DariusMirrorRuntimeHealth.IsHandlerPairHealthy(HeroAssetId) + "/" +
                             DariusMirrorRuntimeHealth.IsHandlerPairHealthy(AttackAssetId) + "/" +
                             DariusMirrorRuntimeHealth.IsHandlerPairHealthy(AttackInstanceAssetId) + "/" +
                             DariusMirrorRuntimeHealth.IsHandlerPairHealthy(AttackCritInstanceAssetId) +
               " heroType=" + heroType +
               " contentHero=" + contentSerialized + "/" + contentRuntime;
    }

    internal static string PipelineProfileState()
    {
        DewProfile profile = null;
        DewProfileStats stats = null;
        DewGameContentSettings content = null;
        try { profile = DewSave.profileMain; } catch { }
        try { stats = DewSave.profileStats; } catch { }
        try { content = DewBuildProfile.current != null ? DewBuildProfile.current.content : null; } catch { }

        string selectedSkin = null;
        int loadoutPages = -1;
        bool heroEntry = false;
        if (profile != null)
        {
            heroEntry = profile.heroes != null && profile.heroes.ContainsKey(HeroName);
            if (profile.heroSelectedSkins != null)
                profile.heroSelectedSkins.TryGetValue(HeroName, out selectedSkin);
            if (profile.heroLoadouts != null)
            {
                List<HeroLoadoutData> pages;
                if (profile.heroLoadouts.TryGetValue(HeroName, out pages) && pages != null)
                    loadoutPages = pages.Count;
            }
        }

        return "profile=" + (profile != null ? RuntimeHelpers.GetHashCode(profile).ToString() : "<null>") +
               " stats=" + (stats != null ? RuntimeHelpers.GetHashCode(stats).ToString() : "<null>") +
               " heroEntry=" + heroEntry +
               " selectedSkin=" + (selectedSkin ?? "<null>") +
               " loadoutPages=" + loadoutPages +
               " statsHero=" + (stats != null && stats.heroes != null && stats.heroes.ContainsKey(HeroName)) +
               " contentHero=" + (content != null && content.availableHeroes != null && content.availableHeroes.Contains(HeroName));
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
        try { healthy = IsRegistrationHealthyForBootstrap(); }
        catch (Exception e) { healthError = e.GetType().Name + ":" + e.Message; }

        StringBuilder sb = new StringBuilder();
        sb.Append("registered=").Append(_registered)
          .Append(" healthy=").Append(healthy)
          .Append(" ownerGeneration=").Append(_modOwnerGenerationId)
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
                bool mirrorOk = DariusMirrorRuntimeHealth.IsHandlerPairHealthy(record.assetId);
                if (!resourceOk || !typedOk || !record.networkHandlerRegistered || !prefabOk || !netOk || !mirrorOk)
                    bad.Add(record.name + "(res=" + resourceOk + ",typed=" + typedOk + ",handler=" +
                        record.networkHandlerRegistered + ",prefab=" + prefabOk + ",net=" + netOk +
                        ",mirror=" + mirrorOk + ")");
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
               ",bundlePath=" + (_bundlePath ?? "<null>") +
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
               ",root=" + (_root ?? "<null>") +
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
