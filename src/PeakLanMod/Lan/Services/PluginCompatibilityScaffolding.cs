using PeakLanMod.Lan.Model;
using System;
using BepInEx.Configuration;
using ExitGames.Client.Photon;
using UnityEngine;
using PeakLanMod.Lan.State;
using PeakLanMod.Lan.UI;

namespace PeakLanMod.Lan.Services;

internal interface ILanPluginOptions
{
    ConfigEntry<string> RoomName { get; }
    ConfigEntry<KeyboardShortcut> HostKey { get; }
    ConfigEntry<KeyboardShortcut> JoinKey { get; }
    ConfigEntry<LanWorkflowMode> WorkflowMode { get; }
    ConfigEntry<bool> AutoLockWorkflowModeAfterSuccessfulHost { get; }
    ConfigEntry<string> LocalServerAddress { get; }
    ConfigEntry<int> LocalServerPort { get; }
    ConfigEntry<ConnectionProtocol> LocalServerProtocol { get; }
    ConfigEntry<bool> AutoDetectHostLanIpv4 { get; }
    ConfigEntry<string> AllowedHostInterfaces { get; }
    ConfigEntry<bool> AutoUpdateLuxonConfigOnHost { get; }
    ConfigEntry<string> LuxonConfigPath { get; }
    ConfigEntry<bool> AutoStartLocalServerOnHost { get; }
    ConfigEntry<string> LocalServerExecutablePath { get; }
    ConfigEntry<string> LocalServerWorkingDirectory { get; }
    ConfigEntry<string> LocalServerStartArguments { get; }
    ConfigEntry<bool> AutoStopOwnedLocalServerOnExit { get; }
    ConfigEntry<bool> AutoStopOwnedLocalServerOnLeaveRoom { get; }
    ConfigEntry<bool> ForceKillOwnedLocalServerOnExit { get; }
    ConfigEntry<int> OwnedLocalServerStopTimeoutMs { get; }
    ConfigEntry<bool> AutoRetryDirectHostUntilReady { get; }
    ConfigEntry<bool> AutoSkipPhotonFailureDialog { get; }
    ConfigEntry<bool> EnableLocalServerReadinessCheck { get; }
    ConfigEntry<int> LocalServerReadinessTimeoutMs { get; }
    ConfigEntry<int> LocalServerReadinessPollIntervalMs { get; }
    ConfigEntry<bool> LanDiscoveryEnabled { get; }
    ConfigEntry<int> LanDiscoveryUdpPort { get; }
    ConfigEntry<int> LanDiscoveryBroadcastIntervalMs { get; }
    ConfigEntry<int> LanDiscoveryEntryTtlMs { get; }
    ConfigEntry<string> LanDiscoveryProtocolVersion { get; }
    ConfigEntry<bool> LanDiscoveryRequireVersionMatch { get; }
    ConfigEntry<bool> EnableStructuredErrorMapping { get; }
}

internal interface ILanWorkflowPolicyService
{
    void ApplyLanWorkflowMode(bool force, string source);
    void TryAutoLockWorkflowModeAfterSuccessfulHost(string source);
}

internal interface IDirectConnectCoordinator
{
    void RequestDirectHostStart(string source);
    void TryProcessQueuedDirectHostStart(string source);
    void StartDirectJoin();
    void RequestDirectJoinStart(string roomName, string source, LocalServerEndpoint endpoint);
    void TryProcessQueuedDirectJoinStart(string source);
}

internal interface ILanOverlayController
{
    void UpdateLanPanelCollapseForSettingsScreen();
    bool ShouldRenderLanUiOverlay();
    void RenderLanUiOverlay();
}

internal interface ILanDiscoveryRuntimeCoordinator
{
    void SyncLanDiscoveryRuntime(string source);
    void RefreshLanDiscoveryBroadcast(string source);
    void StopLanDiscoveryBroadcast(string source);
    void ShutdownLanDiscoveryRuntime(string source);
    LanSessionInfo[] GetDiscoverySnapshot();
    (string Phase, DateTime UpdatedAtUtc) GetConnectionPhaseSnapshot();
}

