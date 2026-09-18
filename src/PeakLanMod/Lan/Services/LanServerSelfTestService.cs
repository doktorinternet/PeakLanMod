using BepInEx;
using PeakLanMod.Lan.Model;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace PeakLanMod.Lan.Services;

// Runs a one-shot, best-effort local Luxon server smoke test at startup so the player can see
// whether their local server setup works before they try to host. Never leaves a test-only
// process running: it only stops a process it started itself for the test.
internal sealed class LanServerSelfTestService : ILanServerSelfTestService
{
    private const int ExistingServerProbeTimeoutMs = 800;
    private const int RemoteHeartbeatIntervalMs = 5000;

    private readonly ILanPluginOptions _options;
    private readonly ILanClientEventLog _clientEventLog;
    private int _hasStarted;
    private volatile LanServerSelfTestStatus _status = LanServerSelfTestStatus.NotRun;
    private volatile string _resultMessage = string.Empty;
    private volatile bool _isLocalTarget = true;
    private CancellationTokenSource? _heartbeatCts;

    internal LanServerSelfTestService(
        ILanPluginOptions options,
        ILanClientEventLog clientEventLog)
    {
        _options = options;
        _clientEventLog = clientEventLog;
    }

    public LanServerSelfTestStatus Status => _status;

    public string ResultMessage => _resultMessage;

    // Remote reachability can never be fully verified (see RemoteServerUnreachableInitial), so a
    // remote Failed result should not block hosting the way a local Failed result does - only local
    // gates the Host button; callers should check this alongside Status.
    public bool IsLocalTarget => _isLocalTarget;

    public void RunStartupSelfTest(string source)
    {
        if (Interlocked.CompareExchange(ref _hasStarted, 1, 0) != 0)
        {
            return;
        }

        _status = LanServerSelfTestStatus.Running;

        Task.Run(() => ExecuteSelfTest(source));
    }

    private void ExecuteSelfTest(string source)
    {
        try
        {
            string host = _options.LanServerAddress.Value.Trim();
            int port = _options.LanServerPort.Value;
            var protocol = _options.LanServerProtocol.Value;
            int httpProbePort = _options.LanServerHttpProbePort.Value;
            bool isLocalTarget = LuxonReadinessProbe.IsLocalHost(host);
            _isLocalTarget = isLocalTarget;

            Plugin.Log.LogInfo(
                $"{source}: server startup self-test starting. IsLocalTarget={isLocalTarget}");
            _clientEventLog.Log(LanClientLogMessages.ConnectModeConfigured(isLocalTarget));
            _clientEventLog.Log(LanClientLogMessages.ServerSelfTestStarted());

            if (!isLocalTarget)
            {
                (bool reachable, string probeMessage) = ProbeRemote(
                    host,
                    port,
                    protocol,
                    httpProbePort);

                Complete(
                    source,
                    reachable ? LanServerSelfTestStatus.Passed : LanServerSelfTestStatus.Failed,
                    probeMessage,
                    reachable
                        ? LanClientLogMessages.RemoteServerReachableInitial()
                        : LanClientLogMessages.RemoteServerUnreachableInitial(probeMessage));

                // The user only cares about this one configured remote address (not every session
                // on the LAN), so keep re-checking it in the background: if it comes up/goes down
                // later, the Host button and log panel should reflect that without a game restart.
                RunRemoteHeartbeat(source, host, port, protocol, httpProbePort);
                return;
            }

            if (LuxonReadinessProbe.TryProbeNameServer(
                    host,
                    port,
                    protocol,
                    httpProbePort,
                    ExistingServerProbeTimeoutMs,
                    out string existingMessage))
            {
                Complete(
                    source,
                    LanServerSelfTestStatus.Passed,
                    $"Detected an already-running local server. {existingMessage}",
                    LanClientLogMessages.LocalServerReady());
                return;
            }

            if (!_options.AutoStartLanServerOnHost.Value)
            {
                const string reason =
                    "No local server was detected and AutoStartLanServerOnHost is disabled, " +
                    "so it was not started for the test.";

                Complete(
                    source,
                    LanServerSelfTestStatus.Failed,
                    reason,
                    LanClientLogMessages.LocalServerNotReady(reason));
                return;
            }

            string executablePath = _options.LanServerExecutablePath.Value.Trim();
            string workingDirectory = _options.LanServerWorkingDirectory.Value.Trim();
            string startArguments = _options.LanServerStartArguments.Value;

            if (!LuxonProcessController.TryEnsureRunning(
                    executablePath,
                    Paths.ConfigPath,
                    workingDirectory,
                    startArguments,
                    out LuxonProcessEnsureResult ensureResult))
            {
                string reason =
                    $"Could not start the local server executable at '{ensureResult.ExecutablePathForLog}' " +
                    $"({ensureResult.Message}).";

                Complete(
                    source,
                    LanServerSelfTestStatus.Failed,
                    reason,
                    LanClientLogMessages.LocalServerNotReady(reason));
                _clientEventLog.Log(LanClientLogMessages.ServerSelfTestDisregardIfNotHosting());
                return;
            }

            int readinessTimeoutMs = Math.Max(0, _options.LanServerReadinessTimeoutMs.Value);
            int readinessPollIntervalMs = Math.Max(50, _options.LanServerReadinessPollIntervalMs.Value);

            bool becameReady = LuxonReadinessProbe.TryWaitForNameServerReady(
                host,
                port,
                protocol,
                httpProbePort,
                readinessTimeoutMs,
                readinessPollIntervalMs,
                out LanServerReadinessResult readinessResult);

            if (ensureResult.StartedByPlugin)
            {
                int stopTimeoutMs = Math.Max(0, _options.OwnedLanServerStopTimeoutMs.Value);

                LuxonProcessController.TryStopOwnedProcess(
                    stopTimeoutMs,
                    forceKill: true,
                    out string stopMessage);

                Plugin.Log.LogInfo(
                    $"{source}: local server startup self-test stopped its temporary process. {stopMessage}");
            }

            if (!becameReady)
            {
                string reason =
                    $"Local server process was able to start but never responded " +
                    $"({readinessResult.LastFailureMessage}).";

                Complete(
                    source,
                    LanServerSelfTestStatus.Failed,
                    reason,
                    LanClientLogMessages.LocalServerNotReady(reason));
                return;
            }

            Complete(
                source,
                LanServerSelfTestStatus.Passed,
                "Local server is operational.",
                LanClientLogMessages.LocalServerReady());
        }
        catch (Exception ex)
        {
            string reason = $"Unexpected error: {ex.GetType().Name}: {ex.Message}";
            bool isLocalTarget = LuxonReadinessProbe.IsLocalHost(
                _options.LanServerAddress.Value.Trim());
            _isLocalTarget = isLocalTarget;

            Complete(
                source,
                LanServerSelfTestStatus.Failed,
                reason,
                isLocalTarget
                    ? LanClientLogMessages.LocalServerNotReady(reason)
                    : LanClientLogMessages.RemoteServerUnreachableInitial(reason));
        }
    }

