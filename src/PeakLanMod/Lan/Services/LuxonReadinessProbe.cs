using ExitGames.Client.Photon;
using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
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
    internal static bool TryWaitForNameServerReady(
        string host,
        int port,
        ConnectionProtocol protocol,
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
        int probeTimeoutMs,
        out string message)
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
                return TryProbeUdp(host, port, effectiveTimeoutMs, out message);

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
        int timeoutMs,
        out string message)
    {
        if (!TryResolveHostIpv4(host, out IPAddress address, out string resolveMessage))
        {
            message = resolveMessage;
            return false;
        }

        try
        {
            using var socket = new Socket(
                AddressFamily.InterNetwork,
                SocketType.Dgram,
                ProtocolType.Udp)
            {
                SendTimeout = timeoutMs,
                ReceiveTimeout = timeoutMs
            };

            socket.Connect(new IPEndPoint(address, port));

            byte[] payload = [0x00];
            int bytesSent = socket.Send(payload);

            if (bytesSent <= 0)
            {
                message = "UDP probe send did not send bytes.";
                return false;
            }

            message = "UDP datagram send succeeded.";
            return true;
        }
        catch (Exception ex)
        {
            message = $"UDP probe failed: {ex.GetType().Name}: {ex.Message}";
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
