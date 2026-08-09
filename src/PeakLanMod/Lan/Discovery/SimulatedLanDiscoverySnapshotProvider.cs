using System;
using PeakLanMod.Lan.Model;

namespace PeakLanMod.Lan.Discovery;

internal sealed class SimulatedLanDiscoverySnapshotProvider
{
    private static readonly string[] HostNames =
    [
        "CAMP-HOST",
        "PEAK-HOST",
        "RIDGE-HOST",
        "FOREST-HOST",
        "SUMMIT-HOST"
    ];

    private static readonly string[] SceneNames =
    [
        "Airport",
        "Cabin",
        "Mountain",
        "Forest"
    ];

    private const int DefaultCount = 8;
    private const int MaxCount = 32;

    internal LanSessionInfo[] BuildSnapshot(
        int requestedCount)
    {
        int count = requestedCount;

        if (count <= 0)
        {
            count = DefaultCount;
        }
        else if (count > MaxCount)
        {
            count = MaxCount;
        }

        DateTime now = DateTime.UtcNow;
        var sessions = new LanSessionInfo[count];

        for (int index = 0; index < count; index++)
        {
            int hostOctet = 20 + index;
            string serverInstanceId = $"sim-{index + 1:00}";
            string hostName = HostNames[index % HostNames.Length];
            string scene = SceneNames[index % SceneNames.Length];
            DateTime sentAt = now.AddMilliseconds(-150 * (index % 5));

            sessions[index] = new LanSessionInfo(
                key: $"simulated-{serverInstanceId}",
                roomName: $"lan-room-{index + 1:00}",
                hostDisplayName: hostName,
                sourceAddress: $"192.168.1.{hostOctet}",
                sourcePort: 47777,
                nameServerAddress: $"192.168.1.{hostOctet}",
                nameServerPort: 5058,
                transport: "Udp",
                scene: scene,
                serverInstanceId: serverInstanceId,
                protocolVersion: "simulated",
                gameVersion: "simulated",
                modVersion: Plugin.DisplayVersion,
                schemaVersion: 1,
                currentPlayers: -1,
                maxPlayers: -1,
                sentAtUtc: sentAt,
                firstSeenUtc: sentAt,
                lastSeenUtc: now,
                expiresAtUtc: now.AddSeconds(4),
                isCompatible: false,
                incompatibilityReason: "PreviewOnlyNotJoinable");
        }

        return sessions;
    }
}