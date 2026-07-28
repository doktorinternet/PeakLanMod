using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Photon.Pun;

namespace PeakLanProbe.Patches;

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

        Plugin.DumpPhotonSettings(
            $"before PhotonNetwork.{__originalMethod.Name}");
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