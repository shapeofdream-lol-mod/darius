using System.Collections;
using UnityEngine;

// Temporary compatibility layer for native model migration.
// This class owns legacy Animator-driven presentation only while EntityAnimation becomes the
// authoritative animation path. It is intentionally isolated so it can be deleted after smoke tests.
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

    public void Stop()
    {
        if (_sequence != null)
        {
            StopCoroutine(_sequence);
            _sequence = null;
        }
        if (_animator != null) _animator.speed = 1f;
    }

    private void OnDestroy()
    {
        Stop();
    }
}
