using BepInEx;
using BepInEx.Logging;
using Photon.Pun;
using Photon.Realtime;
using ExitGames.Client.Photon;
using HarmonyLib;
using BepInEx.Configuration;
using Zorro.Core;
using System;
using System.Collections.Generic;
using UnityEngine;
using System.Text;
using System.Security.Cryptography;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using PeakLanMod.Lan.Discovery;
using PeakLanMod.Lan.Model;
using PeakLanMod.Lan.State;
using PeakLanMod.Lan.Services;
using PeakLanMod.Lan.UI;
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
        LocalServer,

        [Obsolete("Use LocalServer.")]
        LocalPhotonServer = LocalServer
    }

    public const string PluginGuid = "BadHorse.PeakLanMod";
    public const string PluginName = "PEAK LAN Mod";
    public const string PluginVersion = "0.2.0";

    internal static ManualLogSource Log { get; private set; } = null!;

    private Harmony? _harmony;
    private ClientState? _previousState;
    private bool _pendingDirectHostStart;
    private bool _pendingDirectHostConnectRequested;
    private bool _queuedHostPreflightCompleted;
    private DateTime _queuedHostReadinessStartedAtUtc;
    private int _queuedHostReadinessAttempts;
    private static readonly LanConnectionStateStore LanDiscoveryStateStore = new();
    private static readonly UdpLanDiscoveryListener LanDiscoveryListener =
        new(LanDiscoveryStateStore);
    private static readonly UdpLanDiscoveryBroadcaster LanDiscoveryBroadcaster = new();
    private static readonly LanDiscoveredSessionsViewModel LanDiscoveredSessionsViewModel = new();
    private static readonly LanStatusPresenterBridge LanStatusPresenterBridge = new();
    private static readonly string LanDiscoveryServerInstanceId =
        Guid.NewGuid().ToString("N");
    private static int _lastLanDiscoverySnapshotCount = -1;
    private static bool? _lastLanDiscoveryListenerRunning;
    private static bool? _lastLanDiscoveryBroadcasterRunning;
    private float _lastNotReadyLogAt = -999f;
    private float _lastReconnectAttemptAt = -999f;

    private void Awake()
    {
        Log = Logger;

        MigrateLegacyPhotonModeNameInConfig();

        ConfigureDirectConnect();
        SyncLanDiscoveryRuntime("Awake");

        gameObject.AddComponent<PhotonCallbackProbe>();

        _harmony = new Harmony(PluginGuid);
        _harmony.PatchAll();

        Logger.LogInfo("PEAK LAN Mod loaded.");
        DumpPhotonSettings("Plugin.Awake");
    }
    private void Update()
    {
        LogPhotonStateChanges();
        SyncLanDiscoveryRuntime("Update");

        if (!DirectConnectEnabled.Value)
        {
            return;
        }

        if (_hostKey.Value.IsDown())
        {
            Logger.LogInfo("Host key pressed.");

            RequestDirectHostStart("HostKey");
        }

        if (_joinKey.Value.IsDown())
        {
            Logger.LogInfo("Join key pressed.");
            StartDirectJoin();
        }

        if (AutoRetryDirectHostUntilReady.Value)
        {
            TryProcessQueuedDirectHostStart("Update");
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

        LanDiscoveryStateStore.SetConnectionPhase(currentState.ToString());

        _previousState = currentState;
    }

    private void OnDestroy()
    {
        ShutdownLanDiscoveryRuntime("Plugin.OnDestroy");
        StopOwnedLocalServerProcessOnExit("Plugin.OnDestroy");
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
    private ConfigEntry<bool> _enableLanUiActions = null!;
    private ConfigEntry<int> _lanUiOverlayMaxSessions = null!;

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
            PhotonConnectionMode.LocalServer,
            "Photon endpoint mode: CustomCloud or LocalServer (default). Legacy LocalPhotonServer is auto-migrated.");

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

        AutoDetectHostLanIpv4 = Config.Bind(
            "LanWorkflow",
            "AutoDetectHostIPv4",
            false,
            "Auto-detect host LAN IPv4 during direct host in LocalServer mode.");

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

        AutoUpdateLuxonConfigOnHost = Config.Bind(
            "LanWorkflow",
            "AutoUpdateLuxonConfigOnHost",
            false,
            "Automatically rewrite Luxon external_address values during direct host in LocalServer mode.");

        LuxonConfigPath = Config.Bind(
            "LanWorkflow",
            "LuxonConfigPath",
            "server/config.yml",
            "Relative or absolute path to Luxon config.yml used by host-side external_address automation.");

        AutoStartLocalServerOnHost = Config.Bind(
            "LanWorkflow",
            "AutoStartLocalServerOnHost",
            true,
            "Start local server executable during direct host when no matching process is already running.");

        LocalServerExecutablePath = Config.Bind(
            "LanWorkflow",
            "LocalServerExecutablePath",
            "server/luxon_server.msvc.release.exe",
            "Relative or absolute path to the local server executable for optional host auto-start.");

        LocalServerWorkingDirectory = Config.Bind(
            "LanWorkflow",
            "LocalServerWorkingDirectory",
            string.Empty,
            "Working directory used when launching the local server executable. Leave empty to use executable directory.");

        LocalServerStartArguments = Config.Bind(
            "LanWorkflow",
            "LocalServerStartArguments",
            "config.yml",
            "Arguments passed to the local server executable when host auto-start is enabled.");

        AutoStopOwnedLocalServerOnExit = Config.Bind(
            "LanWorkflow",
            "AutoStopOwnedLocalServerOnExit",
            true,
            "Stop only plugin-owned local server process on plugin unload/game exit.");

        ForceKillOwnedLocalServerOnExit = Config.Bind(
            "LanWorkflow",
            "ForceKillOwnedLocalServerOnExit",
            true,
            "Force-kill plugin-owned local server process on exit when graceful stop times out.");

        OwnedLocalServerStopTimeoutMs = Config.Bind(
            "LanWorkflow",
            "OwnedLocalServerStopTimeoutMs",
            2000,
            "Timeout in milliseconds for graceful stop of plugin-owned local server process.");

        AutoRetryDirectHostUntilReady = Config.Bind(
            "LanWorkflow",
            "AutoRetryDirectHostUntilReady",
            true,
            "Queue host intent on HostKey and auto-complete when Photon becomes connected and ready.");

        EnableLocalServerReadinessCheck = Config.Bind(
            "LanWorkflow",
            "EnableLocalServerReadinessCheck",
            true,
            "Wait for local NameServer readiness before direct host/join connect attempts in LocalServer mode.");

        LocalServerReadinessTimeoutMs = Config.Bind(
            "LanWorkflow",
            "ReadinessTimeoutMs",
            5000,
            "Maximum milliseconds to wait for local NameServer readiness before connect attempts.");

        LocalServerReadinessPollIntervalMs = Config.Bind(
            "LanWorkflow",
            "ReadinessPollIntervalMs",
            250,
            "Milliseconds between local NameServer readiness probe attempts.");

        LanDiscoveryEnabled = Config.Bind(
            "LanWorkflow",
            "DiscoveryEnabled",
            false,
            "Enable UDP LAN session discovery listener and host announcement broadcast in LocalServer mode.");

        LanDiscoveryUdpPort = Config.Bind(
            "LanWorkflow",
            "DiscoveryUdpPort",
            47777,
            "UDP port used for LAN discovery announcements.");

        LanDiscoveryBroadcastIntervalMs = Config.Bind(
            "LanWorkflow",
            "DiscoveryBroadcastIntervalMs",
            1000,
            "Interval in milliseconds for host discovery announcements.");

        LanDiscoveryEntryTtlMs = Config.Bind(
            "LanWorkflow",
            "DiscoveryEntryTtlMs",
            5000,
            "Milliseconds before an unrefreshed discovered session is evicted.");

        LanDiscoveryProtocolVersion = Config.Bind(
            "LanWorkflow",
            "ProtocolVersion",
            "1",
            "Discovery protocol version string advertised and required for session compatibility.");

        LanDiscoveryRequireVersionMatch = Config.Bind(
            "LanWorkflow",
            "RequireVersionMatch",
            true,
            "Require exact game/mod version match for discovery session compatibility.");

        _enableLanUiActions = Config.Bind(
            "LanWorkflow",
            "EnableLanUiActions",
            false,
            "Enable M6 LAN UI actions and discovered-session overlay in LocalServer mode.");

        _lanUiOverlayMaxSessions = Config.Bind(
            "LanWorkflow",
            "LanUiOverlayMaxSessions",
            6,
            "Maximum discovered sessions rendered in M6 overlay.");
    }

    private void SyncLanDiscoveryRuntime(
        string source)
    {
        if (!IsLocalServerMode || !LanDiscoveryEnabled.Value)
        {
            if (LanDiscoveryBroadcaster.IsRunning)
            {
                LanDiscoveryBroadcaster.Stop($"{source}: mode/config disabled");
            }

            if (LanDiscoveryListener.IsRunning)
            {
                LanDiscoveryListener.Stop($"{source}: mode/config disabled");
            }

            return;
        }

        if (!LanDiscoveryListener.IsRunning)
        {
            if (!LanDiscoveryListener.TryStart(
                    LanDiscoveryUdpPort.Value,
                    LanDiscoveryEntryTtlMs.Value,
                    EvaluateLanSessionCompatibility,
                    out string listenerMessage))
            {
                Log.LogError(
                    $"{source}: failed to start LAN discovery listener. " +
                    $"Reason={listenerMessage}");
            }
            else
            {
                Log.LogInfo(
                    $"{source}: LAN discovery listener active. " +
                    $"Port={LanDiscoveryUdpPort.Value}; " +
                    $"TtlMs={LanDiscoveryEntryTtlMs.Value}; " +
                    $"Message={listenerMessage}");
            }
        }

        int sessionCount = LanDiscoveryListener.GetSnapshot().Length;
        bool listenerRunning = LanDiscoveryListener.IsRunning;
        bool broadcasterRunning = LanDiscoveryBroadcaster.IsRunning;

        bool changed = sessionCount != _lastLanDiscoverySnapshotCount
            || listenerRunning != _lastLanDiscoveryListenerRunning
            || broadcasterRunning != _lastLanDiscoveryBroadcasterRunning;

        if (changed)
        {
            _lastLanDiscoverySnapshotCount = sessionCount;
            _lastLanDiscoveryListenerRunning = listenerRunning;
            _lastLanDiscoveryBroadcasterRunning = broadcasterRunning;

            Log.LogInfo(
                $"{source}: LAN discovery snapshot count={sessionCount}; " +
                $"ListenerRunning={listenerRunning}; " +
                $"BroadcasterRunning={broadcasterRunning}");
        }
    }

    internal static void RefreshLanDiscoveryBroadcast(
        string source)
    {
        if (!IsLocalServerMode || !LanDiscoveryEnabled.Value)
        {
            return;
        }

        if (!PhotonNetwork.InRoom || !PhotonNetwork.IsMasterClient)
        {
            if (LanDiscoveryBroadcaster.IsRunning)
            {
                LanDiscoveryBroadcaster.Stop($"{source}: not in master room");
            }

            return;
        }

        if (!LanDiscoveryBroadcaster.TryStart(
                LanDiscoveryUdpPort.Value,
                LanDiscoveryBroadcastIntervalMs.Value,
                BuildLanDiscoveryAnnouncement,
                out string startMessage))
        {
            Log.LogError(
                $"{source}: failed to start LAN discovery broadcaster. " +
                $"Reason={startMessage}");

            return;
        }

        Log.LogInfo(
            $"{source}: LAN discovery broadcaster active. " +
            $"Port={LanDiscoveryUdpPort.Value}; " +
            $"IntervalMs={LanDiscoveryBroadcastIntervalMs.Value}; " +
            $"Message={startMessage}; " +
            $"Room={PhotonNetwork.CurrentRoom?.Name ?? "<none>"}");
    }

    internal static void StopLanDiscoveryBroadcast(
        string source)
    {
        if (LanDiscoveryBroadcaster.IsRunning)
        {
            LanDiscoveryBroadcaster.Stop(source);
        }
    }

    private static void ShutdownLanDiscoveryRuntime(
        string source)
    {
        StopLanDiscoveryBroadcast($"{source}: shutdown");

        if (LanDiscoveryListener.IsRunning)
        {
            LanDiscoveryListener.Stop($"{source}: shutdown");
        }
    }

    private static LanSessionCompatibility EvaluateLanSessionCompatibility(
        LanDiscoveryAnnouncement announcement)
    {
        string expectedProtocol =
            LanDiscoveryProtocolVersion.Value.Trim();

        if (!string.Equals(
                announcement.ProtocolVersion,
                expectedProtocol,
                StringComparison.Ordinal))
        {
            return new LanSessionCompatibility(
                isCompatible: false,
                reason: "IncompatibleProtocolVersion");
        }

        if (!LanDiscoveryRequireVersionMatch.Value)
        {
            return LanSessionCompatibility.Compatible;
        }

        string gameVersion = Application.version ?? string.Empty;

        if (!string.Equals(
                announcement.GameVersion,
                gameVersion,
                StringComparison.Ordinal))
        {
            return new LanSessionCompatibility(
                isCompatible: false,
                reason: "IncompatibleGameVersion");
        }

        if (!string.Equals(
                announcement.ModVersion,
                PluginVersion,
                StringComparison.Ordinal))
        {
            return new LanSessionCompatibility(
                isCompatible: false,
                reason: "IncompatibleModVersion");
        }

        return LanSessionCompatibility.Compatible;
    }

    private static LanDiscoveryAnnouncement BuildLanDiscoveryAnnouncement()
    {
        string roomName = PhotonNetwork.CurrentRoom?.Name
            ?? string.Empty;

        string scene = UnityEngine.SceneManagement.SceneManager
            .GetActiveScene()
            .name;

        return new LanDiscoveryAnnouncement(
            type: LanDiscoveryMessageCodec.AnnouncementType,
            schemaVersion: LanDiscoveryMessageCodec.SchemaVersionV1,
            protocolVersion: LanDiscoveryProtocolVersion.Value.Trim(),
            gameVersion: Application.version ?? string.Empty,
            modVersion: PluginVersion,
            roomName: roomName,
            hostDisplayName: PhotonNetwork.NickName ?? string.Empty,
            nameServerAddress: LocalServerAddress.Value.Trim(),
            nameServerPort: LocalServerPort.Value,
            transport: LocalServerProtocol.Value.ToString(),
            scene: scene,
            serverInstanceId: LanDiscoveryServerInstanceId,
            sentAtUtc: DateTime.UtcNow);
    }

    private void OnGUI()
    {
        if (!IsLocalServerMode)
        {
            return;
        }

        if (_enableLanUiActions.Value
            && LanDiscoveryEnabled.Value
            && IsMainMenuScene())
        {
            RenderLanUiOverlay();
        }

    }

    private static bool IsMainMenuScene()
    {
        UnityEngine.SceneManagement.Scene scene =
            UnityEngine.SceneManagement
                .SceneManager
                .GetActiveScene();

        if (!scene.isLoaded)
        {
            return false;
        }

        return string.Equals(
            scene.name,
            "Title",
            StringComparison.OrdinalIgnoreCase);
    }

    private void RefreshLanUiSessions()
    {
        LanSessionInfo[] snapshot = LanDiscoveryListener.GetSnapshot();
        LanDiscoveredSessionsViewModel.UpdateSessions(snapshot);
    }

    private void RequestDirectHostStart(
        string source)
    {
        if (!AutoRetryDirectHostUntilReady.Value)
        {
            _ = StartDirectHostOnce();
            return;
        }

        QueueDirectHostStart();
        TryProcessQueuedDirectHostStart(source);
    }

    private void TryJoinSelectedLanSession()
    {
        LanSessionInfo? selected =
            LanDiscoveredSessionsViewModel.GetSelectedSessionOrNull();

        if (selected is null)
        {
            Log.LogInfo("LAN UI join-selected requested, but no session is selected.");
            return;
        }

        if (!selected.IsCompatible)
        {
            Log.LogWarning(
                "LAN UI join-selected blocked due to incompatible session. " +
                $"Room={selected.RoomName}; " +
                $"Reason={selected.IncompatibilityReason}");
            return;
        }

        if (!TryResolveDiscoverySessionTransport(
                selected.Transport,
                out ConnectionProtocol protocol))
        {
            Log.LogWarning(
                "LAN UI join-selected ignored unsupported transport. " +
                $"Transport={selected.Transport}; " +
                $"Room={selected.RoomName}");
            return;
        }

        _roomName.Value = selected.RoomName;
        LocalServerAddress.Value = selected.NameServerAddress;
        LocalServerPort.Value = selected.NameServerPort;
        LocalServerProtocol.Value = protocol;

        Log.LogInfo(
            "LAN UI join-selected applied discovered session settings. " +
            $"Room={selected.RoomName}; " +
            $"Endpoint={SanitizeEndpointForLog(selected.NameServerAddress)}:{selected.NameServerPort}; " +
            $"Protocol={protocol}");

        StartDirectJoin();
    }

    private static bool TryResolveDiscoverySessionTransport(
        string transport,
        out ConnectionProtocol protocol)
    {
        return Enum.TryParse(
            transport,
            ignoreCase: true,
            out protocol);
    }

    private void RenderLanUiOverlay()
    {
        RefreshLanUiSessions();

        IReadOnlyList<LanSessionInfo> sessions = LanDiscoveredSessionsViewModel.Sessions;
        int selectedIndex = LanDiscoveredSessionsViewModel.SelectedIndex;
        (string phase, DateTime _) = LanDiscoveryStateStore.GetConnectionPhaseSnapshot();

        string summaryLine = LanStatusPresenterBridge.BuildSummaryLine(
            phase,
            GetConfiguredLocalEndpoint(),
            sessions.Count);

        int visibleRows = Math.Max(1, Math.Min(_lanUiOverlayMaxSessions.Value, sessions.Count));
        float panelHeight = 110f + (visibleRows * 24f);
        var panelRect = new Rect(
            16f,
            56f,
            1100f,
            Mathf.Max(170f, panelHeight));

        GUI.Box(panelRect, "LAN Sessions");

        GUI.Label(
            new Rect(panelRect.x + 12f, panelRect.y + 24f, panelRect.width - 24f, 22f),
            summaryLine);

        if (GUI.Button(
                new Rect(panelRect.x + 12f, panelRect.y + 50f, 120f, 26f),
                "Host LAN"))
        {
            Log.LogInfo("LAN UI host button clicked.");
            RequestDirectHostStart("LanUiHostButton");
        }

        if (GUI.Button(
                new Rect(panelRect.x + 138f, panelRect.y + 50f, 120f, 26f),
                "Join Selected"))
        {
            Log.LogInfo("LAN UI join-selected button clicked.");
            TryJoinSelectedLanSession();
        }

        if (GUI.Button(
                new Rect(panelRect.x + 264f, panelRect.y + 50f, 110f, 26f),
                "Refresh"))
        {
            RefreshLanUiSessions();
            Log.LogInfo(
                $"LAN UI refresh clicked. SessionCount={LanDiscoveredSessionsViewModel.SessionCount}");
        }

        float rowY = panelRect.y + 82f;

        if (sessions.Count == 0)
        {
            GUI.Label(
                new Rect(panelRect.x + 12f, rowY, panelRect.width - 24f, 22f),
                "No discovered sessions yet. Keep host in-room and click Refresh.");
            return;
        }

        int renderCount = Math.Min(visibleRows, sessions.Count);

        for (int index = 0; index < renderCount; index++)
        {
            LanSessionInfo session = sessions[index];
            string rowLabel = LanStatusPresenterBridge.BuildSessionRowLabel(
                session,
                index == selectedIndex,
                index + 1);

            if (GUI.Button(
                    new Rect(panelRect.x + 12f, rowY + (index * 24f), panelRect.width - 24f, 22f),
                    rowLabel))
            {
                if (LanDiscoveredSessionsViewModel.TrySelectIndex(index))
                {
                    Log.LogInfo(
                        "LAN UI selected discovered session from list. " +
                        $"Room={session.RoomName}; " +
                        $"Endpoint={SanitizeEndpointForLog(session.NameServerAddress)}:{session.NameServerPort}; " +
                        $"Compatible={session.IsCompatible}; " +
                        $"Reason={session.IncompatibilityReason}");
                }
            }
        }

        if (sessions.Count > renderCount)
        {
            GUI.Label(
                new Rect(panelRect.x + 12f, rowY + (renderCount * 24f), panelRect.width - 24f, 20f),
                $"Showing first {renderCount} of {sessions.Count} sessions. Increase LanUiOverlayMaxSessions to show more.");
        }

        LanSessionInfo? selectedSession = LanDiscoveredSessionsViewModel.GetSelectedSessionOrNull();

        if (selectedSession is not null && !selectedSession.IsCompatible)
        {
            GUI.Label(
                new Rect(panelRect.x + 390f, panelRect.y + 50f, panelRect.width - 402f, 26f),
                $"Selected session blocked: {selectedSession.IncompatibilityReason}");
        }
    }

    private void QueueDirectHostStart()
    {
        _pendingDirectHostStart = true;
        _pendingDirectHostConnectRequested = false;
        _queuedHostPreflightCompleted = false;
        ResetQueuedHostReadinessWindow();

        Log.LogInfo(
            "Queued direct host start request. " +
            "Waiting for local server process and Photon ready state.");
    }

    private void TryProcessQueuedDirectHostStart(
        string source)
    {
        if (!_pendingDirectHostStart)
        {
            return;
        }

        if (PhotonNetwork.InRoom)
        {
            _pendingDirectHostStart = false;
            _queuedHostPreflightCompleted = false;
            ResetQueuedHostReadinessWindow();

            Log.LogInfo(
                $"{source}: queued host request cleared because client is already in a room.");

            return;
        }

        if (!StartDirectHostOnce())
        {
            return;
        }

        _pendingDirectHostStart = false;
        _pendingDirectHostConnectRequested = false;
        _queuedHostPreflightCompleted = false;
        ResetQueuedHostReadinessWindow();

        Log.LogInfo(
            $"{source}: queued direct host request completed.");
    }

    private bool StartDirectHostOnce()
    {
        bool queuedHostFlow =
            _pendingDirectHostStart
            && AutoRetryDirectHostUntilReady.Value;

        if (!queuedHostFlow || !_queuedHostPreflightCompleted)
        {
            ApplyHostLanIpv4Selection();
            ApplyHostLuxonConfigAutomation();

            if (!EnsureHostLocalServerProcess())
            {
                _pendingDirectHostStart = false;
                _queuedHostPreflightCompleted = false;
                ResetQueuedHostReadinessWindow();
                return false;
            }

            if (!EnsureLocalServerReadinessBeforeConnect(
                    source: "StartDirectHost",
                    queuedHostFlow))
            {
                _pendingDirectHostConnectRequested = false;
                return false;
            }

            if (queuedHostFlow)
            {
                _queuedHostPreflightCompleted = true;

                Log.LogInfo(
                    "StartDirectHost: queued host preflight completed. " +
                    "Waiting for Photon connected+ready before entering room flow.");
            }
        }

        EnsureOnlineModeForDirectConnect("StartDirectHost");

        if (!CanStartDirectConnection(ref _pendingDirectHostConnectRequested))
        {
            return false;
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
        return true;
    }

    private static bool EnsureHostLocalServerProcess()
    {
        if (!IsLocalServerMode)
        {
            return true;
        }

        if (!AutoStartLocalServerOnHost.Value)
        {
            return true;
        }

        string executablePath = LocalServerExecutablePath.Value.Trim();
        string workingDirectory = LocalServerWorkingDirectory.Value.Trim();
        string startArguments = LocalServerStartArguments.Value;

        if (!LuxonProcessController.TryEnsureRunning(
                executablePath,
            Paths.ConfigPath,
                workingDirectory,
                startArguments,
                out LuxonProcessEnsureResult result))
        {
            Log.LogError(
                "Local server host auto-start failed. " +
                $"Executable={result.ExecutablePathForLog}; " +
                $"WorkingDirectory={result.WorkingDirectoryForLog}; " +
                $"Reason={result.Message}");

            NotifyLocalServerNotDetected("auto-start failed");
            return false;
        }

        Log.LogInfo(
            "Local server host process check succeeded. " +
            $"Ownership={LuxonProcessController.OwnershipState}; " +
            $"StartedByPlugin={result.StartedByPlugin}; " +
            $"ExternalProcessDetected={result.ExternalProcessDetected}; " +
            $"Pid={result.ProcessId}; " +
            $"Executable={result.ExecutablePathForLog}; " +
            $"WorkingDirectory={result.WorkingDirectoryForLog}; " +
            $"Message={result.Message}");

        return true;
    }

    private static void StopOwnedLocalServerProcessOnExit(
        string source)
    {
        if (!IsLocalServerMode)
        {
            return;
        }

        if (!AutoStopOwnedLocalServerOnExit.Value)
        {
            Log.LogInfo(
                $"{source}: owned local server stop on exit is disabled.");
            return;
        }

        int timeoutMs = Math.Max(0, OwnedLocalServerStopTimeoutMs.Value);
        bool forceKill = ForceKillOwnedLocalServerOnExit.Value;

        if (LuxonProcessController.TryStopOwnedProcess(
                timeoutMs,
                forceKill,
                out string resultMessage))
        {
            Log.LogInfo(
                $"{source}: local server process stop succeeded. " +
                $"{resultMessage}");
            return;
        }

        Log.LogInfo(
            $"{source}: local server process stop skipped or incomplete. " +
            $"{resultMessage}; " +
            $"Ownership={LuxonProcessController.OwnershipState}");
    }

    private static void ApplyHostLanIpv4Selection()
    {
        if (!IsLocalServerMode)
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

    private static void ApplyHostLuxonConfigAutomation()
    {
        if (!IsLocalServerMode)
        {
            return;
        }

        if (!AutoUpdateLuxonConfigOnHost.Value)
        {
            return;
        }

        string endpointHost = LocalServerAddress.Value.Trim();
        string configPath = LuxonConfigPath.Value.Trim();

        if (!LuxonConfigManager.TryUpdateExternalAddresses(
                endpointHost,
                configPath,
                out LuxonConfigUpdateResult result))
        {
            Log.LogWarning(
                "Luxon config host automation failed. " +
                $"Host={SanitizeEndpointForLog(endpointHost)}; " +
                $"ConfigPath={result.ConfigPathForLog}; " +
                $"Reason={result.Message}");

            return;
        }

        Log.LogInfo(
            "Luxon config host automation succeeded. " +
            $"Host={SanitizeEndpointForLog(endpointHost)}; " +
            $"ConfigPath={result.ConfigPathForLog}; " +
            $"UpdatedEntries={result.UpdatedEntryCount}; " +
            $"MatchedEntries={result.MatchedEntryCount}; " +
            $"Changed={result.WasChanged}");
    }

    private void StartDirectJoin()
    {
        if (!EnsureLocalServerReadinessBeforeConnect(
                source: "StartDirectJoin",
                queuedHostFlow: false))
        {
            return;
        }

        EnsureOnlineModeForDirectConnect("StartDirectJoin");

        bool joinConnectRequested = false;

        if (!CanStartDirectConnection(ref joinConnectRequested))
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

    private bool EnsureLocalServerReadinessBeforeConnect(
        string source,
        bool queuedHostFlow)
    {
        if (!IsLocalServerMode)
        {
            return true;
        }

        if (!EnableLocalServerReadinessCheck.Value)
        {
            return true;
        }

        int timeoutMs = Math.Max(0, LocalServerReadinessTimeoutMs.Value);
        int pollIntervalMs = Math.Max(50, LocalServerReadinessPollIntervalMs.Value);

        string host = LocalServerAddress.Value.Trim();
        int port = LocalServerPort.Value;
        ConnectionProtocol protocol = LocalServerProtocol.Value;

        if (queuedHostFlow)
        {
            return EnsureQueuedHostReadinessBeforeConnect(
                source,
                host,
                port,
                protocol,
                timeoutMs,
                pollIntervalMs);
        }

        if (!LuxonReadinessProbe.TryWaitForNameServerReady(
                host,
                port,
                protocol,
                timeoutMs,
                pollIntervalMs,
                out LocalServerReadinessResult result))
        {
            Log.LogError(
                $"{source}: local NameServer readiness timed out. " +
                $"Endpoint={SanitizeEndpointForLog(host)}:{port}; " +
                $"Protocol={protocol}; " +
                $"ElapsedMs={result.ElapsedMilliseconds}; " +
                $"Attempts={result.AttemptCount}; " +
                $"LastFailure={result.LastFailureMessage}");

            NotifyLocalServerNotDetected("readiness timeout");
            return false;
        }

        Log.LogInfo(
            $"{source}: local NameServer readiness confirmed. " +
            $"Endpoint={SanitizeEndpointForLog(host)}:{port}; " +
            $"Protocol={protocol}; " +
            $"ElapsedMs={result.ElapsedMilliseconds}; " +
            $"Attempts={result.AttemptCount}; " +
            $"Message={result.SuccessMessage}");

        return true;
    }

    private bool EnsureQueuedHostReadinessBeforeConnect(
        string source,
        string host,
        int port,
        ConnectionProtocol protocol,
        int timeoutMs,
        int pollIntervalMs)
    {
        DateTime now = DateTime.UtcNow;

        if (_queuedHostReadinessStartedAtUtc == default)
        {
            _queuedHostReadinessStartedAtUtc = now;
            _queuedHostReadinessAttempts = 0;

            Log.LogInfo(
                $"{source}: queued host readiness wait started. " +
                $"Endpoint={SanitizeEndpointForLog(host)}:{port}; " +
                $"Protocol={protocol}; " +
                $"TimeoutMs={timeoutMs}; " +
                $"PollIntervalMs={pollIntervalMs}");
        }

        _queuedHostReadinessAttempts++;

        int perAttemptTimeoutMs = Math.Max(
            100,
            Math.Min(pollIntervalMs, 1000));

        if (LuxonReadinessProbe.TryProbeNameServer(
                host,
                port,
                protocol,
                perAttemptTimeoutMs,
                out string probeMessage))
        {
            int elapsedMs = (int)Math.Max(
                0,
                (now - _queuedHostReadinessStartedAtUtc).TotalMilliseconds);

            Log.LogInfo(
                $"{source}: queued host readiness confirmed. " +
                $"Endpoint={SanitizeEndpointForLog(host)}:{port}; " +
                $"Protocol={protocol}; " +
                $"ElapsedMs={elapsedMs}; " +
                $"Attempts={_queuedHostReadinessAttempts}; " +
                $"Message={probeMessage}");

            ResetQueuedHostReadinessWindow();
            return true;
        }

        int elapsedSinceStartMs = (int)Math.Max(
            0,
            (now - _queuedHostReadinessStartedAtUtc).TotalMilliseconds);

        if (_queuedHostReadinessAttempts == 1
            || _queuedHostReadinessAttempts % 5 == 0)
        {
            Log.LogInfo(
                $"{source}: queued host readiness pending. " +
                $"Endpoint={SanitizeEndpointForLog(host)}:{port}; " +
                $"Protocol={protocol}; " +
                $"ElapsedMs={elapsedSinceStartMs}; " +
                $"Attempts={_queuedHostReadinessAttempts}; " +
                $"LastFailure={probeMessage}");
        }

        if (elapsedSinceStartMs < timeoutMs)
        {
            return false;
        }

        Log.LogError(
            $"{source}: queued host readiness timed out. " +
            $"Endpoint={SanitizeEndpointForLog(host)}:{port}; " +
            $"Protocol={protocol}; " +
            $"ElapsedMs={elapsedSinceStartMs}; " +
            $"Attempts={_queuedHostReadinessAttempts}; " +
            $"LastFailure={probeMessage}");

        NotifyLocalServerNotDetected("readiness timeout");

        _pendingDirectHostStart = false;
        ResetQueuedHostReadinessWindow();

        return false;
    }

    private void ResetQueuedHostReadinessWindow()
    {
        _queuedHostReadinessStartedAtUtc = default;
        _queuedHostReadinessAttempts = 0;
    }

    private bool CanStartDirectConnection(
        ref bool connectRequested)
    {
        if (!PhotonNetwork.IsConnectedAndReady)
        {
            ClientState currentState = PhotonNetwork.NetworkClientState;
            float now = Time.realtimeSinceStartup;
            bool shouldLogNotReady = now - _lastNotReadyLogAt >= 2f;

            if (shouldLogNotReady)
            {
                Logger.LogWarning(
                    "Photon is not connected and ready. " +
                    $"Current state: {currentState}");

                _lastNotReadyLogAt = now;
            }

            if (!PhotonNetwork.IsConnected
                && currentState == ClientState.Disconnected)
            {
                if (!connectRequested)
                {
                    if (now - _lastReconnectAttemptAt < 1.5f)
                    {
                        return false;
                    }

                    Logger.LogInfo(
                        "Attempting Photon reconnect via NetworkingUtilities.ConnectToNetwork(). " +
                        "Press the host/join key again unless queued host auto-retry is enabled.");

                    Peak.Network.NetworkingUtilities.ConnectToNetwork();
                    connectRequested = true;
                    _lastReconnectAttemptAt = now;
                }
            }
            else
            {
                connectRequested = false;
            }

            return false;
        }

        connectRequested = false;

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
    internal static ConfigEntry<bool> AutoDetectHostLanIpv4 = null!;
    internal static ConfigEntry<string> PreferredHostIpv4 = null!;
    internal static ConfigEntry<string> AllowedHostInterfaces = null!;
    internal static ConfigEntry<bool> AutoUpdateLuxonConfigOnHost = null!;
    internal static ConfigEntry<string> LuxonConfigPath = null!;
    internal static ConfigEntry<bool> AutoStartLocalServerOnHost = null!;
    internal static ConfigEntry<string> LocalServerExecutablePath = null!;
    internal static ConfigEntry<string> LocalServerWorkingDirectory = null!;
    internal static ConfigEntry<string> LocalServerStartArguments = null!;
    internal static ConfigEntry<bool> AutoStopOwnedLocalServerOnExit = null!;
    internal static ConfigEntry<bool> ForceKillOwnedLocalServerOnExit = null!;
    internal static ConfigEntry<int> OwnedLocalServerStopTimeoutMs = null!;
    internal static ConfigEntry<bool> AutoRetryDirectHostUntilReady = null!;
    internal static ConfigEntry<bool> EnableLocalServerReadinessCheck = null!;
    internal static ConfigEntry<int> LocalServerReadinessTimeoutMs = null!;
    internal static ConfigEntry<int> LocalServerReadinessPollIntervalMs = null!;
    internal static ConfigEntry<bool> LanDiscoveryEnabled = null!;
    internal static ConfigEntry<int> LanDiscoveryUdpPort = null!;
    internal static ConfigEntry<int> LanDiscoveryBroadcastIntervalMs = null!;
    internal static ConfigEntry<int> LanDiscoveryEntryTtlMs = null!;
    internal static ConfigEntry<string> LanDiscoveryProtocolVersion = null!;
    internal static ConfigEntry<bool> LanDiscoveryRequireVersionMatch = null!;

    internal static bool IsLocalServerMode =>
        PhotonMode.Value == PhotonConnectionMode.LocalServer;

    internal static void ApplyConfiguredPhotonSettings()
    {
        PhotonConnectionMode mode = PhotonMode.Value;

        var settings = PhotonNetwork.PhotonServerSettings.AppSettings;

        switch (mode)
        {
            case PhotonConnectionMode.CustomCloud:
                ApplyCustomCloudSettings(settings);
                return;

            case PhotonConnectionMode.LocalServer:
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
        if (!IsLocalServerMode)
        {
            return;
        }

        Log.LogInfo(
            $"Local server detected at {GetConfiguredLocalEndpoint()}.");
    }

    internal static void NotifyLocalServerNotDetected(
        string reason)
    {
        if (!IsLocalServerMode)
        {
            return;
        }

        Log.LogInfo(
            $"Local server not detected at {GetConfiguredLocalEndpoint()}: {reason}");
    }

    private static string GetConfiguredLocalEndpoint()
    {
        string address = LocalServerAddress.Value.Trim();
        int port = LocalServerPort.Value;
        ConnectionProtocol protocol = LocalServerProtocol.Value;

        return $"{address}:{port} ({protocol})";
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
                "Cannot apply LocalServer mode.");

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
            "Applied Photon mode LocalServer: " +
            $"Server={serverAddress}; " +
            $"Port={settings.Port}; " +
            $"Protocol={settings.Protocol}; " +
            $"UseNameServer={settings.UseNameServer}");
    }

    private static void MigrateLegacyPhotonModeNameInConfig()
    {
        try
        {
            string configPath = Path.Combine(
                Paths.ConfigPath,
                PluginGuid + ".cfg");

            if (!File.Exists(configPath))
            {
                return;
            }

            string existing = File.ReadAllText(configPath);

            if (existing.IndexOf(
                    "LocalPhotonServer",
                    StringComparison.Ordinal) < 0)
            {
                return;
            }

            string updated = Regex.Replace(
                existing,
                @"^(\s*Mode\s*=\s*)LocalPhotonServer(\s*)$",
                "$1LocalServer$2",
                RegexOptions.Multiline);

            if (string.Equals(existing, updated, StringComparison.Ordinal))
            {
                return;
            }

            File.WriteAllText(configPath, updated);

            Log.LogInfo(
                "Config migration: Mode LocalPhotonServer -> LocalServer completed.");
        }
        catch (Exception ex)
        {
            Log.LogWarning(
                "Config migration skipped after failure. " +
                $"Error={ex.GetType().Name}; " +
                $"Message={ex.Message}");
        }
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