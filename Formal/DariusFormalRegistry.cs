using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;

// Registers the formal Darius Memories / Essence as real runtime resources.
// The first playable milestone deliberately keeps them excluded from the random pool and
// drops one test set beside the local host player. This avoids touching permanent save data.
public static class DariusFormalRegistry
{
    private static readonly Dictionary<string, UnityEngine.Object> ResourcesByGuid = new Dictionary<string, UnityEngine.Object>();
    private static readonly Dictionary<UnityEngine.Object, string> GuidByObject = new Dictionary<UnityEngine.Object, string>();
    private static readonly Dictionary<uint, GameObject> NetworkPrefabs = new Dictionary<uint, GameObject>();
    private static readonly List<GameObject> OwnedPrefabs = new List<GameObject>();
    private static GameObject _runtimeActorRoot;

    private static bool _registered;
    private static int _lastDroppedHeroInstanceId;

    public static St_Darius_Decimate Decimate { get; private set; }
    public static St_Darius_CripplingStrike CripplingStrike { get; private set; }
    public static St_Darius_Apprehend Apprehend { get; private set; }
    public static St_Darius_NoxianGuillotine NoxianGuillotine { get; private set; }
    public static St_Darius_Flash Flash { get; private set; }
    public static St_Darius_Ghost Ghost { get; private set; }
    public static St_D_Darius_Hemorrhage Hemorrhage { get; private set; }
    public static Gem_Darius_Hemorrhage LegacyHemorrhage { get; private set; }
    public static Ai_Darius_Decimate DecimateAbility { get; private set; }
    public static Ai_Darius_CripplingStrike CripplingStrikeAbility { get; private set; }
    public static Ai_Darius_Apprehend ApprehendAbility { get; private set; }
    public static Ai_Darius_NoxianGuillotine NoxianGuillotineAbility { get; private set; }

    // Stable GUIDs: keeping them deterministic is important for resource/network identity.
    private const string GuidQ = "b67dbf56-5817-424d-8c46-16129def0bf2";
    private const string GuidW = "0a1641d1-1cbe-4a99-9d95-7b315b51420c";
    private const string GuidE = "a016b459-fe0f-4571-a28c-d3c49a29d14f";
    private const string GuidR = "fbb73980-8b6f-4623-88b0-946aaa201e73";
    private const string GuidFlash = "f6100a54-c4b9-4138-9dc4-44bcf9048a71";
    private const string GuidGhost = "02b48d18-3f96-48e0-8296-e02e2395888e";
    private const string GuidIdentity = "acdd533c-6e5d-4974-9895-e90a103b5307";
    private const string GuidGem = "44e808d0-ad21-458d-8832-de7eabe3c5ac"; // legacy v0.11 Essence only
    private const string GuidAiQ = "6ba4cdd8-7e47-46d1-8f18-76f25e375aa2";
    private const string GuidAiW = "8bfb1788-d4e0-4765-801e-af08c028791c";
    private const string GuidAiE = "c006572b-f1bb-41c3-bdfa-90a51f402485";
    private const string GuidAiR = "6a3da3e5-c434-4789-ae28-547c45dd7912";

    // Only W/E are ordinary run Memories. Q/R are Hero_Darius character skills and
    // Hemorrhage is the Identity slot; those three must not enter the random Memory pool.
    public static readonly KeyValuePair<string, Rarity>[] SkillPoolEntries =
    {
        new KeyValuePair<string, Rarity>("St_Darius_CripplingStrike", Rarity.Common),
        new KeyValuePair<string, Rarity>("St_Darius_Apprehend", Rarity.Common)
    };

    public static readonly KeyValuePair<string, Rarity>[] GemPoolEntries = new KeyValuePair<string, Rarity>[0];

    public static IEnumerator InitializeAndDropWhenReady()
    {
        DariusLog.Info("REG", "InitializeAndDropWhenReady started; waiting for DewResources.database.");
        while (DewResources.database == null)
            yield return null;

        DariusLog.Info("REG", "DewResources.database ready; registering formal resources.");
        Register();

        // Primary test path from v0.9.6 onward: native Deja Vu start selection.
        DariusDejaVuRegistry.RegisterAll();
        DariusLog.Info("DEJAVU", "Darius memory resources registered in the native Deja Vu/content pipeline.");
        yield break;
    }

