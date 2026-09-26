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
    // Loads the packaged League Flash activation asset from assets/audio/flash.ogg as Ogg Vorbis.
    public static IEnumerator PreloadCompressedAudio()
    {
        int generation = CaptureLifecycleGeneration();
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
            if (!IsLifecycleGenerationCurrent(generation))
            {
                UnityEngine.Object.Destroy(clip);
                DariusLog.DebugInfo("SFX-ASSET", "Discarded stale Flash OGG decode from an unloaded media generation.");
                yield break;
            }
            clip.name = "DariusSfx_flash_League";
            Clips["flash"] = clip;
            DariusLog.Info("SFX-ASSET", "Loaded authentic League Flash OGG length=" + clip.length.ToString("0.000") +
                "s channels=" + clip.channels + " hz=" + clip.frequency);
        }
    }

    private static bool TryPlayVoice2D(Hero owner, string key, DariusAudioChannel channel, float volume)
    {
        try
        {
            AudioClip clip;
            if (Clips.TryGetValue(key, out clip) && clip != null)
            {
                PlayVoiceClip2D(key, clip, channel, volume);
                return true;
            }

            string path = AssetPath("audio", key + ".ogg");
            if (owner == null || string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                DariusLog.Warn("VO-ASSET", "Voice OGG missing key=" + key + " path=" + (path ?? "<null>"));
                return false;
            }
            owner.StartCoroutine(LoadVoiceOggAndPlay(owner, key, path, channel, volume));
            return true;
        }
        catch (Exception e)
        {
            DariusLog.Exception("VO-PLAY", e, "Dedicated 2D VO request failed key=" + key);
            return false;
        }
    }

    private static IEnumerator LoadVoiceOggAndPlay(Hero owner, string key, string path, DariusAudioChannel channel, float volume)
    {
        int generation = CaptureLifecycleGeneration();
        string uri;
        try { uri = new Uri(path).AbsoluteUri; }
        catch (Exception e)
        {
            DariusLog.Exception("VO-ASSET", e, "Failed to create local URI for voice OGG key=" + key);
            yield break;
        }

        AudioClip clip = null;
        using (UnityWebRequest req = UnityWebRequestMultimedia.GetAudioClip(uri, (UnityEngine.AudioType)14))
        {
            yield return req.SendWebRequest();
            if (!string.IsNullOrEmpty(req.error))
            {
                DariusLog.Error("VO-ASSET", "Voice OGG decode failed key=" + key + " error=" + req.error);
                yield break;
            }
            try { clip = DownloadHandlerAudioClip.GetContent(req); }
            catch (Exception e) { DariusLog.Exception("VO-ASSET", e, "Voice OGG GetContent failed key=" + key); }
        }

        if (clip == null) yield break;
        if (!IsLifecycleGenerationCurrent(generation))
        {
            UnityEngine.Object.Destroy(clip);
            DariusLog.DebugInfo("VO-ASSET", "Discarded stale voice OGG decode key=" + key + " from an unloaded media generation.");
            yield break;
        }

        AudioClip cached;
        if (Clips.TryGetValue(key, out cached) && cached != null)
        {
            UnityEngine.Object.Destroy(clip);
            clip = cached;
        }
        else
        {
            clip.name = "DariusVoice_" + key;
            Clips[key] = clip;
            DariusLog.DebugInfo("VO-ASSET", "Voice OGG decoded on demand key=" + key + " length=" + clip.length.ToString("0.000") +
                "s channels=" + clip.channels + " hz=" + clip.frequency);
        }

        // Preserve the original trigger instead of dropping the first line while its file decodes.
        if (IsLocalOwner(owner)) PlayVoiceClip2D(key, clip, channel, volume);
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