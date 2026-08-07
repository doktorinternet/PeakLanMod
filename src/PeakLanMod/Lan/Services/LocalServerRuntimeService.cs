using BepInEx;
using ExitGames.Client.Photon;
using PeakLanMod.Lan.Diagnostics;
using PeakLanMod.Lan.Model;
using Photon.Pun;
using Photon.Realtime;
using System;

namespace PeakLanMod.Lan.Services;

internal sealed class LocalServerRuntimeService : ILocalServerRuntimeService
{
    private readonly ILanPluginOptions _options;
    private readonly ILanErrorStateService _errorState;
    private readonly ILanIdentityAndValidation _identityAndValidation;
    private LocalServerEndpoint? _transientJoinEndpointOverride;
    private DateTime _queuedHostReadinessStartedAtUtc;
    private int _queuedHostReadinessAttempts;

    internal LocalServerRuntimeService(
        ILanPluginOptions options,
        ILanErrorStateService errorState,
        ILanIdentityAndValidation identityAndValidation)
    {
        _options = options;
        _errorState = errorState;
        _identityAndValidation = identityAndValidation;
    }

    public bool WasLastQueuedHostReadinessTimeout { get; private set; }

    public bool EnsureHostLocalServerProcess()
    {
        if (!Plugin.IsLocalServerMode)
        {
            return true;
        }

        if (!_options.AutoStartLocalServerOnHost.Value)
        {
            return true;
        }

        string executablePath = _options.LocalServerExecutablePath.Value.Trim();
        string workingDirectory = _options.LocalServerWorkingDirectory.Value.Trim();
        string startArguments = _options.LocalServerStartArguments.Value;

        if (!LuxonProcessController.TryEnsureRunning(
                executablePath,
                Paths.ConfigPath,
                workingDirectory,
                startArguments,
                out LuxonProcessEnsureResult result))
        {
            _errorState.ReportStructuredLanError(
                LanErrorClassifier.ClassifyAutoStartFailure(),
                source: "EnsureHostLocalServerProcess",
                message: "Local server process start/attach failed.",
                context: result.Message);

            Plugin.Log.LogError(
                "Local server host auto-start failed. " +
                $"Executable={result.ExecutablePathForLog}; " +
                $"WorkingDirectory={result.WorkingDirectoryForLog}; " +
                $"Reason={result.Message}");

            _errorState.NotifyLocalServerNotDetected("auto-start failed");
            return false;
        }

        Plugin.Log.LogInfo(
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

    public void StopOwnedLocalServerProcessOnExit(
        string source)
    {
        if (!Plugin.IsLocalServerMode)
        {
            return;
        }

        if (!_options.AutoStopOwnedLocalServerOnExit.Value)
        {
            Plugin.Log.LogInfo(
                $"{source}: owned local server stop on exit is disabled.");
            return;
        }

        int timeoutMs = Math.Max(0, _options.OwnedLocalServerStopTimeoutMs.Value);
        bool forceKill = _options.ForceKillOwnedLocalServerOnExit.Value;

        if (LuxonProcessController.TryStopOwnedProcess(
                timeoutMs,
                forceKill,
                out string resultMessage))
        {
            Plugin.Log.LogInfo(
                $"{source}: local server process stop succeeded. " +
                $"{resultMessage}");
            return;
        }

        Plugin.Log.LogInfo(
            $"{source}: local server process stop skipped or incomplete. " +
            $"{resultMessage}; " +
            $"Ownership={LuxonProcessController.OwnershipState}");
    }

    public void ApplyHostLanIpv4Selection()
    {
        if (!Plugin.IsLocalServerMode)
        {
            return;
        }

        if (!_options.AutoDetectHostLanIpv4.Value)
        {
            return;
        }

        if (!LanEndpointResolver.TryResolveHostLanIpv4(
                _options.AllowedHostInterfaces.Value,
                out string selectedIpv4,
                out string reason))
        {
            Plugin.Log.LogWarning(
                "Host LAN IPv4 selection failed. " +
                $"Reason={reason}; " +
                $"KeepingLocalServerAddress={_identityAndValidation.SanitizeEndpointForLog(_options.LocalServerAddress.Value)}");

            return;
        }

        string previousAddress =
            _options.LocalServerAddress.Value.Trim();

        if (string.Equals(
                previousAddress,
                selectedIpv4,
                StringComparison.OrdinalIgnoreCase))
        {
            Plugin.Log.LogInfo(
                "Host LAN IPv4 selection kept existing LocalServerAddress. " +
                $"Selected={_identityAndValidation.SanitizeEndpointForLog(selectedIpv4)}; " +
                $"SelectionReason={reason}");

            return;
        }

        _options.LocalServerAddress.Value = selectedIpv4;

        Plugin.Log.LogInfo(
            "Host LAN IPv4 selection updated LocalServerAddress. " +
            $"Previous={_identityAndValidation.SanitizeEndpointForLog(previousAddress)}; " +
            $"Selected={_identityAndValidation.SanitizeEndpointForLog(selectedIpv4)}; " +
            $"SelectedFingerprint={_identityAndValidation.Fingerprint(selectedIpv4)}; " +
            $"SelectionReason={reason}");
    }

    public void ApplyHostLuxonConfigAutomation()
    {
        if (!Plugin.IsLocalServerMode)
        {
            return;
        }

        if (!_options.AutoUpdateLuxonConfigOnHost.Value)
        {
            return;
        }

        string endpointHost = _options.LocalServerAddress.Value.Trim();
        string configPath = _options.LuxonConfigPath.Value.Trim();

        if (!LuxonConfigManager.TryUpdateExternalAddresses(
                endpointHost,
                configPath,
                out LuxonConfigUpdateResult result))
        {
            Plugin.Log.LogWarning(
                "Luxon config host automation failed. " +
                $"Host={_identityAndValidation.SanitizeEndpointForLog(endpointHost)}; " +
                $"ConfigPath={result.ConfigPathForLog}; " +
                $"Reason={result.Message}");

            return;
        }

        Plugin.Log.LogInfo(
            "Luxon config host automation succeeded. " +
            $"Host={_identityAndValidation.SanitizeEndpointForLog(endpointHost)}; " +
            $"ConfigPath={result.ConfigPathForLog}; " +
            $"UpdatedEntries={result.UpdatedEntryCount}; " +
            $"MatchedEntries={result.MatchedEntryCount}; " +
            $"Changed={result.WasChanged}");
    }

    public bool EnsureLocalServerReadinessBeforeConnect(
        string source,
        bool queuedHostFlow,
        LocalServerEndpoint? endpointOverride = null)
    {
        WasLastQueuedHostReadinessTimeout = false;

        if (!Plugin.IsLocalServerMode)
        {
            return true;
        }

        if (!_options.EnableLocalServerReadinessCheck.Value)
        {
            return true;
        }

        int timeoutMs = Math.Max(0, _options.LocalServerReadinessTimeoutMs.Value);
        int pollIntervalMs = Math.Max(50, _options.LocalServerReadinessPollIntervalMs.Value);

        LocalServerEndpoint endpoint = endpointOverride
            ?? GetConfiguredLocalServerEndpoint();

        string host = endpoint.Address.Trim();
        int port = endpoint.Port;
        ConnectionProtocol protocol = endpoint.Protocol;

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
            _errorState.ReportStructuredLanError(
                LanErrorClassifier.ClassifyReadinessTimeout(),
                source,
                "Local NameServer readiness timed out.",
                result.LastFailureMessage);

            Plugin.Log.LogError(
                $"{source}: local NameServer readiness timed out. " +
                $"Endpoint={_identityAndValidation.SanitizeEndpointForLog(host)}:{port}; " +
                $"Protocol={protocol}; " +
                $"ElapsedMs={result.ElapsedMilliseconds}; " +
                $"Attempts={result.AttemptCount}; " +
                $"LastFailure={result.LastFailureMessage}");

            _errorState.NotifyLocalServerNotDetected("readiness timeout");
            return false;
        }

        Plugin.Log.LogInfo(
            $"{source}: local NameServer readiness confirmed. " +
            $"Endpoint={_identityAndValidation.SanitizeEndpointForLog(host)}:{port}; " +
            $"Protocol={protocol}; " +
            $"ElapsedMs={result.ElapsedMilliseconds}; " +
            $"Attempts={result.AttemptCount}; " +
            $"Message={result.SuccessMessage}");

        _errorState.ClearStructuredLanError(
            source,
            "name server readiness confirmed");

        return true;
    }

    public void ResetQueuedHostReadinessWindow()
    {
        _queuedHostReadinessStartedAtUtc = default;
        _queuedHostReadinessAttempts = 0;
        WasLastQueuedHostReadinessTimeout = false;
    }

    public string GetConfiguredLocalEndpoint()
    {
        string address = _options.LocalServerAddress.Value.Trim();
        int port = _options.LocalServerPort.Value;
        ConnectionProtocol protocol = _options.LocalServerProtocol.Value;

        return $"{address}:{port} ({protocol})";
    }

    public string GetEffectiveLocalEndpoint()
    {
        LocalServerEndpoint endpoint =
            GetEffectiveLocalServerEndpointForConnection();

        return $"{endpoint.Address}:{endpoint.Port} ({endpoint.Protocol})";
    }

    public LocalServerEndpoint GetConfiguredLocalServerEndpoint()
    {
        string address = _options.LocalServerAddress.Value.Trim();
        int port = _options.LocalServerPort.Value;
        ConnectionProtocol protocol = _options.LocalServerProtocol.Value;

        return new LocalServerEndpoint(address, port, protocol);
    }

    public LocalServerEndpoint GetEffectiveLocalServerEndpointForConnection()
    {
        return _transientJoinEndpointOverride
            ?? GetConfiguredLocalServerEndpoint();
    }

    public bool IsJoinEndpointOverrideActive =>
        _transientJoinEndpointOverride is not null;

    public void ApplyTransientJoinEndpointOverride(
        LocalServerEndpoint endpoint,
        string source)
    {
        _transientJoinEndpointOverride = endpoint;

        Plugin.Log.LogInfo(
            $"{source}: runtime join endpoint override applied. " +
            $"Endpoint={_identityAndValidation.SanitizeEndpointForLog(endpoint.Address)}:{endpoint.Port}; " +
            $"Protocol={endpoint.Protocol}");
    }

    public void ClearTransientJoinEndpointOverride(
        string source)
    {
        if (_transientJoinEndpointOverride is null)
        {
            return;
        }

        _transientJoinEndpointOverride = null;

        Plugin.Log.LogInfo(
            $"{source}: cleared runtime join endpoint override.");
    }

    public void ApplyConfiguredPhotonSettings()
    {
        var settings = PhotonNetwork.PhotonServerSettings.AppSettings;

        ApplyLocalServerSettings(settings);
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

            Plugin.Log.LogInfo(
                $"{source}: queued host readiness wait started. " +
                $"Endpoint={_identityAndValidation.SanitizeEndpointForLog(host)}:{port}; " +
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

            Plugin.Log.LogInfo(
                $"{source}: queued host readiness confirmed. " +
                $"Endpoint={_identityAndValidation.SanitizeEndpointForLog(host)}:{port}; " +
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
            Plugin.Log.LogInfo(
                $"{source}: queued host readiness pending. " +
                $"Endpoint={_identityAndValidation.SanitizeEndpointForLog(host)}:{port}; " +
                $"Protocol={protocol}; " +
                $"ElapsedMs={elapsedSinceStartMs}; " +
                $"Attempts={_queuedHostReadinessAttempts}; " +
                $"LastFailure={probeMessage}");
        }

        if (elapsedSinceStartMs < timeoutMs)
        {
            return false;
        }

        Plugin.Log.LogError(
            $"{source}: queued host readiness timed out. " +
            $"Endpoint={_identityAndValidation.SanitizeEndpointForLog(host)}:{port}; " +
            $"Protocol={protocol}; " +
            $"ElapsedMs={elapsedSinceStartMs}; " +
            $"Attempts={_queuedHostReadinessAttempts}; " +
            $"LastFailure={probeMessage}");

        _errorState.ReportStructuredLanError(
            LanErrorClassifier.ClassifyReadinessTimeout(),
            source,
            "Queued host readiness timed out.",
            probeMessage);

        _errorState.NotifyLocalServerNotDetected("readiness timeout");

        WasLastQueuedHostReadinessTimeout = true;
        ResetQueuedHostReadinessWindow();

        return false;
    }

    private void ApplyLocalServerSettings(
        AppSettings settings)
    {
        LocalServerEndpoint endpoint =
            GetEffectiveLocalServerEndpointForConnection();

        string serverAddress = endpoint.Address.Trim();

        if (string.IsNullOrWhiteSpace(serverAddress))
        {
            Plugin.Log.LogError(
                "LocalServerAddress is empty. " +
                "Cannot apply LocalServer mode.");

            return;
        }

        int configuredPort = endpoint.Port;

        if (configuredPort is < 1 or > 65535)
        {
            Plugin.Log.LogError(
                $"LocalServerPort '{configuredPort}' is invalid. " +
                "Expected range 1-65535.");

            return;
        }

        settings.UseNameServer = true;
        settings.Server = serverAddress;
        settings.Port = (ushort)configuredPort;
        settings.Protocol = endpoint.Protocol;
        settings.FixedRegion = string.Empty;

        Plugin.Log.LogInfo(
            "Applied Photon mode LocalServer: " +
            $"Server={serverAddress}; " +
            $"Port={settings.Port}; " +
            $"Protocol={settings.Protocol}; " +
            $"UseNameServer={settings.UseNameServer}; " +
            $"EndpointSource={(IsJoinEndpointOverrideActive ? "join-runtime" : "config")}");
    }
}
