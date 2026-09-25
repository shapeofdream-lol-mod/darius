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
    private static Transform _modOwner;
    private static int _modOwnerInstanceId;

    public static void BindOwner(Transform owner)
    {
        if (owner == null) throw new ArgumentNullException(nameof(owner));
        int ownerId = owner.gameObject.GetInstanceID();
        if (_modOwnerInstanceId == ownerId)
        {
            _modOwner = owner;
            return;
        }

        if (_resourceRoot != null || _lifecycleBridgeObject != null || _registered)
            throw new InvalidOperationException("Traveler runtime generation must be cleaned before binding a new ModBehaviour owner.");

        _modOwner = owner;
        _modOwnerInstanceId = ownerId;
        DariusLog.Info("TRAVELER-LIFECYCLE", "Bound runtime resource ownership to ModBehaviour owner=" + ownerId + ".");
    }

    public static IEnumerator InitializeWhenReady()
    {
        // Workshop mods can be instantiated before DewBuildProfile/profile services are ready, while
        // the lobby/reward UI may already contain persisted Hero_Darius references from the prior
        // session. Waiting for *all* profile services here creates a lookup race. Only the resource
        // database is required to create the runtime Hero/Skin/type bridges, so register that core
        // as soon as possible and finish profile/content integration later.
        DariusLog.Info("TRAVELER", "Waiting for DewResources.database before core Hero_Darius registration.");
        while (DewResources.database == null)
            yield return null;

        EnsureCoreRegisteredForBootstrap("InitializeWhenReady core phase");

        // Profile/content may come online a few frames later on Workshop boot. Once ready, perform
        // one targeted native unlock/loadout registration pass; scene transitions do not re-inject it.
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
        if (DewResources.database == null) return false;

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