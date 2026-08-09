using System;
using Photon.Pun;
using Photon.Realtime;
using PeakLanMod.Lan.Diagnostics;
using PeakLanMod.Lan.Model;
using PeakLanMod.Lan.Services;
using PhotonPlayer = Photon.Realtime.Player;
using UnityEngine.SceneManagement;

namespace PeakLanMod;

internal sealed class PhotonCallbackProbe :
    MonoBehaviourPunCallbacks
{
    private static string Time =>
        DateTime.Now.ToString("HH:mm:ss.fff");

    private static bool IsVerboseDiagnosticsEnabled =>
        LanRuntimeContext.Options.EnableVerboseDiagnostics.Value;

    public override void OnConnectedToMaster()
    {
        AuthenticationValues? auth = PhotonNetwork.AuthValues;
        string userId = auth?.UserId ?? string.Empty;

        if (IsVerboseDiagnosticsEnabled || !PhotonNetwork.OfflineMode)
        {
            Plugin.Log.LogInfo(
                $"[{Time}] CALLBACK OnConnectedToMaster: " +
                $"region={PhotonNetwork.CloudRegion}; " +
                $"offlineMode={PhotonNetwork.OfflineMode}; " +
                $"gameVersion={PhotonNetwork.GameVersion ?? "<null>"}; " +
                $"appVersion={PhotonNetwork.PhotonServerSettings.AppSettings.AppVersion ?? "<null>"}; " +
                $"authType={auth?.AuthType.ToString() ?? "<null>"}; " +
                $"userIdFingerprint={LanRuntimeContext.Fingerprint(userId)}; " +
                $"userIdLength={userId.Length}");
        }

        if (LanRuntimeContext.IsLanServerMode)
        {
            if (PhotonNetwork.OfflineMode)
            {
                LanRuntimeContext.Services.ErrorState.NotifyLanServerNotDetected(
                    "OfflineMode fallback active");
            }
            else
            {
                LanRuntimeContext.Services.ErrorState.NotifyLanServerDetected();
                LanRuntimeContext.Services.ErrorState.ClearStructuredLanError(
                    source: "OnConnectedToMaster",
                    reason: "connected to master");
            }
        }
    }

    public override void OnCreatedRoom()
    {
        AuthenticationValues? auth = PhotonNetwork.AuthValues;
        string userId = auth?.UserId ?? string.Empty;

        Plugin.Log.LogInfo(
            $"[{Time}] CALLBACK OnCreatedRoom: " +
            $"room={PhotonNetwork.CurrentRoom?.Name}; " +
            $"offlineMode={PhotonNetwork.OfflineMode}; " +
            $"gameVersion={PhotonNetwork.GameVersion ?? "<null>"}; " +
            $"appVersion={PhotonNetwork.PhotonServerSettings.AppSettings.AppVersion ?? "<null>"}; " +
            $"userIdFingerprint={LanRuntimeContext.Fingerprint(userId)}");

            LanRuntimeContext.Services.WorkflowPolicy.TryAutoLockWorkflowModeAfterSuccessfulHost(
                "PhotonCallbackProbe.OnCreatedRoom");

            LanRuntimeContext.Services.DirectConnect.CompletePendingAttempt(
                "PhotonCallbackProbe.OnCreatedRoom");

            LanRuntimeContext.Services.DiscoveryRuntime.RefreshLanDiscoveryBroadcast("OnCreatedRoom");
    }

    public override void OnJoinedRoom()
    {
        PhotonPlayer localPlayer = PhotonNetwork.LocalPlayer;
        AuthenticationValues? auth = PhotonNetwork.AuthValues;
        string userId = auth?.UserId ?? string.Empty;

        Plugin.Log.LogInfo(
            $"[{Time}] CALLBACK OnJoinedRoom: " +
            $"room={PhotonNetwork.CurrentRoom?.Name}; " +
            $"players={PhotonNetwork.CurrentRoom?.PlayerCount}; " +
            $"actor={localPlayer?.ActorNumber}; " +
            $"nickname={localPlayer?.NickName}; " +
            $"master={PhotonNetwork.IsMasterClient}; " +
            $"scene={UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}; " +
            $"offlineMode={PhotonNetwork.OfflineMode}; " +
            $"gameVersion={PhotonNetwork.GameVersion ?? "<null>"}; " +
            $"appVersion={PhotonNetwork.PhotonServerSettings.AppSettings.AppVersion ?? "<null>"}; " +
            $"authType={auth?.AuthType.ToString() ?? "<null>"}; " +
            $"userIdFingerprint={LanRuntimeContext.Fingerprint(userId)}; " +
            $"userIdLength={userId.Length}");

            LanRuntimeContext.Services.ErrorState.ClearStructuredLanError(
                source: "OnJoinedRoom",
                reason: "joined room");

            LanRuntimeContext.Services.DirectConnect.CompletePendingAttempt(
                "PhotonCallbackProbe.OnJoinedRoom");

            LanRuntimeContext.Services.DiscoveryRuntime.RefreshLanDiscoveryBroadcast("OnJoinedRoom");
    }

    public override void OnJoinRoomFailed(
        short returnCode,
        string message)
    {
        Plugin.Log.LogError(
            $"[{Time}] CALLBACK OnJoinRoomFailed: " +
            $"code={returnCode}; " +
            $"message={message}");

        LanErrorCode code = LanErrorClassifier.ClassifyJoinRoomFailure(
            returnCode,
            message);

        LanRuntimeContext.Services.ErrorState.ReportStructuredLanError(
            code,
            source: "OnJoinRoomFailed",
            message: message,
            context: $"returnCode={returnCode}");
    }

    public override void OnCreateRoomFailed(
        short returnCode,
        string message)
    {
        Plugin.Log.LogError(
            $"[{Time}] CALLBACK OnCreateRoomFailed: " +
            $"code={returnCode}; " +
            $"message={message}; " +
            $"state={PhotonNetwork.NetworkClientState}; " +
            $"scene={UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}; " +
            $"server={LanRuntimeContext.Options.LanServerAddress.Value}:{LanRuntimeContext.Options.LanServerPort.Value}; " +
            $"protocol={LanRuntimeContext.Options.LanServerProtocol.Value}");

        if (LanRuntimeContext.IsLanServerMode)
        {
            LanRuntimeContext.Services.ErrorState.NotifyLanServerNotDetected(
                $"create room failed {returnCode}");
        }

        LanErrorCode code = LanErrorClassifier.ClassifyCreateRoomFailure(
            returnCode,
            message);

        LanRuntimeContext.Services.ErrorState.ReportStructuredLanError(
            code,
            source: "OnCreateRoomFailed",
            message: message,
            context: $"returnCode={returnCode}");
    }

    public override void OnDisconnected(
        DisconnectCause cause)
    {
        var client = PhotonNetwork.NetworkingClient;
        var peer = client?.LoadBalancingPeer;
        string clientState = client?.State.ToString() ?? "<null>";
        string peerState = peer?.PeerState.ToString() ?? "<null>";
        string serverAddress = peer?.ServerAddress ?? "<null>";
        string protocol = peer?.TransportProtocol.ToString() ?? "<null>";
        bool isAttemptActive = LanRuntimeContext.Services.DirectConnect.IsDirectAttemptActive();
        bool shouldDeferDisconnectError = LanRuntimeContext.Services.DirectConnect.ShouldDeferDisconnectError(
            cause,
            out int deferredElapsedMs,
            out int deferredTimeoutMs);
        string sceneName = SceneManager.GetActiveScene().name;

        string disconnectLine =
            $"[{Time}] CALLBACK OnDisconnected: " +
            $"cause={cause}; " +
            $"clientState={clientState}; " +
            $"peerState={peerState}; " +
            $"serverAddress={serverAddress}; " +
            $"protocol={protocol}; " +
            $"state={PhotonNetwork.NetworkClientState}; " +
            $"scene={sceneName}; " +
            $"attemptActive={isAttemptActive}; " +
            $"deferError={shouldDeferDisconnectError}";

        if (shouldDeferDisconnectError)
        {
            if (IsVerboseDiagnosticsEnabled)
            {
                Plugin.Log.LogInfo(disconnectLine);
            }
        }
        else if (isAttemptActive)
        {
            Plugin.Log.LogError(disconnectLine);
        }
        else
        {
            Plugin.Log.LogWarning(disconnectLine);
        }

        LanRuntimeContext.Services.DirectConnect.CancelPendingAttemptOnDisconnect(
            cause,
            clientState,
            serverAddress);

        if (shouldDeferDisconnectError)
        {
            if (IsVerboseDiagnosticsEnabled)
            {
                Plugin.Log.LogInfo(
                    "Deferring disconnect error surfacing during host startup window. " +
                    $"Cause={cause}; " +
                    $"ElapsedMs={deferredElapsedMs}; " +
                    $"TimeoutMs={deferredTimeoutMs}; " +
                    $"Endpoint={serverAddress}");
            }

            return;
        }

        LanRuntimeContext.Services.DiscoveryRuntime.StopLanDiscoveryBroadcast("OnDisconnected");

        if (LanRuntimeContext.IsLanServerMode)
        {
            LanRuntimeContext.Services.ErrorState.NotifyLanServerNotDetected(
                $"disconnect cause {cause}");
        }

        bool isPassiveStartupDisconnect =
            !isAttemptActive
            && !PhotonNetwork.InRoom
            && string.Equals(sceneName, "Title", StringComparison.Ordinal);

        if (isPassiveStartupDisconnect)
        {
            LanRuntimeContext.Services.ErrorState.ClearStructuredLanError(
                source: "OnDisconnected",
                reason: $"passive startup disconnect {cause}");

            Plugin.Log.LogInfo(
                "Ignoring passive startup disconnect as non-attempt failure. " +
                $"Cause={cause}; Endpoint={serverAddress}; Protocol={protocol}");

            return;
        }

        LanErrorCode code = LanErrorClassifier.ClassifyDisconnect(
            cause,
            clientState);

        if (code == LanErrorCode.None)
        {
            LanRuntimeContext.Services.ErrorState.ClearStructuredLanError(
                source: "OnDisconnected",
                reason: $"non-actionable disconnect cause {cause}");
            return;
        }

        string detailMessage = BuildBestEffortDisconnectMessage(
            cause,
            clientState,
            serverAddress,
            protocol);

        LanRuntimeContext.Services.ErrorState.ReportStructuredLanError(
            code,
            source: "OnDisconnected",
            message: detailMessage,
            context: $"clientState={clientState}; peerState={peerState}; serverAddress={serverAddress}; protocol={protocol}");
    }

    public override void OnLeftRoom()
    {
        Plugin.Log.LogInfo(
            $"[{Time}] CALLBACK OnLeftRoom: " +
            $"scene={UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}; " +
            $"offlineMode={PhotonNetwork.OfflineMode}");

        LanRuntimeContext.Services.ErrorState.ClearStructuredLanError(
            source: "OnLeftRoom",
            reason: "left room");

        LanRuntimeContext.Services.ErrorState.HandleLeftRoom();
    }

    public override void OnPlayerEnteredRoom(
        PhotonPlayer newPlayer)
    {
        if (!IsVerboseDiagnosticsEnabled)
        {
            return;
        }

        Plugin.Log.LogInfo(
            $"[{Time}] CALLBACK OnPlayerEnteredRoom: " +
            $"actor={newPlayer.ActorNumber}; " +
            $"nickname={newPlayer.NickName}; " +
            $"userId={newPlayer.UserId}; " +
            $"players={PhotonNetwork.CurrentRoom?.PlayerCount}");
    }

    public override void OnPlayerLeftRoom(
        PhotonPlayer otherPlayer)
    {
        if (!IsVerboseDiagnosticsEnabled)
        {
            return;
        }

        Plugin.Log.LogWarning(
            $"[{Time}] CALLBACK OnPlayerLeftRoom: " +
            $"actor={otherPlayer.ActorNumber}; " +
            $"nickname={otherPlayer.NickName}; " +
            $"userId={otherPlayer.UserId}; " +
            $"players={PhotonNetwork.CurrentRoom?.PlayerCount}");
    }

    private static string BuildBestEffortDisconnectMessage(
        DisconnectCause cause,
        string clientState,
        string serverAddress,
        string protocol)
    {
        if (cause == DisconnectCause.Exception)
        {
            return "NameServer/Master connect failed (socket or protocol exception). " +
                $"Endpoint={serverAddress}; Protocol={protocol}; ClientState={clientState}";
        }

        if (cause == DisconnectCause.ServerTimeout
            || cause == DisconnectCause.ClientTimeout)
        {
            return "Network timeout while establishing Photon connection. " +
                $"Endpoint={serverAddress}; Protocol={protocol}; ClientState={clientState}";
        }

        return $"Photon disconnect cause={cause}; Endpoint={serverAddress}; Protocol={protocol}; ClientState={clientState}";
    }
}