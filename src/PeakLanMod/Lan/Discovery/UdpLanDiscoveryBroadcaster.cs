using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace PeakLanMod.Lan.Discovery;

internal readonly struct LanDiscoveryAnnouncement
{
    internal LanDiscoveryAnnouncement(
        string type,
        int schemaVersion,
        string protocolVersion,
        string gameVersion,
        string modVersion,
        string roomName,
        string hostDisplayName,
        string nameServerAddress,
        int nameServerPort,
        string transport,
        string scene,
        string serverInstanceId,
        DateTime sentAtUtc)
    {
        Type = type;
        SchemaVersion = schemaVersion;
        ProtocolVersion = protocolVersion;
        GameVersion = gameVersion;
        ModVersion = modVersion;
        RoomName = roomName;
        HostDisplayName = hostDisplayName;
        NameServerAddress = nameServerAddress;
        NameServerPort = nameServerPort;
        Transport = transport;
        Scene = scene;
        ServerInstanceId = serverInstanceId;
        SentAtUtc = sentAtUtc;
    }

    internal string Type { get; }
    internal int SchemaVersion { get; }
    internal string ProtocolVersion { get; }
    internal string GameVersion { get; }
    internal string ModVersion { get; }
    internal string RoomName { get; }
    internal string HostDisplayName { get; }
    internal string NameServerAddress { get; }
    internal int NameServerPort { get; }
    internal string Transport { get; }
    internal string Scene { get; }
    internal string ServerInstanceId { get; }
    internal DateTime SentAtUtc { get; }

    internal string SessionKey =>
        $"{ServerInstanceId}|{RoomName}";
}

internal static class LanDiscoveryMessageCodec
{
    internal const string AnnouncementType = "peak_lan_announce";
    internal const int SchemaVersionV1 = 1;

    private static readonly Regex StringPropertyRegex =
        new("\"(?<key>[a-zA-Z0-9_]+)\"\\s*:\\s*\"(?<value>(?:\\\\.|[^\"])*)\"", RegexOptions.Compiled);

    private static readonly Regex IntPropertyRegex =
        new("\"(?<key>[a-zA-Z0-9_]+)\"\\s*:\\s*(?<value>-?[0-9]+)", RegexOptions.Compiled);

    internal static string SerializeAnnouncement(
        LanDiscoveryAnnouncement announcement)
    {
        var builder = new StringBuilder(512);
        builder.Append('{');
        AppendString(builder, "type", announcement.Type);
        builder.Append(',');
        AppendInt(builder, "schema_version", announcement.SchemaVersion);
        builder.Append(',');
        AppendString(builder, "protocol_version", announcement.ProtocolVersion);
        builder.Append(',');
        AppendString(builder, "game_version", announcement.GameVersion);
        builder.Append(',');
        AppendString(builder, "mod_version", announcement.ModVersion);
        builder.Append(',');
        AppendString(builder, "room_name", announcement.RoomName);
        builder.Append(',');
        AppendString(builder, "host_display_name", announcement.HostDisplayName);
        builder.Append(',');
        AppendString(builder, "nameserver_address", announcement.NameServerAddress);
        builder.Append(',');
        AppendInt(builder, "nameserver_port", announcement.NameServerPort);
        builder.Append(',');
        AppendString(builder, "transport", announcement.Transport);
        builder.Append(',');
        AppendString(builder, "scene", announcement.Scene);
        builder.Append(',');
        AppendString(builder, "server_instance_id", announcement.ServerInstanceId);
        builder.Append(',');
        AppendString(builder, "sent_at_utc", announcement.SentAtUtc.ToUniversalTime().ToString("O"));
        builder.Append('}');
        return builder.ToString();
    }

