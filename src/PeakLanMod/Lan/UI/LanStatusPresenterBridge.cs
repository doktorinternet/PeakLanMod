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
        return $"Endpoint={configuredEndpoint} | Sessions={sessionCount}";
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
}
