using System;
using System.Collections;
using System.Reflection;
using Mirror;

// Read-only health probe for Mirror's runtime spawn-handler tables.
// Registration ownership remains in Traveler/Formal; this helper never repairs or re-registers.
internal static class DariusMirrorRuntimeHealth
{
    private static readonly FieldInfo SpawnHandlersField =
        typeof(NetworkClient).GetField("spawnHandlers", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

    private static readonly FieldInfo UnspawnHandlersField =
        typeof(NetworkClient).GetField("unspawnHandlers", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

    internal static bool IsHandlerPairHealthy(uint assetId)
    {
        bool spawnKnown;
        bool unspawnKnown;
        bool spawn = ContainsAssetId(SpawnHandlersField, assetId, out spawnKnown);
        bool unspawn = ContainsAssetId(UnspawnHandlersField, assetId, out unspawnKnown);

        // Mirror versions differ in whether these tables are inspectable. Unknown must not turn a
        // supported public RegisterSpawnHandler call into a false failure. When a table is visible,
        // however, a missing key proves that our local "registered" bookkeeping is stale.
        if (spawnKnown && !spawn) return false;
        if (unspawnKnown && !unspawn) return false;
        return true;
    }

    internal static string Describe(uint assetId)
    {
        bool spawnKnown;
        bool unspawnKnown;
        bool spawn = ContainsAssetId(SpawnHandlersField, assetId, out spawnKnown);
        bool unspawn = ContainsAssetId(UnspawnHandlersField, assetId, out unspawnKnown);
        return "assetId=" + assetId +
               " spawn=" + (spawnKnown ? spawn.ToString() : "<uninspectable>") +
               " unspawn=" + (unspawnKnown ? unspawn.ToString() : "<uninspectable>");
    }

    private static bool ContainsAssetId(FieldInfo field, uint assetId, out bool known)
    {
        known = false;
        if (field == null) return true;

        try
        {
            IDictionary table = field.GetValue(null) as IDictionary;
            if (table == null) return true;
            known = true;
            return table.Contains(assetId);
        }
        catch
        {
            return true;
        }
    }
}
