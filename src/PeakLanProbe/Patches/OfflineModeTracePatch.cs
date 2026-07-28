using System.Diagnostics;
using System.Reflection;
using HarmonyLib;
using Photon.Pun;

namespace PeakLanProbe.Patches;

[HarmonyPatch]
internal static class OfflineModeTracePatch
{
    private static MethodBase? TargetMethod()
    {
        return AccessTools.PropertySetter(
            typeof(PhotonNetwork),
            nameof(PhotonNetwork.OfflineMode));
    }

    private static void Prefix(bool value)
    {
        string level = value ? "Warning" : "Info";

        var trace = new StackTrace(
            skipFrames: 1,
            fNeedFileInfo: false);

        string message =
            $"PhotonNetwork.OfflineMode set to {value}; " +
            $"currentState={PhotonNetwork.NetworkClientState}; " +
            $"connected={PhotonNetwork.IsConnected}; " +
            $"ready={PhotonNetwork.IsConnectedAndReady}\n" +
            $"{trace}";

        if (value)
        {
            Plugin.Log.LogWarning(message);
            return;
        }

        Plugin.Log.LogInfo(message);
    }
}