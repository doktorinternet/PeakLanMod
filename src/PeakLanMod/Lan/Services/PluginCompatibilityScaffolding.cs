using PeakLanMod.Lan.Model;
using System;
using BepInEx.Configuration;
using ExitGames.Client.Photon;
using Photon.Realtime;
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
    ConfigEntry<string> LanServerAddress { get; }
    ConfigEntry<int> LanServerPort { get; }
    ConfigEntry<ConnectionProtocol> LanServerProtocol { get; }
    ConfigEntry<bool> AutoDetectHostLanIpv4 { get; }
    ConfigEntry<string> AllowedHostInterfaces { get; }
    ConfigEntry<bool> AutoUpdateLuxonConfigOnHost { get; }
    ConfigEntry<string> LuxonConfigPath { get; }
    ConfigEntry<bool> AutoStartLanServerOnHost { get; }
    ConfigEntry<string> LanServerExecutablePath { get; }
    ConfigEntry<string> LanServerWorkingDirectory { get; }
    ConfigEntry<string> LanServerStartArguments { get; }
    ConfigEntry<bool> AutoStopOwnedLanServerOnExit { get; }
    ConfigEntry<bool> AutoStopOwnedLanServerOnLeaveRoom { get; }
    ConfigEntry<bool> ForceKillOwnedLanServerOnExit { get; }
    ConfigEntry<int> OwnedLanServerStopTimeoutMs { get; }
    ConfigEntry<bool> AutoRetryDirectHostUntilReady { get; }
    ConfigEntry<int> HostCreateRoomTimeoutSeconds { get; }
    ConfigEntry<int> DirectConnectAttemptIntervalMs { get; }
    ConfigEntry<bool> AutoSkipPhotonFailureDialog { get; }
    ConfigEntry<bool> EnableLanServerReadinessCheck { get; }
    ConfigEntry<int> LanServerReadinessTimeoutMs { get; }
    ConfigEntry<int> LanServerReadinessPollIntervalMs { get; }
    ConfigEntry<bool> LanDiscoveryEnabled { get; }
    ConfigEntry<int> LanDiscoveryUdpPort { get; }
    ConfigEntry<int> LanDiscoveryBroadcastIntervalMs { get; }
    ConfigEntry<int> LanDiscoveryEntryTtlMs { get; }
    ConfigEntry<string> LanDiscoveryProtocolVersion { get; }
    ConfigEntry<bool> LanDiscoveryRequireVersionMatch { get; }
    ConfigEntry<bool> UseSimulatedServerListEntries { get; }
    ConfigEntry<int> SimulatedServerListCount { get; }
    ConfigEntry<bool> EnableStructuredErrorMapping { get; }
    ConfigEntry<bool> EnableVerboseDiagnostics { get; }
    ConfigEntry<bool> PersistCustomizationSelectionOffline { get; }
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
    void RequestDirectJoinStart(string roomName, string source, LanServerEndpoint endpoint);
    void TryProcessQueuedDirectJoinStart(string source);
    void CompletePendingAttempt(string source);
    void CancelPendingAttemptOnDisconnect(DisconnectCause cause, string clientState, string serverAddress);
    bool IsDirectAttemptActive();
    bool ShouldDeferDisconnectError(DisconnectCause cause, out int elapsedMs, out int timeoutMs);
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
    void NotifyLanServerDetected();
    void NotifyLanServerNotDetected(string reason);
    void ReportStructuredLanError(LanErrorCode code, string source, string message, string context);
    void ClearStructuredLanError(string source, string reason);
    void HandleLeftRoom();
    LanErrorDetail? GetConnectionErrorSnapshot();
}

