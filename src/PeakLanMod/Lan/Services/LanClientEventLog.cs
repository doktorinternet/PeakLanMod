using System;
using System.Collections.Generic;

namespace PeakLanMod.Lan.Services;

// Curated, player-facing event history shown in the in-game LOG panel (separate from the verbose BepInEx log).
internal sealed class LanClientEventLog : ILanClientEventLog
{
    private const int MaxEntries = 160;
    private readonly object _sync = new();
    private readonly List<string> _entries = new();

    public void Log(string message)
    {
        string normalizedMessage = string.IsNullOrWhiteSpace(message)
            ? "(empty update)"
            : message.Trim();
        string entry = $"[{DateTime.Now:HH:mm:ss}] {normalizedMessage}";

        lock (_sync)
        {
            _entries.Add(entry);

            if (_entries.Count > MaxEntries)
            {
                _entries.RemoveRange(0, _entries.Count - MaxEntries);
            }
        }
    }

    public IReadOnlyList<string> GetEntriesSnapshot()
    {
        lock (_sync)
        {
            return _entries.ToArray();
        }
    }

    public string? GetLatestEntry()
    {
        lock (_sync)
        {
            return _entries.Count == 0
                ? null
                : _entries[_entries.Count - 1];
        }
    }
}
