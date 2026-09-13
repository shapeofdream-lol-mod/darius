using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;

public sealed class DariusCombatController : MonoBehaviour
{
    // ===== Prototype balance =====
    public const float Q_COOLDOWN = 6.0f;
    public const float Q_WINDUP = 0.75f;
    public const float Q_INNER_RADIUS = 2.40f;
    public const float Q_OUTER_RADIUS = 4.25f;
    public const float Q_BASE_AD_RATIO = 1.20f;
    public const float Q_NO_BLEED_OUTER_AD_RATIO = 1.80f;
    public const float Q_HEAL_MAX_HP_PER_TARGET = 0.10f;
    public const float Q_HEAL_CAP_MAX_HP = 0.50f;

    public const float W_COOLDOWN = 5.0f;
    public const float W_ARM_TIME = 4.0f;
    public const float W_BONUS_AD_RATIO = 0.75f; // 175% total attack equivalent.
    public const float W_SLOW_STRENGTH = 0.90f;
    public const float W_SLOW_DURATION = 1.25f;

    public const float E_COOLDOWN = 10.0f;
    public const float E_RANGE = 5.35f;
    public const float E_HALF_ANGLE = 30.0f;
    public const float E_PULL_FRONT_DISTANCE = 1.35f;
    public const float E_SLOW_STRENGTH = 0.40f;
    public const float E_SLOW_DURATION = 1.25f;

    public const float R_COOLDOWN = 24.0f;
    public const float R_RANGE = 6.0f;
    public const float R_NO_BLEED_AD_RATIO = 1.575f; // Pass1 hotfix2: -25% legacy harness parity.
    public const float R_BLEED_BASE_AD_RATIO = 1.05f;
    public const float R_DAMAGE_PER_STACK = 0.20f; // +20% multiplicative per stack; 5 stacks = 200% of base.

    public const int BLEED_MAX_STACKS = 5;
    public const float BLEED_DURATION = 5.0f;
    public const float BLEED_TICK_INTERVAL = 1.0f;
    public const float BLEED_AD_RATIO_PER_STACK_PER_TICK = 0.05f;
    public const float NOXIAN_MIGHT_DURATION = 5.0f;
    public const float NOXIAN_MIGHT_AD_PERCENT = 50.0f;

    public Hero hero { get; private set; }

    private bool _initialized;
    private bool _hemorrhageEnabled;
    private bool _wArmed;
    private float _wArmUntil;
    private float _qReadyAt;
    private float _wReadyAt;
    private float _eReadyAt;
    private float _rReadyAt;
    private float _noxianMightUntil;
    private StatBonus _noxianMightBonus;
    private GameObject _legacyWArmVfx;
    private GameObject _legacyNoxianMightVfx;

    private readonly Dictionary<Entity, BleedState> _bleeds = new Dictionary<Entity, BleedState>();
    private readonly List<Entity> _scratchEntities = new List<Entity>(64);

    private ClientEventManager _clientEvents;

    private sealed class BleedState
    {
        public int stacks;
        public float expireAt;
        public float nextTickAt;
    }

    public void Initialize(Hero targetHero)
    {
        if (targetHero == null)
            return;

        if (_initialized && hero == targetHero)
            return;

        CleanupSubscriptions();
        hero = targetHero;
        _initialized = true;

        _clientEvents = NetworkedManagerBase<ClientEventManager>.instance;
        if (_clientEvents != null)
            _clientEvents.OnAttackHit += OnAttackHit;

        DariusLog.Info("LEGACY", "Legacy hotkey controller attached to " + DariusLog.EntityLabel(hero));
    }

    private void OnDestroy()
    {
        CleanupSubscriptions();
        ClearLegacyWArmVfx();
        RemoveNoxianMight();
    }

    private void CleanupSubscriptions()
    {
        if (_clientEvents != null)
        {
            try { _clientEvents.OnAttackHit -= OnAttackHit; } catch { }
        }
        _clientEvents = null;
    }

    private void Update()
    {
        if (!_initialized || hero == null || hero.IsNullOrInactive())
            return;

        // Legacy F5-F9 direct-cast controls are intentionally disabled in v0.13.
        // Normal gameplay uses independently equipable Memories and the Identity slot.

        if (_wArmed && Time.time > _wArmUntil)
        {
            _wArmed = false;
            ClearLegacyWArmVfx();
        }

        UpdateBleeds();
        UpdateNoxianMight();
    }

    // ==================== Q: Decimate ====================
    private void TryCastQ()
    {
        if (Time.time < _qReadyAt) return;
        _qReadyAt = Time.time + Q_COOLDOWN;
        StartCoroutine(CastQ());
    }

