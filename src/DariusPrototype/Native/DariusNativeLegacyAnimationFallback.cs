// Compatibility layer for temporary legacy Animator presentation.
// EntityAnimation remains the preferred authority; this class only owns old Animator sequences.
internal sealed class DariusNativeLegacyAnimationFallback : MonoBehaviour
{
    private Animator _animator;
    private Coroutine _sequence;

    public void Initialize(Animator animator)
    {
        _animator = animator;
    }

    public bool PlayState(string state, float fade = 0.05f, float speed = 1f)
    {
        if (_animator == null || string.IsNullOrEmpty(state)) return false;
        int hash = Animator.StringToHash(state);
        if (!_animator.HasState(0, hash)) return false;
        _animator.speed = Mathf.Max(0.05f, speed);
        if (fade > 0.001f) _animator.CrossFadeInFixedTime(hash, fade);
        else _animator.Play(hash, 0, 0f);
        return true;
    }

    public void PlayAttack(string clip, float fade = 0.05f)
    {
        PlayState(clip, fade, 1f);
    }

    public void PlayOneShot(string clip)
    {
        PlayState(clip, 0.05f, 1f);
    }

    public void PlayTimed(string clip, float duration, float speed = 1f)
    {
        StopSequence();
        PlayState(clip, 0.05f, speed);
        if (duration > 0f)
            _sequence = StartCoroutine(ResetSpeedAfter(duration));
    }

    public void PlaySequence(IEnumerator sequence)
    {
        StopSequence();
        if (sequence != null) _sequence = StartCoroutine(sequence);
    }

    private IEnumerator ResetSpeedAfter(float duration)
    {
        yield return new WaitForSeconds(duration);
        if (_animator != null) _animator.speed = 1f;
        _sequence = null;
    }

    public void SetArmedState(string clip, bool armed)
    {
        if (armed) PlayState(clip, 0.05f, 1f);
        else Stop();
    }

    public void Stop()
    {
        StopSequence();
        if (_animator != null) _animator.speed = 1f;
    }

    public void StopSequence()
    {
        if (_sequence != null)
        {
            try { StopCoroutine(_sequence); } catch { }
            _sequence = null;
        }
    }

    private void OnDestroy()
    {
        Stop();
    }
}
