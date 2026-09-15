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