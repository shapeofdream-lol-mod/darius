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
}