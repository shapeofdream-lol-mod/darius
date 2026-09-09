using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

// v0.21.0 directional basic-attack pipeline.
// AttackTrigger still owns cadence/crit/attack lifecycle. Target selection no longer owns the swing:
// the cast intent is a direction, the native melee instance is anchored to the caster, and the final
// authoritative hit shape is a caster-centred sector. Every enemy inside that sector may be hit.
public sealed class At_DariusAxe : AttackTrigger
{
    public const float AttackRange = 1.75f;
    public const float AttackArcDegrees = 110f;
    public const float AttackHalfAngle = AttackArcDegrees * 0.5f;
    public const float ContactTolerance = 0.10f;

    public override void OnCastStart(int configIndex, CastInfo info)
    {
        Hero hero = null;
        try { hero = info.caster as Hero; }
        catch (Exception e)
        {
            DariusLog.Exception("ATK-DIR", e, "At_DariusAxe owner lookup failed");
            return;
        }
        if (hero == null)
        {
            DariusLog.Warn("ATK-DIR", "At_DariusAxe.OnCastStart configIndex=" + configIndex + " has no Hero caster.");
            return;
        }

        DariusVoiceRuntime.NotifyAttack(hero);

        Vector3 direction = DariusDirectionalBasicAttackGeometry.ResolveAimDirection(hero, info);
        if (direction.sqrMagnitude > 0.001f)
        {
            direction.y = 0f; direction.Normalize();
            try { info.angle = CastInfo.GetAngle(direction); } catch { }
        }
        DariusDirectionalBasicAttackState state = hero.GetComponent<DariusDirectionalBasicAttackState>();
        if (state == null) state = hero.gameObject.AddComponent<DariusDirectionalBasicAttackState>();
        state.Capture(direction, configIndex);

        // IMPORTANT: no target/range rejection here. A basic attack is always allowed to swing into
        // empty space. Whether anything is hit is decided only by the directional sector at impact.
        base.OnCastStart(configIndex, info);

        float effective = AttackRange;
        try
        {
            if (configs != null && configIndex >= 0 && configIndex < configs.Length && configs[configIndex] != null)
            {
                TriggerConfig cfg = configs[configIndex];
                effective = cfg.effectiveRange > 0.05f ? cfg.effectiveRange : AttackRange;
            }
        }
        catch { }

        DariusLog.DebugInfo("ATK-DIR", "At_DariusAxe.OnCastStart configIndex=" + configIndex +
            " caster=" + DariusLog.EntityLabel(hero) + " dir=" + DariusLog.Vec(direction) +
            " range=" + AttackRange.ToString("0.###") + " arc=" + AttackArcDegrees.ToString("0.#") +
            " effectiveRange=" + effective.ToString("0.###") + " targetIgnoredForAim=true");

        try
        {
            DariusBasicAttackVisualRuntime runtime = hero.GetComponent<DariusBasicAttackVisualRuntime>();
            if (runtime == null) runtime = hero.gameObject.AddComponent<DariusBasicAttackVisualRuntime>();
            runtime.Bind(hero);
            runtime.NotifyNativeAttackStarted(configIndex, info);
        }
        catch (Exception e)
        {
            DariusLog.Exception("ATK-DIR", e, "At_DariusAxe native attack-start visual hook failed");
        }
    }

    public override AbilityInstance OnCastComplete(int configIndex, CastInfo info)
    {
        Hero hero = null; try { hero = info.caster as Hero; } catch { }
        int resolvedConfig = configIndex;
        try { DariusDirectionalBasicAttackState state = hero != null ? hero.GetComponent<DariusDirectionalBasicAttackState>() : null; if (state != null && Time.time - state.CapturedAt <= 1.5f) resolvedConfig = state.ConfigIndex; } catch { }
        AbilityInstance configured = null;
        try { if (configs != null && resolvedConfig >= 0 && resolvedConfig < configs.Length && configs[resolvedConfig] != null) configured = configs[resolvedConfig].spawnedInstance; } catch { }
        if (configured != null)
        {
            try { AbilityInstance native = base.OnCastComplete(resolvedConfig, info); if (native != null) return native; }
            catch (Exception e) { DariusLog.Exception("ATK-COMPLETE", e, "Native basic-attack instance path failed; using registered runtime prefab"); }
        }
        return SpawnDirectRuntimeAttack(hero, resolvedConfig, info);
    }

