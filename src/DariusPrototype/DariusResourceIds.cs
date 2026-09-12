// Stable resource GUIDs shared by the formal registry (DariusFormalRegistry) and the native
// Deja Vu injection path (DariusDejaVuRegistry).
//
// These values are persisted in saves and used for resource/network identity: never change an
// existing value, and never define a second copy elsewhere. A GUID mismatch silently breaks
// loadout/constellation resolution and multiplayer identity.
public static class DariusResourceIds
{
    public const string Q = "b67dbf56-5817-424d-8c46-16129def0bf2";
    public const string W = "0a1641d1-1cbe-4a99-9d95-7b315b51420c";
    public const string E = "a016b459-fe0f-4571-a28c-d3c49a29d14f";
    public const string R = "fbb73980-8b6f-4623-88b0-946aaa201e73";

    public const string Flash = "f6100a54-c4b9-4138-9dc4-44bcf9048a71";
    public const string Ghost = "02b48d18-3f96-48e0-8296-e02e2395888e";

    public const string Identity = "acdd533c-6e5d-4974-9895-e90a103b5307";
    public const string LegacyGem = "44e808d0-ad21-458d-8832-de7eabe3c5ac"; // legacy v0.11 Essence only

    public const string AbilityQ = "6ba4cdd8-7e47-46d1-8f18-76f25e375aa2";
    public const string AbilityW = "8bfb1788-d4e0-4765-801e-af08c028791c";
    public const string AbilityE = "c006572b-f1bb-41c3-bdfa-90a51f402485";
    public const string AbilityR = "6a3da3e5-c434-4789-ae28-547c45dd7912";
}
