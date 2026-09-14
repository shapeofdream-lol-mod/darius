public static partial class DariusFormalLocalization
{
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