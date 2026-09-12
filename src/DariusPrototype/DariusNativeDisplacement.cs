using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

// Native-first forced-movement bridge for Darius E.
// Shape of Dreams exposes EntityControl displacement primitives (Displacement / DispByDestination).
// Their exact public surface has changed between builds, so this adapter resolves that contract
// reflectively and only falls back to a short server-authoritative interpolation if the native API
// cannot be invoked. It never uses Entity.Teleport for Apprehend.
public static class DariusNativeDisplacement
{
    private const BindingFlags AnyInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private const BindingFlags AnyStatic = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    private static bool _loggedContract;

    public static bool TryPull(Entity target, Vector3 destination, float duration, string reason)
    {
        if (target == null || target.IsNullInactiveDeadOrKnockedOut()) return false;
        try
        {
            object control = FindControl(target);
            if (control == null)
            {
                LogContractOnce("EntityControl component/property was not found");
                return false;
            }

            Type dispType = FindRuntimeType("DispByDestination");
            if (dispType == null)
            {
                LogContractOnce("DispByDestination type was not found");
                return false;
            }

            object displacement = CreateDisplacement(dispType, target, destination, duration);
            if (displacement == null)
            {
                LogContractOnce("DispByDestination could not be constructed");
                return false;
            }

            string[] preferred = { "StartDisplacement", "StartDisplacementLocal" };
            for (int n = 0; n < preferred.Length; n++)
            {
                MethodInfo[] methods = control.GetType().GetMethods(AnyInstance);
                for (int i = 0; i < methods.Length; i++)
                {
                    MethodInfo m = methods[i];
                    if (!string.Equals(m.Name, preferred[n], StringComparison.Ordinal)) continue;
                    object[] args;
                    if (!TryBuildArgs(m.GetParameters(), displacement, target, control, destination, duration, out args)) continue;
                    try
                    {
                        m.Invoke(control, args);
                        DariusLog.Info("E-DISPLACE", "Native " + m.Name + " via " + dispType.Name +
                            " target=" + DariusLog.EntityLabel(target) + " dest=" + DariusLog.Vec(destination) +
                            " duration=" + duration.ToString("0.###") + " reason=" + reason);
                        return true;
                    }
                    catch (TargetInvocationException tie)
                    {
                        Exception inner = tie.InnerException ?? tie;
                        DariusLog.Exception("E-DISPLACE", inner, "Native " + m.Name + " invocation failed");
                    }
                    catch (Exception e)
                    {
                        DariusLog.Exception("E-DISPLACE", e, "Native " + m.Name + " invocation failed");
                    }
                }
            }

            LogContractOnce("No compatible EntityControl.StartDisplacement/StartDisplacementLocal overload accepted DispByDestination");
        }
        catch (Exception e)
        {
            DariusLog.Exception("E-DISPLACE", e, "Native displacement bridge failed");
        }
        return false;
    }

    // Used only when the native displacement contract cannot be reached. This remains a visible,
    // time-based pull and deliberately avoids the old one-frame Teleport(target, destination).
    public static IEnumerator SmoothFallback(List<PullEntry> entries, float duration)
    {
        if (entries == null || entries.Count == 0) yield break;
        float startTime = Time.time;
        float safeDuration = Mathf.Max(0.05f, duration);
        while (Time.time - startTime < safeDuration)
        {
            float t = Mathf.Clamp01((Time.time - startTime) / safeDuration);
            // SmoothStep keeps the hook feeling like acceleration/deceleration rather than a warp.
            float eased = t * t * (3f - 2f * t);
            for (int i = 0; i < entries.Count; i++)
            {
                PullEntry e = entries[i];
                if (e.target == null || e.target.IsNullInactiveDeadOrKnockedOut()) continue;
                Vector3 p = Vector3.Lerp(e.start, e.destination, eased);
                p.y = e.target.transform.position.y;
                e.target.transform.position = p;
            }
            yield return null;
        }

        for (int i = 0; i < entries.Count; i++)
        {
            PullEntry e = entries[i];
            if (e.target == null || e.target.IsNullInactiveDeadOrKnockedOut()) continue;
            Vector3 p = e.destination;
            p.y = e.target.transform.position.y;
            e.target.transform.position = p;
        }
    }

    public sealed class PullEntry
    {
        public Entity target;
        public Vector3 start;
        public Vector3 destination;
    }

    private static object FindControl(Entity target)
    {
        Type t = target.GetType();
        PropertyInfo p = t.GetProperty("Control", AnyInstance) ?? t.GetProperty("control", AnyInstance);
        if (p != null && p.GetIndexParameters().Length == 0)
        {
            object value = p.GetValue(target, null);
            if (value != null) return value;
        }
        FieldInfo f = t.GetField("Control", AnyInstance) ?? t.GetField("control", AnyInstance) ?? t.GetField("<Control>k__BackingField", AnyInstance);
        if (f != null)
        {
            object value = f.GetValue(target);
            if (value != null) return value;
        }
        Component[] components = target.GetComponents<Component>();
        for (int i = 0; i < components.Length; i++)
        {
            Component c = components[i];
            if (c != null && string.Equals(c.GetType().Name, "EntityControl", StringComparison.Ordinal)) return c;
        }
        return null;
    }

