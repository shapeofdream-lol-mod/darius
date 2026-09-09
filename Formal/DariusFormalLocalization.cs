using System;
using System.Collections.Generic;
using DewInternal;

// Darius localization deliberately returns fresh tooltip text on every localization request.
// The direct-execution abilities do not use the game's serialized ScalingValue fields, so this
// class mirrors their runtime Memory-level math and can render the same current -> next preview
// the native Memory UI expects when DescriptionSettings asks for level scaling.
public static class DariusFormalLocalization
{
    private const string ValueColor = "yellow";
    private const string HealColor = "#6fdc8c";
    private const string KeywordColor = "#e38b7b";
    private const string PreviousValueColor = "#a9b0bb";

    public static bool TrySkillName(string key, out string value)
    {
        value = null;
        if (string.IsNullOrEmpty(key)) return false;
        if (key.Contains("Darius_Decimate")) value = DariusLanguage.IsEnglish ? "Decimate" : "大杀四方";
        else if (key.Contains("Darius_CripplingStrike")) value = DariusLanguage.IsEnglish ? "Crippling Strike" : "致残打击";
        else if (key.Contains("Darius_Apprehend")) value = DariusLanguage.IsEnglish ? "Apprehend" : "无情铁手";
        else if (key.Contains("Darius_NoxianGuillotine")) value = DariusLanguage.IsEnglish ? "Noxian Guillotine" : "诺克萨斯断头台";
        else if (key.Contains("Darius_Flash")) value = DariusLanguage.IsEnglish ? "Flash" : "闪现";
        else if (key.Contains("Darius_Ghost")) value = DariusLanguage.IsEnglish ? "Ghost" : "疾跑";
        return value != null;
    }

    // Fallback used by direct GetSkillDescription/GetGemDescription string calls that do not
    // provide DescriptionSettings. Level-aware conversion is handled by TryLevelAwareDescription.
    public static bool TrySkillDescription(string key, out string value)
    {
        SkillTrigger registered = ResolveRegisteredSkill(key);
        int level = registered != null ? DariusMemoryScaling.GetLevel(registered) : 1;
        return TryBuildDescription(key, registered, level, null, false, null, out value);
    }

    public static bool TryLevelAwareDescription(SkillTrigger skill, int level, int? previousLevel, bool showComparison, out string value)
    {
        value = null;
        if (skill == null) return false;
        string key = skill.GetType().Name;
        if (!IsDariusMemoryKey(key)) key = skill.name;
        return TryBuildDescription(key, skill, level, previousLevel, showComparison, null, out value);
    }

    public static bool TryLevelAwareDescription(string key, SkillTrigger skill, int level, int? previousLevel, bool showComparison, out string value)
    {
        return TryBuildDescription(key, skill, level, previousLevel, showComparison, null, out value);
    }

    // Native tooltip path: use DescriptionSettings' live Hero when available so the displayed
    // AD/max-HP numbers track the same entity the stock expression renderer is evaluating.
    public static bool TryLevelAwareDescription(string key, SkillTrigger skill, int level, int? previousLevel, bool showComparison, Hero contextHero, out string value)
    {
        return TryBuildDescription(key, skill, level, previousLevel, showComparison, contextHero, out value);
    }

