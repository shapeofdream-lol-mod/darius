using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;

// Shape of Dreams - Darius / Hand of Noxus Traveler.
// Release runtime: independent Hero_Darius, Darius-owned skin/attack resources,
// League-authentic presentation assets, native constellation integration, and persistent
// scene-transition self-healing.

public sealed class DariusPrototypeMod : ModBehaviour
{
    [ModConfig.LabelText("Audio Volume / 技能音效音量")]
    public DariusAudioVolumeConfig skillAudioVolume = new DariusAudioVolumeConfig();

    public override void OnConfigChanged()
    {
        DariusAudioSettingsRuntime.Bind(skillAudioVolume);
        Debug.Log("[DariusAudio] config applied q=" + skillAudioVolume.qVolume.ToString("0.##") + " w=" + skillAudioVolume.wVolume.ToString("0.##") + " e=" + skillAudioVolume.eVolume.ToString("0.##") + " r=" + skillAudioVolume.rVolume.ToString("0.##") + " basic=" + skillAudioVolume.basicAttackVolume.ToString("0.##") + " dodge=" + skillAudioVolume.dodgeVolume.ToString("0.##") + " voice=" + skillAudioVolume.voiceVolume.ToString("0.##"));
    }

    private static bool _bootstrapped;
    private static bool _applicationQuitting;
    private static bool _shutdownCompleted;
    private DariusDiagnostics _diagnostics;

    private void LateUpdate() { DariusAudioSettingsRuntime.Bind(skillAudioVolume); }

    private void Awake()
    {
        // Workshop ModBehaviour instances can exist a frame before Start(). Resolve the loader-provided
        // ModItem.path here so every later icon/model/audio lookup already knows the numeric Workshop root.
        // Configure() is intentionally safe when instance/mod is not populated yet; Start() retries it.
        DariusModEnvironment.Configure(this);
        DariusAudioSettingsRuntime.Bind(skillAudioVolume);
        DariusLog.Initialize();
        TravelerBasicAttackVfxReplication.Initialize();
        try
        {
            if (DewResources.database != null && !string.IsNullOrEmpty(DariusModEnvironment.Root))
                DariusTravelerRegistry.EnsureCoreRegisteredForLookup("Mod Awake synchronous Workshop bootstrap");
        }
        catch (Exception e)
        {
            // Unity may invoke Awake before every stock resource service is fully usable. This is a
            // best-effort head start only; Start() and InitializeWhenReady() retry the same bootstrap.
            DariusLog.Exception("TRAVELER-EARLY", e, "Awake Workshop bootstrap deferred to Start/coroutine");
        }
    }

    private void Start()
    {
        // Re-read ModItem.path now that the loader has completed the ModBehaviour contract.
        DariusModEnvironment.Configure(this);
        DariusAudioSettingsRuntime.Bind(skillAudioVolume);
        DariusLog.Initialize();
        DariusLog.Info("BOOT", "DariusPrototype BUILD=v0.30.6-final mecha-vfx-visual-hotfix3; Start() entered. bootstrapped=" + _bootstrapped +
            " modRoot=" + (DariusModEnvironment.Root ?? "<null>") + " source=" + DariusModEnvironment.SourceLabel);
        _diagnostics = gameObject.GetComponent<DariusDiagnostics>();
        if (_diagnostics == null) _diagnostics = gameObject.AddComponent<DariusDiagnostics>();
        _diagnostics.Initialize();
        DariusLog.Info("BOOT", "Initial snapshot: " + DariusDiagnostics.Snapshot());

        instance.isAlteringGameplay = true;
        if (!_bootstrapped)
        {
            // A presentation-only Harmony patch must never prevent Hero_Darius from registering.
            // RC2 aborted Start() here when StarList.Refresh gained an overload, which made the
            // entire Traveler disappear even though the DLL itself compiled correctly.
            try
            {
                harmony.PatchAll();
                DariusLog.Info("PATCH", "Harmony PatchAll completed.");
            }
            catch (Exception e)
            {
                DariusLog.Exception("PATCH", e, "Harmony PatchAll reported a failure; continuing critical Darius boot so the Traveler can still register");
            }

            try
            {
                DariusModLifecycle.Install(harmony);
            }
            catch (Exception e)
            {
                DariusLog.Exception("MOD-LIFECYCLE", e, "DewMod.UnloadAll lifecycle guard install failed; continuing boot");
            }

            try
            {
                DariusRuntimeResourceCompatibility.Install(harmony);
            }
            catch (Exception e)
            {
                DariusLog.Exception("PATCH", e, "Runtime resource compatibility install failed; continuing boot for diagnostics/self-heal");
            }

            try
            {
                DariusDejaVuRegistry.InstallLocalizationPatches(harmony);
            }
            catch (Exception e)
            {
                DariusLog.Exception("PATCH", e, "Localization patch install failed; continuing boot");
            }

            try
            {
                DariusRInputGuard.Install(harmony);
            }
            catch (Exception e)
            {
                DariusLog.Exception("PATCH", e, "R input guard install failed; continuing boot");
            }

            _bootstrapped = true;
        }
        else
        {
            DariusLog.Info("BOOT", "Scene-created ModBehaviour detected; keeping existing Harmony/runtime registrations instead of duplicating them.");
        }

        // If Dew's resource database is already online, synchronously install the minimum Hero/Skin
        // resource bridge before lobby/mastery UI gets another frame to resolve persisted Hero_Darius.
        // If it is not ready yet, InitializeWhenReady below performs the same operation as soon as it is.
        try
        {
            if (DewResources.database != null)
                DariusTravelerRegistry.EnsureCoreRegisteredForLookup("Mod Start synchronous Workshop bootstrap");
        }
        catch (Exception e)
        {
            DariusLog.Exception("TRAVELER-EARLY", e, "Synchronous Workshop Hero/Skin bootstrap failed; coroutine fallback remains active");
        }

        // Media loading comes after ModItem.path has been resolved and after the resource lookup guard
        // is installed. This prevents numeric Workshop folders from falling back to local Mods paths.
        try
        {
            DariusMedia.PreloadAll();
            StartCoroutine(DariusMedia.PreloadCompressedAudio());
        }
        catch (Exception e) { DariusLog.Exception("MEDIA", e, "PreloadAll/PreloadCompressedAudio failed"); }

        // Always start the critical registration coroutine even when an optional Harmony/UI patch
        // failed. This is the authoritative path that creates Hero_Darius/Skin_Darius_Default.
        StartCoroutine(DariusTravelerRegistry.InitializeWhenReady());
        DariusLog.Info("BOOT", "Registration/repair coroutine started. Target=independent Hero_Darius, native Hero/Skin/Loadout/Profile/Mirror paths.");
    }

