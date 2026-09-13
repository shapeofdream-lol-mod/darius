using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class DariusConstellationLocalization
{
    private static readonly Dictionary<string, string> Names = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        { DariusConstellationIds.DConqueror, "征服者" },
        { DariusConstellationIds.DTriumph, "凯旋" },
        { DariusConstellationIds.DAlacrity, "传说：欢欣" },
        { DariusConstellationIds.DLastStand, "坚毅不倒" },
        { DariusConstellationIds.DAxiomArcanist, "公理秘术" },
        { DariusConstellationIds.DNoxianArena, "诺克萨斯竞技场" },
        { DariusConstellationIds.LSecondWind, "复苏之风" },
        { DariusConstellationIds.LOvergrowth, "过度生长" },
        { DariusConstellationIds.LRevitalize, "复苏" },
        { DariusConstellationIds.LConditioning, "调节" },
        { DariusConstellationIds.LUnflinching, "坚定" },
        { DariusConstellationIds.ICripplingStrike, "诺克萨斯武库：致残打击" },
        { DariusConstellationIds.IApprehend, "诺克萨斯武库：无情铁手" },
        { DariusConstellationIds.IInstantDecimate, "旋斧即决" },
        { DariusConstellationIds.IWarFervor, "战争热诚" },
        { DariusConstellationIds.ILegacyCripplingStrike, "斩断退路" },
        { DariusConstellationIds.ILegacyApprehend, "铁腕征服" },
        { DariusConstellationIds.ILegacyGuillotine, "断头台的律法" },
        { DariusConstellationIds.DBloodRush, "血路疾行" },
        { DariusConstellationIds.DNoxianMight, "真正的诺克萨斯之力" },
        { DariusConstellationIds.DDunkmaster, "扣篮王" },
        { DariusConstellationIds.LBloodPrice, "血债血偿" },
        { DariusConstellationIds.FHandOfNoxus, "诺克萨斯之手" },
        { DariusConstellationIds.FNimbusCloak, "灵光披风" },
        { DariusConstellationIds.FCelerity, "迅捷" },
        { DariusConstellationIds.FGatheringStorm, "风暴聚集" },
        { DariusConstellationIds.FCosmicInsight, "星界洞悉" }
    };

    private static readonly Dictionary<string, string> Descriptions = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        { DariusConstellationIds.DConqueror, "对敌人造成直接命中会获得1层征服者，持续5秒，最多6层。每层固定提高1.5/2/2.5/3攻击力。" },
        { DariusConstellationIds.DTriumph, "击杀敌人时回复已损失生命值的6%/7%/8%/9%。同一次击杀只会结算一次。" },
        { DariusConstellationIds.DAlacrity, "每6/5/4/3次击杀获得1层欢欣，每层提高4%攻击速度，最多5层，本局持续。" },
        { DariusConstellationIds.DLastStand, "生命值低于40%时，固定提高9/11/13/15攻击力。" },
        { DariusConstellationIds.DAxiomArcanist, "诺克萨斯断头台伤害提高10%/13%/16%/20%，并使诺克萨斯之力持续时间延长1/1.25/1.5/2秒。" },
        { DariusConstellationIds.DNoxianArena, "10米内至少存在1个敌人时进入【诺克萨斯竞技场】：基础固定获得3/4/5/6攻击力与0.5/0.75/1/1.25护甲，并获得8%/10%/12%/15%攻击速度和3%/4%/5%/6%移动速度。附近敌人最多按10个计算，每少1个敌人，全部加成提高10%；存在精英时总效果×1.5，存在Boss时总效果×2（Boss优先），Boss场景最终倍率封顶×3，单独面对Boss时即为×3。" },
        { DariusConstellationIds.LSecondWind, "受到伤害后逐步回复当前已损失生命值的4%/5%/6%/7%，新的受击会刷新可回复量。" },
        { DariusConstellationIds.LOvergrowth, "固定提高25/32/38/45最大生命值。" },
        { DariusConstellationIds.LRevitalize, "大杀四方的治疗量提高12%/16%/20%/24%；生命值低于40%时额外提高6%。" },
        { DariusConstellationIds.LConditioning, "固定提高0.5/0.75/1/1.25护甲。" },
        { DariusConstellationIds.LUnflinching, "生命值低于40%时，移动速度提高8%/10%/12%/14%。" },
        { DariusConstellationIds.ICripplingStrike, "诺克萨斯从不教人给敌人第二次逃跑的机会。开战时，德莱厄斯附近出现1个【致残打击】记忆；记忆等级等于本星座等级。" },
        { DariusConstellationIds.IApprehend, "真正的强者不会等猎物自己走近。开战时，德莱厄斯附近出现1个【无情铁手】记忆；记忆等级等于本星座等级。" },
        { DariusConstellationIds.IInstantDecimate, "斧刃不必蓄势，杀意已经足够。Q【大杀四方】取消0.75秒前摇、蓄力动作与治疗，直接播放旋转攻击并立即结算；伤害提高20%/25%/30%/35%，所有命中均按外圈伤害处理；仅在已拥有【出血】身份时必定叠加1层【出血】。" },
        { DariusConstellationIds.IWarFervor, "战争不在乎鲜血来自谁。只要在5/5.5/6/6.5秒内跨目标累计施加5次【出血】，即可触发【诺克萨斯之力】，获得35%攻击力，并继续享受【出血】记忆每5级带来的成长。" },
        { DariusConstellationIds.ILegacyCripplingStrike, "逃跑只是把死亡拖得更久。W【致残打击】的减速额外延长0.25/0.35/0.45/0.55秒；命中时，目标每有1层【出血】，W剩余冷却额外减少0.5/0.6/0.7/0.8秒。" },
        { DariusConstellationIds.ILegacyApprehend, "被铁腕拖回来的敌人，连铠甲也失去了意义。E【无情铁手】拉中的敌人被标记3秒；期间德莱厄斯的技能与【出血】造成的物理伤害提高10%/13%/16%/20%。" },
        { DariusConstellationIds.ILegacyGuillotine, "断头台只承认力量，不承认侥幸。R【诺克萨斯断头台】伤害额外提高15%/20%/25%/30%，并与【公理秘术】乘算。" },
        { DariusConstellationIds.DBloodRush, "鲜血会替你指出逃兵的方向。每个正在流血的敌人使移动速度提高3%/3.5%/4%/4.5%，最多计算5个目标。" },
        { DariusConstellationIds.DNoxianMight, "君王以世袭的名义让你跪下，而诺克萨斯让你站起来。触发【诺克萨斯之力】时，额外固定获得9/12/15/18攻击力。" },
        { DariusConstellationIds.DDunkmaster, "高高跃起，然后让所有人记住这一斧。用【诺克萨斯断头台】完成击杀后，移动速度提高15%/20%/25%/30%，持续3秒。" },
        { DariusConstellationIds.LBloodPrice, "流下的血总要有人偿还。击杀仍带有【出血】的敌人时，回复最大生命值的2.5%/3.5%/4.5%/5.5%。" },
        { DariusConstellationIds.FHandOfNoxus, "伤口不会轻易闭合，德莱厄斯也不会停止追猎。【出血】持续时间延长1/1.5/2秒。" },
        { DariusConstellationIds.FNimbusCloak, "施放闪现或疾跑后，移动速度提高15%/20%/25%，持续2秒。" },
        { DariusConstellationIds.FCelerity, "移动速度提高5%/6.5%/8%。" },
        { DariusConstellationIds.FGatheringStorm, "每达到5个局内英雄等级获得一层风暴聚集；每层固定提高2.5/3/3.5攻击力。" },
        { DariusConstellationIds.FCosmicInsight, "闪现与疾跑的实际恢复时间缩短20%/25%/30%。" }
    };

    public static bool TryName(string key, out string value)
    {
        value = null;
        string id = Normalize(key);
        if (id != null) return DariusLanguage.IsEnglish
            ? DariusEnglishLocalization.TryStarName(id, out value)
            : Names.TryGetValue(id, out value);
        return DariusEquipmentConstellationLocalization.TryName(key, out value);
    }

    public static bool TryDescription(string key, out string value)
    {
        value = null;
        string id = Normalize(key);
        if (id == null) return DariusEquipmentConstellationLocalization.TryDescription(key, out value);

        // The stock constellation detail widget asks for both a description and a separate lore row.
        // Darius used the same gameplay description for both, producing the duplicated grey line in
        // the lower panel. Keep the gameplay description in its native row and intentionally blank lore.
        if (IsLoreKey(key))
        {
            value = string.Empty;
            return true;
        }
        return DariusLanguage.IsEnglish
            ? DariusEnglishLocalization.TryStarDescription(id, out value)
            : Descriptions.TryGetValue(id, out value);
    }

    private static bool IsLoreKey(string key)
    {
        if (string.IsNullOrEmpty(key)) return false;
        return key.EndsWith(".lore", StringComparison.OrdinalIgnoreCase) ||
               key.EndsWith("_lore", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsDariusStarKey(string key) => Normalize(key) != null || DariusEquipmentConstellationLocalization.IsEquipmentKey(key);

    private static string Normalize(string key)
    {
        if (string.IsNullOrEmpty(key)) return null;
        foreach (string id in Names.Keys)
            if (key.IndexOf(id, StringComparison.OrdinalIgnoreCase) >= 0 || key.IndexOf(id.Replace("Se_", string.Empty), StringComparison.OrdinalIgnoreCase) >= 0)
                return id;
        return null;
    }
}


// Keep the stock constellation browser state coherent when the list is rebuilt after a run,
// category switch, loadout migration, or star purchase. The stock UI keeps a hovered index
// separately from the rebuilt item list; a stale index is enough to make StarDetails repeatedly
// index past the end of the list until the menu is closed.
[HarmonyPatch(typeof(UI_Lobby_Constellations_StarList), "Refresh", new Type[] { })]
public static class DariusConstellationStarListRefreshPatch
{
    private static long _refreshStartedTicks;

    private static readonly MethodInfo GenericStarLookup = typeof(DewResources).GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
        .First(m => m.Name == "GetByType" && m.IsGenericMethodDefinition && m.GetParameters().Length == 2 && m.GetParameters()[0].ParameterType == typeof(Type));

    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        MethodInfo replacement = AccessTools.Method(typeof(DariusConstellationStarListRefreshPatch), nameof(GetStarForBrowser));
        foreach (CodeInstruction instruction in instructions)
        {
            MethodInfo called = instruction.operand as MethodInfo;
            if (called != null && called.IsGenericMethod && called.GetGenericMethodDefinition() == GenericStarLookup &&
                called.GetGenericArguments().Length == 1 && called.GetGenericArguments()[0] == typeof(StarEffect))
            {
                instruction.opcode = OpCodes.Call;
                instruction.operand = replacement;
            }
            yield return instruction;
        }
    }

    private static StarEffect GetStarForBrowser(Type type, ResourceLoadSettings settings)
    {
        if (type == null) return null;
        StarEffect runtimeStar;
        if (DariusRuntimeResourceCompatibility.TryResolveRuntimeStar(type, out runtimeStar)) return runtimeStar;
        return DewResources.GetByType<StarEffect>(type, settings);
    }

    [HarmonyPrefix]
    private static void Prefix(UI_Lobby_Constellations_StarList __instance)
    {
        if (__instance == null) return;
        _refreshStartedTicks = DateTime.UtcNow.Ticks;
        try
        {
            __instance.hoveredIndex = -1;
            if (__instance.listGroup != null) __instance.listGroup.currentIndex = -1;

            // Dew.ClearTypeReferences only resets _allHeroes. If another Mod interrupts the ensuing
            // rebuild, _allHeroes can be populated while _allStarTypes remains empty forever. Repair
            // that impossible partial-cache state once before the stock UI enumerates constellations.
            IReadOnlyList<Type> stars = Dew.allStarTypes;
            if (stars == null || stars.Count == 0)
            {
                Dew.ClearTypeReferences();
                Dew.InitAllTypeReferences();
                DariusConstellationRegistry.ReassertTypeCache();
                DariusLog.Warn("CONSTELLATION-UI", "Recovered an empty Dew star-type cache before constellation refresh.");
            }
        }
        catch (Exception e) { DariusLog.Exception("CONSTELLATION-UI", e, "Could not prepare global constellation list refresh"); }
    }

    [HarmonyPostfix]
    private static void Postfix(UI_Lobby_Constellations_StarList __instance)
    {
        if (__instance == null) return;
        try
        {
            int count = __instance.items != null ? __instance.items.Count : 0;
            double elapsedMs = _refreshStartedTicks > 0
                ? TimeSpan.FromTicks(DateTime.UtcNow.Ticks - _refreshStartedTicks).TotalMilliseconds
                : -1.0;
            if (__instance.hoveredIndex < 0 || __instance.hoveredIndex >= count) __instance.hoveredIndex = -1;
            if (__instance.listGroup != null && (__instance.listGroup.currentIndex < -1 || __instance.listGroup.currentIndex >= count))
                __instance.listGroup.currentIndex = -1;
            // Do not overwrite the shared constellation portrait/background widgets. The lobby reuses
            // them across travelers and does not reliably restore the previous Sprite, which can visually
            // contaminate stock heroes after visiting a custom traveler. Star icons remain isolated below.
            DariusLog.DebugInfo("CONSTELLATION-UI", "Global star list refresh count=" + count +
                " elapsedMs=" + elapsedMs.ToString("0.0"));
        }
        catch (Exception e)
        {
            DariusLog.Exception("CONSTELLATION-UI", e, "Could not normalize Darius constellation star-list selection");
        }
    }
}

