using System;
using UnityEngine;

// v0.21.0 directional basic-attack pipeline.
// AttackTrigger keeps stock melee cadence/crit/attack lifecycle and Target acquisition semantics.
// Darius only snapshots the accepted swing intent/range and applies its final forward-sector filter.
public sealed class At_DariusAxe : AttackTrigger
{
    public const float AttackRange = 2.60f;
    public const float AttackArcDegrees = 90f;
    public const float AttackHalfAngle = AttackArcDegrees * 0.5f;
    public const float ContactTolerance = 0.10f;

    public static float ResolveEffectiveRange(TriggerConfig cfg)
    {
        if (cfg == null) return AttackRange;
        try
        {
            if (cfg.effectiveRange > 0.05f) return cfg.effectiveRange;
            if (cfg.castMethod != null && cfg.castMethod._range > 0.05f) return cfg.castMethod._range;
        }
        catch { }
        return AttackRange;
    }

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
        TriggerConfig activeConfig = null;
        try
        {
            if (configs != null && configIndex >= 0 && configIndex < configs.Length)
                activeConfig = configs[configIndex];
        }
        catch { }
        float effective = ResolveEffectiveRange(activeConfig);

        DariusDirectionalBasicAttackState state = hero.GetComponent<DariusDirectionalBasicAttackState>();
        if (state == null) state = hero.gameObject.AddComponent<DariusDirectionalBasicAttackState>();
        state.Capture(direction, configIndex, effective);

        // The registered preset follows stock melee Target semantics and permits empty-space swings
        // through AttackTrigger.allowNonTargetedCast. Final damage geometry is decided at impact.
        base.OnCastStart(configIndex, info);

        DariusLog.DebugInfo("ATK-DIR", "At_DariusAxe.OnCastStart configIndex=" + configIndex +
            " caster=" + DariusLog.EntityLabel(hero) + " dir=" + DariusLog.Vec(direction) +
            " fallbackRange=" + AttackRange.ToString("0.###") + " arc=" + AttackArcDegrees.ToString("0.#") +
            " effectiveRange=" + effective.ToString("0.###") + " castMethod=Target allowNonTargeted=true");

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
        Hero hero = null;
        try { hero = info.caster as Hero; } catch { }

        int resolvedConfig = configIndex;
        try
        {
            DariusDirectionalBasicAttackState state = hero != null ? hero.GetComponent<DariusDirectionalBasicAttackState>() : null;
            if (state != null && Time.time - state.CapturedAt <= 1.5f) resolvedConfig = state.ConfigIndex;
        }
        catch { }

        AbilityInstance configured = null;
        try
        {
            if (configs != null && resolvedConfig >= 0 && resolvedConfig < configs.Length && configs[resolvedConfig] != null)
                configured = configs[resolvedConfig].spawnedInstance;
        }
        catch { }

        DariusLog.DebugInfo("PIPELINE", "skill=AA stage=trigger-input config=" + configIndex +
            " resolvedConfig=" + resolvedConfig +
            " caster=" + DariusLog.EntityLabel(info.caster) +
            " configured=" + (configured != null ? configured.name : "<null>") +
            " state={" + DariusRuntimeAudit.DescribeAbilityState(this, resolvedConfig) + "}");

        if (configured == null)
        {
            DariusLog.Error("ATK-COMPLETE", "Native basic-attack config has no spawnedInstance; direct runtime fallback is disabled. state={" +
                DariusRuntimeAudit.DescribeAbilityState(this, resolvedConfig) + "}");
            return null;
        }

        try
        {
            AbilityInstance native = base.OnCastComplete(resolvedConfig, info);
            DariusLog.DebugInfo("PIPELINE", "skill=AA stage=native-complete-output config=" + resolvedConfig +
                " result=" + (native != null ? native.name : "<null>") +
                " state={" + DariusRuntimeAudit.DescribeAbilityState(this, resolvedConfig) + "}");
            if (native == null)
                DariusLog.Error("ATK-COMPLETE", "Native basic-attack completion returned no instance; direct runtime fallback is disabled. state={" +
                    DariusRuntimeAudit.DescribeAbilityState(this, resolvedConfig) + "}");
            return native;
        }
        catch (Exception e)
        {
            DariusLog.Exception("ATK-COMPLETE", e, "Native basic-attack instance path failed; no direct runtime fallback will run. state={" +
                DariusRuntimeAudit.DescribeAbilityState(this, resolvedConfig) + "}");
            throw;
        }
    }
}

public sealed class Ai_DariusAxe : MeleeAttackInstance
{
    protected override void OnCreate()
    {
        base.OnCreate();
        DariusLog.DebugInfo("PIPELINE", "skill=AA stage=instance-create crit=false instance=" + name +
            " caster=" + DariusLog.EntityLabel(info.caster));
        DariusDirectionalBasicAttackGeometry.AnchorNativeMeleeInstance(this, info, false);
    }
}

public sealed class Ai_DariusAxe_Crit : MeleeAttackInstance
{
    protected override void OnCreate()
    {
        base.OnCreate();
        DariusLog.DebugInfo("PIPELINE", "skill=AA stage=instance-create crit=true instance=" + name +
            " caster=" + DariusLog.EntityLabel(info.caster));
        DariusDirectionalBasicAttackGeometry.AnchorNativeMeleeInstance(this, info, true);
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
