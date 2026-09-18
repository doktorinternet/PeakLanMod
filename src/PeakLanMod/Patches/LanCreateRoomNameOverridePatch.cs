using System.Reflection;
using ExitGames.Client.Photon;
using HarmonyLib;
using PeakLanMod.Lan.Model;
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
    private static void Prefix(ref string __0, ref RoomOptions __1)
    {
        if (!LanRuntimeContext.IsLanServerMode)
        {
            return;
        }

        NetworkConnectorHandleConnectionStatePatch
            .ApplyLanHostRoomNameOverride(ref __0);

        string password = LanRuntimeContext.ConsumePendingHostRoomPassword();

        if (string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        (string salt, string hash) = LanRoomPasswordPolicy.Create(password);
        Hashtable properties = __1.CustomRoomProperties ?? new Hashtable();
        properties[LanRoomPasswordPolicy.SaltPropertyKey] = salt;
        properties[LanRoomPasswordPolicy.HashPropertyKey] = hash;
        __1.CustomRoomProperties = properties;

        Plugin.Log.LogInfo("Created password-protected LAN room properties.");
    }
}