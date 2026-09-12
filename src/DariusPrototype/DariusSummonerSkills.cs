using System;
using System.Collections;
using System.Reflection;
using Mirror;
using UnityEngine;

// Darius replaces the stock dodge/movement slot with two League-style summoner spells.
// Their economy is anchored to Vesper's native movement Memory at runtime so balance changes in
// Shape of Dreams automatically carry over instead of hard-coding a separate cooldown model.
public static class DariusSummonerBalance
{
    public const float FlashCooldown = 3.0f;
    public static float Cooldown { get; private set; } = 6f;
    public static int MaxCharges { get; private set; } = 1;
    public static int AddedCharges { get; private set; } = 1;
    public static float MinimumDelay { get; private set; } = 0.05f;
    public static float FlashDistance { get; private set; } = 4.0f;
    public static float GhostDuration { get; private set; } = 3.0f;
    public static float GhostBonusPercentFallback { get; private set; } = 40f;
    public static string NativeSourceName { get; private set; } = "fallback";

    public static void InitializeFromNativeMovement(SkillTrigger nativeMovement)
    {
        try
        {
            if (nativeMovement == null || nativeMovement.configs == null || nativeMovement.configs.Length == 0 || nativeMovement.configs[0] == null)
            {
                DariusLog.Warn("SUMMONER-BALANCE", "Native Vesper movement skill unavailable; using conservative fallback values.");
                return;
            }

            TriggerConfig cfg = nativeMovement.configs[0];
            NativeSourceName = nativeMovement.name;
            Cooldown = Mathf.Max(0.1f, DariusTriggerConfigRuntimeEditor.GetCooldownTime(cfg, Cooldown));
            MaxCharges = Mathf.Max(1, DariusTriggerConfigRuntimeEditor.GetMaxCharges(cfg, MaxCharges));
            AddedCharges = Mathf.Max(1, DariusTriggerConfigRuntimeEditor.GetAddedCharges(cfg, AddedCharges));
            MinimumDelay = Mathf.Max(0.01f, DariusTriggerConfigRuntimeEditor.GetMinimumDelay(cfg, MinimumDelay));

            float range = 0f;
            try
            {
                if (cfg.castMethod != null)
                    range = Mathf.Max(Mathf.Abs(cfg.castMethod._radius), Mathf.Abs(cfg.castMethod._range));
            }
            catch { }

            // Some movement skills keep dash distance outside CastMethod. Probe numeric members with
            // movement/range/distance names and only accept sane world-space values.
            range = Mathf.Max(range, ProbeDistance(nativeMovement));
            range = Mathf.Max(range, ProbeDistance(cfg));
            if (cfg.spawnedInstance != null) range = Mathf.Max(range, ProbeDistance(cfg.spawnedInstance));
            if (cfg.appliedStatusEffect != null) range = Mathf.Max(range, ProbeDistance(cfg.appliedStatusEffect));
            if (range >= 1.0f && range <= 12.0f) FlashDistance = range;

            // Ghost spreads roughly one native-dodge worth of extra travel across a short window.
            // This keeps its mobility budget tied to the stock dodge without turning it into another dash.
            GhostDuration = Mathf.Clamp(Cooldown * 0.45f, 2.5f, 5.0f);
            GhostBonusPercentFallback = Mathf.Clamp(FlashDistance * 10f, 25f, 65f);

            DariusLog.Info("SUMMONER-BALANCE", "Inherited movement template=" + NativeSourceName +
                " ghostCooldown=" + Cooldown.ToString("0.###") + " flashCooldown=" + FlashCooldown.ToString("0.###") + " maxCharges=" + MaxCharges +
                " addedCharges=" + AddedCharges + " minDelay=" + MinimumDelay.ToString("0.###") +
                " flashDistance=" + FlashDistance.ToString("0.###") + " ghostDuration=" + GhostDuration.ToString("0.###") +
                " ghostFallbackBonus=" + GhostBonusPercentFallback.ToString("0.#") + "%");
        }
        catch (Exception e)
        {
            DariusLog.Exception("SUMMONER-BALANCE", e, "Failed reading native movement values; fallback retained");
        }
    }

