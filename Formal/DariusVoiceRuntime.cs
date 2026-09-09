using System;
using System.Collections.Generic;
using UnityEngine;

// Pass 2 full-character voice router. It never manufactures lines or substitutes another skin's
// bank: DariusMedia only returns an event when the selected LoL skin actually owns that Wwise pool.
public sealed class DariusVoiceRuntime : MonoBehaviour
{
    private Hero _hero;
    private Vector3 _lastPosition;
    private float _movingSince = -1f;
    private float _nextMoveVoiceAt;
    private float _nextAttackVoiceAt;
    private float _nextKillVoiceAt;
    private bool _firstMovePlayed;
    private bool _wasDead;
    private int _multiKillCount;
    private float _lastKillAt = -999f;
    private bool _firstKillPlayed;
    private readonly HashSet<int> _recentVictims = new HashSet<int>();
    private float _recentVictimClearAt;

    public static DariusVoiceRuntime Ensure(Hero hero)
    {
        if (hero == null) return null;
        DariusVoiceRuntime runtime = hero.GetComponent<DariusVoiceRuntime>();
        if (runtime == null) runtime = hero.gameObject.AddComponent<DariusVoiceRuntime>();
        runtime.Bind(hero);
        return runtime;
    }

    private void Awake()
    {
        _hero = GetComponent<Hero>();
        if (_hero != null) _lastPosition = _hero.transform.position;
    }

    public void Bind(Hero hero)
    {
        if (hero == null) return;
        if (_hero != hero)
        {
            _hero = hero;
            _lastPosition = hero.transform.position;
            _movingSince = -1f;
            _nextMoveVoiceAt = Time.unscaledTime + 1.0f;
        }
    }

    private bool IsLocalHero()
    {
        try { return _hero != null && DewPlayer.local != null && DewPlayer.local.hero == _hero; }
        catch { return false; }
    }

    private void Update()
    {
        if (!IsLocalHero() || _hero == null) return;

        bool dead = false;
        try { dead = _hero.currentHealth <= 0.001f || _hero.IsNullInactiveDeadOrKnockedOut(); }
        catch { try { dead = _hero.currentHealth <= 0.001f; } catch { } }

        if (dead != _wasDead)
        {
            if (dead)
            {
                DariusMedia.PlayVoiceEvent(_hero, "death", 0.86f);
                _movingSince = -1f;
            }
            else
            {
                DariusMedia.PlayVoiceEvent(_hero, "respawn", 0.84f);
                _nextMoveVoiceAt = Time.unscaledTime + 1.5f;
            }
            _wasDead = dead;
        }
        if (dead) return;

        Vector3 now = _hero.transform.position;
        Vector3 delta = now - _lastPosition;
        delta.y = 0f;
        _lastPosition = now;
        bool moving = delta.sqrMagnitude > 0.0009f;
        float t = Time.unscaledTime;

        if (moving)
        {
            if (_movingSince < 0f) _movingSince = t;
            if (!_firstMovePlayed)
            {
                if (DariusMedia.PlayVoiceEvent(_hero, "move_first", 0.78f) || DariusMedia.PlayVoiceEvent(_hero, "move", 0.78f))
                {
                    _firstMovePlayed = true;
                    _nextMoveVoiceAt = t + UnityEngine.Random.Range(8.0f, 12.0f);
                }
            }
            else if (t >= _nextMoveVoiceAt)
            {
                bool longMove = t - _movingSince >= 4.0f;
                bool played = longMove && DariusMedia.PlayVoiceEvent(_hero, "move_long", 0.78f);
                if (!played) played = DariusMedia.PlayVoiceEvent(_hero, "move", 0.78f);
                _nextMoveVoiceAt = t + UnityEngine.Random.Range(9.0f, 14.0f);
            }
        }
        else
        {
            _movingSince = -1f;
        }

        if (_recentVictims.Count > 0 && t >= _recentVictimClearAt) _recentVictims.Clear();
        if (_multiKillCount > 0 && t - _lastKillAt > 10f) _multiKillCount = 0;
    }

    public static void NotifyAttack(Hero hero)
    {
        DariusVoiceRuntime runtime = Ensure(hero);
        if (runtime == null || !runtime.IsLocalHero()) return;
        float t = Time.unscaledTime;
        if (t < runtime._nextAttackVoiceAt) return;
        if (UnityEngine.Random.value > 0.34f) return;
        if (DariusMedia.PlayVoiceEvent(hero, "attack", 0.76f))
            runtime._nextAttackVoiceAt = t + UnityEngine.Random.Range(5.5f, 9.0f);
    }

    public static void NotifyKill(Hero hero, Entity victim)
    {
        DariusVoiceRuntime runtime = Ensure(hero);
        if (runtime == null || !runtime.IsLocalHero() || victim == null) return;
        int id;
        try { id = victim.GetInstanceID(); } catch { return; }
        if (runtime._recentVictims.Contains(id)) return;
        runtime._recentVictims.Add(id);
        runtime._recentVictimClearAt = Time.unscaledTime + 2.0f;

        float t = Time.unscaledTime;
        if (t - runtime._lastKillAt <= 10f) runtime._multiKillCount++;
        else runtime._multiKillCount = 1;
        runtime._lastKillAt = t;

        if (t < runtime._nextKillVoiceAt) return;
        bool played = false;
        if (runtime._multiKillCount >= 5) played = DariusMedia.PlayVoiceEvent(hero, "kill_penta", 0.88f);
        if (!played && !runtime._firstKillPlayed)
        {
            played = DariusMedia.PlayVoiceEvent(hero, "kill_first", 0.84f);
            runtime._firstKillPlayed = true;
        }
        if (!played) played = DariusMedia.PlayVoiceEvent(hero, "kill", 0.82f);
        if (played) runtime._nextKillVoiceAt = t + 2.4f;
    }

    public static void NotifyPMax(Hero hero)
    {
        DariusVoiceRuntime runtime = Ensure(hero);
        if (runtime == null || !runtime.IsLocalHero()) return;
        DariusMedia.PlayVoiceEvent(hero, "pmax", 0.86f);
    }
}
