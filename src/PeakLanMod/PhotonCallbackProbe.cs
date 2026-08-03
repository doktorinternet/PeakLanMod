using System;
using Photon.Pun;
using Photon.Realtime;
using PhotonPlayer = Photon.Realtime.Player;

namespace PeakLanMod;

internal sealed class PhotonCallbackProbe :
    MonoBehaviourPunCallbacks
{
    private static string Time =>
        DateTime.Now.ToString("HH:mm:ss.fff");

    public override void OnConnectedToMaster()
    {
        AuthenticationValues? auth = PhotonNetwork.AuthValues;
        string userId = auth?.UserId ?? string.Empty;

        Plugin.Log.LogInfo(
            $"[{Time}] CALLBACK OnConnectedToMaster: " +
            $"region={PhotonNetwork.CloudRegion}; " +
            $"offlineMode={PhotonNetwork.OfflineMode}; " +
            $"gameVersion={PhotonNetwork.GameVersion ?? "<null>"}; " +
            $"appVersion={PhotonNetwork.PhotonServerSettings.AppSettings.AppVersion ?? "<null>"}; " +
            $"authType={auth?.AuthType.ToString() ?? "<null>"}; " +
            $"userIdFingerprint={Plugin.Fingerprint(userId)}; " +
            $"userIdLength={userId.Length}");

        if (Plugin.IsLocalServerMode)
        {
            if (PhotonNetwork.OfflineMode)
            {
                Plugin.NotifyLocalServerNotDetected(
                    "OfflineMode fallback active");
            }
            else
            {
                Plugin.NotifyLocalServerDetected();
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
            $"userIdFingerprint={Plugin.Fingerprint(userId)}");

            Plugin.RefreshLanDiscoveryBroadcast("OnCreatedRoom");
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
            $"userIdFingerprint={Plugin.Fingerprint(userId)}; " +
            $"userIdLength={userId.Length}");

            Plugin.RefreshLanDiscoveryBroadcast("OnJoinedRoom");
    }

    public override void OnJoinRoomFailed(
        short returnCode,
        string message)
    {
        Plugin.Log.LogError(
            $"[{Time}] CALLBACK OnJoinRoomFailed: " +
            $"code={returnCode}; " +
            $"message={message}");
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
            $"localMode={Plugin.PhotonMode.Value}; " +
            $"server={Plugin.LocalServerAddress.Value}:{Plugin.LocalServerPort.Value}; " +
            $"protocol={Plugin.LocalServerProtocol.Value}");

        if (Plugin.IsLocalServerMode)
        {
            Plugin.NotifyLocalServerNotDetected(
                $"create room failed {returnCode}");
        }
    }

    public override void OnDisconnected(
        DisconnectCause cause)
    {
        var client = PhotonNetwork.NetworkingClient;
        var peer = client?.LoadBalancingPeer;

        Plugin.Log.LogError(
            $"[{Time}] CALLBACK OnDisconnected: " +
            $"cause={cause}; " +
            $"clientState={client?.State.ToString() ?? "<null>"}; " +
            $"peerState={peer?.PeerState.ToString() ?? "<null>"}; " +
            $"serverAddress={peer?.ServerAddress ?? "<null>"}; " +
            $"protocol={peer?.TransportProtocol.ToString() ?? "<null>"}; " +
            $"state={PhotonNetwork.NetworkClientState}; " +
            $"scene={UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}");

        Plugin.StopLanDiscoveryBroadcast("OnDisconnected");

        if (Plugin.IsLocalServerMode)
        {
            Plugin.NotifyLocalServerNotDetected(
                $"disconnect cause {cause}");
        }
    }

    public override void OnLeftRoom()
    {
        Plugin.Log.LogInfo(
            $"[{Time}] CALLBACK OnLeftRoom: " +
            $"scene={UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}; " +
            $"offlineMode={PhotonNetwork.OfflineMode}");

        Plugin.HandleLeftRoom();
    }

    public override void OnPlayerEnteredRoom(
        PhotonPlayer newPlayer)
    {
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
        Plugin.Log.LogWarning(
            $"[{Time}] CALLBACK OnPlayerLeftRoom: " +
            $"actor={otherPlayer.ActorNumber}; " +
            $"nickname={otherPlayer.NickName}; " +
            $"userId={otherPlayer.UserId}; " +
            $"players={PhotonNetwork.CurrentRoom?.PlayerCount}");
    }
}