    public static TriggerConfig CreateFlashConfig(AbilityTrigger parent)
    {
        return CreateConfig(parent, FlashCooldown);
    }

    public static TriggerConfig CreateGhostConfig(AbilityTrigger parent)
    {
        return CreateConfig(parent, Cooldown);
    }

    private static TriggerConfig CreateConfig(AbilityTrigger parent, float cooldown)
    {
        TriggerConfig cfg = DariusTriggerConfigRuntimeEditor.CreateBase(parent, cooldown);
        DariusTriggerConfigRuntimeEditor.SetMaxCharges(cfg, MaxCharges);
        DariusTriggerConfigRuntimeEditor.SetAddedCharges(cfg, AddedCharges);
        DariusTriggerConfigRuntimeEditor.SetMinimumDelay(cfg, MinimumDelay);
        cfg.startCharges = MaxCharges;
        cfg.spawnedInstance = null;
        cfg.appliedStatusEffect = null;
        cfg.isActive = true;
        cfg.canReceiveCooldownReduction = true;
        cfg.alwaysCastImmediately = true;
        cfg.castByMoveDirectionByDefault = true;
        cfg.castByMoveDirectionGamepad = true;
        cfg.faceForward = false;
        cfg.castMethod = cfg.castMethod ?? new CastMethodData();
        cfg.castMethod.type = CastMethodType.None;
        cfg.castMethod._range = FlashDistance;
        cfg.castMethod._radius = FlashDistance;
        return cfg;
    }

    public static string BuildFlashDescription()
    {
        if (DariusLanguage.IsEnglish)
            return "Blink <color=yellow>" + FlashDistance.ToString("0.##") + " m</color> in the movement or aim direction, passing through enemies and other units. " +
                   "Base cooldown is <color=yellow>" + FlashCooldown.ToString("0.##") + " seconds</color>; charges follow the native Shape of Dreams dodge system, up to <color=yellow>" + MaxCharges + "</color>.";
        return "朝移动/指向方向瞬间闪烁<color=yellow>" + FlashDistance.ToString("0.##") + "米</color>，可以直接越过敌人与其他单位。" +
               "基础冷却固定为<color=yellow>" + FlashCooldown.ToString("0.##") + "秒</color>；充能数继承《梦之形》原版闪避，最多<color=yellow>" +
               MaxCharges + "</color>次充能。";
    }

    public static string BuildGhostDescription(Hero hero = null)
    {
        float bonus = hero != null ? GetGhostBonusPercent(hero) : GhostBonusPercentFallback;
        if (DariusLanguage.IsEnglish)
            return "Enter Ghost for <color=yellow>" + GhostDuration.ToString("0.##") + " seconds</color>, currently granting about <color=yellow>" + bonus.ToString("0.#") + "%</color> Move Speed. " +
                   "The bonus is derived from the native dodge distance budget. Cooldown and charges follow the native dodge: <color=yellow>" + Cooldown.ToString("0.##") + " seconds</color>, up to <color=yellow>" + MaxCharges + "</color>.";
        return "进入疾跑状态<color=yellow>" + GhostDuration.ToString("0.##") + "秒</color>，当前额外移动速度约<color=yellow>" +
               bonus.ToString("0.#") + "%</color>。疾跑的额外移动距离预算按原版闪避距离实时换算；冷却与充能直接继承原版闪避：<color=yellow>" +
               Cooldown.ToString("0.##") + "秒</color>，最多<color=yellow>" + MaxCharges + "</color>次充能。";
    }

    public static float GetGhostBonusPercent(Hero hero)
    {
        float speed = ProbeMovementSpeed(hero);
        if (speed <= 0.05f) return GhostBonusPercentFallback;
        float extraMetersPerSecond = FlashDistance / Mathf.Max(0.1f, GhostDuration);
        return Mathf.Clamp(extraMetersPerSecond / speed * 100f, 20f, 80f);
    }

