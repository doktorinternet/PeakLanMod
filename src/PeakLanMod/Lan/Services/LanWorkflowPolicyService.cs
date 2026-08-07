using System.Collections.Generic;
using PeakLanMod.Lan.Model;
using Photon.Pun;

namespace PeakLanMod.Lan.Services;

internal sealed class LanWorkflowPolicyService : ILanWorkflowPolicyService
{
    private readonly ILanPluginOptions _options;
    private LanWorkflowMode? _lastAppliedLanWorkflowMode;

    internal LanWorkflowPolicyService(
        ILanPluginOptions options)
    {
        _options = options;
    }

    public void ApplyLanWorkflowMode(
        bool force,
        string source)
    {
        LanWorkflowMode mode = _options.WorkflowMode.Value;

        if (!force && _lastAppliedLanWorkflowMode == mode)
        {
            return;
        }

        switch (mode)
        {
            case LanWorkflowMode.AutoSetup:
                ApplyLanWorkflowPreset(
                    source,
                    mode,
                    autoDetectHostIpv4: true,
                    autoUpdateLuxonConfigOnHost: true);
                break;

            case LanWorkflowMode.LockedRuntime:
                ApplyLanWorkflowPreset(
                    source,
                    mode,
                    autoDetectHostIpv4: false,
                    autoUpdateLuxonConfigOnHost: false);
                break;

            case LanWorkflowMode.Advanced:
                Plugin.Log.LogInfo(
                    $"{source}: LanWorkflow mode Advanced active. " +
                    $"Using explicit settings: " +
                    $"AutoDetectHostIPv4={_options.AutoDetectHostLanIpv4.Value}; " +
                    $"AutoUpdateLuxonConfigOnHost={_options.AutoUpdateLuxonConfigOnHost.Value}.");
                break;

            default:
                Plugin.Log.LogWarning(
                    $"{source}: unknown LanWorkflow mode '{mode}'. " +
                    "Falling back to Advanced behavior.");
                break;
        }

        _lastAppliedLanWorkflowMode = mode;
    }

    public void TryAutoLockWorkflowModeAfterSuccessfulHost(
        string source)
    {
        if (!LanRuntimeContext.IsLocalServerMode)
        {
            return;
        }

        if (!_options.AutoLockWorkflowModeAfterSuccessfulHost.Value)
        {
            return;
        }

        if (_options.WorkflowMode.Value != LanWorkflowMode.AutoSetup)
        {
            return;
        }

        if (!PhotonNetwork.InRoom || !PhotonNetwork.IsMasterClient)
        {
            return;
        }

        _options.WorkflowMode.Value = LanWorkflowMode.LockedRuntime;
        _options.AutoLockWorkflowModeAfterSuccessfulHost.Value = false;

        Plugin.Log.LogInfo(
            $"{source}: auto-switched LanWorkflow WorkflowMode " +
            "from AutoSetup to LockedRuntime after successful host room creation.");
    }

    private void ApplyLanWorkflowPreset(
        string source,
        LanWorkflowMode mode,
        bool autoDetectHostIpv4,
        bool autoUpdateLuxonConfigOnHost)
    {
        bool changedAutoDetect = SetConfigEntryValue(
            _options.AutoDetectHostLanIpv4,
            autoDetectHostIpv4);

        bool changedAutoUpdate = SetConfigEntryValue(
            _options.AutoUpdateLuxonConfigOnHost,
            autoUpdateLuxonConfigOnHost);

        Plugin.Log.LogInfo(
            $"{source}: LanWorkflow mode {mode} applied. " +
            $"AutoDetectHostIPv4={_options.AutoDetectHostLanIpv4.Value}" +
            (changedAutoDetect ? " (updated)" : string.Empty) +
            "; " +
            $"AutoUpdateLuxonConfigOnHost={_options.AutoUpdateLuxonConfigOnHost.Value}" +
            (changedAutoUpdate ? " (updated)" : string.Empty) +
            ".");
    }

    private static bool SetConfigEntryValue<T>(
        BepInEx.Configuration.ConfigEntry<T> entry,
        T value)
    {
        if (EqualityComparer<T>.Default.Equals(entry.Value, value))
        {
            return false;
        }

        entry.Value = value;
        return true;
    }
}