    private static bool TryBuildDescription(string key, SkillTrigger skill, int level, int? previousLevel, bool showComparison, Hero contextHero, out string value)
    {
        value = null;
        if (string.IsNullOrEmpty(key)) return false;
        level = DariusMemoryScaling.NormalizeLevel(level);
        if (previousLevel.HasValue) previousLevel = DariusMemoryScaling.NormalizeLevel(previousLevel.Value);
        bool compare = showComparison && previousLevel.HasValue && previousLevel.Value != level;

        Hero hero = contextHero;
        try { if (hero == null) hero = DewPlayer.local != null ? DewPlayer.local.hero : null; } catch { }
        float ad = 0f;
        float maxHealth = 0f;
        bool live = false;
        try
        {
            if (hero != null && hero.Status != null)
            {
                ad = hero.Status.finalStats.attackDamage;
                maxHealth = hero.Status.maxHealth;
                live = true;
            }
        }
        catch { }

        string prefix = compare
            ? "<color=" + PreviousValueColor + ">Lv." + previousLevel.Value + "</color> → <color=" + ValueColor + ">Lv." + level + "</color>　"
            : string.Empty;

        if (key.Contains("Darius_Decimate"))
        {
            bool instant = DariusConstellationRuntime.IsInstantQ(hero);
            float qMultiplier = DariusConstellationRuntime.GetInstantQDamageMultiplier(hero);
            float healMultiplier = DariusConstellationRuntime.GetQHealMultiplier(hero);
            string inner = DamageByRatio(ad, lvl => Ai_Darius_Decimate.DamageRatioAtLevel(Ai_Darius_Decimate.InnerAdRatio, lvl) * qMultiplier, live, level, previousLevel, compare);
            string outer = DamageByRatio(ad, lvl => Ai_Darius_Decimate.DamageRatioAtLevel(Ai_Darius_Decimate.OuterNoEssenceAdRatio, lvl) * qMultiplier, live, level, previousLevel, compare);
            string outerHemorrhage = DamageByRatio(ad, lvl => Ai_Darius_Decimate.DamageRatioAtLevel(Ai_Darius_Decimate.InnerAdRatio, lvl) * qMultiplier, live, level, previousLevel, compare);
            string healOne = HealByRatio(maxHealth, lvl => Ai_Darius_Decimate.HealPerTargetAtLevel(lvl) * healMultiplier, maxHealth > 0f, level, previousLevel, compare);
            string opener = instant
                ? (DariusLanguage.IsEnglish ? "Immediately swing the axe around Darius. " : "立即挥斧，对周围敌人造成伤害。")
                : (DariusLanguage.IsEnglish ? "After a " + V("0.75-second") + " windup, swing the axe around Darius. " : "蓄力" + V("0.75秒") + "后挥斧，对周围敌人造成伤害。");
            value = DariusLanguage.IsEnglish
                ? prefix + opener + "The inner " + V("2.40 m") + " deals " + inner + " physical damage; the outer blade deals " + outer + " physical damage. " +
                  (instant ? string.Empty : "Each enemy hit by the outer blade restores " + healOne + ". ") +
                  "With " + K("Hemorrhage") + ", outer-blade damage becomes " + outerHemorrhage + " and applies " + V("1") + " " + K("Hemorrhage stack") + "."
                : prefix + opener + V("2.40米") + "内圈造成" + inner + "物理伤害；斧刃外圈造成" + outer + "物理伤害，" +
                  (instant ? string.Empty : "每命中一名敌人回复" + healOne + "最大生命值。") +
                  "装备" + K("【出血】") + "后，外圈伤害变为" + outerHemorrhage + "，并施加" + V("1层") + K("【出血】") + "。";
            return true;
        }
        if (key.Contains("Darius_CripplingStrike"))
        {
            string total = TotalAttackDamageByBonusRatio(ad, DariusCripplingStrikeRuntime.BonusAdRatioAtLevel, live, level, previousLevel, compare);
            string slowDuration = SecondsByValue(DariusCripplingStrikeRuntime.SlowDurationAtLevel, level, previousLevel, compare);
            value = DariusLanguage.IsEnglish
                ? prefix + "Your next basic attack within " + V("4 seconds") + " deals " + total + " physical damage and slows by " + V("90%") + " for " + slowDuration + "."
                : prefix + V("4秒") + "内的下一次普攻造成" + total + "物理伤害，并使目标减速" + V("90%") + "，持续" + slowDuration + "。";
            return true;
        }
        if (key.Contains("Darius_Apprehend"))
        {
            string range = CompareValue(lvl => Ai_Darius_Apprehend.RangeAtLevel(lvl).ToString("0.##") + (DariusLanguage.IsEnglish ? " m" : "米"), level, previousLevel, compare, ValueColor);
            string slowDuration = SecondsByValue(Ai_Darius_Apprehend.SlowDurationAtLevel, level, previousLevel, compare);
            value = DariusLanguage.IsEnglish
                ? prefix + "Pull enemies in a " + V("60°") + " cone up to " + range + " in front of Darius, then slow them by " + V("40%") + " for " + slowDuration + "."
                : prefix + "将前方" + range + "、" + V("60°") + "范围内的敌人拉至身前，并使其减速" + V("40%") + "，持续" + slowDuration + "。";
            return true;
        }
        if (key.Contains("Darius_NoxianGuillotine"))
        {
            string noBleed = DamageByRatio(ad, lvl => Ai_Darius_NoxianGuillotine.DamageRatioAtLevel(false, 0, lvl), live, level, previousLevel, compare);
            string zero = DamageByRatio(ad, lvl => Ai_Darius_NoxianGuillotine.DamageRatioAtLevel(true, 0, lvl), live, level, previousLevel, compare);
            value = DariusLanguage.IsEnglish
                ? prefix + "Leap to an enemy within " + V("6 m") + " and deal " + K("pure damage") + ". Without " + K("Hemorrhage") + ", deal " + noBleed +
                  "; with it, deal " + zero + ", increased by " + V("25%") + " per " + K("Hemorrhage stack") + ". A kill immediately resets the cooldown and grants " + K("Noxian Might") + "."
                : prefix + "跃向" + V("6米") + "内的敌人并造成" + K("纯粹伤害") + "。未装备" + K("【出血】") + "时造成" + noBleed +
                  "；装备后造成" + zero + "，每层" + K("【出血】") + "使伤害提高" + V("25%") + "。击杀目标时立即重置冷却，并获得" + K("【诺克萨斯之力】") + "。";
            return true;
        }
        if (key.Contains("Darius_Flash"))
        {
            value = DariusSummonerBalance.BuildFlashDescription();
            return true;
        }
        if (key.Contains("Darius_Ghost"))
        {
            value = DariusSummonerBalance.BuildGhostDescription(hero);
            return true;
        }
        if (IsHemorrhageKey(key))
        {
            string tick = DamageByRatio(ad, DariusHemorrhageRuntime.AdRatioPerStackAtLevel, live, level, previousLevel, compare);
            int fervor = DariusConstellationRuntime.GetWarFervorLevel(hero);
            bool warFervor = fervor > 0;
            string stackCap = CompareValue(lvl => DariusHemorrhageRuntime.MaxStacksAtLevel(lvl) + (DariusLanguage.IsEnglish ? " stacks" : "层"), level, previousLevel, compare, ValueColor);
            string mightAd = CompareValue(lvl => DariusHemorrhageRuntime.NoxianMightAttackDamagePercentAtLevel(lvl, warFervor).ToString("0.#") + "%", level, previousLevel, compare, ValueColor);
            string trigger = DariusLanguage.IsEnglish
                ? (warFervor ? "Applying 5 " + K("Hemorrhage") + " stacks within " + V(DariusConstellationRuntime.GetWarFervorWindow(hero).ToString("0.#") + " seconds") : "Reaching maximum " + K("Hemorrhage") + " stacks on one enemy")
                : (warFervor ? "在" + V(DariusConstellationRuntime.GetWarFervorWindow(hero).ToString("0.#") + "秒") + "内累计施加5次" + K("【出血】") : "同一敌人叠满" + K("【出血】"));
            value = DariusLanguage.IsEnglish
                ? prefix + "Basic attacks make enemies bleed for " + V("5 seconds") + ", dealing " + tick + " physical damage per stack each second, up to " + stackCap + ". " + trigger + " grants " + K("Noxian Might") + " for " + V("5 seconds") + ", increasing Attack Damage by " + mightAd + "."
                : prefix + "普攻使敌人流血" + V("5秒") + "，每秒每层造成" + tick + "物理伤害，最多" + stackCap + "。" + trigger + "后获得" + V("5秒") + K("【诺克萨斯之力】") + "，攻击力提高" + mightAd + "。";
            return true;
        }
        return false;
    }