    public static void Register()
    {
        if (_registered) return;
        if (DewResources.database == null)
        {
            DariusLog.Warn("REG", "DewResources database is not ready; formal resources were not registered yet.");
            return;
        }

        // Register clean, warm-initialized AbilityInstance prefabs first. The engineering audit confirmed
        // that Gem/Essence compatibility depends on the stock SkillTrigger -> AbilityInstance ->
        // PrepareAndSpawn chain. These templates live activeSelf=true under an inactive resource root,
        // so cloned Actors have already run Awake before Dew assigns parentActor, without ever becoming
        // live scene actors themselves.
        DecimateAbility = RegisterPrefab<Ai_Darius_Decimate>("Ai_Darius_Decimate", GuidAiQ, null);
        CripplingStrikeAbility = RegisterPrefab<Ai_Darius_CripplingStrike>("Ai_Darius_CripplingStrike", GuidAiW, null);
        ApprehendAbility = RegisterPrefab<Ai_Darius_Apprehend>("Ai_Darius_Apprehend", GuidAiE, null);
        NoxianGuillotineAbility = RegisterPrefab<Ai_Darius_NoxianGuillotine>("Ai_Darius_NoxianGuillotine", GuidAiR, null);

        Decimate = RegisterPrefab<St_Darius_Decimate>("St_Darius_Decimate", GuidQ, skill =>
        {
            ConfigureSkillPrefab(skill, Rarity.Common, 6.0f, "Q", "Q", DecimateAbility, cfg =>
            {
                cfg.alwaysCastImmediately = true;
                cfg.castMethod.type = CastMethodType.None;
                cfg.castMethod._radius = 4.25f;
            });
        });
        CripplingStrike = RegisterPrefab<St_Darius_CripplingStrike>("St_Darius_CripplingStrike", GuidW, skill =>
        {
            ConfigureSkillPrefab(skill, Rarity.Common, 5.0f, "W", "W", CripplingStrikeAbility, cfg =>
            {
                cfg.alwaysCastImmediately = true;
                cfg.castMethod.type = CastMethodType.None;
                cfg.castMethod._radius = 0f;
            });
        });
        Apprehend = RegisterPrefab<St_Darius_Apprehend>("St_Darius_Apprehend", GuidE, skill =>
        {
            ConfigureSkillPrefab(skill, Rarity.Common, 10.0f, "E", "E", ApprehendAbility, cfg =>
            {
                cfg.alwaysCastImmediately = true;
                cfg.faceForward = true;
                cfg.castMethod.type = CastMethodType.Cone;
                cfg.castMethod._radius = 5.35f;
                cfg.castMethod._angle = 60.0f;
            });
        });
        NoxianGuillotine = RegisterPrefab<St_Darius_NoxianGuillotine>("St_Darius_NoxianGuillotine", GuidR, skill =>
        {
            ConfigureSkillPrefab(skill, Rarity.Rare, 24.0f, "R", "R", NoxianGuillotineAbility, cfg =>
            {
                cfg.faceForward = true;
                // Point input + Darius-side target resolver avoids intermittent native Target-mode
                // input rejection before St_Darius_NoxianGuillotine.OnCastComplete is reached.
                cfg.castMethod.type = CastMethodType.Point;
                cfg.castMethod._range = St_Darius_NoxianGuillotine.CastRange;
                cfg.castMethod._radius = St_Darius_NoxianGuillotine.AimPointAssistRadius;
                cfg.castMethod._isClamping = true;
                cfg.alwaysCastImmediately = false;
                cfg.targetValidator = cfg.targetValidator ?? new AbilityTargetValidator();
                cfg.targetValidator.targets = EntityRelation.Enemy;
            });
            skill.type = SkillType.Ultimate;
        });

        // The new summoner-spell movement slot inherits the live Vesper dodge economy.
        // St_M_Charge is the stock Vesper movement trigger in the current game build; if a future
        // build renames it we still register using conservative fallbacks and log the mismatch.
        SkillTrigger nativeMovement = TryResolveNativeSkill("St_M_Charge", "SUMMONER-CONFIG");
        DariusSummonerBalance.InitializeFromNativeMovement(nativeMovement);
        Flash = RegisterPrefab<St_Darius_Flash>("St_Darius_Flash", GuidFlash, skill =>
        {
            ConfigureSummonerSkillPrefab(skill, "FLASH", nativeMovement);
        });
        Ghost = RegisterPrefab<St_Darius_Ghost>("St_Darius_Ghost", GuidGhost, skill =>
        {
            ConfigureSummonerSkillPrefab(skill, "GHOST", nativeMovement);
        });

        Hemorrhage = RegisterPrefab<St_D_Darius_Hemorrhage>("St_D_Darius_Hemorrhage", GuidIdentity, identity =>
        {
            ConfigureIdentityPrefab(identity);
        });

        // Keep the old v0.11 Gem resource loadable for hot-reload/save compatibility, but never
        // put it into normal pools or Deja Vu again. New gameplay uses the Hero_Darius Identity Memory.
        LegacyHemorrhage = RegisterPrefab<Gem_Darius_Hemorrhage>("Gem_Darius_Hemorrhage", GuidGem, gem =>
        {
            gem.rarity = Rarity.Rare;
            gem.excludeFromPool = true;
            gem.isCooldownEnabled = false;
            gem.enableStatBonus = false;
            gem.icon = DariusPrototypeIcons.Get("H");
        });

        // Register Hero_Darius constellation StarEffect resources through the same runtime database/network path.
        // This keeps stars native to the game's constellation/profile system instead of maintaining a parallel save.
        DariusConstellationRegistry.Register();

        // Runtime maps are updated precisely by RegisterObject; do not rebuild the global database here.

        _registered = true;
        DariusLog.Info("REG", "Formal Darius resources registered: Q/W/E/R + Flash/Ghost + Hemorrhage + 38 native constellation/equipment stars + legacy Essence + 4 AbilityInstances.");
    }

