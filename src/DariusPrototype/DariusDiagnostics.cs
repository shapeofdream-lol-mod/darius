using System;
using System.Collections;
using System.Reflection;
using System.Text;
using HarmonyLib;
using Mirror;
using UnityEngine;

// High-level flight recorder for first-pass in-game testing.
// Keeps the log useful even if a failure happens outside one of the skill try/catch blocks.
public sealed class DariusDiagnostics : MonoBehaviour
{
    private static bool _nativeMeleeReferenceDumped;

    private float _nextHeartbeat;
    private bool _lastServer;
    private bool _lastClient;
    private int _lastHeroId;
    private bool _unityHooked;
    private bool _nativeMeleeDumpStarted;

    public void Initialize()
    {
        if (!_unityHooked)
        {
            Application.logMessageReceivedThreaded += OnUnityLog;
            _unityHooked = true;
        }

        _lastServer = SafeServerActive();
        _lastClient = SafeClientActive();
        _lastHeroId = GetHeroId();
        _nextHeartbeat = Time.unscaledTime + 3f;
        DariusLog.Info("DIAG", "Diagnostics initialized. " + Snapshot());

        if (!_nativeMeleeDumpStarted)
        {
            _nativeMeleeDumpStarted = true;
            StartCoroutine(DumpNativeMeleeReferencesWhenReady());
        }
    }

    private IEnumerator DumpNativeMeleeReferencesWhenReady()
    {
        while (DewResources.database == null)
            yield return null;

        // Give stock resource maps one extra frame to finish their normal boot population.
        yield return null;
        DumpNativeMeleeReferenceSet();
    }

    public static void DumpNativeMeleeReferenceSet()
    {
        if (_nativeMeleeReferenceDumped || DewResources.database == null) return;
        _nativeMeleeReferenceDumped = true;

        DariusLog.Info("ATK-REF", "Native melee reference dump BEGIN. Read-only; no stock resource is mutated.");
        DumpNativeAttack("Vesper", "At_Atk_VesperMace");
        DumpNativeAttack("Mist", "At_Atk_MistSabre");
        DumpNativeAttack("Husk", "At_Atk_HuskSword");
        DariusLog.Info("ATK-REF", "Darius comparison constants fallbackRange=" + At_DariusAxe.AttackRange.ToString("0.###") +
            " arc=" + At_DariusAxe.AttackArcDegrees.ToString("0.#") + " tolerance=" + At_DariusAxe.ContactTolerance.ToString("0.###"));
        DariusLog.Info("ATK-REF", "Native melee reference dump END.");
    }

    private static void DumpNativeAttack(string label, string triggerTypeName)
    {
        AttackTrigger trigger = ResolveResourceByTypeName(triggerTypeName) as AttackTrigger;
        if (trigger == null)
        {
            DariusLog.Warn("ATK-REF", label + " trigger not found type=" + triggerTypeName);
            return;
        }

        bool ignoreRangeCheck = false;
        try { ignoreRangeCheck = trigger.ignoreRangeCheck; } catch { }
        DariusLog.Info("ATK-REF", label + " trigger=" + trigger.name +
            " type=" + trigger.GetType().FullName +
            " allowNonTargetedCast=" + trigger.allowNonTargetedCast +
            " ignoreRangeCheck=" + ignoreRangeCheck +
            " configs=" + (trigger.configs != null ? trigger.configs.Length.ToString() : "<null>"));

        TriggerConfig[] configs = trigger.configs;
        if (configs == null) return;
        for (int i = 0; i < configs.Length; i++)
        {
            TriggerConfig cfg = configs[i];
            if (cfg == null)
            {
                DariusLog.Warn("ATK-REF", label + " config[" + i + "]=<null>");
                continue;
            }

            CastMethodData method = cfg.castMethod;
            float effectiveRange = -1f;
            try { effectiveRange = cfg.effectiveRange; } catch { }
            string methodText = method == null
                ? "<null>"
                : method.type + " range=" + method._range.ToString("0.###") +
                  " radius=" + method._radius.ToString("0.###") +
                  " angle=" + method._angle.ToString("0.###") +
                  " clamp=" + method._isClamping;
            AbilityInstance spawned = null;
            try { spawned = cfg.spawnedInstance; } catch { }

            DariusLog.Info("ATK-REF", label + " config[" + i + "] effectiveRange=" + effectiveRange.ToString("0.###") +
                " castMethod=" + methodText +
                " faceForward=" + cfg.faceForward +
                " castByMoveDefault=" + cfg.castByMoveDirectionByDefault +
                " castByMoveGamepad=" + cfg.castByMoveDirectionGamepad +
                " spawned=" + (spawned != null ? spawned.name + "/" + spawned.GetType().Name : "<null>"));

            MeleeAttackInstance melee = spawned as MeleeAttackInstance;
            if (melee != null) DumpMeleeInstance(label + " config[" + i + "]", melee);
        }
    }

