using BepInEx;
using BepInEx.Logging;
using Photon.Pun;
using Photon.Realtime;
using ExitGames.Client.Photon;
using HarmonyLib;
using BepInEx.Configuration;
using Zorro.Core;
using System;
using UnityEngine;
using System.Text;
using System.Security.Cryptography;
using System.Reflection;
using System.Net;
using System.Net.Sockets;
using PeakLanMod.Lan.Services;
namespace PeakLanMod;

// Here are some basic resources on code style and naming conventions to help
// you in your first CSharp plugin!
// https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions
// https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/identifier-names
// https://learn.microsoft.com/en-us/dotnet/standard/design-guidelines/names-of-namespaces

// The BepInAutoPlugin attribute comes from the Hamunii.BepInEx.AutoPlugin
// NuGet package, and it will generate the BepInPlugin attribute for you!
// For more info, see https://github.com/Hamunii/BepInEx.AutoPlugin

/// <summary>
/// The BepInEx plugin class of PeakLanMod.
/// </summary>
[BepInPlugin(
    PluginGuid,
    PluginName,
    PluginVersion)]
public sealed class Plugin : BaseUnityPlugin
{
    internal enum PhotonConnectionMode
    {
        CustomCloud,
        LocalPhotonServer
    }

    public const string PluginGuid = "BadHorse.PeakLanMod";
    public const string PluginName = "PEAK LAN Mod";
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

