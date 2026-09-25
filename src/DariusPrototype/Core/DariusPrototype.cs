using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;

// Shape of Dreams - Darius / Hand of Noxus Traveler.
// Release runtime: independent Hero_Darius, Darius-owned skin/attack resources,
// League-authentic presentation assets, native constellation integration, and MOD-owned
// runtime prefab templates registered into Dew's lookup/network maps.

public sealed class DariusPrototypeMod : ModBehaviour
{
    [ModConfig.LabelText("Audio Volume / 技能音效音量")]
    public DariusAudioVolumeConfig skillAudioVolume = new DariusAudioVolumeConfig();

    public override void OnConfigChanged()
    {
        DariusAudioSettingsRuntime.Bind(skillAudioVolume);
        Debug.Log("[DariusAudio] config applied q=" + skillAudioVolume.qVolume.ToString("0.##") + " w=" + skillAudioVolume.wVolume.ToString("0.##") + " e=" + skillAudioVolume.eVolume.ToString("0.##") + " r=" + skillAudioVolume.rVolume.ToString("0.##") + " basic=" + skillAudioVolume.basicAttackVolume.ToString("0.##") + " dodge=" + skillAudioVolume.dodgeVolume.ToString("0.##") + " voice=" + skillAudioVolume.voiceVolume.ToString("0.##"));
    }

    private static int _activeModInstanceId;
    private bool _bootstrapped;
    private bool _applicationQuitting;
    private bool _shutdownCompleted;
    private int _instanceId;
    private DariusDiagnostics _diagnostics;

    private void Awake()
    {
        // Workshop ModBehaviour instances can exist a frame before Start(). Resolve the loader-provided
        // ModItem.path here so every later icon/model/audio lookup already knows the numeric Workshop root.
        // Configure() is intentionally safe when instance/mod is not populated yet; Start() retries it.
        DariusModEnvironment.Configure(this);
        DariusAudioSettingsRuntime.Bind(skillAudioVolume);
        DariusLog.Initialize();

        _instanceId = GetInstanceID();
        if (_activeModInstanceId != 0 && _activeModInstanceId != _instanceId)
        {
            DariusLog.Info("BOOT", "New ModBehaviour instance superseded owner=" + _activeModInstanceId +
                " with owner=" + _instanceId + "; cleaning the previous runtime generation before bootstrap.");
            ResetSharedRuntimeForReplacement();
        }
        _activeModInstanceId = _instanceId;
        DariusTravelerRegistry.BindOwner(transform);
        DariusFormalRegistry.BindOwner(transform);

        TravelerBasicAttackVfxReplication.Initialize();
        try
        {
            if (DewResources.database != null && !string.IsNullOrEmpty(DariusModEnvironment.Root))
                DariusTravelerRegistry.EnsureCoreRegisteredForBootstrap("Mod Awake synchronous Workshop bootstrap");
        }
        catch (Exception e)
        {
            // Unity may invoke Awake before every stock resource service is fully usable. This is a
            // best-effort head start only; Start() and InitializeWhenReady() retry the same bootstrap.
            DariusLog.Exception("TRAVELER-EARLY", e, "Awake Workshop bootstrap deferred to Start/coroutine");
        }
    }

