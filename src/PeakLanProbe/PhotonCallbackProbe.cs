using System;
using Photon.Pun;
using Photon.Realtime;
using PhotonPlayer = Photon.Realtime.Player;

namespace PeakLanProbe;

internal sealed class PhotonCallbackProbe :
    MonoBehaviourPunCallbacks
{
    private static string Time =>
        DateTime.Now.ToString("HH:mm:ss.fff");

    public override void OnConnectedToMaster()
    {
        Plugin.Log.LogInfo(
            $"[{Time}] CALLBACK OnConnectedToMaster: " +
            $"region={PhotonNetwork.CloudRegion}");
    }

    public override void OnCreatedRoom()
    {
        Plugin.Log.LogInfo(
            $"[{Time}] CALLBACK OnCreatedRoom: " +
            $"room={PhotonNetwork.CurrentRoom?.Name}");
    }

    public override void OnJoinedRoom()
    {
        PhotonPlayer localPlayer = PhotonNetwork.LocalPlayer;

        Plugin.Log.LogInfo(
            $"[{Time}] CALLBACK OnJoinedRoom: " +
            $"room={PhotonNetwork.CurrentRoom?.Name}; " +
            $"players={PhotonNetwork.CurrentRoom?.PlayerCount}; " +
            $"actor={localPlayer?.ActorNumber}; " +
            $"nickname={localPlayer?.NickName}; " +
            $"master={PhotonNetwork.IsMasterClient}; " +
            $"scene={UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}");
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

    public override void OnDisconnected(
        DisconnectCause cause)
    {
        Plugin.Log.LogError(
            $"[{Time}] CALLBACK OnDisconnected: " +
            $"cause={cause}; " +
            $"state={PhotonNetwork.NetworkClientState}; " +
            $"scene={UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}");
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