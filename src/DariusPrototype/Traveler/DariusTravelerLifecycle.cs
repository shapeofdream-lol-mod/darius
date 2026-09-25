using System;
using System.Collections;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class DariusTravelerLifecycleBridge : MonoBehaviour
{
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
        if (!string.IsNullOrEmpty(scene.name) && scene.name.StartsWith("Room_", StringComparison.OrdinalIgnoreCase))
            StartCoroutine(HardResetUltimateAcrossRoomLoad(scene.name + "#" + scene.handle));
    }

    private void OnActiveSceneChanged(Scene oldScene, Scene newScene)
    {
        if (!string.IsNullOrEmpty(newScene.name) && newScene.name.StartsWith("Room_", StringComparison.OrdinalIgnoreCase))
            StartCoroutine(HardResetUltimateAcrossRoomLoad(newScene.name + "#" + newScene.handle));
    }

    private IEnumerator HardResetUltimateAcrossRoomLoad(string roomToken)
    {
        // This is gameplay state, not resource repair. The live SkillTrigger can appear a few
        // frames after a Room_* scene load, so probe a few short beats; each trigger de-duplicates
        // the room token itself.
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
}

[HarmonyPatch]
public static class DariusTravelerLocalizationPatch
{
    private static bool TryResolveHeroText(string key, out string value)
    {
        value = null;
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
