using BepInEx.Configuration;
using ExitGames.Client.Photon;
using UnityEngine;

namespace PeakLanMod.Lan.Services;

internal sealed class LanPluginOptions : ILanPluginOptions
{
    internal LanPluginOptions(
        ConfigFile config)
    {
        RoomName = config.Bind(
            "Direct Connect",
            "RoomName",
            "badhorse-lan-mod-room_" + System.Guid.NewGuid().ToString("N")[..8],
            "Host room name.");

        HostKey = config.Bind(
            "Direct Connect",
            "HostKey",
            new KeyboardShortcut(KeyCode.F6),
            "Start direct host. Testing parameter.");

        JoinKey = config.Bind(
            "Direct Connect",
            "JoinKey",
            new KeyboardShortcut(KeyCode.F7),
            "Start direct join. Testing parameter.");

        LocalServerAddress = config.Bind(
            "Hosting",
            "LocalServerAddress",
            "127.0.0.1",
            "Local Luxon server hostname or IP. Swap with LAN host address.");

        LocalServerPort = config.Bind(
            "Hosting",
            "LocalServerPort",
            5058,
            "Port of Local Luxon Name Server UDP/TCP port.");

        LocalServerProtocol = config.Bind(
            "Hosting",
            "LocalServerProtocol",
            ConnectionProtocol.Udp,
            "Local server transport protocol.");

        WorkflowMode = config.Bind(
            "LanWorkflow",
            "WorkflowMode",
            Plugin.LanWorkflowMode.AutoSetup,
            "High-level LAN workflow mode: AutoSetup (auto host endpoint/luxon updates), LockedRuntime (stable host endpoint, no host endpoint rewrites), or Advanced (manual control of all LAN workflow settings).");

        AutoLockWorkflowModeAfterSuccessfulHost = config.Bind(
            "LanWorkflow",
            "AutoLockWorkflowModeAfterSuccessfulHost",
            true,
            "Automatically switch WorkflowMode from AutoSetup to LockedRuntime after a successful host room creation. Sets itself to false afterwards.");

        AutoDetectHostLanIpv4 = config.Bind(
            "LanWorkflow",
            "AutoDetectHostIPv4",
            true,
            "Auto-detect host LAN IPv4 during direct host in LocalServer mode. Controlled by WorkflowMode unless WorkflowMode=Advanced.");

        AllowedHostInterfaces = config.Bind(
            "LanWorkflow",
            "AllowedHostInterfaces",
            string.Empty,
            "Optional CSV interface filters (name/description/id contains match) for host LAN IPv4 auto-detection.");

        AutoUpdateLuxonConfigOnHost = config.Bind(
            "LanWorkflow",
            "AutoUpdateLuxonConfigOnHost",
            true,
            "Automatically rewrite Luxon external_address values during direct host in LocalServer mode. Controlled by WorkflowMode unless WorkflowMode=Advanced.");

        LuxonConfigPath = config.Bind(
            "LanWorkflow",
            "LuxonConfigPath",
            "server/config.yml",
            "Relative or absolute path to Luxon config.yml used by host-side external_address automation.");

        AutoStartLocalServerOnHost = config.Bind(
            "LanWorkflow",
            "AutoStartLocalServerOnHost",
            true,
            "Start local server executable during direct host when no matching process is already running.");

        LocalServerExecutablePath = config.Bind(
            "LanWorkflow",
            "LocalServerExecutablePath",
            "server/luxon_server.msvc.release.exe",
            "Relative or absolute path to the local server executable for optional host auto-start.");

        LocalServerWorkingDirectory = config.Bind(
            "LanWorkflow",
            "LocalServerWorkingDirectory",
            "server",
            "Working directory used when launching the local server executable. Leave empty to use executable directory.");

        LocalServerStartArguments = config.Bind(
            "LanWorkflow",
            "LocalServerStartArguments",
            "config.yml",
            "Arguments passed to the local server executable when host auto-start is enabled.");

        AutoStopOwnedLocalServerOnExit = config.Bind(
            "LanWorkflow",
            "AutoStopOwnedLocalServerOnExit",
            true,
            "Stop only plugin-owned local server process on plugin unload/game exit.");

        AutoStopOwnedLocalServerOnLeaveRoom = config.Bind(
            "LanWorkflow",
            "AutoStopOwnedLocalServerOnLeaveRoom",
            true,
            "Stop plugin-owned local server process when leaving a room.");

        ForceKillOwnedLocalServerOnExit = config.Bind(
            "LanWorkflow",
            "ForceKillOwnedLocalServerOnExit",
            true,
            "Force-kill plugin-owned local server process on exit when graceful stop times out.");

        OwnedLocalServerStopTimeoutMs = config.Bind(
            "LanWorkflow",
            "OwnedLocalServerStopTimeoutMs",
            2000,
            "Timeout in milliseconds for graceful stop of plugin-owned local server process.");

        AutoRetryDirectHostUntilReady = config.Bind(
            "LanWorkflow",
            "AutoRetryDirectHostUntilReady",
            true,
            "Queue host intent on HostKey and auto-complete when the server becomes connected and ready.");

        EnableLocalServerReadinessCheck = config.Bind(
            "LanWorkflow",
            "EnableLocalServerReadinessCheck",
            true,
            "Wait for local NameServer readiness before direct host/join connect attempts in LocalServer mode.");

        AutoSkipPhotonFailureDialog = config.Bind(
            "LanWorkflow",
            "AutoSkipPhotonFailureDialog",
            true,
            "Auto-apply offline fallback to bypass the default Photon retry/offline popup on menu entry and post-room return.");

        LocalServerReadinessTimeoutMs = config.Bind(
            "LanWorkflow",
            "ReadinessTimeoutMs",
            5000,
            "Maximum milliseconds to wait for local NameServer readiness before connect attempts.");

        LocalServerReadinessPollIntervalMs = config.Bind(
            "LanWorkflow",
            "ReadinessPollIntervalMs",
            250,
            "Milliseconds between local NameServer readiness probe attempts.");

        LanDiscoveryEnabled = config.Bind(
            "LanWorkflow",
            "DiscoveryEnabled",
            false,
            "Enable UDP LAN session discovery listener and host announcement broadcast in LocalServer mode.");

        LanDiscoveryUdpPort = config.Bind(
            "LanWorkflow",
            "DiscoveryUdpPort",
            47777,
            "UDP port used for LAN discovery announcements.");

        LanDiscoveryBroadcastIntervalMs = config.Bind(
            "LanWorkflow",
            "DiscoveryBroadcastIntervalMs",
            1000,
            "Interval in milliseconds for host discovery announcements.");

        LanDiscoveryEntryTtlMs = config.Bind(
            "LanWorkflow",
            "DiscoveryEntryTtlMs",
            5000,
            "Milliseconds before an unrefreshed discovered session is evicted.");

        LanDiscoveryProtocolVersion = config.Bind(
            "LanWorkflow",
            "ProtocolVersion",
            "1",
            "Discovery protocol version string advertised and required for session compatibility.");

        LanDiscoveryRequireVersionMatch = config.Bind(
            "LanWorkflow",
            "RequireVersionMatch",
            true,
            "Require exact game/mod version match for discovery session compatibility.");

        EnableStructuredErrorMapping = config.Bind(
            "LanWorkflow",
            "EnableStructuredErrorMapping",
            true,
            "Enable deterministic LAN error classification and UI/status surfacing.");
    }

