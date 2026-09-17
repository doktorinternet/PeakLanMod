using System.Reflection;
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

        if (!LanRuntimeContext.Options.RequirePasswordForHostedRoom.Value)
        {
            return;
        }

        string password = LanRuntimeContext.ConsumePendingHostRoomPassword();
        if (string.IsNullOrWhiteSpace(password))
        {
            Plugin.Log.LogWarning(
                "Host room password required but no password was supplied for this room creation attempt.");
            return;
        }

        if (__1 is null)
        {
            __1 = new RoomOptions();
        }

        if (LanRoomPasswordPolicy.TryApplyToRoomOptions(__1, password, out string salt, out string hash))
        {
            Plugin.Log.LogInfo(
                "Password-protected room policy injected into host room creation. " +
                $"SaltFingerprint={LanRuntimeContext.Fingerprint(salt)}; " +
                $"HashFingerprint={LanRuntimeContext.Fingerprint(hash)}");
        }
    }
}