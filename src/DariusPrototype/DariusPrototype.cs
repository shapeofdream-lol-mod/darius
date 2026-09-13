using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;

// Shape of Dreams - Darius / Hand of Noxus Traveler.
// Release runtime: independent Hero_Darius, Darius-owned skin/attack resources,
// League-authentic presentation assets, native constellation integration, and persistent
// scene-transition self-healing.

public sealed class DariusPrototypeMod : ModBehaviour
{
    [ModConfig.LabelText("Audio Volume / 技能音效音量")]
    public DariusAudioVolumeConfig skillAudioVolume = new DariusAudioVolumeConfig();

    public override void OnConfigChanged()
    {
        DariusAudioSettingsRuntime.Bind(skillAudioVolume);
        DariusLog.Info("AUDIO-CONFIG", "Applied q=" + skillAudioVolume.qVolume.ToString("0.##") +
            " w=" + skillAudioVolume.wVolume.ToString("0.##") +
            " e=" + skillAudioVolume.eVolume.ToString("0.##") +
            " r=" + skillAudioVolume.rVolume.ToString("0.##") +
            " basic=" + skillAudioVolume.basicAttackVolume.ToString("0.##") +
            " dodge=" + skillAudioVolume.dodgeVolume.ToString("0.##") +
            " voice=" + skillAudioVolume.voiceVolume.ToString("0.##"));
    }

    private static bool _bootstrapped;
    private static bool _applicationQuitting;
    private static bool _shutdownCompleted;
    private DariusDiagnostics _diagnostics;

    private void Awake()
    {
        // Workshop ModBehaviour instances can exist a frame before Start(). Resolve the loader-provided
        // ModItem.path here so every later icon/model/audio lookup already knows the numeric Workshop root.
        // Configure() is intentionally safe when instance/mod is not populated yet; Start() retries it.
        DariusModEnvironment.Configure(this);
        DariusAudioSettingsRuntime.Bind(skillAudioVolume);
        DariusLog.Initialize();
        TravelerBasicAttackVfxReplication.Initialize();
        try
        {
            if (DewResources.database != null && !string.IsNullOrEmpty(DariusModEnvironment.Root))
                DariusTravelerRegistry.EnsureCoreRegisteredForLookup("Mod Awake synchronous Workshop bootstrap");
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
                DariusModLifecycle.Install(harmony);
            }
            catch (Exception e)
            {
                DariusLog.Exception("MOD-LIFECYCLE", e, "DewMod.UnloadAll lifecycle guard install failed; continuing boot");
            }

            try
            {
                DariusRuntimeResourceCompatibility.Install(harmony);
            }
            catch (Exception e)
            {
                DariusLog.Exception("PATCH", e, "Runtime resource compatibility install failed; continuing boot for diagnostics/self-heal");
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
        else
        {
            DariusLog.Info("BOOT", "Scene-created ModBehaviour detected; keeping existing Harmony/runtime registrations instead of duplicating them.");
        }

        // If Dew's resource database is already online, synchronously install the minimum Hero/Skin
        // resource bridge before lobby/mastery UI gets another frame to resolve persisted Hero_Darius.
        // If it is not ready yet, InitializeWhenReady below performs the same operation as soon as it is.
        try
        {
            if (DewResources.database != null)
                DariusTravelerRegistry.EnsureCoreRegisteredForLookup("Mod Start synchronous Workshop bootstrap");
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
        DariusLog.Info("BOOT", "Registration/repair coroutine started. Target=independent Hero_Darius, native Hero/Skin/Loadout/Profile/Mirror paths.");
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

        bool trueModUnload = DariusModLifecycle.IsModManagerUnloading;
        if (_applicationQuitting || !Application.isPlaying || trueModUnload)
        {
            ShutdownRuntimeResources(trueModUnload ? "DewMod.UnloadAll hot reload" : "real shutdown");
        }
        else
        {
            DariusLog.Info("BOOT", "Ordinary scene lifecycle destroyed ModBehaviour; preserving persistent Darius resources. Constellation state was snapshotted first.");
        }
    }

    private void ShutdownRuntimeResources(string reason)
    {
        if (_shutdownCompleted) return;
        _shutdownCompleted = true;
        DariusLog.Info("BOOT", "Cleaning Darius runtime resources: " + reason);
        DariusTravelerRegistry.UnregisterRuntimeOnly();
        DariusFormalRegistry.Unregister();
        try { harmony.UnpatchAll(harmony.Id); } catch { }
        _bootstrapped = false;
        DariusLog.Flush();
        if (_applicationQuitting || !Application.isPlaying) DariusLog.Shutdown();
    }
}