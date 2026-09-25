using System;
using System.Reflection;
using Mirror;

// Sole compatibility boundary for Mirror internals required by runtime-created network templates.
// Custom spawn handlers use Mirror's public API; only template identity/cache setup lives here.
internal static class DariusRuntimeNetworkBridge
{
    internal static void ConfigureTemplateIdentity(NetworkIdentity identity, uint assetId, string label)
    {
        if (identity == null) return;

        try
        {
            FieldInfo field = typeof(NetworkIdentity).GetField("_assetId", BindingFlags.Instance | BindingFlags.NonPublic)
                           ?? typeof(NetworkIdentity).GetField("assetId", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                           ?? typeof(NetworkIdentity).GetField("<assetId>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
            if (field != null)
            {
                field.SetValue(identity, assetId);
            }
            else
            {
                PropertyInfo property = typeof(NetworkIdentity).GetProperty(
                    "assetId",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (property != null && property.CanWrite) property.SetValue(identity, assetId, null);
                else throw new MissingMemberException(typeof(NetworkIdentity).FullName, "assetId");
            }

            identity.sceneId = 0UL;

            FieldInfo sceneField = typeof(NetworkIdentity).GetField(
                "_isSceneObject",
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (sceneField != null) sceneField.SetValue(identity, false);

            // Runtime-created template objects have already executed NetworkIdentity.Awake.
            // Keep only the template's pre-spawn flag normalized; live clones are never rewritten.
            FieldInfo spawnedField = typeof(NetworkIdentity).GetField(
                "hasSpawned",
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (spawnedField != null) spawnedField.SetValue(identity, false);
        }
        catch (Exception e)
        {
            DariusLog.Exception("NET-BRIDGE", e, "Could not configure runtime template identity label=" + label + " assetId=" + assetId);
            throw;
        }
    }

    internal static void RebuildNetworkBehaviours(NetworkIdentity identity, string label)
    {
        if (identity == null) return;

        try
        {
            MethodInfo method = typeof(NetworkIdentity).GetMethod(
                "InitializeNetworkBehaviours",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (method == null)
                throw new MissingMethodException(typeof(NetworkIdentity).FullName, "InitializeNetworkBehaviours");

            method.Invoke(identity, null);
        }
        catch (Exception e)
        {
            DariusLog.Exception("NET-BRIDGE", e, "Could not rebuild NetworkBehaviour cache label=" + label);
            throw;
        }
    }
}
