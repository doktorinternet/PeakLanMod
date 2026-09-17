using System;
using System.Collections;
using System.Security.Cryptography;
using System.Text;
using ExitGames.Client.Photon;
using Photon.Realtime;

namespace PeakLanMod.Lan.Model;

internal sealed class LanRoomPasswordPolicy
{
    internal const string SaltPropertyName = "pwd_salt";
    internal const string HashPropertyName = "pwd_hash";

    private LanRoomPasswordPolicy(string salt, string hash)
    {
        Salt = salt;
        Hash = hash;
    }

    internal string Salt { get; }
    internal string Hash { get; }

    internal static LanRoomPasswordPolicy Create(string password)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            throw new ArgumentException(
                "Password cannot be empty.",
                nameof(password));
        }

        byte[] saltBytes = new byte[16];
        RandomNumberGenerator.Fill(saltBytes);

        string saltText = Convert.ToBase64String(saltBytes);
        string hashText = ComputePasswordHash(saltText, password);

        return new LanRoomPasswordPolicy(saltText, hashText);
    }

    internal static bool TryApplyToRoomOptions(
        RoomOptions roomOptions,
        string password,
        out string salt,
        out string hash)
    {
        salt = string.Empty;
        hash = string.Empty;

        if (roomOptions is null)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            return false;
        }

        LanRoomPasswordPolicy policy = Create(password);
        salt = policy.Salt;
        hash = policy.Hash;

        ExitGames.Client.Photon.Hashtable customProperties = new ExitGames.Client.Photon.Hashtable();
        if (roomOptions.CustomRoomProperties is not null)
        {
            foreach (DictionaryEntry entry in roomOptions.CustomRoomProperties)
            {
                customProperties[entry.Key] = entry.Value;
            }
        }

        customProperties[SaltPropertyName] = salt;
        customProperties[HashPropertyName] = hash;
        roomOptions.CustomRoomProperties = customProperties;

        return true;
    }

    private static string ComputePasswordHash(
        string salt,
        string password)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(salt + password);
        using SHA256 sha256 = SHA256.Create();
        byte[] hashBytes = sha256.ComputeHash(bytes);
        return BitConverter.ToString(hashBytes).Replace("-", string.Empty);
    }
}
