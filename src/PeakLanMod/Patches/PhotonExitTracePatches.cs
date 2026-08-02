using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Photon.Pun;

namespace PeakLanMod.Patches;

[HarmonyPatch]
internal static class PhotonExitTracePatches
{
    private static readonly HashSet<string> TargetNames =
    [
        nameof(PhotonNetwork.Disconnect),
        nameof(PhotonNetwork.LeaveRoom),
        nameof(PhotonNetwork.CloseConnection)
    ];

    private static IEnumerable<MethodBase> TargetMethods()
    {
        return AccessTools
            .GetDeclaredMethods(typeof(PhotonNetwork))
            .Where(method =>
                TargetNames.Contains(method.Name));
    }

    [HarmonyPrefix]
    private static void Prefix(
        MethodBase __originalMethod,
        object[] __args)
    {
        string arguments = string.Join(
            ", ",
            __args.Select(argument =>
                argument?.ToString() ?? "<null>"));

        var trace = new StackTrace(
            skipFrames: 2,
            fNeedFileInfo: false);

        Plugin.Log.LogWarning(
            $"Photon exit call: " +
            $"PhotonNetwork.{__originalMethod.Name}" +
            $"({arguments})\n" +
            $"{trace}");
    }
}