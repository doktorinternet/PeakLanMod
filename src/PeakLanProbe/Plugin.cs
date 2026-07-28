using BepInEx;
using BepInEx.Logging;
using Photon.Pun;
using Photon.Realtime;
using HarmonyLib;
using BepInEx.Configuration;
using Zorro.Core;
using System;
using UnityEngine;
using System.Text;
using System.Security.Cryptography;
namespace PeakLanProbe;

// Here are some basic resources on code style and naming conventions to help
// you in your first CSharp plugin!
// https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions
// https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/identifier-names
// https://learn.microsoft.com/en-us/dotnet/standard/design-guidelines/names-of-namespaces

// The BepInAutoPlugin attribute comes from the Hamunii.BepInEx.AutoPlugin
// NuGet package, and it will generate the BepInPlugin attribute for you!
// For more info, see https://github.com/Hamunii/BepInEx.AutoPlugin

/// <summary>
/// The BepInEx plugin class of PeakLanProbe.
/// </summary>
[BepInPlugin(
    PluginGuid,
    PluginName,
    PluginVersion)]
public sealed class Plugin : BaseUnityPlugin
{
    public const string PluginGuid = "AntonWahlberg.PeakLanProbe";
    public const string PluginName = "PEAK LAN Probe";
    public const string PluginVersion = "0.1.0";

    internal static ManualLogSource Log { get; private set; } = null!;

    private Harmony? _harmony;
    private ClientState? _previousState;

    private void Awake()
    {
        Log = Logger;

        ConfigureDirectConnect();

        gameObject.AddComponent<PhotonCallbackProbe>();

        _harmony = new Harmony(PluginGuid);
        _harmony.PatchAll();

        Logger.LogInfo("PEAK LAN Probe loaded.");
        DumpPhotonSettings("Plugin.Awake");
    }
    private void Update()
    {
        LogPhotonStateChanges();

        if (!DirectConnectEnabled.Value)
        {
            return;
        }

        if (_hostKey.Value.IsDown())
        {
            Logger.LogInfo("Host key pressed.");
            StartDirectHost();
        }

        if (_joinKey.Value.IsDown())
        {
            Logger.LogInfo("Join key pressed.");
            StartDirectJoin();
        }
    }

    private void LogPhotonStateChanges()
    {
        ClientState currentState =
            PhotonNetwork.NetworkClientState;

        if (_previousState == currentState)
        {
            return;
        }

        Logger.LogInfo(
            $"Photon state: " +
            $"{_previousState?.ToString() ?? "<initial>"} " +
            $"-> {currentState}; " +
            $"connected={PhotonNetwork.IsConnected}; " +
            $"ready={PhotonNetwork.IsConnectedAndReady}; " +
            $"region={PhotonNetwork.CloudRegion}; " +
            $"inLobby={PhotonNetwork.InLobby}; " +
            $"inRoom={PhotonNetwork.InRoom}; " +
            $"room={PhotonNetwork.CurrentRoom?.Name ?? "<none>"}; " +
            $"players={PhotonNetwork.CurrentRoom?.PlayerCount ?? 0}");

        _previousState = currentState;
    }

    private void OnDestroy()
    {
        _harmony?.UnpatchSelf();
    }

    internal static void DumpPhotonSettings(string source)
    {
        var settings =
            PhotonNetwork.PhotonServerSettings.AppSettings;

        Log.LogInfo(
            $"Photon settings [{source}]: " +
            $"UseNameServer={settings.UseNameServer}; " +
            $"Server={settings.Server ?? "<null>"}; " +
            $"Port={settings.Port}; " +
            $"Protocol={settings.Protocol}; " +
            $"FixedRegion={settings.FixedRegion ?? "<null>"}; " +
            $"AppVersion={settings.AppVersion ?? "<null>"}; " +
            $"RealtimeFingerprint=" +
                $"{Fingerprint(settings.AppIdRealtime)}; " +
            $"VoiceFingerprint=" +
                $"{Fingerprint(settings.AppIdVoice)}");
    }
    internal static ConfigEntry<bool> DirectConnectEnabled =
        null!;

    private ConfigEntry<string> _roomName = null!;
    private ConfigEntry<string> _region = null!;
    private ConfigEntry<KeyboardShortcut> _hostKey = null!;
    private ConfigEntry<KeyboardShortcut> _joinKey = null!;
    private ConfigEntry<string> _appIdRealtime = null!;
    private ConfigEntry<string> _appIdVoice = null!;

