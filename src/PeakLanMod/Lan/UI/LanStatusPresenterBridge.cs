using System;
using System.Globalization;
using PeakLanMod.Lan.Model;

namespace PeakLanMod.Lan.UI;

internal sealed class LanStatusPresenterBridge
{
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
            (nameof(session.CurrentPlayers), FormatOccupancyValue(session.CurrentPlayers)),
            (nameof(session.MaxPlayers), FormatOccupancyValue(session.MaxPlayers)),
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

    private static string BuildOccupancyDisplay(
        int currentPlayers,
        int maxPlayers)
    {
        string current = FormatOccupancyValue(currentPlayers);
        string max = FormatOccupancyValue(maxPlayers);
        return $"{current}/{max}";
    }

    private static string FormatOccupancyValue(
        int value)
    {
        return value >= 0
            ? value.ToString(CultureInfo.InvariantCulture)
            : "?";
    }
}
