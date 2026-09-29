using System;
using System.Linq;
using System.Reflection;

public static partial class DariusDejaVuRegistry
{
    private static string MethodLabel(MethodBase m)
    {
        try { return m.DeclaringType.FullName + "." + m.Name + "(" + string.Join(",", m.GetParameters().Select(p => p.ParameterType.Name).ToArray()) + ")"; }
        catch { return m != null ? m.Name : "<null>"; }
    }

    private static bool LooksLikeNameRequest(MethodBase method, string key)
    {
        string methodName = method != null ? method.Name : string.Empty;
        if (!string.IsNullOrEmpty(methodName) &&
            (methodName.IndexOf("Name", StringComparison.OrdinalIgnoreCase) >= 0 || methodName.IndexOf("Title", StringComparison.OrdinalIgnoreCase) >= 0))
            return true;
        if (string.IsNullOrEmpty(key)) return false;
        return key.EndsWith(".name", StringComparison.OrdinalIgnoreCase) ||
               key.EndsWith("_name", StringComparison.OrdinalIgnoreCase) ||
               key.IndexOf(".title", StringComparison.OrdinalIgnoreCase) >= 0 ||
               key.IndexOf("_title", StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
