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
    public static IEnumerator InitializeWhenReady()
    {
        // Workshop mods can be instantiated before DewBuildProfile/profile services are ready, while
        // the lobby/reward UI may already contain persisted Hero_Darius references from the prior
        // session. Waiting for *all* profile services here creates a lookup race. Only the resource
        // database is required to create the runtime Hero/Skin/type bridges, so register that core
        // as soon as possible and finish profile/content integration later.
        DariusLog.Info("TRAVELER", "Waiting only for DewResources.database before core Hero_Darius registration.");
        while (DewResources.database == null)
            yield return null;

        EnsureCoreRegisteredForLookup("InitializeWhenReady core phase");

        // Profile/content may come online a few frames later on Workshop boot. Once ready, perform
        // the same native unlock/loadout validation pass used by local installs.
        while (DewBuildProfile.current == null || DewBuildProfile.current.content == null ||
               DewSave.profileMain == null || DewSave.profileStats == null)
            yield return null;

        CompleteProfileRegistration("InitializeWhenReady profile/content phase");
    }

    public static bool EnsureCoreRegisteredForLookup(string reason)
    {
        if (_registered && HeroPrefab != null && AreSkinResourcesReady()) return true;
        // Registration itself can invoke Dew/UI callbacks. Never recursively enter Register(); once
        // Skin+Hero have been created those callbacks can safely consume them, otherwise they defer.
        if (_registering) return HeroPrefab != null && DefaultSkin != null;
        if (DewResources.database == null) return false;

        try
        {
            // Skills must exist before the HeroSkill AssetRef arrays are built.
            DariusFormalRegistry.Register();
            if (!_registered) Register();
            else RepairRuntimeRegistration(reason ?? "core lookup self-heal");
            bool ok = HeroPrefab != null && AreSkinResourcesReady();
            if (ok)
                DariusLog.DebugInfoThrottled("TRAVELER-EARLY", reason ?? "lookup", "Core Hero_Darius/Skin_Darius_Default resources available before profile completion.", 2.0);
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
            if (!EnsureCoreRegisteredForLookup(reason + " core prerequisite")) return;
        }

        try
        {
            RegisterTypes();
            RegisterContent(DewBuildProfile.current != null ? DewBuildProfile.current.content : null);
            EnsureProfiles();
            RepairRuntimeRegistration(reason);
            NotifyDewMissingReferenceRepair();
            DariusLog.Info("TRAVELER", "Late profile/content registration completed reason=" + reason);
        }
        catch (Exception e)
        {
            DariusLog.Exception("TRAVELER", e, "Late profile/content registration failed reason=" + reason);
        }
    }

    private static void NotifyDewMissingReferenceRepair()
    {
        try
        {
            // The public DewResources repair pair is the authoritative way to revisit already-created
            // AssetRef/UI references after a runtime resource becomes available. Merely invoking
            // onRepairMissingReferences is not equivalent to running the repair transaction.
            DewResources.RepairMissingReferences_Prepare();
            DewResources.RepairMissingReferences_Repair();
            // RepairMissingReferences can replace/light-load resource references. Reassert our
            // runtime Hero.icon/mainColor and every Skin.previewImage only after that transaction.
            RepairHeroCosmeticContract(HeroPrefab);
            DariusLog.Info("TRAVELER-EARLY", "Executed DewResources missing-reference Prepare->Repair and rebound Hero/Skin presentation fields after Workshop profile completion.");
        }
        catch (Exception e)
        {
            DariusLog.Exception("TRAVELER-EARLY", e, "DewResources missing-reference Prepare/Repair failed");
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
        _resourceRoot = new GameObject("DariusTraveler_RuntimeResources");
        _resourceRoot.hideFlags = HideFlags.HideAndDontSave;
        _resourceRoot.SetActive(false);
        UnityEngine.Object.DontDestroyOnLoad(_resourceRoot);
    }

    private static void CreateLifecycleBridge()
    {
        DariusTravelerLifecycleBridge existing = UnityEngine.Object.FindFirstObjectByType<DariusTravelerLifecycleBridge>();
        if (existing != null) return;
        GameObject go = new GameObject("DariusTraveler_PersistentLifecycle");
        go.hideFlags = HideFlags.HideAndDontSave;
        go.AddComponent<DariusTravelerLifecycleBridge>();
        UnityEngine.Object.DontDestroyOnLoad(go);
        OwnedObjects.Add(go);
        DariusLog.Info("TRAVELER-LIFECYCLE", "Persistent scene lifecycle bridge created. Runtime Hero/Skin/Profile maps will be reasserted around every scene transition.");
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