    private void Start()
    {
        // Re-read ModItem.path now that the loader has completed the ModBehaviour contract.
        DariusModEnvironment.Configure(this);
        DariusAudioSettingsRuntime.Bind(skillAudioVolume);
        DariusLog.Initialize();
        DariusLog.Info("BOOT", "DariusPrototype version=" + DariusModEnvironment.Version + "; Start() entered. bootstrapped=" + _bootstrapped +
            " modRoot=" + (DariusModEnvironment.Root ?? "<null>") + " source=" + DariusModEnvironment.SourceLabel);
        _diagnostics = gameObject.GetComponent<DariusDiagnostics>();
        if (_diagnostics == null) _diagnostics = gameObject.AddComponent<DariusDiagnostics>();
        _diagnostics.Initialize();
        DariusLog.Info("BOOT", "Initial snapshot: " + DariusDiagnostics.Snapshot());

        instance.isAlteringGameplay = true;
        if (!_bootstrapped)
        {
            // A presentation-only Harmony patch must never prevent Hero_Darius from registering.
            // RC2 aborted Start() here when StarList.Refresh gained an overload, which made the
            // entire Traveler disappear even though the DLL itself compiled correctly.
            try
            {
                harmony.PatchAll();
                DariusLog.Info("PATCH", "Harmony PatchAll completed.");
            }
            catch (Exception e)
            {
                DariusLog.Exception("PATCH", e, "Harmony PatchAll reported a failure; continuing critical Darius boot so the Traveler can still register");
            }

            try
            {
                DariusRuntimeResourceCompatibility.Install(harmony);
            }
            catch (Exception e)
            {
                DariusLog.Exception("PATCH", e, "Runtime resource compatibility install failed; continuing boot for diagnostics");
            }

            try
            {
                DariusDejaVuRegistry.InstallLocalizationPatches(harmony);
            }
            catch (Exception e)
            {
                DariusLog.Exception("PATCH", e, "Localization patch install failed; continuing boot");
            }

            try
            {
                DariusRInputGuard.Install(harmony);
            }
            catch (Exception e)
            {
                DariusLog.Exception("PATCH", e, "R input guard install failed; continuing boot");
            }

            _bootstrapped = true;
        }

        // If Dew's resource database is already online, synchronously install the minimum Hero/Skin
        // resource bridge before lobby/mastery UI gets another frame to resolve persisted Hero_Darius.
        // If it is not ready yet, InitializeWhenReady below performs the same operation as soon as it is.
        try
        {
            if (DewResources.database != null)
                DariusTravelerRegistry.EnsureCoreRegisteredForBootstrap("Mod Start synchronous Workshop bootstrap");
        }
        catch (Exception e)
        {
            DariusLog.Exception("TRAVELER-EARLY", e, "Synchronous Workshop Hero/Skin bootstrap failed; coroutine fallback remains active");
        }

        // Media loading comes after ModItem.path has been resolved and after the resource lookup guard
        // is installed. This prevents numeric Workshop folders from falling back to local Mods paths.
        try
        {
            DariusMedia.PreloadAll();
            StartCoroutine(DariusMedia.PreloadCompressedAudio());
        }
        catch (Exception e) { DariusLog.Exception("MEDIA", e, "PreloadAll/compressed-audio preload failed"); }

        // Always start the critical registration coroutine even when an optional Harmony/UI patch
        // failed. This is the authoritative path that creates Hero_Darius/Skin_Darius_Default.
        StartCoroutine(DariusTravelerRegistry.InitializeWhenReady());
        DariusLog.Info("BOOT", "Registration coroutine started. Target=independent Hero_Darius, native Hero/Skin/Loadout/Profile/Mirror paths.");
    }

    private void OnApplicationQuit()
    {
        _applicationQuitting = true;
        try { DariusConstellationPersistence.SaveCurrent(DewSave.profileMain, "application quit"); } catch { }
        ShutdownRuntimeResources("application quit");
    }

    private void OnDestroy()
    {
        if (_diagnostics != null) Destroy(_diagnostics);

        // Save before *any* scene or mod lifecycle destruction. A profile Validate can run during
        // the next transition/reload before the replacement dynamic assembly has re-registered its
        // StarEffects; without this snapshot newly purchased custom stars could be refunded.
        try { DariusConstellationPersistence.SaveCurrent(DewSave.profileMain, "ModBehaviour OnDestroy pre-cleanup"); } catch { }

        if (_instanceId != 0 && _activeModInstanceId != 0 && _activeModInstanceId != _instanceId)
        {
            DariusLog.Info("BOOT", "Superseded ModBehaviour destroyed owner=" + _instanceId +
                " activeOwner=" + _activeModInstanceId + "; shared runtime belongs to the newer instance.");
            return;
        }

        ShutdownRuntimeResources(_applicationQuitting ? "application quit" : "ModBehaviour destroyed");
        if (_activeModInstanceId == _instanceId) _activeModInstanceId = 0;
    }

    private void ResetSharedRuntimeForReplacement()
    {
        DariusRInputGuard.Uninstall();
        try { harmony.UnpatchAll(harmony.Id); } catch { }
        DariusRuntimeResourceCompatibility.ResetInstallState();
        DariusDejaVuRegistry.ResetPatchInstallState();
        DariusTravelerRegistry.ShutdownRuntimeResources();
        DariusFormalRegistry.ShutdownRuntimeResources();
    }

    private void ShutdownRuntimeResources(string reason)
    {
        if (_shutdownCompleted) return;
        _shutdownCompleted = true;
        DariusLog.Info("BOOT", "Cleaning Darius runtime resources: " + reason);
        DariusRInputGuard.Uninstall();
        try { harmony.UnpatchAll(harmony.Id); } catch { }
        DariusRuntimeResourceCompatibility.ResetInstallState();
        DariusDejaVuRegistry.ResetPatchInstallState();
        DariusTravelerRegistry.ShutdownRuntimeResources();
        DariusFormalRegistry.ShutdownRuntimeResources();
        _bootstrapped = false;
        DariusLog.Flush();
        if (_applicationQuitting || !Application.isPlaying) DariusLog.Shutdown();
    }
}
