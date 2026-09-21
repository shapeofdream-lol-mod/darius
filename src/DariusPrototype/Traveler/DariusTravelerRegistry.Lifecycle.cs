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
    private static GameObject _lifecycleBridgeObject;
    private static bool _resourceLifecycleHooked;

    public static IEnumerator InitializeWhenReady()
    {
        // Workshop mods can be instantiated before DewBuildProfile/profile services are ready, while
        // the lobby/reward UI may already contain persisted Hero_Darius references from the prior
        // session. Waiting for *all* profile services here creates a lookup race. Only the resource
        // database is required to create the runtime Hero/Skin/type bridges, so register that core
        // as soon as possible and finish profile/content integration later.
        DariusLog.Info("TRAVELER", "Waiting for DewResources database/variant owner before core Hero_Darius registration.");
        while (DewResources.database == null || DewResources.variantsParent == null)
            yield return null;

        EnsureCoreRegisteredForBootstrap("InitializeWhenReady core phase");

        // Profile/content may come online a few frames later on Workshop boot. Once ready, perform
        // the same native unlock/loadout validation pass used by local installs.
        while (DewBuildProfile.current == null || DewBuildProfile.current.content == null ||
               DewSave.profileMain == null || DewSave.profileStats == null)
            yield return null;

        CompleteProfileRegistration("InitializeWhenReady profile/content phase");
    }

    public static bool EnsureCoreRegisteredForBootstrap(string reason)
    {
        bool complete = _registered &&
                        HeroPrefab != null &&
                        AreSkinResourcesReady() &&
                        AttackPrefab != null &&
                        AttackInstancePrefab != null &&
                        AttackCritInstancePrefab != null;
        if (complete) return true;

        // Registration itself can invoke Dew/profile callbacks. Never recursively enter Register().
        if (_registering) return HeroPrefab != null && DefaultSkin != null;
        if (DewResources.database == null || DewResources.variantsParent == null) return false;

        try
        {
            // Only ModBehaviour/bootstrap is allowed to create a Traveler resource generation.
            // If an earlier generation was destroyed during a network/scene teardown, discard its
            // stale registration state as a unit and create one clean generation here, after the
            // replacement ModBehaviour has entered the new scene.
            if (_registered)
            {
                DariusLog.Info("TRAVELER-BOOTSTRAP",
                    "Discarding incomplete Traveler resource generation before bootstrap recreation reason=" +
                    (reason ?? "<unknown>"));
                UnregisterRuntimeOnly();
            }

            // Skills must exist before the HeroSkill AssetRef arrays are built.
            DariusFormalRegistry.Register();
            Register();

            bool ok = HeroPrefab != null && AreSkinResourcesReady() &&
                      AttackPrefab != null && AttackInstancePrefab != null && AttackCritInstancePrefab != null;
            if (ok)
                DariusLog.DebugInfoThrottled("TRAVELER-EARLY", reason ?? "bootstrap",
                    "Core Hero_Darius/Skin_Darius_Default resources available from authoritative bootstrap.", 2.0);
            return ok;
        }
        catch (Exception e)
        {
            DariusLog.Exception("TRAVELER-EARLY", e, "Core registration failed reason=" + (reason ?? "<unknown>"));
            return false;
        }
    }

    private static void CompleteProfileRegistration(string reason)
    {
        if (!_registered || HeroPrefab == null || !AreSkinResourcesReady())
        {
            if (!EnsureCoreRegisteredForBootstrap(reason + " core prerequisite")) return;
        }

        try
        {
            RegisterTypes();
            RegisterContent(DewBuildProfile.current != null ? DewBuildProfile.current.content : null);
            EnsureProfiles();
            RepairRuntimeRegistration(reason);
            RepairHeroCosmeticContract(HeroPrefab);
            DariusLog.Info("TRAVELER", "Late profile/content registration completed reason=" + reason);
        }
        catch (Exception e)
        {
            DariusLog.Exception("TRAVELER", e, "Late profile/content registration failed reason=" + reason);
        }
    }

    public static void Register()
    {
        if (_registered || _registering) return;
        if (DewResources.database == null) throw new InvalidOperationException("DewResources.database is null.");
        if (DariusFormalRegistry.Decimate == null || DariusFormalRegistry.NoxianGuillotine == null || DariusFormalRegistry.Hemorrhage == null)
            throw new InvalidOperationException("Darius skill resources must be registered before Hero_Darius.");

        _registering = true;
        try
        {
            HookResourceLifecycle();
            CreateResourceRoot();
            ConfigureDariusSkillOwnership();
            CreateAndRegisterNativeBasicAttack();
            CreateAndRegisterSkin();
            CreateAndRegisterHero();
            RegisterTypes();
            RegisterContent(DewBuildProfile.current != null ? DewBuildProfile.current.content : null);
            EnsureProfiles();
            ValidateRegistration();
            _registered = true;
            CreateLifecycleBridge();
            RepairRuntimeRegistration("initial post-register finalization");

            DariusLog.Info("TRAVELER", "Hero_Darius registration READY. heroGuid=" + HeroGuid + " heroAssetId=" + HeroAssetId +
                " skins=" + string.Join(",", Array.ConvertAll(SkinSpecs, s => s.name)) + " Q=" + DariusFormalRegistry.Decimate.name + " R=" + DariusFormalRegistry.NoxianGuillotine.name +
                " Identity=" + DariusFormalRegistry.Hemorrhage.name);
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
        Transform variantsParent = DewResources.variantsParent;
        if (variantsParent == null)
            throw new InvalidOperationException("DewResources.variantsParent is null.");

        // Runtime resource templates belong to Dew's resource-variant lifetime. Keeping a separate
        // DontDestroyOnLoad scene root let networked Hero/Attack templates outlive the lifecycle
        // that owns their resource mappings and caused lobby teardown/rebuild drift.
        _resourceRoot = new GameObject("DariusTraveler_RuntimeResources");
        _resourceRoot.hideFlags = HideFlags.HideAndDontSave;
        _resourceRoot.transform.SetParent(variantsParent, false);
        _resourceRoot.SetActive(false);
    }

    private static void HookResourceLifecycle()
    {
        if (_resourceLifecycleHooked || DewResources.onVariantsCleared == null) return;
        DewResources.onVariantsCleared.Remove(OnDewVariantsCleared);
        DewResources.onVariantsCleared.Add(OnDewVariantsCleared);
        _resourceLifecycleHooked = true;
    }

    private static void UnhookResourceLifecycle()
    {
        if (!_resourceLifecycleHooked) return;
        try
        {
            if (DewResources.onVariantsCleared != null)
                DewResources.onVariantsCleared.Remove(OnDewVariantsCleared);
        }
        catch { }
        _resourceLifecycleHooked = false;
    }

    private static void OnDewVariantsCleared()
    {
        if (_registering || DariusModLifecycle.IsModManagerUnloading || !Application.isPlaying) return;
        if (!_registered && HeroPrefab == null && AttackPrefab == null && DefaultSkin == null) return;

        DariusLog.Info("TRAVELER-LIFECYCLE", "Dew resource variants were cleared; recreating the Darius runtime resource generation at the official resource boundary.");
        UnregisterRuntimeOnly();
        EnsureCoreRegisteredForBootstrap("DewResources.onVariantsCleared");
    }

    private static void CreateLifecycleBridge()
    {
        if (_lifecycleBridgeObject != null) return;
        _lifecycleBridgeObject = new GameObject("DariusTraveler_PersistentLifecycle");
        _lifecycleBridgeObject.hideFlags = HideFlags.HideAndDontSave;
        _lifecycleBridgeObject.AddComponent<DariusTravelerLifecycleBridge>();
        UnityEngine.Object.DontDestroyOnLoad(_lifecycleBridgeObject);
        DariusLog.Info("TRAVELER-LIFECYCLE", "Persistent scene lifecycle bridge created. Scene/profile hooks only reassert mappings; Dew resource lifecycle owns resource recreation.");
    }

    private static void DestroyLifecycleBridge()
    {
        if (_lifecycleBridgeObject == null) return;
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