// The stock property getters only check for -1; stale positive indices survive a list rebuild and
// then throw from every input poll. Return null for any index outside the current item list.
[HarmonyPatch(typeof(UI_Lobby_Constellations_StarList), "get_selectedStar")]
public static class DariusConstellationSelectedStarBoundsPatch
{
    [HarmonyPrefix]
    private static bool Prefix(UI_Lobby_Constellations_StarList __instance, ref StarEffect __result)
    {
        if (__instance != null && __instance.items != null && __instance.selectedIndex >= 0 && __instance.selectedIndex < __instance.items.Count)
            return true;
        __result = null;
        return false;
    }
}

[HarmonyPatch(typeof(UI_Lobby_Constellations_StarList), "get_hoveredStar")]
public static class DariusConstellationHoveredStarBoundsPatch
{
    [HarmonyPrefix]
    private static bool Prefix(UI_Lobby_Constellations_StarList __instance, ref StarEffect __result)
    {
        if (__instance != null && __instance.items != null && __instance.hoveredIndex >= 0 && __instance.hoveredIndex < __instance.items.Count)
            return true;
        __result = null;
        return false;
    }
}


// Preserve Riot's full-colour rune/ability artwork inside the stock star browser. The stock
// constellation UI normally treats star icons as monochrome masks and applies the branch colour;
// opaque League rune backgrounds therefore collapse into featureless red/green circles. For
// Darius-owned stars we keep the stock lock/dim behaviour, but neutralize branch tint on visible
// icon images and force the runtime Riot Sprite after both Setup and Refresh.
[HarmonyPatch(typeof(UI_Lobby_Constellations_StarItem), "Setup", new Type[] { typeof(StarEffect), typeof(int) })]
public static class DariusConstellationStarItemSetupPresentationPatch
{
    [HarmonyPostfix]
    private static void Postfix(UI_Lobby_Constellations_StarItem __instance, StarEffect __0)
    {
        DariusConstellationItemPresentation.RememberAndApply(__instance, __0);
    }
}

