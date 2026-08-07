using System;

namespace PeakLanMod.Lan.Model;

internal sealed class LanErrorDetail
{
    internal LanErrorDetail(
        LanErrorCode code,
        string source,
        string phase,
        string message,
        string context,
        DateTime occurredAtUtc)
    {
        Code = code;
        Source = source;
        Phase = phase;
        Message = message;
        Context = context;
        OccurredAtUtc = occurredAtUtc;
    }

    internal LanErrorCode Code { get; }
    internal string Source { get; }
    internal string Phase { get; }
    internal string Message { get; }
    internal string Context { get; }
    internal DateTime OccurredAtUtc { get; }
}