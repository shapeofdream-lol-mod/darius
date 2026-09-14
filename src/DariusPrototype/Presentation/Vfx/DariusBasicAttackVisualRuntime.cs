// Hero_Darius basic-attack visual adapter.
// v0.18 no longer watches a foreign hero attack event and never scans the scene to delete
// another traveler's effects. Hero_Darius owns At_DariusAxe; its native
// AttackTrigger.OnCastStart hook calls NotifyNativeAttackStarted at the same point the stock
// combat pipeline starts an attack.
public sealed class DariusBasicAttackVisualRuntime : MonoBehaviour
{
    private Hero _hero;
    private bool _alternate;
    private float _lastImpactFxAt;

    public void Bind(Hero hero)
    {
        if (_hero == hero) return;
        Unbind();
        _hero = hero;
        if (_hero == null) return;
        try { _hero.ActorEvent_OnAttackHit += OnAttackHit; }
        catch (Exception e) { DariusLog.Exception("ATK-NATIVE", e, "subscribe ActorEvent_OnAttackHit failed"); }
        DariusLog.Info("ATK-NATIVE", "Darius native basic-attack visual adapter bound to " + DariusLog.EntityLabel(_hero));
    }

    public void NotifyNativeAttackStarted(int configIndex, CastInfo info)
    {
        if (_hero == null) return;

        // The range guide must follow the exact live AttackTrigger configuration. Never hard-code
        // a second gameplay radius here: if balance changes At_DariusAxe, the visual updates with it.
        float range = At_DariusAxe.AttackRange;
        try
        {
            At_DariusAxe attack = DariusTravelerRegistry.AttackPrefab;
            if (attack != null && attack.configs != null && configIndex >= 0 && configIndex < attack.configs.Length)
            {
                TriggerConfig cfg = attack.configs[configIndex];
                if (cfg != null)
                {
                    float effective = cfg.effectiveRange;
                    if (effective > 0.05f) range = effective;
                    else if (cfg.castMethod != null && cfg.castMethod._range > 0.05f) range = cfg.castMethod._range;
                }
            }
        }
        catch (Exception e)
        {
            DariusLog.Exception("ATK-RANGE-VFX", e, "Could not read live attack range; using At_DariusAxe.AttackRange fallback.");
        }

        Vector3 forward = _hero.transform.forward;
        try
        {
            DariusDirectionalBasicAttackState aim = _hero.GetComponent<DariusDirectionalBasicAttackState>();
            if (aim != null) forward = aim.GetDirection(forward);
            else forward = DariusDirectionalBasicAttackGeometry.ResolveAimDirection(_hero, info);
        }
        catch { }
        // Range-guide VFX intentionally disabled: gameplay reach includes target/contact geometry,
        // so a hard radial arc is both visually abrupt and misleading. Keep only the weapon swing.

        DariusCripplingStrikeRuntime w = _hero.GetComponent<DariusCripplingStrikeRuntime>();
        bool empowered = w != null && w.IsArmed;
        if (empowered)
        {
            w.NotifyNativeAttackStarted(forward);
            DariusLog.DebugInfo("ATK-NATIVE", "At_DariusAxe cast start -> W/Spell2 owns the swing animation; configuredRange=" + range.ToString("0.###"));
            return;
        }

        _alternate = !_alternate;
        DariusSkinAnimationHooks.PlayAttack(_hero, _alternate, configIndex == 1, forward);
        DariusPrototypeVfx.CreateBasicAttackSwing(_hero, _alternate);
        NetworkIdentity identity = _hero.GetComponent<NetworkIdentity>();
        TravelerBasicAttackVfxReplication.Broadcast(identity, 0, (byte)(_alternate ? 1 : 0), configIndex == 1,
            _hero.transform.position, forward);
        DariusLog.DebugInfo("ATK-NATIVE", "At_DariusAxe cast start -> " + (configIndex == 1 ? "Crit" : (_alternate ? "Attack2" : "Attack1")) +
            " + Darius axe trail/SFX; configuredRange=" + range.ToString("0.###"));
    }

    private void OnAttackHit(EventInfoAttackHit info)
    {
        if (_hero == null || info.attacker != _hero || info.victim == null) return;

        Vector3 delta = info.victim.transform.position - _hero.transform.position;
        delta.y = 0f;
        float contactDist = delta.magnitude, sectorAngle = -1f; Vector3 contact = info.victim.transform.position;
        try { DariusDirectionalBasicAttackGeometry.IsInsideAttackSector(_hero as Hero_Darius, info.victim, out contactDist, out sectorAngle, out contact); } catch { }
        DariusDirectionalBasicAttackState attackState = _hero.GetComponent<DariusDirectionalBasicAttackState>();
        DariusLog.Info("ATK-HIT", "victim=" + DariusLog.EntityLabel(info.victim) +
            " attacker=" + DariusLog.EntityLabel(_hero) +
            " centerDist=" + delta.magnitude.ToString("0.###") +
            " contactDist=" + contactDist.ToString("0.###") + " angle=" + sectorAngle.ToString("0.##") +
            " swingSerial=" + (attackState != null ? attackState.Serial.ToString() : "0"));

        if (Time.unscaledTime - _lastImpactFxAt < 0.11f) return;
        _lastImpactFxAt = Time.unscaledTime;
        try { DariusPrototypeVfx.CreateBasicAttackImpact(_hero, info.victim.transform.position); }
        catch (Exception e) { DariusLog.Exception("ATK-NATIVE", e, "basic attack impact VFX failed"); }
        TravelerBasicAttackVfxReplication.Broadcast(_hero.GetComponent<NetworkIdentity>(), 1, 0, false,
            info.victim.transform.position, Vector3.zero);
    }

    private void OnReplicatedTravelerBasicAttackVfx(TravelerBasicAttackVfxMessage message)
    {
        if (_hero == null) Bind(GetComponent<Hero>());
        if (_hero == null) return;
        if (message.phase == 0)
        {
            _alternate = message.variant != 0;
            DariusSkinAnimationHooks.PlayAttack(_hero, _alternate, message.critical != 0, message.direction);
            DariusPrototypeVfx.CreateBasicAttackSwing(_hero, _alternate);
        }
        else if (message.phase == 1) DariusPrototypeVfx.CreateBasicAttackImpact(_hero, message.position);
    }

    private void OnDestroy() { Unbind(); }

    private void Unbind()
    {
        if (_hero != null)
        {
            try { _hero.ActorEvent_OnAttackHit -= OnAttackHit; } catch { }
        }
        _hero = null;
    }
}
