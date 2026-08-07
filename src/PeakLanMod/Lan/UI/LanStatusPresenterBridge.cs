using System;
using PeakLanMod.Lan.Model;

namespace PeakLanMod.Lan.UI;

internal sealed class LanStatusPresenterBridge
{
    internal string BuildSummaryLine(
        string connectionPhase,
        string configuredEndpoint,
        int sessionCount,
        LanErrorDetail? error)
    {
        string errorToken = error is null
            ? "None"
            : error.Code.ToString();

        return $"Phase={connectionPhase} | Endpoint={configuredEndpoint} | Sessions={sessionCount} | Error={errorToken}";
    }

    internal string BuildErrorLine(
        LanErrorDetail error)
    {
        string context = string.IsNullOrWhiteSpace(error.Context)
            ? "<none>"
            : error.Context;

        return $"Last error: {error.Code} ({error.Message}) Source={error.Source} Context={context} At={error.OccurredAtUtc:HH:mm:ss} UTC";
    }

    internal string BuildSessionRowLabel(
        LanSessionInfo session,
        int displayIndex)
    {
        string compatibility = session.IsCompatible
            ? "Compatible"
            : session.IncompatibilityReason;

        return $"{displayIndex}. {session.RoomName} @ {session.NameServerAddress}:{session.NameServerPort} [{session.Transport}] {compatibility} Scene={session.Scene}";
    }

    internal string BuildAdminIdentityRowLabel(
        LanSessionInfo session,
        string hostIdentityFingerprint)
    {
        return $"Source={session.SourceAddress}:{session.SourcePort} | Host={session.HostDisplayName} | SessionId={session.ServerInstanceId} | HostFp={hostIdentityFingerprint}";
    }
}