internal interface ILanErrorStateService
{
    void LogPhotonStateChanges();
    void NotifyLocalServerDetected();
    void NotifyLocalServerNotDetected(string reason);
    void ReportStructuredLanError(LanErrorCode code, string source, string message, string context);
    void ClearStructuredLanError(string source, string reason);
    void HandleLeftRoom();
    LanErrorDetail? GetConnectionErrorSnapshot();
}

internal interface ILocalServerRuntimeService
{
    bool EnsureHostLocalServerProcess();
    void StopOwnedLocalServerProcessOnExit(string source);
    void ApplyHostLanIpv4Selection();
    void ApplyHostLuxonConfigAutomation();
    bool EnsureLocalServerReadinessBeforeConnect(string source, bool queuedHostFlow, LocalServerEndpoint? endpointOverride = null);
    bool WasLastQueuedHostReadinessTimeout { get; }
    void ResetQueuedHostReadinessWindow();
    string GetConfiguredLocalEndpoint();
    string GetEffectiveLocalEndpoint();
    LocalServerEndpoint GetConfiguredLocalServerEndpoint();
    LocalServerEndpoint GetEffectiveLocalServerEndpointForConnection();
    bool IsJoinEndpointOverrideActive { get; }
    void ApplyTransientJoinEndpointOverride(LocalServerEndpoint endpoint, string source);
    void ClearTransientJoinEndpointOverride(string source);
    void ApplyConfiguredPhotonSettings();
    void DumpPhotonSettings(string source);
}

internal interface ILanModePolicyService
{
    bool IsLocalServerModeEnabled { get; }
}

internal interface ILanIdentityAndValidation
{
    string NormalizeRoomName(string roomName);
    bool TryNormalizeRoomName(string roomName, out string normalizedRoomName, out string failureReason);
    string NormalizeRoomNameInputForUi(string roomName);
    bool TryContainsBlockedHostRoomNameTerm(string normalizedRoomName, out string blockedTerm);
    bool TryGetValidatedHostRoomName(string roomName, out string normalizedRoomName, out string failureReason);
    bool TryGetValidatedHostRoomNameFromInput(string roomName, out string normalizedRoomName, out string failureReason);
    string PullU();
    bool IsCurrentUserInX7GateSet();
    string SanitizeEndpointForLog(string endpoint);
    string Fingerprint(string value);
}

internal interface IPluginCompatibilityServices
{
    ILanPluginOptions Options { get; }
    ILanModePolicyService ModePolicy { get; }
    ILanWorkflowPolicyService WorkflowPolicy { get; }
    IDirectConnectCoordinator DirectConnect { get; }
    ILanOverlayController Overlay { get; }
    ILanDiscoveryRuntimeCoordinator DiscoveryRuntime { get; }
    ILanErrorStateService ErrorState { get; }
    ILocalServerRuntimeService LocalServerRuntime { get; }
    ILanIdentityAndValidation IdentityAndValidation { get; }
}

internal sealed class PluginCompatibilityServices : IPluginCompatibilityServices
{
    private PluginCompatibilityServices(
        ILanPluginOptions options,
        ILanWorkflowPolicyService workflowPolicy)
    {
        var connectionStateStore = new LanConnectionStateStore();

        Options = options;
        ModePolicy = new LanModePolicyService();
        WorkflowPolicy = workflowPolicy;
        DiscoveryRuntime = new LanDiscoveryRuntimeCoordinator(
            options,
            connectionStateStore,
            Plugin.PluginVersion);
        ErrorState = new LanErrorStateService(
            options,
            connectionStateStore);
        IdentityAndValidation = new LanIdentityAndValidation();
        LocalServerRuntime = options is PlaceholderLanPluginOptions
            ? new PlaceholderLocalServerRuntimeService()
            : new LocalServerRuntimeService(
                options,
                ErrorState,
                IdentityAndValidation);
        DirectConnect = options is PlaceholderLanPluginOptions
            ? new PlaceholderDirectConnectCoordinator()
            : new DirectConnectCoordinator(
                options,
                LocalServerRuntime,
                IdentityAndValidation);
        Overlay = options is PlaceholderLanPluginOptions
            ? new PlaceholderLanOverlayController()
            : new LanOverlayController(
                options,
                DirectConnect,
                DiscoveryRuntime,
                ErrorState,
                LocalServerRuntime,
                IdentityAndValidation);
    }