public static class DariusConstellationItemPresentation
{
    private static readonly Dictionary<int, string> ItemIconKeys = new Dictionary<int, string>();

    public static void RememberAndApply(UI_Lobby_Constellations_StarItem item, StarEffect star)
    {
        if (item == null) return;
        int itemId = item.GetInstanceID();
        // StarItem objects are pooled and reused across travelers. Always forget the previous
        // mod icon before inspecting the newly assigned StarEffect, otherwise Refresh can repaint
        // another traveler's constellation with a stale League icon.
        ItemIconKeys.Remove(itemId);
        if (star == null) return;
        string key;
        if (!DariusConstellationRegistry.TryGetIconKey(star, out key)) return;
        ItemIconKeys[itemId] = key;
        Apply(item, key);
    }

    public static void ApplyRemembered(UI_Lobby_Constellations_StarItem item)
    {
        if (item == null) return;
        string key;
        if (ItemIconKeys.TryGetValue(item.GetInstanceID(), out key)) Apply(item, key);
    }

    public static bool HasRememberedItems(UI_Lobby_Constellations_StarList list)
    {
        if (list == null || list.items == null) return false;
        for (int i = 0; i < list.items.Count; i++)
        {
            UI_Lobby_Constellations_StarItem item = list.items[i];
            if (item != null && ItemIconKeys.ContainsKey(item.GetInstanceID())) return true;
        }
        return false;
    }

