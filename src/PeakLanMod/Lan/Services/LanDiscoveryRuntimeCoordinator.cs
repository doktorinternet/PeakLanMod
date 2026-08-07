using System;
using Photon.Pun;
using PeakLanMod.Lan.Discovery;
using PeakLanMod.Lan.Model;
using PeakLanMod.Lan.State;
using UnityEngine;

namespace PeakLanMod.Lan.Services;

internal sealed class LanDiscoveryRuntimeCoordinator : ILanDiscoveryRuntimeCoordinator
{
    private readonly ILanPluginOptions _options;
    private readonly LanConnectionStateStore _connectionStateStore;
    private readonly UdpLanDiscoveryListener _listener;
    private readonly UdpLanDiscoveryBroadcaster _broadcaster;
    private readonly string _pluginVersion;
    private readonly string _serverInstanceId;
    private int _lastSnapshotCount = -1;
    private bool? _lastListenerRunning;
    private bool? _lastBroadcasterRunning;

    internal LanDiscoveryRuntimeCoordinator(
        ILanPluginOptions options,
        LanConnectionStateStore connectionStateStore,
        string pluginVersion)
    {
        _options = options;
        _connectionStateStore = connectionStateStore;
        _listener = new UdpLanDiscoveryListener(connectionStateStore);
        _broadcaster = new UdpLanDiscoveryBroadcaster();
        _pluginVersion = pluginVersion;
        _serverInstanceId = Guid.NewGuid().ToString("N");
    }

    public void SyncLanDiscoveryRuntime(
        string source)
    {
        if (!LanRuntimeContext.IsLocalServerMode || !_options.LanDiscoveryEnabled.Value)
        {
            if (_broadcaster.IsRunning)
            {
                _broadcaster.Stop($"{source}: mode/config disabled");
            }

            if (_listener.IsRunning)
            {
                _listener.Stop($"{source}: mode/config disabled");
            }

            return;
        }

        if (!_listener.IsRunning)
        {
            if (!_listener.TryStart(
                    _options.LanDiscoveryUdpPort.Value,
                    _options.LanDiscoveryEntryTtlMs.Value,
                    EvaluateLanSessionCompatibility,
                    out string listenerMessage))
            {
                Plugin.Log.LogError(
                    $"{source}: failed to start LAN discovery listener. " +
                    $"Reason={listenerMessage}");
            }
            else
            {
                Plugin.Log.LogInfo(
                    $"{source}: LAN discovery listener active. " +
                    $"Port={_options.LanDiscoveryUdpPort.Value}; " +
                    $"TtlMs={_options.LanDiscoveryEntryTtlMs.Value}; " +
                    $"Message={listenerMessage}");
            }
        }

        int sessionCount = _listener.GetSnapshot().Length;
        bool listenerRunning = _listener.IsRunning;
        bool broadcasterRunning = _broadcaster.IsRunning;

        bool changed = sessionCount != _lastSnapshotCount
            || listenerRunning != _lastListenerRunning
            || broadcasterRunning != _lastBroadcasterRunning;

        if (changed)
        {
            _lastSnapshotCount = sessionCount;
            _lastListenerRunning = listenerRunning;
            _lastBroadcasterRunning = broadcasterRunning;

            Plugin.Log.LogInfo(
                $"{source}: LAN discovery snapshot count={sessionCount}; " +
                $"ListenerRunning={listenerRunning}; " +
                $"BroadcasterRunning={broadcasterRunning}");
        }
    }

