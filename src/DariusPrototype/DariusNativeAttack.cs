using System;
using System.Reflection;
using UnityEngine;

// v0.21.0 directional basic-attack pipeline.
// AttackTrigger still owns cadence/crit/attack lifecycle. Darius follows the stock Vesper input
// contract (Target + allowNonTargetedCast) while final damage geometry remains a Darius-owned
// forward sector. Every enemy inside that sector may be hit.
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

    // The stock melee references (Vesper/Mist/Husk) all use Target cast semantics and permit
    // non-targeted swings through AttackTrigger.allowNonTargetedCast. Keep Darius on that native
    // input path instead of using an ability-style Cone indicator; the Darius sector patch remains
    // the single authority for the final 90-degree cleave geometry.
    public static void ApplyVesperStyleTriggerSemantics(At_DariusAxe attack)
    {
        if (attack == null) return;
        try
        {
            attack.allowNonTargetedCast = true;
            attack.ignoreRangeCheck = false;

            TriggerConfig[] attackConfigs = attack.configs;
            if (attackConfigs == null) return;
            for (int i = 0; i < attackConfigs.Length; i++)
            {
                TriggerConfig cfg = attackConfigs[i];
                if (cfg == null || cfg.castMethod == null) continue;
                cfg.castMethod.type = CastMethodType.Target;
                cfg.castMethod._range = AttackRange;
                cfg.castMethod._radius = 0f;
                cfg.castMethod._angle = 0f;
                cfg.castMethod._isClamping = false;
                cfg.faceForward = true;
            }
        }
        catch (Exception e)
        {
            DariusLog.Exception("ATK-NATIVE-CONFIG", e, "Could not apply Vesper-style Darius attack semantics");
        }
    }

    // Darius attack instances are structurally cloned from Vesper. Preserve Vesper's fixed melee
    // broad phase instead of treating TriggerConfig.effectiveRange as a collider scale factor.
    // The registry historically normalized the cloned DewCollider to AttackRange; this read-only
    // stock-copy step restores Vesper's exact geometry on the inactive Darius prefab before a live
    // instance is spawned. Final Darius reach/arc is still enforced by the sector hit patch.
    public static void AlignBroadPhaseWithVesper(MeleeAttackInstance destination, bool critical)
    {
        if (destination == null || DewResources.database == null) return;
        string sourceTypeName = critical ? "Ai_Atk_VesperMace_Crit" : "Ai_Atk_VesperMace";
        MeleeAttackInstance source = ResolveStockMeleeByTypeName(sourceTypeName);
        if (source == null)
        {
            DariusLog.Warn("ATK-NATIVE-PREFAB", "Could not resolve stock Vesper melee source " + sourceTypeName + "; keeping cloned fixed broad-phase.");
            return;
        }

        try
        {
            SetBoolMember(destination, "scaleRangeWithTriggerRange", false);
            Component[] sourceComponents = source.GetComponentsInChildren<Component>(true);
            Component[] destinationComponents = destination.GetComponentsInChildren<Component>(true);
            int sourceDewCount = CountDewColliders(sourceComponents);
            int destinationDewCount = CountDewColliders(destinationComponents);
            int copyCount = Mathf.Min(sourceDewCount, destinationDewCount);
            int copiedFields = 0;
            for (int i = 0; i < copyCount; i++)
            {
                Component sourceDew = GetNthDewCollider(sourceComponents, i);
                Component destinationDew = GetNthDewCollider(destinationComponents, i);
                copiedFields += CopyColliderGeometry(sourceDew, destinationDew);
            }
            DariusLog.DebugInfo("ATK-NATIVE-PREFAB", "Aligned " + destination.name + " broad-phase with " + sourceTypeName +
                " dewColliders=" + copyCount + " copiedGeometryFields=" + copiedFields + " scaleWithTriggerRange=false");
        }
        catch (Exception e)
        {
            DariusLog.Exception("ATK-NATIVE-PREFAB", e, "Failed restoring Vesper broad-phase geometry for " + destination.name);
        }
    }

    private static int CountDewColliders(Component[] components)
    {
        if (components == null) return 0;
        int count = 0;
        for (int i = 0; i < components.Length; i++)
            if (components[i] != null && components[i].GetType().Name.IndexOf("DewCollider", StringComparison.OrdinalIgnoreCase) >= 0) count++;
        return count;
    }

    private static Component GetNthDewCollider(Component[] components, int index)
    {
        if (components == null || index < 0) return null;
        int current = 0;
        for (int i = 0; i < components.Length; i++)
        {
            Component component = components[i];
            if (component == null || component.GetType().Name.IndexOf("DewCollider", StringComparison.OrdinalIgnoreCase) < 0) continue;
            if (current == index) return component;
            current++;
        }
        return null;
    }

    private static int CopyColliderGeometry(Component source, Component destination)
    {
        if (source == null || destination == null) return 0;
        int copied = 0;
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        for (Type sourceType = source.GetType(); sourceType != null && sourceType != typeof(Component); sourceType = sourceType.BaseType)
        {
            FieldInfo[] sourceFields = sourceType.GetFields(flags);
            for (int i = 0; i < sourceFields.Length; i++)
            {
                FieldInfo sourceField = sourceFields[i];
                if (sourceField.IsStatic || !LooksLikeColliderGeometry(sourceField.Name)) continue;
                FieldInfo destinationField = FindField(destination.GetType(), sourceField.Name, sourceField.FieldType);
                if (destinationField == null || destinationField.IsStatic || destinationField.IsInitOnly) continue;
                try
                {
                    destinationField.SetValue(destination, sourceField.GetValue(source));
                    copied++;
                }
                catch { }
            }
        }
        return copied;
    }

    private static bool LooksLikeColliderGeometry(string name)
    {
        if (string.IsNullOrEmpty(name)) return false;
        string n = name.ToLowerInvariant();
        return n.Contains("shape") || n.Contains("radius") || n.Contains("range") || n.Contains("size") ||
               n.Contains("center") || n.Contains("offset") || n.Contains("point") || n.Contains("vert") || n.Contains("polygon");
    }

    private static FieldInfo FindField(Type type, string name, Type fieldType)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        for (Type current = type; current != null; current = current.BaseType)
        {
            FieldInfo field = current.GetField(name, flags);
            if (field != null && field.FieldType == fieldType) return field;
        }
        return null;
    }

    private static void SetBoolMember(object owner, string name, bool value)
    {
        if (owner == null) return;
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        for (Type type = owner.GetType(); type != null; type = type.BaseType)
        {
            FieldInfo field = type.GetField(name, flags) ?? type.GetField("<" + name + ">k__BackingField", flags);
            if (field != null && field.FieldType == typeof(bool) && !field.IsInitOnly)
            {
                field.SetValue(owner, value);
                return;
            }
        }
        PropertyInfo property = owner.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (property != null && property.CanWrite && property.PropertyType == typeof(bool)) property.SetValue(owner, value, null);
    }

    private static MeleeAttackInstance ResolveStockMeleeByTypeName(string typeName)
    {
        Type resourceType = null;
        Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
        for (int i = 0; i < assemblies.Length && resourceType == null; i++)
        {
            try { resourceType = assemblies[i].GetType(typeName, false); }
            catch { }
        }
        if (resourceType == null) return null;

        MethodInfo[] methods = typeof(DewResources).GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
        for (int i = 0; i < methods.Length; i++)
        {
            MethodInfo method = methods[i];
            if (method.Name != "GetByType" || method.IsGenericMethod) continue;
            ParameterInfo[] parameters = method.GetParameters();
            if (parameters.Length < 1 || parameters[0].ParameterType != typeof(Type)) continue;
            object[] args = new object[parameters.Length];
            args[0] = resourceType;
            for (int j = 1; j < parameters.Length; j++)
                args[j] = parameters[j].HasDefaultValue ? parameters[j].DefaultValue :
                    (parameters[j].ParameterType.IsValueType ? Activator.CreateInstance(parameters[j].ParameterType) : null);
            try
            {
                MeleeAttackInstance result = method.Invoke(null, args) as MeleeAttackInstance;
                if (result != null) return result;
            }
            catch { }
        }
        return null;
    }

    public static void ApplyVesperStyleNativeContract(At_DariusAxe attack)
    {
        ApplyVesperStyleTriggerSemantics(attack);
        AlignBroadPhaseWithVesper(DariusTravelerRegistry.AttackInstancePrefab, false);
        AlignBroadPhaseWithVesper(DariusTravelerRegistry.AttackCritInstancePrefab, true);
    }

    public override void OnCastStart(int configIndex, CastInfo info)
    {
        // Reassert the stock-melee contract at the lifecycle boundary. The Hero binder applies this
        // earlier for indicator/input presentation; this call protects hot-reload/self-heal paths.
        ApplyVesperStyleNativeContract(this);

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

        // Match stock melee behaviour: a target is useful for acquisition/aiming, but an empty-space
        // attack remains legal because allowNonTargetedCast=true. Final damage is decided at impact.
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
        // Vesper's stock melee instance keeps scaleRangeWithTriggerRange=false. Darius now retains
        // the same fixed broad-phase semantics; the directional sector performs the precise range cut.
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
            At_DariusAxe.ApplyVesperStyleNativeContract(attack);
            ability.attackAbilityPreset = new AssetRef<AttackTrigger>(attack);
            DariusLog.DebugInfo("ATK-NATIVE-BIND", "Bound EntityAbility.attackAbilityPreset -> At_DariusAxe owner=" + hero.name +
                " reason=" + reason + " method=Target range=" + At_DariusAxe.AttackRange.ToString("0.###") +
                " arc=" + At_DariusAxe.AttackArcDegrees.ToString("0.#") + " broadPhase=VesperFixed");
        }
        catch (Exception e)
        {
            DariusLog.Exception("ATK-NATIVE-BIND", e, "Failed to bind Darius native attack reason=" + reason);
        }
    }

    private void Awake() { EnsureBound("Hero_Darius.Awake"); }
    private void OnEnable() { EnsureBound("Hero_Darius.OnEnable"); }
}