    private static AbilityInstance SpawnDirectRuntimeAttack(Hero hero, int configIndex, CastInfo info)
    {
        if (hero == null) return null;
        MeleeAttackInstance prefab = configIndex == 1 ? (MeleeAttackInstance)DariusTravelerRegistry.AttackCritInstancePrefab : DariusTravelerRegistry.AttackInstancePrefab;
        if (prefab == null) { DariusLog.Error("ATK-FALLBACK", "Registered runtime attack prefab is null configIndex=" + configIndex); return null; }
        try
        {
            Vector3 direction = DariusDirectionalBasicAttackGeometry.ResolveAimDirection(hero, info);
            DariusDirectionalBasicAttackState state = hero.GetComponent<DariusDirectionalBasicAttackState>(); if (state != null) direction = state.GetDirection(direction);
            direction.y = 0f; if (direction.sqrMagnitude < 0.0001f) direction = hero.transform.forward; if (direction.sqrMagnitude < 0.0001f) direction = Vector3.forward; direction.Normalize();
            MethodInfo generic = FindPrefabCreateMethod(); if (generic == null) { DariusLog.Error("ATK-FALLBACK", "Actor.CreateAbilityInstance<T> prefab overload was not found"); return null; }
            AbilityInstance spawned = generic.MakeGenericMethod(prefab.GetType()).Invoke(hero, new object[] { prefab, hero.transform.position, (Quaternion?)Quaternion.LookRotation(direction, Vector3.up), info, null }) as AbilityInstance;
            DariusLog.Info("ATK-FALLBACK", "Direct runtime basic attack configIndex=" + configIndex + " prefab=" + prefab.name + " result=" + (spawned != null ? spawned.name : "<null>")); return spawned;
        }
        catch (TargetInvocationException e) { DariusLog.Exception("ATK-FALLBACK", e.InnerException ?? e, "Direct runtime basic attack threw"); }
        catch (Exception e) { DariusLog.Exception("ATK-FALLBACK", e, "Direct runtime basic attack failed"); }
        return null;
    }

    private static MethodInfo FindPrefabCreateMethod()
    {
        MethodInfo[] methods = typeof(Actor).GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        for (int i = 0; i < methods.Length; i++) { MethodInfo m = methods[i]; if (m == null || m.Name != "CreateAbilityInstance" || !m.IsGenericMethodDefinition) continue; ParameterInfo[] ps = m.GetParameters(); if (ps.Length == 5 && ps[0].ParameterType.IsGenericParameter && ps[1].ParameterType == typeof(Vector3) && Nullable.GetUnderlyingType(ps[2].ParameterType) == typeof(Quaternion) && ps[3].ParameterType == typeof(CastInfo)) return m; }
        return null;
    }
}

public sealed class Ai_DariusAxe : MeleeAttackInstance
{
    protected override void OnCreate()
    {
        base.OnCreate();
        DariusDirectionalBasicAttackGeometry.AnchorNativeMeleeInstance(this, info, false);
    }
}

public sealed class Ai_DariusAxe_Crit : MeleeAttackInstance
{
    protected override void OnCreate()
    {
        base.OnCreate();
        DariusDirectionalBasicAttackGeometry.AnchorNativeMeleeInstance(this, info, true);
    }
}

public sealed class DariusDirectionalBasicAttackState : MonoBehaviour
{
    public Vector3 Direction { get; private set; }
    public float CapturedAt { get; private set; }
    public int ConfigIndex { get; private set; }
    public int Serial { get; private set; }

    public void Capture(Vector3 direction, int configIndex)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f) direction = transform.forward;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f) direction = Vector3.forward;
        Direction = direction.normalized;
        ConfigIndex = configIndex;
        CapturedAt = Time.time;
        Serial++;
    }

    public Vector3 GetDirection(Vector3 fallback)
    {
        // A native melee instance resolves very shortly after the swing starts. If some unrelated
        // Actor.DoBasicAttackHit path fires much later, do not reuse a stale attack direction.
        if (Time.time - CapturedAt <= 1.25f && Direction.sqrMagnitude > 0.0001f) return Direction;
        fallback.y = 0f;
        return fallback.sqrMagnitude > 0.0001f ? fallback.normalized : Vector3.forward;
    }

    public bool HasFreshCapture => Serial > 0 && Time.time - CapturedAt <= 1.25f;
}