    private static void Apply(UI_Lobby_Constellations_StarItem item, string iconKey)
    {
        Sprite desired = DariusPrototypeIcons.Get(iconKey);
        if (desired == null) return;
        try
        {
            UI_StarIcon icon = item.GetComponentInChildren<UI_StarIcon>(true);
            if (icon == null) return;
            ApplyImage(icon.iconFillMask, desired);
            ApplyImage(icon.iconFill, desired);
            ApplyImage(icon.iconBg, desired);
        }
        catch { }
    }

    private static void ApplyImage(UnityEngine.UI.Image image, Sprite desired)
    {
        if (image == null) return;
        image.sprite = desired;
        Color color = image.color;
        if (Mathf.Max(color.r, Mathf.Max(color.g, color.b)) > 0.22f)
            image.color = new Color(1f, 1f, 1f, color.a);
    }
}

public static class DariusLobbyConstellationPresentation
{
    public static void ApplyHeroPortrait(Component origin)
    {
        if (origin == null) return;
        try
        {
            if (DewPlayer.local == null || !string.Equals(DewPlayer.local.selectedHeroType, DariusTravelerRegistry.HeroName, StringComparison.Ordinal)) return;
            Sprite portrait = DariusPrototypeIcons.Get("HERO");
            if (portrait == null) return;

            int applied = 0;
            Transform cursor = origin.transform;
            Transform highest = cursor;
            for (int i = 0; cursor != null && i < 10; i++, cursor = cursor.parent) highest = cursor;
            Component[] components = highest != null ? highest.GetComponentsInChildren<Component>(true) : origin.GetComponentsInChildren<Component>(true);
            foreach (Component component in components)
            {
                if (component == null || component.gameObject == null) continue;
                string objectName = component.gameObject.name.ToLowerInvariant();
                string typeName = component.GetType().Name.ToLowerInvariant();
                bool namedPortrait = objectName.Contains("hero") || objectName.Contains("portrait") || objectName.Contains("traveler") ||
                                    objectName.Contains("character") || objectName.Contains("profile") ||
                                    typeName.Contains("heroicon") || typeName.Contains("portrait");
                if (!namedPortrait || objectName.Contains("skill") || objectName.Contains("memory") || objectName.Contains("star") || objectName.Contains("lock")) continue;
                if (TrySetSpriteAndWhite(component, portrait)) applied++;
            }

            // Serialized controller references are more reliable than GameObject names on some UI prefabs.
            cursor = origin.transform;
            for (int i = 0; cursor != null && i < 10; i++, cursor = cursor.parent)
            {
                foreach (Component controller in cursor.GetComponents<Component>())
                {
                    if (controller == null) continue;
                    string tn = controller.GetType().Name;
                    if (tn.IndexOf("Constellation", StringComparison.OrdinalIgnoreCase) < 0 &&
                        tn.IndexOf("Hero", StringComparison.OrdinalIgnoreCase) < 0) continue;
                    applied += ApplyControllerPortraitReferences(controller, portrait);
                }
            }

            // Some 1.3.x constellation prefabs expose the large traveler portrait as one or more
            // generically named square Images instead of a serialized heroIcon field. Always sweep
            // the screen for the largest portrait-like square targets, because named/controller paths
            // may fix one preview widget yet still leave the top-left profile square on the stock fog.
            applied += ApplyLargeSquarePortraitCandidates(components, portrait);

            DariusLog.DebugInfoThrottled("HERO-PORTRAIT", "constellation-root",
                "Rebound Darius champion portrait in constellation UI targets=" + applied, 0.75);
        }
        catch (Exception e)
        {
            DariusLog.Exception("HERO-PORTRAIT", e, "Constellation hero portrait repair failed");
        }
    }

