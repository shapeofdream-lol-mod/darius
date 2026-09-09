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
public static class DariusMedia
{
    private static readonly Dictionary<string, Texture2D> Textures = new Dictionary<string, Texture2D>(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, AudioClip> Clips = new Dictionary<string, AudioClip>(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, int> LastPoolIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, float> LastVoiceAt = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, Dictionary<string, string[]>> Pass2SfxBySkin = new Dictionary<string, Dictionary<string, string[]>>(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, Dictionary<string, string[]>> Pass2VoiceBySkin = new Dictionary<string, Dictionary<string, string[]>>(StringComparer.OrdinalIgnoreCase);
    private static bool _pass2PoolsLoaded;

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

    // Loads the packaged League Flash activation asset from assets/audio/flash.ogg as Ogg Vorbis.
    public static IEnumerator PreloadCompressedAudio()
    {
        string path = AssetPath("audio", "flash.ogg");
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            DariusLog.Warn("SFX-ASSET", "Authentic League Flash OGG missing; Flash will be silent. path=" + (path ?? "<null>"));
            yield break;
        }

        string uri;
        try { uri = new Uri(path).AbsoluteUri; }
        catch (Exception e)
        {
            DariusLog.Exception("SFX-ASSET", e, "Failed to create local URI for Flash OGG path=" + path);
            yield break;
        }

        using (UnityWebRequest req = UnityWebRequestMultimedia.GetAudioClip(uri, (UnityEngine.AudioType)14))
        {
            yield return req.SendWebRequest();
            if (!string.IsNullOrEmpty(req.error))
            {
                DariusLog.Error("SFX-ASSET", "Flash OGG decode failed error=" + req.error + " path=" + path);
                yield break;
            }
            AudioClip clip = null;
            try { clip = DownloadHandlerAudioClip.GetContent(req); }
            catch (Exception e) { DariusLog.Exception("SFX-ASSET", e, "Flash OGG GetContent failed"); }
            if (clip == null)
            {
                DariusLog.Error("SFX-ASSET", "Flash OGG request succeeded but returned a null AudioClip.");
                yield break;
            }
            clip.name = "DariusSfx_flash_League";
            Clips["flash"] = clip;
            DariusLog.Info("SFX-ASSET", "Loaded authentic League Flash OGG length=" + clip.length.ToString("0.000") +
                "s channels=" + clip.channels + " hz=" + clip.frequency);
        }
    }

    public static Texture2D Texture(string key)
    {
        Texture2D cached;
        if (Textures.TryGetValue(key, out cached) && cached != null) return cached;
        string path = AssetPath("vfx", key + ".png");
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            DariusLog.Warn("VFX-ASSET", "Texture missing key=" + key + " path=" + (path ?? "<null>"));
            Textures[key] = null;
            return null;
        }
        try
        {
            byte[] data = File.ReadAllBytes(path);
            Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.name = "DariusVfx_" + key;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            if (!ImageConversion.LoadImage(tex, data, false))
            {
                UnityEngine.Object.Destroy(tex);
                DariusLog.Warn("VFX-ASSET", "ImageConversion.LoadImage failed key=" + key + " file=" + path);
                Textures[key] = null;
                return null;
            }
            Textures[key] = tex;
            DariusLog.DebugInfo("VFX-ASSET", "Loaded texture key=" + key + " size=" + tex.width + "x" + tex.height);
            return tex;
        }
        catch (Exception e)
        {
            DariusLog.Exception("VFX-ASSET", e, "Texture load failed key=" + key + " file=" + path);
            Textures[key] = null;
            return null;
        }
    }

    public static AudioClip Clip(string key)
    {
        AudioClip cached;
        if (Clips.TryGetValue(key, out cached) && cached != null) return cached;
        string path = AssetPath("audio", key + ".wav");
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            DariusLog.Warn("SFX-ASSET", "Audio missing key=" + key + " path=" + (path ?? "<null>"));
            Clips[key] = null;
            return null;
        }
        try
        {
            AudioClip clip = LoadPcmWave(path, "DariusSfx_" + key);
            Clips[key] = clip;
            if (clip != null)
                DariusLog.DebugInfo("SFX-ASSET", "Loaded audio key=" + key + " length=" + clip.length.ToString("0.000") + "s channels=" + clip.channels + " hz=" + clip.frequency);
            return clip;
        }
        catch (Exception e)
        {
            DariusLog.Exception("SFX-ASSET", e, "Audio load failed key=" + key + " file=" + path);
            Clips[key] = null;
            return null;
        }
    }