internal interface ILanServerRuntimeService
{
    bool EnsureHostLanServerProcess();
    void StopOwnedLanServerProcessOnExit(string source);
    void ApplyHostLanIpv4Selection();
    void ApplyHostLuxonConfigAutomation();
    bool EnsureLanServerReadinessBeforeConnect(string source, bool queuedHostFlow, LanServerEndpoint? endpointOverride = null);
    void ResetQueuedHostReadinessWindow();
    string GetConfiguredLocalEndpoint();
    string GetEffectiveLocalEndpoint();
    LanServerEndpoint GetConfiguredLanServerEndpoint();
    LanServerEndpoint GetEffectiveLanServerEndpointForConnection();
    bool IsJoinEndpointOverrideActive { get; }
    void ApplyTransientJoinEndpointOverride(LanServerEndpoint endpoint, string source);
    void ClearTransientJoinEndpointOverride(string source);
    void ApplyConfiguredPhotonSettings();
    void DumpPhotonSettings(string source);
}

internal interface ILanModePolicyService
{
    bool IsLanServerModeEnabled { get; }
}

internal interface ILanCustomizationPersistenceService
{
    void TryCaptureLocalCustomization(CharacterCustomization customization, string source);
    void TryRestoreLocalCustomization(CharacterCustomization customization, string source);
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
    ILanServerRuntimeService LanServerRuntime { get; }
    ILanIdentityAndValidation IdentityAndValidation { get; }
    ILanCustomizationPersistenceService CustomizationPersistence { get; }
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
        CustomizationPersistence = options is PlaceholderLanPluginOptions
            ? new PlaceholderLanCustomizationPersistenceService()
            : new LanCustomizationPersistenceService(options);
        LanServerRuntime = options is PlaceholderLanPluginOptions
            ? new PlaceholderLanServerRuntimeService()
            : new LanServerRuntimeService(
                options,
                ErrorState,
                IdentityAndValidation);
        DirectConnect = options is PlaceholderLanPluginOptions
            ? new PlaceholderDirectConnectCoordinator()
            : new DirectConnectCoordinator(
                options,
                LanServerRuntime,
                IdentityAndValidation);
        Overlay = options is PlaceholderLanPluginOptions
            ? new PlaceholderLanOverlayController()
            : new LanOverlayController(
                options,
                DirectConnect,
                DiscoveryRuntime,
                ErrorState,
                LanServerRuntime,
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
    public ILanServerRuntimeService LanServerRuntime { get; }
    public ILanIdentityAndValidation IdentityAndValidation { get; }
    public ILanCustomizationPersistenceService CustomizationPersistence { get; }

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
        public ConfigEntry<string> LanServerAddress => NotReady<string>();
        public ConfigEntry<int> LanServerPort => NotReady<int>();
        public ConfigEntry<ConnectionProtocol> LanServerProtocol => NotReady<ConnectionProtocol>();
        public ConfigEntry<bool> AutoDetectHostLanIpv4 => NotReady<bool>();
        public ConfigEntry<string> AllowedHostInterfaces => NotReady<string>();
        public ConfigEntry<bool> AutoUpdateLuxonConfigOnHost => NotReady<bool>();
        public ConfigEntry<string> LuxonConfigPath => NotReady<string>();
        public ConfigEntry<bool> AutoStartLanServerOnHost => NotReady<bool>();
        public ConfigEntry<string> LanServerExecutablePath => NotReady<string>();
        public ConfigEntry<string> LanServerWorkingDirectory => NotReady<string>();
        public ConfigEntry<string> LanServerStartArguments => NotReady<string>();
        public ConfigEntry<bool> AutoStopOwnedLanServerOnExit => NotReady<bool>();
        public ConfigEntry<bool> AutoStopOwnedLanServerOnLeaveRoom => NotReady<bool>();
        public ConfigEntry<bool> ForceKillOwnedLanServerOnExit => NotReady<bool>();
        public ConfigEntry<int> OwnedLanServerStopTimeoutMs => NotReady<int>();
        public ConfigEntry<bool> AutoRetryDirectHostUntilReady => NotReady<bool>();
        public ConfigEntry<int> HostCreateRoomTimeoutSeconds => NotReady<int>();
        public ConfigEntry<int> DirectConnectAttemptIntervalMs => NotReady<int>();
        public ConfigEntry<bool> AutoSkipPhotonFailureDialog => NotReady<bool>();
        public ConfigEntry<bool> EnableLanServerReadinessCheck => NotReady<bool>();
        public ConfigEntry<int> LanServerReadinessTimeoutMs => NotReady<int>();
        public ConfigEntry<int> LanServerReadinessPollIntervalMs => NotReady<int>();
        public ConfigEntry<bool> LanDiscoveryEnabled => NotReady<bool>();
        public ConfigEntry<int> LanDiscoveryUdpPort => NotReady<int>();
        public ConfigEntry<int> LanDiscoveryBroadcastIntervalMs => NotReady<int>();
        public ConfigEntry<int> LanDiscoveryEntryTtlMs => NotReady<int>();
        public ConfigEntry<string> LanDiscoveryProtocolVersion => NotReady<string>();
        public ConfigEntry<bool> LanDiscoveryRequireVersionMatch => NotReady<bool>();
        public ConfigEntry<bool> UseSimulatedServerListEntries => NotReady<bool>();
        public ConfigEntry<int> SimulatedServerListCount => NotReady<int>();
        public ConfigEntry<bool> EnableStructuredErrorMapping => NotReady<bool>();
        public ConfigEntry<bool> EnableVerboseDiagnostics => NotReady<bool>();
        public ConfigEntry<bool> PersistCustomizationSelectionOffline => NotReady<bool>();
    }