    public static void DropTestPack(DewPlayer player, bool force)
    {
        if (!_registered) Register();
        if (!_registered || player == null || player.hero == null) return;

        if (!NetworkServer.active)
        {
            DariusLog.Warn("DROP", "Test pack can only be spawned by the host/server.");
            return;
        }

        int heroId = player.hero.GetInstanceID();
        if (!force && _lastDroppedHeroInstanceId == heroId) return;
        _lastDroppedHeroInstanceId = heroId;

        Vector3 center = player.hero.agentPosition;
        try
        {
            Vector3 qPos = GoodDropPosition(center, 2.0f);
            Vector3 wPos = GoodDropPosition(center, 2.2f);
            Vector3 ePos = GoodDropPosition(center, 2.4f);
            Vector3 rPos = GoodDropPosition(center, 2.6f);
            Vector3 hPos = GoodDropPosition(center, 2.8f);
            DariusLog.Info("DROP", "Spawning test pack center=" + DariusLog.Vec(center) +
                " Q=" + DariusLog.Vec(qPos) + " W=" + DariusLog.Vec(wPos) + " E=" + DariusLog.Vec(ePos) +
                " R=" + DariusLog.Vec(rPos) + " H=" + DariusLog.Vec(hPos));
            LogSkillPrefabState("Q", Decimate);
            LogSkillPrefabState("W", CripplingStrike);
            LogSkillPrefabState("E", Apprehend);
            LogSkillPrefabState("R", NoxianGuillotine);

            TrySpawnSkill("Q", Decimate, qPos, player);
            TrySpawnSkill("W", CripplingStrike, wPos, player);
            TrySpawnSkill("E", Apprehend, ePos, player);
            TrySpawnSkill("R", NoxianGuillotine, rPos, player);
            TrySpawnSkill("H-IDENTITY", Hemorrhage, hPos, player);
            DariusLog.Info("DROP", "Internal memory spawn diagnostic completed.");
        }
        catch (Exception e)
        {
            DariusLog.Exception("DROP", e, "Failed to spawn formal test pack");
        }
    }

    private static void ConfigureSkillPrefab(SkillTrigger skill, Rarity rarity, float cooldown, string iconKey, string locationName, AbilityInstance spawnedInstance, Action<TriggerConfig> extra)
    {
        skill.rarity = rarity;
        skill.type = SkillType.Normal;
        skill.tags = DescriptionTags.None;
        skill.excludeFromPool = false;
        skill.isLevelUpEnabled = true;
        skill.useCustomSkillHastePerLevel = false;
        skill.startEffect = null;
        skill.endEffect = null;
        skill.characterSkillOwner = string.Empty;
        SetSkillLocation(skill, locationName);
        skill.configs = new TriggerConfig[]
        {
            DariusTriggerConfigRuntimeEditor.CreateBase(skill, cooldown),
            DariusTriggerConfigRuntimeEditor.CreateBase(skill, cooldown)
        };

        for (int i = 0; i < skill.configs.Length; i++)
        {
            TriggerConfig cfg = skill.configs[i];
            cfg.triggerIcon = DariusPrototypeIcons.Get(iconKey);
            cfg.spawnedInstance = spawnedInstance;
            cfg.isActive = true;
            cfg.canReceiveCooldownReduction = true;
            cfg.castMethod = cfg.castMethod ?? new CastMethodData();
            extra?.Invoke(cfg);
        }

        DariusTriggerConfigRuntimeEditor.AttachAll(skill);
        DariusLog.Info("CONFIG", "Prefab preconfigured " + skill.name + " configs=" + skill.configs.Length +
            " cooldown=" + cooldown + " icon=" + iconKey + " spawnedInstance=" +
            (spawnedInstance != null ? spawnedInstance.name : "<null>"));
    }