    internal static IPluginCompatibilityServices CreateDefault()
    {
        return new PluginCompatibilityServices(
            new PlaceholderLanPluginOptions(),
            new PlaceholderLanWorkflowPolicyService());
    }

    internal static IPluginCompatibilityServices CreateForPlugin(
        ConfigFile config)
    {
        var options = new LanPluginOptions(config);
        var workflowPolicy = new LanWorkflowPolicyService(options);

        return new PluginCompatibilityServices(
            options,
            workflowPolicy);
    }

    public ILanPluginOptions Options { get; }
    public ILanModePolicyService ModePolicy { get; }
    public ILanWorkflowPolicyService WorkflowPolicy { get; }
    public IDirectConnectCoordinator DirectConnect { get; }
    public ILanOverlayController Overlay { get; }
    public ILanDiscoveryRuntimeCoordinator DiscoveryRuntime { get; }
    public ILanErrorStateService ErrorState { get; }
    public ILocalServerRuntimeService LocalServerRuntime { get; }
    public ILanIdentityAndValidation IdentityAndValidation { get; }

    private sealed class PlaceholderLanPluginOptions : ILanPluginOptions
    {
        private static ConfigEntry<T> NotReady<T>()
        {
            throw new InvalidOperationException(
                "Plugin compatibility services are not initialized yet.");
        }

        public ConfigEntry<string> RoomName => NotReady<string>();
        public ConfigEntry<KeyboardShortcut> HostKey => NotReady<KeyboardShortcut>();
        public ConfigEntry<KeyboardShortcut> JoinKey => NotReady<KeyboardShortcut>();
        public ConfigEntry<LanWorkflowMode> WorkflowMode => NotReady<LanWorkflowMode>();
        public ConfigEntry<bool> AutoLockWorkflowModeAfterSuccessfulHost => NotReady<bool>();
        public ConfigEntry<string> LocalServerAddress => NotReady<string>();
        public ConfigEntry<int> LocalServerPort => NotReady<int>();
        public ConfigEntry<ConnectionProtocol> LocalServerProtocol => NotReady<ConnectionProtocol>();
        public ConfigEntry<bool> AutoDetectHostLanIpv4 => NotReady<bool>();
        public ConfigEntry<string> AllowedHostInterfaces => NotReady<string>();
        public ConfigEntry<bool> AutoUpdateLuxonConfigOnHost => NotReady<bool>();
        public ConfigEntry<string> LuxonConfigPath => NotReady<string>();
        public ConfigEntry<bool> AutoStartLocalServerOnHost => NotReady<bool>();
        public ConfigEntry<string> LocalServerExecutablePath => NotReady<string>();
        public ConfigEntry<string> LocalServerWorkingDirectory => NotReady<string>();
        public ConfigEntry<string> LocalServerStartArguments => NotReady<string>();
        public ConfigEntry<bool> AutoStopOwnedLocalServerOnExit => NotReady<bool>();
        public ConfigEntry<bool> AutoStopOwnedLocalServerOnLeaveRoom => NotReady<bool>();
        public ConfigEntry<bool> ForceKillOwnedLocalServerOnExit => NotReady<bool>();
        public ConfigEntry<int> OwnedLocalServerStopTimeoutMs => NotReady<int>();
        public ConfigEntry<bool> AutoRetryDirectHostUntilReady => NotReady<bool>();
        public ConfigEntry<bool> AutoSkipPhotonFailureDialog => NotReady<bool>();
        public ConfigEntry<bool> EnableLocalServerReadinessCheck => NotReady<bool>();
        public ConfigEntry<int> LocalServerReadinessTimeoutMs => NotReady<int>();
        public ConfigEntry<int> LocalServerReadinessPollIntervalMs => NotReady<int>();
        public ConfigEntry<bool> LanDiscoveryEnabled => NotReady<bool>();
        public ConfigEntry<int> LanDiscoveryUdpPort => NotReady<int>();
        public ConfigEntry<int> LanDiscoveryBroadcastIntervalMs => NotReady<int>();
        public ConfigEntry<int> LanDiscoveryEntryTtlMs => NotReady<int>();
        public ConfigEntry<string> LanDiscoveryProtocolVersion => NotReady<string>();
        public ConfigEntry<bool> LanDiscoveryRequireVersionMatch => NotReady<bool>();
        public ConfigEntry<bool> EnableStructuredErrorMapping => NotReady<bool>();
    }

