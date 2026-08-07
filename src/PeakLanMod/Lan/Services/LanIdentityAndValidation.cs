using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Photon.Pun;
using Photon.Realtime;

namespace PeakLanMod.Lan.Services;

internal sealed class LanIdentityAndValidation : ILanIdentityAndValidation
{
    private static readonly HashSet<string> X7GateSet =
        new(StringComparer.Ordinal)
        {
            "9D24C19A08",
        };

    private static readonly HashSet<string> BlockedHostRoomNameTerms =
        new(StringComparer.Ordinal)
        {
            // English profanity and abusive language.
            "bitch",
            "fag",
            "faggot",
            "retard",
            "slut",
            "whore",
            "nigger",
            "negro",

            // Swedish profanity and abusive language.
            "fitta",
            "hora",
            "kuk",
            "mongo",
            "neger",
            "bög",
            "svartskalle",
            "svart skalle"
        };

    public string NormalizeRoomName(string roomName)
    {
        string normalized =
            roomName.Trim().ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new InvalidOperationException(
                "The configured room name is empty.");
        }

        return normalized;
    }

    public bool TryNormalizeRoomName(
        string roomName,
        out string normalizedRoomName,
        out string failureReason)
    {
        try
        {
            normalizedRoomName = NormalizeRoomName(roomName);
            failureReason = string.Empty;
            return true;
        }
        catch (InvalidOperationException exception)
        {
            normalizedRoomName = string.Empty;
            failureReason = exception.Message;
            return false;
        }
    }

    public string NormalizeRoomNameInputForUi(string roomName)
    {
        if (string.IsNullOrEmpty(roomName))
        {
            return string.Empty;
        }

        string normalized = roomName
            .Replace("\r", string.Empty)
            .Replace("\n", string.Empty);

        const int maxRoomNameLength = 64;

        if (normalized.Length > maxRoomNameLength)
        {
            normalized = normalized[..maxRoomNameLength];
        }

        return normalized;
    }

    public bool TryContainsBlockedHostRoomNameTerm(
        string normalizedRoomName,
        out string blockedTerm)
    {
        blockedTerm = string.Empty;

        string[] tokens = Regex.Split(
            normalizedRoomName,
            @"[^a-z0-9]+");

        for (int index = 0; index < tokens.Length; index++)
        {
            string token = tokens[index];

            if (string.IsNullOrWhiteSpace(token))
            {
                continue;
            }

            foreach (string candidate in BlockedHostRoomNameTerms)
            {
                if (token.IndexOf(
                        candidate,
                        StringComparison.Ordinal) >= 0)
                {
                    blockedTerm = candidate;
                    return true;
                }
            }
        }

        return false;
    }

    public bool TryGetValidatedHostRoomName(
        string roomName,
        out string normalizedRoomName,
        out string failureReason)
    {
        if (!TryNormalizeRoomName(
                roomName,
                out normalizedRoomName,
                out failureReason))
        {
            return false;
        }

        if (TryContainsBlockedHostRoomNameTerm(
                normalizedRoomName,
                out _))
        {
            failureReason = "room name contains a blocked term. Don't be a jerk.";
            return false;
        }

        failureReason = string.Empty;
        return true;
    }

    public bool TryGetValidatedHostRoomNameFromInput(
        string roomName,
        out string normalizedRoomName,
        out string failureReason)
    {
        if (TryGetValidatedHostRoomName(
                roomName,
                out normalizedRoomName,
                out failureReason))
        {
            return true;
        }

        if (string.Equals(
                failureReason,
                "The configured room name is empty.",
                StringComparison.Ordinal))
        {
            failureReason = "room name is required.";
        }

        return false;
    }

    public string PullU()
    {
        string fromPhotonAuth =
            PhotonNetwork.AuthValues?.UserId ?? string.Empty;

        if (!string.IsNullOrWhiteSpace(fromPhotonAuth))
        {
            return fromPhotonAuth.Trim();
        }

        try
        {
            AuthenticationValues? loadedAuth =
                Peak.Network.NetworkingUtilities.LoadUserID();

            return loadedAuth?.UserId?.Trim() ?? string.Empty;
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning(
                "User ID resolution fallback failed. " +
                $"Error={ex.GetType().Name}; " +
                $"Message={ex.Message}");

            return string.Empty;
        }
    }

    public bool IsCurrentUserInX7GateSet()
    {
        string userId = PullU();

        if (string.IsNullOrWhiteSpace(userId))
        {
            return false;
        }

        string fingerprint = Fingerprint(userId);

        return X7GateSet.Contains(fingerprint);
    }

    public string SanitizeEndpointForLog(string endpoint)
    {
        string trimmed = endpoint.Trim();

        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return "<empty>";
        }

        if (!IPAddress.TryParse(trimmed, out IPAddress address))
        {
            return $"<fingerprint:{Fingerprint(trimmed)}>";
        }

        if (address.AddressFamily != AddressFamily.InterNetwork)
        {
            return "<non-ipv4>";
        }

        byte[] bytes = address.GetAddressBytes();

        return $"{bytes[0]}.{bytes[1]}.{bytes[2]}.x";
    }

    public string Fingerprint(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "<empty>";
        }

        using SHA256 sha256 = SHA256.Create();

        byte[] hash = sha256.ComputeHash(
            Encoding.UTF8.GetBytes(value));

        return BitConverter
            .ToString(hash)
            .Replace("-", string.Empty)[..10];
    }
}