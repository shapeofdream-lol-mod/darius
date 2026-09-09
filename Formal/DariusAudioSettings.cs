using System;
using UnityEngine;

// Native Shape of Dreams ModConfig-backed mix controls. ModConfig owns persistence and its
// settings UI edits these public fields directly; DariusMedia reads the current value at every
// playback, so changes take effect without restarting or rebuilding the mod.
public enum DariusAudioChannel
{
    Auto,
    Q,
    W,
    E,
    R,
    BasicAttack,
    Dodge,
    Voice
}

public sealed class DariusAudioVolumeConfig : ModConfig
{
    [Range(0f, 200f)]
    [ModConfig.LabelText("Q Volume / Q 技能音量")]
    [ModConfig.Description("0% mutes; 100% is default; maximum 200%. Changes apply to subsequent Q sounds immediately. / 0% 静音，100% 为默认音量，最高 200%。修改后立即用于之后播放的 Q 技能音效。")]
    public float qVolume = 100f;

    [Range(0f, 200f)]
    [ModConfig.LabelText("W Volume / W 技能音量")]
    [ModConfig.Description("0% mutes; 100% is default; maximum 200%. Changes apply to subsequent W sounds immediately. / 0% 静音，100% 为默认音量，最高 200%。修改后立即用于之后播放的 W 技能音效。")]
    public float wVolume = 100f;

    [Range(0f, 200f)]
    [ModConfig.LabelText("E Volume / E 技能音量")]
    [ModConfig.Description("0% mutes; 100% is default; maximum 200%. Changes apply to subsequent E sounds immediately. / 0% 静音，100% 为默认音量，最高 200%。修改后立即用于之后播放的 E 技能音效。")]
    public float eVolume = 100f;

    [Range(0f, 200f)]
    [ModConfig.LabelText("R Volume / R 技能音量")]
    [ModConfig.Description("0% mutes; 100% is default; maximum 200%. Changes apply to subsequent R sounds immediately. / 0% 静音，100% 为默认音量，最高 200%。修改后立即用于之后播放的 R 技能音效。")]
    public float rVolume = 100f;

    [Range(0f, 200f)]
    [ModConfig.LabelText("Basic Attack Volume / 普攻音量")]
    [ModConfig.Description("0% mutes; 100% is default; maximum 200%. Controls only basic-attack sounds played by this Traveler mod. / 0% 静音，100% 为默认音量，最高 200%。仅控制该旅行者 Mod 自己播放的普攻音效。")]
    public float basicAttackVolume = 100f;

    [Range(0f, 200f)]
    [ModConfig.LabelText("Dodge Skill Volume / 闪避技能音量")]
    [ModConfig.Description("0% mutes; 100% is default; maximum 200%. Controls Traveler dodge and movement skill sounds such as Flash and Ghost. / 0% 静音，100% 为默认音量，最高 200%。控制闪现、疾跑等旅行者闪避/位移技能音效。")]
    public float dodgeVolume = 100f;

    [Range(0f, 200f)]
    [ModConfig.LabelText("Voice Volume / 人物语音音量")]
    [ModConfig.Description("0% mutes; 100% is default; maximum 200%. Controls only voice lines played by this mod, without affecting skill sounds or the base game. / 0% 静音，100% 为默认音量，最高 200%。仅控制该旅行者及其皮肤由本 Mod 播放的人物语音，不影响技能音效或游戏本体声音。")]
    public float voiceVolume = 100f;

    public float GetMultiplier(DariusAudioChannel channel)
    {
        float percent;
        switch (channel)
        {
            case DariusAudioChannel.Q: percent = qVolume; break;
            case DariusAudioChannel.W: percent = wVolume; break;
            case DariusAudioChannel.E: percent = eVolume; break;
            case DariusAudioChannel.R: percent = rVolume; break;
            case DariusAudioChannel.BasicAttack: percent = basicAttackVolume; break;
            case DariusAudioChannel.Dodge: percent = dodgeVolume; break;
            case DariusAudioChannel.Voice: percent = voiceVolume; break;
            default: return 1f;
        }
        float multiplier = Mathf.Clamp(percent, 0f, 200f) * 0.01f;
        Debug.Log("[DariusAudio] resolve channel=" + channel + " configuredPercent=" + percent.ToString("0.##") + " multiplier=" + multiplier.ToString("0.###"));
        return multiplier;
    }
}

public static class DariusAudioSettingsRuntime
{
    private static DariusAudioVolumeConfig _config;

    public static void Bind(DariusAudioVolumeConfig config)
    {
        _config = config;
    }

    public static float Multiplier(DariusAudioChannel channel)
    {
        return _config != null ? _config.GetMultiplier(channel) : 1f;
    }

    public static DariusAudioChannel Resolve(string key)
    {
        if (string.IsNullOrEmpty(key)) return DariusAudioChannel.Auto;
        string normalized = key.Trim().ToLowerInvariant();
        if (normalized.StartsWith("vo_", StringComparison.Ordinal) || normalized.StartsWith("voice_", StringComparison.Ordinal) || normalized.Contains("_vo_") || normalized.Contains("voice"))
            return DariusAudioChannel.Voice;
        if (normalized == "flash" || normalized == "ghost" || normalized.Contains("dodge") || normalized.Contains("dash"))
            return DariusAudioChannel.Dodge;
        if (normalized.Contains("basic_attack") || normalized.StartsWith("attack", StringComparison.Ordinal) || normalized.StartsWith("aa_", StringComparison.Ordinal))
            return DariusAudioChannel.BasicAttack;
        if (normalized.StartsWith("q_", StringComparison.Ordinal) || normalized == "q") return DariusAudioChannel.Q;
        if (normalized.StartsWith("w_", StringComparison.Ordinal) || normalized == "w") return DariusAudioChannel.W;
        if (normalized.StartsWith("e_", StringComparison.Ordinal) || normalized == "e") return DariusAudioChannel.E;
        if (normalized.StartsWith("r_", StringComparison.Ordinal) || normalized == "r") return DariusAudioChannel.R;
        return DariusAudioChannel.Auto;
    }
}
