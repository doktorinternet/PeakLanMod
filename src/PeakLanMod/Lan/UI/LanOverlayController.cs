using ExitGames.Client.Photon;
using PeakLanMod.Lan.Diagnostics;
using PeakLanMod.Lan.Model;
using PeakLanMod.Lan.Services;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace PeakLanMod.Lan.UI;

internal sealed class LanOverlayController : ILanOverlayController
{
    private readonly ILanPluginOptions _options;
    private readonly IDirectConnectCoordinator _directConnect;
    private readonly ILanDiscoveryRuntimeCoordinator _discoveryRuntime;
    private readonly ILanErrorStateService _errorState;
    private readonly ILanServerRuntimeService _LanServerRuntime;
    private readonly ILanIdentityAndValidation _identityAndValidation;
    private readonly LanDiscoveredSessionsViewModel _discoveredSessionsViewModel = new();
    private readonly LanStatusPresenterBridge _statusPresenterBridge = new();
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

    internal LanOverlayController(
        ILanPluginOptions options,
        IDirectConnectCoordinator directConnect,
        ILanDiscoveryRuntimeCoordinator discoveryRuntime,
        ILanErrorStateService errorState,
        ILanServerRuntimeService LanServerRuntime,
        ILanIdentityAndValidation identityAndValidation)
    {
        _options = options;
        _directConnect = directConnect;
        _discoveryRuntime = discoveryRuntime;
        _errorState = errorState;
        _LanServerRuntime = LanServerRuntime;
        _identityAndValidation = identityAndValidation;
        _lanPreferredRoomNameInput = _options.RoomName.Value;
    }

    public void UpdateLanPanelCollapseForSettingsScreen()
    {
        if (!LanRuntimeContext.IsLanServerMode)
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

                Plugin.Log.LogInfo(
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

            Plugin.Log.LogInfo(
                "LAN UI auto-expanded because settings screen was closed.");
        }
    }

    public bool ShouldRenderLanUiOverlay()
    {
        return LanRuntimeContext.IsLanServerMode
            && _options.LanDiscoveryEnabled.Value
            && IsMainMenuScene();
    }

    public void RenderLanUiOverlay()
    {
        EnsureLanUiStyles();

        EnsureLanUiSessionsRefreshed();

        _lanPreferredRoomNameInput = _identityAndValidation.NormalizeRoomNameInputForUi(
            _lanPreferredRoomNameInput);

        if (string.IsNullOrEmpty(_lanPreferredRoomNameInput)
            && !string.IsNullOrWhiteSpace(_options.RoomName.Value))
        {
            _lanPreferredRoomNameInput = _identityAndValidation.NormalizeRoomNameInputForUi(
                _options.RoomName.Value);
        }

        IReadOnlyList<LanSessionInfo> sessions = _discoveredSessionsViewModel.Sessions;
        int selectedIndex = _discoveredSessionsViewModel.SelectedIndex;
        (string phase, DateTime _) = _discoveryRuntime.GetConnectionPhaseSnapshot();
        LanErrorDetail? connectionError = _errorState.GetConnectionErrorSnapshot();
        LanSessionInfo? selectedSession = _discoveredSessionsViewModel.GetSelectedSessionOrNull();

        bool canJoinSelected = TryCanJoinSelectedSession(
            selectedSession,
            out string joinUnavailableReason);

        string summaryLine = _statusPresenterBridge.BuildSummaryLine(
            phase,
            _LanServerRuntime.GetConfiguredLocalEndpoint(),
            sessions.Count,
            connectionError);

        string lastRefreshLabel = _lastLanUiRefreshAtUtc == default
            ? "Last refresh: never"
            : $"Last refresh: {_lastLanUiRefreshAtUtc:HH:mm:ss} UTC";

        string modVersionLabel = $"{Plugin.PluginName}: {Plugin.PluginVersion}";

        bool showServerRows = !_isLanServerListCollapsed;
        bool p0 = _identityAndValidation.IsCurrentUserInX7GateSet();
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

                Plugin.Log.LogInfo(
                    "LAN UI manually expanded while settings screen is visible; auto-collapse suspended until settings closes.");
            }

