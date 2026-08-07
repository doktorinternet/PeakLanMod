using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using PeakLanMod.Lan.Model;
using PeakLanMod.Lan.Services;
using PeakLanMod.Lan.State;

namespace PeakLanMod.Lan.Discovery;

internal readonly struct LanSessionCompatibility
{
    internal LanSessionCompatibility(
        bool isCompatible,
        string reason)
    {
        IsCompatible = isCompatible;
        Reason = reason;
    }

    internal bool IsCompatible { get; }
    internal string Reason { get; }

    internal static LanSessionCompatibility Compatible =>
        new(true, string.Empty);
}

internal sealed class UdpLanDiscoveryListener : IDisposable
{
    private readonly object _sync = new();
    private readonly LanConnectionStateStore _stateStore;
    private UdpClient? _udpClient;
    private CancellationTokenSource? _cancellationTokenSource;
    private Task? _listenTask;
    private Func<LanDiscoveryAnnouncement, LanSessionCompatibility>? _compatibilityEvaluator;
    private int _port;
    private int _ttlMs;

    internal UdpLanDiscoveryListener(
        LanConnectionStateStore stateStore)
    {
        _stateStore = stateStore;
    }

    internal bool IsRunning
    {
        get
        {
            lock (_sync)
            {
                return _listenTask is not null;
            }
        }
    }

    internal bool TryStart(
        int port,
        int ttlMs,
        Func<LanDiscoveryAnnouncement, LanSessionCompatibility> compatibilityEvaluator,
        out string message)
    {
        if (port is < 1 or > 65535)
        {
            message = "Discovery UDP port must be in range 1-65535.";
            return false;
        }

        if (compatibilityEvaluator is null)
        {
            message = "Compatibility evaluator is null.";
            return false;
        }

        lock (_sync)
        {
            if (_listenTask is not null)
            {
                _port = port;
                _ttlMs = Math.Max(1000, ttlMs);
                _compatibilityEvaluator = compatibilityEvaluator;
                message = "Discovery listener already running; updated runtime settings.";
                return true;
            }

            try
            {
                _udpClient = new UdpClient(AddressFamily.InterNetwork);
                _udpClient.Client.SetSocketOption(
                    SocketOptionLevel.Socket,
                    SocketOptionName.ReuseAddress,
                    true);
                _udpClient.Client.Bind(new IPEndPoint(IPAddress.Any, port));
                _udpClient.Client.ReceiveTimeout = 1000;

                _port = port;
                _ttlMs = Math.Max(1000, ttlMs);
                _compatibilityEvaluator = compatibilityEvaluator;
                _cancellationTokenSource = new CancellationTokenSource();
                _listenTask = Task.Run(ListenLoop);

                message = "Discovery listener started.";
                return true;
            }
            catch (Exception ex)
            {
                DisposeSockets();
                message = $"Failed to start listener: {ex.GetType().Name}: {ex.Message}";
                return false;
            }
        }
    }

    internal void Stop(
        string reason)
    {
        Task? taskToWait;

        lock (_sync)
        {
            _cancellationTokenSource?.Cancel();
            taskToWait = _listenTask;
            _listenTask = null;
        }

        if (taskToWait is not null)
        {
            try
            {
                taskToWait.Wait(1000);
            }
            catch
            {
                // No-op on shutdown path.
            }
        }

        lock (_sync)
        {
            DisposeSockets();
        }

        Plugin.Log.LogInfo(
            $"LAN discovery listener stopped. Reason={reason}");
    }

