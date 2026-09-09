using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

// Loads League of Legends presentation icons packaged under assets/icons.
// BuildAndInstall.ps1 validates the release asset set before compilation; runtime fallback remains only as a last-resort guard for damaged installs.
public static class DariusPrototypeIcons
{
    private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();
    private static readonly Dictionary<string, string> FileNames = new Dictionary<string, string>
    {
        { "Q", "Darius_Q_Decimate.png" },
        { "W", "Darius_W_CripplingStrike.png" },
        { "E", "Darius_E_Apprehend.png" },
        { "R", "Darius_R_NoxianGuillotine.png" },
        { "H", "Darius_P_Hemorrhage.png" },
        { "HERO", "Darius_Champion.png" },
        { "SKIN_CLASSIC", "Darius_Skin_Default.png" },
        { "SKIN_GODKING", "Darius_Skin_GodKing.png" },
        { "SKIN_DUNKMASTER", "Darius_Skin_Dunkmaster.png" },
        { "SKIN_MECHA", "Darius_Skin_Mecha.png" },
        { "FLASH", "Darius_M_Flash.png" },
        { "GHOST", "Darius_M_Ghost.png" },
        { "RUNE_CONQUEROR", "Rune_Conqueror.png" },
        { "RUNE_TRIUMPH", "Rune_Triumph.png" },
        { "RUNE_ALACRITY", "Rune_Alacrity.png" },
        { "RUNE_LASTSTAND", "Rune_LastStand.png" },
        { "RUNE_AXIOM", "Rune_AxiomArcanist.png" },
        { "RUNE_SECONDWIND", "Rune_SecondWind.png" },
        { "RUNE_OVERGROWTH", "Rune_Overgrowth.png" },
        { "RUNE_REVITALIZE", "Rune_Revitalize.png" },
        { "RUNE_CONDITIONING", "Rune_Conditioning.png" },
        { "RUNE_UNFLINCHING", "Rune_Unflinching.png" },
        { "RUNE_FERVOR", "Rune_FervorOfBattle.png" },
        { "RUNE_NIMBUS", "Rune_NimbusCloak.png" },
        { "RUNE_CELERITY", "Rune_Celerity.png" },
        { "RUNE_GATHERING", "Rune_GatheringStorm.png" },
        { "RUNE_COSMIC", "Rune_CosmicInsight.png" },
        { "ITEM_TRINITY", "Item_TrinityForce.png" },
        { "ITEM_BLACK_CLEAVER", "Item_BlackCleaver.png" },
        { "ITEM_SHOJIN", "Item_SpearOfShojin.png" },
        { "ITEM_STERAK", "Item_SteraksGage.png" },
        { "ITEM_DEATHS_DANCE", "Item_DeathsDance.png" },
        { "ITEM_BLOODMAIL", "Item_OverlordsBloodmail.png" },
        { "ITEM_SUNDERED_SKY", "Item_SunderedSky.png" },
        { "ITEM_STRIDEBREAKER", "Item_Stridebreaker.png" },
        { "ITEM_DEAD_MANS", "Item_DeadMansPlate.png" },
        { "ITEM_YOUMUU", "Item_YoumuusGhostblade.png" },
        { "STAR_AWOO", "star_awoo.png" }
    };