    private sealed class PlaceholderLanWorkflowPolicyService : ILanWorkflowPolicyService
    {
        public void ApplyLanWorkflowMode(
            bool force,
            string source)
        {
        }

        public void TryAutoLockWorkflowModeAfterSuccessfulHost(
            string source)
        {
        }
    }

    private sealed class PlaceholderDirectConnectCoordinator : IDirectConnectCoordinator
    {
        public void RequestDirectHostStart(string source)
        {
        }

        public void TryProcessQueuedDirectHostStart(string source)
        {
        }

        public void StartDirectJoin()
        {
        }

        public void RequestDirectJoinStart(string roomName, string source, LocalServerEndpoint endpoint)
        {
        }

        public void TryProcessQueuedDirectJoinStart(string source)
        {
        }
    }

    private sealed class PlaceholderLanOverlayController : ILanOverlayController
    {
        public void UpdateLanPanelCollapseForSettingsScreen()
        {
        }

        public bool ShouldRenderLanUiOverlay()
        {
            return false;
        }

        public void RenderLanUiOverlay()
        {
        }
    }

    private sealed class PlaceholderLocalServerRuntimeService : ILocalServerRuntimeService
    {
        public bool EnsureHostLocalServerProcess()
        {
            return false;
        }

        public void StopOwnedLocalServerProcessOnExit(string source)
        {
        }

        public void ApplyHostLanIpv4Selection()
        {
        }

        public void ApplyHostLuxonConfigAutomation()
        {
        }

        public bool EnsureLocalServerReadinessBeforeConnect(
            string source,
            bool queuedHostFlow,
            LocalServerEndpoint? endpointOverride = null)
        {
            return false;
        }

        public void ResetQueuedHostReadinessWindow()
        {
        }

        public bool WasLastQueuedHostReadinessTimeout => false;

        public string GetConfiguredLocalEndpoint()
        {
            return "<not-initialized>";
        }

        public string GetEffectiveLocalEndpoint()
        {
            return "<not-initialized>";
        }

        public LocalServerEndpoint GetConfiguredLocalServerEndpoint()
        {
            return new LocalServerEndpoint(string.Empty, 0, ConnectionProtocol.Udp);
        }

        public LocalServerEndpoint GetEffectiveLocalServerEndpointForConnection()
        {
            return new LocalServerEndpoint(string.Empty, 0, ConnectionProtocol.Udp);
        }

        public bool IsJoinEndpointOverrideActive => false;

        public void ApplyTransientJoinEndpointOverride(
            LocalServerEndpoint endpoint,
            string source)
        {
        }

        public void ClearTransientJoinEndpointOverride(
            string source)
        {
        }

        public void ApplyConfiguredPhotonSettings()
        {
        }

        public void DumpPhotonSettings(string source)
        {
        }
    }
}
