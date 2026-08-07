using HarmonyLib;
using PeakLanMod.Lan.Services;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine.SceneManagement;

namespace PeakLanMod.Patches;

[HarmonyPatch(
    typeof(MainMenuPageHandler),
    "Update")]
internal static class MainMenuPageHandlerUpdateBypassPatch
{
    private static bool _loggedSuppressedFrame;

    [HarmonyPrefix]
    private static bool Prefix()
    {
        if (!LanRuntimeContext.IsLanServerMode
            || !LanRuntimeContext.Options.AutoSkipPhotonFailureDialog.Value)
        {
            _loggedSuppressedFrame = false;
            return true;
        }

        if (!string.Equals(
                SceneManager.GetActiveScene().name,
                "Title",
                System.StringComparison.Ordinal))
        {
            _loggedSuppressedFrame = false;
            return true;
        }

        if (PhotonNetwork.InRoom)
        {
            _loggedSuppressedFrame = false;
            return true;
        }

        ClientState state = PhotonNetwork.NetworkClientState;

        if (state != ClientState.Disconnected)
        {
            _loggedSuppressedFrame = false;
            return true;
        }

        if (!PhotonNetwork.OfflineMode)
        {
            PhotonNetwork.OfflineMode = true;
        }

        if (!_loggedSuppressedFrame)
        {
            _loggedSuppressedFrame = true;

            Plugin.Log.LogInfo(
                "LanServer mode: suppressing MainMenuPageHandler.Update " +
                "while startup state is disconnected to bypass Photon retry/offline popup.");
        }

        // Prevent the startup disconnected modal path from executing.
        return false;
    }
}