    private void OnApplicationQuit()
    {
        _applicationQuitting = true;
        try { DariusConstellationPersistence.SaveCurrent(DewSave.profileMain, "application quit"); } catch { }
        ShutdownRuntimeResources("application quit");
    }

    private void OnDestroy()
    {
        if (_diagnostics != null) Destroy(_diagnostics);

        // Save before *any* scene or mod lifecycle destruction. A profile Validate can run during
        // the next transition/reload before the replacement dynamic assembly has re-registered its
        // StarEffects; without this snapshot newly purchased custom stars could be refunded.
        try { DariusConstellationPersistence.SaveCurrent(DewSave.profileMain, "ModBehaviour OnDestroy pre-cleanup"); } catch { }

        bool trueModUnload = DariusModLifecycle.IsModManagerUnloading;
        if (_applicationQuitting || !Application.isPlaying || trueModUnload)
        {
            ShutdownRuntimeResources(trueModUnload ? "DewMod.UnloadAll hot reload" : "real shutdown");
        }
        else
        {
            DariusLog.Info("BOOT", "Ordinary scene lifecycle destroyed ModBehaviour; preserving persistent Darius resources. Constellation state was snapshotted first.");
        }
    }

    private void ShutdownRuntimeResources(string reason)
    {
        if (_shutdownCompleted) return;
        _shutdownCompleted = true;
        DariusLog.Info("BOOT", "Cleaning Darius runtime resources: " + reason);
        DariusTravelerRegistry.UnregisterRuntimeOnly();
        DariusFormalRegistry.Unregister();
        try { harmony.UnpatchAll(harmony.Id); } catch { }
        _bootstrapped = false;
        DariusLog.Flush();
        if (_applicationQuitting || !Application.isPlaying) DariusLog.Shutdown();
    }
}

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

public static class DariusPrototypeVfx
{
    private static bool GodKing(Hero owner)
    {
        try { return owner != null && DariusSkinAnimationHooks.IsGodKing(owner); } catch { return false; }
    }

    private static Color SkinColor(Hero owner, Color classicColor, Color godKingColor)
    {
        return GodKing(owner) ? godKingColor : classicColor;
    }

    private static string SkinTexture(Hero owner, string classicKey, string godKingKey)
    {
        return GodKing(owner) ? godKingKey : classicKey;
    }

    private static string SkinFxName(Hero owner, string suffix)
    {
        return (GodKing(owner) ? "Darius_GodKing_" : "Darius_Classic_") + suffix;
    }

    private static string VariantKey(Hero owner)
    {
        try { return DariusSkinAnimationHooks.GetVariantKey(owner) ?? "Classic"; }
        catch { return "Classic"; }
    }

    private static bool Dunkmaster(Hero owner)
    {
        return string.Equals(VariantKey(owner), "Dunkmaster", StringComparison.OrdinalIgnoreCase);
    }

    private static bool Mecha(Hero owner)
    {
        return string.Equals(VariantKey(owner), "Mecha", StringComparison.OrdinalIgnoreCase);
    }