    private void ListenLoop()
    {
        while (true)
        {
            UdpClient? udpClient;
            CancellationTokenSource? cts;
            Func<LanDiscoveryAnnouncement, LanSessionCompatibility>? compatibilityEvaluator;
            int ttlMs;

            lock (_sync)
            {
                udpClient = _udpClient;
                cts = _cancellationTokenSource;
                compatibilityEvaluator = _compatibilityEvaluator;
                ttlMs = _ttlMs;
            }

            if (udpClient is null || cts is null || compatibilityEvaluator is null)
            {
                return;
            }

            if (cts.IsCancellationRequested)
            {
                return;
            }

            int removed = _stateStore.RemoveExpiredSessions(DateTime.UtcNow);

            if (removed > 0)
            {
                Plugin.Log.LogInfo(
                    $"LAN discovery session TTL eviction removed {removed} stale session(s)." );
            }

            try
            {
                IPEndPoint remote = new(IPAddress.Any, 0);
                byte[] bytes = udpClient.Receive(ref remote);
                string payload = Encoding.UTF8.GetString(bytes);

                if (!LanDiscoveryMessageCodec.TryParseAnnouncement(
                        payload,
                        out LanDiscoveryAnnouncement announcement,
                        out string parseReason))
                {
                    Plugin.Log.LogInfo(
                        "LAN discovery ignored malformed packet. " +
                        $"Reason={parseReason}; " +
                        $"Source={SanitizeSource(remote)}");

                    continue;
                }

                LanSessionCompatibility compatibility =
                    compatibilityEvaluator(announcement);

                DateTime nowUtc = DateTime.UtcNow;
                string sourceAddress = ResolveSourceAddress(remote);
                int sourcePort = remote.Port;

                var session = new LanSessionInfo(
                    key: announcement.SessionKey,
                    roomName: announcement.RoomName,
                    hostDisplayName: announcement.HostDisplayName,
                    sourceAddress: sourceAddress,
                    sourcePort: sourcePort,
                    nameServerAddress: announcement.NameServerAddress,
                    nameServerPort: announcement.NameServerPort,
                    transport: announcement.Transport,
                    scene: announcement.Scene,
                    serverInstanceId: announcement.ServerInstanceId,
                    protocolVersion: announcement.ProtocolVersion,
                    gameVersion: announcement.GameVersion,
                    modVersion: announcement.ModVersion,
                    schemaVersion: announcement.SchemaVersion,
                    sentAtUtc: announcement.SentAtUtc,
                    firstSeenUtc: nowUtc,
                    lastSeenUtc: nowUtc,
                    expiresAtUtc: nowUtc.AddMilliseconds(ttlMs),
                    isCompatible: compatibility.IsCompatible,
                    incompatibilityReason: compatibility.Reason);

                LanSessionUpdateKind updateKind =
                    _stateStore.UpsertDiscoveredSession(session);

                if (updateKind == LanSessionUpdateKind.Added)
                {
                    Plugin.Log.LogInfo(
                        "LAN discovery session added. " +
                        $"Room={announcement.RoomName}; " +
                        $"Host={LanRuntimeContext.Fingerprint(announcement.HostDisplayName)}; " +
                        $"Source={SanitizeSource(remote)}; " +
                        $"Compatible={compatibility.IsCompatible}; " +
                        $"Reason={compatibility.Reason}");
                }
                else if (updateKind == LanSessionUpdateKind.Updated && !compatibility.IsCompatible)
                {
                    Plugin.Log.LogInfo(
                        "LAN discovery session updated (incompatible). " +
                        $"Room={announcement.RoomName}; " +
                        $"Reason={compatibility.Reason}");
                }
            }
            catch (SocketException ex) when (ex.SocketErrorCode == SocketError.TimedOut)
            {
                continue;
            }
            catch (ObjectDisposedException)
            {
                return;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning(
                    "LAN discovery listener receive failed. " +
                    $"Error={ex.GetType().Name}; " +
                    $"Message={ex.Message}");
            }
        }
    }

    private static string SanitizeSource(IPEndPoint endpoint)
    {
        if (endpoint.Address.AddressFamily != AddressFamily.InterNetwork)
        {
            return "<non-ipv4>";
        }

        byte[] bytes = endpoint.Address.GetAddressBytes();
        return $"{bytes[0]}.{bytes[1]}.{bytes[2]}.x:{endpoint.Port}";
    }

    private static string ResolveSourceAddress(
        IPEndPoint endpoint)
    {
        if (endpoint.Address.AddressFamily != AddressFamily.InterNetwork)
        {
            return "<non-ipv4>";
        }

        return endpoint.Address.ToString();
    }

    internal LanSessionInfo[] GetSnapshot()
    {
        return _stateStore
            .GetDiscoveredSessionsSnapshot(DateTime.UtcNow)
            .ToArray();
    }

    private void DisposeSockets()
    {
        _cancellationTokenSource?.Dispose();
        _cancellationTokenSource = null;

        _udpClient?.Dispose();
        _udpClient = null;

        _compatibilityEvaluator = null;
        _port = 0;
        _ttlMs = 0;
    }

    public void Dispose()
    {
        Stop("Dispose");
    }
}
