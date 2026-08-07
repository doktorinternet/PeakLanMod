namespace PeakLanMod.Lan.Model;

internal enum LanErrorCode
{
    None = 0,
    LuxonNotRunning,
    NameServerUnreachable,
    MasterServerRedirectFailed,
    GameServerRedirectFailed,
    RoomDoesNotExist,
    IncompatibleGameVersion,
    IncompatibleModVersion,
    IncompatibleProtocolVersion,
    Timeout,
    UnknownPhotonFailure
}