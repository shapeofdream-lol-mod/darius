public sealed class DariusTravelerLifecycleBridge : MonoBehaviour
{
    private Coroutine _repairRoutine;

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        SceneManager.activeSceneChanged += OnActiveSceneChanged;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.activeSceneChanged -= OnActiveSceneChanged;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        QueueRepair("sceneLoaded:" + scene.name + "/" + mode);
        if (!string.IsNullOrEmpty(scene.name) && scene.name.StartsWith("Room_", StringComparison.OrdinalIgnoreCase))
            StartCoroutine(HardResetUltimateAcrossRoomLoad(scene.name + "#" + scene.handle));
    }

    private IEnumerator HardResetUltimateAcrossRoomLoad(string roomToken)
    {
        // Room transitions can rebuild the live SkillTrigger a few frames after sceneLoaded. Probe
        // several short beats; each specific trigger instance self-deduplicates this room token.
        int[] beats = { 0, 1, 3, 7, 15 };
        int frame = 0;
        int beatIndex = 0;
        int total = 0;
        while (beatIndex < beats.Length)
        {
            if (frame >= beats[beatIndex])
            {
                total += St_Darius_NoxianGuillotine.HardResetAllLiveForNewRoom(roomToken);
                beatIndex++;
            }
            frame++;
            yield return null;
        }
        DariusLog.Info("R-ROOM-RESET", "Room-load reset sweep complete token=" + roomToken + " resetCalls=" + total);
    }

    private void OnActiveSceneChanged(Scene oldScene, Scene newScene)
    {
        QueueRepair("activeSceneChanged:" + oldScene.name + "->" + newScene.name);
        if (!string.IsNullOrEmpty(newScene.name) && newScene.name.StartsWith("Room_", StringComparison.OrdinalIgnoreCase))
            StartCoroutine(HardResetUltimateAcrossRoomLoad(newScene.name + "#" + newScene.handle));
    }

    private void QueueRepair(string reason)
    {
        if (_repairRoutine != null) StopCoroutine(_repairRoutine);
        _repairRoutine = StartCoroutine(RepairAcrossRebuildFrames(reason));
    }

    private IEnumerator RepairAcrossRebuildFrames(string reason)
    {
        // SoD rebuilds Dew type/content/profile caches over several frames while leaving/entering
        // a run. Reassert the same persistent runtime resource contract after those rebuild beats.
        int[] beats = { 0, 1, 3, 7, 15 };
        int frame = 0;
        int beatIndex = 0;
        while (beatIndex < beats.Length)
        {
            if (frame >= beats[beatIndex])
            {
                DariusTravelerRegistry.RepairRuntimeRegistration(reason + "/frame" + frame);
                beatIndex++;
            }
            frame++;
            yield return null;
        }
        _repairRoutine = null;
    }
}

[HarmonyPatch]
public static class DariusTravelerLifecyclePatches
{
    [HarmonyPostfix]
    [HarmonyPatch(typeof(DewProfile), nameof(DewProfile.Validate))]
    private static void ProfileValidatePostfix()
    {
        DariusTravelerRegistry.RepairRuntimeRegistration("DewProfile.Validate");
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(DewGameContentSettings), nameof(DewGameContentSettings.Init))]
    private static void ContentInitPrefix(DewGameContentSettings __instance)
    {
        DariusTravelerRegistry.RepairRuntimeRegistration("DewGameContentSettings.Init prefix");
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(DewGameContentSettings), nameof(DewGameContentSettings.Init))]
    private static void ContentInitPostfix(DewGameContentSettings __instance)
    {
        if (__instance == (DewBuildProfile.current != null ? DewBuildProfile.current.content : null))
            DariusTravelerRegistry.RepairRuntimeRegistration("DewGameContentSettings.Init");
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(DewProfileStats), nameof(DewProfileStats.Validate))]
    private static void ProfileStatsValidatePostfix()
    {
        DariusTravelerRegistry.RepairRuntimeRegistration("DewProfileStats.Validate");
    }
}

[HarmonyPatch]
public static class DariusTravelerLocalizationPatch
{
    private static bool TryResolveHeroText(string key, out string value)
    {
        value = null;
        // Ahri baseline: Japanese localization wins before the English/Chinese fallback.
        if (DariusLanguage.IsJapanese &&
            DariusJapaneseLocalization.TryGet(key, key != null && key.IndexOf("Description", StringComparison.OrdinalIgnoreCase) >= 0, out value))
            return true;
        if (string.Equals(key, "Hero_Darius_Name", StringComparison.Ordinal)) value = DariusLanguage.IsEnglish ? "The Hand of Noxus" : "诺克萨斯之手";
        else if (string.Equals(key, "Hero_Darius_Subtitle", StringComparison.Ordinal)) value = DariusLanguage.IsEnglish ? "Darius" : "德莱厄斯";
        else if (string.Equals(key, "Hero_Darius_Description", StringComparison.Ordinal)) value = DariusLanguage.IsEnglish ? "The embodiment of Noxian strength. Crush enemies with a great axe, Hemorrhage, and the guillotine." : "诺克萨斯力量的象征。以巨斧、出血与断头台压垮敌人。";
        return value != null;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(DewLocalization), nameof(DewLocalization.GetUIValue), new Type[] { typeof(string) })]
    private static void GetUIValuePostfix(string __0, ref string __result)
    {
        string value;
        if (TryResolveHeroText(__0, out value)) __result = value;
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(DewLocalization), nameof(DewLocalization.TryGetUIValue))]
    private static void TryGetUIValuePostfix(string __0, ref string __1, ref bool __result)
    {
        string custom;
        if (!TryResolveHeroText(__0, out custom)) return;
        __1 = custom;
        __result = true;
    }
}
