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
        if (_instanceId != 0 && !IsActiveGeneration(_generationId, _instanceId))
            return;
        DariusAudioSettingsRuntime.Bind(skillAudioVolume);
        Debug.Log("[DariusAudio] config applied q=" + skillAudioVolume.qVolume.ToString("0.##") + " w=" + skillAudioVolume.wVolume.ToString("0.##") + " e=" + skillAudioVolume.eVolume.ToString("0.##") + " r=" + skillAudioVolume.rVolume.ToString("0.##") + " basic=" + skillAudioVolume.basicAttackVolume.ToString("0.##") + " dodge=" + skillAudioVolume.dodgeVolume.ToString("0.##") + " voice=" + skillAudioVolume.voiceVolume.ToString("0.##"));
    }

    private static int _activeModInstanceId;
    private static int _activeGenerationId;
    private static int _generationSerial;
    private static int _bootstrapBlockedFrame = -1;

    internal static int ActiveModInstanceId { get { return _activeModInstanceId; } }
    internal static int ActiveGenerationId { get { return _activeGenerationId; } }

    internal static bool IsBootstrapBlockedThisFrame
    {
        get
        {
            try { return _bootstrapBlockedFrame == Time.frameCount; }
            catch { return false; }
        }
    }

    internal static bool IsActiveGeneration(int generationId, int ownerId)
    {
        return generationId > 0 && ownerId != 0 &&
               _activeGenerationId == generationId &&
               _activeModInstanceId == ownerId;
    }

    internal static void BlockBootstrapForCurrentFrame(string reason)
    {
        int frame;
        try { frame = Time.frameCount; }
        catch { frame = -1; }
        if (frame < 0) return;

        if (_bootstrapBlockedFrame != frame)
            DariusLog.Info("BOOT-BARRIER", "Blocked runtime resource rebuild for teardown frame=" + frame +
                " reason=" + (reason ?? "<unknown>") + ".");
        _bootstrapBlockedFrame = frame;
    }

    private bool _bootstrapped;
    private bool _resourceBridgeReady;
    private bool _criticalGameplayPatchReady;
    private bool _applicationQuitting;
    private bool _shutdownCompleted;
    private int _instanceId;
    private int _generationId;
    private DariusDiagnostics _diagnostics;

    private void Awake()
    {
        // Workshop ModBehaviour instances can exist a frame before Start(). Resolve the loader-provided
        // ModItem.path here so every later icon/model/audio lookup already knows the numeric Workshop root.
        // Configure() is intentionally safe when instance/mod is not populated yet; Start() retries it.
        DariusModEnvironment.Configure(this);
        DariusMedia.RefreshRootFromEnvironment();
        DariusPrototypeIcons.RefreshRootOwnership();
        DariusAudioSettingsRuntime.Bind(skillAudioVolume);
        DariusLog.Initialize();

        _instanceId = GetInstanceID();
        _generationId = unchecked(++_generationSerial);
        if (_activeModInstanceId != 0 && _activeModInstanceId != _instanceId)
        {
            DariusLog.Info("BOOT", "New ModBehaviour generation=" + _generationId +
                " owner=" + _instanceId + " superseded generation=" + _activeGenerationId +
                " owner=" + _activeModInstanceId + "; cleaning the previous runtime generation before bootstrap.");
            DariusRuntimeAudit.LogSnapshot("before replacement cleanup newGeneration=" + _generationId, true);
            BlockBootstrapForCurrentFrame("ModBehaviour replacement cleanup");
            ResetSharedRuntimeForReplacement();
            DariusRuntimeAudit.LogSnapshot("after replacement cleanup newGeneration=" + _generationId, true);
        }
        _activeModInstanceId = _instanceId;
        _activeGenerationId = _generationId;
        DariusLog.Info("BOOT", "Activated ModBehaviour generation=" + _generationId + " owner=" + _instanceId +
            " gameObject=" + gameObject.name + " scene=" + gameObject.scene.name + ".");
        DariusTravelerRegistry.BindOwner(transform, _generationId);
        DariusFormalRegistry.BindOwner(transform, _generationId);

        try
        {
            DariusRuntimeResourceCompatibility.Install(harmony);
            _resourceBridgeReady = true;
        }
        catch (Exception e)
        {
            // Runtime-only Hero/Skin/Skill resources must never enter Dew's maps before the three
            // required lookup endpoints are bridged. Start() gets one clean retry; no registration
            // is allowed from this Awake generation until that succeeds.
            DariusLog.Exception("PATCH", e, "Required runtime resource bridge failed in Awake; early Darius bootstrap is blocked");
            return;
        }

        try
        {
            DariusDirectionalBasicAttackSectorPatch.InstallRequired(harmony);
            _criticalGameplayPatchReady = true;
        }
        catch (Exception e)
        {
            DariusLog.Exception("PATCH-CRITICAL", e,
                "Critical Darius gameplay patch failed in Awake; core registration is blocked until Start retries it");
            return;
        }

        try
        {
            if (!IsBootstrapBlockedThisFrame &&
                DewResources.database != null &&
                !string.IsNullOrEmpty(DariusModEnvironment.Root))
                DariusTravelerRegistry.EnsureCoreRegisteredForBootstrap(
                    "Mod Awake synchronous Workshop bootstrap", _generationId, _instanceId);
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
        if (!IsActiveGeneration(_generationId, _instanceId))
        {
            DariusLog.Info("BOOT-LIFECYCLE", "Ignoring stale ModBehaviour.Start generation=" + _generationId +
                " owner=" + _instanceId + " activeGeneration=" + _activeGenerationId +
                " activeOwner=" + _activeModInstanceId + ".");
            return;
        }

        // Re-read ModItem.path now that the loader has completed the ModBehaviour contract.
        DariusModEnvironment.Configure(this);
        DariusMedia.RefreshRootFromEnvironment();
        DariusPrototypeIcons.RefreshRootOwnership();
        DariusAudioSettingsRuntime.Bind(skillAudioVolume);
        DariusLog.Initialize();
        DariusLog.Info("BOOT", "DariusPrototype version=" + DariusModEnvironment.Version + "; Start() entered. bootstrapped=" + _bootstrapped +
            " modRoot=" + (DariusModEnvironment.Root ?? "<null>") + " source=" + DariusModEnvironment.SourceLabel);
        _diagnostics = gameObject.GetComponent<DariusDiagnostics>();
        if (_diagnostics == null) _diagnostics = gameObject.AddComponent<DariusDiagnostics>();
        _diagnostics.Initialize();
        DariusLog.Info("BOOT", "Initial snapshot: " + DariusDiagnostics.Snapshot());

        if (!_resourceBridgeReady)
        {
            try
            {
                DariusRuntimeResourceCompatibility.Install(harmony);
                _resourceBridgeReady = true;
            }
            catch (Exception e)
            {
                DariusLog.Exception("PATCH", e, "Required runtime resource bridge failed in Start; aborting this Darius runtime generation");
                ShutdownRuntimeResources("required runtime resource bridge unavailable");
                return;
            }
        }

        if (!_criticalGameplayPatchReady)
        {
            try
            {
                DariusDirectionalBasicAttackSectorPatch.InstallRequired(harmony);
                _criticalGameplayPatchReady = true;
            }
            catch (Exception e)
            {
                DariusLog.Exception("PATCH-CRITICAL", e,
                    "Critical directional basic-attack patch is unavailable; aborting this Darius runtime generation");
                ShutdownRuntimeResources("critical gameplay patch unavailable");
                return;
            }
        }

        TravelerBasicAttackVfxReplication.Initialize();
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

            try
            {
                DariusRuntimeAudit.InstallHooks(harmony);
            }
            catch (Exception e)
            {
                DariusLog.Exception("AUDIT-HOOK", e, "Read-only runtime audit hook install failed; continuing boot");
            }

            _bootstrapped = true;
        }

        // If Dew's resource database is already online, synchronously install the minimum Hero/Skin
        // resource bridge before lobby/mastery UI gets another frame to resolve persisted Hero_Darius.
        // If it is not ready yet, InitializeWhenReady below performs the same operation as soon as it is.
        try
        {
            if (!IsBootstrapBlockedThisFrame &&
                DewResources.database != null &&
                !string.IsNullOrEmpty(DariusModEnvironment.Root))
                DariusTravelerRegistry.EnsureCoreRegisteredForBootstrap(
                    "Mod Start synchronous Workshop bootstrap", _generationId, _instanceId);
        }
        catch (Exception e)
        {
            DariusLog.Exception("TRAVELER-EARLY", e, "Synchronous Workshop Hero/Skin bootstrap failed; coroutine fallback remains active");
        }

        // Media uses the same generation/barrier rule as runtime prefab templates. A replacement
        // may have scheduled old Texture/AudioClip destruction this frame; do not create the next
        // generation's media objects until Unity has crossed that frame boundary.
        StartCoroutine(PreloadMediaWhenReady(_generationId, _instanceId));

        // Always start the critical registration coroutine even when an optional Harmony/UI patch
        // failed. This is the authoritative path that creates Hero_Darius/Skin_Darius_Default.
        StartCoroutine(DariusTravelerRegistry.InitializeWhenReady(_generationId, _instanceId));
        DariusLog.Info("BOOT", "Registration coroutine started generation=" + _generationId +
            " owner=" + _instanceId +
            ". Target=independent Hero_Darius, native Hero/Skin/Loadout/Profile/Mirror paths.");
        DariusRuntimeAudit.LogSnapshot("Start registration coroutine scheduled generation=" + _generationId, true);
    }

    private IEnumerator PreloadMediaWhenReady(int generationId, int ownerId)
    {
        while (IsBootstrapBlockedThisFrame || string.IsNullOrEmpty(DariusMedia.Root))
        {
            if (!IsActiveGeneration(generationId, ownerId)) yield break;
            yield return null;
        }
        if (!IsActiveGeneration(generationId, ownerId)) yield break;

        try
        {
            DariusMedia.PreloadAll();
        }
        catch (Exception e)
        {
            DariusLog.Exception("MEDIA", e, "PreloadAll failed generation=" + generationId);
        }

        if (!IsActiveGeneration(generationId, ownerId)) yield break;
        IEnumerator compressed = null;
        try { compressed = DariusMedia.PreloadCompressedAudio(); }
        catch (Exception e)
        {
            DariusLog.Exception("MEDIA", e, "Compressed-audio preload creation failed generation=" + generationId);
        }
        if (compressed != null) yield return compressed;
    }

    private void OnEnable()
    {
        if (_instanceId != 0)
            DariusLog.Info("BOOT-LIFECYCLE", "ModBehaviour OnEnable generation=" + _generationId +
                " owner=" + _instanceId + " scene=" + gameObject.scene.name +
                " activeHierarchy=" + gameObject.activeInHierarchy + ".");
    }

    private void OnDisable()
    {
        if (_instanceId != 0)
            DariusLog.Info("BOOT-LIFECYCLE", "ModBehaviour OnDisable generation=" + _generationId +
                " owner=" + _instanceId + " scene=" + gameObject.scene.name +
                " activeHierarchy=" + gameObject.activeInHierarchy +
                " quitting=" + _applicationQuitting + " shutdownCompleted=" + _shutdownCompleted + ".");
    }

    private void OnApplicationQuit()
    {
        _applicationQuitting = true;
        if (!IsActiveGeneration(_generationId, _instanceId))
        {
            DariusLog.Info("BOOT-LIFECYCLE", "Ignoring application-quit cleanup from superseded generation=" +
                _generationId + " owner=" + _instanceId + ".");
            return;
        }
        ShutdownRuntimeResources("application quit");
    }

    private void OnDestroy()
    {
        if (_diagnostics != null) Destroy(_diagnostics);

        if (_instanceId != 0 && _activeModInstanceId != 0 && _activeModInstanceId != _instanceId)
        {
            DariusLog.Info("BOOT", "Superseded ModBehaviour destroyed generation=" + _generationId +
                " owner=" + _instanceId + " activeGeneration=" + _activeGenerationId +
                " activeOwner=" + _activeModInstanceId + "; shared runtime belongs to the newer instance.");
            return;
        }

        ShutdownRuntimeResources(_applicationQuitting ? "application quit" : "ModBehaviour destroyed");
        if (_activeModInstanceId == _instanceId)
        {
            _activeModInstanceId = 0;
            _activeGenerationId = 0;
        }
    }

    private void ResetSharedRuntimeForReplacement()
    {
        TravelerBasicAttackVfxReplication.Shutdown();
        DariusRInputGuard.Uninstall();
        try { harmony.UnpatchAll(harmony.Id); } catch { }
        DariusRuntimeAudit.ResetHooks();
        DariusRuntimeResourceCompatibility.ResetInstallState();
        DariusDejaVuRegistry.ResetPatchInstallState();
        DariusTravelerRegistry.ShutdownRuntimeResources();
        DariusFormalRegistry.ShutdownRuntimeResources();
        DariusLolVfxRuntime.Unload();
        DariusMedia.Unload();
        DariusPrototypeIcons.Unload();
        _resourceBridgeReady = false;
        _criticalGameplayPatchReady = false;
    }

    private void ShutdownRuntimeResources(string reason)
    {
        if (_shutdownCompleted) return;
        _shutdownCompleted = true;
        BlockBootstrapForCurrentFrame(reason);
        DariusLog.Info("BOOT", "Cleaning Darius runtime resources generation=" + _generationId +
            " owner=" + _instanceId + " reason=" + reason);
        DariusRuntimeAudit.LogSnapshot("before shutdown generation=" + _generationId + " reason=" + reason, true);
        TravelerBasicAttackVfxReplication.Shutdown();
        DariusRInputGuard.Uninstall();
        try { harmony.UnpatchAll(harmony.Id); } catch { }
        DariusRuntimeAudit.ResetHooks();
        DariusRuntimeResourceCompatibility.ResetInstallState();
        DariusDejaVuRegistry.ResetPatchInstallState();
        DariusTravelerRegistry.ShutdownRuntimeResources();
        DariusFormalRegistry.ShutdownRuntimeResources();
        DariusLolVfxRuntime.Unload();
        DariusMedia.Unload();
        DariusPrototypeIcons.Unload();
        if (_applicationQuitting || !Application.isPlaying)
            DariusNativeModelAssets.Unload();
        _resourceBridgeReady = false;
        _criticalGameplayPatchReady = false;
        _bootstrapped = false;
        DariusRuntimeAudit.LogSnapshot("after shutdown generation=" + _generationId + " reason=" + reason, true);
        DariusLog.Flush();
        if (_applicationQuitting || !Application.isPlaying) DariusLog.Shutdown();
    }
}
