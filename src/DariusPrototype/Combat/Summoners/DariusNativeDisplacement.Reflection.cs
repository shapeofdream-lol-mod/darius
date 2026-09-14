public static partial class DariusNativeDisplacement
{
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