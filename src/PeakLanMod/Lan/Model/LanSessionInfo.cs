using System;

namespace PeakLanMod.Lan.Model;

internal sealed class LanSessionInfo
{
    internal LanSessionInfo(
        string key,
        string roomName,
        string hostDisplayName,
        string nameServerAddress,
        int nameServerPort,
        string transport,
        string scene,
        string serverInstanceId,
        string protocolVersion,
        string gameVersion,
        string modVersion,
        int schemaVersion,
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
        NameServerAddress = nameServerAddress;
        NameServerPort = nameServerPort;
        Transport = transport;
        Scene = scene;
        ServerInstanceId = serverInstanceId;
        ProtocolVersion = protocolVersion;
        GameVersion = gameVersion;
        ModVersion = modVersion;
        SchemaVersion = schemaVersion;
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
    internal string NameServerAddress { get; }
    internal int NameServerPort { get; }
    internal string Transport { get; }
    internal string Scene { get; }
    internal string ServerInstanceId { get; }
    internal string ProtocolVersion { get; }
    internal string GameVersion { get; }
    internal string ModVersion { get; }
    internal int SchemaVersion { get; }
    internal DateTime SentAtUtc { get; }
    internal DateTime FirstSeenUtc { get; }
    internal DateTime LastSeenUtc { get; }
    internal DateTime ExpiresAtUtc { get; }
    internal bool IsCompatible { get; }
    internal string IncompatibilityReason { get; }
}
