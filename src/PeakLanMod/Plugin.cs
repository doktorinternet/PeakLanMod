using BepInEx;
using BepInEx.Logging;
using Photon.Pun;
using Photon.Realtime;
using ExitGames.Client.Photon;
using HarmonyLib;
using BepInEx.Configuration;
using Zorro.Core;
using System;
using System.Collections.Generic;
using UnityEngine;
using PeakLanMod.Lan.Discovery;
using PeakLanMod.Lan.Diagnostics;
using PeakLanMod.Lan.Model;
using PeakLanMod.Lan.State;
using PeakLanMod.Lan.Services;
namespace PeakLanMod;

// Here are some basic resources on code style and naming conventions to help
// you in your first CSharp plugin!
// https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions
// https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/identifier-names
// https://learn.microsoft.com/en-us/dotnet/standard/design-guidelines/names-of-namespaces

// The BepInAutoPlugin attribute comes from the Hamunii.BepInEx.AutoPlugin
// NuGet package, and it will generate the BepInPlugin attribute for you!
// For more info, see https://github.com/Hamunii/BepInEx.AutoPlugin

/// <summary>
/// The BepInEx plugin class of PeakLanMod.
/// </summary>
[BepInPlugin(
    PluginGuid,
    PluginName,
    PluginVersion)]
public sealed class Plugin : BaseUnityPlugin
{
    internal enum LanWorkflowMode
    {
        AutoSetup,
        LockedRuntime,
        Advanced
    }

    public const string PluginGuid = "BadHorse.PeakLanMod";
    public const string PluginName = "PEAK LAN Mod";
    public const string PluginVersion = "0.5.0";

    internal static ManualLogSource Log { get; private set; } = null!;

    private Harmony? _harmony;
    private static IPluginCompatibilityServices CompatibilityServices { get; set; } =
        PluginCompatibilityServices.CreateDefault();

    private ConfigEntry<string> RoomName =>
        Services.Options.RoomName;

    private ConfigEntry<KeyboardShortcut> HostKey =>
        Services.Options.HostKey;

    private ConfigEntry<KeyboardShortcut> JoinKey =>
        Services.Options.JoinKey;

    internal static IPluginCompatibilityServices Services =>
        CompatibilityServices;

    private void Awake()
    {
        Log = Logger;
        CompatibilityServices = PluginCompatibilityServices.CreateForPlugin(Config);

        ApplyLanWorkflowMode(force: true, source: "Awake");
        SyncLanDiscoveryRuntime("Awake");

        gameObject.AddComponent<PhotonCallbackProbe>();

        _harmony = new Harmony(PluginGuid);
        _harmony.PatchAll();

        Logger.LogInfo("PEAK LAN Mod loaded.");
        Logger.LogInfo("Phase 0 scaffolding active: plugin-backed compatibility services wired.");
        DumpPhotonSettings("Plugin.Awake");
    }

    private void Update()
    {
        ApplyLanWorkflowMode(force: false, source: "Update");

        LogPhotonStateChanges();
        SyncLanDiscoveryRuntime("Update");
        Services.Overlay.UpdateLanPanelCollapseForSettingsScreen();

        if (HostKey.Value.IsDown())
        {
            Logger.LogInfo("Host key pressed.");

            RequestDirectHostStart("HostKey");
        }

        if (JoinKey.Value.IsDown())
        {
            Logger.LogInfo("Join key pressed.");
            StartDirectJoin();
        }

        if (AutoRetryDirectHostUntilReady.Value)
        {
            TryProcessQueuedDirectHostStart("Update");
        }

        TryProcessQueuedDirectJoinStart("Update");
    }

    private void LogPhotonStateChanges()
    {
        Services.ErrorState.LogPhotonStateChanges();
    }

    private void OnDestroy()
    {
        ShutdownLanDiscoveryRuntime("Plugin.OnDestroy");
        StopOwnedLocalServerProcessOnExit("Plugin.OnDestroy");
        _harmony?.UnpatchSelf();
    }

