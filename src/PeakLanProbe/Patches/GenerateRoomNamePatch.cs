using HarmonyLib;

namespace PeakLanProbe.Patches;

[HarmonyPatch(typeof(SteamLobbyHandler), "GenerateRoomName")]
internal static class GenerateRoomNamePatch
{
    [HarmonyPostfix]
    private static void Postfix(
        SteamLobbyHandler __instance,
        string __result)
    {
        Plugin.Log.LogInfo(
            $"GenerateRoomName: " +
            $"LobbySteamId={__instance.LobbySteamId}; " +
            $"RoomName={__result}");
    }
}