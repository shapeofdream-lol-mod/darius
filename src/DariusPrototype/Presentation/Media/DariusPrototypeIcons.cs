// Loads League of Legends presentation icons packaged under assets/icons.
// The file-name map below is the single source of truth for the icon set; runtime fallback remains only as a last-resort guard for damaged installs.
public static partial class DariusPrototypeIcons
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
}