    public void RefreshLanDiscoveryBroadcast(
        string source)
    {
        if (!LanRuntimeContext.IsLocalServerMode || !_options.LanDiscoveryEnabled.Value)
        {
            return;
        }

        if (!PhotonNetwork.InRoom || !PhotonNetwork.IsMasterClient)
        {
            if (_broadcaster.IsRunning)
            {
                _broadcaster.Stop($"{source}: not in master room");
            }

            return;
        }

        if (!_broadcaster.TryStart(
                _options.LanDiscoveryUdpPort.Value,
                _options.LanDiscoveryBroadcastIntervalMs.Value,
                BuildLanDiscoveryAnnouncement,
                out string startMessage))
        {
            Plugin.Log.LogError(
                $"{source}: failed to start LAN discovery broadcaster. " +
                $"Reason={startMessage}");

            return;
        }

        Plugin.Log.LogInfo(
            $"{source}: LAN discovery broadcaster active. " +
            $"Port={_options.LanDiscoveryUdpPort.Value}; " +
            $"IntervalMs={_options.LanDiscoveryBroadcastIntervalMs.Value}; " +
            $"Message={startMessage}; " +
            $"Room={PhotonNetwork.CurrentRoom?.Name ?? "<none>"}");
    }

    public void StopLanDiscoveryBroadcast(
        string source)
    {
        if (_broadcaster.IsRunning)
        {
            _broadcaster.Stop(source);
        }
    }

    public void ShutdownLanDiscoveryRuntime(
        string source)
    {
        StopLanDiscoveryBroadcast($"{source}: shutdown");

        if (_listener.IsRunning)
        {
            _listener.Stop($"{source}: shutdown");
        }
    }

    public LanSessionInfo[] GetDiscoverySnapshot()
    {
        return _listener.GetSnapshot();
    }

    public (string Phase, DateTime UpdatedAtUtc) GetConnectionPhaseSnapshot()
    {
        return _connectionStateStore.GetConnectionPhaseSnapshot();
    }

    private LanSessionCompatibility EvaluateLanSessionCompatibility(
        LanDiscoveryAnnouncement announcement)
    {
        string expectedProtocol =
            _options.LanDiscoveryProtocolVersion.Value.Trim();

        if (!string.Equals(
                announcement.ProtocolVersion,
                expectedProtocol,
                StringComparison.Ordinal))
        {
            return new LanSessionCompatibility(
                isCompatible: false,
                reason: "IncompatibleProtocolVersion");
        }

        if (!_options.LanDiscoveryRequireVersionMatch.Value)
        {
            return LanSessionCompatibility.Compatible;
        }

        string gameVersion = Application.version ?? string.Empty;

        if (!string.Equals(
                announcement.GameVersion,
                gameVersion,
                StringComparison.Ordinal))
        {
            return new LanSessionCompatibility(
                isCompatible: false,
                reason: "IncompatibleGameVersion");
        }

        if (!string.Equals(
                announcement.ModVersion,
                _pluginVersion,
                StringComparison.Ordinal))
        {
            return new LanSessionCompatibility(
                isCompatible: false,
                reason: "IncompatibleModVersion");
        }

        return LanSessionCompatibility.Compatible;
    }

    private LanDiscoveryAnnouncement BuildLanDiscoveryAnnouncement()
    {
        string roomName = PhotonNetwork.CurrentRoom?.Name
            ?? string.Empty;

        string scene = UnityEngine.SceneManagement.SceneManager
            .GetActiveScene()
            .name;

        return new LanDiscoveryAnnouncement(
            type: LanDiscoveryMessageCodec.AnnouncementType,
            schemaVersion: LanDiscoveryMessageCodec.SchemaVersionV1,
            protocolVersion: _options.LanDiscoveryProtocolVersion.Value.Trim(),
            gameVersion: Application.version ?? string.Empty,
            modVersion: _pluginVersion,
            roomName: roomName,
            hostDisplayName: PhotonNetwork.NickName ?? string.Empty,
            nameServerAddress: _options.LocalServerAddress.Value.Trim(),
            nameServerPort: _options.LocalServerPort.Value,
            transport: _options.LocalServerProtocol.Value.ToString(),
            scene: scene,
            serverInstanceId: _serverInstanceId,
            sentAtUtc: DateTime.UtcNow);
    }
}
