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
using PeakLanMod.Lan.UI;
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
    private static readonly LanDiscoveredSessionsViewModel LanDiscoveredSessionsViewModel = new();
    private static readonly LanStatusPresenterBridge LanStatusPresenterBridge = new();
    private bool _isLanServerListCollapsed;
    private bool _lanPanelCollapsedBySettingsAutomation;
    private bool _allowLanPanelExpandedWhileSettingsVisible;
    private float _lastSettingsScreenProbeAt = -999f;
    private Vector2 _lanServerListScroll = Vector2.zero;
    private string _lanPreferredRoomNameInput = string.Empty;
    private float _lastLanUiRefreshAtRealtime = -999f;
    private DateTime _lastLanUiRefreshAtUtc;
    private bool _lanUiStyleInitialized;
    private GUIStyle? _lanUiPanelStyle;
    private GUIStyle? _lanUiTitleStyle;
    private GUIStyle? _lanUiLabelStyle;
    private GUIStyle? _lanUiRightLabelStyle;
    private GUIStyle? _lanUiButtonStyle;
    private GUIStyle? _lanUiTextFieldStyle;
    private GUIStyle? _lanUiRowStyle;
    private GUIStyle? _lanUiSelectedRowStyle;
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

        ConfigureDirectConnect();
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
        UpdateLanPanelCollapseForSettingsScreen();

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

    private void UpdateLanPanelCollapseForSettingsScreen()
    {
        if (!IsLocalServerMode)
        {
            return;
        }

        float now = Time.realtimeSinceStartup;

        if (now - _lastSettingsScreenProbeAt < 0.25f)
        {
            return;
        }

        _lastSettingsScreenProbeAt = now;

        bool settingsScreenVisible = IsSettingsScreenVisible();

        if (settingsScreenVisible)
        {
            if (_allowLanPanelExpandedWhileSettingsVisible)
            {
                return;
            }

            if (!_isLanServerListCollapsed)
            {
                _isLanServerListCollapsed = true;
                _lanPanelCollapsedBySettingsAutomation = true;

                Log.LogInfo(
                    "LAN UI auto-collapsed because settings screen is visible.");
            }

            return;
        }

        _allowLanPanelExpandedWhileSettingsVisible = false;

        if (_lanPanelCollapsedBySettingsAutomation
            && _isLanServerListCollapsed)
        {
            _isLanServerListCollapsed = false;
            _lanPanelCollapsedBySettingsAutomation = false;

            Log.LogInfo(
                "LAN UI auto-expanded because settings screen was closed.");
        }
    }

    private static bool IsSettingsScreenVisible()
    {
        if (!IsMainMenuScene())
        {
            return false;
        }

        RectTransform[] transforms = UnityEngine.Object.FindObjectsByType<RectTransform>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);

        for (int index = 0; index < transforms.Length; index++)
        {
            RectTransform current = transforms[index];

            if (!current.gameObject.activeInHierarchy)
            {
                continue;
            }

            string name = current.gameObject.name;

            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            if (IsLikelySettingsPanelName(name))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsLikelySettingsPanelName(
        string name)
    {
        string lower = name.ToLowerInvariant();

        if (!lower.Contains("setting"))
        {
            return false;
        }

        if (lower.Contains("button"))
        {
            return false;
        }

        return lower.Contains("panel")
            || lower.Contains("page")
            || lower.Contains("screen")
            || lower.Contains("menu")
            || lower.Contains("window");
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
    private void ConfigureDirectConnect()
    {
        _lanPreferredRoomNameInput = RoomName.Value;
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
        if (!IsLocalServerMode)
        {
            return;
        }

        if (LanDiscoveryEnabled.Value
            && IsMainMenuScene())
        {
            RenderLanUiOverlay();
        }

    }

    private static bool IsMainMenuScene()
    {
        UnityEngine.SceneManagement.Scene scene =
            UnityEngine.SceneManagement
                .SceneManager
                .GetActiveScene();

        if (!scene.isLoaded)
        {
            return false;
        }

        return string.Equals(
            scene.name,
            "Title",
            StringComparison.OrdinalIgnoreCase);
    }

    private void RefreshLanUiSessions()
    {
        LanSessionInfo[] snapshot = Services
            .DiscoveryRuntime
            .GetDiscoverySnapshot();
        LanDiscoveredSessionsViewModel.UpdateSessions(snapshot);
        _lastLanUiRefreshAtRealtime = Time.realtimeSinceStartup;
        _lastLanUiRefreshAtUtc = DateTime.UtcNow;
    }

    private void EnsureLanUiSessionsRefreshed()
    {
        const float autoRefreshIntervalSeconds = 1f;

        float now = Time.realtimeSinceStartup;

        if (now - _lastLanUiRefreshAtRealtime < autoRefreshIntervalSeconds)
        {
            return;
        }

        RefreshLanUiSessions();
    }

    private static bool TryCanJoinSelectedSession(
        LanSessionInfo? selectedSession,
        out string reason)
    {
        if (selectedSession is null)
        {
            reason = "Select a discovered session first.";
            return false;
        }

        if (!selectedSession.IsCompatible)
        {
            reason = $"Selected session is incompatible: {selectedSession.IncompatibilityReason}";
            return false;
        }

        if (!TryResolveDiscoverySessionTransport(
                selectedSession.Transport,
                out _))
        {
            reason = $"Unsupported transport: {selectedSession.Transport}";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    private void RequestDirectHostStart(
        string source)
    {
        Services
            .DirectConnect
            .RequestDirectHostStart(source);
    }

    private void TryJoinSelectedLanSession()
    {
        LanSessionInfo? selected =
            LanDiscoveredSessionsViewModel.GetSelectedSessionOrNull();

        if (selected is null)
        {
            Log.LogInfo("LAN UI join-selected requested, but no session is selected.");
            return;
        }

        if (!selected.IsCompatible)
        {
            if (LanErrorClassifier.TryClassifyDiscoveryIncompatibility(
                    selected.IncompatibilityReason,
                    out LanErrorCode incompatibilityCode))
            {
                ReportStructuredLanError(
                    incompatibilityCode,
                    source: "TryJoinSelectedLanSession",
                    message: "Selected discovered session is incompatible.",
                    context: selected.IncompatibilityReason);
            }

            Log.LogWarning(
                "LAN UI join-selected blocked due to incompatible session. " +
                $"Room={selected.RoomName}; " +
                $"Reason={selected.IncompatibilityReason}");
            return;
        }

        if (!TryResolveDiscoverySessionTransport(
                selected.Transport,
                out ConnectionProtocol protocol))
        {
            Log.LogWarning(
                "LAN UI join-selected ignored unsupported transport. " +
                $"Transport={selected.Transport}; " +
                $"Room={selected.RoomName}");
            return;
        }

        if (!TryNormalizeRoomName(
                selected.RoomName,
                out string selectedRoomName,
                out string normalizeFailureReason))
        {
            Log.LogWarning(
                "LAN UI join-selected blocked due to invalid selected room name. " +
                $"RawRoom={selected.RoomName}; " +
                $"Reason={normalizeFailureReason}");
            return;
        }

        Log.LogInfo(
            "LAN UI join-selected staged discovered session as runtime join target. " +
            $"Room={selectedRoomName}; " +
            $"Endpoint={SanitizeEndpointForLog(selected.NameServerAddress)}:{selected.NameServerPort}; " +
            $"Protocol={protocol}");

        RequestDirectJoinStart(
            selectedRoomName,
            "StartDirectJoinSelected",
            new LocalServerEndpoint(
                selected.NameServerAddress,
                selected.NameServerPort,
                protocol));
    }

    private static bool TryResolveDiscoverySessionTransport(
        string transport,
        out ConnectionProtocol protocol)
    {
        return Enum.TryParse(
            transport,
            ignoreCase: true,
            out protocol);
    }

    private void RenderLanUiOverlay()
    {
        EnsureLanUiStyles();

        EnsureLanUiSessionsRefreshed();

        _lanPreferredRoomNameInput = NormalizeRoomNameInputForUi(
            _lanPreferredRoomNameInput);

        if (string.IsNullOrEmpty(_lanPreferredRoomNameInput)
            && !string.IsNullOrWhiteSpace(RoomName.Value))
        {
            _lanPreferredRoomNameInput = NormalizeRoomNameInputForUi(
                RoomName.Value);
        }

        IReadOnlyList<LanSessionInfo> sessions = LanDiscoveredSessionsViewModel.Sessions;
        int selectedIndex = LanDiscoveredSessionsViewModel.SelectedIndex;
        (string phase, DateTime _) = Services
            .DiscoveryRuntime
            .GetConnectionPhaseSnapshot();
        LanErrorDetail? connectionError = Services
            .ErrorState
            .GetConnectionErrorSnapshot();
        LanSessionInfo? selectedSession = LanDiscoveredSessionsViewModel.GetSelectedSessionOrNull();

        bool canJoinSelected = TryCanJoinSelectedSession(
            selectedSession,
            out string joinUnavailableReason);

        string summaryLine = LanStatusPresenterBridge.BuildSummaryLine(
            phase,
            GetConfiguredLocalEndpoint(),
            sessions.Count,
            connectionError);

        string lastRefreshLabel = _lastLanUiRefreshAtUtc == default
            ? "Last refresh: never"
            : $"Last refresh: {_lastLanUiRefreshAtUtc:HH:mm:ss} UTC";

        bool showServerRows = !_isLanServerListCollapsed;
        bool p0 = Q1();
        float adminPanelExtraHeight = p0
            ? 48f
            : 0f;
        const float panelMargin = 16f;
        float panelWidth;
        float panelHeight;

        if (showServerRows)
        {
            float maxPanelWidth = Math.Max(360f, Screen.width - (panelMargin * 2f));
            panelWidth = Math.Min(960f, maxPanelWidth);
            float desiredPanelHeight = 136f + adminPanelExtraHeight + (sessions.Count * 24f);
            float maxPanelHeight = Math.Max(170f, Screen.height - (panelMargin * 2f));
            panelHeight = Mathf.Clamp(desiredPanelHeight, 170f, maxPanelHeight);
        }
        else
        {
            panelWidth = 252f;
            panelHeight = 72f;
        }

        var panelRect = new Rect(
            Screen.width - panelWidth - panelMargin,
            panelMargin,
            panelWidth,
            panelHeight);

        Color previousPanelColor = GUI.color;
        GUI.color = new Color(0.08f, 0.08f, 0.1f, 1f);
        GUI.DrawTexture(panelRect, Texture2D.whiteTexture, ScaleMode.StretchToFill);
        GUI.color = previousPanelColor;

        string collapseToggleLabel = showServerRows
            ? "-"
            : "+";

        if (GUI.Button(
                new Rect(panelRect.x + panelRect.width - 24f, panelRect.y + 2f, 22f, 22f),
            collapseToggleLabel,
            _lanUiButtonStyle ?? GUI.skin.button))
        {
            bool nextCollapsed = !_isLanServerListCollapsed;
            _isLanServerListCollapsed = nextCollapsed;

            if (nextCollapsed)
            {
                _allowLanPanelExpandedWhileSettingsVisible = false;
            }
            else if (IsSettingsScreenVisible())
            {
                _allowLanPanelExpandedWhileSettingsVisible = true;
                _lanPanelCollapsedBySettingsAutomation = false;

                Log.LogInfo(
                    "LAN UI manually expanded while settings screen is visible; auto-collapse suspended until settings closes.");
            }

            Log.LogInfo(
                $"LAN UI server list toggled. Collapsed={_isLanServerListCollapsed}");
        }

        float actionButtonY = showServerRows
            ? panelRect.y + 74f
            : panelRect.y + 34f;

        bool canHostFromInput = TryGetValidatedHostRoomNameFromInput(
            _lanPreferredRoomNameInput,
            out string validatedHostRoomName,
            out string hostUnavailableReason);

        if (showServerRows)
        {
            GUI.Label(
                new Rect(panelRect.x + 12f, panelRect.y + 50f, 86f, 20f),
                "Room Name:",
                _lanUiLabelStyle ?? GUI.skin.label);

            string updatedPreferredRoomName = GUI.TextField(
                new Rect(panelRect.x + 98f, panelRect.y + 48f, panelRect.width - 110f, 22f),
                _lanPreferredRoomNameInput,
                _lanUiTextFieldStyle ?? GUI.skin.textField);

            updatedPreferredRoomName = NormalizeRoomNameInputForUi(
                updatedPreferredRoomName);

            if (!string.Equals(
                    updatedPreferredRoomName,
                    _lanPreferredRoomNameInput,
                    StringComparison.Ordinal))
            {
                _lanPreferredRoomNameInput = updatedPreferredRoomName;
                RoomName.Value = _lanPreferredRoomNameInput;
            }
        }

        bool previousHostEnabled = GUI.enabled;
        GUI.enabled = canHostFromInput;

        if (GUI.Button(
            new Rect(panelRect.x + 12f, actionButtonY, 120f, 26f),
                "Host LAN",
                _lanUiButtonStyle ?? GUI.skin.button))
        {
            RoomName.Value = validatedHostRoomName;
            Log.LogInfo("LAN UI host button clicked.");
            RequestDirectHostStart("LanUiHostButton");
        }

        GUI.enabled = previousHostEnabled;

        if (!showServerRows)
        {
            return;
        }

        GUI.Label(
            new Rect(panelRect.x + 12f, panelRect.y + 24f, panelRect.width - 24f, 22f),
            summaryLine,
            _lanUiTitleStyle ?? GUI.skin.label);

        if (connectionError is not null)
        {
            GUI.Label(
                new Rect(panelRect.x + 12f, panelRect.y + panelRect.height - 44f, panelRect.width - 24f, 20f),
                LanStatusPresenterBridge.BuildErrorLine(connectionError),
                _lanUiLabelStyle ?? GUI.skin.label);
        }

        GUI.Label(
            new Rect(panelRect.x + 12f, panelRect.y + panelRect.height - 24f, panelRect.width - 24f, 20f),
            lastRefreshLabel,
            _lanUiRightLabelStyle ?? GUI.skin.label);

        bool previousGuiEnabled = GUI.enabled;
        GUI.enabled = canJoinSelected;

        if (GUI.Button(
                new Rect(panelRect.x + 138f, actionButtonY, 120f, 26f),
            "Join Selected",
            _lanUiButtonStyle ?? GUI.skin.button)
            && canJoinSelected)
        {
            Log.LogInfo("LAN UI join-selected button clicked.");
            TryJoinSelectedLanSession();
        }

        GUI.enabled = previousGuiEnabled;

        if (GUI.Button(
            new Rect(panelRect.x + 264f, actionButtonY, 110f, 26f),
                "Refresh",
                _lanUiButtonStyle ?? GUI.skin.button))
        {
            RefreshLanUiSessions();
            Log.LogInfo(
            $"LAN UI refresh clicked. SessionCount={LanDiscoveredSessionsViewModel.SessionCount}; RefreshedAtUtc={_lastLanUiRefreshAtUtc:O}");
        }

        if (p0)
        {
            string adminLine = selectedSession is null
                ? "Admin: select a session to view identity telemetry."
                : LanStatusPresenterBridge.BuildAdminIdentityRowLabel(
                    selectedSession,
                    MixSig(selectedSession));

            GUI.Label(
                new Rect(panelRect.x + 12f, panelRect.y + 106f, panelRect.width - 24f, 20f),
                adminLine,
                _lanUiLabelStyle ?? GUI.skin.label);
        }

        float rowY = panelRect.y + 106f + adminPanelExtraHeight;

        if (!canHostFromInput)
        {
            GUI.Label(
                new Rect(panelRect.x + 390f, panelRect.y + 74f, panelRect.width - 402f, 20f),
            $"Cannot host: {hostUnavailableReason}",
            _lanUiLabelStyle ?? GUI.skin.label);
        }

        if (sessions.Count == 0)
        {
            GUI.Label(
                new Rect(panelRect.x + 12f, rowY, panelRect.width - 24f, 22f),
                "No discovered sessions yet. Keep host in-room and click Refresh.",
                _lanUiLabelStyle ?? GUI.skin.label);
            return;
        }

        float listViewportHeight = Math.Max(
            24f,
            panelRect.height - 136f - adminPanelExtraHeight);

        var listViewportRect = new Rect(
            panelRect.x + 12f,
            rowY,
            panelRect.width - 24f,
            listViewportHeight);

        float rowHeight = 24f;
        float listContentHeight = Math.Max(
            listViewportHeight,
            sessions.Count * rowHeight);

        var listContentRect = new Rect(
            0f,
            0f,
            Math.Max(120f, listViewportRect.width - 18f),
            listContentHeight);

        _lanServerListScroll = GUI.BeginScrollView(
            listViewportRect,
            _lanServerListScroll,
            listContentRect,
            false,
            true);

        for (int index = 0; index < sessions.Count; index++)
        {
            LanSessionInfo session = sessions[index];
            bool isSelected = index == selectedIndex;
            string rowLabel = LanStatusPresenterBridge.BuildSessionRowLabel(
                session,
                index + 1);

            var rowRect = new Rect(
                0f,
                index * rowHeight,
                listContentRect.width,
                22f);

            Color previousGuiColor = GUI.color;

            if (isSelected)
            {
                GUI.color = new Color(0.78f, 0.93f, 0.78f, 1f);
            }

            bool clicked = GUI.Button(
                rowRect,
                rowLabel,
                isSelected
                    ? (_lanUiSelectedRowStyle ?? GUI.skin.button)
                    : (_lanUiRowStyle ?? GUI.skin.button));

            GUI.color = previousGuiColor;

            if (clicked)
            {
                if (LanDiscoveredSessionsViewModel.TrySelectIndex(index))
                {
                    Log.LogInfo(
                        "LAN UI selected discovered session from list. " +
                        $"Room={session.RoomName}; " +
                        $"Endpoint={SanitizeEndpointForLog(session.NameServerAddress)}:{session.NameServerPort}; " +
                        $"Compatible={session.IsCompatible}; " +
                        $"Reason={session.IncompatibilityReason}");
                }
            }
        }

        GUI.EndScrollView();

        if (!canJoinSelected)
        {
            GUI.Label(
                new Rect(panelRect.x + 390f, panelRect.y + 50f, panelRect.width - 402f, 26f),
            $"Join unavailable: {joinUnavailableReason}",
            _lanUiLabelStyle ?? GUI.skin.label);
        }
    }

    private void EnsureLanUiStyles()
    {
        if (_lanUiStyleInitialized)
        {
            return;
        }

        _lanUiStyleInitialized = true;
        // Style with PEAK-like earthy tones while keeping Unity font handling untouched.
        Texture2D panelTexture = CreateSolidTexture(new Color(0.13f, 0.11f, 0.09f, 0.96f));
        Texture2D buttonNormalTexture = CreateSolidTexture(new Color(0.86f, 0.74f, 0.51f, 1f));
        Texture2D buttonHoverTexture = CreateSolidTexture(new Color(0.94f, 0.82f, 0.6f, 1f));
        Texture2D buttonActiveTexture = CreateSolidTexture(new Color(0.7f, 0.56f, 0.36f, 1f));
        Texture2D fieldTexture = CreateSolidTexture(new Color(0.23f, 0.19f, 0.15f, 1f));
        Texture2D selectedRowTexture = CreateSolidTexture(new Color(0.52f, 0.43f, 0.27f, 1f));

        _lanUiPanelStyle = new GUIStyle(GUI.skin.box)
        {
            padding = new RectOffset(10, 10, 8, 8),
            normal =
            {
                background = panelTexture,
                textColor = new Color(0.98f, 0.92f, 0.8f, 1f)
            }
        };

        _lanUiTitleStyle = new GUIStyle(GUI.skin.label)
        {
            fontStyle = FontStyle.Bold,
            normal =
            {
                textColor = new Color(0.98f, 0.9f, 0.74f, 1f)
            }
        };

        _lanUiLabelStyle = new GUIStyle(GUI.skin.label)
        {
            normal =
            {
                textColor = new Color(0.95f, 0.9f, 0.82f, 1f)
            }
        };

        _lanUiRightLabelStyle = new GUIStyle(_lanUiLabelStyle)
        {
            alignment = TextAnchor.UpperRight
        };

        _lanUiButtonStyle = new GUIStyle(GUI.skin.button)
        {
            fontStyle = FontStyle.Bold,
            normal =
            {
                background = buttonNormalTexture,
                textColor = new Color(0.17f, 0.13f, 0.08f, 1f)
            },
            hover =
            {
                background = buttonHoverTexture,
                textColor = new Color(0.13f, 0.1f, 0.06f, 1f)
            },
            active =
            {
                background = buttonActiveTexture,
                textColor = new Color(0.99f, 0.95f, 0.85f, 1f)
            }
        };

        _lanUiTextFieldStyle = new GUIStyle(GUI.skin.textField)
        {
            normal =
            {
                background = fieldTexture,
                textColor = new Color(0.98f, 0.92f, 0.8f, 1f)
            },
            focused =
            {
                background = buttonActiveTexture,
                textColor = new Color(1f, 0.97f, 0.9f, 1f)
            }
        };

        _lanUiRowStyle = new GUIStyle(_lanUiButtonStyle)
        {
            alignment = TextAnchor.MiddleLeft,
            fontStyle = FontStyle.Normal,
            padding = new RectOffset(8, 8, 2, 2)
        };

        _lanUiSelectedRowStyle = new GUIStyle(_lanUiRowStyle)
        {
            fontStyle = FontStyle.Bold
        };

        _lanUiSelectedRowStyle.normal.background = selectedRowTexture;
        _lanUiSelectedRowStyle.hover.background = selectedRowTexture;
        _lanUiSelectedRowStyle.active.background = buttonActiveTexture;
        _lanUiSelectedRowStyle.normal.textColor = new Color(1f, 0.96f, 0.84f, 1f);
        _lanUiSelectedRowStyle.hover.textColor = new Color(1f, 0.98f, 0.88f, 1f);
        _lanUiSelectedRowStyle.active.textColor = new Color(1f, 1f, 0.9f, 1f);
    }

    private static Texture2D CreateSolidTexture(
        Color color)
    {
        var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
        {
            hideFlags = HideFlags.HideAndDontSave
        };

        texture.SetPixel(0, 0, color);
        texture.Apply(updateMipmaps: false, makeNoLongerReadable: false);
        return texture;
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