            Plugin.Log.LogInfo(
                $"LAN UI server list toggled. Collapsed={_isLanServerListCollapsed}");
        }

        float actionButtonY = showServerRows
            ? panelRect.y + 74f
            : panelRect.y + 34f;

        bool canHostFromInput = _identityAndValidation.TryGetValidatedHostRoomNameFromInput(
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

            updatedPreferredRoomName = _identityAndValidation.NormalizeRoomNameInputForUi(
                updatedPreferredRoomName);

            if (!string.Equals(
                    updatedPreferredRoomName,
                    _lanPreferredRoomNameInput,
                    StringComparison.Ordinal))
            {
                _lanPreferredRoomNameInput = updatedPreferredRoomName;
                _options.RoomName.Value = _lanPreferredRoomNameInput;
            }
        }

        bool previousHostEnabled = GUI.enabled;
        GUI.enabled = canHostFromInput;

        if (GUI.Button(
                new Rect(panelRect.x + 12f, actionButtonY, 120f, 26f),
                "Host LAN",
                _lanUiButtonStyle ?? GUI.skin.button))
        {
            _options.RoomName.Value = validatedHostRoomName;
            Plugin.Log.LogInfo("LAN UI host button clicked.");
            _directConnect.RequestDirectHostStart("LanUiHostButton");
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
                _statusPresenterBridge.BuildErrorLine(connectionError),
                _lanUiLabelStyle ?? GUI.skin.label);
        }

        float footerY = panelRect.y + panelRect.height - 24f;
        float footerWidth = panelRect.width - 24f;
        float footerHalfWidth = footerWidth * 0.5f;

        GUI.Label(
            new Rect(panelRect.x + 12f, footerY, footerHalfWidth, 20f),
            lastRefreshLabel,
            _lanUiLabelStyle ?? GUI.skin.label);

        GUI.Label(
            new Rect(panelRect.x + 12f + footerHalfWidth, footerY, footerHalfWidth, 20f),
            modVersionLabel,
            _lanUiRightLabelStyle ?? GUI.skin.label);

        bool previousGuiEnabled = GUI.enabled;
        GUI.enabled = canJoinSelected;

        if (GUI.Button(
                new Rect(panelRect.x + 138f, actionButtonY, 120f, 26f),
                "Join Selected",
                _lanUiButtonStyle ?? GUI.skin.button)
            && canJoinSelected)
        {
            Plugin.Log.LogInfo("LAN UI join-selected button clicked.");
            TryJoinSelectedLanSession();
        }

        GUI.enabled = previousGuiEnabled;

        if (GUI.Button(
                new Rect(panelRect.x + 264f, actionButtonY, 110f, 26f),
                "Refresh",
                _lanUiButtonStyle ?? GUI.skin.button))
        {
            RefreshLanUiSessions();
            Plugin.Log.LogInfo(
                $"LAN UI refresh clicked. SessionCount={_discoveredSessionsViewModel.SessionCount}; RefreshedAtUtc={_lastLanUiRefreshAtUtc:O}");
        }

        if (p0)
        {
            string adminLine = selectedSession is null
                ? "Admin: select a session to view identity telemetry."
                : _statusPresenterBridge.BuildAdminIdentityRowLabel(
                    selectedSession,
                    BuildSessionIdentitySignature(selectedSession));

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
            string rowLabel = _statusPresenterBridge.BuildSessionRowLabel(
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
                if (_discoveredSessionsViewModel.TrySelectIndex(index))
                {
                    Plugin.Log.LogInfo(
                        "LAN UI selected discovered session from list. " +
                        $"Room={session.RoomName}; " +
                        $"Endpoint={_identityAndValidation.SanitizeEndpointForLog(session.NameServerAddress)}:{session.NameServerPort}; " +
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

    private void RefreshLanUiSessions()
    {
        LanSessionInfo[] snapshot = _discoveryRuntime.GetDiscoverySnapshot();
        _discoveredSessionsViewModel.UpdateSessions(snapshot);
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

    private void TryJoinSelectedLanSession()
    {
        LanSessionInfo? selected = _discoveredSessionsViewModel.GetSelectedSessionOrNull();

        if (selected is null)
        {
            Plugin.Log.LogInfo("LAN UI join-selected requested, but no session is selected.");
            return;
        }

        if (!selected.IsCompatible)
        {
            if (LanErrorClassifier.TryClassifyDiscoveryIncompatibility(
                    selected.IncompatibilityReason,
                    out LanErrorCode incompatibilityCode))
            {
                _errorState.ReportStructuredLanError(
                    incompatibilityCode,
                    source: "TryJoinSelectedLanSession",
                    message: "Selected discovered session is incompatible.",
                    context: selected.IncompatibilityReason);
            }

            Plugin.Log.LogWarning(
                "LAN UI join-selected blocked due to incompatible session. " +
                $"Room={selected.RoomName}; " +
                $"Reason={selected.IncompatibilityReason}");
            return;
        }

        if (!TryResolveDiscoverySessionTransport(
                selected.Transport,
                out ConnectionProtocol protocol))
        {
            Plugin.Log.LogWarning(
                "LAN UI join-selected ignored unsupported transport. " +
                $"Transport={selected.Transport}; " +
                $"Room={selected.RoomName}");
            return;
        }

        if (!_identityAndValidation.TryNormalizeRoomName(
                selected.RoomName,
                out string selectedRoomName,
                out string normalizeFailureReason))
        {
            Plugin.Log.LogWarning(
                "LAN UI join-selected blocked due to invalid selected room name. " +
                $"RawRoom={selected.RoomName}; " +
                $"Reason={normalizeFailureReason}");
            return;
        }

        Plugin.Log.LogInfo(
            "LAN UI join-selected staged discovered session as runtime join target. " +
            $"Room={selectedRoomName}; " +
            $"Endpoint={_identityAndValidation.SanitizeEndpointForLog(selected.NameServerAddress)}:{selected.NameServerPort}; " +
            $"Protocol={protocol}");

        _directConnect.RequestDirectJoinStart(
            selectedRoomName,
            "StartDirectJoinSelected",
            new LanServerEndpoint(
                selected.NameServerAddress,
                selected.NameServerPort,
                protocol));
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

    private static bool TryResolveDiscoverySessionTransport(
        string transport,
        out ConnectionProtocol protocol)
    {
        return Enum.TryParse(
            transport,
            ignoreCase: true,
            out protocol);
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

    private string BuildSessionIdentitySignature(
        LanSessionInfo session)
    {
        return _identityAndValidation.Fingerprint(
            $"{session.SourceAddress}|{session.HostDisplayName}");
    }
}
