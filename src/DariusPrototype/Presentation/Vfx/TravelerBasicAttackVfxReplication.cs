using System;
using Mirror;
using UnityEngine;

// Visual-only multiplayer relay for Darius basic attacks.
// The local owner renders immediately; this message only mirrors the swing/impact to observers.
public struct TravelerBasicAttackVfxMessage : NetworkMessage
{
    public uint netId;
    public byte phase;
    public byte variant;
    public byte critical;
    public Vector3 position;
    public Vector3 direction;
}

public static class TravelerBasicAttackVfxReplication
{
    private static bool _initialized;

    public static void Initialize()
    {
        if (_initialized) return;
        try
        {
            // This assembly is built with dotnet rather than Unity's Mirror Weaver, so register the
            // one custom message serializer explicitly instead of relying on generated code.
            Writer<TravelerBasicAttackVfxMessage>.write = WriteMessage;
            Reader<TravelerBasicAttackVfxMessage>.read = ReadMessage;

            // ModBehaviour can be recreated across scenes. Replace keeps the handler current without
            // accumulating registrations, and also restores it after Mirror resets its handler table.
            NetworkClient.ReplaceHandler<TravelerBasicAttackVfxMessage>(OnClientMessage);
            NetworkServer.ReplaceHandler<TravelerBasicAttackVfxMessage>(OnServerMessage);
            _initialized = true;
        }
        catch (Exception e)
        {
            _initialized = false;
            try { NetworkClient.UnregisterHandler<TravelerBasicAttackVfxMessage>(); } catch { }
            try { NetworkServer.UnregisterHandler<TravelerBasicAttackVfxMessage>(); } catch { }
            // Optional presentation networking must never abort Hero_Darius registration.
            DariusLog.Exception("ATK-NET", e, "Basic-attack VFX replication initialization failed");
        }
    }

    public static void Shutdown()
    {
        if (!_initialized) return;
        try { NetworkClient.UnregisterHandler<TravelerBasicAttackVfxMessage>(); } catch { }
        try { NetworkServer.UnregisterHandler<TravelerBasicAttackVfxMessage>(); } catch { }
        Writer<TravelerBasicAttackVfxMessage>.write = null;
        Reader<TravelerBasicAttackVfxMessage>.read = null;
        _initialized = false;
    }

    public static void Broadcast(NetworkIdentity identity, byte phase, byte variant, bool critical, Vector3 position, Vector3 direction)
    {
        if (identity == null || identity.netId == 0 || phase > 1 || variant > 1) return;
        if (!IsFinite(position) || !IsFinite(direction)) return;

        TravelerBasicAttackVfxMessage message = new TravelerBasicAttackVfxMessage
        {
            netId = identity.netId,
            phase = phase,
            variant = variant,
            critical = (byte)(critical ? 1 : 0),
            position = position,
            direction = direction
        };

        try
        {
            // Client/host owners already rendered locally, so send through the server and let it
            // forward to ready observers except the owner. Dedicated-server execution relays directly.
            if (NetworkClient.active && NetworkClient.connection != null)
                NetworkClient.Send(message);
            else if (NetworkServer.active)
                RelayToObservers(identity, message);
        }
        catch (Exception e)
        {
            DariusLog.Exception("ATK-NET", e, "Basic-attack VFX broadcast failed");
        }
    }

    private static void OnServerMessage(NetworkConnectionToClient sender, TravelerBasicAttackVfxMessage message)
    {
        try
        {
            if (!IsValid(message)) return;

            NetworkIdentity identity;
            if (!NetworkServer.spawned.TryGetValue(message.netId, out identity) || identity == null) return;

            // Trust boundary: a client may only broadcast presentation for an object it owns.
            if (identity.connectionToClient != sender) return;

            RelayToObservers(identity, message);
        }
        catch (Exception e)
        {
            DariusLog.Exception("ATK-NET", e, "Basic-attack VFX server relay failed");
        }
    }

    private static void RelayToObservers(NetworkIdentity identity, TravelerBasicAttackVfxMessage message)
    {
        if (identity == null || !NetworkServer.active) return;
        NetworkServer.SendToReadyObservers(identity, message, false);
    }

    private static void OnClientMessage(TravelerBasicAttackVfxMessage message)
    {
        try
        {
            if (!IsValid(message)) return;

            NetworkIdentity identity;
            if (!NetworkClient.spawned.TryGetValue(message.netId, out identity) || identity == null) return;

            identity.gameObject.SendMessage(
                "OnReplicatedTravelerBasicAttackVfx",
                message,
                SendMessageOptions.DontRequireReceiver);
        }
        catch (Exception e)
        {
            DariusLog.Exception("ATK-NET", e, "Basic-attack VFX client dispatch failed");
        }
    }

    private static bool IsValid(TravelerBasicAttackVfxMessage message)
    {
        return message.netId != 0 &&
               message.phase <= 1 &&
               message.variant <= 1 &&
               message.critical <= 1 &&
               IsFinite(message.position) &&
               IsFinite(message.direction);
    }

    private static bool IsFinite(Vector3 value)
    {
        return !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
               !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
               !float.IsNaN(value.z) && !float.IsInfinity(value.z);
    }

    private static void WriteMessage(NetworkWriter writer, TravelerBasicAttackVfxMessage message)
    {
        writer.WriteUInt(message.netId);
        writer.WriteByte(message.phase);
        writer.WriteByte(message.variant);
        writer.WriteByte(message.critical);
        writer.WriteVector3(message.position);
        writer.WriteVector3(message.direction);
    }

    private static TravelerBasicAttackVfxMessage ReadMessage(NetworkReader reader)
    {
        return new TravelerBasicAttackVfxMessage
        {
            netId = reader.ReadUInt(),
            phase = reader.ReadByte(),
            variant = reader.ReadByte(),
            critical = reader.ReadByte(),
            position = reader.ReadVector3(),
            direction = reader.ReadVector3()
        };
    }
}