    private static void ConfigureSummonerSkillPrefab(SkillTrigger skill, string iconKey, SkillTrigger nativeMovement)
    {
        if (skill == null) return;
        skill.rarity = Rarity.Character;
        skill.type = SkillType.Normal;
        skill.tags = DescriptionTags.None;
        skill.excludeFromPool = true;
        skill.isLevelUpEnabled = false;
        skill.useCustomSkillHastePerLevel = false;
        skill.startEffect = null;
        skill.endEffect = null;
        skill.characterSkillOwner = "Hero_Darius";
        if (!CopySkillLocation(nativeMovement, skill, "SUMMONER-CONFIG")) SetSkillLocation(skill, "Movement");
        bool isFlash = string.Equals(iconKey, "FLASH", StringComparison.OrdinalIgnoreCase);
        skill.configs = new TriggerConfig[]
        {
            isFlash ? DariusSummonerBalance.CreateFlashConfig(skill) : DariusSummonerBalance.CreateGhostConfig(skill),
            isFlash ? DariusSummonerBalance.CreateFlashConfig(skill) : DariusSummonerBalance.CreateGhostConfig(skill)
        };
        for (int i = 0; i < skill.configs.Length; i++)
        {
            skill.configs[i].triggerIcon = DariusPrototypeIcons.Get(iconKey);
        }
        DariusTriggerConfigRuntimeEditor.AttachAll(skill);
        DariusLog.Info("SUMMONER-CONFIG", "Configured " + skill.name + " as Hero_Darius movement choice; source=" +
            (nativeMovement != null ? nativeMovement.name : "<fallback>") + " cooldown=" + DariusSummonerBalance.Cooldown.ToString("0.###") +
            " charges=" + DariusSummonerBalance.MaxCharges + " icon=" + iconKey);
    }