    private static string LolSystem(Hero owner, string classicName, string godKingName, string dunkmasterName = null, string mechaName = null)
    {
        string variant = VariantKey(owner);
        if (string.Equals(variant, "GodKing", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(godKingName)) return godKingName;
        if (string.Equals(variant, "Dunkmaster", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(dunkmasterName)) return dunkmasterName;
        if (string.Equals(variant, "Mecha", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(mechaName)) return mechaName;
        return classicName;
    }

    private static Quaternion LolFacing(Vector3 direction)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f) return Quaternion.identity;
        return Quaternion.LookRotation(direction.normalized, Vector3.up);
    }

    private static Material NewMaterial(Color color, Texture texture = null)
    {
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Transparent");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        if (shader == null)
            throw new InvalidOperationException("No compatible unlit shader found for Darius VFX.");
        Material m = new Material(shader);
        m.name = "DariusVfxMaterial";
        m.color = color;
        if (texture != null) m.mainTexture = texture;
        m.renderQueue = 3000;
        return m;
    }

    private static Mesh MakeQuad(bool vertical)
    {
        Mesh mesh = new Mesh();
        mesh.name = vertical ? "DariusVfxVerticalQuad" : "DariusVfxGroundQuad";
        if (vertical)
        {
            mesh.vertices = new Vector3[]
            {
                new Vector3(-0.5f,-0.5f,0f), new Vector3(0.5f,-0.5f,0f),
                new Vector3(0.5f,0.5f,0f), new Vector3(-0.5f,0.5f,0f)
            };
        }
        else
        {
            mesh.vertices = new Vector3[]
            {
                new Vector3(-0.5f,0f,-0.5f), new Vector3(0.5f,0f,-0.5f),
                new Vector3(0.5f,0f,0.5f), new Vector3(-0.5f,0f,0.5f)
            };
        }
        mesh.uv = new Vector2[] { new Vector2(0,0), new Vector2(1,0), new Vector2(1,1), new Vector2(0,1) };
        mesh.triangles = new int[] { 0,2,1, 0,3,2 };
        mesh.RecalculateBounds();
        mesh.RecalculateNormals();
        return mesh;
    }

    private static GameObject TexturedQuad(string name, string textureKey, Vector3 position, Vector3 scale,
        float lifetime, Color color, bool vertical = false, Transform follow = null, float rotateSpeed = 0f, Quaternion? rotation = null)
    {
        Texture2D texture = DariusMedia.Texture(textureKey);
        if (texture == null) return null;
        GameObject go = new GameObject(name);
        go.transform.position = position;
        go.transform.rotation = rotation ?? Quaternion.identity;
        MeshFilter mf = go.AddComponent<MeshFilter>();
        mf.sharedMesh = MakeQuad(vertical);
        MeshRenderer mr = go.AddComponent<MeshRenderer>();
        mr.material = NewMaterial(color, texture);
        DariusVfxQuadMotion motion = go.AddComponent<DariusVfxQuadMotion>();
        motion.lifetime = lifetime;
        motion.startScale = scale;
        motion.endScale = scale * (vertical ? 1.12f : 1.06f);
        motion.rotateDegreesPerSecond = rotateSpeed;
        motion.follow = follow;
        if (follow != null) motion.followOffset = position - follow.position;
        motion.billboard = vertical;
        go.transform.localScale = scale;
        return go;
    }

    private static GameObject PersistentTexturedQuad(string name, string textureKey, Transform follow, Vector3 offset,
        Vector3 scale, Color color, bool vertical, float rotateSpeed, float pulseAmount, float pulseSpeed)
    {
        Texture2D texture = DariusMedia.Texture(textureKey);
        if (texture == null || follow == null) return null;
        GameObject go = new GameObject(name);
        go.transform.position = follow.position + offset;
        MeshFilter mf = go.AddComponent<MeshFilter>();
        mf.sharedMesh = MakeQuad(vertical);
        MeshRenderer mr = go.AddComponent<MeshRenderer>();
        mr.material = NewMaterial(color, texture);
        DariusPersistentVfxPulse pulse = go.AddComponent<DariusPersistentVfxPulse>();
        pulse.follow = follow;
        pulse.followOffset = offset;
        pulse.baseScale = scale;
        pulse.rotateDegreesPerSecond = rotateSpeed;
        pulse.pulseAmount = pulseAmount;
        pulse.pulseSpeed = pulseSpeed;
        pulse.billboard = vertical;
        go.transform.localScale = scale;
        return go;
    }

    public static void CreateBasicAttackRangeArc(Hero owner, Vector3 forward, float range)
    {
        if (owner == null || range <= 0.05f) return;
        forward.y = 0f;
        if (forward.sqrMagnitude <= 0.0001f) forward = owner.transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude <= 0.0001f) forward = Vector3.forward;
        forward.Normalize();

        // Shape of Dreams' melee readability uses a curved ground sector. The sector angle is purely
        // presentation; every outer point is exactly `range` metres from the Hero, so its radial edge
        // can never claim more or less reach than At_DariusAxe's live trigger configuration.
        const float halfAngle = 55f;
        const int arcSegments = 18;
        GameObject go = new GameObject(SkinFxName(owner, "BasicAttack_RangeArc"));
        LineRenderer line = go.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.loop = false;
        line.positionCount = arcSegments + 3;
        line.startWidth = 0.055f;
        line.endWidth = 0.055f;
        line.numCornerVertices = 3;
        line.numCapVertices = 2;
        Color c = SkinColor(owner, new Color(0.94f, 0.42f, 0.34f, 0.72f), new Color(1.00f, 0.63f, 0.16f, 0.82f));
        line.startColor = c;
        line.endColor = new Color(c.r, c.g, c.b, 0.42f);
        line.material = NewMaterial(c);

        Vector3 center = owner.transform.position + Vector3.up * 0.07f;
        line.SetPosition(0, center);
        for (int i = 0; i <= arcSegments; i++)
        {
            float t = i / (float)arcSegments;
            float angle = Mathf.Lerp(-halfAngle, halfAngle, t);
            Vector3 dir = Quaternion.Euler(0f, angle, 0f) * forward;
            line.SetPosition(i + 1, center + dir * range);
        }
        line.SetPosition(arcSegments + 2, center);
        UnityEngine.Object.Destroy(go, 0.30f);
        DariusLog.DebugInfoThrottled("ATK-RANGE-VFX", "arc",
            "Basic attack ground sector rendered with exact radial range=" + range.ToString("0.###") +
            "m halfAngle=" + halfAngle.ToString("0.#"), 1.0);
    }

    public static void CreateBasicAttackSwing(Hero owner, bool alternate)
    {
        if (owner == null) return;
        // Pass 5 removes the old hand-authored axe trail when an exact Riot skin system exists.
        // Classic/God-King have no separate champion BA system in the supplied current source, so
        // their model animation + exact Wwise attack SFX are left unmodified rather than fabricated.
        if (Dunkmaster(owner))
            DariusLolVfxRuntime.PlayAttached(owner, "Darius_Skin04_BA", owner.transform);
        else if (Mecha(owner))
            DariusLolVfxRuntime.PlayAttached(owner, alternate ? "Darius_Skin67_BA02" : "Darius_Skin67_BA01", owner.transform);
        DariusMedia.PlayForSkin("basic_attack", owner, DariusAudioChannel.BasicAttack, alternate ? 0.42f : 0.36f);
    }

    public static void CreateBasicAttackImpact(Hero owner, Vector3 p)
    {
        // No generic hand-made impact flash. The original LoL champion banks used by this package
        // do not expose a universal Darius basic-hit target system, so absence is preferred to a
        // fabricated substitute. Hit SFX still routes through the skin-specific audio bank.
    }

    public static void CreateBasicAttackImpact(Vector3 p) { CreateBasicAttackImpact(null, p); }

