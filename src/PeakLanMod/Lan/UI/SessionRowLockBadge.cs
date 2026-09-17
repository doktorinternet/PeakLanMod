namespace PeakLanMod.Lan.UI;

// Small, self-contained helper so the server-list lock indicator is not another
// inline conditional in LanOverlayController's row-rendering code.
internal static class SessionRowLockBadge
{
    private const string LockedPrefix = "[Locked] ";

    internal static string ApplyTo(string primaryLine, bool requiresPassword)
    {
        return requiresPassword
            ? LockedPrefix + primaryLine
            : primaryLine;
    }
}