    private static int ApplyLargeSquarePortraitCandidates(Component[] components, Sprite portrait)
    {
        if (components == null || portrait == null) return 0;
        List<KeyValuePair<Component, float>> candidates = new List<KeyValuePair<Component, float>>();
        foreach (Component component in components)
        {
            if (component == null || component.gameObject == null) continue;
            string n = component.gameObject.name.ToLowerInvariant();
            if (n.Contains("background") || n.Contains("panel") || n.Contains("frame") || n.Contains("star") ||
                n.Contains("skill") || n.Contains("memory") || n.Contains("lock") || n.Contains("tab") ||
                n.Contains("branch") || n.Contains("line")) continue;
            PropertyInfo sp = component.GetType().GetProperty("sprite", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            PropertyInfo tp = component.GetType().GetProperty("texture", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            bool spriteTarget = sp != null && sp.CanWrite && sp.PropertyType == typeof(Sprite);
            bool textureTarget = tp != null && tp.CanWrite && typeof(Texture).IsAssignableFrom(tp.PropertyType);
            if (!spriteTarget && !textureTarget) continue;
            RectTransform rect = component.transform as RectTransform;
            if (rect == null) continue;
            float width = Mathf.Abs(rect.rect.width);
            float height = Mathf.Abs(rect.rect.height);
            if (width < 96f || height < 96f) continue;
            float ratio = height > 0.01f ? width / height : 0f;
            if (ratio < 0.72f || ratio > 1.38f) continue;
            float area = width * height;
            if (area < 9000f || area > 180000f) continue;
            candidates.Add(new KeyValuePair<Component, float>(component, area));
        }

        candidates.Sort((a, b) => b.Value.CompareTo(a.Value));
        int applied = 0;
        int limit = Mathf.Min(3, candidates.Count);
        for (int i = 0; i < limit; i++)
        {
            KeyValuePair<Component, float> kv = candidates[i];
            if (TrySetSpriteAndWhite(kv.Key, portrait))
            {
                applied++;
                DariusLog.DebugInfoThrottled("HERO-PORTRAIT", "square-target:" + kv.Key.GetInstanceID(),
                    "Applied Darius portrait to square constellation image name=" + kv.Key.gameObject.name +
                    " area=" + kv.Value.ToString("0"), 2.0);
            }
        }
        return applied;
    }

    private static int ApplyControllerPortraitReferences(Component controller, Sprite portrait)
    {
        int applied = 0;
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        foreach (FieldInfo field in controller.GetType().GetFields(flags))
        {
            string n = field.Name.ToLowerInvariant();
            if (!n.Contains("hero") && !n.Contains("portrait") && !n.Contains("traveler") && !n.Contains("character")) continue;
            object value = null;
            try { value = field.GetValue(controller); } catch { }
            Component component = value as Component;
            if (component != null && TrySetSpriteAndWhite(component, portrait)) applied++;
        }
        foreach (PropertyInfo property in controller.GetType().GetProperties(flags))
        {
            if (!property.CanRead || property.GetIndexParameters().Length != 0) continue;
            string n = property.Name.ToLowerInvariant();
            if (!n.Contains("hero") && !n.Contains("portrait") && !n.Contains("traveler") && !n.Contains("character")) continue;
            object value = null;
            try { value = property.GetValue(controller, null); } catch { }
            Component component = value as Component;
            if (component != null && TrySetSpriteAndWhite(component, portrait)) applied++;
        }
        return applied;
    }

    private static bool TrySetSpriteAndWhite(Component component, Sprite sprite)
    {
        if (component == null || sprite == null) return false;
        try
        {
            bool applied = false;
            PropertyInfo p = component.GetType().GetProperty("sprite", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (p != null && p.CanWrite && p.PropertyType == typeof(Sprite))
            {
                p.SetValue(component, sprite, null);
                applied = true;
            }
            PropertyInfo tp = component.GetType().GetProperty("texture", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (tp != null && tp.CanWrite && typeof(Texture).IsAssignableFrom(tp.PropertyType) && tp.PropertyType.IsAssignableFrom(sprite.texture.GetType()))
            {
                tp.SetValue(component, sprite.texture, null);
                applied = true;
            }
            if (!applied) return false;

            PropertyInfo cp = component.GetType().GetProperty("color", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (cp != null && cp.CanWrite && cp.PropertyType == typeof(Color))
            {
                Color old = Color.white;
                try { if (cp.CanRead) old = (Color)cp.GetValue(component, null); } catch { }
                cp.SetValue(component, new Color(1f, 1f, 1f, old.a), null);
            }
            return true;
        }
        catch { return false; }
    }
}


// Defensive release guard for stock constellation UI. Runtime-injected stars are hydrated from a
// native StarEffect contract above; if a game build still indexes a stock presentation array beyond
// its bounds, contain that UI-only exception for Hero_Darius instead of leaving the constellation
// menu in a permanently corrupted hover/detail state.
[HarmonyPatch(typeof(UI_Lobby_Constellations_StarItem), "Refresh", new Type[] { })]
public static class DariusConstellationStarItemRefreshGuard
{
    [HarmonyPostfix]
    private static void Postfix(UI_Lobby_Constellations_StarItem __instance)
    {
        DariusConstellationItemPresentation.ApplyRemembered(__instance);
    }

    [HarmonyFinalizer]
    private static Exception Finalizer(Exception __exception)
    {
        if (__exception == null) return null;
        try
        {
            bool darius = DewPlayer.local != null &&
                string.Equals(DewPlayer.local.selectedHeroType, DariusTravelerRegistry.HeroName, StringComparison.Ordinal);
            if (darius && (__exception is IndexOutOfRangeException || __exception is ArgumentOutOfRangeException))
            {
                DariusLog.Warn("CONSTELLATION-UI", "Contained stock StarItem.Refresh bounds exception for Hero_Darius: " + __exception.GetType().Name);
                return null;
            }
        }
        catch { }
        return __exception;
    }
}
