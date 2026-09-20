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
}