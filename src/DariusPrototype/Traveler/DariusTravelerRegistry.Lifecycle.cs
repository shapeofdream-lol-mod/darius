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
    private const int CoreRegistrationRetryLimit = 6;
    private const int ProfileRegistrationRetryLimit = 8;

    private static GameObject _lifecycleBridgeObject;
    private static Transform _modOwner;
    private static int _modOwnerInstanceId;
    private static int _modOwnerGenerationId;
    private static object _registeredDatabase;
    private static string _registeredModRoot;

    public static void BindOwner(Transform owner, int generationId)
    {
        if (owner == null) throw new ArgumentNullException(nameof(owner));
        if (generationId <= 0) throw new ArgumentOutOfRangeException(nameof(generationId));

        int ownerId = owner.gameObject.GetInstanceID();
        if (_modOwnerInstanceId == ownerId && _modOwnerGenerationId == generationId)
        {
            _modOwner = owner;
            return;
        }

        if (_resourceRoot != null || _lifecycleBridgeObject != null || _registered)
            throw new InvalidOperationException("Traveler runtime generation must be cleaned before binding a new ModBehaviour owner.");

        _modOwner = owner;
        _modOwnerInstanceId = ownerId;
        _modOwnerGenerationId = generationId;
        DariusLog.Info("TRAVELER-LIFECYCLE", "Bound runtime resource ownership generation=" + generationId +
            " owner=" + ownerId + ".");
    }

    private static bool IsExpectedGenerationActive(int generationId, int ownerId)
    {
        return generationId > 0 &&
               ownerId != 0 &&
               _modOwnerGenerationId == generationId &&
               _modOwnerInstanceId == ownerId &&
               _modOwner != null &&
               DariusPrototypeMod.IsActiveGeneration(generationId, ownerId);
    }

    private static bool IsCoreRegistrationHealthy()
    {
        object database = DewResources.database;
        string currentRoot = DariusModEnvironment.Root;
        if (!_registered ||
            !IsExpectedGenerationActive(_modOwnerGenerationId, _modOwnerInstanceId) ||
            database == null ||
            !ReferenceEquals(database, _registeredDatabase) ||
            string.IsNullOrEmpty(currentRoot) ||
            !string.Equals(currentRoot, _registeredModRoot, StringComparison.OrdinalIgnoreCase) ||
            HeroPrefab == null ||
            !AreSkinResourcesReady() ||
            AttackPrefab == null ||
            AttackInstancePrefab == null ||
            AttackCritInstancePrefab == null ||
            !DariusFormalRegistry.IsRegistrationHealthyForBootstrap())
            return false;

        if (!DariusUnsupportedResourceBridge.IsTypedResourceIdentityMapped(
                database, typeof(Hero_Darius), HeroName, HeroGuid) ||
            !DariusUnsupportedResourceBridge.IsTypedResourceIdentityMapped(
                database, typeof(At_DariusAxe), AttackName, AttackGuid) ||
            !DariusUnsupportedResourceBridge.IsTypedResourceIdentityMapped(
                database, typeof(Ai_DariusAxe), AttackInstanceName, AttackInstanceGuid) ||
            !DariusUnsupportedResourceBridge.IsTypedResourceIdentityMapped(
                database, typeof(Ai_DariusAxe_Crit), AttackCritInstanceName, AttackCritInstanceGuid))
            return false;

        if (!DariusUnsupportedResourceBridge.IsNetworkGuidMapped(database, HeroAssetId, HeroGuid) ||
            !DariusUnsupportedResourceBridge.IsNetworkGuidMapped(database, AttackAssetId, AttackGuid) ||
            !DariusUnsupportedResourceBridge.IsNetworkGuidMapped(database, AttackInstanceAssetId, AttackInstanceGuid) ||
            !DariusUnsupportedResourceBridge.IsNetworkGuidMapped(database, AttackCritInstanceAssetId, AttackCritInstanceGuid))
            return false;

        if (!DariusMirrorRuntimeHealth.IsHandlerPairHealthy(HeroAssetId) ||
            !DariusMirrorRuntimeHealth.IsHandlerPairHealthy(AttackAssetId) ||
            !DariusMirrorRuntimeHealth.IsHandlerPairHealthy(AttackInstanceAssetId) ||
            !DariusMirrorRuntimeHealth.IsHandlerPairHealthy(AttackCritInstanceAssetId))
            return false;

        for (int i = 0; i < SkinSpecs.Length; i++)
            if (!DariusUnsupportedResourceBridge.IsNamedResourceIdentityMapped(
                    database, SkinSpecs[i].name, SkinSpecs[i].guid))
                return false;

        try
        {
            if (!Dew.allHeroes.Contains(typeof(Hero_Darius))) return false;
            for (int i = 0; i < DariusRegisteredSkillTypes.Length; i++)
                if (!Dew.allSkills.Contains(DariusRegisteredSkillTypes[i])) return false;
            for (int i = 0; i < DariusRegisteredHeroSkillTypes.Length; i++)
                if (!Dew.allHeroSkills.Contains(DariusRegisteredHeroSkillTypes[i])) return false;
        }
        catch
        {
            return false;
        }

        return true;
    }

    private static bool AreStockConstructionTemplatesReady(out string missing)
    {
        missing = null;
        try
        {
            Hero hero = DewResources.GetByShortTypeName<Hero>("Hero_Vesper");
            if (hero == null) { missing = "Hero_Vesper"; return false; }

            Skin skin = DewResources.GetByName<Skin>("Skin_Vesper_Default");
            if (skin == null) { missing = "Skin_Vesper_Default"; return false; }

            Type attackType = AccessTools.TypeByName("At_Atk_VesperMace");
            AttackTrigger attack = attackType != null
                ? DewResources.GetByType<AttackTrigger>(attackType)
                : null;
            if (attack == null) { missing = "At_Atk_VesperMace"; return false; }

            MeleeAttackInstance normal = null;
            if (attack.configs != null)
            {
                for (int i = 0; i < attack.configs.Length; i++)
                {
                    TriggerConfig config = attack.configs[i];
                    MeleeAttackInstance instance = config != null
                        ? config.spawnedInstance as MeleeAttackInstance
                        : null;
                    if (instance == null) continue;
                    if (instance.GetType().Name.IndexOf("Crit", StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        normal = instance;
                        break;
                    }
                }
            }
            if (normal == null)
            {
                Type normalType = AccessTools.TypeByName("Ai_Atk_VesperMace");
                if (normalType != null)
                    normal = DewResources.GetByType<MeleeAttackInstance>(normalType);
            }
            if (normal == null) { missing = "Ai_Atk_VesperMace"; return false; }

            return true;
        }
        catch (Exception e)
        {
            missing = e.GetType().Name + ":" + e.Message;
            return false;
        }
    }

    public static IEnumerator InitializeWhenReady(int generationId, int ownerId)
    {
        DariusLog.Info("TRAVELER", "Bootstrap coroutine entered generation=" + generationId +
            " owner=" + ownerId + "; waiting for resource database + Mod root.");

        while (IsExpectedGenerationActive(generationId, ownerId) &&
               (DewResources.database == null ||
                string.IsNullOrEmpty(DariusModEnvironment.Root) ||
                DariusPrototypeMod.IsBootstrapBlockedThisFrame))
        {
            yield return null;
        }
        if (!IsExpectedGenerationActive(generationId, ownerId))
        {
            DariusLog.Info("TRAVELER-LIFECYCLE", "Stale bootstrap coroutine exited before core registration generation=" +
                generationId + " owner=" + ownerId + ".");
            yield break;
        }

        string missingTemplate;
        float templateDeadline = Time.unscaledTime + 5f;
        while (IsExpectedGenerationActive(generationId, ownerId) &&
               !AreStockConstructionTemplatesReady(out missingTemplate))
        {
            if (Time.unscaledTime >= templateDeadline)
            {
                DariusLog.Error("TRAVELER",
                    "Stock construction templates did not become ready within 5s generation=" +
                    generationId + " owner=" + ownerId + " missing=" + (missingTemplate ?? "<unknown>") + ".");
                yield break;
            }

            DariusLog.DebugInfoThrottled("TRAVELER-READY", "stock-templates",
                "Waiting for stock construction templates; missing=" + (missingTemplate ?? "<unknown>"), 1.0);
            yield return null;
        }
        if (!IsExpectedGenerationActive(generationId, ownerId)) yield break;

        bool coreReady = false;
        for (int attempt = 1; attempt <= CoreRegistrationRetryLimit; attempt++)
        {
            if (!IsExpectedGenerationActive(generationId, ownerId)) yield break;

            while (DariusPrototypeMod.IsBootstrapBlockedThisFrame)
            {
                if (!IsExpectedGenerationActive(generationId, ownerId)) yield break;
                yield return null;
            }

            if (DewResources.database == null || string.IsNullOrEmpty(DariusModEnvironment.Root))
            {
                attempt--;
                yield return null;
                continue;
            }

            coreReady = EnsureCoreRegisteredForBootstrap(
                "InitializeWhenReady core phase attempt=" + attempt,
                generationId,
                ownerId);
            if (coreReady) break;

            if (attempt < CoreRegistrationRetryLimit) yield return null;
        }

        if (!coreReady)
        {
            DariusLog.Error("TRAVELER", "Core Hero_Darius bootstrap exhausted " + CoreRegistrationRetryLimit +
                " attempts generation=" + generationId + " owner=" + ownerId + ".");
            yield break;
        }

        while (IsExpectedGenerationActive(generationId, ownerId) &&
               (DewBuildProfile.current == null || DewBuildProfile.current.content == null ||
                DewSave.profileMain == null || DewSave.profileStats == null))
        {
            yield return null;
        }
        if (!IsExpectedGenerationActive(generationId, ownerId)) yield break;

        for (int attempt = 1; attempt <= ProfileRegistrationRetryLimit; attempt++)
        {
            if (!IsExpectedGenerationActive(generationId, ownerId)) yield break;

            if (CompleteProfileRegistration(
                    "InitializeWhenReady profile/content phase attempt=" + attempt,
                    generationId,
                    ownerId))
                yield break;

            if (attempt < ProfileRegistrationRetryLimit)
                yield return new WaitForSecondsRealtime(0.15f);
        }

        DariusLog.Error("TRAVELER", "Late profile/content registration exhausted " +
            ProfileRegistrationRetryLimit + " attempts generation=" + generationId +
            " owner=" + ownerId + "; core resources remain registered for diagnostics.");
    }

    public static bool EnsureCoreRegisteredForBootstrap(string reason, int generationId, int ownerId)
    {
        if (!IsExpectedGenerationActive(generationId, ownerId))
        {
            DariusLog.DebugInfo("TRAVELER-LIFECYCLE", "Rejected stale core bootstrap request generation=" +
                generationId + " owner=" + ownerId + " reason=" + (reason ?? "<unknown>"));
            return false;
        }

        if (DariusPrototypeMod.IsBootstrapBlockedThisFrame ||
            DewResources.database == null ||
            string.IsNullOrEmpty(DariusModEnvironment.Root))
            return false;

        if (IsCoreRegistrationHealthy())
        {
            RegisterContent(DewBuildProfile.current != null ? DewBuildProfile.current.content : null);
            return true;
        }

        if (_registering) return false;

        bool rootChanged = _registered &&
                           !string.IsNullOrEmpty(_registeredModRoot) &&
                           !string.Equals(_registeredModRoot, DariusModEnvironment.Root, StringComparison.OrdinalIgnoreCase);
        bool hasTravelerState = _registered || _resourceRoot != null || HeroPrefab != null ||
                                AttackPrefab != null || OwnedObjects.Count > 0 ||
                                ResourcesByGuid.Count > 0 || NetworkPrefabs.Count > 0;
        if (hasTravelerState)
        {
            DariusLog.Warn("TRAVELER-BOOTSTRAP",
                "Discarding unhealthy/partial Traveler generation before next-frame recreation reason=" +
                (reason ?? "<unknown>") + " rootChanged=" + rootChanged +
                " state=" + DiagnosticState());
            DariusRuntimeAudit.LogSnapshot(
                "before unhealthy Traveler generation discard: " + (reason ?? "<unknown>"), true);
            UnregisterRuntimeOnly();

            if (rootChanged)
            {
                // Icons/media/VFX and Formal prefab presentation may already point at the early
                // fallback root. Invalidate those root-dependent objects as one transaction; the
                // bootstrap barrier prevents their replacements from being created this frame.
                DariusFormalRegistry.Unregister();
                DariusLolVfxRuntime.Unload();
                DariusMedia.Unload();
                DariusPrototypeIcons.Unload();
                DariusLog.Info("TRAVELER-BOOTSTRAP",
                    "Authoritative Mod root changed; cleared Formal and presentation caches before rebuild.");
            }

            DariusRuntimeAudit.LogSnapshot(
                "after unhealthy Traveler generation discard: " + (reason ?? "<unknown>"), true);
            return false;
        }

        try
        {
            DariusFormalRegistry.Register();
            if (!DariusFormalRegistry.IsRegistrationHealthyForBootstrap())
                return false;

            Register();
            bool ok = IsCoreRegistrationHealthy();
            if (ok)
            {
                RegisterContent(DewBuildProfile.current != null ? DewBuildProfile.current.content : null);
                DariusLog.DebugInfoThrottled("TRAVELER-EARLY", reason ?? "bootstrap",
                    "Core Hero_Darius generation is healthy and mapped in Dew/Mirror runtime indexes.", 2.0);
            }
            return ok;
        }
        catch (Exception e)
        {
            DariusLog.Exception("TRAVELER-EARLY", e,
                "Core registration failed reason=" + (reason ?? "<unknown>"));
            return false;
        }
    }

    private static bool CompleteProfileRegistration(string reason, int generationId, int ownerId)
    {
        if (!IsExpectedGenerationActive(generationId, ownerId)) return false;

        if (!IsCoreRegistrationHealthy())
        {
            if (!EnsureCoreRegisteredForBootstrap(
                    reason + " core prerequisite", generationId, ownerId))
                return false;
        }

        if (!IsExpectedGenerationActive(generationId, ownerId)) return false;

        try
        {
            RegisterTypes();
            RegisterContent(DewBuildProfile.current != null ? DewBuildProfile.current.content : null);
            EnsureProfiles();
            ValidateProfileRegistration();
            RepairHeroCosmeticContract(HeroPrefab);
            DariusLog.Info("TRAVELER", "Late profile/content registration completed generation=" +
                generationId + " owner=" + ownerId + " reason=" + reason +
                " state=" + DiagnosticState());
            DariusRuntimeAudit.LogSnapshot(
                "Traveler profile/content registration completed: " + reason, false);
            return true;
        }
        catch (Exception e)
        {
            DariusLog.Exception("TRAVELER", e,
                "Late profile/content registration failed generation=" + generationId +
                " owner=" + ownerId + " reason=" + reason + "; retry remains bounded to this generation");
            return false;
        }
    }

    public static void Register()
    {
        if (_registered || _registering) return;
        if (!IsExpectedGenerationActive(_modOwnerGenerationId, _modOwnerInstanceId))
            throw new InvalidOperationException("Traveler registration attempted from a stale ModBehaviour generation.");
        if (DariusPrototypeMod.IsBootstrapBlockedThisFrame)
            throw new InvalidOperationException("Traveler registration attempted during the teardown frame barrier.");
        if (DewResources.database == null) throw new InvalidOperationException("DewResources.database is null.");
        if (string.IsNullOrEmpty(DariusModEnvironment.Root))
            throw new InvalidOperationException("Darius Mod root is not ready.");
        if (DariusFormalRegistry.Decimate == null || DariusFormalRegistry.NoxianGuillotine == null || DariusFormalRegistry.Hemorrhage == null)
            throw new InvalidOperationException("Darius skill resources must be registered before Hero_Darius.");

        _registering = true;
        try
        {
            CreateResourceRoot();
            ConfigureDariusSkillOwnership();
            CreateAndRegisterNativeBasicAttack();
            CreateAndRegisterSkin();
            CreateAndRegisterHero();
            RegisterTypes();
            RegisterContent(DewBuildProfile.current != null ? DewBuildProfile.current.content : null);
            ValidateRegistration();
            _registeredDatabase = DewResources.database;
            _registeredModRoot = DariusModEnvironment.Root;
            _registered = true;
            CreateLifecycleBridge();

            DariusLog.Info("TRAVELER", "Hero_Darius registration READY. heroGuid=" + HeroGuid + " heroAssetId=" + HeroAssetId +
                " skins=" + string.Join(",", Array.ConvertAll(SkinSpecs, s => s.name)) + " Q=" + DariusFormalRegistry.Decimate.name + " R=" + DariusFormalRegistry.NoxianGuillotine.name +
                " Identity=" + DariusFormalRegistry.Hemorrhage.name + " state=" + DiagnosticState());
            DariusRuntimeAudit.LogSnapshot("Traveler registration READY", true);
        }
        catch
        {
            // Awake may race the final initialization of stock native templates on Workshop boot.
            // Never keep a half-created runtime graph; a clean Start/coroutine retry is safer.
            try { UnregisterRuntimeOnly(); } catch { }
            throw;
        }
        finally
        {
            _registering = false;
        }
    }

    private static void CreateResourceRoot()
    {
        if (_resourceRoot != null) return;

        if (_modOwner == null)
            throw new InvalidOperationException("DariusTravelerRegistry has no ModBehaviour owner.");

        // Runtime prefab templates belong to the current ModBehaviour generation. Parenting the
        // inactive root to the loader-owned container makes Unity's normal mod lifecycle the final
        // ownership boundary instead of keeping a parallel persistent scene graph alive.
        _resourceRoot = new GameObject("DariusTraveler_RuntimeResources");
        _resourceRoot.hideFlags = HideFlags.HideAndDontSave;
        _resourceRoot.transform.SetParent(_modOwner, false);
        _resourceRoot.SetActive(false);
    }

    private static void CreateLifecycleBridge()
    {
        if (_lifecycleBridgeObject != null) return;
        if (_modOwner == null)
            throw new InvalidOperationException("DariusTravelerRegistry has no ModBehaviour owner.");

        _lifecycleBridgeObject = new GameObject("DariusTraveler_Lifecycle");
        _lifecycleBridgeObject.hideFlags = HideFlags.HideAndDontSave;
        _lifecycleBridgeObject.transform.SetParent(_modOwner, false);
        _lifecycleBridgeObject.AddComponent<DariusTravelerLifecycleBridge>();
        DariusLog.Info("TRAVELER-LIFECYCLE", "Scene lifecycle bridge attached to the current ModBehaviour owner.");
    }

    private static void DestroyLifecycleBridge()
    {
        if (_lifecycleBridgeObject == null) return;

        // Destroy is end-of-frame, but SceneManager callbacks are live until OnDisable. Disable the
        // bridge synchronously so a superseded generation cannot observe a later scene event in the
        // teardown frame and mutate R room state after ownership has moved on.
        try
        {
            if (_lifecycleBridgeObject.activeSelf)
                _lifecycleBridgeObject.SetActive(false);
        }
        catch { }

        UnityEngine.Object.Destroy(_lifecycleBridgeObject);
        _lifecycleBridgeObject = null;
    }

    private static void ConfigureDariusSkillOwnership()
    {
        ConfigureCharacterSkill(DariusFormalRegistry.Decimate, Rarity.Character, HeroName, "Q", true);
        ConfigureCharacterSkill(DariusFormalRegistry.NoxianGuillotine, Rarity.Character, HeroName, "R", true);
        ConfigureCharacterSkill(DariusFormalRegistry.Hemorrhage, Rarity.Identity, HeroName, "Identity", true);
        ConfigureCharacterSkill(DariusFormalRegistry.Flash, Rarity.Character, HeroName, "Movement", true);
        ConfigureCharacterSkill(DariusFormalRegistry.Ghost, Rarity.Character, HeroName, "Movement", true);

        // W/E remain ordinary Memories and are not welded to Hero_Darius.
        ConfigureCharacterSkill(DariusFormalRegistry.CripplingStrike, Rarity.Common, string.Empty, "W", false);
        ConfigureCharacterSkill(DariusFormalRegistry.Apprehend, Rarity.Common, string.Empty, "E", false);
    }
}