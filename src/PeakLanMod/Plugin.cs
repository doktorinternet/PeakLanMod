using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using System.Reflection;
using UnityEngine;
using Zorro.Core;
using PeakLanMod.Lan.Services;

namespace PeakLanMod;

[BepInPlugin(
    PluginGuid,
    PluginName,
    PluginVersion)]
public sealed class Plugin : BaseUnityPlugin
{
    public const string PluginGuid = "BadHorse.PeakLanMod";
    public const string PluginName = "PEAK LAN Mod";
    public const string PluginVersion = PluginBuildInfo.BepInPluginVersion;

    internal static string DisplayVersion =>
        typeof(Plugin)
            .Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion
            ?.Split('+')[0]
        ?? PluginVersion;

    internal static ManualLogSource Log { get; private set; } = null!;

    private Harmony? _harmony;

    private ConfigEntry<KeyboardShortcut> HostKey =>
        LanRuntimeContext.Options.HostKey;

    private ConfigEntry<KeyboardShortcut> JoinKey =>
        LanRuntimeContext.Options.JoinKey;

    private void Awake()
    {
        Log = Logger;
        LanRuntimeContext.Initialize(
            PluginCompatibilityServices.CreateForPlugin(Config));

        LanRuntimeContext.Services.WorkflowPolicy.ApplyLanWorkflowMode(
            force: true,
            source: "Awake");
        LanRuntimeContext.Services.DiscoveryRuntime.SyncLanDiscoveryRuntime("Awake");

        gameObject.AddComponent<PhotonCallbackProbe>();
        gameObject.AddComponent<MainMenuBouncingTaglineOverlay>();

        _harmony = new Harmony(PluginGuid);
        _harmony.PatchAll();

        Logger.LogInfo($"{PluginName} loaded. DisplayVersion={DisplayVersion}; PluginVersion={PluginVersion}");
        LanRuntimeContext.Services.LanServerRuntime.DumpPhotonSettings("Plugin.Awake");
    }

    private void Update()
    {
        IPluginCompatibilityServices services =
            LanRuntimeContext.Services;

        services.WorkflowPolicy.ApplyLanWorkflowMode(
            force: false,
            source: "Update");

        services.ErrorState.LogPhotonStateChanges();
        services.DiscoveryRuntime.SyncLanDiscoveryRuntime("Update");
        services.Overlay.UpdateLanPanelCollapseForSettingsScreen();

        if (HostKey.Value.IsDown())
        {
            Logger.LogInfo("Host key pressed.");
            services.DirectConnect.RequestDirectHostStart("HostKey");
        }

        if (JoinKey.Value.IsDown())
        {
            Logger.LogInfo("Join key pressed.");
            services.DirectConnect.StartDirectJoin();
        }

        if (LanRuntimeContext.Options.AutoRetryDirectHostUntilReady.Value)
        {
            services.DirectConnect.TryProcessQueuedDirectHostStart("Update");
        }

        services.DirectConnect.TryProcessQueuedDirectJoinStart("Update");
    }

    private void OnDestroy()
    {
        IPluginCompatibilityServices services =
            LanRuntimeContext.Services;

        services.DiscoveryRuntime.ShutdownLanDiscoveryRuntime("Plugin.OnDestroy");
        services.LanServerRuntime.StopOwnedLanServerProcessOnExit("Plugin.OnDestroy");
        _harmony?.UnpatchSelf();
    }

    private void OnGUI()
    {
        ILanOverlayController overlay =
            LanRuntimeContext.Services.Overlay;

        if (overlay.ShouldRenderLanUiOverlay())
        {
            overlay.RenderLanUiOverlay();
        }
    }

}
