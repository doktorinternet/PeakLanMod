using HarmonyLib;
using Peak.Network;
using Photon.Realtime;

namespace PeakLanMod.Patches;

[HarmonyPatch(
    typeof(NetworkConnector),
    nameof(NetworkConnector.Start))]
internal static class NetworkConnectorStartPatch
{
    [HarmonyPrefix]
    private static void Prefix()
    {
        ConnectionState state =
            GameHandler
                .GetService<ConnectionService>()
                .StateMachine
                .CurrentState;

        Plugin.Log.LogInfo(
            $"NetworkConnector.Start PREFIX: " +
            $"state={state.GetType().FullName}");

        DumpStateProperties(state);
    }

    private static void DumpStateProperties(
        ConnectionState state)
    {
        switch (state)
        {
            case HostState host:
                Plugin.Log.LogInfo(
                    $"HostState: RoomName={host.RoomName}");
                break;

            case JoinSpecificRoomState join:
                Plugin.Log.LogInfo(
                    $"JoinSpecificRoomState: " +
                    $"RoomName={join.RoomName}; " +
                    $"Region={join.RegionToJoin}");
                break;

            case InRoomState:
                Plugin.Log.LogInfo("InRoomState");
                break;

            default:
                Plugin.Log.LogInfo(
                    $"Unhandled state: {state}");
                break;
        }
    }
}