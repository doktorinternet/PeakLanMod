using System;
using PeakLanMod.Lan.Model;

namespace PeakLanMod.Lan.Services;

// Single source of truth for player-facing strings shown in the in-game LOG panel.
internal static class LanClientLogMessages
{
    private const string UnknownRoomName = "unknown room";
    private const string UnknownOwner = "unknown host";

    internal static string HostingStarted(
        string roomName,
        bool passwordProtected)
    {
        return $"Hosting new lobby: {DescribeRoomName(roomName)}{PasswordSuffix(passwordProtected)}";
    }

    internal static string JoinedRoom(
        string roomName,
        bool passwordProtected,
        string roomOwner)
    {
        return $"Joined {DescribeOwner(roomOwner)}'s lobby: {DescribeRoomName(roomName)}{PasswordSuffix(passwordProtected)}";
    }

    internal static string LeftRoom(
        string roomName,
        string roomOwner)
    {
        return $"Left {DescribeOwner(roomOwner)}'s lobby: {DescribeRoomName(roomName)}.";
    }

    internal static string FailedToHost(
        string roomName,
        string message)
    {
        return $"Failed to host lobby {DescribeRoomName(roomName)}: {message}";
    }

    internal static string FailedToJoin(
        string roomName,
        string message)
    {
        return $"Failed to join lobby {DescribeRoomName(roomName)}: {message}";
    }

    internal static string Disconnected(
        string message)
    {
        return $"Disconnected: {message}";
    }

    internal static string ConnectModeConfigured(
        bool isLocalTarget)
    {
        return isLocalTarget
            ? "Connect mode is configured for a local server."
            : "Connect mode is configured for a remote server.";
    }

    internal static string ServerSelfTestStarted()
    {
        return "Checking server setup...";
    }

    internal static string LocalServerReady()
    {
        return "Local server is properly setup and ready for hosting.";
    }

    internal static string LocalServerNotReady(
        string reason)
    {
        return $"Local server is not properly setup, hosting is disabled. Reason: {reason}";
    }

    internal static string RemoteServerReachableInitial()
    {
        return "Remote server connection looks good.";
    }

    internal static string RemoteServerUnreachableInitial(
        string reason)
    {
        return $"Remote server connection is missing: no response when requested. Reason: {reason}. " +
            "It may still be running with HTTP disabled - hosting stays enabled, but server status should be verified manually.";
    }

    internal static string RemoteServerConnectionLost()
    {
        return "Lost connection to remote server. It may still be running with HTTP disabled. Hosting stays enabled, but server status should be verified manually.";
    }

    internal static string RemoteServerConnectionRegained()
    {
        return "Regained connection to remote server.";
    }

    internal static string ServerSelfTestDisregardIfNotHosting()
    {
        return "If you don't plan to host, you can safely disregard this message.";
    }

    internal static string HostUnavailable(
        string reason)
    {
        return $"Cannot host: {reason}";
    }

    internal static string ConnectionProblem(
        string message)
    {
        return $"Connection problem: {message}";
    }

    internal static string ForStructuredError(
        LanErrorDetail detail,
        string activeAttemptRoomName)
    {
        return detail.Source switch
        {
            "OnCreateRoomFailed" => FailedToHost(activeAttemptRoomName, detail.Message),
            "EnsureHostLanServerProcess" => FailedToHost(activeAttemptRoomName, detail.Message),
            "OnJoinRoomFailed" => FailedToJoin(activeAttemptRoomName, detail.Message),
            "LanRoomPasswordAuthService" => FailedToJoin(activeAttemptRoomName, detail.Message),
            "TryJoinSelectedLanSession" => FailedToJoin(activeAttemptRoomName, detail.Message),
            "OnDisconnected" => Disconnected(detail.Message),
            _ when detail.Source.IndexOf("Host", StringComparison.OrdinalIgnoreCase) >= 0
                => FailedToHost(activeAttemptRoomName, detail.Message),
            _ when detail.Source.IndexOf("Join", StringComparison.OrdinalIgnoreCase) >= 0
                => FailedToJoin(activeAttemptRoomName, detail.Message),
            _ => ConnectionProblem(detail.Message)
        };
    }

    private static string DescribeRoomName(string roomName)
    {
        return string.IsNullOrWhiteSpace(roomName)
            ? UnknownRoomName
            : roomName;
    }

    private static string DescribeOwner(string roomOwner)
    {
        return string.IsNullOrWhiteSpace(roomOwner)
            ? UnknownOwner
            : roomOwner;
    }

    private static string PasswordSuffix(bool passwordProtected)
    {
        return passwordProtected
            ? " | Password protected"
            : string.Empty;
    }
}
