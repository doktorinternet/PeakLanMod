using System;
using System.Reflection;
using HarmonyLib;
using Peak.Network;
using PeakLanMod.Lan.Services;

namespace PeakLanMod.Patches;

[HarmonyPatch]
internal static class NetworkConnectorHandleConnectionStatePatch
{
    [ThreadStatic]
    private static string? _lanHostRoomNameOverride;

    [ThreadStatic]
    private static HostState? _lanHostState;

    private static MethodBase TargetMethod()
    {
        return AccessTools.Method(
            typeof(NetworkConnector),
            "HandleConnectionState",
            [typeof(ConnectionState)]);
    }

    [HarmonyPrefix]
    private static void Prefix(ConnectionState state)
    {
        ClearOverride();

        if (!LanRuntimeContext.IsLanServerMode
            || state is not HostState hostState
            || string.IsNullOrWhiteSpace(hostState.RoomName))
        {
            return;
        }

        _lanHostRoomNameOverride = hostState.RoomName;
        _lanHostState = hostState;
    }

    [HarmonyFinalizer]
    private static Exception? Finalizer(Exception? __exception)
    {
        ClearOverride();
        return __exception;
    }

    internal static void ApplyLanHostRoomNameOverride(ref string roomName)
    {
        if (string.IsNullOrWhiteSpace(_lanHostRoomNameOverride))
        {
            return;
        }

        roomName = _lanHostRoomNameOverride;
        if (_lanHostState is not null)
        {
            _lanHostState.RoomName = roomName;
        }

        ClearOverride();
    }

    private static void ClearOverride()
    {
        _lanHostRoomNameOverride = null;
        _lanHostState = null;
    }
}