    private IEnumerator CastQ()
    {
        // Important: movement is deliberately NOT locked during windup.
        DariusPrototypeVfx.CreateQTelegraph(hero.transform, Q_INNER_RADIUS, Q_OUTER_RADIUS, Q_WINDUP);
        yield return new WaitForSeconds(Q_WINDUP);

        if (hero == null || hero.IsNullOrInactive())
            yield break;

        // Soul of the skill: exactly ONE overlap query and ONE resolution per target.
        HashSet<Entity> hit = new HashSet<Entity>();
        Collider[] cols = Physics.OverlapSphere(hero.transform.position, Q_OUTER_RADIUS);
        int outerHits = 0;

        for (int i = 0; i < cols.Length; i++)
        {
            Entity target = cols[i].GetComponentInParent<Entity>();
            if (!IsValidEnemy(target) || !hit.Add(target))
                continue;

            float distance = PlanarDistance(hero.transform.position, target.transform.position);
            bool isInner = distance <= Q_INNER_RADIUS;
            bool isOuter = !isInner && distance <= Q_OUTER_RADIUS;
            if (!isInner && !isOuter)
                continue;

            float ad = hero.Status.finalStats.attackDamage;
            float ratio;

            if (isInner)
            {
                ratio = Q_BASE_AD_RATIO;
            }
            else if (_hemorrhageEnabled)
            {
                // Legacy diagnostic path mirrors the formal skill: Hemorrhage removes the outer damage bonus but keeps outer healing.
                ratio = Q_BASE_AD_RATIO;
                ApplyHemorrhage(target, 1);
                outerHits++;
            }
            else
            {
                ratio = Q_NO_BLEED_OUTER_AD_RATIO;
                outerHits++;
            }

            DealPhysical(target, ad * ratio);
            DariusPrototypeVfx.CreateHitFlash(hero, target.transform.position, isOuter);
        }

        if (outerHits > 0)
        {
            float healFraction = Mathf.Min(Q_HEAL_CAP_MAX_HP, Q_HEAL_MAX_HP_PER_TARGET * outerHits);
            HealSelf(hero.Status.maxHealth * healFraction);
        }

        DariusPrototypeVfx.CreateQSwing(hero, hero.transform.position, Q_INNER_RADIUS, Q_OUTER_RADIUS);
    }

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

    // ==================== Hemorrhage Essence ====================
    private void ToggleHemorrhage()
    {
        _hemorrhageEnabled = !_hemorrhageEnabled;
        if (!_hemorrhageEnabled)
        {
            _bleeds.Clear();
            RemoveNoxianMight();
        }
        DariusLog.Info("LEGACY", "Hemorrhage Essence simulation toggled: " + (_hemorrhageEnabled ? "ON" : "OFF"));
        DariusPrototypeVfx.CreateEssenceToggle(hero.transform.position, _hemorrhageEnabled);
    }

    private void ApplyHemorrhage(Entity target, int normalStacks)
    {
        if (!_hemorrhageEnabled || !IsValidEnemy(target))
            return;

        int stacksToApply = HasNoxianMight() ? BLEED_MAX_STACKS : normalStacks;
        BleedState state;
        if (!_bleeds.TryGetValue(target, out state))
        {
            state = new BleedState { stacks = 0, nextTickAt = Time.time + BLEED_TICK_INTERVAL };
            _bleeds[target] = state;
        }

        state.stacks = Mathf.Clamp(state.stacks + stacksToApply, 1, BLEED_MAX_STACKS);
        if (HasNoxianMight()) state.stacks = BLEED_MAX_STACKS;
        state.expireAt = Time.time + BLEED_DURATION;

        if (state.stacks >= BLEED_MAX_STACKS)
            GrantNoxianMight();

        DariusPrototypeVfx.CreateBleedStack(target.transform, state.stacks);
    }

    private void UpdateBleeds()
    {
        if (_bleeds.Count == 0 || hero == null)
            return;

        _scratchEntities.Clear();
        foreach (KeyValuePair<Entity, BleedState> pair in _bleeds)
        {
            Entity target = pair.Key;
            BleedState state = pair.Value;
            if (target == null || target.IsNullInactiveDeadOrKnockedOut() || Time.time >= state.expireAt)
            {
                _scratchEntities.Add(target);
                continue;
            }

            if (Time.time >= state.nextTickAt)
            {
                state.nextTickAt += BLEED_TICK_INTERVAL;
                float ad = hero.Status.finalStats.attackDamage;
                DealPhysical(target, ad * BLEED_AD_RATIO_PER_STACK_PER_TICK * state.stacks);
                DariusPrototypeVfx.CreateBleedTick(target.transform.position, state.stacks);
            }
        }

        for (int i = 0; i < _scratchEntities.Count; i++)
            _bleeds.Remove(_scratchEntities[i]);
    }

    private int GetBleedStacks(Entity target)
    {
        BleedState state;
        if (target != null && _bleeds.TryGetValue(target, out state) && Time.time < state.expireAt)
            return state.stacks;
        return 0;
    }

