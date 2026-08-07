namespace PeakLanMod.Lan.Services;

internal sealed class LanModePolicyService : ILanModePolicyService
{
    // Local-server workflow is the current architecture invariant for this mod.
    public bool IsLocalServerModeEnabled => true;
}