        Logger.LogInfo("PEAK LAN Mod loaded.");
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
            "badhorse-lan-mod-room_" + Guid.NewGuid().ToString("N")[..8],
            "Host room name.");

        _region = Config.Bind(
            "Direct Connect",
            "Region",
            "",
            "Photon Cloud region. Leave blank for local server mode.");

        _hostKey = Config.Bind(
            "Direct Connect",
            "HostKey",
            new KeyboardShortcut(KeyCode.F6),
            "Start direct host. Testing parameter.");

        _joinKey = Config.Bind(
            "Direct Connect",
            "JoinKey",
            new KeyboardShortcut(KeyCode.F7),
            "Start direct join. Testing parameter.");

        PhotonMode = Config.Bind(
            "Photon",
            "Mode",
            PhotonConnectionMode.LocalPhotonServer,
            "Photon endpoint mode: CustomCloud or LocalPhotonServer (default).");

        AppIdRealtime = Config.Bind(
            "Photon",
            "AppIdRealtime",
            string.Empty,
            "Custom Photon PUN application ID. Photon Cloud only.");

        AppIdVoice = Config.Bind(
            "Photon",
            "AppIdVoice",
            string.Empty,
            "Custom Photon Voice application ID. Photon Cloud only.");

        LocalServerAddress = Config.Bind(
            "Photon",
            "LocalServerAddress",
            "127.0.0.1",
            "Local Photon Server hostname or IP. Swap with LAN host address.");

        LocalServerPort = Config.Bind(
            "Photon",
            "LocalServerPort",
            5058,
            "Port of Local Luxon Name Server UDP/TCP port.");

        LocalServerProtocol = Config.Bind(
            "Photon",
            "LocalServerProtocol",
            ConnectionProtocol.Udp,
            "Photon transport protocol for local server mode.");

        ShowLocalServerStatusUi = Config.Bind(
            "Photon",
            "ShowLocalServerStatusUI",
            true,
            "Show in-game local server reachability notifications in LocalPhotonServer mode.");

        StatusUiMinIntervalSeconds = Config.Bind(
            "Photon",
            "StatusUIMinIntervalSeconds",
            5,
            "Minimum seconds between local server status notifications.");

        ShowStatusOverlayFallback = Config.Bind(
            "Photon",
            "ShowStatusOverlayFallback",
            true,
            "Show a simple on-screen status overlay when scene UI notifications are unavailable.");

        AutoDetectHostLanIpv4 = Config.Bind(
            "LanWorkflow",
            "AutoDetectHostIPv4",
            false,
            "Auto-detect host LAN IPv4 during direct host in LocalPhotonServer mode.");

        PreferredHostIpv4 = Config.Bind(
            "LanWorkflow",
            "PreferredHostIPv4",
            string.Empty,
            "Optional manual host LAN IPv4 override. When set, this value is used instead of interface auto-detection.");

        AllowedHostInterfaces = Config.Bind(
            "LanWorkflow",
            "AllowedHostInterfaces",
            string.Empty,
            "Optional CSV interface filters (name/description/id contains match) for host LAN IPv4 auto-detection.");
    }

    private void OnGUI()
    {
        if (!IsLocalPhotonServerMode)
        {
            return;
        }

        if (!ShowStatusOverlayFallback.Value)
        {
            return;
        }

        if (PhotonNetwork.InLobby || PhotonNetwork.InRoom)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(_overlayStatusMessage))
        {
            return;
        }

        var rect = new Rect(
            16f,
            16f,
            980f,
            32f);

        GUI.Label(
            rect,
            _overlayStatusMessage);
    }

    private void StartDirectHost()
    {
        ApplyHostLanIpv4Selection();

        EnsureOnlineModeForDirectConnect("StartDirectHost");

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

    private static void ApplyHostLanIpv4Selection()
    {
        if (!IsLocalPhotonServerMode)
        {
            return;
        }

        if (!AutoDetectHostLanIpv4.Value)
        {
            return;
        }

        string preferredHostIpv4 =
            PreferredHostIpv4.Value.Trim();

        if (!LanEndpointResolver.TryResolveHostLanIpv4(
                preferredHostIpv4,
                AllowedHostInterfaces.Value,
                out string selectedIpv4,
                out string reason))
        {
            Log.LogWarning(
                "Host LAN IPv4 selection failed. " +
                $"Reason={reason}; " +
                $"KeepingLocalServerAddress={SanitizeEndpointForLog(LocalServerAddress.Value)}");

            return;
        }

        string previousAddress =
            LocalServerAddress.Value.Trim();

        if (string.Equals(
                previousAddress,
                selectedIpv4,
                StringComparison.OrdinalIgnoreCase))
        {
            Log.LogInfo(
                "Host LAN IPv4 selection kept existing LocalServerAddress. " +
                $"Selected={SanitizeEndpointForLog(selectedIpv4)}; " +
                $"SelectionReason={reason}");

            return;
        }

        LocalServerAddress.Value = selectedIpv4;

        Log.LogInfo(
            "Host LAN IPv4 selection updated LocalServerAddress. " +
            $"Previous={SanitizeEndpointForLog(previousAddress)}; " +
            $"Selected={SanitizeEndpointForLog(selectedIpv4)}; " +
            $"SelectedFingerprint={Fingerprint(selectedIpv4)}; " +
            $"SelectionReason={reason}");
    }

    private void StartDirectJoin()
    {
        EnsureOnlineModeForDirectConnect("StartDirectJoin");

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

            if (!PhotonNetwork.IsConnected
                && PhotonNetwork.NetworkClientState == ClientState.Disconnected)
            {
                Logger.LogInfo(
                    "Attempting Photon reconnect via NetworkingUtilities.ConnectToNetwork(). " +
                    "Press the host/join key again after connected-to-master.");

                Peak.Network.NetworkingUtilities.ConnectToNetwork();
            }

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

    private static void EnsureOnlineModeForDirectConnect(
        string source)
    {
        if (!PhotonNetwork.OfflineMode)
        {
            Log.LogInfo(
                $"{source}: OfflineMode is false.");

            return;
        }

        Log.LogWarning(
            $"{source}: OfflineMode was true before direct connect. " +
            "Forcing OfflineMode=false.");

        PhotonNetwork.OfflineMode = false;

        Log.LogInfo(
            $"{source}: OfflineMode after force={PhotonNetwork.OfflineMode}.");
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

    private static string SanitizeEndpointForLog(
        string endpoint)
    {
        string trimmed = endpoint.Trim();

        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return "<empty>";
        }

        if (!IPAddress.TryParse(trimmed, out IPAddress address))
        {
            return $"<fingerprint:{Fingerprint(trimmed)}>";
        }

        if (address.AddressFamily != AddressFamily.InterNetwork)
        {
            return "<non-ipv4>";
        }

        byte[] bytes = address.GetAddressBytes();

        return $"{bytes[0]}.{bytes[1]}.{bytes[2]}.x";
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

    internal static ConfigEntry<PhotonConnectionMode> PhotonMode = null!;
    internal static ConfigEntry<string> AppIdRealtime = null!;
    internal static ConfigEntry<string> AppIdVoice = null!;
    internal static ConfigEntry<string> LocalServerAddress = null!;
    internal static ConfigEntry<int> LocalServerPort = null!;
    internal static ConfigEntry<ConnectionProtocol> LocalServerProtocol = null!;
    internal static ConfigEntry<bool> ShowLocalServerStatusUi = null!;
    internal static ConfigEntry<int> StatusUiMinIntervalSeconds = null!;
    internal static ConfigEntry<bool> ShowStatusOverlayFallback = null!;
    internal static ConfigEntry<bool> AutoDetectHostLanIpv4 = null!;
    internal static ConfigEntry<string> PreferredHostIpv4 = null!;
    internal static ConfigEntry<string> AllowedHostInterfaces = null!;

    private static float _lastStatusUiAt = -999f;
    private static string _overlayStatusMessage = string.Empty;
    private static bool _uiTypeMissingLogged;
    private static bool _uiMethodMissingLogged;
    private static bool _fallbackTypeMissingLogged;
    private static bool _fallbackMethodMissingLogged;
    private static string _lastUiUnavailableScene = string.Empty;
    private static Type? _notificationsType;
    private static MethodInfo? _addNotificationMethod;
    private static Type? _playerConnectionLogType;
    private static MethodInfo? _addConnectionLogMessageMethod;

    internal static bool IsLocalPhotonServerMode =>
        PhotonMode.Value == PhotonConnectionMode.LocalPhotonServer;

    internal static void ApplyConfiguredPhotonSettings()
    {
        PhotonConnectionMode mode = PhotonMode.Value;

        var settings = PhotonNetwork.PhotonServerSettings.AppSettings;

        switch (mode)
        {
            case PhotonConnectionMode.CustomCloud:
                ApplyCustomCloudSettings(settings);
                return;

            case PhotonConnectionMode.LocalPhotonServer:
                ApplyLocalServerSettings(settings);
                return;

            default:
                Log.LogError(
                    $"Unknown Photon mode '{mode}'. " +
                    "Falling back to CustomCloud.");
                ApplyCustomCloudSettings(settings);
                return;
        }
    }

    internal static void NotifyLocalServerDetected()
    {
        if (!IsLocalPhotonServerMode)
        {
            return;
        }

        ShowLocalServerStatusNotification(
            $"Local server detected at {GetConfiguredLocalEndpoint()}.");
    }

    internal static void NotifyLocalServerNotDetected(
        string reason)
    {
        if (!IsLocalPhotonServerMode)
        {
            return;
        }

        ShowLocalServerStatusNotification(
            $"Local server not detected at {GetConfiguredLocalEndpoint()}: {reason}");
    }

    private static string GetConfiguredLocalEndpoint()
    {
        string address = LocalServerAddress.Value.Trim();
        int port = LocalServerPort.Value;
        ConnectionProtocol protocol = LocalServerProtocol.Value;

        return $"{address}:{port} ({protocol})";
    }

    private static void ShowLocalServerStatusNotification(
        string message)
    {
        if (!ShowLocalServerStatusUi.Value)
        {
            return;
        }

        int minIntervalSeconds = Math.Max(
            0,
            StatusUiMinIntervalSeconds.Value);

        float now = Time.realtimeSinceStartup;

        if (now - _lastStatusUiAt < minIntervalSeconds)
        {
            return;
        }

        if (!TryResolveNotificationsUi(
                out UnityEngine.Object? instance,
                out MethodInfo? addNotification))
        {
            if (!TryResolvePlayerConnectionLogUi(
                    out instance,
                    out addNotification))
            {
                LogUiUnavailableOncePerScene();
                ShowOverlayStatusFallback(message);
                return;
            }
        }

        if (instance is null || addNotification is null)
        {
            return;
        }

        try
        {
            addNotification.Invoke(instance, [message]);
            _lastStatusUiAt = now;

            Log.LogInfo(
                $"UI notification: {message}");

            ShowOverlayStatusFallback(message);
        }
        catch (Exception ex)
        {
            Log.LogWarning(
                "Failed to send UI notification. " +
                $"Error={ex.GetType().Name}; " +
                $"Message={ex.Message}");

            ShowOverlayStatusFallback(message);
        }
    }

    private static void ShowOverlayStatusFallback(
        string message)
    {
        if (!ShowStatusOverlayFallback.Value)
        {
            return;
        }

        _overlayStatusMessage = $"PEAK LAN: {message}";
    }

    private static bool TryResolveNotificationsUi(
        out UnityEngine.Object? instance,
        out MethodInfo? addNotification)
    {
        const string typeName = "UI_Notifications, Assembly-CSharp";

        if (_notificationsType is null)
        {
            _notificationsType = Type.GetType(typeName);

            if (_notificationsType is null)
            {
                if (!_uiTypeMissingLogged)
                {
                    Log.LogWarning(
                        "UI_Notifications type is not available. " +
                        "Cannot show local server status in UI.");

                    _uiTypeMissingLogged = true;
                }

                instance = null;
                addNotification = null;

                return false;
            }
        }

        Type notificationsType = _notificationsType;

        addNotification = _addNotificationMethod;

        if (addNotification is null)
        {
            addNotification = notificationsType.GetMethod(
                "AddNotification",
                BindingFlags.Instance | BindingFlags.Public,
                null,
                [typeof(string)],
                null);

            if (addNotification is null)
            {
                if (!_uiMethodMissingLogged)
                {
                    Log.LogWarning(
                        "UI_Notifications.AddNotification(string) not found. " +
                        "Cannot show local server status in UI.");

                    _uiMethodMissingLogged = true;
                }

                instance = null;

                return false;
            }

            _addNotificationMethod = addNotification;
        }

        instance = UnityEngine.Object.FindFirstObjectByType(
            notificationsType);

        if (instance is null)
        {
            return false;
        }

        return true;
    }

    private static bool TryResolvePlayerConnectionLogUi(
        out UnityEngine.Object? instance,
        out MethodInfo? addMessage)
    {
        const string typeName = "PlayerConnectionLog, Assembly-CSharp";

        if (_playerConnectionLogType is null)
        {
            _playerConnectionLogType = Type.GetType(typeName);

            if (_playerConnectionLogType is null)
            {
                if (!_fallbackTypeMissingLogged)
                {
                    Log.LogWarning(
                        "PlayerConnectionLog type is not available. " +
                        "No fallback in-game status UI sink found.");

                    _fallbackTypeMissingLogged = true;
                }

                instance = null;
                addMessage = null;
                return false;
            }
        }

        Type fallbackType = _playerConnectionLogType;
        addMessage = _addConnectionLogMessageMethod;

        if (addMessage is null)
        {
            addMessage = fallbackType.GetMethod(
                "AddMessage",
                BindingFlags.Instance | BindingFlags.Public,
                null,
                [typeof(string)],
                null);

            if (addMessage is null)
            {
                if (!_fallbackMethodMissingLogged)
                {
                    Log.LogWarning(
                        "PlayerConnectionLog.AddMessage(string) not found. " +
                        "No fallback in-game status UI sink found.");

                    _fallbackMethodMissingLogged = true;
                }

                instance = null;
                return false;
            }

            _addConnectionLogMessageMethod = addMessage;
        }

        instance = UnityEngine.Object.FindFirstObjectByType(
            fallbackType);

        return instance is not null;
    }

    private static void LogUiUnavailableOncePerScene()
    {
        string sceneName = UnityEngine.SceneManagement
            .SceneManager
            .GetActiveScene()
            .name;

        if (string.Equals(
                _lastUiUnavailableScene,
                sceneName,
                StringComparison.Ordinal))
        {
            return;
        }

        _lastUiUnavailableScene = sceneName;

        Log.LogInfo(
            "No in-game status UI sink found in current scene. " +
            $"Scene={sceneName}");
    }

    private static void ApplyCustomCloudSettings(
        AppSettings settings)
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

        settings.UseNameServer = true;
        settings.Server = string.Empty;
        settings.Port = 0;
        settings.FixedRegion = string.Empty;

        settings.AppIdRealtime = realtimeId;

        if (!string.IsNullOrWhiteSpace(voiceId))
        {
            settings.AppIdVoice = voiceId;
        }

        Log.LogInfo(
            "Applied Photon mode CustomCloud: " +
            $"UseNameServer={settings.UseNameServer}; " +
            $"Realtime={Fingerprint(realtimeId)}; " +
            $"Voice={Fingerprint(voiceId)}");
    }

    private static void ApplyLocalServerSettings(
        AppSettings settings)
    {
        string serverAddress = LocalServerAddress.Value.Trim();

        if (string.IsNullOrWhiteSpace(serverAddress))
        {
            Log.LogError(
                "LocalServerAddress is empty. " +
                "Cannot apply LocalPhotonServer mode.");

            return;
        }

        int configuredPort = LocalServerPort.Value;

        if (configuredPort is < 1 or > 65535)
        {
            Log.LogError(
                $"LocalServerPort '{configuredPort}' is invalid. " +
                "Expected range 1-65535.");

            return;
        }

        settings.UseNameServer = true;
        settings.Server = serverAddress;
        settings.Port = (ushort)configuredPort;
        settings.Protocol = LocalServerProtocol.Value;
        settings.FixedRegion = string.Empty;

        Log.LogInfo(
            "Applied Photon mode LocalPhotonServer: " +
            $"Server={serverAddress}; " +
            $"Port={settings.Port}; " +
            $"Protocol={settings.Protocol}; " +
            $"UseNameServer={settings.UseNameServer}");
    }

    internal static string Fingerprint(string value)
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