    private static float ProbeMovementSpeed(Hero hero)
    {
        if (hero == null || hero.Status == null) return 0f;
        try
        {
            object finalStats = hero.Status.finalStats;
            if (finalStats == null) return 0f;
            Type t = finalStats.GetType();
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            foreach (string name in new[] { "movementSpeed", "moveSpeed", "baseMovementSpeed" })
            {
                FieldInfo f = t.GetField(name, flags);
                if (f != null)
                {
                    float value = Convert.ToSingle(f.GetValue(finalStats));
                    if (value > 0f) return value;
                }
                PropertyInfo p = t.GetProperty(name, flags);
                if (p != null && p.GetIndexParameters().Length == 0)
                {
                    float value = Convert.ToSingle(p.GetValue(finalStats, null));
                    if (value > 0f) return value;
                }
            }
        }
        catch { }
        return 0f;
    }

    private static float ProbeDistance(object obj)
    {
        if (obj == null) return 0f;
        float best = 0f;
        try
        {
            Type t = obj.GetType();
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            foreach (FieldInfo f in t.GetFields(flags))
            {
                string n = f.Name.ToLowerInvariant();
                if (!(n.Contains("distance") || n.Contains("range") || n.Contains("dash"))) continue;
                if (f.FieldType != typeof(float) && f.FieldType != typeof(double) && f.FieldType != typeof(int)) continue;
                float v = Mathf.Abs(Convert.ToSingle(f.GetValue(obj)));
                if (v >= 1f && v <= 12f) best = Mathf.Max(best, v);
            }
            foreach (PropertyInfo p in t.GetProperties(flags))
            {
                string n = p.Name.ToLowerInvariant();
                if (!(n.Contains("distance") || n.Contains("range") || n.Contains("dash"))) continue;
                if (p.GetIndexParameters().Length != 0 || !p.CanRead) continue;
                if (p.PropertyType != typeof(float) && p.PropertyType != typeof(double) && p.PropertyType != typeof(int)) continue;
                float v = Mathf.Abs(Convert.ToSingle(p.GetValue(obj, null)));
                if (v >= 1f && v <= 12f) best = Mathf.Max(best, v);
            }
        }
        catch { }
        return best;
    }
}

public sealed class St_Darius_Flash : SkillTrigger
{
    protected override void OnPrepare()
    {
        base.OnPrepare();
        configs = new[] { DariusSummonerBalance.CreateFlashConfig(this), DariusSummonerBalance.CreateFlashConfig(this) };
        for (int i = 0; i < configs.Length; i++) configs[i].triggerIcon = DariusPrototypeIcons.Get("FLASH");
        DariusLog.Info("FLASH-CONFIG", "Prepared flash from native movement template=" + DariusSummonerBalance.NativeSourceName);
    }

    protected override void OnLevelChange(int oldLevel, int newLevel)
    {
        if (newLevel < 1) return;
        if (owner == null) { ClientSkillEvent_OnLevelChange?.Invoke(oldLevel, newLevel); return; }
        base.OnLevelChange(oldLevel, newLevel);
    }

    public override AbilityInstance OnCastComplete(int configIndex, CastInfo info)
    {
        AbilityInstance result = null;
        try { result = base.OnCastComplete(configIndex, info); }
        catch (Exception e) { DariusLog.Exception("FLASH", e, "Stock movement cooldown bookkeeping failed"); }
        try
        {
            Execute(info);
            Hero ownerHero = info.caster as Hero;
            if (ownerHero != null) DariusConstellationRuntime.NotifyMovementSpell(ownerHero, this, "Flash");
        }
        catch (Exception e) { DariusLog.Exception("FLASH", e, "Flash execution failed"); }
        return result;
    }

    private static void Execute(CastInfo info)
    {
        Hero owner = info.caster as Hero;
        if (owner == null) return;
        Vector3 dir = DariusSummonerRuntime.ResolveDirection(owner, info);
        Vector3 start = owner.transform.position;
        Vector3 destination = DariusFlashWarp.ResolveDestination(owner, start, dir, DariusSummonerBalance.FlashDistance);
        DariusPrototypeVfx.CreateSummonerFlash(start, destination);
        if (NetworkServer.active)
        {
            bool forcedPhase = DariusFlashWarp.TeleportThroughUnits(owner, start, destination, dir);
            DariusLog.Info("FLASH", "Teleported " + DariusLog.EntityLabel(owner) + " baseCooldown=" + DariusSummonerBalance.FlashCooldown.ToString("0.###") +
                " distance=" + Vector3.Distance(start, destination).ToString("0.###") + " forcedPhase=" + forcedPhase +
                " from=" + DariusLog.Vec(start) + " requested=" + DariusLog.Vec(destination) + " actual=" + DariusLog.Vec(owner.transform.position));
        }
    }
}

