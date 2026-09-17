using System;
using System.Collections.Generic;
using ExitGames.Client.Photon;
using PeakLanMod.Lan.Model;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.SceneManagement;
using Zorro.Core;
using PhotonPlayer = Photon.Realtime.Player;

namespace PeakLanMod.Lan.Services;

internal sealed class LanRoomPasswordAuthService : MonoBehaviourPunCallbacks, IOnEventCallback
{
    private const byte PasswordVerifyEventCode = 81;
    private const byte PasswordAuthOkEventCode = 82;
    private const byte PasswordAuthDeniedEventCode = 83;
    private const float VerificationTimeoutSeconds = 4f;

    private readonly Dictionary<int, float> _hostVerificationDeadlines = new();
    private bool _awaitingLocalVerification;
    private bool _returningToMainMenu;
    private float _localVerificationDeadline;

    public override void OnJoinedRoom()
    {
        if (!LanRuntimeContext.IsLanServerMode || PhotonNetwork.IsMasterClient)
        {
            return;
        }

        if (!TryGetRoomPasswordSalt(out string salt))
        {
            return;
        }

        string password = LanRuntimeContext.Services.DirectConnect
            .ConsumeJoinPasswordForAuthentication();
        string passwordHash = LanRoomPasswordPolicy.ComputePasswordHash(salt, password);

        _awaitingLocalVerification = true;
        _localVerificationDeadline = Time.realtimeSinceStartup + VerificationTimeoutSeconds;

        bool sent = PhotonNetwork.RaiseEvent(
            PasswordVerifyEventCode,
            passwordHash,
            new RaiseEventOptions { Receivers = ReceiverGroup.MasterClient },
            SendOptions.SendReliable);

        if (sent)
        {
            Plugin.Log.LogInfo("Password verification submitted to the room host.");
            return;
        }

        DenyLocalJoin("could not send password verification");
    }

    public override void OnPlayerEnteredRoom(PhotonPlayer newPlayer)
    {
        if (!LanRuntimeContext.IsLanServerMode
            || !PhotonNetwork.IsMasterClient
            || !TryGetExpectedPasswordHash(out _))
        {
            return;
        }

        _hostVerificationDeadlines[newPlayer.ActorNumber] =
            Time.realtimeSinceStartup + VerificationTimeoutSeconds;
        Plugin.Log.LogInfo($"Password verification required for joining actor {newPlayer.ActorNumber}.");
    }

    public override void OnPlayerLeftRoom(PhotonPlayer otherPlayer)
    {
        _hostVerificationDeadlines.Remove(otherPlayer.ActorNumber);
    }

    public override void OnLeftRoom()
    {
        _awaitingLocalVerification = false;
        _localVerificationDeadline = 0f;
        _hostVerificationDeadlines.Clear();
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        if (!_awaitingLocalVerification)
        {
            return;
        }

        DenyLocalJoin("disconnected while password verification was pending");
    }

    public void OnEvent(EventData photonEvent)
    {
        if (photonEvent.Code == PasswordVerifyEventCode)
        {
            HandlePasswordVerify(photonEvent);
        }
        else if (photonEvent.Code == PasswordAuthOkEventCode)
        {
            HandlePasswordAuthOk(photonEvent);
        }
        else if (photonEvent.Code == PasswordAuthDeniedEventCode)
        {
            HandlePasswordAuthDenied(photonEvent);
        }
    }

    private void Update()
    {
        float now = Time.realtimeSinceStartup;

        if (_awaitingLocalVerification && now >= _localVerificationDeadline)
        {
            DenyLocalJoin("password verification timed out");
        }

        if (!LanRuntimeContext.IsLanServerMode
            || !PhotonNetwork.IsMasterClient
            || _hostVerificationDeadlines.Count == 0)
        {
            return;
        }

        var expiredActors = new List<int>();

        foreach ((int actorNumber, float deadline) in _hostVerificationDeadlines)
        {
            if (now >= deadline)
            {
                expiredActors.Add(actorNumber);
            }
        }

        foreach (int actorNumber in expiredActors)
        {
            _hostVerificationDeadlines.Remove(actorNumber);
            PhotonPlayer? player = PhotonNetwork.CurrentRoom?.GetPlayer(actorNumber);

            if (player == null)
            {
                continue;
            }

            Plugin.Log.LogWarning($"Password verification timed out for joining actor {actorNumber}; closing connection.");
            PhotonNetwork.CloseConnection(player);
        }
    }

