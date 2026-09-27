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
public static partial class DariusFormalRegistry
{
    private static readonly Dictionary<string, UnityEngine.Object> ResourcesByGuid = new Dictionary<string, UnityEngine.Object>();

    private static readonly Dictionary<UnityEngine.Object, string> GuidByObject = new Dictionary<UnityEngine.Object, string>();

    private static readonly Dictionary<uint, GameObject> NetworkPrefabs = new Dictionary<uint, GameObject>();

    private sealed class RuntimeRegistration
    {
        public readonly Type type;
        public readonly string name;
        public readonly string guid;
        public readonly uint assetId;
        public bool networkHandlerRegistered;

        public RuntimeRegistration(Type type, string name, string guid, uint assetId)
        {
            this.type = type;
            this.name = name;
            this.guid = guid;
            this.assetId = assetId;
        }
    }

    private static readonly Dictionary<string, RuntimeRegistration> RegistrationsByGuid =
        new Dictionary<string, RuntimeRegistration>(StringComparer.Ordinal);

    private static readonly List<GameObject> OwnedPrefabs = new List<GameObject>();

    private static GameObject _runtimeActorRoot;
    private static Transform _modOwner;
    private static int _modOwnerInstanceId;
    private static int _modOwnerGenerationId;
    private static string _registeredModRoot;

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

    public static Se_Darius_NoxianMight NoxianMightStatus { get; private set; }

    public static Se_Darius_Hemorrhage HemorrhageStatus { get; private set; }

    public static Ai_Darius_Decimate DecimateAbility { get; private set; }

    public static Ai_Darius_CripplingStrike CripplingStrikeAbility { get; private set; }

    public static Ai_Darius_Apprehend ApprehendAbility { get; private set; }

    public static Ai_Darius_NoxianGuillotine NoxianGuillotineAbility { get; private set; }

    // Stable GUIDs: keeping them deterministic is important for resource/network identity.
    // The values live in DariusResourceIds so the formal registry and the Deja Vu injection
    // path can never drift apart.
    private const string GuidQ = DariusResourceIds.Q;

    private const string GuidW = DariusResourceIds.W;

    private const string GuidE = DariusResourceIds.E;

    private const string GuidR = DariusResourceIds.R;

    private const string GuidFlash = DariusResourceIds.Flash;

    private const string GuidGhost = DariusResourceIds.Ghost;

    private const string GuidIdentity = DariusResourceIds.Identity;

    private const string GuidGem = DariusResourceIds.LegacyGem; // legacy v0.11 Essence only
    private const string GuidNoxianMightStatus = DariusResourceIds.StatusNoxianMight;
    private const string GuidHemorrhageStatus = DariusResourceIds.StatusHemorrhage;
    private const string GuidAiQ = DariusResourceIds.AbilityQ;

    private const string GuidAiW = DariusResourceIds.AbilityW;

    private const string GuidAiE = DariusResourceIds.AbilityE;

    private const string GuidAiR = DariusResourceIds.AbilityR;

    public static void BindOwner(Transform owner, int generationId, int ownerId)
    {
        if (owner == null) throw new ArgumentNullException(nameof(owner));
        if (generationId <= 0) throw new ArgumentOutOfRangeException(nameof(generationId));
        if (ownerId == 0) throw new ArgumentOutOfRangeException(nameof(ownerId));
        if (_modOwnerInstanceId == ownerId && _modOwnerGenerationId == generationId)
        {
            _modOwner = owner;
            return;
        }

        if (_runtimeActorRoot != null || _registered || OwnedPrefabs.Count > 0)
            throw new InvalidOperationException("Formal runtime generation must be cleaned before binding a new ModBehaviour owner.");

        _modOwner = owner;
        _modOwnerInstanceId = ownerId;
        _modOwnerGenerationId = generationId;
        DariusLog.Info("REG-LIFECYCLE", "Bound Formal runtime resource ownership generation=" + generationId +
            " modBehaviourOwner=" + ownerId +
            " gameObjectOwner=" + owner.gameObject.GetInstanceID() + ".");
    }

    internal static bool IsOwnedByActiveGeneration
    {
        get
        {
            return _modOwner != null &&
                   _modOwnerGenerationId > 0 &&
                   _modOwnerInstanceId != 0 &&
                   DariusPrototypeMod.IsActiveGeneration(_modOwnerGenerationId, _modOwnerInstanceId);
        }
    }

    public static void ShutdownRuntimeResources()
    {
        Unregister();
        _modOwner = null;
        _modOwnerInstanceId = 0;
        _modOwnerGenerationId = 0;
        _registeredModRoot = null;
    }

    public static IEnumerator InitializeAndDropWhenReady()
    {
        // Capture authority at call time. Iterator bodies do not execute until MoveNext(), so the
        // actual yielding routine must receive immutable owner/generation values explicitly.
        int generationId = _modOwnerGenerationId;
        int ownerId = _modOwnerInstanceId;
        return InitializeAndDropWhenReadyOwned(generationId, ownerId);
    }

    private static IEnumerator InitializeAndDropWhenReadyOwned(int generationId, int ownerId)
    {
        DariusLog.Info("REG", "InitializeAndDropWhenReady started generation=" + generationId +
            " owner=" + ownerId + "; waiting for DewResources.database + Mod root.");
        while (DewResources.database == null ||
               string.IsNullOrEmpty(DariusModEnvironment.Root) ||
               DariusPrototypeMod.IsBootstrapBlockedThisFrame)
        {
            if (!DariusPrototypeMod.IsActiveGeneration(generationId, ownerId) ||
                _modOwnerGenerationId != generationId || _modOwnerInstanceId != ownerId)
                yield break;
            yield return null;
        }

        if (!DariusPrototypeMod.IsActiveGeneration(generationId, ownerId) ||
            _modOwnerGenerationId != generationId || _modOwnerInstanceId != ownerId)
            yield break;

        DariusLog.Info("REG", "DewResources.database ready; registering formal resources for captured generation=" +
            generationId + " owner=" + ownerId + ".");
        Register();
        if (!IsRegistrationHealthyForBootstrap()) yield break;

        DariusDejaVuRegistry.RegisterAll();
        DariusLog.Info("DEJAVU", "Darius memory resources registered in the native Deja Vu/content pipeline.");
    }
}