    private void ConfigureDirectConnect()
    {
        DirectConnectEnabled = Config.Bind(
            "Direct Connect",
            "Enabled",
            true,
            "Enable the experimental direct-connect flow.");

        _roomName = Config.Bind(
            "Direct Connect",
            "RoomName",
            "antonwahlberg-test-room",
            "Photon room name.");

        _region = Config.Bind(
            "Direct Connect",
            "Region",
            "eu",
            "Photon Cloud region.");

        _appIdRealtime = Config.Bind(
            "Photon",
            "AppIdRealtime",
            string.Empty,
            "Custom Photon PUN application ID.");

        _appIdVoice = Config.Bind(
            "Photon",
            "AppIdVoice",
            string.Empty,
            "Custom Photon Voice application ID.");

        _hostKey = Config.Bind(
            "Direct Connect",
            "HostKey",
            new KeyboardShortcut(KeyCode.F6),
            "Start direct host.");

        _joinKey = Config.Bind(
            "Direct Connect",
            "JoinKey",
            new KeyboardShortcut(KeyCode.F7),
            "Start direct join.");

        AppIdRealtime = Config.Bind(
            "Photon",
            "AppIdRealtime",
            string.Empty,
            "Custom Photon PUN application ID.");

        AppIdVoice = Config.Bind(
            "Photon",
            "AppIdVoice",
            string.Empty,
            "Custom Photon Voice application ID.");
    }

    private void StartDirectHost()
    {
        if (!CanStartDirectConnection())
        {
            return;
        }

        string roomName = NormalizeRoomName(
            _roomName.Value);

        var connectionService =
            GameHandler.GetService<ConnectionService>();

        HostState hostState =
            connectionService.StateMachine
                .SwitchState<HostState>();

        hostState.RoomName = roomName;

        Logger.LogInfo(
            $"Starting direct host: " +
            $"room={roomName}; " +
            $"region={PhotonNetwork.CloudRegion}");

        LoadAirport();
    }

    private void StartDirectJoin()
    {
        if (!CanStartDirectConnection())
        {
            return;
        }

        string roomName = NormalizeRoomName(
            _roomName.Value);

        string region =
            _region.Value.Trim().ToLowerInvariant();

        var connectionService =
            GameHandler.GetService<ConnectionService>();

        JoinSpecificRoomState joinState =
            connectionService.StateMachine
                .SwitchState<JoinSpecificRoomState>();

        joinState.RoomName = roomName;
        joinState.RegionToJoin = region;

        Logger.LogInfo(
            $"Starting direct join: " +
            $"room={roomName}; " +
            $"region={region}; " +
            $"currentRegion={PhotonNetwork.CloudRegion}");

        LoadAirport();
    }

    private bool CanStartDirectConnection()
    {
        if (!PhotonNetwork.IsConnectedAndReady)
        {
            Logger.LogError(
                "Photon is not connected and ready. " +
                $"Current state: " +
                $"{PhotonNetwork.NetworkClientState}");

            return false;
        }

        if (PhotonNetwork.InRoom)
        {
            Logger.LogError(
                "Already in a Photon room.");

            return false;
        }

        return true;
    }

    private static string NormalizeRoomName(
        string roomName)
    {
        string normalized =
            roomName.Trim().ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new InvalidOperationException(
                "The configured room name is empty.");
        }

        return normalized;
    }

    private static void LoadAirport()
    {
        LoadingScreenHandler loadingScreen =
            RetrievableResourceSingleton<
                LoadingScreenHandler>.Instance;

        loadingScreen.Load(
            LoadingScreen.LoadingScreenType.Basic,
            null,
            loadingScreen.LoadSceneProcess(
                "Airport",
                networked: false,
                yieldForCharacterSpawn: true));
    }

    internal static ConfigEntry<string> AppIdRealtime = null!;
    internal static ConfigEntry<string> AppIdVoice = null!;

    internal static void ApplyCustomPhotonSettings()
    {
        string realtimeId = AppIdRealtime.Value.Trim();
        string voiceId = AppIdVoice.Value.Trim();

        if (string.IsNullOrWhiteSpace(realtimeId))
        {
            Log.LogError(
                "Custom AppIdRealtime is empty. " +
                "Refusing to use PEAK's official Photon application.");

            return;
        }

        var settings =
            PhotonNetwork.PhotonServerSettings.AppSettings;

        settings.AppIdRealtime = realtimeId;

        if (!string.IsNullOrWhiteSpace(voiceId))
        {
            settings.AppIdVoice = voiceId;
        }

        Log.LogInfo(
            "Applied custom Photon settings: " +
            $"Realtime={Fingerprint(realtimeId)}; " +
            $"Voice={Fingerprint(voiceId)}");
    }

    private static string Fingerprint(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "<empty>";
        }

        using SHA256 sha256 = SHA256.Create();

        byte[] hash = sha256.ComputeHash(
            Encoding.UTF8.GetBytes(value));

        return BitConverter
            .ToString(hash)
            .Replace("-", string.Empty)[..10];
    }
}