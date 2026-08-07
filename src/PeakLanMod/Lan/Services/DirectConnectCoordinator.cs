using ExitGames.Client.Photon;
using PeakLanMod.Lan.Model;
using Photon.Pun;
using Photon.Realtime;
using System;
using UnityEngine;
using Zorro.Core;

namespace PeakLanMod.Lan.Services;

internal sealed class DirectConnectCoordinator : IDirectConnectCoordinator
{
    private readonly ILanPluginOptions _options;
    private readonly ILanServerRuntimeService _LanServerRuntime;
    private readonly ILanIdentityAndValidation _identityAndValidation;
    private bool _pendingDirectHostStart;
    private bool _pendingDirectHostConnectRequested;
    private bool _queuedHostPreflightCompleted;
    private bool _pendingDirectJoinStart;
    private bool _pendingDirectJoinConnectRequested;
    private string _pendingDirectJoinRoomName = string.Empty;
    private string _pendingDirectJoinSource = string.Empty;
    private LanServerEndpoint? _pendingDirectJoinEndpoint;
    private float _lastNotReadyLogAt = -999f;
    private float _lastReconnectAttemptAt = -999f;

    internal DirectConnectCoordinator(
        ILanPluginOptions options,
        ILanServerRuntimeService LanServerRuntime,
        ILanIdentityAndValidation identityAndValidation)
    {
        _options = options;
        _LanServerRuntime = LanServerRuntime;
        _identityAndValidation = identityAndValidation;
    }

    public void RequestDirectHostStart(
        string source)
    {
        ClearPendingDirectJoinState(
            clearEndpointOverride: true,
            source: source,
            reason: "host intent started");

        if (!_options.AutoRetryDirectHostUntilReady.Value)
        {
            _ = StartDirectHostOnce();
            return;
        }

        QueueDirectHostStart();
        TryProcessQueuedDirectHostStart(source);
    }

    public void TryProcessQueuedDirectHostStart(
        string source)
    {
        if (!_pendingDirectHostStart)
        {
            return;
        }

        if (PhotonNetwork.InRoom)
        {
            _pendingDirectHostStart = false;
            _queuedHostPreflightCompleted = false;
            _LanServerRuntime.ResetQueuedHostReadinessWindow();

            Plugin.Log.LogInfo(
                $"{source}: queued host request cleared because client is already in a room.");

            return;
        }

        if (!StartDirectHostOnce())
        {
            return;
        }

        _pendingDirectHostStart = false;
        _pendingDirectHostConnectRequested = false;
        _queuedHostPreflightCompleted = false;
        _LanServerRuntime.ResetQueuedHostReadinessWindow();

        Plugin.Log.LogInfo(
            $"{source}: queued direct host request completed.");
    }

    public void StartDirectJoin()
    {
        if (!TryGetNormalizedConfiguredRoomName(out string roomName))
        {
            return;
        }

        RequestDirectJoinStart(
            roomName,
            "StartDirectJoin",
            _LanServerRuntime.GetConfiguredLanServerEndpoint());
    }

    public void RequestDirectJoinStart(
        string roomName,
        string source,
        LanServerEndpoint endpoint)
    {
        _pendingDirectJoinStart = true;
        _pendingDirectJoinConnectRequested = false;
        _pendingDirectJoinRoomName = roomName;
        _pendingDirectJoinSource = source;
        _pendingDirectJoinEndpoint = endpoint;

        _LanServerRuntime.ApplyTransientJoinEndpointOverride(
            endpoint,
            source);

        Plugin.Log.LogInfo(
            $"{source}: queued direct join request. " +
            $"Room={roomName}; " +
            $"Endpoint={_identityAndValidation.SanitizeEndpointForLog(endpoint.Address)}:{endpoint.Port}; " +
            $"Protocol={endpoint.Protocol}");

        TryProcessQueuedDirectJoinStart(source);
    }