public static class DariusDirectionalBasicAttackGeometry
{
    public static Vector3 ResolveAimDirection(Hero hero, CastInfo info)
    {
        Vector3 direction = Vector3.zero;
        try { Entity target = info.target; if (target != null && target != hero) { direction = target.transform.position - hero.transform.position; direction.y = 0f; } } catch { direction = Vector3.zero; }
        if (direction.sqrMagnitude <= 0.001f)
        {
            try
            {
                direction = info.point - hero.transform.position;
                direction.y = 0f;
            }
            catch { direction = Vector3.zero; }
        }
        if (direction.sqrMagnitude <= 0.001f)
        {
            try { direction = info.forward; direction.y = 0f; } catch { direction = Vector3.zero; }
        }
        if (direction.sqrMagnitude <= 0.001f)
        {
            direction = hero.transform.forward;
            direction.y = 0f;
        }
        if (direction.sqrMagnitude <= 0.001f) direction = Vector3.forward;
        return direction.normalized;
    }

    public static void AnchorNativeMeleeInstance(MeleeAttackInstance instance, CastInfo info, bool critical)
    {
        if (instance == null) return;
        Hero owner = null;
        try { owner = info.caster as Hero; } catch { }
        if (owner == null) return;
        Vector3 direction = ResolveAimDirection(owner, info);
        try
        {
            DariusDirectionalBasicAttackState state = owner.GetComponent<DariusDirectionalBasicAttackState>();
            if (state != null) direction = state.GetDirection(direction);
            instance.transform.position = owner.transform.position;
            if (direction.sqrMagnitude > 0.0001f)
                instance.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
            DariusLog.DebugInfo("ATK-INSTANCE", "Anchored " + instance.name + " to caster=" + DariusLog.EntityLabel(owner) +
                " pos=" + DariusLog.Vec(instance.transform.position) + " dir=" + DariusLog.Vec(direction) +
                " crit=" + critical);
        }
        catch (Exception e) { DariusLog.Exception("ATK-INSTANCE", e, "Failed to anchor directional native melee instance"); }
    }

    public static bool IsInsideAttackSector(Hero_Darius hero, Entity target, out float contactDistance, out float angle, out Vector3 contactPoint)
    {
        contactDistance = float.PositiveInfinity;
        angle = 180f;
        contactPoint = target != null ? target.transform.position : Vector3.zero;
        if (hero == null || target == null || target == hero) return false;

        Vector3 origin = hero.transform.position;
        contactPoint = ClosestContactPoint(target, origin);
        Vector3 delta = contactPoint - origin;
        delta.y = 0f;
        contactDistance = delta.magnitude;
        if (contactDistance <= 0.001f) { angle = 0f; return true; }
        if (contactDistance > At_DariusAxe.AttackRange + At_DariusAxe.ContactTolerance) return false;

        Vector3 fallback = hero.transform.forward;
        DariusDirectionalBasicAttackState state = hero.GetComponent<DariusDirectionalBasicAttackState>();
        Vector3 direction = state != null ? state.GetDirection(fallback) : fallback;
        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.0001f) direction = Vector3.forward;
        direction.Normalize();
        angle = Vector3.Angle(direction, delta / contactDistance);
        return angle <= At_DariusAxe.AttackHalfAngle + 0.5f;
    }

    private static Vector3 ClosestContactPoint(Entity target, Vector3 origin)
    {
        Vector3 best = target.transform.position;
        float bestSqr = HorizontalSqr(best - origin);
        try
        {
            Collider[] colliders = target.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider c = colliders[i];
                if (c == null || !c.enabled) continue;
                Vector3 p = c.ClosestPoint(origin);
                float sqr = HorizontalSqr(p - origin);
                if (sqr < bestSqr) { bestSqr = sqr; best = p; }
            }
        }
        catch { }
        return best;
    }

    private static float HorizontalSqr(Vector3 v) { return v.x * v.x + v.z * v.z; }
}