    private static Type FindRuntimeType(string shortName)
    {
        Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
        for (int i = 0; i < assemblies.Length; i++)
        {
            Type direct = assemblies[i].GetType(shortName, false);
            if (direct != null) return direct;
            try
            {
                Type[] types = assemblies[i].GetTypes();
                for (int j = 0; j < types.Length; j++)
                    if (types[j] != null && string.Equals(types[j].Name, shortName, StringComparison.Ordinal)) return types[j];
            }
            catch (ReflectionTypeLoadException rtle)
            {
                Type[] types = rtle.Types;
                if (types == null) continue;
                for (int j = 0; j < types.Length; j++)
                    if (types[j] != null && string.Equals(types[j].Name, shortName, StringComparison.Ordinal)) return types[j];
            }
            catch { }
        }
        return null;
    }

    private static object CreateDisplacement(Type type, Entity target, Vector3 destination, float duration)
    {
        object value = null;
        try { value = Activator.CreateInstance(type, true); }
        catch
        {
            if (type.IsValueType)
            {
                try { value = Activator.CreateInstance(type); } catch { }
            }
        }
        if (value == null) return null;

        float distance = Vector3.Distance(target.transform.position, destination);
        Type cursor = type;
        while (cursor != null && cursor != typeof(object))
        {
            FieldInfo[] fields = cursor.GetFields(AnyInstance | BindingFlags.DeclaredOnly);
            for (int i = 0; i < fields.Length; i++) TryAssign(fields[i], value, target, destination, duration, distance);
            PropertyInfo[] props = cursor.GetProperties(AnyInstance | BindingFlags.DeclaredOnly);
            for (int i = 0; i < props.Length; i++) TryAssign(props[i], value, target, destination, duration, distance);
            cursor = cursor.BaseType;
        }
        return value;
    }

    private static void TryAssign(FieldInfo field, object owner, Entity target, Vector3 destination, float duration, float distance)
    {
        if (field == null || field.IsStatic || field.IsInitOnly) return;
        object v;
        if (!TryInferValue(field.FieldType, field.Name, target, null, destination, duration, distance, out v)) return;
        try { field.SetValue(owner, v); } catch { }
    }

    private static void TryAssign(PropertyInfo prop, object owner, Entity target, Vector3 destination, float duration, float distance)
    {
        if (prop == null || !prop.CanWrite || prop.GetIndexParameters().Length != 0) return;
        object v;
        if (!TryInferValue(prop.PropertyType, prop.Name, target, null, destination, duration, distance, out v)) return;
        try { prop.SetValue(owner, v, null); } catch { }
    }

    private static bool TryBuildArgs(ParameterInfo[] ps, object displacement, Entity target, object control, Vector3 destination, float duration, out object[] args)
    {
        args = new object[ps.Length];
        float distance = Vector3.Distance(target.transform.position, destination);
        bool usedDisplacement = false;
        for (int i = 0; i < ps.Length; i++)
        {
            Type pt = ps[i].ParameterType;
            if (pt.IsByRef) pt = pt.GetElementType();
            if (pt != null && pt.IsInstanceOfType(displacement))
            {
                args[i] = displacement;
                usedDisplacement = true;
                continue;
            }
            object inferred;
            if (TryInferValue(pt, ps[i].Name, target, control, destination, duration, distance, out inferred))
            {
                args[i] = inferred;
                continue;
            }
            if (ps[i].HasDefaultValue)
            {
                args[i] = ps[i].DefaultValue;
                continue;
            }
            return false;
        }
        return usedDisplacement;
    }

    private static bool TryInferValue(Type type, string name, Entity target, object control, Vector3 destination, float duration, float distance, out object value)
    {
        value = null;
        if (type == null) return false;
        string n = (name ?? string.Empty).ToLowerInvariant();
        if (type == typeof(Vector3)) { value = destination; return true; }
        if (type == typeof(float))
        {
            if (n.Contains("speed")) value = distance / Mathf.Max(0.05f, duration);
            else if (n.Contains("goal") && n.Contains("distance")) value = 0.05f;
            else value = duration;
            return true;
        }
        if (type == typeof(double)) { value = (double)duration; return true; }
        if (type == typeof(bool))
        {
            value = n.Contains("force") || n.Contains("move") || n.Contains("disableagent");
            return true;
        }
        if (type == typeof(AnimationCurve)) { value = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f); return true; }
        if (typeof(Entity).IsAssignableFrom(type)) { value = target; return true; }
        if (control != null && type.IsInstanceOfType(control)) { value = control; return true; }
        if (type.IsEnum) { value = Activator.CreateInstance(type); return true; }
        if (!type.IsValueType) { value = null; return true; }
        try { value = Activator.CreateInstance(type); return true; } catch { return false; }
    }

    private static void LogContractOnce(string message)
    {
        if (_loggedContract) return;
        _loggedContract = true;
        DariusLog.Warn("E-DISPLACE", message + "; using smooth server fallback for this build.");
    }
}
