using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

public static partial class DariusMedia
{
    private static readonly Dictionary<string, string[]> ClassicSfx = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
    {
        { "q_windup", new string[] { "lol_classic_q_windup_01", "lol_classic_q_windup_02" } },
        { "q_hit", new string[] { "lol_classic_q_hit_01", "lol_classic_q_hit_02", "lol_classic_q_hit_03", "lol_classic_q_hit_04" } },
        { "w_arm", new string[] { "lol_classic_w_arm_01" } },
        { "w_swing", new string[] { "lol_classic_w_swing_01", "lol_classic_w_swing_02", "lol_classic_w_swing_03", "lol_classic_w_swing_04" } },
        { "w_hit", new string[] { "lol_classic_w_hit_01", "lol_classic_w_hit_02", "lol_classic_w_hit_03", "lol_classic_w_hit_04" } },
        { "e_cast", new string[] { "lol_classic_e_cast_01", "lol_classic_e_cast_02", "lol_classic_e_cast_03", "lol_classic_e_cast_04" } },
        { "e_pull", new string[] { "lol_classic_e_pull_01", "lol_classic_e_pull_02", "lol_classic_e_pull_03" } },
        { "r_cast", new string[] { "lol_classic_r_cast_01", "lol_classic_r_cast_02", "lol_classic_r_cast_03", "lol_classic_r_cast_04" } },
        { "r_hit", new string[] { "lol_classic_r_hit_01", "lol_classic_r_hit_02", "lol_classic_r_hit_03", "lol_classic_r_hit_04" } },
        { "r_reset", new string[] { "lol_classic_r_reset_01", "lol_classic_r_reset_02", "lol_classic_r_reset_03" } },
        { "basic_attack", new string[] { "lol_classic_basic_attack_01", "lol_classic_basic_attack_02", "lol_classic_basic_attack_03", "lol_classic_basic_attack_04" } },
    };

    private static readonly Dictionary<string, string[]> GodKingSfx = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
    {
        { "q_windup", new string[] { "lol_godking_q_windup_01", "lol_godking_q_windup_02" } },
        { "q_hit", new string[] { "lol_godking_q_hit_01", "lol_godking_q_hit_02" } },
        { "w_arm", new string[] { "lol_godking_w_arm_01" } },
        { "w_swing", new string[] { "lol_godking_w_swing_01" } },
        { "w_hit", new string[] { "lol_godking_w_hit_01", "lol_godking_w_hit_02" } },
        { "e_cast", new string[] { "lol_godking_e_cast_01" } },
        { "e_pull", new string[] { "lol_godking_e_pull_01", "lol_godking_e_pull_02" } },
        { "r_cast", new string[] { "lol_godking_r_cast_01", "lol_godking_r_cast_02", "lol_godking_r_cast_03" } },
        { "r_hit", new string[] { "lol_godking_r_hit_01", "lol_godking_r_hit_02", "lol_godking_r_hit_03", "lol_godking_r_hit_04", "lol_godking_r_hit_05", "lol_godking_r_hit_06" } },
        { "r_reset", new string[] { "lol_godking_r_reset_01", "lol_godking_r_reset_02", "lol_godking_r_reset_03" } },
        { "basic_attack", new string[] { "lol_godking_basic_attack_01", "lol_godking_basic_attack_02", "lol_godking_basic_attack_03" } },
    };

    private static readonly Dictionary<string, string[]> ClassicVoice = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
    {
        { "q", new string[] { "vo_classic_q_01", "vo_classic_q_02", "vo_classic_q_03", "vo_classic_q_04" } },
        { "w", new string[] { "vo_classic_w_01", "vo_classic_w_02", "vo_classic_w_03", "vo_classic_w_04" } },
        { "e", new string[] {  } },
        { "r", new string[] { "vo_classic_r_01", "vo_classic_r_02", "vo_classic_r_03", "vo_classic_r_04" } },
    };