    public static Sprite Get(string key)
    {
        Sprite sprite;
        if (Cache.TryGetValue(key, out sprite) && sprite != null) return sprite;

        string fileName;
        if (FileNames.TryGetValue(key, out fileName))
        {
            string path = FindIconPath(fileName);
            if (!string.IsNullOrEmpty(path))
            {
                try
                {
                    byte[] data = File.ReadAllBytes(path);
                    Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    tex.name = "DariusLoLIcon_" + key;
                    tex.wrapMode = TextureWrapMode.Clamp;
                    tex.filterMode = FilterMode.Point;
                    if (ImageConversion.LoadImage(tex, data, false))
                    {
                        tex = PrepareUiTexture(tex, key);
                        sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), Mathf.Max(tex.width, tex.height));
                        sprite.name = "DariusLoLIcon_" + key;
                        Cache[key] = sprite;
                        DariusLog.Info("ICON", "Loaded League icon key=" + key + " file=" + path + " size=" + tex.width + "x" + tex.height);
                        return sprite;
                    }
                    DariusLog.Warn("ICON", "ImageConversion.LoadImage returned false for key=" + key + " file=" + path);
                }
                catch (Exception e)
                {
                    DariusLog.Exception("ICON", e, "Failed loading League icon key=" + key + " file=" + path);
                }
            }
            else
            {
                DariusLog.Warn("ICON", "Packaged League icon file not found for key=" + key + " expected=" + fileName + ". Falling back to emergency generated icon.");
            }
        }

        sprite = CreateFallback(key);
        Cache[key] = sprite;
        return sprite;
    }

    private static string FindIconPath(string fileName)
    {
        try
        {
            string root = DariusModEnvironment.ResolveRoot();
            if (!string.IsNullOrEmpty(root))
            {
                string path = Path.Combine(root, "assets", "icons", fileName);
                if (File.Exists(path)) return path;
            }
        }
        catch { }
        return null;
    }

    private static Texture2D PrepareUiTexture(Texture2D source, string key)
    {
        if (source == null) return null;
        try
        {
            int sourceMax = Mathf.Max(source.width, source.height);
            int targetMax = sourceMax < 96 ? 256 : (sourceMax < 160 ? 192 : sourceMax);
            if (targetMax <= sourceMax)
            {
                source.wrapMode = TextureWrapMode.Clamp;
                source.filterMode = FilterMode.Bilinear;
                return source;
            }

            float scale = targetMax / (float)sourceMax;
            int width = Mathf.Max(source.width, Mathf.RoundToInt(source.width * scale));
            int height = Mathf.Max(source.height, Mathf.RoundToInt(source.height * scale));
            Texture2D upscaled = new Texture2D(width, height, TextureFormat.RGBA32, false);
            upscaled.name = source.name + "_UI";
            upscaled.wrapMode = TextureWrapMode.Clamp;
            upscaled.filterMode = FilterMode.Bilinear;
            Color[] pixels = new Color[width * height];
            for (int y = 0; y < height; y++)
            {
                float v = (y + 0.5f) / height;
                for (int x = 0; x < width; x++)
                {
                    float u = (x + 0.5f) / width;
                    pixels[y * width + x] = source.GetPixelBilinear(u, v);
                }
            }
            upscaled.SetPixels(pixels);
            upscaled.Apply(false, false);
            DariusLog.DebugInfoThrottled("ICON", key + ":ui-upscale",
                "Upscaled packaged UI icon key=" + key + " from " + source.width + "x" + source.height +
                " to " + width + "x" + height + ".", 5.0);
            return upscaled;
        }
        catch (Exception e)
        {
            DariusLog.Exception("ICON", e, "Could not prepare packaged UI icon key=" + key + " for crisp rendering");
            source.wrapMode = TextureWrapMode.Clamp;
            source.filterMode = FilterMode.Bilinear;
            return source;
        }
    }

    private static Sprite CreateFallback(string key)
    {
        const int size = 64;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.name = "DariusFallbackIcon_" + key;
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        Color bg = new Color(0.16f, 0.025f, 0.025f, 1f);
        Color red = new Color(0.70f, 0.04f, 0.035f, 1f);
        Color gold = new Color(0.93f, 0.72f, 0.32f, 1f);
        Color dark = new Color(0.05f, 0.01f, 0.01f, 1f);
        Color[] pixels = new Color[size * size];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = bg;

        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            int edge = Mathf.Min(Mathf.Min(x, size - 1 - x), Mathf.Min(y, size - 1 - y));
            if (edge < 4) pixels[y * size + x] = dark;
        }

        if (key == "Q") DrawRing(pixels, size, 31.5f, 31.5f, 24f, 6f, gold);
        else if (key == "W")
        {
            DrawThickLine(pixels, size, 17, 48, 47, 16, 5, red);
            DrawThickLine(pixels, size, 20, 51, 50, 19, 2, gold);
        }
        else if (key == "E")
        {
            DrawThickLine(pixels, size, 16, 18, 32, 45, 5, red);
            DrawThickLine(pixels, size, 48, 18, 32, 45, 5, red);
            DrawThickLine(pixels, size, 31, 45, 31, 53, 4, gold);
        }
        else if (key == "R")
        {
            DrawThickLine(pixels, size, 31, 13, 31, 52, 4, gold);
            DrawThickLine(pixels, size, 18, 20, 45, 20, 6, red);
            DrawThickLine(pixels, size, 18, 20, 24, 29, 4, red);
        }
        else if (key == "FLASH")
        {
            DrawDisc(pixels, size, 31, 31, 11, gold);
            DrawThickLine(pixels, size, 12, 31, 51, 31, 3, new Color(1f, 0.92f, 0.60f, 1f));
            DrawThickLine(pixels, size, 31, 12, 31, 51, 3, new Color(1f, 0.92f, 0.60f, 1f));
        }
        else if (key == "GHOST")
        {
            Color cyan = new Color(0.40f, 0.86f, 1f, 1f);
            DrawDisc(pixels, size, 31, 25, 11, cyan);
            DrawThickLine(pixels, size, 19, 39, 29, 31, 4, cyan);
            DrawThickLine(pixels, size, 34, 31, 47, 43, 4, cyan);
            DrawThickLine(pixels, size, 31, 35, 23, 51, 3, cyan);
        }
        else
        {
            for (int i = 0; i < 5; i++) DrawDisc(pixels, size, 16 + i * 8, 34, 4, red);
            DrawDisc(pixels, size, 32, 22, 7, gold);
        }

        tex.SetPixels(pixels);
        tex.Apply(false, false);
        Sprite sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 64f);
        sprite.name = "DariusFallbackIcon_" + key;
        DariusLog.Warn("ICON", "Using generated fallback icon for key=" + key);
        return sprite;
    }

    private static void DrawRing(Color[] px, int size, float cx, float cy, float radius, float thickness, Color c)
    {
        float min = (radius - thickness) * (radius - thickness);
        float max = (radius + thickness) * (radius + thickness);
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float dx = x - cx;
            float dy = y - cy;
            float d = dx * dx + dy * dy;
            if (d >= min && d <= max) px[y * size + x] = c;
        }
    }

    private static void DrawDisc(Color[] px, int size, int cx, int cy, int radius, Color c)
    {
        int rr = radius * radius;
        for (int y = Mathf.Max(0, cy - radius); y <= Mathf.Min(size - 1, cy + radius); y++)
        for (int x = Mathf.Max(0, cx - radius); x <= Mathf.Min(size - 1, cx + radius); x++)
        {
            int dx = x - cx;
            int dy = y - cy;
            if (dx * dx + dy * dy <= rr) px[y * size + x] = c;
        }
    }

    private static void DrawThickLine(Color[] px, int size, int x0, int y0, int x1, int y1, int radius, Color c)
    {
        int steps = Mathf.Max(Mathf.Abs(x1 - x0), Mathf.Abs(y1 - y0));
        if (steps <= 0) { DrawDisc(px, size, x0, y0, radius, c); return; }
        for (int i = 0; i <= steps; i++)
        {
            float t = i / (float)steps;
            int x = Mathf.RoundToInt(Mathf.Lerp(x0, x1, t));
            int y = Mathf.RoundToInt(Mathf.Lerp(y0, y1, t));
            DrawDisc(px, size, x, y, radius, c);
        }
    }
}
