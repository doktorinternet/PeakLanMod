using System.Reflection;
using System;
using HarmonyLib;
using Photon.Pun;

namespace PeakLanMod.Patches;

[HarmonyPatch]
internal static class OfflineModeTracePatch
{
    private static bool? _lastLoggedValue;
    private static DateTime _lastLogAtUtc;

    private static MethodBase? TargetMethod()
    {
        return AccessTools.PropertySetter(
            typeof(PhotonNetwork),
            nameof(PhotonNetwork.OfflineMode));
    }

    private static void Prefix(bool value)
    {
        DateTime nowUtc = DateTime.UtcNow;

        if (_lastLoggedValue == value
            && _lastLogAtUtc != default
            && (nowUtc - _lastLogAtUtc).TotalMilliseconds < 2000)
        {
            return;
        }

        _lastLoggedValue = value;
        _lastLogAtUtc = nowUtc;

        string message =
            $"PhotonNetwork.OfflineMode set to {value}; " +
            $"currentState={PhotonNetwork.NetworkClientState}; " +
            $"connected={PhotonNetwork.IsConnected}; " +
            $"ready={PhotonNetwork.IsConnectedAndReady}";

        if (value)
        {
            Plugin.Log.LogWarning(message);
            return;
        }

        Plugin.Log.LogInfo(message);
    }
}