    private sealed class PlaceholderLanCustomizationPersistenceService : ILanCustomizationPersistenceService
    {
        public void TryCaptureLocalCustomization(
            CharacterCustomization customization,
            string source)
        {
        }

        public void TryRestoreLocalCustomization(
            CharacterCustomization customization,
            string source)
        {
        }
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

        public void RequestDirectJoinStart(string roomName, string source, LanServerEndpoint endpoint)
        {
        }

        public void TryProcessQueuedDirectJoinStart(string source)
        {
        }

        public void CompletePendingAttempt(string source)
        {
        }

        public void CancelPendingAttemptOnDisconnect(
            DisconnectCause cause,
            string clientState,
            string serverAddress)
        {
        }

        public bool IsDirectAttemptActive()
        {
            return false;
        }

        public bool ShouldDeferDisconnectError(
            DisconnectCause cause,
            out int elapsedMs,
            out int timeoutMs)
        {
            elapsedMs = 0;
            timeoutMs = 0;
            return false;
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

    private sealed class PlaceholderLanServerRuntimeService : ILanServerRuntimeService
    {
        public bool EnsureHostLanServerProcess()
        {
            return false;
        }

        public void StopOwnedLanServerProcessOnExit(string source)
        {
        }

        public void ApplyHostLanIpv4Selection()
        {
        }

        public void ApplyHostLuxonConfigAutomation()
        {
        }

        public bool EnsureLanServerReadinessBeforeConnect(
            string source,
            bool queuedHostFlow,
            LanServerEndpoint? endpointOverride = null)
        {
            return false;
        }

        public void ResetQueuedHostReadinessWindow()
        {
        }

        public string GetConfiguredLocalEndpoint()
        {
            return "<not-initialized>";
        }

        public string GetEffectiveLocalEndpoint()
        {
            return "<not-initialized>";
        }

        public LanServerEndpoint GetConfiguredLanServerEndpoint()
        {
            return new LanServerEndpoint(string.Empty, 0, ConnectionProtocol.Udp);
        }

        public LanServerEndpoint GetEffectiveLanServerEndpointForConnection()
        {
            return new LanServerEndpoint(string.Empty, 0, ConnectionProtocol.Udp);
        }

        public bool IsJoinEndpointOverrideActive => false;

        public void ApplyTransientJoinEndpointOverride(
            LanServerEndpoint endpoint,
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
