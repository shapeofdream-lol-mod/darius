using System.Collections;
using System.Reflection;

// Single compatibility boundary for DewResourceDatabase's undocumented secondary indexes.
// Public Dew resource/profile/content APIs stay outside this class.
internal static class DariusRuntimeResourceDatabaseBridge
{
    internal static bool SetMap(object database, string fieldName, object key, object value)
    {
        if (database == null || key == null) return false;
        FieldInfo field = database.GetType().GetField(
            fieldName,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        IDictionary map = field != null ? field.GetValue(database) as IDictionary : null;
        if (map == null) return false;
        map[key] = value;
        return true;
    }

    internal static bool RemoveMapIfOwned(object database, string fieldName, object key, object expectedValue)
    {
        if (database == null || key == null) return false;
        FieldInfo field = database.GetType().GetField(
            fieldName,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        IDictionary map = field != null ? field.GetValue(database) as IDictionary : null;
        if (map == null || !map.Contains(key) || !Equals(map[key], expectedValue)) return false;
        map.Remove(key);
        return true;
    }
}