    public static void CreateQWindupTrail(Hero owner)
    {
        // Weapon-attached motion is authored inside the converted Riot system; do not layer a hand-made trail.
        if (owner == null) return;
        DariusLog.DebugInfo("LOL-VFX-BRIDGE", "Q windup uses converted Riot particle system; manual weapon trail suppressed.");
    }

    public static void CreateWAttackSwing(Hero owner)
    {
        if (owner == null) return;
        string system = LolSystem(owner, "darius_Base_W_weapon_02", "Darius_Skin15_W_Cast", "Darius_Skin04_W_Weapon_Child", "Darius_Skin67_W_Cast");
        Transform anchor = DariusSkinAnimationHooks.GetWeaponAnchor(owner);
        if (anchor == null) anchor = owner.transform;
        DariusLolVfxRuntime.PlayAttached(owner, system, anchor);
    }

    public static void CreateEWindup(Hero owner)
    {
        if (owner == null) return;
        Transform anchor = DariusSkinAnimationHooks.GetWeaponAnchor(owner);
        if (anchor == null) anchor = owner.transform;
        DariusLolVfxRuntime.PlayAttached(owner, LolSystem(owner, "darius_Base_E_weapon_trigger", "Darius_Skin15_E_Cast", null, "Darius_Skin67_E_Cast"), anchor);
        DariusMedia.PlayForSkin("e_cast", owner, 0.82f);
        DariusMedia.PlaySkillVoice(owner, "e", DariusAudioChannel.E, 0.78f);
    }

    public static void CreateRWindup(Hero owner)
    {
        if (owner == null) return;
        Transform weapon = DariusSkinAnimationHooks.GetWeaponAnchor(owner);
        if (weapon == null) weapon = owner.transform;
        DariusLolVfxRuntime.PlayAttached(owner, LolSystem(owner, "darius_Base_R_cast_axe", "Darius_Skin15_R_cast_axe", null, "Darius_Skin67_R_CastAxe"), weapon);
        if (GodKing(owner))
        {
            // Both are direct conversions from Skin15. The visible lunging beast itself is the restored
            // Wolf_Mat SKN submesh animated by the original Spell4 lion-bone animation.
            DariusLolVfxRuntime.PlayAttached(owner, "Darius_Skin15_R_Cast_Wolf", owner.transform);
            DariusLolVfxRuntime.PlayAttached(owner, "Darius_Skin15_R_Trail", weapon);
        }
        else if (Mecha(owner))
        {
            DariusLolVfxRuntime.PlayAttached(owner, "Darius_Skin67_R_CastWolf", owner.transform);
            DariusLolVfxRuntime.PlayAttached(owner, "Darius_Skin67_R_Trail", weapon);
        }
        DariusMedia.PlayForSkin("r_cast", owner, 0.96f);
        DariusMedia.PlaySkillVoice(owner, "r", DariusAudioChannel.R, 0.82f);
    }