    internal static bool TryParseAnnouncement(
        string payload,
        out LanDiscoveryAnnouncement announcement,
        out string reason)
    {
        announcement = default;
        reason = string.Empty;

        if (string.IsNullOrWhiteSpace(payload))
        {
            reason = "Payload is empty.";
            return false;
        }

        if (!TryReadString(payload, "type", out string type)
            || !string.Equals(type, AnnouncementType, StringComparison.Ordinal))
        {
            reason = "Unsupported announcement type.";
            return false;
        }

        if (!TryReadInt(payload, "schema_version", out int schemaVersion))
        {
            reason = "Missing schema_version.";
            return false;
        }

        if (!TryReadString(payload, "protocol_version", out string protocolVersion)
            || !TryReadString(payload, "game_version", out string gameVersion)
            || !TryReadString(payload, "mod_version", out string modVersion)
            || !TryReadString(payload, "room_name", out string roomName)
            || !TryReadString(payload, "host_display_name", out string hostDisplayName)
            || !TryReadString(payload, "nameserver_address", out string nameServerAddress)
            || !TryReadInt(payload, "nameserver_port", out int nameServerPort)
            || !TryReadString(payload, "transport", out string transport)
            || !TryReadString(payload, "scene", out string scene)
            || !TryReadString(payload, "server_instance_id", out string serverInstanceId)
            || !TryReadString(payload, "sent_at_utc", out string sentAtUtcText))
        {
            reason = "Announcement is missing one or more required fields.";
            return false;
        }

        if (!DateTime.TryParse(
                sentAtUtcText,
                provider: null,
                System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal,
                out DateTime sentAtUtc))
        {
            reason = "Invalid sent_at_utc timestamp.";
            return false;
        }

        if (nameServerPort is < 1 or > 65535)
        {
            reason = "nameserver_port is outside 1-65535.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(roomName)
            || string.IsNullOrWhiteSpace(serverInstanceId))
        {
            reason = "room_name or server_instance_id is empty.";
            return false;
        }

        announcement = new LanDiscoveryAnnouncement(
            type,
            schemaVersion,
            protocolVersion,
            gameVersion,
            modVersion,
            roomName,
            hostDisplayName,
            nameServerAddress,
            nameServerPort,
            transport,
            scene,
            serverInstanceId,
            sentAtUtc.ToUniversalTime());

        return true;
    }

    private static void AppendString(
        StringBuilder builder,
        string key,
        string value)
    {
        builder.Append('"');
        builder.Append(key);
        builder.Append("\":\"");
        builder.Append(EscapeJson(value));
        builder.Append('"');
    }

    private static void AppendInt(
        StringBuilder builder,
        string key,
        int value)
    {
        builder.Append('"');
        builder.Append(key);
        builder.Append("\":");
        builder.Append(value);
    }

    private static string EscapeJson(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(value.Length + 8);

        foreach (char current in value)
        {
            switch (current)
            {
                case '"':
                    builder.Append("\\\"");
                    break;
                case '\\':
                    builder.Append("\\\\");
                    break;
                case '\b':
                    builder.Append("\\b");
                    break;
                case '\f':
                    builder.Append("\\f");
                    break;
                case '\n':
                    builder.Append("\\n");
                    break;
                case '\r':
                    builder.Append("\\r");
                    break;
                case '\t':
                    builder.Append("\\t");
                    break;
                default:
                    if (char.IsControl(current))
                    {
                        builder.Append("\\u");
                        builder.Append(((int)current).ToString("x4"));
                    }
                    else
                    {
                        builder.Append(current);
                    }

                    break;
            }
        }

        return builder.ToString();
    }

    private static bool TryReadString(
        string payload,
        string key,
        out string value)
    {
        foreach (Match match in StringPropertyRegex.Matches(payload))
        {
            string currentKey = match.Groups["key"].Value;

            if (!string.Equals(currentKey, key, StringComparison.Ordinal))
            {
                continue;
            }

            value = Regex.Unescape(match.Groups["value"].Value);
            return true;
        }

        value = string.Empty;
        return false;
    }