    private static void DumpMeleeInstance(string label, MeleeAttackInstance melee)
    {
        object scaleWithTrigger = ReadMemberValue(melee, "scaleRangeWithTriggerRange");
        DariusLog.Info("ATK-REF", label + " melee=" + melee.name +
            " type=" + melee.GetType().FullName +
            " scaleRangeWithTriggerRange=" + FormatValue(scaleWithTrigger) +
            " localScale=" + DariusLog.Vec(melee.transform.localScale));

        try
        {
            Collider[] colliders = melee.GetComponentsInChildren<Collider>(true);
            DariusLog.Info("ATK-REF", label + " unityColliders=" + colliders.Length);
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider c = colliders[i];
                if (c == null) continue;
                SphereCollider sphere = c as SphereCollider;
                CapsuleCollider capsule = c as CapsuleCollider;
                BoxCollider box = c as BoxCollider;
                string geometry;
                if (sphere != null)
                    geometry = "sphere radius=" + sphere.radius.ToString("0.###") + " center=" + DariusLog.Vec(sphere.center);
                else if (capsule != null)
                    geometry = "capsule radius=" + capsule.radius.ToString("0.###") + " height=" + capsule.height.ToString("0.###") +
                               " direction=" + capsule.direction + " center=" + DariusLog.Vec(capsule.center);
                else if (box != null)
                    geometry = "box size=" + DariusLog.Vec(box.size) + " center=" + DariusLog.Vec(box.center);
                else
                    geometry = c.GetType().Name;

                DariusLog.Info("ATK-REF", label + " collider[" + i + "] " + geometry +
                    " enabled=" + c.enabled + " trigger=" + c.isTrigger +
                    " transformScale=" + DariusLog.Vec(c.transform.localScale));
            }

            Component[] components = melee.GetComponentsInChildren<Component>(true);
            int dewIndex = 0;
            for (int i = 0; i < components.Length; i++)
            {
                Component component = components[i];
                if (component == null || component.GetType().Name.IndexOf("DewCollider", StringComparison.OrdinalIgnoreCase) < 0) continue;
                DariusLog.Info("ATK-REF", label + " dewCollider[" + dewIndex + "] type=" + component.GetType().FullName +
                    " " + DescribeGeometryMembers(component));
                dewIndex++;
            }
        }
        catch (Exception e)
        {
            DariusLog.Exception("ATK-REF", e, "Could not inspect melee colliders for " + label);
        }
    }

    private static string DescribeGeometryMembers(Component component)
    {
        if (component == null) return "<null>";
        StringBuilder sb = new StringBuilder();
        int count = 0;
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        for (Type t = component.GetType(); t != null && t != typeof(Component) && count < 20; t = t.BaseType)
        {
            FieldInfo[] fields = t.GetFields(flags);
            for (int i = 0; i < fields.Length && count < 20; i++)
            {
                FieldInfo field = fields[i];
                if (field.IsStatic || !LooksLikeGeometryMember(field.Name)) continue;
                try
                {
                    object value = field.GetValue(component);
                    string formatted = FormatValue(value);
                    if (formatted == null) continue;
                    if (sb.Length > 0) sb.Append(' ');
                    sb.Append(field.Name).Append('=').Append(formatted);
                    count++;
                }
                catch { }
            }
        }
        return sb.Length > 0 ? sb.ToString() : "geometryMembers=<none-readable>";
    }

    private static bool LooksLikeGeometryMember(string name)
    {
        if (string.IsNullOrEmpty(name)) return false;
        string n = name.ToLowerInvariant();
        return n.Contains("shape") || n.Contains("radius") || n.Contains("range") || n.Contains("size") ||
               n.Contains("center") || n.Contains("offset") || n.Contains("scale") || n.Contains("width") || n.Contains("height");
    }

    private static object ReadMemberValue(object owner, string name)
    {
        if (owner == null || string.IsNullOrEmpty(name)) return null;
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        for (Type t = owner.GetType(); t != null; t = t.BaseType)
        {
            FieldInfo field = t.GetField(name, flags) ?? t.GetField("<" + name + ">k__BackingField", flags);
            if (field != null)
            {
                try { return field.GetValue(owner); } catch { return null; }
            }
        }
        try
        {
            PropertyInfo property = owner.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property != null && property.CanRead) return property.GetValue(owner, null);
        }
        catch { }
        return null;
    }

    private static string FormatValue(object value)
    {
        if (value == null) return "<null>";
        if (value is float f) return f.ToString("0.###");
        if (value is double d) return d.ToString("0.###");
        if (value is Vector2 v2) return "(" + v2.x.ToString("0.###") + "," + v2.y.ToString("0.###") + ")";
        if (value is Vector3 v3) return DariusLog.Vec(v3);
        Type t = value.GetType();
        if (t.IsPrimitive || t.IsEnum || value is string) return value.ToString();
        return null;
    }

    private static UnityEngine.Object ResolveResourceByTypeName(string typeName)
    {
        Type resourceType = AccessTools.TypeByName(typeName);
        if (resourceType == null) return null;
        MethodInfo[] methods = typeof(DewResources).GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
        for (int i = 0; i < methods.Length; i++)
        {
            MethodInfo m = methods[i];
            if (m.Name != "GetByType" || m.IsGenericMethod) continue;
            ParameterInfo[] ps = m.GetParameters();
            if (ps.Length < 1 || ps[0].ParameterType != typeof(Type)) continue;
            object[] args = new object[ps.Length];
            args[0] = resourceType;
            for (int j = 1; j < ps.Length; j++)
                args[j] = ps[j].HasDefaultValue ? ps[j].DefaultValue : (ps[j].ParameterType.IsValueType ? Activator.CreateInstance(ps[j].ParameterType) : null);
            try
            {
                UnityEngine.Object result = m.Invoke(null, args) as UnityEngine.Object;
                if (result != null) return result;
            }
            catch { }
        }
        return null;
    }

    private void Update()
    {
        bool server = SafeServerActive();
        bool client = SafeClientActive();
        int heroId = GetHeroId();

        if (server != _lastServer || client != _lastClient)
        {
            DariusLog.Info("DIAG-NET", "Network state changed server=" + _lastServer + "->" + server +
                " client=" + _lastClient + "->" + client);
            _lastServer = server;
            _lastClient = client;
        }

        if (heroId != _lastHeroId)
        {
            DariusLog.Info("DIAG-HERO", "Local hero changed instanceId=" + _lastHeroId + "->" + heroId +
                " hero=" + DariusLog.EntityLabel(DewPlayer.local != null ? DewPlayer.local.hero : null));
            _lastHeroId = heroId;
        }

        if (Time.unscaledTime >= _nextHeartbeat)
        {
            _nextHeartbeat = Time.unscaledTime + 30f;
            DariusLog.DebugInfo("HEARTBEAT", Snapshot());
        }
    }

    private void OnDestroy()
    {
        if (_unityHooked)
        {
            Application.logMessageReceivedThreaded -= OnUnityLog;
            _unityHooked = false;
        }
        DariusLog.Info("DIAG", "Diagnostics destroyed.");
    }

    private static void OnUnityLog(string condition, string stackTrace, LogType type)
    {
        // DariusLog itself writes through Unity's logger. Ignore those messages to prevent recursion/duplication.
        if (!string.IsNullOrEmpty(condition) && condition.StartsWith("[DariusPrototype]", StringComparison.Ordinal))
            return;

        if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert)
            return;

        try
        {
            string msg = "Unity " + type + ": " + (condition ?? "<null>");
            if (!string.IsNullOrEmpty(stackTrace)) msg += Environment.NewLine + stackTrace;
            DariusLog.Error("UNITY", msg);
        }
        catch { }
    }

    public static string Snapshot()
    {
        StringBuilder sb = new StringBuilder();
        try
        {
            sb.Append("server=").Append(NetworkServer.active)
              .Append(" client=").Append(NetworkClient.active)
              .Append(" frame=").Append(Time.frameCount)
              .Append(" t=").Append(Time.time.ToString("0.00"))
              .Append(" unscaled=").Append(Time.unscaledTime.ToString("0.00"));
        }
        catch (Exception e)
        {
            sb.Append("network/time snapshot failed: ").Append(e.Message);
        }

        try
        {
            sb.Append(" player=").Append(DewPlayer.local != null ? DewPlayer.local.name : "<null>")
              .Append(" hero=").Append(DariusLog.EntityLabel(DewPlayer.local != null ? DewPlayer.local.hero : null));
        }
        catch (Exception e)
        {
            sb.Append(" player snapshot failed: ").Append(e.Message);
        }

        try
        {
            sb.Append(" resourceDB=").Append(DewResources.database != null ? "ready" : "null");
        }
        catch (Exception e)
        {
            sb.Append(" resourceDB snapshot failed: ").Append(e.Message);
        }

        return sb.ToString();
    }

    private static int GetHeroId()
    {
        try { return DewPlayer.local != null && DewPlayer.local.hero != null ? DewPlayer.local.hero.GetInstanceID() : 0; }
        catch { return 0; }
    }

    private static bool SafeServerActive()
    {
        try { return NetworkServer.active; }
        catch { return false; }
    }

    private static bool SafeClientActive()
    {
        try { return NetworkClient.active; }
        catch { return false; }
    }
}
