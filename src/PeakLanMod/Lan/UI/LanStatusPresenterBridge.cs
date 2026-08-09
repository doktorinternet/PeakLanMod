using System;
using System.Globalization;
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

    internal string BuildAdminTelemetryPanelData(
        LanSessionInfo session,
        string hostIdentityFingerprint,
        float valueColumnOffsetPx)
    {
        (string Key, object? Value)[] rows =
        [
            (nameof(session.ServerInstanceId), session.ServerInstanceId),
            (nameof(session.RoomName), session.RoomName),
            (nameof(session.HostDisplayName), session.HostDisplayName),
            (nameof(session.SourceAddress), session.SourceAddress),
            (nameof(session.SourcePort), session.SourcePort),
            (nameof(session.NameServerAddress), session.NameServerAddress),
            (nameof(session.NameServerPort), session.NameServerPort),
            (nameof(session.Transport), session.Transport),
            (nameof(session.Scene), session.Scene),
            (nameof(session.ProtocolVersion), session.ProtocolVersion),
            (nameof(session.GameVersion), session.GameVersion),
            (nameof(session.ModVersion), session.ModVersion),
            (nameof(session.SchemaVersion), session.SchemaVersion),
            (nameof(session.SentAtUtc), session.SentAtUtc),
            (nameof(session.FirstSeenUtc), session.FirstSeenUtc),
            (nameof(session.LastSeenUtc), session.LastSeenUtc),
            (nameof(session.ExpiresAtUtc), session.ExpiresAtUtc),
            (nameof(session.IsCompatible), session.IsCompatible),
            (nameof(session.IncompatibilityReason), session.IncompatibilityReason),
            ("HostIdentityFingerprint", hostIdentityFingerprint)
        ];

        string[] formattedRows = new string[rows.Length];
        int safeValueColumnOffsetPx = Math.Max(80, (int)MathF.Round(valueColumnOffsetPx));

        for (int index = 0; index < rows.Length; index++)
        {
            string key = SanitizeForTmpRichText(rows[index].Key);
            string value = SanitizeForTmpRichText(FormatValue(rows[index].Value));
            formattedRows[index] = $"{key}:<pos={safeValueColumnOffsetPx}px>{value}";
        }

        return string.Join(Environment.NewLine, formattedRows);
    }

    private static string SanitizeForTmpRichText(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return value
            .Replace("<", "‹")
            .Replace(">", "›");
    }

    private static string FormatValue(object? value)
    {
        return value switch
        {
            DateTime dateTime => dateTime.ToString("yyyy-MM-dd HH:mm.ss", CultureInfo.InvariantCulture),
            null => string.Empty,
            _ => value.ToString() ?? string.Empty
        };
    }
}
