using ExitGames.Client.Photon;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace PeakLanMod.Lan.Services;

internal readonly struct LanServerReadinessResult
{
    private LanServerReadinessResult(
        bool success,
        int attemptCount,
        int elapsedMilliseconds,
        string successMessage,
        string lastFailureMessage)
    {
        Success = success;
        AttemptCount = attemptCount;
        ElapsedMilliseconds = elapsedMilliseconds;
        SuccessMessage = successMessage;
        LastFailureMessage = lastFailureMessage;
    }

    internal bool Success { get; }
    internal int AttemptCount { get; }
    internal int ElapsedMilliseconds { get; }
    internal string SuccessMessage { get; }
    internal string LastFailureMessage { get; }

    internal static LanServerReadinessResult CreateSuccess(
        int attemptCount,
        int elapsedMilliseconds,
        string successMessage)
    {
        return new LanServerReadinessResult(
            success: true,
            attemptCount,
            elapsedMilliseconds,
            successMessage,
            lastFailureMessage: string.Empty);
    }

    internal static LanServerReadinessResult CreateFailure(
        int attemptCount,
        int elapsedMilliseconds,
        string lastFailureMessage)
    {
        return new LanServerReadinessResult(
            success: false,
            attemptCount,
            elapsedMilliseconds,
            successMessage: string.Empty,
            lastFailureMessage);
    }
}

internal static class LuxonReadinessProbe
{
    // Reused for the lifetime of the plugin rather than created per probe: HttpClient is designed to
    // be a long-lived, shared instance (per-call new/dispose leads to socket churn under repeated use,
    // e.g. LanServerSelfTestService's periodic remote-reachability heartbeat).
    private static readonly HttpClient SharedHttpClient = new();

    internal static bool TryWaitForNameServerReady(
        string host,
        int port,
        ConnectionProtocol protocol,
        int httpProbePort,
        int timeoutMs,
        int pollIntervalMs,
        out LanServerReadinessResult result)
    {
        int effectiveTimeoutMs = Math.Max(0, timeoutMs);
        int effectivePollMs = Math.Max(50, pollIntervalMs);
        int perAttemptTimeoutMs = Math.Max(
            100,
            Math.Min(effectivePollMs, 1000));

        var stopwatch = Stopwatch.StartNew();
        int attempts = 0;
        string lastFailure = "Probe did not run.";

        while (true)
        {
            attempts++;

            if (TryProbeNameServer(
                    host,
                    port,
                    protocol,
                    httpProbePort,
                    perAttemptTimeoutMs,
                    out string message))
            {
                result = LanServerReadinessResult.CreateSuccess(
                    attempts,
                    (int)stopwatch.ElapsedMilliseconds,
                    message);
                return true;
            }

            lastFailure = message;

            if (stopwatch.ElapsedMilliseconds >= effectiveTimeoutMs)
            {
                result = LanServerReadinessResult.CreateFailure(
                    attempts,
                    (int)stopwatch.ElapsedMilliseconds,
                    lastFailure);
                return false;
            }

            int remainingMs = effectiveTimeoutMs - (int)stopwatch.ElapsedMilliseconds;

            if (remainingMs <= 0)
            {
                result = LanServerReadinessResult.CreateFailure(
                    attempts,
                    (int)stopwatch.ElapsedMilliseconds,
                    lastFailure);
                return false;
            }

            int delayMs = Math.Min(effectivePollMs, remainingMs);
            Task.Delay(delayMs).Wait();
        }
    }