    public ConfigEntry<string> RoomName { get; }
    public ConfigEntry<KeyboardShortcut> HostKey { get; }
    public ConfigEntry<KeyboardShortcut> JoinKey { get; }
    public ConfigEntry<Plugin.LanWorkflowMode> WorkflowMode { get; }
    public ConfigEntry<bool> AutoLockWorkflowModeAfterSuccessfulHost { get; }
    public ConfigEntry<string> LocalServerAddress { get; }
    public ConfigEntry<int> LocalServerPort { get; }
    public ConfigEntry<ConnectionProtocol> LocalServerProtocol { get; }
    public ConfigEntry<bool> AutoDetectHostLanIpv4 { get; }
    public ConfigEntry<string> AllowedHostInterfaces { get; }
    public ConfigEntry<bool> AutoUpdateLuxonConfigOnHost { get; }
    public ConfigEntry<string> LuxonConfigPath { get; }
    public ConfigEntry<bool> AutoStartLocalServerOnHost { get; }
    public ConfigEntry<string> LocalServerExecutablePath { get; }
    public ConfigEntry<string> LocalServerWorkingDirectory { get; }
    public ConfigEntry<string> LocalServerStartArguments { get; }
    public ConfigEntry<bool> AutoStopOwnedLocalServerOnExit { get; }
    public ConfigEntry<bool> AutoStopOwnedLocalServerOnLeaveRoom { get; }
    public ConfigEntry<bool> ForceKillOwnedLocalServerOnExit { get; }
    public ConfigEntry<int> OwnedLocalServerStopTimeoutMs { get; }
    public ConfigEntry<bool> AutoRetryDirectHostUntilReady { get; }
    public ConfigEntry<bool> AutoSkipPhotonFailureDialog { get; }
    public ConfigEntry<bool> EnableLocalServerReadinessCheck { get; }
    public ConfigEntry<int> LocalServerReadinessTimeoutMs { get; }
    public ConfigEntry<int> LocalServerReadinessPollIntervalMs { get; }
    public ConfigEntry<bool> LanDiscoveryEnabled { get; }
    public ConfigEntry<int> LanDiscoveryUdpPort { get; }
    public ConfigEntry<int> LanDiscoveryBroadcastIntervalMs { get; }
    public ConfigEntry<int> LanDiscoveryEntryTtlMs { get; }
    public ConfigEntry<string> LanDiscoveryProtocolVersion { get; }
    public ConfigEntry<bool> LanDiscoveryRequireVersionMatch { get; }
    public ConfigEntry<bool> EnableStructuredErrorMapping { get; }
}