    private static readonly Dictionary<string, string[]> GodKingVoice = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
    {
        { "q", new string[] { "vo_godking_q_01", "vo_godking_q_02", "vo_godking_q_03" } },
        { "w", new string[] { "vo_godking_w_01", "vo_godking_w_02", "vo_godking_w_03", "vo_godking_w_04", "vo_godking_w_05", "vo_godking_w_06", "vo_godking_w_07", "vo_godking_w_08", "vo_godking_w_09", "vo_godking_w_10", "vo_godking_w_11", "vo_godking_w_12", "vo_godking_w_13" } },
        { "e", new string[] { "vo_godking_e_01", "vo_godking_e_02", "vo_godking_e_03", "vo_godking_e_04", "vo_godking_e_05", "vo_godking_e_06", "vo_godking_e_07", "vo_godking_e_08" } },
        { "r", new string[] { "vo_godking_r_01", "vo_godking_r_02", "vo_godking_r_03", "vo_godking_r_04", "vo_godking_r_05", "vo_godking_r_06", "vo_godking_r_07" } },
    };

    private static void EnsurePass2PoolsLoaded()
    {
        if (_pass2PoolsLoaded) return;
        try
        {
            string rootPath = Root;
            if (string.IsNullOrEmpty(rootPath))
            {
                DariusLog.Warn("MEDIA-PASS2", "Pass2 media manifest deferred because mod root is not resolved yet.");
                return;
            }

            string path = Path.Combine(rootPath, "assets", "raw_lol_audio", "PASS2_MEDIA_MANIFEST.json");
            if (!File.Exists(path))
            {
                DariusLog.Warn("MEDIA-PASS2", "Pass2 media manifest missing path=" + path);
                return;
            }
            JObject root = JObject.Parse(File.ReadAllText(path));
            foreach (JProperty skinProp in root.Properties())
            {
                JObject skin = skinProp.Value as JObject;
                if (skin == null) continue;
                LoadPoolSection(skinProp.Name, skin["sfx"] as JObject, Pass2SfxBySkin);
                LoadPoolSection(skinProp.Name, skin["vo"] as JObject, Pass2VoiceBySkin);
            }
            _pass2PoolsLoaded = true;
            DariusLog.Info("MEDIA-PASS2", "Loaded authentic Pass2 media routing skins=" + Pass2VoiceBySkin.Count +
                " sfxSkins=" + Pass2SfxBySkin.Count + " source=Riot-Wwise-current-client");
        }
        catch (Exception e) { DariusLog.Exception("MEDIA-PASS2", e, "Failed loading Pass2 media routing manifest; a later call may retry"); }
    }

    private static void LoadPoolSection(string skinKey, JObject section, Dictionary<string, Dictionary<string, string[]>> destination)
    {
        if (section == null || string.IsNullOrEmpty(skinKey)) return;
        Dictionary<string, string[]> events = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        foreach (JProperty eventProp in section.Properties())
        {
            JObject info = eventProp.Value as JObject;
            JArray keys = info != null ? info["keys"] as JArray : null;
            if (keys == null || keys.Count == 0) continue;
            List<string> values = new List<string>();
            for (int i = 0; i < keys.Count; i++)
            {
                string value = (string)keys[i];
                if (!string.IsNullOrEmpty(value)) values.Add(value);
            }
            if (values.Count > 0) events[eventProp.Name] = values.ToArray();
        }
        if (events.Count > 0) destination[skinKey] = events;
    }

    private static Dictionary<string, string[]> GetSfxPools(Hero owner, out string variant)
    {
        variant = VariantKey(owner);
        EnsurePass2PoolsLoaded();
        if (string.Equals(variant, "Classic", StringComparison.OrdinalIgnoreCase)) return ClassicSfx;
        if (string.Equals(variant, "GodKing", StringComparison.OrdinalIgnoreCase)) return GodKingSfx;
        Dictionary<string, string[]> result;
        return Pass2SfxBySkin.TryGetValue(variant, out result) ? result : null;
    }

    private static Dictionary<string, string[]> GetVoicePools(Hero owner, out string variant)
    {
        variant = VariantKey(owner);
        EnsurePass2PoolsLoaded();
        Dictionary<string, string[]> result;
        if (Pass2VoiceBySkin.TryGetValue(variant, out result)) return result;
        // Legacy current-client cast pools are only a defensive fallback for Classic/GodKing if
        // the local Pass2 decoder has not yet been run. Never route a different skin to Classic.
        if (string.Equals(variant, "Classic", StringComparison.OrdinalIgnoreCase)) return ClassicVoice;
        if (string.Equals(variant, "GodKing", StringComparison.OrdinalIgnoreCase)) return GodKingVoice;
        return null;
    }
}