    private static (bool Reachable, string Message) ProbeRemote(
        string host,
        int port,
        ExitGames.Client.Photon.ConnectionProtocol protocol,
        int httpProbePort)
    {
        bool reachable = LuxonReadinessProbe.TryProbeNameServer(
            host,
            port,
            protocol,
            httpProbePort,
            ExistingServerProbeTimeoutMs,
            out string probeMessage);

        return (reachable, probeMessage);
    }

    private void RunRemoteHeartbeat(
        string source,
        string host,
        int port,
        ExitGames.Client.Photon.ConnectionProtocol protocol,
        int httpProbePort)
    {
        var cts = new CancellationTokenSource();
        _heartbeatCts = cts;
        CancellationToken token = cts.Token;

        Plugin.Log.LogInfo(
            $"{source}: remote server heartbeat starting. IntervalMs={RemoteHeartbeatIntervalMs}");

        while (!token.IsCancellationRequested)
        {
            try
            {
                Task.Delay(RemoteHeartbeatIntervalMs, token).Wait(token);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            if (token.IsCancellationRequested)
            {
                break;
            }

            try
            {
                (bool reachable, string probeMessage) = ProbeRemote(
                    host,
                    port,
                    protocol,
                    httpProbePort);

                LanServerSelfTestStatus newStatus = reachable
                    ? LanServerSelfTestStatus.Passed
                    : LanServerSelfTestStatus.Failed;

                // Only react to an actual change: logging every 5 seconds forever while nothing
                // changed would flood both the BepInEx log and the in-game LOG panel.
                if (newStatus == _status)
                {
                    continue;
                }

                Complete(
                    $"{source}.Heartbeat",
                    newStatus,
                    probeMessage,
                    reachable
                        ? LanClientLogMessages.RemoteServerConnectionRegained()
                        : LanClientLogMessages.RemoteServerConnectionLost());
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning(
                    $"{source}: remote server heartbeat check failed: {ex.GetType().Name}: {ex.Message}");
            }
        }

        Plugin.Log.LogInfo($"{source}: remote server heartbeat stopped.");
    }

    public void StopHeartbeat(string source)
    {
        CancellationTokenSource? cts = _heartbeatCts;
        _heartbeatCts = null;

        if (cts is null)
        {
            return;
        }

        Plugin.Log.LogInfo($"{source}: stopping remote server heartbeat.");
        cts.Cancel();
        cts.Dispose();
    }

    private void Complete(
        string source,
        LanServerSelfTestStatus status,
        string diagnosticMessage,
        string clientMessage)
    {
        _resultMessage = diagnosticMessage;
        _status = status;

        if (status == LanServerSelfTestStatus.Passed)
        {
            Plugin.Log.LogInfo($"{source}: server self-test passed. {diagnosticMessage}");
        }
        else
        {
            Plugin.Log.LogWarning($"{source}: server self-test failed. {diagnosticMessage}");
        }

        _clientEventLog.Log(clientMessage);
    }
}