    private static bool TryGetRoomPasswordSalt(out string salt)
    {
        salt = string.Empty;
        object? rawSalt = PhotonNetwork.CurrentRoom?.CustomProperties[LanRoomPasswordPolicy.SaltPropertyKey];

        if (rawSalt is not string configuredSalt || string.IsNullOrWhiteSpace(configuredSalt))
        {
            return false;
        }

        salt = configuredSalt;
        return true;
    }

    private static bool TryGetExpectedPasswordHash(out string hash)
    {
        hash = string.Empty;
        object? rawHash = PhotonNetwork.CurrentRoom?.CustomProperties[LanRoomPasswordPolicy.HashPropertyKey];

        if (rawHash is not string configuredHash || string.IsNullOrWhiteSpace(configuredHash))
        {
            return false;
        }

        hash = configuredHash;
        return true;
    }

    private void HandlePasswordVerify(EventData photonEvent)
    {
        if (!LanRuntimeContext.IsLanServerMode
            || !PhotonNetwork.IsMasterClient
            || !TryGetExpectedPasswordHash(out string expectedHash)
            || photonEvent.Sender <= 0)
        {
            return;
        }

        string submittedHash = photonEvent.CustomData as string ?? string.Empty;
        bool isValid = LanRoomPasswordPolicy.HashesMatch(expectedHash, submittedHash);
        _hostVerificationDeadlines.Remove(photonEvent.Sender);

        PhotonNetwork.RaiseEvent(
            isValid ? PasswordAuthOkEventCode : PasswordAuthDeniedEventCode,
            null,
            new RaiseEventOptions { TargetActors = [photonEvent.Sender] },
            SendOptions.SendReliable);

        if (isValid)
        {
            Plugin.Log.LogInfo($"Password verification accepted for joining actor {photonEvent.Sender}.");
            return;
        }

        Plugin.Log.LogWarning($"Password verification denied for joining actor {photonEvent.Sender}; closing connection.");
        PhotonPlayer? player = PhotonNetwork.CurrentRoom?.GetPlayer(photonEvent.Sender);

        if (player != null)
        {
            PhotonNetwork.CloseConnection(player);
        }
    }

    private void HandlePasswordAuthOk(EventData photonEvent)
    {
        if (!_awaitingLocalVerification || photonEvent.Sender != PhotonNetwork.MasterClient?.ActorNumber)
        {
            return;
        }

        _awaitingLocalVerification = false;
        _localVerificationDeadline = 0f;
        Plugin.Log.LogInfo("Password verification accepted by the room host.");
    }

    private void HandlePasswordAuthDenied(EventData photonEvent)
    {
        if (!_awaitingLocalVerification || photonEvent.Sender != PhotonNetwork.MasterClient?.ActorNumber)
        {
            return;
        }

        DenyLocalJoin("incorrect password");
    }

    private void DenyLocalJoin(string reason)
    {
        _awaitingLocalVerification = false;
        _localVerificationDeadline = 0f;

        LanRuntimeContext.Services.ErrorState.ReportStructuredLanError(
            LanErrorCode.IncorrectPassword,
            source: "LanRoomPasswordAuthService",
            message: "The room password was not accepted.",
            context: reason);

        Plugin.Log.LogWarning($"Password-protected room join denied: {reason}.");

        if (PhotonNetwork.InRoom)
        {
            PhotonNetwork.LeaveRoom();
        }

        NetworkConnector.ChangeConnectionState<DefaultConnectionState>();
        ReturnToMainMenu();
    }

    private void ReturnToMainMenu()
    {
        if (_returningToMainMenu)
        {
            return;
        }

        _returningToMainMenu = true;
        LoadingScreenHandler.KillCurrentLoadingScreen();
        SceneManager.LoadScene("Title", LoadSceneMode.Single);
    }
}