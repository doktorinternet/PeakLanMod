using System;

namespace PeakLanMod.Lan.Services;

internal static class LanRuntimeContext
{
    private static IPluginCompatibilityServices _services =
        PluginCompatibilityServices.CreateDefault();
    private static string _pendingHostRoomPassword = string.Empty;

    internal static void Initialize(IPluginCompatibilityServices services)
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));
    }

    internal static IPluginCompatibilityServices Services =>
        _services;

    internal static ILanPluginOptions Options =>
        _services.Options;

    internal static bool IsLanServerMode =>
        _services.ModePolicy.IsLanServerModeEnabled;

    internal static void SetPendingHostRoomPassword(string password)
    {
        _pendingHostRoomPassword = password ?? string.Empty;
    }

    internal static string ConsumePendingHostRoomPassword()
    {
        string password = _pendingHostRoomPassword;
        _pendingHostRoomPassword = string.Empty;
        return password;
    }

    internal static string Fingerprint(string value)
    {
        return _services
            .IdentityAndValidation
            .Fingerprint(value);
    }

    internal static string GetEffectiveLocalEndpointForLogging()
    {
        return _services
            .LanServerRuntime
            .GetEffectiveLocalEndpoint();
    }
}