    public static bool IsHemorrhageKey(string key) => !string.IsNullOrEmpty(key) && key.Contains("Darius_Hemorrhage");

    public static bool IsDariusMemoryKey(string key)
    {
        if (string.IsNullOrEmpty(key)) return false;
        return key.Contains("Darius_Decimate") || key.Contains("Darius_CripplingStrike") ||
               key.Contains("Darius_Apprehend") || key.Contains("Darius_NoxianGuillotine") || IsHemorrhageKey(key);
    }

    public static string HemorrhageDescription
    {
        get
        {
            string value;
            SkillTrigger skill = DariusFormalRegistry.Hemorrhage;
            int level = skill != null ? DariusMemoryScaling.GetLevel(skill) : 1;
            return TryBuildDescription("St_D_Darius_Hemorrhage", skill, level, null, false, null, out value) ? value : (DariusLanguage.IsEnglish ? "Hemorrhage" : "出血");
        }
    }

    public static List<LocaleNode> Nodes(string text)
    {
        return new List<LocaleNode>
        {
            new LocaleNode { type = LocaleNodeType.Text, textData = text }
        };
    }

    private static int ResolveHemorrhageLevel(Hero hero)
    {
        try
        {
            DariusHemorrhageRuntime runtime = hero != null ? hero.GetComponent<DariusHemorrhageRuntime>() : null;
            if (runtime != null && runtime.isEnabled) return runtime.identityMemoryLevel;
        }
        catch { }
        SkillTrigger identity = DariusFormalRegistry.Hemorrhage;
        return identity != null ? DariusMemoryScaling.GetLevel(identity) : 1;
    }

