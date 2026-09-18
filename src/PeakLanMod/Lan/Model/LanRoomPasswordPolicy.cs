using System;
using System.Security.Cryptography;
using Photon.Realtime;

namespace PeakLanMod.Lan.Model;

internal static class LanRoomPasswordPolicy
{
    internal const string SaltPropertyKey = "pwd_salt";
    internal const string HashPropertyKey = "pwd_hash";

    internal static bool IsPasswordProtected(Room? room)
    {
        return room?.CustomProperties?.ContainsKey(SaltPropertyKey) == true;
    }

    internal static (string Salt, string Hash) Create(string password)
    {
        byte[] salt = new byte[16];

        using (RandomNumberGenerator random = RandomNumberGenerator.Create())
        {
            random.GetBytes(salt);
        }

        string encodedSalt = Convert.ToBase64String(salt);
        return (encodedSalt, ComputePasswordHash(encodedSalt, password));
    }

    internal static string ComputePasswordHash(string encodedSalt, string password)
    {
        byte[] salt = Convert.FromBase64String(encodedSalt);
        byte[] passwordBytes = System.Text.Encoding.UTF8.GetBytes(password ?? string.Empty);
        byte[] material = new byte[salt.Length + passwordBytes.Length];

        Buffer.BlockCopy(salt, 0, material, 0, salt.Length);
        Buffer.BlockCopy(passwordBytes, 0, material, salt.Length, passwordBytes.Length);

        using (SHA256 sha256 = SHA256.Create())
        {
            return Convert.ToBase64String(sha256.ComputeHash(material));
        }
    }

    internal static bool HashesMatch(string expectedHash, string submittedHash)
    {
        try
        {
            byte[] expected = Convert.FromBase64String(expectedHash);
            byte[] submitted = Convert.FromBase64String(submittedHash);

            if (expected.Length != submitted.Length)
            {
                return false;
            }

            int difference = 0;

            for (int index = 0; index < expected.Length; index++)
            {
                difference |= expected[index] ^ submitted[index];
            }

            return difference == 0;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
