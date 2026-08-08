using BepInEx;
using ExitGames.Client.Photon;
using PeakLanMod.Lan.Diagnostics;
using PeakLanMod.Lan.Model;
using Photon.Pun;
using Photon.Realtime;
using System;

namespace PeakLanMod.Lan.Services;

internal sealed class LanServerRuntimeService : ILanServerRuntimeService
{
    private readonly ILanPluginOptions _options;
    private readonly ILanErrorStateService _errorState;
    private readonly ILanIdentityAndValidation _identityAndValidation;
    private LanServerEndpoint? _transientJoinEndpointOverride;
    private DateTime _queuedHostReadinessStartedAtUtc;
    private int _queuedHostReadinessAttempts;

    internal LanServerRuntimeService(
        ILanPluginOptions options,
        ILanErrorStateService errorState,
        ILanIdentityAndValidation identityAndValidation)
    {
        _options = options;
        _errorState = errorState;
        _identityAndValidation = identityAndValidation;
    }

    public bool EnsureHostLanServerProcess()
    {
        if (!LanRuntimeContext.IsLanServerMode)
        {
            return true;
        }

        if (!_options.AutoStartLanServerOnHost.Value)
        {
            return true;
        }

        string executablePath = _options.LanServerExecutablePath.Value.Trim();
        string workingDirectory = _options.LanServerWorkingDirectory.Value.Trim();
        string startArguments = _options.LanServerStartArguments.Value;

        if (!LuxonProcessController.TryEnsureRunning(
                executablePath,
                Paths.ConfigPath,
                workingDirectory,
                startArguments,
                out LuxonProcessEnsureResult result))
        {
            _errorState.ReportStructuredLanError(
                LanErrorClassifier.ClassifyAutoStartFailure(),
                source: "EnsureHostLanServerProcess",
                message: "Local server process start/attach failed.",
                context: result.Message);

            Plugin.Log.LogError(
                "Local server host auto-start failed. " +
                $"Executable={result.ExecutablePathForLog}; " +
                $"WorkingDirectory={result.WorkingDirectoryForLog}; " +
                $"Reason={result.Message}");

            _errorState.NotifyLanServerNotDetected("auto-start failed");
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

    public void StopOwnedLanServerProcessOnExit(
        string source)
    {
        if (!LanRuntimeContext.IsLanServerMode)
        {
            return;
        }

        if (!_options.AutoStopOwnedLanServerOnExit.Value)
        {
            Plugin.Log.LogInfo(
                $"{source}: owned local server stop on exit is disabled.");
            return;
        }

        int timeoutMs = Math.Max(0, _options.OwnedLanServerStopTimeoutMs.Value);
        bool forceKill = _options.ForceKillOwnedLanServerOnExit.Value;

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
        if (!LanRuntimeContext.IsLanServerMode)
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
                $"KeepingLanServerAddress={_identityAndValidation.SanitizeEndpointForLog(_options.LanServerAddress.Value)}");

            return;
        }

        string previousAddress =
            _options.LanServerAddress.Value.Trim();

        if (string.Equals(
                previousAddress,
                selectedIpv4,
                StringComparison.OrdinalIgnoreCase))
        {
            Plugin.Log.LogInfo(
                "Host LAN IPv4 selection kept existing LanServerAddress. " +
                $"Selected={_identityAndValidation.SanitizeEndpointForLog(selectedIpv4)}; " +
                $"SelectionReason={reason}");

            return;
        }

        _options.LanServerAddress.Value = selectedIpv4;

        Plugin.Log.LogInfo(
            "Host LAN IPv4 selection updated LanServerAddress. " +
            $"Previous={_identityAndValidation.SanitizeEndpointForLog(previousAddress)}; " +
            $"Selected={_identityAndValidation.SanitizeEndpointForLog(selectedIpv4)}; " +
            $"SelectedFingerprint={_identityAndValidation.Fingerprint(selectedIpv4)}; " +
            $"SelectionReason={reason}");
    }