// Flash is a teleport, not a dash. Unit bodies must never shorten its path. We ray-test only
// non-Entity world colliders to keep walls/terrain blocking, then use the game's Teleport for
// network/state bookkeeping. If native collision resolution still clamps the endpoint against a
// unit body, the server force-places the Hero at the already world-validated endpoint.
public static class DariusFlashWarp
{
    private const float ProbeHeight = 0.45f;
    private const float WallBackoff = 0.16f;
    private const float NativeClampTolerance = 0.30f;

    public static Vector3 ResolveDestination(Hero owner, Vector3 start, Vector3 direction, float distance)
    {
        if (owner == null) return start;
        Vector3 dir = direction;
        dir.y = 0f;
        if (dir.sqrMagnitude <= 0.001f) return start;
        dir.Normalize();
        float allowed = Mathf.Max(0f, distance);
        try
        {
            Vector3 origin = start + Vector3.up * ProbeHeight;
            RaycastHit[] hits = Physics.RaycastAll(origin, dir, allowed, ~0, QueryTriggerInteraction.Ignore);
            float nearestWorld = allowed;
            for (int i = 0; i < hits.Length; i++)
            {
                RaycastHit hit = hits[i];
                Collider collider = hit.collider;
                if (collider == null || IsUnitCollider(collider, owner)) continue;
                if (hit.distance > 0.01f && hit.distance < nearestWorld) nearestWorld = hit.distance;
            }
            allowed = Mathf.Max(0f, nearestWorld - (nearestWorld < distance ? WallBackoff : 0f));
        }
        catch (Exception e)
        {
            DariusLog.Exception("FLASH-PHASE", e, "World obstruction probe failed; falling back to requested Flash endpoint");
        }
        return start + dir * allowed;
    }

    public static bool TeleportThroughUnits(Hero owner, Vector3 start, Vector3 destination, Vector3 direction)
    {
        if (owner == null) return false;
        Vector3 dir = direction;
        dir.y = 0f;
        if (dir.sqrMagnitude <= 0.001f) dir = destination - start;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.001f) dir.Normalize();

        owner.Teleport(owner, destination);
        try { Physics.SyncTransforms(); } catch { }

        float intended = Vector3.Distance(start, destination);
        Vector3 afterNative = owner.transform.position;
        float progressed = dir.sqrMagnitude > 0.001f ? Vector3.Dot(afterNative - start, dir) : Vector3.Distance(start, afterNative);
        if (intended <= 0.01f || progressed + NativeClampTolerance >= intended) return false;

        // ResolveDestination already stopped at the first non-unit world collider. A shorter native
        // result here is therefore unit-body collision/overlap resolution and is safe to bypass.
        ForcePosition(owner, destination);
        return true;
    }

    private static bool IsUnitCollider(Collider collider, Hero owner)
    {
        if (collider == null) return false;
        try
        {
            if (owner != null && (collider.transform == owner.transform || collider.transform.IsChildOf(owner.transform))) return true;
            Entity entity = collider.GetComponentInParent<Entity>();
            if (entity != null) return true;
        }
        catch { }
        return false;
    }

    private static void ForcePosition(Hero owner, Vector3 destination)
    {
        try
        {
            owner.transform.position = destination;
            Rigidbody body = owner.GetComponent<Rigidbody>();
            if (body != null) body.position = destination;

            // Keep a NavMesh-backed movement component in sync without taking a compile-time
            // dependency on UnityEngine.AIModule. Only components explicitly named as NavMesh
            // agents are considered, so arbitrary gameplay components with a Warp method are not called.
            Component[] components = owner.GetComponents<Component>();
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            for (int i = 0; i < components.Length; i++)
            {
                Component component = components[i];
                if (component == null) continue;
                Type type = component.GetType();
                string typeName = type.FullName ?? type.Name;
                if (typeName.IndexOf("NavMeshAgent", StringComparison.OrdinalIgnoreCase) < 0) continue;
                MethodInfo warp = type.GetMethod("Warp", flags, null, new[] { typeof(Vector3) }, null);
                if (warp != null)
                {
                    try { warp.Invoke(component, new object[] { destination }); } catch { }
                }
            }
            try { Physics.SyncTransforms(); } catch { }
        }
        catch (Exception e)
        {
            DariusLog.Exception("FLASH-PHASE", e, "Failed to force world-validated Flash endpoint");
        }
    }
}