    public static void Play(string key, Vector3 position, float volume = 0.85f, float pitch = 1.0f)
    {
        Play(key, position, DariusAudioSettingsRuntime.Resolve(key), volume, pitch);
    }

    public static void Play(string key, Vector3 position, DariusAudioChannel channel, float volume = 0.85f, float pitch = 1.0f)
    {
        try
        {
            AudioClip clip = Clip(key);
            if (clip == null) return;

            float multiplier = DariusAudioSettingsRuntime.Multiplier(channel);
            float gain = Mathf.Clamp01(volume) * Mathf.Clamp(multiplier, 0f, 2f);
            if (gain <= 0.0001f) return;

            GameObject go = new GameObject("DariusSfx_" + key);
            go.transform.position = position;
            float safePitch = Mathf.Clamp(pitch, 0.60f, 1.50f);
            AudioSource primary = ConfigureAudioSource(go.AddComponent<AudioSource>(), clip, Mathf.Min(1f, gain), safePitch);
            AudioSource boost = null;
            if (gain > 1f)
                boost = ConfigureAudioSource(go.AddComponent<AudioSource>(), clip, Mathf.Clamp01(gain - 1f), safePitch);

            primary.Play();
            if (boost != null) boost.Play();
            UnityEngine.Object.Destroy(go, clip.length / Mathf.Max(0.60f, Mathf.Abs(safePitch)) + 0.15f);
            DariusLog.DebugInfo("SFX", "Played key=" + key + " channel=" + channel + " pos=" + DariusLog.Vec(position) +
                " baseVolume=" + volume.ToString("0.##") + " setting=" + (multiplier * 100f).ToString("0.#") +
                "% effectiveGain=" + gain.ToString("0.##") + " pitch=" + safePitch.ToString("0.##"));
        }
        catch (Exception e)
        {
            DariusLog.Exception("SFX", e, "Play failed key=" + key);
        }
    }

    private static AudioSource ConfigureAudioSource(AudioSource src, AudioClip clip, float volume, float pitch)
    {
        src.clip = clip;
        src.playOnAwake = false;
        src.volume = Mathf.Clamp01(volume);
        src.pitch = pitch;
        src.spatialBlend = 0.35f;
        src.minDistance = 5f;
        src.maxDistance = 45f;
        src.rolloffMode = AudioRolloffMode.Linear;
        return src;
    }

    private static string VariantKey(Hero owner)
    {
        try
        {
            string key = owner != null ? DariusSkinAnimationHooks.GetVariantKey(owner) : null;
            return string.IsNullOrEmpty(key) ? "Classic" : key;
        }
        catch { return "Classic"; }
    }

    private static bool IsGodKing(Hero owner)
    {
        return string.Equals(VariantKey(owner), "GodKing", StringComparison.OrdinalIgnoreCase);
    }

