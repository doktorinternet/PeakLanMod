using HarmonyLib;
using Peak.Network;
using Photon.Realtime;

namespace PeakLanProbe.Patches;

[HarmonyPatch(typeof(NetworkConnector))]
internal static class NetworkConnectorPatches
{
    [HarmonyPatch(nameof(NetworkConnector.Start))]
    [HarmonyPrefix]
    private static void BeforeStart()
    {
        Plugin.Log.LogInfo("NetworkConnector.Start: PREFIX");
        Plugin.DumpPhotonSettings("before NetworkConnector.Start");
    }

    [HarmonyPatch(nameof(NetworkConnector.Start))]
    [HarmonyPostfix]
    private static void AfterStart()
    {
        Plugin.Log.LogInfo("NetworkConnector.Start: POSTFIX");
        Plugin.DumpPhotonSettings("after NetworkConnector.Start");
    }
}

[HarmonyPatch(typeof(NetworkingUtilities))]
internal static class NetworkingUtilitiesPatches
{
    [HarmonyPatch(nameof(NetworkingUtilities.LoadUserID))]
    [HarmonyPostfix]
    private static void AfterLoadUserId(AuthenticationValues __result)
    {
        if (__result is null)
        {
            Plugin.Log.LogWarning(
                "NetworkingUtilities.LoadUserID returned null.");

            return;
        }

        Plugin.Log.LogInfo(
            $"LoadUserID: AuthType={__result.AuthType}; " +
            $"UserIdSet={!string.IsNullOrWhiteSpace(__result.UserId)}; " +
            $"UserIdLength={__result.UserId?.Length ?? 0}");
    }
}