public sealed class St_Darius_Ghost : SkillTrigger
{
    protected override void OnPrepare()
    {
        base.OnPrepare();
        configs = new[] { DariusSummonerBalance.CreateGhostConfig(this), DariusSummonerBalance.CreateGhostConfig(this) };
        for (int i = 0; i < configs.Length; i++) configs[i].triggerIcon = DariusPrototypeIcons.Get("GHOST");
        DariusLog.Info("GHOST-CONFIG", "Prepared ghost from native movement template=" + DariusSummonerBalance.NativeSourceName);
    }

    protected override void OnLevelChange(int oldLevel, int newLevel)
    {
        if (newLevel < 1) return;
        if (owner == null) { ClientSkillEvent_OnLevelChange?.Invoke(oldLevel, newLevel); return; }
        base.OnLevelChange(oldLevel, newLevel);
    }

    public override AbilityInstance OnCastComplete(int configIndex, CastInfo info)
    {
        AbilityInstance result = null;
        try { result = base.OnCastComplete(configIndex, info); }
        catch (Exception e) { DariusLog.Exception("GHOST", e, "Stock movement cooldown bookkeeping failed"); }
        try
        {
            Hero ownerHero = info.caster as Hero;
            if (ownerHero != null)
            {
                DariusGhostRuntime runtime = ownerHero.GetComponent<DariusGhostRuntime>();
                if (runtime == null) runtime = ownerHero.gameObject.AddComponent<DariusGhostRuntime>();
                runtime.Activate(ownerHero);
                DariusConstellationRuntime.NotifyMovementSpell(ownerHero, this, "Ghost");
            }
        }
        catch (Exception e) { DariusLog.Exception("GHOST", e, "Ghost execution failed"); }
        return result;
    }
}

public sealed class DariusGhostRuntime : MonoBehaviour
{
    private Hero _owner;
    private StatBonus _bonus;
    private Coroutine _routine;
    private GameObject _vfx;

    public void Activate(Hero owner)
    {
        _owner = owner;
        if (_routine != null) StopCoroutine(_routine);
        RemoveBonus();
        ClearVfx();
        _routine = StartCoroutine(Run());
    }

    private IEnumerator Run()
    {
        RemoveBonus();
        float bonusPercent = DariusSummonerBalance.GetGhostBonusPercent(_owner);
        if (NetworkServer.active && _owner != null && _owner.Status != null)
        {
            _bonus = new StatBonus();
            _bonus.movementSpeedPercentage = bonusPercent;
            _owner.Status.AddStatBonus(_bonus);
        }
        _vfx = DariusPrototypeVfx.CreateSummonerGhost(_owner != null ? _owner.transform : null, DariusSummonerBalance.GhostDuration);
        DariusLog.Info("GHOST", "Activated owner=" + DariusLog.EntityLabel(_owner) + " bonus=" + bonusPercent.ToString("0.#") +
            "% duration=" + DariusSummonerBalance.GhostDuration.ToString("0.###"));
        yield return new WaitForSeconds(DariusSummonerBalance.GhostDuration);
        RemoveBonus();
        ClearVfx();
        _routine = null;
    }

    private void OnDestroy()
    {
        RemoveBonus();
        ClearVfx();
    }

    private void ClearVfx()
    {
        if (_vfx != null)
        {
            try { Destroy(_vfx); } catch { }
            _vfx = null;
        }
    }

    private void RemoveBonus()
    {
        if (_bonus != null && _owner != null && _owner.Status != null)
        {
            try { _owner.Status.RemoveStatBonus(_bonus); } catch { }
        }
        _bonus = null;
    }
}