    private static SkillTrigger ResolveRegisteredSkill(string key)
    {
        if (string.IsNullOrEmpty(key)) return null;
        if (key.Contains("Darius_Decimate")) return DariusFormalRegistry.Decimate;
        if (key.Contains("Darius_CripplingStrike")) return DariusFormalRegistry.CripplingStrike;
        if (key.Contains("Darius_Apprehend")) return DariusFormalRegistry.Apprehend;
        if (key.Contains("Darius_NoxianGuillotine")) return DariusFormalRegistry.NoxianGuillotine;
        if (IsHemorrhageKey(key)) return DariusFormalRegistry.Hemorrhage;
        if (key.Contains("Darius_Flash")) return DariusFormalRegistry.Flash;
        if (key.Contains("Darius_Ghost")) return DariusFormalRegistry.Ghost;
        return null;
    }

    private static string V(string text) => "<color=" + ValueColor + ">" + text + "</color>";
    private static string K(string text) => "<color=" + KeywordColor + ">" + text + "</color>";
    private static string H(string text) => "<color=" + HealColor + ">" + text + "</color>";

    private static string CompareValue(Func<int, string> raw, int level, int? previousLevel, bool compare, string currentColor)
    {
        string current = raw(level);
        if (!compare || !previousLevel.HasValue || previousLevel.Value == level)
            return "<color=" + currentColor + ">" + current + "</color>";
        string previous = raw(previousLevel.Value);
        if (string.Equals(previous, current, StringComparison.Ordinal))
            return "<color=" + currentColor + ">" + current + "</color>";
        return "<color=" + PreviousValueColor + ">" + previous + "</color> → <color=" + currentColor + ">" + current + "</color>";
    }

    private static string DamageByRatio(float ad, Func<int, float> ratioAtLevel, bool live, int level, int? previousLevel, bool compare)
    {
        return CompareValue(lvl =>
        {
            float ratio = ratioAtLevel(lvl);
            string ratioText = (ratio * 100f).ToString("0.#") + "%";
            if (live)
            {
                string amount = (ad * ratio).ToString("0.#");
                return "<sprite=2><color=#fff><gradient=sv_ad>" + amount + "</gradient></color><sprite=5>" +
                       "<size=80%><color=#a9b0bb>" + (DariusLanguage.IsEnglish ? " (" + ratioText + " AD)" : "（" + ratioText + "攻击力）") + "</color></size>";
            }
            return "<sprite=2><color=#fff><gradient=sv_ad>" + ratioText + "</gradient></color><sprite=5>";
        }, level, previousLevel, compare, ValueColor);
    }

    private static string TotalAttackDamageByBonusRatio(float ad, Func<int, float> bonusRatioAtLevel, bool live, int level, int? previousLevel, bool compare)
    {
        return CompareValue(lvl =>
        {
            float ratio = 1f + bonusRatioAtLevel(lvl);
            string ratioText = (ratio * 100f).ToString("0.#") + "%";
            if (live)
            {
                string amount = (ad * ratio).ToString("0.#");
                return "<sprite=2><color=#fff><gradient=sv_ad>" + amount + "</gradient></color><sprite=5>" +
                       "<size=80%><color=#a9b0bb>" + (DariusLanguage.IsEnglish ? " (" + ratioText + " AD)" : "（" + ratioText + "攻击力）") + "</color></size>";
            }
            return "<sprite=2><color=#fff><gradient=sv_ad>" + ratioText + "</gradient></color><sprite=5>";
        }, level, previousLevel, compare, ValueColor);
    }

    private static string HealByRatio(float maxHealth, Func<int, float> ratioAtLevel, bool live, int level, int? previousLevel, bool compare)
    {
        return CompareValue(lvl =>
        {
            float ratio = ratioAtLevel(lvl);
            string ratioText = (ratio * 100f).ToString("0.#") + (DariusLanguage.IsEnglish ? "% max Health" : "%最大生命");
            if (live)
                return "<color=" + HealColor + ">" + (maxHealth * ratio).ToString("0.#") + "</color>" +
                       "<size=80%><color=#a9b0bb>" + (DariusLanguage.IsEnglish ? " (" + ratioText + ")" : "（" + ratioText + "）") + "</color></size>";
            return "<color=" + HealColor + ">" + ratioText + "</color>";
        }, level, previousLevel, compare, HealColor);
    }

    private static string SecondsByValue(Func<int, float> valueAtLevel, int level, int? previousLevel, bool compare)
    {
        return CompareValue(lvl => valueAtLevel(lvl).ToString("0.##") + (DariusLanguage.IsEnglish ? " seconds" : "秒"),
            level, previousLevel, compare, ValueColor);
    }

    private static string Cooldown(SkillTrigger skill, float baseCooldown, int level, int? previousLevel, bool compare)
    {
        return CompareValue(lvl => DariusMemoryScaling.CooldownAtLevel(skill, baseCooldown, lvl).ToString("0.##") + (DariusLanguage.IsEnglish ? " seconds" : "秒"),
            level, previousLevel, compare, ValueColor);
    }
}
