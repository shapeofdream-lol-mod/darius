public sealed partial class DariusCombatController : MonoBehaviour
{
    // ==================== W: Crippling Strike ====================
    private void TryArmW()
    {
        if (Time.time < _wReadyAt) return;
        _wReadyAt = Time.time + W_COOLDOWN;
        _wArmed = true;
        _wArmUntil = Time.time + W_ARM_TIME;
        ClearLegacyWArmVfx();
        _legacyWArmVfx = DariusPrototypeVfx.CreateWArm(hero);
    }

    private void OnAttackHit(EventInfoAttackHit info)
    {
        if (hero == null || info.attacker != hero || !IsValidEnemy(info.victim))
            return;

        // Hemorrhage is an Essence: ordinary attacks also apply it while the Essence is active.
        if (_hemorrhageEnabled)
            ApplyHemorrhage(info.victim, 1);

        if (!_wArmed)
            return;

        _wArmed = false;
        ClearLegacyWArmVfx();
        float ad = hero.Status.finalStats.attackDamage;
        DealPhysical(info.victim, ad * W_BONUS_AD_RATIO);
        StartCoroutine(ApplySlow(info.victim, W_SLOW_STRENGTH, W_SLOW_DURATION));
        DariusPrototypeVfx.CreateWImpact(info.victim.transform.position);
    }

    // ==================== E: Apprehend ====================
    private void TryCastE()
    {
        if (Time.time < _eReadyAt) return;
        _eReadyAt = Time.time + E_COOLDOWN;

        Vector3 dir = GetAimDirection();
        Vector3 origin = hero.transform.position;
        Collider[] cols = Physics.OverlapSphere(origin, E_RANGE);
        HashSet<Entity> pulled = new HashSet<Entity>();
        int index = 0;

        for (int i = 0; i < cols.Length; i++)
        {
            Entity target = cols[i].GetComponentInParent<Entity>();
            if (!IsValidEnemy(target) || !pulled.Add(target))
                continue;

            Vector3 delta = Flatten(target.transform.position - origin);
            float dist = delta.magnitude;
            if (dist <= 0.001f || dist > E_RANGE)
                continue;

            float angle = Vector3.Angle(dir, delta.normalized);
            if (angle > E_HALF_ANGLE)
                continue;

            float side = ((index & 1) == 0 ? 1f : -1f) * (index / 2) * 0.25f;
            Vector3 perpendicular = new Vector3(-dir.z, 0f, dir.x);
            Vector3 destination = origin + dir * E_PULL_FRONT_DISTANCE + perpendicular * side;

            try { hero.Teleport(target, destination); }
            catch { target.transform.position = destination; }

            StartCoroutine(ApplySlow(target, E_SLOW_STRENGTH, E_SLOW_DURATION));
            index++;
        }

        DariusPrototypeVfx.CreateECone(origin, dir, E_RANGE, E_HALF_ANGLE);
    }

    // ==================== R: Noxian Guillotine ====================
    private void TryCastR()
    {
        if (Time.time < _rReadyAt) return;

        Entity target = FindTargetNearAim(R_RANGE);
        if (target == null)
            return;

        _rReadyAt = Time.time + R_COOLDOWN;
        int stacks = _hemorrhageEnabled ? GetBleedStacks(target) : 0;
        float ad = hero.Status.finalStats.attackDamage;
        float damage;

        if (_hemorrhageEnabled)
            damage = ad * R_BLEED_BASE_AD_RATIO * (1f + R_DAMAGE_PER_STACK * stacks);
        else
            damage = ad * R_NO_BLEED_AD_RATIO;

        Vector3 toward = Flatten(target.transform.position - hero.transform.position).normalized;
        if (toward.sqrMagnitude < 0.01f) toward = GetAimDirection();
        Vector3 landing = target.transform.position - toward * 1.0f;
        try { hero.Teleport(hero, landing); } catch { hero.transform.position = landing; }

        DariusPrototypeVfx.CreateRLeap(hero.transform.position, target.transform.position);
        DealPure(target, damage);

        bool killed = target == null || target.IsNullInactiveDeadOrKnockedOut() || target.currentHealth <= 0f;
        if (killed)
        {
            _rReadyAt = 0f; // Full reset in the prototype.
            DariusPrototypeVfx.CreateRReset(hero.transform.position);
        }
    }
}