// The stock MeleeAttackInstance remains responsible for native damage/crit/on-hit event semantics,
// but every generated hit must lie in the directional sector. Because the stock melee instance is
// an overlap/AOE actor, all enemies inside the sector can pass this filter during the same swing.
[HarmonyPatch]
public static class DariusDirectionalBasicAttackSectorPatch
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        MethodInfo[] methods = typeof(Actor).GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        for (int i = 0; i < methods.Length; i++)
        {
            MethodInfo m = methods[i];
            if (m == null || !string.Equals(m.Name, "DoBasicAttackHit", StringComparison.Ordinal)) continue;
            ParameterInfo[] ps = m.GetParameters();
            bool hasEntity = false;
            for (int j = 0; j < ps.Length; j++)
            {
                if (typeof(Entity).IsAssignableFrom(ps[j].ParameterType)) { hasEntity = true; break; }
            }
            if (hasEntity) yield return m;
        }
    }

    private static bool Prefix(Actor __instance, object[] __args)
    {
        Hero_Darius hero = __instance as Hero_Darius;
        if (hero == null) return true;
        Entity target = null;
        if (__args != null)
        {
            for (int i = 0; i < __args.Length; i++)
            {
                target = __args[i] as Entity;
                if (target != null && !ReferenceEquals(target, __instance)) break;
                target = null;
            }
        }
        if (target == null) return true;

        // Network/animation delay can outlive the short directional snapshot. In that case the
        // custom sector no longer has authoritative intent and must not veto a valid native hit.
        DariusDirectionalBasicAttackState captured = hero.GetComponent<DariusDirectionalBasicAttackState>();
        if (captured == null || !captured.HasFreshCapture) return true;

        try
        {
            float dist, angle; Vector3 contact;
            bool inside = DariusDirectionalBasicAttackGeometry.IsInsideAttackSector(hero, target, out dist, out angle, out contact);
            if (inside)
            {
                DariusLog.DebugInfoThrottled("ATK-SECTOR", "pass:" + target.GetInstanceID(),
                    "PASS target=" + DariusLog.EntityLabel(target) + " contactDist=" + dist.ToString("0.###") +
                    " angle=" + angle.ToString("0.##") + " contact=" + DariusLog.Vec(contact), 0.20);
                return true;
            }
            DariusLog.DebugInfoThrottled("ATK-SECTOR", "block:" + target.GetInstanceID(),
                "BLOCK target=" + DariusLog.EntityLabel(target) + " contactDist=" + dist.ToString("0.###") +
                " angle=" + angle.ToString("0.##") + " range=" + At_DariusAxe.AttackRange.ToString("0.###") +
                " halfAngle=" + At_DariusAxe.AttackHalfAngle.ToString("0.#"), 0.20);
            return false;
        }
        catch (Exception e)
        {
            DariusLog.Exception("ATK-SECTOR", e, "Directional sector validation failed; allowing native hit as fail-safe");
            return true;
        }
    }
}

// Lives on Hero_Darius itself. It asserts the Darius-owned attack preset at the exact native
// EntityAbility lifecycle boundary instead of relying on a one-time prefab assignment.
public sealed class DariusNativeAttackBinder : MonoBehaviour
{
    public void EnsureBound(string reason)
    {
        Hero_Darius hero = GetComponent<Hero_Darius>();
        EntityAbility ability = GetComponent<EntityAbility>();
        At_DariusAxe attack = DariusTravelerRegistry.AttackPrefab;
        if (hero == null || ability == null || attack == null) return;
        try
        {
            ability.attackAbilityPreset = new AssetRef<AttackTrigger>(attack);
            DariusLog.DebugInfo("ATK-NATIVE-BIND", "Bound EntityAbility.attackAbilityPreset -> At_DariusAxe owner=" + hero.name + " reason=" + reason);
        }
        catch (Exception e)
        {
            DariusLog.Exception("ATK-NATIVE-BIND", e, "Failed to bind Darius native attack reason=" + reason);
        }
    }

    private void Awake() { EnsureBound("Hero_Darius.Awake"); }
    private void OnEnable() { EnsureBound("Hero_Darius.OnEnable"); }
}