    internal static bool TryProbeNameServer(
        string host,
        int port,
        ConnectionProtocol protocol,
        int httpProbePort,
        int probeTimeoutMs,
        out string message,
        bool allowBlockingHttpProbe = true)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            message = "Host is empty.";
            return false;
        }

        if (port is < 1 or > 65535)
        {
            message = "Port is outside 1-65535.";
            return false;
        }

        int effectiveTimeoutMs = Math.Max(100, probeTimeoutMs);

        switch (protocol)
        {
            case ConnectionProtocol.Udp:
                return TryProbeUdp(host, port, httpProbePort, effectiveTimeoutMs, out message, allowBlockingHttpProbe);

            case ConnectionProtocol.Tcp:
            case ConnectionProtocol.WebSocket:
            case ConnectionProtocol.WebSocketSecure:
                return TryProbeTcp(host, port, effectiveTimeoutMs, out message);

            default:
                message = $"Unsupported protocol for readiness probe: {protocol}";
                return false;
        }
    }

    private static bool TryProbeTcp(
        string host,
        int port,
        int timeoutMs,
        out string message)
    {
        try
        {
            using var tcpClient = new TcpClient();
            Task connectTask = tcpClient.ConnectAsync(host, port);

            if (!connectTask.Wait(timeoutMs))
            {
                message = $"TCP connect timed out after {timeoutMs}ms.";
                return false;
            }

            if (!tcpClient.Connected)
            {
                message = "TCP connect completed without connected state.";
                return false;
            }

            message = "TCP connect succeeded.";
            return true;
        }
        catch (Exception ex)
        {
            message = $"TCP probe failed: {ex.GetType().Name}: {ex.Message}";
            return false;
        }
    }

    private static bool TryProbeUdp(
        string host,
        int port,
        int httpProbePort,
        int timeoutMs,
        out string message,
        bool allowBlockingHttpProbe)
    {
        if (!TryResolveHostIpv4(host, out IPAddress address, out string resolveMessage))
        {
            message = resolveMessage;
            return false;
        }

        // UDP Send() succeeds even with nothing listening, so on the local machine we can check
        // the OS listener table directly instead of trusting a one-way send.
        if (IsLocalAddress(address))
        {
            return TryProbeLocalUdpListener(port, out message);
        }

        // Luxon's UDP proxies turned out not to actually listen in practice (verified with
        // Get-NetTCPConnection), and a raw UDP send/receive can't reliably prove remote reachability
        // either (silent firewall drops look identical to "nothing is listening", so any fallback
        // guess there is effectively always "yes" and masks real outages). Its HTTP web interface
        // (config.yml HTTP.port, default 5088) is the one signal that's actually been reliable in
        // testing, so that's the sole remote reachability check now - no UDP fallback.
        //
        // allowBlockingHttpProbe=false is used by the queued per-tick host/join readiness polling
        // (main thread, called every Update()): a real HTTP round-trip there would still stall the
        // frame, so those callers get the cached, fire-and-forget variant instead.
        return allowBlockingHttpProbe
            ? TryProbeHttp(host, httpProbePort, timeoutMs, out message)
            : TryProbeHttpNonBlocking(host, httpProbePort, timeoutMs, out message);
    }

    internal static bool TryProbeHttp(
        string host,
        int port,
        int timeoutMs,
        out string message)
    {
        if (port is < 1 or > 65535)
        {
            message = "HTTP probe port is outside 1-65535.";
            return false;
        }

        // Deliberately-nonexistent path: Luxon's "/" serves a ~30KB HTML dashboard (measured), which
        // is wasteful to fetch repeatedly, while an unknown path reliably 404s with ~21 bytes. Either
        // way we get a real HTTP exchange (proving it's genuinely Luxon's web interface, not just some
        // other service on the port), so the tiny response is strictly better for repeated probing.
        try
        {
            using var timeoutCts = new CancellationTokenSource(Math.Max(100, timeoutMs));

            // Run on a thread-pool thread instead of awaiting inline: this method is sometimes called
            // synchronously from the main thread, and blocking there while HttpClient's internal
            // continuations try to resume on a captured UI SynchronizationContext could deadlock.
            using HttpResponseMessage response = Task
                .Run(
                    () => SharedHttpClient.GetAsync(
                        $"http://{host}:{port}/__peaklanmod_probe__",
                        timeoutCts.Token),
                    timeoutCts.Token)
                .GetAwaiter()
                .GetResult();

            // Any completed HTTP exchange (regardless of status code, typically 404 here) proves the
            // web interface is genuinely up and speaking HTTP.
            message = $"HTTP probe reached the web interface (status {(int)response.StatusCode}).";
            return true;
        }
        catch (Exception ex)
        {
            message = $"HTTP probe failed: {ex.GetType().Name}: {ex.Message}";
            return false;
        }
    }

    // Keyed by "host:port": lets the queued per-tick readiness checks (main thread) read the latest
    // known result instantly instead of blocking on a real HTTP round-trip on every call.
    private static readonly ConcurrentDictionary<string, HttpProbeCacheEntry> HttpProbeCache = new();

    private sealed class HttpProbeCacheEntry
    {
        internal volatile bool IsProbeInFlight;
        internal volatile bool LastResult;
        internal volatile string LastMessage = "HTTP probe has not completed yet.";
    }

    private static bool TryProbeHttpNonBlocking(
        string host,
        int port,
        int timeoutMs,
        out string message)
    {
        HttpProbeCacheEntry entry = HttpProbeCache.GetOrAdd(
            $"{host}:{port}",
            static _ => new HttpProbeCacheEntry());

        if (!entry.IsProbeInFlight)
        {
            entry.IsProbeInFlight = true;

            Task.Run(() =>
            {
                entry.LastResult = TryProbeHttp(host, port, timeoutMs, out string probeMessage);
                entry.LastMessage = probeMessage;
                entry.IsProbeInFlight = false;
            });
        }

        message = entry.LastMessage;
        return entry.LastResult;
    }

    private static bool IsLocalAddress(
        IPAddress address)
    {
        return IPAddress.IsLoopback(address)
            || LocalMachineAddresses.Value.Any(candidate => candidate.Equals(address));
    }

    // Cached once: the local machine's own addresses don't change mid-session, and this check runs
    // on a hot polling path (every readiness probe against a local target).
    private static readonly Lazy<IPAddress[]> LocalMachineAddresses = new(() =>
    {
        try
        {
            return Dns.GetHostAddresses(Dns.GetHostName());
        }
        catch
        {
            return [];
        }
    });

    internal static bool IsLocalHost(
        string host)
    {
        return TryResolveHostIpv4(host, out IPAddress address, out _)
            && IsLocalAddress(address);
    }

    private static bool TryProbeLocalUdpListener(
        int port,
        out string message)
    {
        try
        {
            IPEndPoint[] listeners = IPGlobalProperties
                .GetIPGlobalProperties()
                .GetActiveUdpListeners();

            bool isListening = listeners.Any(endpoint => endpoint.Port == port);

            if (isListening)
            {
                message = "Found an active local UDP listener on the target port.";
                return true;
            }

            message = "No active local UDP listener was found on the target port.";
            return false;
        }
        catch (Exception ex)
        {
            message = $"Local UDP listener check failed: {ex.GetType().Name}: {ex.Message}";
            return false;
        }
    }

    private static bool TryResolveHostIpv4(
        string host,
        out IPAddress address,
        out string message)
    {
        if (IPAddress.TryParse(host, out IPAddress? parsed))
        {
            if (parsed.AddressFamily == AddressFamily.InterNetwork)
            {
                address = parsed;
                message = "IPv4 parsed.";
                return true;
            }

            address = IPAddress.None;
            message = "Parsed address is not IPv4.";
            return false;
        }

        try
        {
            IPAddress[] resolved = Dns.GetHostAddresses(host);
            IPAddress? ipv4 = resolved.FirstOrDefault(
                current => current.AddressFamily == AddressFamily.InterNetwork);

            if (ipv4 is null)
            {
                address = IPAddress.None;
                message = "DNS resolved no IPv4 address.";
                return false;
            }

            address = ipv4;
            message = "DNS resolved IPv4.";
            return true;
        }
        catch (Exception ex)
        {
            address = IPAddress.None;
            message = $"DNS resolution failed: {ex.GetType().Name}: {ex.Message}";
            return false;
        }
    }
}
