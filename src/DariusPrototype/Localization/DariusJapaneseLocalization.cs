public static partial class DariusJapaneseLocalization
{
    public static bool TryGet(string key, bool description, out string value)
    {
        value = null;
        if (string.IsNullOrEmpty(key)) return false;
        if (TryGetHeroSkinSkillText(key, description, out value)) return true;
        if (TryGetConstellationText(key, description, out value)) return true;
        if (TryGetItemText(key, description, out value)) return true;
        return false;
    }
}