    private static bool CopySkillLocation(SkillTrigger source, SkillTrigger destination, string logTag)
    {
        if (source == null || destination == null) return false;
        try
        {
            FieldInfo field = typeof(SkillTrigger).GetField("<skillType>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance);
            if (field == null) return false;
            object value = field.GetValue(source);
            field.SetValue(destination, value);
            DariusLog.Info(logTag, "Copied skill location from " + source.name + " to " + destination.name + " value=" + value);
            return true;
        }
        catch (Exception e)
        {
            DariusLog.Exception(logTag, e, "Failed copying native movement skill location");
            return false;
        }
    }

    private static void ConfigureIdentityPrefab(St_D_Darius_Hemorrhage identity)
    {
        identity.rarity = Rarity.Rare;
        identity.type = SkillType.Normal;
        identity.tags = DescriptionTags.None;
        identity.excludeFromPool = false;
        identity.isLevelUpEnabled = true;
        identity.useCustomSkillHastePerLevel = false;
        identity.startEffect = null;
        identity.endEffect = null;
        identity.characterSkillOwner = "Hero_Darius";
        SetSkillLocation(identity, "Identity");
        identity.configs = new TriggerConfig[]
        {
            DariusTriggerConfigRuntimeEditor.CreateBase(identity, 9999f),
            DariusTriggerConfigRuntimeEditor.CreateBase(identity, 9999f)
        };
        for (int i = 0; i < identity.configs.Length; i++)
        {
            TriggerConfig cfg = identity.configs[i];
            cfg.triggerIcon = DariusPrototypeIcons.Get("H");
            cfg.spawnedInstance = null;
            cfg.isActive = false;
            cfg.canReceiveCooldownReduction = false;
            cfg.alwaysCastImmediately = false;
            cfg.castMethod = cfg.castMethod ?? new CastMethodData();
            cfg.castMethod.type = CastMethodType.None;
        }
        DariusTriggerConfigRuntimeEditor.AttachAll(identity);
        DariusLog.Info("IDENTITY-CONFIG", "Configured Hemorrhage as upgradeable Hero_Darius Identity Memory icon=" + (DariusPrototypeIcons.Get("H") != null));
    }

    private static SkillTrigger TryResolveNativeSkill(string typeName, string logTag)
    {
        Type nativeType = AccessTools.TypeByName(typeName);
        if (nativeType == null)
        {
            DariusLog.Warn(logTag, "Native skill type not found: " + typeName);
            return null;
        }
        MethodInfo[] methods = typeof(DewResources).GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
        for (int i = 0; i < methods.Length; i++)
        {
            MethodInfo m = methods[i];
            if (m.Name != "GetByType" || m.IsGenericMethod) continue;
            ParameterInfo[] ps = m.GetParameters();
            if (ps.Length < 1 || ps[0].ParameterType != typeof(Type)) continue;
            try
            {
                object[] args = new object[ps.Length];
                args[0] = nativeType;
                for (int j = 1; j < ps.Length; j++)
                {
                    if (ps[j].HasDefaultValue) args[j] = ps[j].DefaultValue;
                    else args[j] = ps[j].ParameterType.IsValueType ? Activator.CreateInstance(ps[j].ParameterType) : null;
                }
                object result = m.Invoke(null, args);
                SkillTrigger skill = result as SkillTrigger;
                if (skill != null) return skill;
                GameObject go = result as GameObject;
                if (go != null)
                {
                    skill = go.GetComponent<SkillTrigger>();
                    if (skill != null) return skill;
                }
                Component component = result as Component;
                if (component != null)
                {
                    skill = component.GetComponent<SkillTrigger>();
                    if (skill != null) return skill;
                }
            }
            catch (Exception e)
            {
                DariusLog.DebugInfo(logTag, "GetByType probe failed for " + typeName + " via " + m + ": " + e.GetType().Name);
            }
        }
        DariusLog.Warn(logTag, "Could not resolve native skill resource for " + typeName);
        return null;
    }

    private static void SetSkillLocation(SkillTrigger skill, string locationName)
    {
        if (skill == null || string.IsNullOrEmpty(locationName)) return;
        try
        {
            FieldInfo field = typeof(SkillTrigger).GetField("<skillType>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance);
            if (field == null)
            {
                DariusLog.Warn("CONFIG", "SkillTrigger skillType backing field not found for " + skill.name);
                return;
            }
            object value;
            if (field.FieldType.IsEnum)
            {
                string match = Enum.GetNames(field.FieldType).FirstOrDefault(n => string.Equals(n, locationName, StringComparison.OrdinalIgnoreCase));
                if (string.IsNullOrEmpty(match))
                {
                    DariusLog.Warn("CONFIG", "HeroSkillLocation value not found: " + locationName + " for " + skill.name);
                    return;
                }
                value = Enum.Parse(field.FieldType, match, true);
            }
            else
            {
                int numeric = string.Equals(locationName, "Q", StringComparison.OrdinalIgnoreCase) ? 0 :
                              string.Equals(locationName, "W", StringComparison.OrdinalIgnoreCase) ? 1 :
                              string.Equals(locationName, "E", StringComparison.OrdinalIgnoreCase) ? 2 :
                              string.Equals(locationName, "R", StringComparison.OrdinalIgnoreCase) ? 3 :
                              string.Equals(locationName, "Identity", StringComparison.OrdinalIgnoreCase) ? 4 :
                              string.Equals(locationName, "Movement", StringComparison.OrdinalIgnoreCase) ? 5 : 0;
                value = numeric;
            }
            field.SetValue(skill, value);
            DariusLog.Info("CONFIG", "Assigned " + skill.name + " to HeroSkillLocation." + locationName + " value=" + value);
        }
        catch (Exception e)
        {
            DariusLog.Exception("CONFIG", e, "Failed to set skill location " + locationName + " for " + skill.name);
        }
    }

    private static void LogSkillPrefabState(string label, SkillTrigger skill)
    {
        if (skill == null)
        {
            DariusLog.Error("DROP-CONFIG", label + " prefab=<null>");
            return;
        }

        string cfgState = skill.configs == null ? "<null>" : skill.configs.Length.ToString();
        string cfg0 = skill.configs != null && skill.configs.Length > 0 && skill.configs[0] != null ? "ok" : "null";
        string cfg1 = skill.configs != null && skill.configs.Length > 1 && skill.configs[1] != null ? "ok" : "null";
        DariusLog.Info("DROP-CONFIG", label + " prefab=" + skill.name +
            " configs=" + cfgState + " cfg0=" + cfg0 + " cfg1=" + cfg1 + " level=" + skill.level);
    }

    private static void TrySpawnSkill<T>(string label, T prefab, Vector3 pos, DewPlayer player) where T : SkillTrigger
    {
        try
        {
            Dew.CreateSkillTrigger<T>(prefab, pos, 1, player, null);
            DariusLog.Info("DROP-" + label, "CreateSkillTrigger call completed without exception pos=" + DariusLog.Vec(pos));
        }
        catch (Exception e)
        {
            DariusLog.Exception("DROP-" + label, e, "CreateSkillTrigger failed at " + DariusLog.Vec(pos));
        }
    }

    private static void TrySpawnGem(string label, Gem_Darius_Hemorrhage prefab, Vector3 pos, DewPlayer player)
    {
        try
        {
            Dew.CreateGem<Gem_Darius_Hemorrhage>(prefab, pos, 1, player, null);
            DariusLog.Info("DROP-" + label, "CreateGem call completed without exception pos=" + DariusLog.Vec(pos));
        }
        catch (Exception e)
        {
            DariusLog.Exception("DROP-" + label, e, "CreateGem failed at " + DariusLog.Vec(pos));
        }
    }

    private static Vector3 GoodDropPosition(Vector3 center, float radius)
    {
        try { return Dew.GetGoodRewardPosition(center, radius); }
        catch
        {
            Vector3 random = UnityEngine.Random.insideUnitSphere;
            random.y = 0f;
            if (random.sqrMagnitude < 0.001f) random = Vector3.forward;
            return center + random.normalized * radius;
        }
    }

    public static void Unregister()
    {
        DariusLog.Info("REG", "Unregister started. resources=" + ResourcesByGuid.Count + " networkPrefabs=" + NetworkPrefabs.Count);
        _registered = false;
        _lastDroppedHeroInstanceId = 0;
        DariusConstellationRegistry.RemoveInjectedTypes();

        var db = DewResources.database;
        if (db != null)
        {
            // Current (2026) runtime resource registration is keyed by the type's
            // assembly-qualified name. Remove only entries owned by this mod.
            foreach (var pair in GuidByObject.ToArray())
            {
                UnityEngine.Object obj = pair.Key;
                string guid = pair.Value;
                if (obj == null) continue;

                Type type = obj.GetType();
                string aqn = type.AssemblyQualifiedName;

                try
                {
                    string existing;
                    if (!string.IsNullOrEmpty(aqn) &&
                        db.typeAssemblyQualifiedNameToGuid.TryGetValue(aqn, out existing) &&
                        existing == guid)
                    {
                        db.typeAssemblyQualifiedNameToGuid.Remove(aqn);
                    }
                    if (db.allGuids.Contains(guid)) db.allGuids.Remove(guid);

                    // Remove the complete runtime index set that RegisterObject installs.
                    // This prevents stale Type objects from surviving DewMod hot reloads.
                    RemoveDatabaseMapIfOwned(db, "typeToGuid", type, guid);
                    RemoveDatabaseMapIfOwned(db, "guidToType", guid, type);
                    RemoveDatabaseMapIfOwned(db, "typeNameToGuid", type.Name, guid);
                    RemoveDatabaseMapIfOwned(db, "nameToGuid", obj.name, guid);
                    RemoveDatabaseMapIfOwned(db, "guidToName", guid, obj.name);
                    RemoveDatabaseMapIfOwned(db, "typeNameToType", type.Name, type);
                    RemoveDatabaseMapIfOwned(db, "typeNameToType", obj.name, type);
                }
                catch { }
            }

            foreach (uint id in NetworkPrefabs.Keys.ToArray())
            {
                try
                {
                    if (db.netObjectAssetIdToGuid.ContainsKey(id)) db.netObjectAssetIdToGuid.Remove(id);
                }
                catch { }
                try { NetworkClient.UnregisterSpawnHandler(id); } catch { }
            }

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
        Decimate = null;
        CripplingStrike = null;
        Apprehend = null;
        NoxianGuillotine = null;
        Flash = null;
        Ghost = null;
        Hemorrhage = null;
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
        UnityEngine.Object.DontDestroyOnLoad(go);

        // Mirror NetworkBehaviour caches its NetworkIdentity during component initialization.
        // Current working SoD runtime-prefab mods add NetworkIdentity FIRST, then SkillTrigger/Gem.
        // Adding it afterwards leaves netIdentity null and crashes NetworkIdentity.OnStartServer.
        NetworkIdentity preIdentity = go.AddComponent<NetworkIdentity>();
        uint preAssetId = StableAssetId(guid) | 0x80000000u;
        ConfigureNetworkIdentity(preIdentity, preAssetId, name);

        // Match the working reference mod's exact order: the NetworkIdentity already has
        // its runtime assetId before the NetworkBehaviour (SkillTrigger/Gem/Ai) is added.
        T component = go.AddComponent<T>();
        component.name = name;
        if (configure != null) configure(component);

        if (warmAbilityInstance)
        {
            // AbilityInstance/StarEffect templates must stay activeSelf=true. Dew clones the prefab
            // and assigns Actor.parentActor before it gets a chance to activate an inactive clone.
            // Keeping the child active under an inactive persistent root preserves Awake-initialized
            // Actor state while keeping the template itself out of gameplay. This is the same lifecycle
            // contract used by the stable Darius native attack prefabs.
            GameObject root = GetRuntimeActorRoot();
            go.transform.SetParent(root.transform, false);
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
        _runtimeActorRoot = new GameObject("DariusPrototype_FormalRuntimeActors");
        _runtimeActorRoot.hideFlags = HideFlags.HideAndDontSave;
        UnityEngine.Object.DontDestroyOnLoad(_runtimeActorRoot);
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
        if (database == null || key == null) return;
        try
        {
            FieldInfo field = database.GetType().GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            IDictionary map = field != null ? field.GetValue(database) as IDictionary : null;
            if (map == null) return;
            map[key] = value;
            DariusLog.DebugInfo("REG-MAP", fieldName + "[" + key + "]=" + value);
        }
        catch (Exception e)
        {
            DariusLog.Exception("REG-MAP", e, "Failed runtime DB map " + fieldName + " key=" + key);
        }
    }


    private static void RemoveDatabaseMapIfOwned(object database, string fieldName, object key, object expectedValue)
    {
        if (database == null || key == null) return;
        try
        {
            FieldInfo field = database.GetType().GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            IDictionary map = field != null ? field.GetValue(database) as IDictionary : null;
            if (map == null || !map.Contains(key)) return;
            object current = map[key];
            if (Equals(current, expectedValue)) map.Remove(key);
        }
        catch { }
    }

    private static void ConfigureNetworkIdentity(NetworkIdentity identity, uint assetId, string label)
    {
        if (identity == null) return;
        try
        {
            // Mirror 2026 uses _assetId internally; keep fallbacks for older builds.
            FieldInfo field = typeof(NetworkIdentity).GetField("_assetId", BindingFlags.Instance | BindingFlags.NonPublic)
                           ?? typeof(NetworkIdentity).GetField("assetId", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                           ?? typeof(NetworkIdentity).GetField("<assetId>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
            if (field != null)
            {
                field.SetValue(identity, assetId);
            }
            else
            {
                PropertyInfo prop = typeof(NetworkIdentity).GetProperty("assetId", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (prop != null && prop.CanWrite) prop.SetValue(identity, assetId);
                else DariusLog.Warn("NET", "Could not find writable assetId for " + label);
            }

            identity.sceneId = 0UL;
            FieldInfo sceneField = typeof(NetworkIdentity).GetField("_isSceneObject", BindingFlags.NonPublic | BindingFlags.Instance);
            if (sceneField != null) sceneField.SetValue(identity, false);

            // Runtime-created prefab templates execute NetworkIdentity.Awake once during construction.
            // Mirror serializes its private hasSpawned flag; if it stays true on the template, every
            // later Instantiate inherits true and destroys itself with "has already spawned". Keep
            // the template in prefab state so the clone's own Awake is the first real spawn.
            FieldInfo spawnedField = typeof(NetworkIdentity).GetField("hasSpawned", BindingFlags.NonPublic | BindingFlags.Instance);
            if (spawnedField != null) spawnedField.SetValue(identity, false);
        }
        catch (Exception e)
        {
            DariusLog.Exception("NET", e, "Could not configure network identity for " + label);
        }
    }

    private static void RegisterNetworkIdentity(UnityEngine.Object obj, string guid)
    {
        Component component = obj as Component;
        if (component == null) return;

        NetworkIdentity identity = component.GetComponent<NetworkIdentity>();
        if (identity == null)
        {
            try
            {
                identity = component.gameObject.AddComponent<NetworkIdentity>();
                DariusLog.DebugInfo("NET", "Added NetworkIdentity to " + obj.name);
            }
            catch (Exception e)
            {
                DariusLog.Exception("NET", e, "Could not add NetworkIdentity to " + obj.name);
                return;
            }
        }

        uint assetId = StableAssetId(guid) | 0x80000000u;
        ConfigureNetworkIdentity(identity, assetId, obj.name);

        try
        {
            DewResources.database.netObjectAssetIdToGuid[assetId] = guid;
            DariusLog.Info("NET", "Registered network mapping name=" + obj.name + " assetId=" + assetId + " guid=" + guid);
        }
        catch (Exception e)
        {
            DariusLog.Exception("NET", e, "Failed DB network mapping for " + obj.name);
        }

        NetworkPrefabs[assetId] = component.gameObject;

        try
        {
            NetworkClient.RegisterSpawnHandler(assetId, SpawnHandler, UnspawnHandler);
            DariusLog.Info("NET", "Registered spawn handler name=" + obj.name + " assetId=" + assetId);
        }
        catch (Exception e)
        {
            DariusLog.Exception("NET", e, "Could not register spawn handler for " + obj.name);
        }
    }

    private static GameObject SpawnHandler(SpawnMessage msg)
    {
        GameObject prefab;
        if (!NetworkPrefabs.TryGetValue(msg.assetId, out prefab) || prefab == null)
        {
            DariusLog.Error("NET-SPAWN", "Missing network prefab for assetId=" + msg.assetId + " pos=" + DariusLog.Vec(msg.position));
            return null;
        }

        GameObject spawned = null;
        try
        {
            spawned = UnityEngine.Object.Instantiate(prefab, msg.position, msg.rotation);
            if (spawned != null)
            {
                spawned.transform.localScale = msg.scale;
                spawned.name = prefab.name;
                if (!spawned.activeSelf) spawned.SetActive(true);
                NetworkIdentity identity = spawned.GetComponent<NetworkIdentity>();
                if (identity != null)
                {
                    identity.sceneId = 0UL;
                    FieldInfo sceneField = typeof(NetworkIdentity).GetField("_isSceneObject", BindingFlags.NonPublic | BindingFlags.Instance);
                    if (sceneField != null) sceneField.SetValue(identity, false);
                    MethodInfo init = typeof(NetworkIdentity).GetMethod("InitializeNetworkBehaviours", BindingFlags.NonPublic | BindingFlags.Instance);
                    if (init != null) init.Invoke(identity, null);
                }
            }
            DariusLog.Info("NET-SPAWN", "Spawn handler assetId=" + msg.assetId + " prefab=" + prefab.name +
                " result=" + (spawned != null) + " pos=" + DariusLog.Vec(msg.position));
            return spawned;
        }
        catch (Exception e)
        {
            DariusLog.Exception("NET-SPAWN", e, "Spawn handler failed assetId=" + msg.assetId + " prefab=" + prefab.name);
            return null;
        }
    }

    private static void UnspawnHandler(GameObject spawned)
    {
        if (spawned != null)
        {
            DariusLog.DebugInfo("NET-SPAWN", "Unspawn " + spawned.name + "#" + spawned.GetInstanceID());
            SpawnManager.Destroy(spawned);
        }
    }

    public static bool TryGetNetworkPrefab(uint assetId, out GameObject prefab)
    {
        return NetworkPrefabs.TryGetValue(assetId, out prefab) && prefab != null;
    }

    public static bool TryGetResourceByExactType(Type type, out UnityEngine.Object obj)
    {
        obj = null;
        if (type == null) return false;
        foreach (UnityEngine.Object candidate in ResourcesByGuid.Values)
        {
            if (candidate != null && candidate.GetType() == type)
            {
                obj = candidate;
                return true;
            }
        }
        return false;
    }

    public static bool IsDariusResourceKey(string key)
    {
        if (string.IsNullOrEmpty(key)) return false;
        if (ResourcesByGuid.ContainsKey(key)) return true;
        return key == "St_Darius_Decimate" || key == "St_Darius_CripplingStrike" ||
               key == "St_Darius_Apprehend" || key == "St_Darius_NoxianGuillotine" ||
               key == "St_Darius_Flash" || key == "St_Darius_Ghost" ||
               key == "St_D_Darius_Hemorrhage" || key == "Gem_Darius_Hemorrhage" || key == "Ai_Darius_Decimate" ||
               key == "Ai_Darius_CripplingStrike" || key == "Ai_Darius_Apprehend" ||
               key == "Ai_Darius_NoxianGuillotine" || key.StartsWith("Se_Star_Darius_", StringComparison.Ordinal);
    }

    public static bool IsDariusSkillKey(object value)
    {
        if (value == null) return false;
        string text = value as string;
        if (text != null) return IsDariusSkillName(text) || text == GuidQ || text == GuidW || text == GuidE || text == GuidR ||
                                 text == GuidFlash || text == GuidGhost || text == GuidIdentity;
        Type type = value as Type;
        if (type != null) return IsDariusSkillName(type.Name);
        UnityEngine.Object obj = value as UnityEngine.Object;
        return obj != null && (IsDariusSkillName(obj.name) || IsDariusSkillName(obj.GetType().Name));
    }

    public static bool IsDariusGemKey(object value)
    {
        if (value == null) return false;
        string text = value as string;
        if (text != null) return text == "Gem_Darius_Hemorrhage" || text == GuidGem;
        Type type = value as Type;
        if (type != null) return type == typeof(Gem_Darius_Hemorrhage);
        UnityEngine.Object obj = value as UnityEngine.Object;
        return obj != null && (obj.name == "Gem_Darius_Hemorrhage" || obj is Gem_Darius_Hemorrhage);
    }

    public static bool IsDariusRuntimeObject(UnityEngine.Object obj)
    {
        if (obj == null) return false;
        if (GuidByObject.ContainsKey(obj)) return true;
        return IsDariusSkillName(obj.name) || IsDariusSkillName(obj.GetType().Name) ||
               DariusConstellationLocalization.IsDariusStarKey(obj.name) ||
               DariusConstellationLocalization.IsDariusStarKey(obj.GetType().Name) ||
               obj.name == "St_D_Darius_Hemorrhage" || obj is St_D_Darius_Hemorrhage ||
               obj.name == "Gem_Darius_Hemorrhage" || obj is Gem_Darius_Hemorrhage ||
               obj is Ai_Darius_Decimate || obj is Ai_Darius_CripplingStrike ||
               obj is Ai_Darius_Apprehend || obj is Ai_Darius_NoxianGuillotine;
    }

    private static uint StableAssetId(string text)
    {
        unchecked
        {
            uint hash = 2166136261u;
            for (int i = 0; i < text.Length; i++)
            {
                hash ^= text[i];
                hash *= 16777619u;
            }
            return hash == 0u ? 1u : hash;
        }
    }
}