public static class DariusSummonerRuntime
{
    public static Vector3 ResolveDirection(Hero owner, CastInfo info)
    {
        if (owner == null) return Vector3.forward;

        Vector3 dir = Vector3.zero;
        string source = "none";
        Vector3 castPoint = owner.transform.position;
        try { castPoint = info.point; } catch { }

        // Flash is configured as a move-direction skill. v0.18.4 accidentally gave castPoint
        // priority, so mouse/cursor targeting could pull Flash away from the direction the character
        // was actually moving. First use the Darius model bridge's measured world movement; this is
        // independent of EntityControl member names and directly represents the direction seen in game.
        try
        {
            DariusTravelerModelInstance traveler = owner.GetComponentInChildren<DariusTravelerModelInstance>(true);
            Vector3 measured;
            if (traveler != null && traveler.TryGetRecentMovementDirection(out measured))
            {
                dir = measured;
                source = "movement:measured-displacement";
            }
        }
        catch (Exception e)
        {
            DariusLog.DebugInfo("FLASH-DIR", "Measured movement lookup failed: " + e.GetType().Name + ": " + e.Message);
        }

        // Then try live/last EntityControl movement fields for the frame where input direction has
        // just changed but displacement has not yet been sampled.
        try
        {
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            object control = null;
            Type heroType = owner.GetType();
            PropertyInfo controlProperty = heroType.GetProperty("Control", flags) ?? heroType.GetProperty("control", flags);
            if (controlProperty != null && controlProperty.GetIndexParameters().Length == 0)
                control = controlProperty.GetValue(owner, null);
            if (control == null)
            {
                FieldInfo controlField = heroType.GetField("Control", flags) ?? heroType.GetField("control", flags) ??
                                         heroType.GetField("<Control>k__BackingField", flags);
                if (controlField != null) control = controlField.GetValue(owner);
            }
            if (control == null)
            {
                Component[] components = owner.GetComponents<Component>();
                for (int i = 0; i < components.Length; i++)
                {
                    Component c = components[i];
                    if (c != null && c.GetType().Name == "EntityControl") { control = c; break; }
                }
            }

            if (control != null && dir.sqrMagnitude <= 0.04f)
            {
                Type t = control.GetType();
                string[] candidates = { "movementDirection", "moveDirection", "desiredMovementDirection", "lastMovementDirection" };
                for (int i = 0; i < candidates.Length && dir.sqrMagnitude <= 0.04f; i++)
                {
                    string name = candidates[i];
                    PropertyInfo prop = t.GetProperty(name, flags);
                    if (prop != null && prop.GetIndexParameters().Length == 0 && prop.PropertyType == typeof(Vector3))
                    {
                        try { dir = (Vector3)prop.GetValue(control, null); } catch { dir = Vector3.zero; }
                    }
                    if (dir.sqrMagnitude <= 0.04f)
                    {
                        FieldInfo field = t.GetField(name, flags) ?? t.GetField("<" + name + ">k__BackingField", flags);
                        if (field != null && field.FieldType == typeof(Vector3))
                        {
                            try { dir = (Vector3)field.GetValue(control); } catch { dir = Vector3.zero; }
                        }
                    }
                    if (dir.sqrMagnitude > 0.04f) source = "movement:" + name;
                }
                dir.y = 0f;
            }
        }
        catch (Exception e)
        {
            DariusLog.DebugInfo("FLASH-DIR", "Movement-direction reflection failed: " + e.GetType().Name + ": " + e.Message);
        }

        if (dir.sqrMagnitude <= 0.04f)
        {
            try
            {
                dir = castPoint - owner.transform.position;
                dir.y = 0f;
                if (dir.sqrMagnitude > 0.04f) source = "castPoint-fallback";
            }
            catch { dir = Vector3.zero; }
        }

        if (dir.sqrMagnitude <= 0.04f)
        {
            dir = owner.transform.forward;
            dir.y = 0f;
            source = "facing-fallback";
        }

        if (dir.sqrMagnitude <= 0.001f)
        {
            dir = Vector3.forward;
            source = "world-forward-fallback";
        }

        dir.Normalize();
        DariusLog.Info("FLASH-DIR", "source=" + source + " dir=" + DariusLog.Vec(dir) +
            " castPoint=" + DariusLog.Vec(castPoint) + " ownerPos=" + DariusLog.Vec(owner.transform.position));
        return dir;
    }
}
