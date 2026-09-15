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
    private static bool IsLocalOwner(Hero owner)
    {
        if (owner == null) return false;
        try { return DewPlayer.local != null && DewPlayer.local.hero == owner; } catch { return false; }
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
        // Character voice has its own ModConfig channel. Skill cast VO must never inherit the
        // Q/W/E/R SFX slider merely because the line was triggered by that skill.
        if (!TryPlayVoice2D(owner, chosen, DariusAudioChannel.Voice, volume)) return;
        LastVoiceAt[cooldownKey] = Time.unscaledTime;
        DariusLog.DebugInfo("VO", "Requested cast VO skin=" + variant + " skill=" + normalized + " clip=" + chosen + " route=dedicated-2D-local voice-channel");
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
        if (!TryPlayVoice2D(owner, chosen, DariusAudioChannel.Voice, volume)) return false;
        LastVoiceAt[cooldownKey] = Time.unscaledTime;
        DariusLog.DebugInfo("VO-FULL", "Requested skin=" + variant + " event=" + normalized + " clip=" + chosen + " state=decode-or-play");
        return true;
    }

    private static void PlayVoiceClip2D(string key, AudioClip clip, DariusAudioChannel channel, float volume)
    {
        if (clip == null) return;
        try
        {
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
                " listenerCount=" + UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Length);
        }
        catch (Exception e)
        {
            DariusLog.Exception("VO-PLAY", e, "Dedicated 2D VO playback failed key=" + key);
        }
    }

    private static AudioSource ConfigureVoiceSource(AudioSource src, AudioClip clip, float volume)
    {
        src.clip = clip; src.playOnAwake = false; src.spatialBlend = 0f;
        src.rolloffMode = AudioRolloffMode.Linear; src.priority = 0; src.volume = Mathf.Clamp01(volume);
        src.pitch = 1f; src.loop = false; src.bypassReverbZones = true; src.ignoreListenerPause = false;
        return src;
    }
}