    internal static void DumpPhotonSettings(string source)
    {
        var settings =
            PhotonNetwork.PhotonServerSettings.AppSettings;

        Log.LogInfo(
            $"Photon settings [{source}]: " +
            $"UseNameServer={settings.UseNameServer}; " +
            $"Server={settings.Server ?? "<null>"}; " +
            $"Port={settings.Port}; " +
            $"Protocol={settings.Protocol}; " +
            $"FixedRegion={settings.FixedRegion ?? "<null>"}; " +
            $"AppVersion={settings.AppVersion ?? "<null>"}");
    }
    private void ApplyLanWorkflowMode(
        bool force,
        string source)
    {
        Services.WorkflowPolicy.ApplyLanWorkflowMode(force, source);
    }

    private void SyncLanDiscoveryRuntime(
        string source)
    {
        Services.DiscoveryRuntime.SyncLanDiscoveryRuntime(source);
    }

    internal static void RefreshLanDiscoveryBroadcast(
        string source)
    {
        Services.DiscoveryRuntime.RefreshLanDiscoveryBroadcast(source);
    }

    internal static void StopLanDiscoveryBroadcast(
        string source)
    {
        Services.DiscoveryRuntime.StopLanDiscoveryBroadcast(source);
    }

    private static void ShutdownLanDiscoveryRuntime(
        string source)
    {
        Services.DiscoveryRuntime.ShutdownLanDiscoveryRuntime(source);
    }

    private void OnGUI()
    {
        if (Services.Overlay.ShouldRenderLanUiOverlay())
        {
            Services.Overlay.RenderLanUiOverlay();
        }
    }

    private void RequestDirectHostStart(
        string source)
    {
        Services
            .DirectConnect
            .RequestDirectHostStart(source);
    }

    private void TryProcessQueuedDirectHostStart(
        string source)
    {
        Services
            .DirectConnect
            .TryProcessQueuedDirectHostStart(source);
    }

    private void RequestDirectJoinStart(
        string roomName,
        string source,
        LocalServerEndpoint endpoint)
    {
        Services
            .DirectConnect
            .RequestDirectJoinStart(
                roomName,
                source,
                endpoint);
    }

    private void TryProcessQueuedDirectJoinStart(
        string source)
    {
        Services
            .DirectConnect
            .TryProcessQueuedDirectJoinStart(source);
    }

    private static bool EnsureHostLocalServerProcess()
    {
        return Services
            .LocalServerRuntime
            .EnsureHostLocalServerProcess();
    }

    private static void StopOwnedLocalServerProcessOnExit(
        string source)
    {
        Services
            .LocalServerRuntime
            .StopOwnedLocalServerProcessOnExit(source);
    }

    private static void ApplyHostLanIpv4Selection()
    {
        Services
            .LocalServerRuntime
            .ApplyHostLanIpv4Selection();
    }

    private static void ApplyHostLuxonConfigAutomation()
    {
        Services
            .LocalServerRuntime
            .ApplyHostLuxonConfigAutomation();
    }

    private void StartDirectJoin()
    {
        Services
            .DirectConnect
            .StartDirectJoin();
    }

    private static string NormalizeRoomName(
        string roomName)
    {
        return Services
            .IdentityAndValidation
            .NormalizeRoomName(roomName);
    }

    private static bool TryNormalizeRoomName(
        string roomName,
        out string normalizedRoomName,
        out string failureReason)
    {
        return Services
            .IdentityAndValidation
            .TryNormalizeRoomName(
                roomName,
                out normalizedRoomName,
                out failureReason);
    }

    private static string NormalizeRoomNameInputForUi(
        string roomName)
    {
        return Services
            .IdentityAndValidation
            .NormalizeRoomNameInputForUi(roomName);
    }

    private static bool TryContainsBlockedHostRoomNameTerm(
        string normalizedRoomName,
        out string blockedTerm)
    {
        return Services
            .IdentityAndValidation
            .TryContainsBlockedHostRoomNameTerm(
                normalizedRoomName,
                out blockedTerm);
    }

