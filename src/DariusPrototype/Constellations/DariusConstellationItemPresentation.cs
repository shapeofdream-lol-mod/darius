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