    public void TryProcessQueuedDirectJoinStart(
        string source)
    {
        if (!_pendingDirectJoinStart)
        {
            return;
        }

        if (_pendingDirectJoinEndpoint is null)
        {
            ClearPendingDirectJoinState(
                clearEndpointOverride: true,
                source: source,
                reason: "runtime join target missing");
            return;
        }

        if (PhotonNetwork.InRoom)
        {
            ClearPendingDirectJoinState(
                clearEndpointOverride: true,
                source: source,
                reason: "already in room");
            return;
        }

        if (!StartDirectJoinOnce(
                _pendingDirectJoinRoomName,
                _pendingDirectJoinSource,
                _pendingDirectJoinEndpoint.Value))
        {
            return;
        }

        ClearPendingDirectJoinState(
            clearEndpointOverride: true,
            source: source,
            reason: "queued direct join request completed");
    }

    private void QueueDirectHostStart()
    {
        _pendingDirectHostStart = true;
        _pendingDirectHostConnectRequested = false;
        _queuedHostPreflightCompleted = false;
        _LanServerRuntime.ResetQueuedHostReadinessWindow();

        Plugin.Log.LogInfo(
            "Queued direct host start request. " +
            "Waiting for local server process and Photon ready state.");
    }

    private void ClearPendingDirectJoinState(
        bool clearEndpointOverride,
        string source,
        string reason)
    {
        bool hadPendingJoin =
            _pendingDirectJoinStart
            || _pendingDirectJoinEndpoint is not null;

        _pendingDirectJoinStart = false;
        _pendingDirectJoinConnectRequested = false;
        _pendingDirectJoinRoomName = string.Empty;
        _pendingDirectJoinSource = string.Empty;
        _pendingDirectJoinEndpoint = null;

        if (clearEndpointOverride)
        {
            _LanServerRuntime.ClearTransientJoinEndpointOverride(source);
        }

        if (!hadPendingJoin)
        {
            return;
        }

        Plugin.Log.LogInfo(
            $"{source}: cleared queued direct join request ({reason}).");
    }

    private bool StartDirectHostOnce()
    {
        bool queuedHostFlow =
            _pendingDirectHostStart
            && _options.AutoRetryDirectHostUntilReady.Value;

        if (!queuedHostFlow || !_queuedHostPreflightCompleted)
        {
            _LanServerRuntime.ApplyHostLanIpv4Selection();
            _LanServerRuntime.ApplyHostLuxonConfigAutomation();

            if (!_LanServerRuntime.EnsureHostLanServerProcess())
            {
                _pendingDirectHostStart = false;
                _queuedHostPreflightCompleted = false;
                _LanServerRuntime.ResetQueuedHostReadinessWindow();
                return false;
            }

            if (!EnsureLanServerReadinessBeforeConnect(
                    source: "StartDirectHost",
                    queuedHostFlow))
            {
                _pendingDirectHostConnectRequested = false;
                return false;
            }

            if (queuedHostFlow)
            {
                _queuedHostPreflightCompleted = true;

                Plugin.Log.LogInfo(
                    "StartDirectHost: queued host preflight completed. " +
                    "Waiting for Photon connected+ready before entering room flow.");
            }
        }

        EnsureOnlineModeForDirectConnect("StartDirectHost");

        if (!CanStartDirectConnection(ref _pendingDirectHostConnectRequested))
        {
            return false;
        }

        if (!TryGetValidatedConfiguredHostRoomName(out string roomName))
        {
            return false;
        }

        var connectionService =
            GameHandler.GetService<ConnectionService>();

        HostState hostState =
            connectionService.StateMachine
                .SwitchState<HostState>();

        hostState.RoomName = roomName;

        Plugin.Log.LogInfo(
            $"Starting direct host: " +
            $"room={roomName}; " +
            $"region={PhotonNetwork.CloudRegion}");

        LoadAirport();
        return true;
    }

    private bool StartDirectJoinOnce(
        string roomName,
        string source,
        LanServerEndpoint endpoint)
    {
        if (!EnsureLanServerReadinessBeforeConnect(
                source,
                queuedHostFlow: false,
                endpointOverride: endpoint))
        {
            return false;
        }

        EnsureOnlineModeForDirectConnect(source);

        if (!CanStartDirectConnection(ref _pendingDirectJoinConnectRequested))
        {
            return false;
        }

        const string region = "";

        var connectionService =
            GameHandler.GetService<ConnectionService>();

        JoinSpecificRoomState joinState =
            connectionService.StateMachine
                .SwitchState<JoinSpecificRoomState>();

        joinState.RoomName = roomName;
        joinState.RegionToJoin = region;

        Plugin.Log.LogInfo(
            $"Starting direct join: " +
            $"room={roomName}; " +
            $"region={region}; " +
            $"currentRegion={PhotonNetwork.CloudRegion}");

        LoadAirport();
        return true;
    }

