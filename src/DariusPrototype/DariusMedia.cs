using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

// Runtime media loader for Darius VFX textures and skill audio.
// v0.23 LoL-authentic test pass:
// - Classic and God-King skill SFX are decoded from the user's current League Wwise banks.
// - Cast VO is the user's zh_CN League VO and is randomly selected per skill cast.
// - Source pools stay separate rather than being baked together so Wwise variants remain audible.
public static partial class DariusMedia
{
    private static readonly Dictionary<string, Texture2D> Textures = new Dictionary<string, Texture2D>(StringComparer.OrdinalIgnoreCase);

    private static readonly Dictionary<string, AudioClip> Clips = new Dictionary<string, AudioClip>(StringComparer.OrdinalIgnoreCase);

    private static readonly Dictionary<string, int> LastPoolIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

    private static readonly Dictionary<string, float> LastVoiceAt = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);

    private static readonly Dictionary<string, Dictionary<string, string[]>> Pass2SfxBySkin = new Dictionary<string, Dictionary<string, string[]>>(StringComparer.OrdinalIgnoreCase);

    private static readonly Dictionary<string, Dictionary<string, string[]>> Pass2VoiceBySkin = new Dictionary<string, Dictionary<string, string[]>>(StringComparer.OrdinalIgnoreCase);

    private static bool _pass2PoolsLoaded;

    private static string _root;

    private static bool _loggedRoot;

    public static string Root
    {
        get
        {
            if (!string.IsNullOrEmpty(_root)) return _root;
            _root = ResolveModRoot();
            if (!_loggedRoot)
            {
                _loggedRoot = true;
                DariusLog.Info("MEDIA", "Resolved mod media root=" + (_root ?? "<null>"));
            }
            return _root;
        }
    }

    public static void PreloadAll()
    {
        EnsurePass2PoolsLoaded();
        // Pass 5 cleanup: only compatibility/persistent-state textures that still have a live
        // non-skill use remain. Q/W/E/R combat presentation is Riot-authored and is not preloaded
        // from the old hand-authored mask bank.
        string[] textures = {
            "q_outer_ring","q_heal_wisps","hit_flash","bleed_drop","bleed_five_mark","noxian_aura",
            "gk_glow","gk_wisps_red"
        };
        string[] legacyAudio = { "bleed_five","noxian_might","ghost" };
        int tOk = 0, aOk = 0, aExpected = 0;
        for (int i = 0; i < textures.Length; i++) if (Texture(textures[i]) != null) tOk++;
        for (int i = 0; i < legacyAudio.Length; i++) { aExpected++; if (Clip(legacyAudio[i]) != null) aOk++; }
        aOk += PreloadPool(ClassicSfx, ref aExpected);
        aOk += PreloadPool(GodKingSfx, ref aExpected);
        DariusLog.Info("MEDIA", "Preload complete textures=" + tOk + "/" + textures.Length +
            " skillSfx+legacy=" + aOk + "/" + aExpected +
            "; cast VO remains lazy-loaded; League Flash OGG loads asynchronously.");
    }

    private static int PreloadPool(Dictionary<string, string[]> pools, ref int expected)
    {
        int ok = 0;
        foreach (KeyValuePair<string, string[]> kv in pools)
        {
            string[] arr = kv.Value;
            if (arr == null) continue;
            for (int i = 0; i < arr.Length; i++)
            {
                expected++;
                if (Clip(arr[i]) != null) ok++;
            }
        }
        return ok;
    }

    private static string AssetPath(string folder, string file)
    {
        string root = Root;
        return string.IsNullOrEmpty(root) ? null : Path.Combine(root, "assets", folder, file);
    }

    private static string ResolveModRoot()
    {
        return DariusModEnvironment.ResolveRoot();
    }
}