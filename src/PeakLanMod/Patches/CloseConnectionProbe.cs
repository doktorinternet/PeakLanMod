using System.Diagnostics;
using System.Reflection;
using HarmonyLib;
using Photon.Pun;
using PhotonPlayer = Photon.Realtime.Player;

namespace PeakLanMod.Patches;

[HarmonyPatch]
internal static class CloseConnectionProbe
{
    private static MethodInfo TargetMethod()
    {
        return AccessTools.Method(
            typeof(PhotonNetwork),
            nameof(PhotonNetwork.CloseConnection),
            [typeof(PhotonPlayer)]);
    }

    private static void Prefix(
        PhotonPlayer kickPlayer)
    {
        var trace = new StackTrace(
            skipFrames: 1,
            fNeedFileInfo: false);

        Plugin.Log.LogError(
            "HOST CALLED PhotonNetwork.CloseConnection:\n" +
            $"Actor={kickPlayer?.ActorNumber}\n" +
            $"Nickname={kickPlayer?.NickName}\n" +
            $"UserId={kickPlayer?.UserId}\n" +
            $"LocalIsMaster={PhotonNetwork.IsMasterClient}\n" +
            $"EnableCloseConnection=" +
                $"{PhotonNetwork.EnableCloseConnection}\n" +
            $"Room={PhotonNetwork.CurrentRoom?.Name}\n" +
            $"Call stack:\n{trace}");
    }
}