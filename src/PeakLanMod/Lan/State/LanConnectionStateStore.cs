using System;
using System.Collections.Generic;
using System.Linq;
using PeakLanMod.Lan.Model;

namespace PeakLanMod.Lan.State;

internal enum LanSessionUpdateKind
{
    Ignored,
    Added,
    Updated
}

internal sealed class LanConnectionStateStore
{
    private readonly object _sync = new();
    private readonly Dictionary<string, LanSessionInfo> _sessions =
        new(StringComparer.OrdinalIgnoreCase);
    private string _connectionPhase = "Idle";
    private DateTime _connectionPhaseUpdatedAtUtc = DateTime.UtcNow;
    private LanErrorDetail? _connectionError;

    internal LanSessionUpdateKind UpsertDiscoveredSession(LanSessionInfo session)
    {
        lock (_sync)
        {
            if (!_sessions.TryGetValue(session.Key, out LanSessionInfo? existing))
            {
                _sessions[session.Key] = session;
                return LanSessionUpdateKind.Added;
            }

            if (AreEquivalent(existing, session))
            {
                _sessions[session.Key] = new LanSessionInfo(
                    key: session.Key,
                    roomName: session.RoomName,
                    hostDisplayName: session.HostDisplayName,
                    sourceAddress: session.SourceAddress,
                    sourcePort: session.SourcePort,
                    nameServerAddress: session.NameServerAddress,
                    nameServerPort: session.NameServerPort,
                    transport: session.Transport,
                    scene: session.Scene,
                    serverInstanceId: session.ServerInstanceId,
                    protocolVersion: session.ProtocolVersion,
                    gameVersion: session.GameVersion,
                    modVersion: session.ModVersion,
                    schemaVersion: session.SchemaVersion,
                    sentAtUtc: session.SentAtUtc,
                    firstSeenUtc: existing.FirstSeenUtc,
                    lastSeenUtc: session.LastSeenUtc,
                    expiresAtUtc: session.ExpiresAtUtc,
                    isCompatible: session.IsCompatible,
                    incompatibilityReason: session.IncompatibilityReason);

                return LanSessionUpdateKind.Ignored;
            }

            _sessions[session.Key] = new LanSessionInfo(
                key: session.Key,
                roomName: session.RoomName,
                hostDisplayName: session.HostDisplayName,
                sourceAddress: session.SourceAddress,
                sourcePort: session.SourcePort,
                nameServerAddress: session.NameServerAddress,
                nameServerPort: session.NameServerPort,
                transport: session.Transport,
                scene: session.Scene,
                serverInstanceId: session.ServerInstanceId,
                protocolVersion: session.ProtocolVersion,
                gameVersion: session.GameVersion,
                modVersion: session.ModVersion,
                schemaVersion: session.SchemaVersion,
                sentAtUtc: session.SentAtUtc,
                firstSeenUtc: existing.FirstSeenUtc,
                lastSeenUtc: session.LastSeenUtc,
                expiresAtUtc: session.ExpiresAtUtc,
                isCompatible: session.IsCompatible,
                incompatibilityReason: session.IncompatibilityReason);

            return LanSessionUpdateKind.Updated;
        }
    }

    internal int RemoveExpiredSessions(DateTime nowUtc)
    {
        lock (_sync)
        {
            string[] expiredKeys = _sessions
                .Where(entry => entry.Value.ExpiresAtUtc <= nowUtc)
                .Select(entry => entry.Key)
                .ToArray();

            foreach (string key in expiredKeys)
            {
                _sessions.Remove(key);
            }

            return expiredKeys.Length;
        }
    }

    internal IReadOnlyList<LanSessionInfo> GetDiscoveredSessionsSnapshot(
        DateTime nowUtc)
    {
        lock (_sync)
        {
            _ = RemoveExpiredSessions(nowUtc);

            return _sessions.Values
                .OrderByDescending(current => current.LastSeenUtc)
                .ThenBy(current => current.RoomName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }

    internal void SetConnectionPhase(
        string phase)
    {
        string normalized = string.IsNullOrWhiteSpace(phase)
            ? "Unknown"
            : phase.Trim();

        lock (_sync)
        {
            _connectionPhase = normalized;
            _connectionPhaseUpdatedAtUtc = DateTime.UtcNow;
        }
    }

    internal (string Phase, DateTime UpdatedAtUtc) GetConnectionPhaseSnapshot()
    {
        lock (_sync)
        {
            return (_connectionPhase, _connectionPhaseUpdatedAtUtc);
        }
    }

    internal void SetConnectionError(
        LanErrorDetail detail)
    {
        lock (_sync)
        {
            _connectionError = detail;
        }
    }

    internal bool ClearConnectionError()
    {
        lock (_sync)
        {
            if (_connectionError is null)
            {
                return false;
            }

            _connectionError = null;
            return true;
        }
    }

    internal LanErrorDetail? GetConnectionErrorSnapshot()
    {
        lock (_sync)
        {
            return _connectionError;
        }
    }

    private static bool AreEquivalent(
        LanSessionInfo current,
        LanSessionInfo incoming)
    {
        return string.Equals(current.RoomName, incoming.RoomName, StringComparison.Ordinal)
            && string.Equals(current.HostDisplayName, incoming.HostDisplayName, StringComparison.Ordinal)
            && string.Equals(current.SourceAddress, incoming.SourceAddress, StringComparison.Ordinal)
            && current.SourcePort == incoming.SourcePort
            && string.Equals(current.NameServerAddress, incoming.NameServerAddress, StringComparison.Ordinal)
            && current.NameServerPort == incoming.NameServerPort
            && string.Equals(current.Transport, incoming.Transport, StringComparison.Ordinal)
            && string.Equals(current.Scene, incoming.Scene, StringComparison.Ordinal)
            && string.Equals(current.ProtocolVersion, incoming.ProtocolVersion, StringComparison.Ordinal)
            && string.Equals(current.GameVersion, incoming.GameVersion, StringComparison.Ordinal)
            && string.Equals(current.ModVersion, incoming.ModVersion, StringComparison.Ordinal)
            && current.SchemaVersion == incoming.SchemaVersion
            && current.IsCompatible == incoming.IsCompatible
            && string.Equals(current.IncompatibilityReason, incoming.IncompatibilityReason, StringComparison.Ordinal);
    }
}
