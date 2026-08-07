using HarmonyLib;
using Peak.Network;

namespace PeakLanMod.Patches;

[HarmonyPatch(
    typeof(NetworkingUtilities),
    nameof(NetworkingUtilities.ConnectToNetwork))]
internal static class PhotonAppIdPatch
{
    [HarmonyPrefix]
    private static void Prefix()
    {
        Plugin.ApplyConfiguredPhotonSettings();
    }
}