    private static void EnsurePass2PoolsLoaded()
    {
        if (_pass2PoolsLoaded) return;
        _pass2PoolsLoaded = true;
        try
        {
            string path = Path.Combine(Root, "assets", "raw_lol_audio", "PASS2_MEDIA_MANIFEST.json");
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
            DariusLog.Info("MEDIA-PASS2", "Loaded authentic Pass2 media routing skins=" + Pass2VoiceBySkin.Count +
                " sfxSkins=" + Pass2SfxBySkin.Count + " source=Riot-Wwise-current-client");
        }
        catch (Exception e) { DariusLog.Exception("MEDIA-PASS2", e, "Failed loading Pass2 media routing manifest"); }
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

    private static bool IsLocalOwner(Hero owner)
    {
        if (owner == null) return false;
        try { return DewPlayer.local != null && DewPlayer.local.hero == owner; } catch { return false; }
    }

    private static string NormalizeSfxEvent(string key)
    {
        // The existing animation graph calls q_swing at the exact Q blade-impact timing.
        if (string.Equals(key, "q_swing", StringComparison.OrdinalIgnoreCase)) return "q_hit";
        return key;
    }

    private static string Pick(Dictionary<string, string[]> pools, string eventKey, string poolPrefix)
    {
        string[] pool;
        if (pools == null || !pools.TryGetValue(eventKey, out pool) || pool == null || pool.Length == 0) return null;
        if (pool.Length == 1) return pool[0];
        int index = UnityEngine.Random.Range(0, pool.Length);
        int previous;
        string repeatKey = poolPrefix + ":" + eventKey;
        if (LastPoolIndex.TryGetValue(repeatKey, out previous) && previous == index)
            index = (index + 1 + UnityEngine.Random.Range(0, pool.Length - 1)) % pool.Length;
        LastPoolIndex[repeatKey] = index;
        return pool[index];
    }

    // Skin-aware authentic League SFX. If a new/unmapped logical key is requested, keep the
    // previous packaged sound as a compatibility fallback rather than making the action silent.
    public static void PlayForSkin(string key, Hero owner, Vector3 position, float volume = 0.85f)
    {
        PlayForSkin(key, owner, position, DariusAudioSettingsRuntime.Resolve(key), volume);
    }

    public static void PlayForSkin(string key, Hero owner, Vector3 position, DariusAudioChannel channel, float volume = 0.85f)
    {
        string variant;
        Dictionary<string, string[]> pools = GetSfxPools(owner, out variant);
        string eventKey = NormalizeSfxEvent(key);
        string chosen = Pick(pools, eventKey, "pass2-sfx-" + variant);
        if (!string.IsNullOrEmpty(chosen))
        {
            Play(chosen, position, channel, volume, 1.0f);
            return;
        }
        // Never substitute another skin's sound. Classic/GodKing retain the old logical-key
        // compatibility route only for non-skin-specific legacy events. Dunkmaster/Mecha stay
        // silent if their exact Riot Wwise event is genuinely absent.
        if (string.Equals(variant, "Classic", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(variant, "GodKing", StringComparison.OrdinalIgnoreCase))
            Play(key, position, channel, volume, 1.0f);
        else
            DariusLog.DebugInfoThrottled("SFX-PASS2", variant + ":" + eventKey,
                "No exact Riot SFX mapping for skin=" + variant + " event=" + eventKey + "; no cross-skin fallback used.", 10f);
    }

    public static void PlayForSkin(string key, Hero owner, float volume = 0.85f)
    {
        if (owner == null) return;
        PlayForSkin(key, owner, owner.transform.position, DariusAudioSettingsRuntime.Resolve(key), volume);
    }

    public static void PlayForSkin(string key, Hero owner, DariusAudioChannel channel, float volume = 0.85f)
    {
        if (owner == null) return;
        PlayForSkin(key, owner, owner.transform.position, channel, volume);
    }

    // Random cast VO from the corresponding skin's zh_CN Wwise event pool. Local-owner only:
    // this avoids duplicate VO when multiplayer/network presentation mirrors the same cast.
    public static void PlaySkillVoice(Hero owner, string skill, DariusAudioChannel channel, float volume = 0.78f)
    {
        if (!IsLocalOwner(owner) || string.IsNullOrEmpty(skill)) return;
        string normalized = skill.Trim().ToLowerInvariant();
        string cooldownKey = owner.GetInstanceID().ToString() + ":" + normalized;
        float last;
        if (LastVoiceAt.TryGetValue(cooldownKey, out last) && Time.unscaledTime - last < 0.12f) return;

        string variant;
        Dictionary<string, string[]> pools = GetVoicePools(owner, out variant);
        string chosen = Pick(pools, normalized, "pass2-vo-" + variant);
        if (string.IsNullOrEmpty(chosen))
        {
            DariusLog.DebugInfo("VO", "No authentic cast VO event for skin=" + variant + " skill=" + normalized);
            return;
        }
        LastVoiceAt[cooldownKey] = Time.unscaledTime;
        // Character voice has its own ModConfig channel. Skill cast VO must never inherit the
        // Q/W/E/R SFX slider merely because the line was triggered by that skill.
        PlayVoice2D(chosen, DariusAudioChannel.Voice, volume);
        DariusLog.DebugInfo("VO", "Random cast VO skin=" + variant + " skill=" + normalized + " clip=" + chosen + " route=dedicated-2D-local voice-channel");
    }

    // Full character VO event routing for movement/basic attacks/kills/death/respawn/passive-max.
    // Only events present in the selected skin's exact Riot Wwise bank are allowed.
    public static bool PlayVoiceEvent(Hero owner, string eventKey, float volume = 0.78f)
    {
        if (!IsLocalOwner(owner) || string.IsNullOrEmpty(eventKey)) return false;
        string variant;
        Dictionary<string, string[]> pools = GetVoicePools(owner, out variant);
        string normalized = eventKey.Trim().ToLowerInvariant();
        string cooldownKey = owner.GetInstanceID().ToString() + ":full:" + normalized;
        float last;
        if (LastVoiceAt.TryGetValue(cooldownKey, out last) && Time.unscaledTime - last < 0.20f) return false;
        string chosen = Pick(pools, normalized, "pass2-full-vo-" + variant);
        if (string.IsNullOrEmpty(chosen)) return false;
        LastVoiceAt[cooldownKey] = Time.unscaledTime;
        PlayVoice2D(chosen, DariusAudioChannel.Voice, volume);
        DariusLog.DebugInfo("VO-FULL", "Played skin=" + variant + " event=" + normalized + " clip=" + chosen);
        return true;
    }

    private static void PlayVoice2D(string key, DariusAudioChannel channel, float volume)
    {
        try
        {
            AudioClip clip = Clip(key);
            if (clip == null) return;
            float multiplier = DariusAudioSettingsRuntime.Multiplier(channel);
            float gain = Mathf.Clamp(volume, 0f, 1.5f) * Mathf.Clamp(multiplier, 0f, 2f);
            if (gain <= 0.0001f) return;

            GameObject go = new GameObject("DariusVoice2D_" + key);
            AudioSource src = ConfigureVoiceSource(go.AddComponent<AudioSource>(), clip, Mathf.Min(1f, gain));
            AudioSource boost = null;
            if (gain > 1f) boost = ConfigureVoiceSource(go.AddComponent<AudioSource>(), clip, Mathf.Clamp01(gain - 1f));
            src.Play();
            if (boost != null) boost.Play();

            DariusVoicePlaybackProbe probe = go.AddComponent<DariusVoicePlaybackProbe>();
            probe.source = src;
            probe.clipKey = key;
            probe.requestedGain = gain;
            UnityEngine.Object.Destroy(go, clip.length + 0.35f);
            DariusLog.Info("VO-PLAY", "Started dedicated 2D VO key=" + key + " channel=" + channel +
                " clipLength=" + clip.length.ToString("0.000") + " gain=" + gain.ToString("0.##") +
                " listenerCount=" + UnityEngine.Object.FindObjectsOfType<AudioListener>().Length);
        }
        catch (Exception e)
        {
            DariusLog.Exception("VO-PLAY", e, "Dedicated 2D VO failed key=" + key);
        }
    }

    private static AudioSource ConfigureVoiceSource(AudioSource src, AudioClip clip, float volume)
    {
        src.clip = clip; src.playOnAwake = false; src.spatialBlend = 0f;
        src.rolloffMode = AudioRolloffMode.Linear; src.priority = 0; src.volume = Mathf.Clamp01(volume);
        src.pitch = 1f; src.loop = false; src.bypassReverbZones = true; src.ignoreListenerPause = false;
        return src;
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

    private static AudioClip LoadPcmWave(string path, string clipName)
    {
        byte[] bytes = File.ReadAllBytes(path);
        if (bytes.Length < 44 || ReadFourCC(bytes, 0) != "RIFF" || ReadFourCC(bytes, 8) != "WAVE")
            throw new InvalidDataException("Not a RIFF/WAVE file: " + path);

        int offset = 12;
        int channels = 0, sampleRate = 0, bits = 0, format = 0;
        int dataOffset = -1, dataLength = 0;
        while (offset + 8 <= bytes.Length)
        {
            string id = ReadFourCC(bytes, offset);
            int len = BitConverter.ToInt32(bytes, offset + 4);
            int body = offset + 8;
            if (len < 0 || body + len > bytes.Length) break;
            if (id == "fmt ")
            {
                format = BitConverter.ToInt16(bytes, body);
                channels = BitConverter.ToInt16(bytes, body + 2);
                sampleRate = BitConverter.ToInt32(bytes, body + 4);
                bits = BitConverter.ToInt16(bytes, body + 14);
            }
            else if (id == "data")
            {
                dataOffset = body;
                dataLength = len;
                break;
            }
            offset = body + len + (len & 1);
        }
        if (format != 1) throw new NotSupportedException("Only PCM WAV is supported; format=" + format);
        if (channels < 1 || channels > 2 || sampleRate <= 0 || dataOffset < 0) throw new InvalidDataException("Invalid WAV header.");
        if (bits != 16) throw new NotSupportedException("Only 16-bit PCM WAV is supported; bits=" + bits);

        int sampleValues = dataLength / 2;
        int frames = sampleValues / channels;
        float[] samples = new float[sampleValues];
        int p = dataOffset;
        for (int i = 0; i < sampleValues; i++, p += 2)
            samples[i] = BitConverter.ToInt16(bytes, p) / 32768f;

        AudioClip clip = AudioClip.Create(clipName, frames, channels, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private static string ReadFourCC(byte[] bytes, int offset)
    {
        return new string(new char[] { (char)bytes[offset], (char)bytes[offset + 1], (char)bytes[offset + 2], (char)bytes[offset + 3] });
    }
}

// Drives short-lived textured quads without requiring an AssetBundle prefab.
public sealed class DariusVfxQuadMotion : MonoBehaviour
{
    public float lifetime = 0.4f;
    public float rotateDegreesPerSecond;
    public Vector3 startScale = Vector3.one;
    public Vector3 endScale = Vector3.one;
    public Transform follow;
    public Vector3 followOffset;
    public bool billboard;
    private float _born;
    private Renderer _renderer;
    private Color _initialColor = Color.white;

    private void Awake()
    {
        _born = Time.time;
        _renderer = GetComponent<Renderer>();
        if (_renderer != null && _renderer.material != null) _initialColor = _renderer.material.color;
    }

    private void Update()
    {
        float t = lifetime <= 0.001f ? 1f : Mathf.Clamp01((Time.time - _born) / lifetime);
        if (follow != null) transform.position = follow.position + followOffset;
        if (billboard && Camera.main != null)
        {
            Vector3 dir = Camera.main.transform.position - transform.position;
            if (dir.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(-dir.normalized, Vector3.up);
        }
        else if (Mathf.Abs(rotateDegreesPerSecond) > 0.01f)
        {
            transform.Rotate(0f, rotateDegreesPerSecond * Time.deltaTime, 0f, Space.World);
        }
        transform.localScale = Vector3.Lerp(startScale, endScale, t);
        if (_renderer != null && _renderer.material != null)
        {
            Color c = _initialColor;
            c.a *= 1f - t;
            _renderer.material.color = c;
        }
        if (t >= 1f) Destroy(gameObject);
    }
}

// Persistent follow/pulse driver used for W's armed weapon glow and Noxian Might.
// Lifetime is controlled by the gameplay runtime so the visual cannot disappear before the state does.
public sealed class DariusPersistentVfxPulse : MonoBehaviour
{
    public Transform follow;
    public Vector3 followOffset;
    public Vector3 baseScale = Vector3.one;
    public float pulseAmount = 0.06f;
    public float pulseSpeed = 6.0f;
    public float rotateDegreesPerSecond;
    public bool billboard;
    private Renderer _renderer;
    private Color _baseColor = Color.white;

    private void Awake()
    {
        _renderer = GetComponent<Renderer>();
        if (_renderer != null && _renderer.material != null) _baseColor = _renderer.material.color;
    }

    private void Update()
    {
        if (follow == null)
        {
            Destroy(gameObject);
            return;
        }
        transform.position = follow.position + followOffset;
        if (billboard && Camera.main != null)
        {
            Vector3 dir = Camera.main.transform.position - transform.position;
            if (dir.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(-dir.normalized, Vector3.up);
        }
        else if (Mathf.Abs(rotateDegreesPerSecond) > 0.01f)
        {
            transform.Rotate(0f, rotateDegreesPerSecond * Time.deltaTime, 0f, Space.World);
        }
        float pulse = 1f + Mathf.Sin(Time.time * pulseSpeed) * pulseAmount;
        transform.localScale = baseScale * pulse;
        if (_renderer != null && _renderer.material != null)
        {
            Color c = _baseColor;
            c.a *= 0.88f + 0.12f * Mathf.Sin(Time.time * pulseSpeed * 0.67f);
            _renderer.material.color = c;
        }
    }
}


// Presentation controller lives on the Noxian Might VFX root. It deliberately does not own the
// gameplay buff; destroying the root only removes presentation. Screen-edge feedback is local-only.
public sealed class DariusNoxianMightPresentation : MonoBehaviour
{
    public Hero owner;
    public Light bodyLight;
    public float baseLightIntensity = 1.65f;
    public Color edgeColor = new Color(0.92f, 0.015f, 0.01f, 1f);

    private void Update()
    {
        if (bodyLight != null)
        {
            float pulse = 0.88f + 0.12f * Mathf.Sin(Time.unscaledTime * 7.0f);
            bodyLight.intensity = baseLightIntensity * pulse;
        }
    }

    private bool IsLocalOwner()
    {
        if (owner == null) return false;
        try { return DewPlayer.local != null && DewPlayer.local.hero == owner; }
        catch { return false; }
    }

    private void OnGUI()
    {
        if (!IsLocalOwner() || Event.current == null || Event.current.type != EventType.Repaint) return;

        float pulse = 0.88f + 0.12f * Mathf.Sin(Time.unscaledTime * 5.3f);
        float depth = Mathf.Clamp(Mathf.Min(Screen.width, Screen.height) * 0.082f, 44f, 92f);
        const int bands = 12;
        float band = depth / bands;
        Color old = GUI.color;
        Texture2D white = Texture2D.whiteTexture;

        // Multiple translucent bands approximate a soft vignette without relying on a UI prefab,
        // Canvas hierarchy or post-processing stack that may be rebuilt between rooms.
        for (int i = 0; i < bands; i++)
        {
            float t = i / (float)(bands - 1);
            float alpha = (0.23f * pulse) * (1f - t) * (1f - t);
            GUI.color = new Color(edgeColor.r, edgeColor.g, edgeColor.b, alpha);
            float inset = i * band;
            GUI.DrawTexture(new Rect(0f, inset, Screen.width, band + 1f), white);
            GUI.DrawTexture(new Rect(0f, Screen.height - inset - band - 1f, Screen.width, band + 1f), white);
            GUI.DrawTexture(new Rect(inset, 0f, band + 1f, Screen.height), white);
            GUI.DrawTexture(new Rect(Screen.width - inset - band - 1f, 0f, band + 1f, Screen.height), white);
        }
        GUI.color = old;
    }
}


public sealed class DariusVoicePlaybackProbe : MonoBehaviour
{
    public AudioSource source;
    public string clipKey;
    public float requestedGain;
    private IEnumerator Start()
    {
        yield return null;
        if (source == null)
        {
            DariusLog.Warn("VO-PROBE", "AudioSource vanished before first-frame verification key=" + (clipKey ?? "<null>"));
            yield break;
        }
        int samples0 = source.timeSamples;
        bool playing0 = source.isPlaying;
        DariusLog.Info("VO-PROBE", "frame+1 key=" + clipKey + " isPlaying=" + playing0 + " timeSamples=" + samples0 +
            " spatialBlend=" + source.spatialBlend.ToString("0.##") + " volume=" + source.volume.ToString("0.##"));
        yield return new WaitForSecondsRealtime(0.08f);
        if (source != null)
        {
            DariusLog.Info("VO-PROBE", "+80ms key=" + clipKey + " isPlaying=" + source.isPlaying + " timeSamples=" + source.timeSamples +
                " advanced=" + (source.timeSamples > samples0));
        }
    }
}