    private bool Q1()
    {
        return Services
            .IdentityAndValidation
            .IsCurrentUserInX7GateSet();
    }

    private static string PullU()
    {
        return Services
            .IdentityAndValidation
            .PullU();
    }

    private static string MixSig(
        LanSessionInfo session)
    {
        string source = session.SourceAddress;
        string displayName = session.HostDisplayName;

        return Fingerprint($"{source}|{displayName}");
    }

    private static bool TryGetValidatedHostRoomName(
        string roomName,
        out string normalizedRoomName,
        out string failureReason)
    {
        return Services
            .IdentityAndValidation
            .TryGetValidatedHostRoomName(
                roomName,
                out normalizedRoomName,
                out failureReason);
    }

    private bool TryGetValidatedHostRoomNameFromInput(
        string roomName,
        out string normalizedRoomName,
        out string failureReason)
    {
        return Services
            .IdentityAndValidation
            .TryGetValidatedHostRoomNameFromInput(
                roomName,
                out normalizedRoomName,
                out failureReason);
    }

    private static string SanitizeEndpointForLog(
        string endpoint)
    {
        return Services
            .IdentityAndValidation
            .SanitizeEndpointForLog(endpoint);
    }

    internal static ConfigEntry<LanWorkflowMode> WorkflowMode => Services.Options.WorkflowMode;
    internal static ConfigEntry<bool> AutoLockWorkflowModeAfterSuccessfulHost => Services.Options.AutoLockWorkflowModeAfterSuccessfulHost;
    internal static ConfigEntry<string> LocalServerAddress => Services.Options.LocalServerAddress;
    internal static ConfigEntry<int> LocalServerPort => Services.Options.LocalServerPort;
    internal static ConfigEntry<ConnectionProtocol> LocalServerProtocol => Services.Options.LocalServerProtocol;
    internal static ConfigEntry<bool> AutoDetectHostLanIpv4 => Services.Options.AutoDetectHostLanIpv4;
    internal static ConfigEntry<string> AllowedHostInterfaces => Services.Options.AllowedHostInterfaces;
    internal static ConfigEntry<bool> AutoUpdateLuxonConfigOnHost => Services.Options.AutoUpdateLuxonConfigOnHost;
    internal static ConfigEntry<string> LuxonConfigPath => Services.Options.LuxonConfigPath;
    internal static ConfigEntry<bool> AutoStartLocalServerOnHost => Services.Options.AutoStartLocalServerOnHost;
    internal static ConfigEntry<string> LocalServerExecutablePath => Services.Options.LocalServerExecutablePath;
    internal static ConfigEntry<string> LocalServerWorkingDirectory => Services.Options.LocalServerWorkingDirectory;
    internal static ConfigEntry<string> LocalServerStartArguments => Services.Options.LocalServerStartArguments;
    internal static ConfigEntry<bool> AutoStopOwnedLocalServerOnExit => Services.Options.AutoStopOwnedLocalServerOnExit;
    internal static ConfigEntry<bool> AutoStopOwnedLocalServerOnLeaveRoom => Services.Options.AutoStopOwnedLocalServerOnLeaveRoom;
    internal static ConfigEntry<bool> ForceKillOwnedLocalServerOnExit => Services.Options.ForceKillOwnedLocalServerOnExit;
    internal static ConfigEntry<int> OwnedLocalServerStopTimeoutMs => Services.Options.OwnedLocalServerStopTimeoutMs;
    internal static ConfigEntry<bool> AutoRetryDirectHostUntilReady => Services.Options.AutoRetryDirectHostUntilReady;
    internal static ConfigEntry<bool> AutoSkipPhotonFailureDialog => Services.Options.AutoSkipPhotonFailureDialog;
    internal static ConfigEntry<bool> EnableLocalServerReadinessCheck => Services.Options.EnableLocalServerReadinessCheck;
    internal static ConfigEntry<int> LocalServerReadinessTimeoutMs => Services.Options.LocalServerReadinessTimeoutMs;
    internal static ConfigEntry<int> LocalServerReadinessPollIntervalMs => Services.Options.LocalServerReadinessPollIntervalMs;
    internal static ConfigEntry<bool> LanDiscoveryEnabled => Services.Options.LanDiscoveryEnabled;
    internal static ConfigEntry<int> LanDiscoveryUdpPort => Services.Options.LanDiscoveryUdpPort;
    internal static ConfigEntry<int> LanDiscoveryBroadcastIntervalMs => Services.Options.LanDiscoveryBroadcastIntervalMs;
    internal static ConfigEntry<int> LanDiscoveryEntryTtlMs => Services.Options.LanDiscoveryEntryTtlMs;
    internal static ConfigEntry<string> LanDiscoveryProtocolVersion => Services.Options.LanDiscoveryProtocolVersion;
    internal static ConfigEntry<bool> LanDiscoveryRequireVersionMatch => Services.Options.LanDiscoveryRequireVersionMatch;
    internal static ConfigEntry<bool> EnableStructuredErrorMapping => Services.Options.EnableStructuredErrorMapping;