    private void GrantNoxianMight()
    {
        _noxianMightUntil = Time.time + NOXIAN_MIGHT_DURATION;

        if (_noxianMightBonus == null)
        {
            _noxianMightBonus = new StatBonus();
            _noxianMightBonus.attackDamagePercentage = NOXIAN_MIGHT_AD_PERCENT;
            try { hero.Status.AddStatBonus(_noxianMightBonus); } catch { }
        }

        if (_legacyNoxianMightVfx == null)
            _legacyNoxianMightVfx = DariusPrototypeVfx.CreateNoxianMight(hero.transform);
    }

    private bool HasNoxianMight()
    {
        return _hemorrhageEnabled && Time.time < _noxianMightUntil;
    }

    private void UpdateNoxianMight()
    {
        if (_noxianMightBonus != null && Time.time >= _noxianMightUntil)
            RemoveNoxianMight();
    }

    private void RemoveNoxianMight()
    {
        if (_noxianMightBonus != null && hero != null)
        {
            try { hero.Status.RemoveStatBonus(_noxianMightBonus); } catch { }
        }
        if (_legacyNoxianMightVfx != null)
        {
            try { UnityEngine.Object.Destroy(_legacyNoxianMightVfx); } catch { }
            _legacyNoxianMightVfx = null;
        }
        _noxianMightBonus = null;
        _noxianMightUntil = 0f;
    }

    private void ClearLegacyWArmVfx()
    {
        if (_legacyWArmVfx != null)
        {
            try { UnityEngine.Object.Destroy(_legacyWArmVfx); } catch { }
            _legacyWArmVfx = null;
        }
    }

    // ==================== Helpers ====================
    private bool IsValidEnemy(Entity target)
    {
        if (hero == null || target == null || target == hero) return false;
        if (target.IsNullInactiveDeadOrKnockedOut()) return false;
        try { return hero.GetRelation(target).HasFlag(EntityRelation.Enemy); }
        catch { return target is Monster; }
    }

    private void DealPhysical(Entity target, float amount)
    {
        if (target == null || amount <= 0f) return;
        try { hero.DealDamage(hero.PhysicalDamage(amount, 1f), target); }
        catch
        {
            DamageData data = new DamageData(DamageData.SourceType.Physical, amount, 1f);
            data.SetActor(hero);
            data.Dispatch(target);
        }
    }

    private void DealPure(Entity target, float amount)
    {
        if (target == null || amount <= 0f) return;
        // PureDamage is exposed by the public Actor API.
        try { hero.DealDamage(hero.PureDamage(amount, 1f), target); }
        catch
        {
            // Fallback keeps the prototype functional if the exact PureDamage signature changed.
            DamageData data = hero.CreateDamage(DamageData.SourceType.Physical, amount, 1f);
            data.SetActor(hero);
            data.Dispatch(target);
        }
    }

    private void HealSelf(float amount)
    {
        if (amount <= 0f || hero == null) return;
        HealData heal = new HealData(amount);
        heal.SetActor(hero);
        hero.DoHeal(heal, hero);
    }

    private IEnumerator ApplySlow(Entity target, float strength, float duration)
    {
        if (target == null || target.Status == null) yield break;
        StatBonus slow = new StatBonus();
        slow.movementSpeedPercentage = -Mathf.Abs(strength * 100f);
        try { target.Status.AddStatBonus(slow); } catch { yield break; }
        yield return new WaitForSeconds(duration);
        if (target != null && target.Status != null)
        {
            try { target.Status.RemoveStatBonus(slow); } catch { }
        }
    }

    private Entity FindTargetNearAim(float range)
    {
        Vector3 origin = hero.transform.position;
        Vector3 aim = GetAimPoint();
        Collider[] cols = Physics.OverlapSphere(origin, range);
        Entity best = null;
        float bestScore = float.MaxValue;
        HashSet<Entity> seen = new HashSet<Entity>();

        for (int i = 0; i < cols.Length; i++)
        {
            Entity target = cols[i].GetComponentInParent<Entity>();
            if (!IsValidEnemy(target) || !seen.Add(target)) continue;
            float score = PlanarDistance(target.transform.position, aim);
            if (score < bestScore)
            {
                bestScore = score;
                best = target;
            }
        }
        return best;
    }

    private Vector3 GetAimDirection()
    {
        Vector3 delta = Flatten(GetAimPoint() - hero.transform.position);
        if (delta.sqrMagnitude < 0.001f)
            delta = Flatten(hero.transform.forward);
        return delta.normalized;
    }

    private Vector3 GetAimPoint()
    {
        Camera cam = Camera.main;
        if (cam == null) return hero.transform.position + hero.transform.forward * 4f;

        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        Plane plane = new Plane(Vector3.up, hero.transform.position);
        float enter;
        if (plane.Raycast(ray, out enter))
            return ray.GetPoint(enter);
        return hero.transform.position + hero.transform.forward * 4f;
    }

    private static Vector3 Flatten(Vector3 v) { v.y = 0f; return v; }
    private static float PlanarDistance(Vector3 a, Vector3 b) { return Flatten(a - b).magnitude; }
}
