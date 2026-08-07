using PeakLanMod.Lan.Model;

namespace PeakLanMod.Lan.Services;

internal interface ILanPluginOptions
{
}

internal interface ILanWorkflowPolicyService
{
}

internal interface IDirectConnectCoordinator
{
}

internal interface ILanOverlayController
{
}

internal interface ILanDiscoveryRuntimeCoordinator
{
    void RefreshLanDiscoveryBroadcast(string source);
    void StopLanDiscoveryBroadcast(string source);
}

internal interface ILanErrorStateService
{
    void NotifyLocalServerDetected();
    void NotifyLocalServerNotDetected(string reason);
    void ReportStructuredLanError(LanErrorCode code, string source, string message, string context);
    void ClearStructuredLanError(string source, string reason);
    void HandleLeftRoom();
}

internal interface ILocalServerRuntimeService
{
    void ApplyConfiguredPhotonSettings();
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
    private PluginCompatibilityServices()
    {
        Options = new PlaceholderLanPluginOptions();
        WorkflowPolicy = new PlaceholderLanWorkflowPolicyService();
        DirectConnect = new PlaceholderDirectConnectCoordinator();
        Overlay = new PlaceholderLanOverlayController();
        DiscoveryRuntime = new PluginBackedLanDiscoveryRuntimeCoordinator();
        ErrorState = new PluginBackedLanErrorStateService();
        LocalServerRuntime = new PluginBackedLocalServerRuntimeService();
        IdentityAndValidation = new LanIdentityAndValidation();
    }

    internal static IPluginCompatibilityServices CreateDefault()
    {
        return new PluginCompatibilityServices();
    }

    public ILanPluginOptions Options { get; }
    public ILanWorkflowPolicyService WorkflowPolicy { get; }
    public IDirectConnectCoordinator DirectConnect { get; }
    public ILanOverlayController Overlay { get; }
    public ILanDiscoveryRuntimeCoordinator DiscoveryRuntime { get; }
    public ILanErrorStateService ErrorState { get; }
    public ILocalServerRuntimeService LocalServerRuntime { get; }
    public ILanIdentityAndValidation IdentityAndValidation { get; }

    private sealed class PlaceholderLanPluginOptions : ILanPluginOptions
    {
    }

    private sealed class PlaceholderLanWorkflowPolicyService : ILanWorkflowPolicyService
    {
    }

    private sealed class PlaceholderDirectConnectCoordinator : IDirectConnectCoordinator
    {
    }

    private sealed class PlaceholderLanOverlayController : ILanOverlayController
    {
    }

    private sealed class PluginBackedLanDiscoveryRuntimeCoordinator : ILanDiscoveryRuntimeCoordinator
    {
        public void RefreshLanDiscoveryBroadcast(string source)
        {
            Plugin.RefreshLanDiscoveryBroadcast(source);
        }

        public void StopLanDiscoveryBroadcast(string source)
        {
            Plugin.StopLanDiscoveryBroadcast(source);
        }
    }

    private sealed class PluginBackedLanErrorStateService : ILanErrorStateService
    {
        public void NotifyLocalServerDetected()
        {
            Plugin.NotifyLocalServerDetected();
        }

        public void NotifyLocalServerNotDetected(string reason)
        {
            Plugin.NotifyLocalServerNotDetected(reason);
        }

        public void ReportStructuredLanError(LanErrorCode code, string source, string message, string context)
        {
            Plugin.ReportStructuredLanError(code, source, message, context);
        }

        public void ClearStructuredLanError(string source, string reason)
        {
            Plugin.ClearStructuredLanError(source, reason);
        }

        public void HandleLeftRoom()
        {
            Plugin.HandleLeftRoom();
        }
    }

    private sealed class PluginBackedLocalServerRuntimeService : ILocalServerRuntimeService
    {
        public void ApplyConfiguredPhotonSettings()
        {
            Plugin.ApplyConfiguredPhotonSettings();
        }
    }
}
