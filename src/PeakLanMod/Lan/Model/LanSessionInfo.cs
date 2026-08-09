using System;

namespace PeakLanMod.Lan.Model;

internal sealed class LanSessionInfo
{
    internal LanSessionInfo(
        string key,
        string roomName,
        string hostDisplayName,
        string sourceAddress,
        int sourcePort,
        string nameServerAddress,
        int nameServerPort,
        string transport,
        string scene,
        string serverInstanceId,
        string protocolVersion,
        string gameVersion,
        string modVersion,
        int schemaVersion,
        int currentPlayers,
        int maxPlayers,
        DateTime sentAtUtc,
        DateTime firstSeenUtc,
        DateTime lastSeenUtc,
        DateTime expiresAtUtc,
        bool isCompatible,
        string incompatibilityReason)
    {
        Key = key;
        RoomName = roomName;
        HostDisplayName = hostDisplayName;
        SourceAddress = sourceAddress;
        SourcePort = sourcePort;
        NameServerAddress = nameServerAddress;
        NameServerPort = nameServerPort;
        Transport = transport;
        Scene = scene;
        ServerInstanceId = serverInstanceId;
        ProtocolVersion = protocolVersion;
        GameVersion = gameVersion;
        ModVersion = modVersion;
        SchemaVersion = schemaVersion;
        CurrentPlayers = currentPlayers;
        MaxPlayers = maxPlayers;
        SentAtUtc = sentAtUtc;
        FirstSeenUtc = firstSeenUtc;
        LastSeenUtc = lastSeenUtc;
        ExpiresAtUtc = expiresAtUtc;
        IsCompatible = isCompatible;
        IncompatibilityReason = incompatibilityReason;
    }

    internal string Key { get; }
    internal string RoomName { get; }
    internal string HostDisplayName { get; }
    internal string SourceAddress { get; }
    internal int SourcePort { get; }
    internal string NameServerAddress { get; }
    internal int NameServerPort { get; }
    internal string Transport { get; }
    internal string Scene { get; }
    internal string ServerInstanceId { get; }
    internal string ProtocolVersion { get; }
    internal string GameVersion { get; }
    internal string ModVersion { get; }
    internal int SchemaVersion { get; }
    internal int CurrentPlayers { get; }
    internal int MaxPlayers { get; }
    internal DateTime SentAtUtc { get; }
    internal DateTime FirstSeenUtc { get; }
    internal DateTime LastSeenUtc { get; }
    internal DateTime ExpiresAtUtc { get; }
    internal bool IsCompatible { get; }
    internal string IncompatibilityReason { get; }
}
