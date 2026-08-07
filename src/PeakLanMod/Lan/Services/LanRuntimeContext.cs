using System;

namespace PeakLanMod.Lan.Services;

internal static class LanRuntimeContext
{
    private static IPluginCompatibilityServices _services =
        PluginCompatibilityServices.CreateDefault();

    internal static void Initialize(IPluginCompatibilityServices services)
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));
    }

    internal static IPluginCompatibilityServices Services =>
        _services;

    internal static ILanPluginOptions Options =>
        _services.Options;

    internal static bool IsLocalServerMode =>
        _services.ModePolicy.IsLocalServerModeEnabled;

    internal static string Fingerprint(string value)
    {
        return _services
            .IdentityAndValidation
            .Fingerprint(value);
    }

    internal static string GetEffectiveLocalEndpointForLogging()
    {
        return _services
            .LocalServerRuntime
            .GetEffectiveLocalEndpoint();
    }
}
