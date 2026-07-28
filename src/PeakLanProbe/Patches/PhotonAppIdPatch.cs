using HarmonyLib;
using Peak.Network;

namespace PeakLanProbe.Patches;

[HarmonyPatch(
    typeof(NetworkingUtilities),
    nameof(NetworkingUtilities.ConnectToNetwork))]
internal static class PhotonAppIdPatch
{
    [HarmonyPrefix]
    private static void Prefix()
    {
        if (!Plugin.DirectConnectEnabled.Value)
        {
            return;
        }

        Plugin.ApplyConfiguredPhotonSettings();
    }
}