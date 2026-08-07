using System;
using Photon.Pun;
using Photon.Realtime;
using PeakLanMod.Lan.Model;
using PeakLanMod.Lan.State;

namespace PeakLanMod.Lan.Services;

internal sealed class LanErrorStateService : ILanErrorStateService
{
    private readonly ILanPluginOptions _options;
    private readonly LanConnectionStateStore _connectionStateStore;
    private ClientState? _previousState;

    internal LanErrorStateService(
        ILanPluginOptions options,
        LanConnectionStateStore connectionStateStore)
    {
        _options = options;
        _connectionStateStore = connectionStateStore;
    }

    public void LogPhotonStateChanges()
    {
        ClientState currentState =
            PhotonNetwork.NetworkClientState;

        if (_previousState == currentState)
        {
            return;
        }

        Plugin.Log.LogInfo(
            $"Photon state: " +
            $"{_previousState?.ToString() ?? "<initial>"} " +
            $"-> {currentState}; " +
            $"connected={PhotonNetwork.IsConnected}; " +
            $"ready={PhotonNetwork.IsConnectedAndReady}; " +
            $"region={PhotonNetwork.CloudRegion}; " +
            $"inLobby={PhotonNetwork.InLobby}; " +
            $"inRoom={PhotonNetwork.InRoom}; " +
            $"room={PhotonNetwork.CurrentRoom?.Name ?? "<none>"}; " +
            $"players={PhotonNetwork.CurrentRoom?.PlayerCount ?? 0}");

        _connectionStateStore.SetConnectionPhase(currentState.ToString());

        _previousState = currentState;
    }

    public void NotifyLocalServerDetected()
    {
        if (!LanRuntimeContext.IsLocalServerMode)
        {
            return;
        }

        ClearStructuredLanError(
            source: "NotifyLocalServerDetected",
            reason: "local server detected");

        Plugin.Log.LogInfo(
            $"Local server detected at {LanRuntimeContext.GetEffectiveLocalEndpointForLogging()}.");
    }

    public void NotifyLocalServerNotDetected(
        string reason)
    {
        if (!LanRuntimeContext.IsLocalServerMode)
        {
            return;
        }

        Plugin.Log.LogInfo(
            $"Local server not detected at {LanRuntimeContext.GetEffectiveLocalEndpointForLogging()}: {reason}");
    }

    public void ReportStructuredLanError(
        LanErrorCode code,
        string source,
        string message,
        string context)
    {
        if (!LanRuntimeContext.IsLocalServerMode
            || !_options.EnableStructuredErrorMapping.Value
            || code == LanErrorCode.None)
        {
            return;
        }

        string phase = PhotonNetwork.NetworkClientState.ToString();

        var detail = new LanErrorDetail(
            code,
            source,
            phase,
            message,
            context,
            DateTime.UtcNow);

        _connectionStateStore.SetConnectionError(detail);

        Plugin.Log.LogWarning(
            "LAN structured error classified. " +
            $"Code={detail.Code}; " +
            $"Source={detail.Source}; " +
            $"Phase={detail.Phase}; " +
            $"Message={detail.Message}; " +
            $"Context={detail.Context}");
    }

    public void ClearStructuredLanError(
        string source,
        string reason)
    {
        if (!LanRuntimeContext.IsLocalServerMode
            || !_options.EnableStructuredErrorMapping.Value)
        {
            return;
        }

        if (!_connectionStateStore.ClearConnectionError())
        {
            return;
        }

        Plugin.Log.LogInfo(
            "LAN structured error cleared. " +
            $"Source={source}; " +
            $"Reason={reason}");
    }

    public void HandleLeftRoom()
    {
        if (!LanRuntimeContext.IsLocalServerMode)
        {
            return;
        }

        if (!_options.AutoStopOwnedLocalServerOnLeaveRoom.Value)
        {
            return;
        }

        LanRuntimeContext.Services.LocalServerRuntime.StopOwnedLocalServerProcessOnExit(
            "PhotonCallbackProbe.OnLeftRoom");
    }

    public LanErrorDetail? GetConnectionErrorSnapshot()
    {
        return _connectionStateStore.GetConnectionErrorSnapshot();
    }
}