    private static LineRenderer FallbackRing(string name, Vector3 center, float radius, Color color, float width, float lifetime)
    {
        GameObject go = new GameObject(name);
        go.transform.position = center + Vector3.up * 0.08f;
        LineRenderer line = go.AddComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.loop = true;
        line.positionCount = 64;
        line.startWidth = width;
        line.endWidth = width;
        line.startColor = color;
        line.endColor = color;
        line.material = NewMaterial(color);
        for (int i = 0; i < 64; i++)
        {
            float a = i * Mathf.PI * 2f / 64f;
            line.SetPosition(i, new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius));
        }
        UnityEngine.Object.Destroy(go, lifetime);
        return line;
    }

    // Q needs a readable pre-impact range telegraph. The previous 0.08-0.13 alpha texture was
    // effectively invisible in Shape of Dreams' blue combat rooms. Keep the actual impact sweep
    // separate, but make both the outer blade edge and inner dead-zone boundary unambiguous.
    public static void CreateQTelegraph(Transform owner, float inner, float outer, float duration)
    {
        if (owner == null) return;
        Hero heroOwner = owner.GetComponentInParent<Hero>();
        if (heroOwner == null) return;
        // The LoL system is authored in champion-space units (Q outer radius ~425), and the runtime
        // converter applies the same 0.00921 LoL->Shape-of-Dreams scale as the champion model.
        DariusLolVfxRuntime.PlayAttached(heroOwner, LolSystem(heroOwner, "Darius_Base_Q_Ring_Windup", "Darius_Skin15_Q_Ring_Windup", "Darius_Skin04_Q_Ring_Windup", "Darius_Skin67_Q_RingWindup"), owner);
        // Riot's actual Q_Ring emitters already contain ~0.75 s authored delays. Starting this
        // system only at the gameplay impact added the same delay a second time, which is why the
        // God-King spinning flash appeared roughly one second after Darius had finished Q. Start
        // the delayed ring now, alongside the windup, so its authored delay lands on the spin.
        DariusLolVfxRuntime.PlayAttached(heroOwner, LolSystem(heroOwner, "Darius_Base_Q_Ring", "Darius_Skin15_Q_Ring", "Darius_Skin04_Q_Ring", "Darius_Skin67_Q_Ring"), owner);
        DariusLog.DebugInfo("Q-TELEGRAPH", "Converted Riot Q windup + authored-delayed ring started together inner=" + inner.ToString("0.###") +
            " outer=" + outer.ToString("0.###") + " duration=" + duration.ToString("0.###"));
        DariusMedia.PlayForSkin("q_windup", heroOwner, owner.position, 0.84f);
        DariusMedia.PlaySkillVoice(heroOwner, "q", DariusAudioChannel.Q, 0.78f);
    }

    // One sweep only. Layer a pale steel axe arc over a short blood-red shock ring.
    public static void CreateQSwing(Hero owner, Vector3 center, float inner, float outer)
    {
        if (owner == null) return;
        // Q_Ring was started at cast-start because its Riot emitters contain the windup delay.
        // Do not spawn it again here. Skin15's zero-delay activation flash belongs exactly here.
        if (GodKing(owner)) DariusLolVfxRuntime.PlayAttached(owner, "Darius_Skin15_Q_Activate", owner.transform);
        else if (Mecha(owner)) DariusLolVfxRuntime.PlayAttached(owner, "Darius_Skin67_Q_Activate", owner.transform);
    }

    public static void CreateQInstantSwing(Hero owner)
    {
        if (owner == null) return;
        // Fast-forward only the authored emitter delay; do not globally speed up particle lifetimes.
        // Layers authored at 0.75 s start now, while 0.80/0.85 s accents retain their 0.05/0.10 s
        // spacing inside the spin. This preserves the Riot sequence when the constellation removes windup.
        DariusLolVfxRuntime.PlayAttachedAtAuthoredTime(owner,
            LolSystem(owner, "Darius_Base_Q_Ring", "Darius_Skin15_Q_Ring", "Darius_Skin04_Q_Ring", "Darius_Skin67_Q_Ring"), owner.transform, Ai_Darius_Decimate.Windup);
        if (GodKing(owner)) DariusLolVfxRuntime.PlayAttached(owner, "Darius_Skin15_Q_Activate", owner.transform);
        else if (Mecha(owner)) DariusLolVfxRuntime.PlayAttached(owner, "Darius_Skin67_Q_Activate", owner.transform);
        DariusMedia.PlaySkillVoice(owner, "q", DariusAudioChannel.Q, 0.78f);
    }

    public static void CreateHitFlash(Vector3 position, bool outer)
    {
        // Legacy compatibility path cannot know the active skin; use Classic authored target VFX.
        DariusLolVfxRuntime.PlayWorld(outer ? "darius_Base_Q_tar" : "darius_Base_Q_tar_inner", position);
    }


    public static void CreateHitFlash(Hero owner, Vector3 position, bool outer)
    {
        string classic = outer ? "darius_Base_Q_tar" : "darius_Base_Q_tar_inner";
        string godKing = outer ? "Darius_Skin15_Q_tar" : "Darius_Skin15_Q_tar_inner";
        string mecha = outer ? "Darius_Skin67_Q_tar" : "Darius_Skin67_Q_TarInner";
        DariusLolVfxRuntime.PlayWorld(LolSystem(owner, classic, godKing, null, mecha), position);
    }

    // Compatibility stub. q_hit.wav was an early recreated placeholder and is intentionally silent.
    public static void PlayQHit(Vector3 p) { }

    public static void CreateQHeal(Transform owner)
    {
        if (owner == null) return;
        Hero heroOwner = owner.GetComponentInParent<Hero>();
        if (heroOwner == null) return;
        DariusLolVfxRuntime.PlayAttached(heroOwner, LolSystem(heroOwner, "Darius_Base_Q_Heal", "Darius_Skin15_Q_Heal", null, "Darius_Skin67_Q_Heal"), owner);
    }

    // W should visibly stay armed until the empowered basic attack is spent/expired.
    public static GameObject CreateWArm(Hero owner)
    {
        if (owner == null) return null;
        Transform anchor = DariusSkinAnimationHooks.GetWeaponAnchor(owner);
        if (anchor == null) anchor = owner.transform;
        GameObject fx = DariusLolVfxRuntime.PlayAttachedPersistent(owner, LolSystem(owner, "darius_Base_W_weapon_01", "Darius_Skin15_W_weapon_01", "Darius_Skin04_W_Weapon_Child", "Darius_Skin67_W_Weapon01"), anchor);
        if (GodKing(owner))
        {
            // weapon_02 carries the real Skin15 Darius_Axe_Mat / body AttachedMesh overlays.
            // It is kept as a separate Riot system but tied to weapon_01's lifetime.
            GameObject overlayFx = DariusLolVfxRuntime.PlayAttachedPersistent(owner, "Darius_Skin15_W_weapon_02", anchor);
            if (fx != null && overlayFx != null)
            {
                DariusLolVfxLinkedObjects links = fx.GetComponent<DariusLolVfxLinkedObjects>();
                if (links != null) links.Add(overlayFx);
            }
            else if (fx == null) fx = overlayFx;
        }
        DariusMedia.PlayForSkin("w_arm", owner, 0.88f);
        DariusMedia.PlaySkillVoice(owner, "w", DariusAudioChannel.W, 0.78f);
        return fx;
    }

    public static void CreateWImpact(Hero owner, Vector3 p)
    {
        DariusLolVfxRuntime.PlayWorld(LolSystem(owner, "darius_Base_W_tar", "Darius_Skin15_W_tar", null, "Darius_Skin67_W_tar"), p);
        DariusMedia.PlayForSkin("w_hit", owner, p, 0.98f);
    }
    public static void CreateWImpact(Vector3 p) { CreateWImpact(null, p); }

    // E's green cone in the reference recording is only targeting UI. The cast itself is a quick
    // forward hook/scrape, so use a short crescent and then target-to-owner streaks.
    public static void CreateECone(Hero owner, Vector3 origin, Vector3 dir, float range, float halfAngle)
    {
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.001f) dir = owner != null ? owner.transform.forward : Vector3.forward;
        dir.Normalize();
        if (GodKing(owner))
        {
            DariusLolVfxRuntime.PlayWorld("Darius_Skin15_E_ClawMarks", origin, LolFacing(dir));
            DariusLolVfxRuntime.PlayWorld("Darius_Skin15_E_Axegrab_Collision", origin, LolFacing(dir));
        }
        else if (Dunkmaster(owner))
            DariusLolVfxRuntime.PlayWorld("Darius_Skin04_E_Axegrab_Collision", origin, LolFacing(dir));
        else if (Mecha(owner))
        {
            DariusLolVfxRuntime.PlayWorld("Darius_Skin67_E_ClawMarks", origin, LolFacing(dir));
            DariusLolVfxRuntime.PlayWorld("Darius_Skin67_E_AxegrabCollision", origin, LolFacing(dir));
        }
        else DariusLolVfxRuntime.PlayWorld("Darius_Base_E_Axegrab_Collision", origin, LolFacing(dir));
    }

    public static void CreateECone(Vector3 origin, Vector3 dir, float range, float halfAngle) { CreateECone(null, origin, dir, range, halfAngle); }

    public static void CreateEPull(Hero owner, Vector3 from, Vector3 to)
    {
        Vector3 dir = to - from;
        Quaternion facing = LolFacing(dir);
        DariusLolVfxRuntime.PlayWorld(LolSystem(owner, "darius_Base_E_tar_unit_trail", "Darius_Skin15_E_tar_unit_trail", "Darius_Skin04_E_Tar_Unit_Trail", "Darius_Skin67_E_TarUnitTrail"), from, facing);
        DariusLolVfxRuntime.PlayWorld(LolSystem(owner, "darius_Base_E_tar", "Darius_Skin15_E_tar", null, "Darius_Skin67_E_tar"), from);
        if (GodKing(owner)) DariusLolVfxRuntime.PlayWorld("Darius_Skin15_E_tar_02", from);
        else if (Mecha(owner))
        {
            // Skin67 E_Tar02 contains a target CameraTrail. Bind it to the same short movement
            // interval as the actual pull instead of spawning it on a static world root (which
            // forced the runtime to drop Trail_BLend as unsupported).
            DariusLolVfxRuntime.PlayWorldBetween(owner, "Darius_Skin67_E_Tar02", from, to, 0.22f, facing);
        }
    }
    public static void CreateEPull(Vector3 from, Vector3 to) { CreateEPull(null, from, to); }

    public static void PlayEPull(Hero owner, Vector3 p) { DariusMedia.PlayForSkin("e_pull", owner, p,0.94f); }
    public static void PlayEPull(Vector3 p) { DariusMedia.Play("e_pull", p,0.94f); }

    public static void CreateRLeap(Hero owner, Vector3 from, Vector3 to)
    {
        // R_Trail is started at cast time and follows the original weapon attachment.
        // No procedural LineRenderer replacement is added here.
    }
    public static void CreateRLeap(Vector3 from, Vector3 to) { CreateRLeap(null, from, to); }

    public static void CreateRImpact(Hero owner, Entity target, Vector3 p)
    {
        DariusLolVfxRuntime.PlayWorld(LolSystem(owner, "darius_Base_R_tar", "Darius_Skin15_R_tar", null, "Darius_Skin67_R_tar"), p);
        if (GodKing(owner))
        {
            // Skin15 ships an additional impact system. Its own disabled flags are now respected,
            // so only Riot-authored active flash/up-glow/BlastColumn layers are instantiated.
            DariusLolVfxRuntime.PlayWorld("Darius_Skin15_R_tar_02", p);
        }
        else if (Dunkmaster(owner))
            DariusLolVfxRuntime.PlayWorld("Darius_Skin04_R_Tar_Backboard", p);
        else if (Mecha(owner))
        {
            // Temp_Avatar in Skin67_R_TarStartVFX is an AttachedMesh on the victim. Preserve the
            // target transform so the generic target-overlay path can reproduce it instead of
            // logging "no owning traveler" and dropping the layer.
            if (target != null) DariusLolVfxRuntime.PlayAttached(null, "Darius_Skin67_R_TarStartVFX", target.transform);
            else DariusLolVfxRuntime.PlayWorld("Darius_Skin67_R_TarStartVFX", p);
            DariusLolVfxRuntime.PlayWorld("Darius_Skin67_R_Tar02", p);
        }
        DariusMedia.PlayForSkin("r_hit", owner, p, 1.0f);
    }
    public static void CreateRImpact(Hero owner, Vector3 p) { CreateRImpact(owner, null, p); }
    public static void CreateRImpact(Vector3 p) { CreateRImpact(null, null, p); }

    public static void CreateRReset(Hero owner, Vector3 p)
    {
        DariusLolVfxRuntime.PlayWorld(LolSystem(owner, "darius_Base_r_refresh_01", "Darius_Skin15_darius_Base_r_refresh_01", null, "Darius_Skin67_DariusBase_R_DariusBaseRefresh01"), p);
        DariusMedia.PlayForSkin("r_reset", owner, p, 0.74f);
    }
    public static void CreateRReset(Vector3 p) { CreateRReset(null, p); }

    public static void CreateEssenceToggle(Vector3 p, bool on)
    {
        if (on) TexturedQuad("Darius_Essence", "bleed_drop", p+Vector3.up*0.12f,new Vector3(0.72f,1f,0.72f),0.34f,Color.white,false,null,35f);
        else FallbackRing("Darius_EssenceOff",p,1.05f,new Color(0.3f,0.3f,0.3f,0.62f),0.08f,0.35f);
    }

    public static void CreateBleedStack(Hero owner, Transform t, int stacks)
    {
        if (t == null || stacks <= 0) return;
        if (Mecha(owner))
        {
            DariusLolVfxRuntime.PlayWorld("Darius_Skin67_HemoBleedIndicatorHit", t.position + Vector3.up * 0.7f);
            return;
        }
        float s = 0.30f + Mathf.Clamp(stacks, 1, 10) * 0.035f;
        TexturedQuad(SkinFxName(owner, "BleedApply_" + stacks),"bleed_drop",t.position+Vector3.up*1.35f,
            new Vector3(s,s,1f),0.20f,SkinColor(owner,new Color(1f,0.45f,0.38f,0.90f),new Color(1f,0.68f,0.16f,0.94f)),true,t,0f);
        // Hemorrhage stack application is intentionally silent; bleed_apply.wav was an early
        // placeholder and produced the shared pop-like hit sound reported in v0.18.2b.
    }
    public static void CreateBleedStack(Transform t, int stacks) { CreateBleedStack(null, t, stacks); }

    public static GameObject CreateBleedStackMarker(Hero owner, Transform t, int stacks)
    {
        if (t == null || stacks <= 0) return null;
        if (Mecha(owner))
        {
            int n = Mathf.Clamp(stacks, 1, 5);
            return DariusLolVfxRuntime.PlayAttachedPersistent(owner, "Darius_Skin67_DariusBaseHemoCounter0" + n, t);
        }
        int count = Mathf.Clamp(stacks, 1, 12);
        GameObject root = new GameObject(SkinFxName(owner, "BleedStacks_" + stacks));
        root.transform.SetParent(t, false);
        root.transform.localPosition = Vector3.up * 1.56f;
        root.transform.localRotation = Quaternion.identity;
        root.transform.localScale = Vector3.one;
        const int perRow = 6;
        float spacing = 0.18f;
        Color pip = SkinColor(owner,new Color(1f,0.35f,0.30f,0.96f),new Color(1f,0.66f,0.14f,0.98f));
        for (int i = 0; i < count; i++)
        {
            int row = i / perRow;
            int indexInRow = i % perRow;
            int rowCount = Mathf.Min(perRow, count - row * perRow);
            float x = (indexInRow - (rowCount - 1) * 0.5f) * spacing;
            float y = row * 0.17f;
            PersistentTexturedQuad(SkinFxName(owner, "BleedStackDot_" + (i + 1)), "bleed_drop", root.transform,
                new Vector3(x, y, 0f), new Vector3(0.26f,0.26f,1f), pip, true, 0f, 0.025f, GodKing(owner) ? 5.5f : 4.0f);
        }
        return root;
    }
    public static GameObject CreateBleedStackMarker(Transform t, int stacks) { return CreateBleedStackMarker(null, t, stacks); }

    public static GameObject CreateBleedFiveMark(Hero owner, Transform t)
    {
        if (t == null) return null;
        if (Mecha(owner))
            return DariusLolVfxRuntime.PlayAttachedPersistent(owner, "Darius_Skin67_DariusBasePassiveOverheadMaxStack", t);
        return PersistentTexturedQuad(SkinFxName(owner, "BleedFiveMark"),"bleed_five_mark",t,Vector3.up*1.66f,
            new Vector3(GodKing(owner) ? 1.02f : 0.92f,GodKing(owner) ? 1.02f : 0.92f,1f),
            SkinColor(owner,new Color(1f,0.30f,0.25f,0.94f),new Color(1f,0.72f,0.18f,0.98f)),true,0f,0.055f,GodKing(owner) ? 7.0f : 5.8f);
    }
    public static GameObject CreateBleedFiveMark(Transform t) { return CreateBleedFiveMark(null, t); }

    public static void CreateBleedTick(Hero owner, Vector3 p, int stacks)
    {
        if (Mecha(owner))
        {
            DariusLolVfxRuntime.PlayWorld("Darius_Skin67_HemoBleedIndicatorTalon", p + Vector3.up * 0.45f);
            return;
        }
        float visualStacks = Mathf.Min(stacks, 10);
        TexturedQuad(SkinFxName(owner, "BleedTick"),"hit_flash",p+Vector3.up*0.09f,
            new Vector3(0.36f+visualStacks*0.055f,1f,0.36f+visualStacks*0.055f),0.13f,
            SkinColor(owner,new Color(0.52f,0.05f,0.06f,0.56f),new Color(0.96f,0.34f,0.02f,0.64f)),false,null,GodKing(owner) ? 90f : 65f);
    }
    public static void CreateBleedTick(Vector3 p, int stacks) { CreateBleedTick(null, p, stacks); }

    public static GameObject CreateNoxianMight(Transform owner)
    {
        if (owner == null) return null;
        Hero heroOwner = null;
        try { heroOwner = owner.GetComponentInParent<Hero>(); } catch { }
        if (Dunkmaster(heroOwner))
            return DariusLolVfxRuntime.PlayAttachedPersistent(heroOwner, "Darius_Skin04_P_enraged", owner);
        if (Mecha(heroOwner))
        {
            GameObject rootFx = DariusLolVfxRuntime.PlayAttachedPersistent(heroOwner, "Darius_Skin67_P_enraged", owner);
            GameObject left = DariusLolVfxRuntime.PlayAttachedPersistent(heroOwner, "Darius_Skin67_P_EnragedShoulderL", owner);
            GameObject right = DariusLolVfxRuntime.PlayAttachedPersistent(heroOwner, "Darius_Skin67_P_EnragedShoulderR", owner);
            if (rootFx != null)
            {
                DariusLolVfxLinkedObjects links = rootFx.GetComponent<DariusLolVfxLinkedObjects>();
                if (links != null) { if (left != null) links.Add(left); if (right != null) links.Add(right); }
            }
            return rootFx ?? left ?? right;
        }
        bool godKing = GodKing(heroOwner);
        GameObject root = new GameObject(SkinFxName(heroOwner, "NoxianMight_State"));
        root.transform.SetParent(owner, false);
        root.transform.localPosition = Vector3.zero;

        Color ground = SkinColor(heroOwner, new Color(1f,0.18f,0.10f,0.76f), new Color(1f,0.58f,0.08f,0.84f));
        Color body = SkinColor(heroOwner, new Color(1f,0.08f,0.05f,0.46f), new Color(0.95f,0.24f,0.02f,0.52f));
        Color bloom = SkinColor(heroOwner, new Color(1f,0.16f,0.08f,0.30f), new Color(1f,0.72f,0.16f,0.38f));
        Color mark = SkinColor(heroOwner, new Color(1f,0.20f,0.14f,0.98f), new Color(1f,0.82f,0.24f,1.00f));

        // Presentation only: God-King has its own orange/gold profile and object namespace.
        PersistentTexturedQuad(SkinFxName(heroOwner, "NoxianMight_GroundAura"),SkinTexture(heroOwner, "noxian_aura", "gk_glow"),root.transform,Vector3.up*0.07f,
            new Vector3(godKing ? 2.80f : 2.55f,1f,godKing ? 2.80f : 2.55f),ground,false,godKing ? 62f : 46f,0.080f,godKing ? 7.2f : 6.2f);
        PersistentTexturedQuad(SkinFxName(heroOwner, "NoxianMight_BodyAura"),SkinTexture(heroOwner, "noxian_aura", "gk_wisps_red"),root.transform,Vector3.up*1.02f,
            new Vector3(godKing ? 2.12f : 1.95f,godKing ? 2.48f : 2.35f,1f),body,true,0f,0.105f,godKing ? 8.8f : 7.5f);
        PersistentTexturedQuad(SkinFxName(heroOwner, "NoxianMight_BodyBloom"),"hit_flash",root.transform,Vector3.up*1.02f,
            new Vector3(godKing ? 1.58f : 1.40f,godKing ? 2.10f : 1.95f,1f),bloom,true,0f,0.090f,godKing ? 9.6f : 8.3f);
        PersistentTexturedQuad(SkinFxName(heroOwner, "NoxianMight_Mark"),"bleed_five_mark",root.transform,Vector3.up*1.82f,
            new Vector3(godKing ? 0.98f : 0.88f,godKing ? 0.98f : 0.88f,1f),mark,true,0f,0.050f,godKing ? 6.6f : 5.2f);

        GameObject lightObject = new GameObject(SkinFxName(heroOwner, "NoxianMight_BodyLight"));
        lightObject.transform.SetParent(root.transform, false);
        lightObject.transform.localPosition = Vector3.up * 1.02f;
        Light bodyLight = lightObject.AddComponent<Light>();
        bodyLight.type = LightType.Point;
        bodyLight.color = SkinColor(heroOwner, new Color(1f,0.035f,0.018f,1f), new Color(1f,0.42f,0.035f,1f));
        bodyLight.range = godKing ? 3.8f : 3.4f;
        bodyLight.intensity = godKing ? 1.95f : 1.65f;
        bodyLight.shadows = LightShadows.None;

        DariusNoxianMightPresentation presentation = root.AddComponent<DariusNoxianMightPresentation>();
        presentation.owner = heroOwner;
        presentation.bodyLight = bodyLight;
        presentation.baseLightIntensity = bodyLight.intensity;
        presentation.edgeColor = SkinColor(heroOwner, new Color(0.92f,0.015f,0.01f,1f), new Color(1.00f,0.38f,0.025f,1f));

        DariusMedia.PlayForSkin("noxian_might", heroOwner, owner.position, 0.96f);
        DariusLog.Info("NOXIAN-VFX", "Created skin-specific body glow presentation skin=" +
            (godKing ? "GodKing" : "Classic") + " owner=" +
            (presentation.owner != null ? DariusLog.EntityLabel(presentation.owner) : owner.name));
        return root;
    }

    // Flash/Ghost keep League's visual language while their actual mobility economy comes from
    // Shape of Dreams' native movement skill. These effects intentionally use the mod's existing
    // texture primitives so they work without an AssetBundle and survive both lobby/run scenes.
    public static void CreateSummonerFlash(Vector3 from, Vector3 to)
    {
        Color gold = new Color(1.00f, 0.90f, 0.36f, 0.96f);
        Color pale = new Color(1.00f, 0.98f, 0.72f, 0.88f);

        TexturedQuad("Darius_Flash_Origin", "hit_flash", from + Vector3.up * 0.14f,
            new Vector3(1.32f,1f,1.32f),0.20f,gold,false,null,220f);
        TexturedQuad("Darius_Flash_Destination", "hit_flash", to + Vector3.up * 0.14f,
            new Vector3(1.62f,1f,1.62f),0.25f,pale,false,null,-260f);
        TexturedQuad("Darius_Flash_Ring", "q_outer_ring", to + Vector3.up * 0.07f,
            new Vector3(1.85f,1f,1.85f),0.24f,new Color(1f,0.82f,0.22f,0.58f),false,null,420f);

        GameObject streak = new GameObject("Darius_Flash_Streak");
        LineRenderer line = streak.AddComponent<LineRenderer>();
        line.positionCount = 2;
        line.useWorldSpace = true;
        line.startWidth = 0.30f;
        line.endWidth = 0.06f;
        line.startColor = pale;
        line.endColor = new Color(1f,0.78f,0.20f,0.06f);
        line.material = NewMaterial(pale);
        line.SetPosition(0, from + Vector3.up * 0.45f);
        line.SetPosition(1, to + Vector3.up * 0.45f);
        UnityEngine.Object.Destroy(streak, 0.12f);

        DariusMedia.Play("flash", to, 0.94f);
    }

    public static GameObject CreateSummonerGhost(Transform owner, float duration)
    {
        if (owner == null) return null;
        GameObject aura = PersistentTexturedQuad("Darius_Ghost_Aura", "noxian_aura", owner, Vector3.up * 0.06f,
            new Vector3(1.85f,1f,1.85f),new Color(0.34f,0.86f,1.00f,0.62f),false,92f,0.08f,8.4f);
        TexturedQuad("Darius_Ghost_Burst", "q_heal_wisps", owner.position + Vector3.up * 0.18f,
            new Vector3(1.65f,1f,1.65f),Mathf.Min(0.48f, Mathf.Max(0.18f, duration * 0.12f)),
            new Color(0.52f,0.94f,1.00f,0.72f),false,owner,90f);
        DariusMedia.Play("ghost", owner.position, 0.90f);
        return aura;
    }
}
