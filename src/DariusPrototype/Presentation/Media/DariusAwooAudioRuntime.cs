// Optional local song player for the user-authored Awoo constellation. The Mod does not bundle
// copyrighted music. If the user puts assets/audio/animals.ogg or animals.wav in their own Mod
// folder, this component uses that exact local file. Missing audio is a silent, non-fatal state.
public sealed class DariusAwooAudioRuntime : MonoBehaviour
{
    private Hero _hero;
    private AudioSource _source;
    private AudioClip _clip;
    private bool _loading;
    private bool _missingLogged;
    private float _pendingPitch = 1f;

    public void Restart(Hero hero, float pitch)
    {
        _hero = hero;
        _pendingPitch = Mathf.Clamp(pitch, 0.5f, 2.0f);
        if (!IsLocalHero()) return;
        EnsureSource();
        if (_clip != null)
        {
            PlayNow();
            return;
        }
        if (_loading) return;
        string root = DariusModEnvironment.ResolveRoot();
        string wav = !string.IsNullOrEmpty(root) ? Path.Combine(root, "assets", "audio", "animals.wav") : null;
        string ogg = !string.IsNullOrEmpty(root) ? Path.Combine(root, "assets", "audio", "animals.ogg") : null;
        if (!string.IsNullOrEmpty(wav) && File.Exists(wav))
        {
            _clip = DariusMedia.Clip("animals");
            if (_clip != null) PlayNow();
            return;
        }
        if (!string.IsNullOrEmpty(ogg) && File.Exists(ogg))
        {
            StartCoroutine(LoadOgg(ogg));
            return;
        }
        if (!_missingLogged)
        {
            _missingLogged = true;
            DariusLog.Warn("STAR-AWOO-AUDIO", "Optional Animals audio is not bundled. Put a user-owned assets/audio/animals.ogg or animals.wav in the Darius Mod folder to enable this star's song playback.");
        }
    }

    public void StopPlayback()
    {
        if (_source != null) { try { _source.Stop(); } catch { } }
    }

    private bool IsLocalHero()
    {
        try { return _hero != null && DewPlayer.local != null && DewPlayer.local.hero == _hero; }
        catch { return true; }
    }

    private void EnsureSource()
    {
        if (_source != null) return;
        _source = gameObject.AddComponent<AudioSource>();
        _source.playOnAwake = false;
        _source.loop = false;
        _source.spatialBlend = 0f;
    }

    private IEnumerator LoadOgg(string path)
    {
        _loading = true;
        string uri;
        try { uri = new Uri(path).AbsoluteUri; }
        catch (Exception e)
        {
            _loading = false;
            DariusLog.Exception("STAR-AWOO-AUDIO", e, "Could not make URI for optional Animals OGG");
            yield break;
        }
        using (UnityWebRequest req = UnityWebRequestMultimedia.GetAudioClip(uri, (UnityEngine.AudioType)14))
        {
            yield return req.SendWebRequest();
            _loading = false;
            if (!string.IsNullOrEmpty(req.error))
            {
                DariusLog.Error("STAR-AWOO-AUDIO", "Optional Animals OGG decode failed: " + req.error);
                yield break;
            }
            try { _clip = DownloadHandlerAudioClip.GetContent(req); }
            catch (Exception e) { DariusLog.Exception("STAR-AWOO-AUDIO", e, "Optional Animals OGG GetContent failed"); }
        }
        if (_clip != null) PlayNow();
    }

    private void PlayNow()
    {
        if (_clip == null || !IsLocalHero()) return;
        EnsureSource();
        _source.Stop();
        _source.clip = _clip;
        _source.pitch = _pendingPitch;
        _source.volume = Mathf.Clamp01(0.75f * DariusAudioSettingsRuntime.Multiplier(DariusAudioChannel.R));
        _source.time = 0f;
        _source.Play();
        DariusLog.DebugInfo("STAR-AWOO-AUDIO", "Restarted optional Animals audio pitch=" + _pendingPitch.ToString("0.0"));
    }
}
