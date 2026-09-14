public sealed partial class DariusEquipmentRuntime : MonoBehaviour
{
    private void RestartAnimals()
    {
        if (_hero == null) return;
        DariusAwooAudioRuntime audio = _hero.GetComponent<DariusAwooAudioRuntime>();
        if (audio == null) audio = _hero.gameObject.AddComponent<DariusAwooAudioRuntime>();
        audio.Restart(_hero, _awooPitch);
    }

    private void NotifyEquipmentKillIfNeeded(Entity victim, string source)
    {
        if (victim == null || _hero == null) return;
        bool killed = victim.IsNullInactiveDeadOrKnockedOut() || victim.currentHealth <= 0f;
        if (killed) DariusConstellationRuntime.NotifyKill(_hero, victim, source);
    }

    private void Update()
    {
        if (!NetworkServer.active || _hero == null || _hero.Status == null) return;
        float hpBefore = _lastHealth;
        float hpNow = _hero.currentHealth;
        if (hpBefore < 0f) hpBefore = hpNow;
        if (hpNow + 0.01f < hpBefore) OnOwnerDamaged(hpBefore - hpNow);

        if (_shojinStacks > 0 && Time.time >= _shojinExpiresAt)
        {
            _shojinStacks = 0;
            _lastShojinSource = null;
        }
        TickDeathDance();
        TickBlackCleaver();
        TickMovement();
        RefreshBloodmail(false);
        RefreshYoumuu(false);
        CheckAwooRoom();

        // Capture after all self-heal/deferred-damage effects so their own corrections are not
        // mistaken for a second incoming hit next frame.
        _lastHealth = _hero.currentHealth;
    }

    private void OnDestroy()
    {
        if (_attackSubscribed && _hero != null)
        {
            try { _hero.ActorEvent_OnAttackHit -= OnAttackHit; } catch { }
        }
        _attackSubscribed = false;
        ClearBlackCleaver();
        RemoveSterakBarrier();
        if (_sterakBarrierRoutine != null) { try { StopCoroutine(_sterakBarrierRoutine); } catch { } }
        if (_bloodmailBonus != null && _hero != null && _hero.Status != null) { try { _hero.Status.RemoveStatBonus(_bloodmailBonus); } catch { } }
        if (_youmuuBonus != null && _hero != null && _hero.Status != null) { try { _hero.Status.RemoveStatBonus(_youmuuBonus); } catch { } }
        _bloodmailBonus = null;
        _youmuuBonus = null;
        _deathDance.Clear();
        _sunderedReady.Clear();
        _recentKills.Clear();
    }
}