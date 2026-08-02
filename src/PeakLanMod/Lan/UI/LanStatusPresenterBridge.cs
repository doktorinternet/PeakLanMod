using System;
using PeakLanMod.Lan.Model;

namespace PeakLanMod.Lan.UI;

internal sealed class LanStatusPresenterBridge
{
    internal string BuildSummaryLine(
        string connectionPhase,
        string configuredEndpoint,
        int sessionCount)
    {
        string phase = string.IsNullOrWhiteSpace(connectionPhase)
            ? "Unknown"
            : connectionPhase;

        return $"Phase={phase} | Endpoint={configuredEndpoint} | Sessions={sessionCount}";
    }

    internal string BuildSessionRowLabel(
        LanSessionInfo session,
        bool isSelected,
        int displayIndex)
    {
        string compatibility = session.IsCompatible
            ? "Compatible"
            : session.IncompatibilityReason;

        string marker = isSelected
            ? "[*]"
            : "[ ]";

        return $"{marker} {displayIndex}. {session.RoomName} @ {session.NameServerAddress}:{session.NameServerPort} [{session.Transport}] {compatibility} Host={Plugin.Fingerprint(session.HostDisplayName)} Scene={session.Scene}";
    }
}