    public void ApplyHostLuxonConfigAutomation()
    {
        if (!LanRuntimeContext.IsLanServerMode)
        {
            return;
        }

        if (!_options.AutoUpdateLuxonConfigOnHost.Value)
        {
            return;
        }

        string endpointHost = _options.LanServerAddress.Value.Trim();
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

    public bool EnsureLanServerReadinessBeforeConnect(
        string source,
        bool queuedHostFlow,
        LanServerEndpoint? endpointOverride = null)
    {
        if (!LanRuntimeContext.IsLanServerMode)
        {
            return true;
        }

        if (!_options.EnableLanServerReadinessCheck.Value)
        {
            return true;
        }

        int timeoutMs = Math.Max(0, _options.LanServerReadinessTimeoutMs.Value);
        int pollIntervalMs = Math.Max(50, _options.LanServerReadinessPollIntervalMs.Value);

        LanServerEndpoint endpoint = endpointOverride
            ?? GetConfiguredLanServerEndpoint();

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
                out LanServerReadinessResult result))
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

            _errorState.NotifyLanServerNotDetected("readiness timeout");
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
    }

    public string GetConfiguredLocalEndpoint()
    {
        string address = _options.LanServerAddress.Value.Trim();
        int port = _options.LanServerPort.Value;
        ConnectionProtocol protocol = _options.LanServerProtocol.Value;

        return $"{address}:{port} ({protocol})";
    }

    public string GetEffectiveLocalEndpoint()
    {
        LanServerEndpoint endpoint =
            GetEffectiveLanServerEndpointForConnection();

        return $"{endpoint.Address}:{endpoint.Port} ({endpoint.Protocol})";
    }

    public LanServerEndpoint GetConfiguredLanServerEndpoint()
    {
        string address = _options.LanServerAddress.Value.Trim();
        int port = _options.LanServerPort.Value;
        ConnectionProtocol protocol = _options.LanServerProtocol.Value;

        return new LanServerEndpoint(address, port, protocol);
    }

    public LanServerEndpoint GetEffectiveLanServerEndpointForConnection()
    {
        return _transientJoinEndpointOverride
            ?? GetConfiguredLanServerEndpoint();
    }

    public bool IsJoinEndpointOverrideActive =>
        _transientJoinEndpointOverride is not null;

    public void ApplyTransientJoinEndpointOverride(
        LanServerEndpoint endpoint,
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

        ApplyLanServerSettings(settings);
    }

    public void DumpPhotonSettings(string source)
    {
        var settings = PhotonNetwork.PhotonServerSettings.AppSettings;

        Plugin.Log.LogInfo(
            $"Photon settings [{source}]: " +
            $"UseNameServer={settings.UseNameServer}; " +
            $"Server={settings.Server ?? "<null>"}; " +
            $"Port={settings.Port}; " +
            $"Protocol={settings.Protocol}; " +
            $"FixedRegion={settings.FixedRegion ?? "<null>"}; " +
            $"AppVersion={settings.AppVersion ?? "<null>"}");
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

        Plugin.Log.LogWarning(
            $"{source}: queued host readiness window elapsed. " +
            $"Endpoint={_identityAndValidation.SanitizeEndpointForLog(host)}:{port}; " +
            $"Protocol={protocol}; " +
            $"ElapsedMs={elapsedSinceStartMs}; " +
            $"Attempts={_queuedHostReadinessAttempts}; " +
            $"LastFailure={probeMessage}; " +
            "Continuing queued host wait.");

        // Queued host flow keeps waiting across timeout windows so a single host press
        // can cover process startup and eventual NameServer readiness.
        ResetQueuedHostReadinessWindow();

        return false;
    }

    private void ApplyLanServerSettings(
        AppSettings settings)
    {
        LanServerEndpoint endpoint =
            GetEffectiveLanServerEndpointForConnection();

        string serverAddress = endpoint.Address.Trim();

        if (string.IsNullOrWhiteSpace(serverAddress))
        {
            Plugin.Log.LogError(
                "LanServerAddress is empty. " +
                "Cannot apply LanServer mode.");

            return;
        }

        int configuredPort = endpoint.Port;

        if (configuredPort is < 1 or > 65535)
        {
            Plugin.Log.LogError(
                $"LanServerPort '{configuredPort}' is invalid. " +
                "Expected range 1-65535.");

            return;
        }

        settings.UseNameServer = true;
        settings.Server = serverAddress;
        settings.Port = (ushort)configuredPort;
        settings.Protocol = endpoint.Protocol;
        settings.FixedRegion = string.Empty;

        Plugin.Log.LogInfo(
            "Applied Photon mode LanServer: " +
            $"Server={serverAddress}; " +
            $"Port={settings.Port}; " +
            $"Protocol={settings.Protocol}; " +
            $"UseNameServer={settings.UseNameServer}; " +
            $"EndpointSource={(IsJoinEndpointOverrideActive ? "join-runtime" : "config")}");
    }
}
