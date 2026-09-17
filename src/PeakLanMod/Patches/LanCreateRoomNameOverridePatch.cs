using System.Reflection;
using HarmonyLib;
using PeakLanMod.Lan.Services;
using Photon.Pun;
using Photon.Realtime;

namespace PeakLanMod.Patches;

[HarmonyPatch]
internal static class LanCreateRoomNameOverridePatch
{
    private static MethodBase TargetMethod()
    {
        return AccessTools.Method(
            typeof(PhotonNetwork),
            nameof(PhotonNetwork.CreateRoom),
            [
                typeof(string),
                typeof(RoomOptions),
                typeof(TypedLobby),
                typeof(string[])
            ]);
    }

    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    private static void Prefix(ref string __0)
    {
        if (!LanRuntimeContext.IsLanServerMode)
        {
            return;
        }

        NetworkConnectorHandleConnectionStatePatch
            .ApplyLanHostRoomNameOverride(ref __0);
    }
}