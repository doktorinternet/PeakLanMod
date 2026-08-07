using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using PeakLanMod.Lan.Services;
using Photon.Pun;
using Photon.Realtime;

namespace PeakLanMod.Patches;

[HarmonyPatch]
internal static class PhotonCallTracePatches
{
    private static readonly HashSet<string> TracedMethods =
    [
        "ConnectUsingSettings",
        "CreateRoom",
        "JoinRoom",
        "JoinOrCreateRoom",
        "JoinRandomRoom",
        "LeaveRoom",
        "Disconnect"
    ];

    private static IEnumerable<MethodBase> TargetMethods()
    {
        return AccessTools
            .GetDeclaredMethods(typeof(PhotonNetwork))
            .Where(method => TracedMethods.Contains(method.Name));
    }

    [HarmonyPrefix]
    private static void Prefix(
        MethodBase __originalMethod,
        object[] __args)
    {
        string arguments = string.Join(
            ", ",
            __args.Select(FormatArgument));

        Plugin.Log.LogInfo(
            $"PhotonNetwork.{__originalMethod.Name}({arguments})");

        if (__originalMethod.Name is nameof(PhotonNetwork.JoinRoom)
            or nameof(PhotonNetwork.CreateRoom)
            or nameof(PhotonNetwork.JoinOrCreateRoom))
        {
            LogJoinContext(__originalMethod.Name);
        }

        LanRuntimeContext.Services.LanServerRuntime.DumpPhotonSettings(
            $"before PhotonNetwork.{__originalMethod.Name}");
    }

    private static void LogJoinContext(string methodName)
    {
        AuthenticationValues? auth = PhotonNetwork.AuthValues;
        string userId = auth?.UserId ?? string.Empty;

        Plugin.Log.LogInfo(
            $"Photon join context [{methodName}]: " +
            $"state={PhotonNetwork.NetworkClientState}; " +
            $"ready={PhotonNetwork.IsConnectedAndReady}; " +
            $"offlineMode={PhotonNetwork.OfflineMode}; " +
            $"gameVersion={PhotonNetwork.GameVersion ?? "<null>"}; " +
            $"appVersion={PhotonNetwork.PhotonServerSettings.AppSettings.AppVersion ?? "<null>"}; " +
            $"authType={auth?.AuthType.ToString() ?? "<null>"}; " +
            $"userIdFingerprint={LanRuntimeContext.Fingerprint(userId)}; " +
            $"userIdLength={userId.Length}");
    }

    private static string FormatArgument(object? value)
    {
        if (value is null)
        {
            return "<null>";
        }

        string text = value.ToString() ?? value.GetType().Name;

        return text.Length <= 200
            ? text
            : text[..200] + "…";
    }
}