    internal static bool IsLocalServerMode =>
        true;

    internal static void ApplyConfiguredPhotonSettings()
    {
        Services
            .LocalServerRuntime
            .ApplyConfiguredPhotonSettings();
    }

    internal static void NotifyLocalServerDetected()
    {
        Services.ErrorState.NotifyLocalServerDetected();
    }

    internal static void NotifyLocalServerNotDetected(
        string reason)
    {
        Services.ErrorState.NotifyLocalServerNotDetected(reason);
    }

    internal static void ReportStructuredLanError(
        LanErrorCode code,
        string source,
        string message,
        string context)
    {
        Services.ErrorState.ReportStructuredLanError(
            code,
            source,
            message,
            context);
    }

    internal static void ClearStructuredLanError(
        string source,
        string reason)
    {
        Services.ErrorState.ClearStructuredLanError(source, reason);
    }

    internal static void HandleLeftRoom()
    {
        Services.ErrorState.HandleLeftRoom();
    }

    internal static void StopOwnedLocalServerProcessForLeaveRoom(
        string source)
    {
        StopOwnedLocalServerProcessOnExit(source);
    }

    private static string GetConfiguredLocalEndpoint()
    {
        return Services
            .LocalServerRuntime
            .GetConfiguredLocalEndpoint();
    }

    private static string GetEffectiveLocalEndpoint()
    {
        return Services
            .LocalServerRuntime
            .GetEffectiveLocalEndpoint();
    }

    internal static string GetEffectiveLocalEndpointForLogging()
    {
        return GetEffectiveLocalEndpoint();
    }

    internal static void TryAutoLockWorkflowModeAfterSuccessfulHost(
        string source)
    {
        Services.WorkflowPolicy.TryAutoLockWorkflowModeAfterSuccessfulHost(source);
    }

    private static bool IsJoinEndpointOverrideActive =>
        Services.LocalServerRuntime.IsJoinEndpointOverrideActive;

    private static LocalServerEndpoint GetConfiguredLocalServerEndpoint()
    {
        return Services
            .LocalServerRuntime
            .GetConfiguredLocalServerEndpoint();
    }

    private static LocalServerEndpoint GetEffectiveLocalServerEndpointForConnection()
    {
        return Services
            .LocalServerRuntime
            .GetEffectiveLocalServerEndpointForConnection();
    }

    private static void ApplyTransientJoinEndpointOverride(
        LocalServerEndpoint endpoint,
        string source)
    {
        Services
            .LocalServerRuntime
            .ApplyTransientJoinEndpointOverride(endpoint, source);
    }

    private static void ClearTransientJoinEndpointOverride(
        string source)
    {
        Services
            .LocalServerRuntime
            .ClearTransientJoinEndpointOverride(source);
    }

    internal static string Fingerprint(string value)
    {
        return Services
            .IdentityAndValidation
            .Fingerprint(value);
    }
}