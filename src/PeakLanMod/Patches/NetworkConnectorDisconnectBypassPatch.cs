using HarmonyLib;
using Photon.Pun;
using Photon.Realtime;

namespace PeakLanMod.Patches;

[HarmonyPatch(
    typeof(NetworkConnector),
    nameof(NetworkConnector.OnDisconnected),
    [typeof(DisconnectCause)])]
internal static class NetworkConnectorDisconnectBypassPatch
{
    [HarmonyPrefix]
    private static bool Prefix(
        DisconnectCause cause)
    {
        if (!Plugin.IsLocalServerMode
            || !Plugin.AutoSkipPhotonFailureDialog.Value)
        {
            return true;
        }

        Plugin.Log.LogInfo(
            "LocalServer mode: bypassing NetworkConnector.OnDisconnected modal. " +
            $"Cause={cause}; " +
            $"Scene={UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}");

        NetworkConnector.ChangeConnectionState<DefaultConnectionState>();

        if (!PhotonNetwork.OfflineMode
            && !PhotonNetwork.InRoom)
        {
            PhotonNetwork.OfflineMode = true;
        }

        // Skip game's default disconnect modal path in local-server mode.
        return false;
    }
}