    private bool EnsureLanServerReadinessBeforeConnect(
        string source,
        bool queuedHostFlow,
        LanServerEndpoint? endpointOverride = null)
    {
        bool ready = _LanServerRuntime.EnsureLanServerReadinessBeforeConnect(
            source,
            queuedHostFlow,
            endpointOverride);

        if (!ready
            && queuedHostFlow
            && _LanServerRuntime.WasLastQueuedHostReadinessTimeout)
        {
            _pendingDirectHostStart = false;
        }

        return ready;
    }

    private bool CanStartDirectConnection(
        ref bool connectRequested)
    {
        if (!PhotonNetwork.IsConnectedAndReady)
        {
            ClientState currentState = PhotonNetwork.NetworkClientState;
            float now = Time.realtimeSinceStartup;
            bool shouldLogNotReady = now - _lastNotReadyLogAt >= 2f;

            if (shouldLogNotReady)
            {
                Plugin.Log.LogWarning(
                    "Photon is not connected and ready. " +
                    $"Current state: {currentState}");

                _lastNotReadyLogAt = now;
            }

            if (!PhotonNetwork.IsConnected
                && currentState == ClientState.Disconnected)
            {
                if (!connectRequested)
                {
                    if (now - _lastReconnectAttemptAt < 1.5f)
                    {
                        return false;
                    }

                    Plugin.Log.LogInfo(
                        "Attempting Photon reconnect via NetworkingUtilities.ConnectToNetwork(). " +
                        "Press the host/join key again unless queued host auto-retry is enabled.");

                    Peak.Network.NetworkingUtilities.ConnectToNetwork();
                    connectRequested = true;
                    _lastReconnectAttemptAt = now;
                }
            }
            else
            {
                connectRequested = false;
            }

            return false;
        }

        connectRequested = false;

        if (PhotonNetwork.InRoom)
        {
            Plugin.Log.LogError(
                "Already in a Photon room.");

            return false;
        }

        return true;
    }

    private static void EnsureOnlineModeForDirectConnect(
        string source)
    {
        if (!PhotonNetwork.OfflineMode)
        {
            return;
        }

        Plugin.Log.LogWarning(
            $"{source}: OfflineMode was true before direct connect. " +
            "Forcing OfflineMode=false.");

        PhotonNetwork.OfflineMode = false;

        Plugin.Log.LogInfo(
            $"{source}: OfflineMode after force={PhotonNetwork.OfflineMode}.");
    }

    private bool TryGetValidatedConfiguredHostRoomName(
        out string roomName)
    {
        if (_identityAndValidation.TryGetValidatedHostRoomName(
                _options.RoomName.Value,
                out roomName,
                out string failureReason))
        {
            return true;
        }

        Plugin.Log.LogError(
            "Direct host requires a valid room name. " +
            $"Reason={failureReason}");

        return false;
    }

    private bool TryGetNormalizedConfiguredRoomName(
        out string roomName)
    {
        if (_identityAndValidation.TryNormalizeRoomName(
                _options.RoomName.Value,
                out roomName,
                out string failureReason))
        {
            return true;
        }

        Plugin.Log.LogError(
            "Direct connect requires a non-empty room name. " +
            $"Reason={failureReason}");

        return false;
    }

    private static void LoadAirport()
    {
        LoadingScreenHandler loadingScreen =
            RetrievableResourceSingleton<
                LoadingScreenHandler>.Instance;

        loadingScreen.Load(
            LoadingScreen.LoadingScreenType.Basic,
            null,
            loadingScreen.LoadSceneProcess(
                "Airport",
                networked: false,
                yieldForCharacterSpawn: true));
    }
}
