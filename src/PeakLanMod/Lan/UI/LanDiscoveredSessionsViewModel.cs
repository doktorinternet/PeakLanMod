using System;
using System.Collections.Generic;
using PeakLanMod.Lan.Model;

namespace PeakLanMod.Lan.UI;

internal sealed class LanDiscoveredSessionsViewModel
{
    private LanSessionInfo[] _sessions = [];
    private int _selectedIndex = -1;
    private string _selectedKey = string.Empty;

    internal void UpdateSessions(
        IReadOnlyList<LanSessionInfo> sessions)
    {
        int count = sessions.Count;
        var next = new LanSessionInfo[count];

        for (int index = 0; index < count; index++)
        {
            next[index] = sessions[index];
        }

        _sessions = next;

        if (_sessions.Length == 0)
        {
            _selectedIndex = -1;
            _selectedKey = string.Empty;
            return;
        }

        if (!string.IsNullOrWhiteSpace(_selectedKey))
        {
            for (int index = 0; index < _sessions.Length; index++)
            {
                if (!string.Equals(_sessions[index].Key, _selectedKey, StringComparison.Ordinal))
                {
                    continue;
                }

                _selectedIndex = index;
                return;
            }
        }

        int compatibleIndex = -1;

        for (int index = 0; index < _sessions.Length; index++)
        {
            if (_sessions[index].IsCompatible)
            {
                compatibleIndex = index;
                break;
            }
        }

        _selectedIndex = compatibleIndex >= 0
            ? compatibleIndex
            : 0;

        _selectedKey = _sessions[_selectedIndex].Key;
    }

    internal bool MoveSelection(
        int delta)
    {
        if (_sessions.Length == 0 || delta == 0)
        {
            return false;
        }

        int nextIndex = (_selectedIndex + delta) % _sessions.Length;

        if (nextIndex < 0)
        {
            nextIndex += _sessions.Length;
        }

        if (nextIndex == _selectedIndex)
        {
            return false;
        }

        _selectedIndex = nextIndex;
        _selectedKey = _sessions[_selectedIndex].Key;
        return true;
    }

    internal bool TrySelectIndex(
        int index)
    {
        if (index < 0 || index >= _sessions.Length)
        {
            return false;
        }

        if (_selectedIndex == index)
        {
            return false;
        }

        _selectedIndex = index;
        _selectedKey = _sessions[index].Key;
        return true;
    }

    internal LanSessionInfo? GetSelectedSessionOrNull()
    {
        if (_selectedIndex < 0 || _selectedIndex >= _sessions.Length)
        {
            return null;
        }

        return _sessions[_selectedIndex];
    }

    internal int SessionCount =>
        _sessions.Length;

    internal int SelectedIndex =>
        _selectedIndex;

    internal IReadOnlyList<LanSessionInfo> Sessions =>
        _sessions;
}
