using System;
using Photon.Realtime;
using PeakLanMod.Lan.Model;

namespace PeakLanMod.Lan.Diagnostics;

internal static class LanErrorClassifier
{
    internal static LanErrorCode ClassifyReadinessTimeout()
    {
        return LanErrorCode.NameServerUnreachable;
    }

    internal static LanErrorCode ClassifyAutoStartFailure()
    {
        return LanErrorCode.LuxonNotRunning;
    }

    internal static LanErrorCode ClassifyJoinRoomFailure(
        short returnCode,
        string message)
    {
        if (returnCode == ErrorCode.GameDoesNotExist)
        {
            return LanErrorCode.RoomDoesNotExist;
        }

        if (ContainsTimeoutToken(message))
        {
            return LanErrorCode.Timeout;
        }

        return LanErrorCode.UnknownPhotonFailure;
    }

    internal static LanErrorCode ClassifyCreateRoomFailure(
        short returnCode,
        string message)
    {
        if (ContainsTimeoutToken(message))
        {
            return LanErrorCode.Timeout;
        }

        return LanErrorCode.UnknownPhotonFailure;
    }

    internal static LanErrorCode ClassifyDisconnect(
        DisconnectCause cause,
        string clientState)
    {
        switch (cause)
        {
            case DisconnectCause.None:
            case DisconnectCause.DisconnectByClientLogic:
                return LanErrorCode.None;
            case DisconnectCause.Exception:
                return LanErrorCode.NameServerUnreachable;
            case DisconnectCause.ClientTimeout:
            case DisconnectCause.ServerTimeout:
                return LanErrorCode.Timeout;
        }

        string causeText = cause.ToString();

        if (ContainsTimeoutToken(causeText))
        {
            return LanErrorCode.Timeout;
        }

        if (ContainsNameServerToken(clientState)
            || ContainsNameServerToken(causeText))
        {
            return LanErrorCode.NameServerUnreachable;
        }

        if (ContainsMasterServerToken(clientState)
            || ContainsMasterServerToken(causeText))
        {
            return LanErrorCode.MasterServerRedirectFailed;
        }

        if (ContainsGameServerToken(clientState)
            || ContainsGameServerToken(causeText))
        {
            return LanErrorCode.GameServerRedirectFailed;
        }

        return LanErrorCode.UnknownPhotonFailure;
    }

    internal static bool TryClassifyDiscoveryIncompatibility(
        string reason,
        out LanErrorCode code)
    {
        if (string.Equals(reason, "IncompatibleGameVersion", StringComparison.Ordinal))
        {
            code = LanErrorCode.IncompatibleGameVersion;
            return true;
        }

        if (string.Equals(reason, "IncompatibleModVersion", StringComparison.Ordinal))
        {
            code = LanErrorCode.IncompatibleModVersion;
            return true;
        }

        if (string.Equals(reason, "IncompatibleProtocolVersion", StringComparison.Ordinal))
        {
            code = LanErrorCode.IncompatibleProtocolVersion;
            return true;
        }

        code = LanErrorCode.None;
        return false;
    }

    private static bool ContainsTimeoutToken(string value)
    {
        return value.IndexOf("timeout", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool ContainsNameServerToken(string value)
    {
        return value.IndexOf("nameserver", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool ContainsMasterServerToken(string value)
    {
        return value.IndexOf("master", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool ContainsGameServerToken(string value)
    {
        return value.IndexOf("game", StringComparison.OrdinalIgnoreCase) >= 0;
    }
}