    private static bool TryReadInt(
        string payload,
        string key,
        out int value)
    {
        foreach (Match match in IntPropertyRegex.Matches(payload))
        {
            string currentKey = match.Groups["key"].Value;

            if (!string.Equals(currentKey, key, StringComparison.Ordinal))
            {
                continue;
            }

            if (int.TryParse(match.Groups["value"].Value, out value))
            {
                return true;
            }

            break;
        }

        value = default;
        return false;
    }
}

internal sealed class UdpLanDiscoveryBroadcaster : IDisposable
{
    private readonly object _sync = new();
    private UdpClient? _udpClient;
    private CancellationTokenSource? _cancellationTokenSource;
    private Task? _broadcastTask;
    private Func<LanDiscoveryAnnouncement>? _announcementFactory;
    private int _port;
    private int _intervalMs;

    internal bool IsRunning
    {
        get
        {
            lock (_sync)
            {
                return _broadcastTask is not null;
            }
        }
    }

    internal bool TryStart(
        int port,
        int intervalMs,
        Func<LanDiscoveryAnnouncement> announcementFactory,
        out string message)
    {
        if (announcementFactory is null)
        {
            message = "Announcement factory is null.";
            return false;
        }

        if (port is < 1 or > 65535)
        {
            message = "Discovery UDP port must be in range 1-65535.";
            return false;
        }

        lock (_sync)
        {
            if (_broadcastTask is not null)
            {
                _announcementFactory = announcementFactory;
                _port = port;
                _intervalMs = Math.Max(250, intervalMs);
                message = "Discovery broadcaster already running; updated runtime settings.";
                return true;
            }

            try
            {
                _udpClient = new UdpClient(AddressFamily.InterNetwork)
                {
                    EnableBroadcast = true
                };

                _udpClient.Client.SetSocketOption(
                    SocketOptionLevel.Socket,
                    SocketOptionName.ReuseAddress,
                    true);

                _cancellationTokenSource = new CancellationTokenSource();
                _announcementFactory = announcementFactory;
                _port = port;
                _intervalMs = Math.Max(250, intervalMs);
                _broadcastTask = Task.Run(BroadcastLoop);
                message = "Discovery broadcaster started.";
                return true;
            }
            catch (Exception ex)
            {
                DisposeSockets();
                message = $"Failed to start broadcaster: {ex.GetType().Name}: {ex.Message}";
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
            taskToWait = _broadcastTask;
            _broadcastTask = null;
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
            $"LAN discovery broadcaster stopped. Reason={reason}");
    }

    private void BroadcastLoop()
    {
        while (true)
        {
            UdpClient? udpClient;
            CancellationTokenSource? cts;
            Func<LanDiscoveryAnnouncement>? announcementFactory;
            int port;
            int intervalMs;

            lock (_sync)
            {
                udpClient = _udpClient;
                cts = _cancellationTokenSource;
                announcementFactory = _announcementFactory;
                port = _port;
                intervalMs = _intervalMs;
            }

            if (udpClient is null || cts is null || announcementFactory is null)
            {
                return;
            }

            if (cts.IsCancellationRequested)
            {
                return;
            }

            try
            {
                LanDiscoveryAnnouncement announcement = announcementFactory();
                string payload = LanDiscoveryMessageCodec.SerializeAnnouncement(announcement);
                byte[] bytes = Encoding.UTF8.GetBytes(payload);

                udpClient.Send(
                    bytes,
                    bytes.Length,
                    new IPEndPoint(IPAddress.Broadcast, port));
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning(
                    "LAN discovery broadcast failed. " +
                    $"Error={ex.GetType().Name}; " +
                    $"Message={ex.Message}");
            }

            try
            {
                Task.Delay(intervalMs, cts.Token).Wait();
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch
            {
                return;
            }
        }
    }

    private void DisposeSockets()
    {
        _cancellationTokenSource?.Dispose();
        _cancellationTokenSource = null;

        _udpClient?.Dispose();
        _udpClient = null;

        _announcementFactory = null;
        _port = 0;
        _intervalMs = 0;
    }

    public void Dispose()
    {
        Stop("Dispose");
    }
}
