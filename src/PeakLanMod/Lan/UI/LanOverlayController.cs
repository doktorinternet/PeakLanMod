using ExitGames.Client.Photon;
using PeakLanMod.Lan.Diagnostics;
using PeakLanMod.Lan.Model;
using PeakLanMod.Lan.Services;
using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PeakLanMod.Lan.UI;

internal sealed class LanOverlayController : ILanOverlayController
{
    private readonly ILanPluginOptions _options;
    private readonly IDirectConnectCoordinator _directConnect;
    private readonly ILanDiscoveryRuntimeCoordinator _discoveryRuntime;
    private readonly ILanErrorStateService _errorState;
    private readonly ILanServerRuntimeService _lanServerRuntime;
    private readonly ILanIdentityAndValidation _identityAndValidation;
    private readonly LanDiscoveredSessionsViewModel _discoveredSessionsViewModel = new();
    private readonly LanStatusPresenterBridge _statusPresenterBridge = new();
    private readonly List<LanSessionRowUi> _sessionRows = new();
    private readonly List<string> _clientStateLogEntries = new();

    private bool _isLanServerListCollapsed;
    private bool _lanPanelCollapsedBySettingsAutomation;
    private bool _allowLanPanelExpandedWhileSettingsVisible;
    private bool _isSyncingRoomInput;
    private float _lastSettingsScreenProbeAt = -999f;
    private float _lastLanUiRefreshAtRealtime = -999f;
    private DateTime _lastLanUiRefreshAtUtc;
    private string _lanPreferredRoomNameInput = string.Empty;

    private GameObject? _overlayCanvasObject;
    private TMP_Text? _templateText;
    private Sprite? _solidSprite;

    private RectTransform? _panelRect;
    private Image? _panelImage;
    private Button? _collapseButton;
    private TMP_Text? _collapseButtonText;
    private TMP_Text? _serverListTitleText;

    private TMP_Text? _roomNameLabelText;
    private InputField? _roomNameInput;
    private Text? _roomNameInputText;
    private Text? _roomNameInputPlaceholder;

    private Button? _hostButton;
    private TMP_Text? _hostButtonText;
    private Button? _joinButton;
    private TMP_Text? _joinButtonText;
    private Button? _refreshButton;
    private TMP_Text? _refreshButtonText;

    private TMP_Text? _hostUnavailableText;
    private TMP_Text? _emptyText;
    private string _pendingJoinUnavailableLog = string.Empty;

    private ScrollRect? _sessionScrollRect;
    private RectTransform? _sessionViewportRect;
    private RectTransform? _sessionContentRect;

    private TMP_Text? _lastRefreshText;
    private TMP_Text? _modVersionText;

    private RectTransform? _statePanelRect;
    private Image? _statePanelImage;
    private TMP_Text? _stateTitleText;
    private TMP_Text? _stateLatestText;
    private ScrollRect? _stateLogScrollRect;
    private RectTransform? _stateLogViewportRect;
    private RectTransform? _stateLogContentRect;
    private TMP_Text? _stateLogBodyText;

    private string _lastLoggedConnectionPhase = string.Empty;
    private string _lastLoggedEndpoint = string.Empty;
    private string _lastLoggedErrorSignature = string.Empty;
    private string _lastRenderedStateLogText = string.Empty;

    private RectTransform? _adminPanelRect;
    private Image? _adminPanelImage;
    private TMP_Text? _adminTitleText;
    private TMP_Text? _adminBodyText;

    private const float PanelMargin = 16f;
    private const float MainPanelExpandedMinWidth = 760f;
    private const float MainPanelExpandedMaxWidth = 1160f;
    private const float MainPanelCollapsedWidth = 380f;
    private const float MainPanelExpandedMinHeight = 236f;
    private const float MainPanelCollapsedHeight = 72f;

    private const float PanelPaddingX = 14f;
    private const float PanelPaddingY = 10f;
    private const float SectionGap = 8f;
    private const float HeaderHeight = 28f;
    private const float InputBandHeight = 34f;
    private const float ActionBandHeight = 34f;
    private const float FooterHeight = 22f;
    private const float FooterBottomPadding = 10f;

    private const float ControlGap = 8f;
    private const float HostButtonWidth = 172f;
    private const float JoinButtonWidth = 248f;
    private const float RefreshButtonWidth = 172f;

    private const float SessionRowHeight = 58f;
    private const float SessionRowGap = 6f;
    private const int MaxVisibleSessionRows = 6;
    private const float SessionRowInnerPaddingX = 12f;
    private const float SessionRowPrimaryTop = 8f;
    private const float SessionRowSecondaryTop = 31f;

    private const float StatePanelGap = 12f;
    private const float StatePanelExpandedHeight = 196f;
    private const float StatePanelCollapsedHeight = 154f;
    private const float StatePanelMinVisibleHeight = 92f;
    private const float StatePanelTopInset = 10f;
    private const float StatePanelBottomInset = 10f;
    private const float StateTitleToLogGap = 4f;
    private const float StateLogToLatestGap = 8f;
    private const float StateLatestHeight = 20f;
    private const float StateLogTextInsetX = 8f;
    private const float StateLogTextInsetTop = 5f;
    private const float StateLogTextInsetBottom = 6f;

    private const float AdminPanelGap = 24f;
    private const float AdminPanelMinWidth = 340f;
    private const float AdminPanelMaxWidth = 560f;
    private const float AdminPanelMinHeight = 112f;
    private const float AdminPanelTopInset = 10f;
    private const float AdminPanelBottomInset = 10f;
    private const float AdminTitleToBodyGap = 4f;
    private const float AdminBodyFontSize = 14f;

    private const float TitleFontSize = 21f;
    private const float LabelFontSize = 15f;
    private const float FooterFontSize = 14f;
    private const float SessionPrimaryFontSize = 18f;
    private const float SessionSecondaryFontSize = 14f;

    private static readonly Color UiTextColor = new(0.16f, 0.12f, 0.08f, 1f);
    private static readonly Color UiMutedTextColor = new(0.28f, 0.22f, 0.16f, 0.88f);
    private static readonly Color UiPanelColor = new(0.9f, 0.81f, 0.66f, 0.70f);
    private static readonly Color UiPanelSecondaryColor = new(0.87f, 0.78f, 0.63f, 0.65f);
    private static readonly Color UiButtonColor = new(0.84f, 0.69f, 0.42f, 0.96f);
    private static readonly Color UiButtonHoverColor = new(0.9f, 0.76f, 0.5f, 0.98f);
    private static readonly Color UiButtonPressedColor = new(0.76f, 0.58f, 0.33f, 0.98f);
    private static readonly Color UiDisabledColor = new(0.66f, 0.58f, 0.46f, 0.84f);
    private static readonly Color UiFieldColor = new(0.95f, 0.87f, 0.74f, 0.94f);
    private static readonly Color UiBorderColor = new(0.15f, 0.11f, 0.08f, 0.42f);
    private static readonly Color UiSessionRowColor = new(0.92f, 0.82f, 0.65f, 0.42f);
    private static readonly Color UiSessionRowSelectedColor = new(0.78f, 0.64f, 0.4f, 0.76f);
    private static readonly Color UiSessionRowPrimarySelectedColor = new(0.13f, 0.1f, 0.07f, 1f);
    private static readonly Color UiStateLatestTextColor = new(0.23f, 0.18f, 0.14f, 0.92f);
    private static readonly Color UiLogSurfaceColor = new(0.08f, 0.09f, 0.12f, 0.95f);
    private static readonly Color UiLogViewportColor = new(0.05f, 0.06f, 0.09f, 0.96f);
    private static readonly Color UiLogBorderColor = new(0.04f, 0.05f, 0.07f, 0.82f);
    private const int MaxClientStateLogEntries = 160;

    internal LanOverlayController(
        ILanPluginOptions options,
        IDirectConnectCoordinator directConnect,
        ILanDiscoveryRuntimeCoordinator discoveryRuntime,
        ILanErrorStateService errorState,
        ILanServerRuntimeService lanServerRuntime,
        ILanIdentityAndValidation identityAndValidation)
    {
        _options = options;
        _directConnect = directConnect;
        _discoveryRuntime = discoveryRuntime;
        _errorState = errorState;
        _lanServerRuntime = lanServerRuntime;
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
        bool shouldRender = LanRuntimeContext.IsLanServerMode
            && _options.LanDiscoveryEnabled.Value
            && IsMainMenuScene();

        if (!shouldRender)
        {
            SetOverlayActive(false);
        }

        return shouldRender;
    }

    public void RenderLanUiOverlay()
    {
        EnsureLanUiSessionsRefreshed();
        EnsureOverlayUi();
        SetOverlayActive(true);

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
        (string phase, DateTime phaseUpdatedAtUtc) = _discoveryRuntime.GetConnectionPhaseSnapshot();
        LanErrorDetail? connectionError = _errorState.GetConnectionErrorSnapshot();
        LanSessionInfo? selectedSession = _discoveredSessionsViewModel.GetSelectedSessionOrNull();
        string configuredEndpoint = _lanServerRuntime.GetConfiguredLocalEndpoint();

        bool canJoinSelected = TryCanJoinSelectedSession(
            selectedSession,
            out string joinUnavailableReason);

        bool canHostFromInput = _identityAndValidation.TryGetValidatedHostRoomNameFromInput(
            _lanPreferredRoomNameInput,
            out string validatedHostRoomName,
            out string hostUnavailableReason);

        EnsureClientStateLogUpdated(
            phase,
            phaseUpdatedAtUtc,
            configuredEndpoint,
            connectionError);

        string lastRefreshLabel = _lastLanUiRefreshAtUtc == default
            ? "Last refresh: never"
            : $"Last refresh: {_lastLanUiRefreshAtUtc:HH:mm:ss} UTC";

        string modVersionLabel = $"{Plugin.PluginName}: {Plugin.DisplayVersion}";

        bool showServerRows = !_isLanServerListCollapsed;
        bool p0 = _identityAndValidation.IsCurrentUserInX7GateSet();
        bool allowAdminByDiagnostics = _options.EnableVerboseDiagnostics.Value;
        bool showAdmin = showServerRows && (p0 || allowAdminByDiagnostics);

        float panelWidth;
        float panelHeight;

        float listTop = PanelPaddingY + HeaderHeight + SectionGap + InputBandHeight + SectionGap + ActionBandHeight + SectionGap;
        float footerTopOffset = FooterBottomPadding + FooterHeight;
        float expandedMinBodyHeight = listTop + footerTopOffset + SectionGap;

        if (showServerRows)
        {
            float maxPanelWidth = Math.Max(MainPanelExpandedMinWidth, Screen.width - (PanelMargin * 2f));

            // Keep the left admin telemetry rail visible on typical widescreen layouts.
            if (showAdmin)
            {
                float maxPanelWidthWithAdminRail =
                    Screen.width
                    - (PanelMargin * 3f)
                    - AdminPanelGap
                    - AdminPanelMinWidth;

                if (maxPanelWidthWithAdminRail >= MainPanelExpandedMinWidth)
                {
                    maxPanelWidth = Math.Min(maxPanelWidth, maxPanelWidthWithAdminRail);
                }
            }

            panelWidth = Math.Min(MainPanelExpandedMaxWidth, maxPanelWidth);
            int visibleSessionRows = Math.Min(sessions.Count, MaxVisibleSessionRows);
            float desiredPanelHeight = expandedMinBodyHeight + CalculateSessionListHeight(visibleSessionRows);
            float maxPanelHeight = Math.Max(MainPanelExpandedMinHeight, Screen.height - (PanelMargin * 2f));
            panelHeight = Mathf.Clamp(desiredPanelHeight, MainPanelExpandedMinHeight, maxPanelHeight);
        }
        else
        {
            panelWidth = MainPanelCollapsedWidth;
            panelHeight = MainPanelCollapsedHeight;
        }

        float panelX = Screen.width - panelWidth - PanelMargin;
        float panelY = PanelMargin;
        SetAbsoluteTopLeftRect(
            _panelRect!,
            panelX,
            panelY,
            panelWidth,
            panelHeight);

        string collapseToggleLabel = showServerRows
            ? "-"
            : "+";

        _collapseButtonText!.text = collapseToggleLabel;
        SetLocalTopLeftRect(_collapseButton!.GetComponent<RectTransform>(), panelWidth - 34f, 6f, 28f, 22f);

        _serverListTitleText!.gameObject.SetActive(showServerRows);

        if (showServerRows)
        {
            _serverListTitleText.text = "SERVER LIST";
            SetLocalTopLeftRect(_serverListTitleText.GetComponent<RectTransform>(), PanelPaddingX, PanelPaddingY, 260f, HeaderHeight);
        }

        float actionButtonY = showServerRows
            ? PanelPaddingY + HeaderHeight + SectionGap + InputBandHeight + SectionGap
            : 34f;

        _hostButton!.interactable = canHostFromInput;
        SetLocalTopLeftRect(_hostButton.GetComponent<RectTransform>(), PanelPaddingX, actionButtonY, HostButtonWidth, ActionBandHeight);

        if (showServerRows)
        {
            _roomNameLabelText!.gameObject.SetActive(true);
            _roomNameInput!.gameObject.SetActive(true);
            float inputBandY = PanelPaddingY + HeaderHeight + SectionGap;
            float roomLabelWidth = 122f;
            float roomFieldX = PanelPaddingX + roomLabelWidth + ControlGap;
            SetLocalTopLeftRect(_roomNameLabelText.GetComponent<RectTransform>(), PanelPaddingX, inputBandY + 6f, roomLabelWidth, 20f);
            SetLocalTopLeftRect(_roomNameInput.GetComponent<RectTransform>(), roomFieldX, inputBandY, panelWidth - roomFieldX - PanelPaddingX, InputBandHeight);

            if (!_roomNameInput.isFocused
                && !string.Equals(_roomNameInput.text, _lanPreferredRoomNameInput, StringComparison.Ordinal))
            {
                _isSyncingRoomInput = true;
                _roomNameInput.text = _lanPreferredRoomNameInput;
                _isSyncingRoomInput = false;
            }
        }
        else
        {
            _roomNameLabelText!.gameObject.SetActive(false);
            _roomNameInput!.gameObject.SetActive(false);
        }

        _joinButton!.gameObject.SetActive(showServerRows);
        _refreshButton!.gameObject.SetActive(showServerRows);
        _joinButton.interactable = canJoinSelected;

        if (showServerRows)
        {
            float joinX = PanelPaddingX + HostButtonWidth + ControlGap;
            float refreshX = panelWidth - PanelPaddingX - RefreshButtonWidth;
            float maxJoinWidth = Math.Max(140f, refreshX - joinX - ControlGap);

            SetLocalTopLeftRect(_joinButton.GetComponent<RectTransform>(), joinX, actionButtonY, Math.Min(JoinButtonWidth, maxJoinWidth), ActionBandHeight);
            SetLocalTopLeftRect(_refreshButton.GetComponent<RectTransform>(), refreshX, actionButtonY, RefreshButtonWidth, ActionBandHeight);
        }

        _lastRefreshText!.gameObject.SetActive(showServerRows);
        _modVersionText!.gameObject.SetActive(showServerRows);

        if (showServerRows)
        {
            float footerY = panelHeight - FooterBottomPadding - FooterHeight;
            float footerWidth = panelWidth - (PanelPaddingX * 2f);
            float footerHalfWidth = footerWidth * 0.5f;

            _lastRefreshText.text = lastRefreshLabel;
            _modVersionText.text = modVersionLabel;
            SetLocalTopLeftRect(_lastRefreshText.GetComponent<RectTransform>(), PanelPaddingX, footerY, footerHalfWidth, FooterHeight);
            SetLocalTopLeftRect(_modVersionText.GetComponent<RectTransform>(), PanelPaddingX + footerHalfWidth, footerY, footerHalfWidth, FooterHeight);
        }

        _hostUnavailableText!.gameObject.SetActive(showServerRows && !canHostFromInput);

        if (showServerRows && !canHostFromInput)
        {
            _hostUnavailableText.text = $"Cannot host: {hostUnavailableReason}";
            float warningX = PanelPaddingX + HostButtonWidth + ControlGap;
            float warningWidth = panelWidth - warningX - PanelPaddingX;
            SetLocalTopLeftRect(_hostUnavailableText.GetComponent<RectTransform>(), warningX, actionButtonY - 20f, warningWidth, 18f);
        }

        // Keep for future dedicated in-game log surface; do not overlay on top of room input.
        _pendingJoinUnavailableLog = canJoinSelected
            ? string.Empty
            : $"Join unavailable: {joinUnavailableReason}";

        RenderClientStatePanel(
            showServerRows,
            panelX,
            panelY,
            panelWidth,
            panelHeight);

        _adminPanelRect!.gameObject.SetActive(showAdmin);

        if (showAdmin)
        {
            _adminTitleText!.text = p0
                ? "ADMIN TELEMETRY"
                : "ADMIN TELEMETRY (DEBUG)";

            float availableAdminWidth = Math.Max(AdminPanelMinWidth, panelX - AdminPanelGap - PanelMargin);
            float adminPanelWidth = Mathf.Clamp(availableAdminWidth, AdminPanelMinWidth, AdminPanelMaxWidth);
            float adminBodyWidth = adminPanelWidth - (PanelPaddingX * 2f);
            float adminValueColumnOffsetPx = Mathf.Clamp(adminBodyWidth * 0.42f, 120f, adminBodyWidth - 24f);

            string adminData = selectedSession is null
                ? "Admin: select a session to view identity telemetry."
                : _statusPresenterBridge.BuildAdminTelemetryPanelData(
                    selectedSession,
                    BuildSessionIdentitySignature(selectedSession),
                    adminValueColumnOffsetPx);

            _adminBodyText!.textWrappingMode = TextWrappingModes.Normal;
            _adminBodyText.overflowMode = TextOverflowModes.Ellipsis;

            Vector2 bodyPreferredSize = _adminBodyText.GetPreferredValues(
                adminData,
                adminBodyWidth,
                4096f);

            float adminBodyHeight = Mathf.Ceil(bodyPreferredSize.y) + 4f;
            float desiredAdminPanelHeight =
                AdminPanelTopInset
                + HeaderHeight
                + AdminTitleToBodyGap
                + adminBodyHeight
                + AdminPanelBottomInset;
            float maxAdminPanelHeight = Mathf.Max(AdminPanelMinHeight, Screen.height - (PanelMargin * 2f));
            float adminPanelHeight = Mathf.Clamp(desiredAdminPanelHeight, AdminPanelMinHeight, maxAdminPanelHeight);
            float adminPanelX = Math.Max(
                PanelMargin,
                panelX - AdminPanelGap - adminPanelWidth);
            float adminPanelY = panelY;

            SetAbsoluteTopLeftRect(
                _adminPanelRect,
                adminPanelX,
                adminPanelY,
                adminPanelWidth,
                adminPanelHeight);

            SetLocalTopLeftRect(
                _adminTitleText!.GetComponent<RectTransform>(),
                PanelPaddingX,
                AdminPanelTopInset,
                adminBodyWidth,
                HeaderHeight);
            float adminBodyY = AdminPanelTopInset + HeaderHeight + AdminTitleToBodyGap;
            float adminBodyRectHeight = Math.Max(20f, adminPanelHeight - adminBodyY - AdminPanelBottomInset);
            SetLocalTopLeftRect(
                _adminBodyText.GetComponent<RectTransform>(),
                PanelPaddingX,
                adminBodyY,
                adminBodyWidth,
                adminBodyRectHeight);
            _adminBodyText!.text = adminData;
        }

        _emptyText!.gameObject.SetActive(false);
        _sessionScrollRect!.gameObject.SetActive(false);

        if (showServerRows)
        {
            float rowY = listTop;
            float maxListViewportHeight = Math.Max(
                24f,
                panelHeight - rowY - FooterHeight - FooterBottomPadding - SectionGap);
            int visibleSessionRows = Math.Min(sessions.Count, MaxVisibleSessionRows);
            float targetListViewportHeight = Math.Max(24f, CalculateSessionListHeight(visibleSessionRows));
            float listViewportHeight = Math.Min(targetListViewportHeight, maxListViewportHeight);

            if (sessions.Count == 0)
            {
                _emptyText.gameObject.SetActive(true);
                _emptyText.text = "No discovered sessions yet. Keep host in-room and click Refresh.";
                SetLocalTopLeftRect(_emptyText.GetComponent<RectTransform>(), PanelPaddingX, rowY + 8f, panelWidth - (PanelPaddingX * 2f), 22f);
                HideUnusedRows(0);
            }
            else
            {
                _sessionScrollRect.gameObject.SetActive(true);
                SetLocalTopLeftRect(_sessionScrollRect.GetComponent<RectTransform>(), PanelPaddingX, rowY, panelWidth - (PanelPaddingX * 2f), listViewportHeight);
                SetLocalTopLeftRect(_sessionViewportRect!, 0f, 0f, panelWidth - (PanelPaddingX * 2f), listViewportHeight);

                float rowStride = SessionRowHeight + SessionRowGap;
                float contentHeight = Math.Max(listViewportHeight, CalculateSessionListHeight(sessions.Count));
                _sessionContentRect!.sizeDelta = new Vector2(panelWidth - (PanelPaddingX * 2f) - 18f, contentHeight);

                for (int index = 0; index < sessions.Count; index++)
                {
                    LanSessionRowUi row = EnsureSessionRow(index);
                    row.Root.gameObject.SetActive(true);
                    SetLocalTopLeftRect(row.Root, 0f, index * rowStride, _sessionContentRect.sizeDelta.x, SessionRowHeight);

                    LanSessionInfo session = sessions[index];
                    row.PrimaryLabel.text = BuildSessionPrimaryLine(session);
                    row.SecondaryLabel.text = BuildSessionSecondaryLine(session);

                    bool isSelected = index == selectedIndex;
                    row.Background.color = isSelected
                        ? UiSessionRowSelectedColor
                        : UiSessionRowColor;
                    row.PrimaryLabel.color = isSelected
                        ? UiSessionRowPrimarySelectedColor
                        : UiTextColor;
                    row.SecondaryLabel.color = UiMutedTextColor;
                }

                HideUnusedRows(sessions.Count);
            }
        }

        _hostButton.onClick.RemoveAllListeners();
        _hostButton.onClick.AddListener(() =>
        {
            _options.RoomName.Value = validatedHostRoomName;
            Plugin.Log.LogInfo("LAN UI host button clicked.");
            _directConnect.RequestDirectHostStart("LanUiHostButton");
        });

        _joinButton.onClick.RemoveAllListeners();
        _joinButton.onClick.AddListener(() =>
        {
            if (!canJoinSelected)
            {
                return;
            }

            Plugin.Log.LogInfo("LAN UI join-selected button clicked.");
            TryJoinSelectedLanSession();
        });
    }

    private void EnsureClientStateLogUpdated(
        string phase,
        DateTime phaseUpdatedAtUtc,
        string configuredEndpoint,
        LanErrorDetail? connectionError)
    {
        if (_clientStateLogEntries.Count == 0)
        {
            AppendClientStateLogEntry("Client state timeline initialized.");
        }

        string sanitizedEndpoint = _identityAndValidation.SanitizeEndpointForLog(configuredEndpoint);

        if (!string.Equals(_lastLoggedEndpoint, sanitizedEndpoint, StringComparison.Ordinal))
        {
            AppendClientStateLogEntry($"Configured endpoint: {sanitizedEndpoint}");
            _lastLoggedEndpoint = sanitizedEndpoint;
        }

        string normalizedPhase = string.IsNullOrWhiteSpace(phase)
            ? "Unknown"
            : phase.Trim();

        if (!string.Equals(_lastLoggedConnectionPhase, normalizedPhase, StringComparison.Ordinal))
        {
            AppendClientStateLogEntry(
                $"Connection phase: {normalizedPhase} (updated {phaseUpdatedAtUtc:HH:mm:ss} UTC)");
            _lastLoggedConnectionPhase = normalizedPhase;
        }

        string errorSignature = BuildErrorSignature(connectionError);

        if (!string.Equals(_lastLoggedErrorSignature, errorSignature, StringComparison.Ordinal))
        {
            if (connectionError is null)
            {
                AppendClientStateLogEntry("Structured LAN error cleared.");
            }
            else
            {
                AppendClientStateLogEntry(
                    $"Error: {connectionError.Code} - {connectionError.Message} (source {connectionError.Source})");
            }

            _lastLoggedErrorSignature = errorSignature;
        }
    }

    private void RenderClientStatePanel(
        bool showServerRows,
        float panelX,
        float panelY,
        float panelWidth,
        float panelHeight)
    {
        if (_statePanelRect is null
            || _stateTitleText is null
            || _stateLatestText is null
            || _stateLogScrollRect is null
            || _stateLogViewportRect is null
            || _stateLogContentRect is null
            || _stateLogBodyText is null)
        {
            return;
        }

        float statePanelX = panelX;
        float statePanelY = panelY + panelHeight + StatePanelGap;
        float availableHeight = Screen.height - statePanelY - PanelMargin;

        if (availableHeight < StatePanelMinVisibleHeight)
        {
            _statePanelRect.gameObject.SetActive(false);
            return;
        }

        float desiredPanelHeight = showServerRows
            ? StatePanelExpandedHeight
            : StatePanelCollapsedHeight;
        float statePanelHeight = Math.Min(desiredPanelHeight, availableHeight);
        float statePanelWidth = panelWidth;

        _statePanelRect.gameObject.SetActive(true);
        SetAbsoluteTopLeftRect(
            _statePanelRect,
            statePanelX,
            statePanelY,
            statePanelWidth,
            statePanelHeight);

        _stateTitleText.text = "LOG";
        SetLocalTopLeftRect(
            _stateTitleText.GetComponent<RectTransform>(),
            PanelPaddingX,
            StatePanelTopInset,
            statePanelWidth - (PanelPaddingX * 2f),
            HeaderHeight);

        string latestEntry = _clientStateLogEntries.Count == 0
            ? "Latest: waiting for status updates"
            : $"Latest: {_clientStateLogEntries[_clientStateLogEntries.Count - 1]}";

        _stateLatestText.text = latestEntry;
        float latestY = statePanelHeight - StatePanelBottomInset - StateLatestHeight;
        SetLocalTopLeftRect(
            _stateLatestText.GetComponent<RectTransform>(),
            PanelPaddingX,
            latestY,
            statePanelWidth - (PanelPaddingX * 2f),
            StateLatestHeight);

        float logY = StatePanelTopInset + HeaderHeight + StateTitleToLogGap;
        float logHeight = Math.Max(
            28f,
            latestY - StateLogToLatestGap - logY);
        float logWidth = statePanelWidth - (PanelPaddingX * 2f);

        SetLocalTopLeftRect(_stateLogScrollRect.GetComponent<RectTransform>(), PanelPaddingX, logY, logWidth, logHeight);
        SetLocalTopLeftRect(_stateLogViewportRect, 0f, 0f, logWidth, logHeight);

        bool shouldStickToBottom = _stateLogScrollRect.verticalNormalizedPosition <= 0.05f;
        string historyText = BuildStateHistoryText();

        if (!string.Equals(_lastRenderedStateLogText, historyText, StringComparison.Ordinal))
        {
            _stateLogBodyText.text = historyText;
            _lastRenderedStateLogText = historyText;
        }

        float textWidth = Math.Max(100f, logWidth - (StateLogTextInsetX * 2f));
        Vector2 preferredSize = _stateLogBodyText.GetPreferredValues(
            _stateLogBodyText.text,
            textWidth,
            4096f);

        float contentHeight = Math.Max(
            logHeight,
            Mathf.Ceil(preferredSize.y) + StateLogTextInsetTop + StateLogTextInsetBottom);
        _stateLogContentRect.sizeDelta = new Vector2(logWidth - 2f, contentHeight);
        SetLocalTopLeftRect(
            _stateLogBodyText.GetComponent<RectTransform>(),
            StateLogTextInsetX,
            StateLogTextInsetTop,
            textWidth,
            contentHeight - StateLogTextInsetTop - StateLogTextInsetBottom);

        if (shouldStickToBottom)
        {
            _stateLogScrollRect.verticalNormalizedPosition = 0f;
        }
    }

    private string BuildStateHistoryText()
    {
        if (_clientStateLogEntries.Count == 0)
        {
            return "No client state updates yet.";
        }

        var builder = new StringBuilder();

        for (int index = 0; index < _clientStateLogEntries.Count; index++)
        {
            if (index > 0)
            {
                builder.Append(Environment.NewLine);
            }

            builder.Append(_clientStateLogEntries[index]);
        }

        return builder.ToString();
    }

    private void AppendClientStateLogEntry(string message)
    {
        string timestamp = DateTime.UtcNow.ToString("HH:mm:ss");
        string normalizedMessage = string.IsNullOrWhiteSpace(message)
            ? "(empty update)"
            : message.Trim();

        _clientStateLogEntries.Add($"[{timestamp}] {normalizedMessage}");

        if (_clientStateLogEntries.Count > MaxClientStateLogEntries)
        {
            int removeCount = _clientStateLogEntries.Count - MaxClientStateLogEntries;
            _clientStateLogEntries.RemoveRange(0, removeCount);
        }
    }

    private static string BuildErrorSignature(LanErrorDetail? connectionError)
    {
        if (connectionError is null)
        {
            return "None";
        }

        return string.Concat(
            connectionError.Code,
            "|",
            connectionError.Message,
            "|",
            connectionError.Source,
            "|",
            connectionError.Context,
            "|",
            connectionError.OccurredAtUtc.ToString("O"));
    }

    private void OnCollapseClicked()
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

    private void OnRefreshClicked()
    {
        RefreshLanUiSessions();
        Plugin.Log.LogInfo(
            $"LAN UI refresh clicked. SessionCount={_discoveredSessionsViewModel.SessionCount}; RefreshedAtUtc={_lastLanUiRefreshAtUtc:O}");
    }

    private void OnRoomNameInputChanged(string value)
    {
        if (_isSyncingRoomInput)
        {
            return;
        }

        if (!string.Equals(value, _lanPreferredRoomNameInput, StringComparison.Ordinal))
        {
            _lanPreferredRoomNameInput = value;
            _options.RoomName.Value = value;
        }
    }

    private void OnRoomNameInputEndEdit(string value)
    {
        string normalized = _identityAndValidation.NormalizeRoomNameInputForUi(value);

        _lanPreferredRoomNameInput = normalized;
        _options.RoomName.Value = normalized;

        if (_roomNameInput != null
            && !string.Equals(_roomNameInput.text, normalized, StringComparison.Ordinal))
        {
            _isSyncingRoomInput = true;
            _roomNameInput.text = normalized;
            _isSyncingRoomInput = false;
        }
    }

    private void OnSessionRowClicked(int index)
    {
        if (!_discoveredSessionsViewModel.TrySelectIndex(index))
        {
            return;
        }

        LanSessionInfo? selected = _discoveredSessionsViewModel.GetSelectedSessionOrNull();

        if (selected is null)
        {
            return;
        }

        Plugin.Log.LogInfo(
            "LAN UI selected discovered session from list. " +
            $"Room={selected.RoomName}; " +
            $"Endpoint={_identityAndValidation.SanitizeEndpointForLog(selected.NameServerAddress)}:{selected.NameServerPort}; " +
            $"Compatible={selected.IsCompatible}; " +
            $"Reason={selected.IncompatibilityReason}");
    }

    private LanSessionRowUi EnsureSessionRow(int index)
    {
        while (_sessionRows.Count <= index)
        {
            int rowIndex = _sessionRows.Count;

            RectTransform rowRoot = CreateUiRect(
                $"LanSessionRow-{rowIndex}",
                _sessionContentRect!);
            Image rowImage = rowRoot.gameObject.AddComponent<Image>();
            rowImage.sprite = EnsureRoundedSprite();
            rowImage.type = Image.Type.Sliced;
            AddFaintBorder(rowImage);

            Button rowButton = rowRoot.gameObject.AddComponent<Button>();
            ConfigureButtonColors(rowButton);
            rowButton.onClick.AddListener(() => OnSessionRowClicked(rowIndex));

            TMP_Text primaryLabel = CreateTmpText(
                "PrimaryLabel",
                rowRoot,
                string.Empty,
                TextAlignmentOptions.MidlineLeft,
                SessionPrimaryFontSize,
                FontStyles.Normal);
            primaryLabel.textWrappingMode = TextWrappingModes.NoWrap;
            SetLocalTopLeftRect(primaryLabel.GetComponent<RectTransform>(), SessionRowInnerPaddingX, SessionRowPrimaryTop, 1200f, 22f);

            TMP_Text secondaryLabel = CreateTmpText(
                "SecondaryLabel",
                rowRoot,
                string.Empty,
                TextAlignmentOptions.MidlineLeft,
                SessionSecondaryFontSize,
                FontStyles.Normal);
            secondaryLabel.textWrappingMode = TextWrappingModes.NoWrap;
            secondaryLabel.color = UiMutedTextColor;
            SetLocalTopLeftRect(secondaryLabel.GetComponent<RectTransform>(), SessionRowInnerPaddingX, SessionRowSecondaryTop, 1200f, 20f);

            _sessionRows.Add(new LanSessionRowUi(
                rowRoot,
                rowImage,
                rowButton,
                primaryLabel,
                secondaryLabel));
        }

        return _sessionRows[index];
    }

    private void HideUnusedRows(int fromIndex)
    {
        for (int index = fromIndex; index < _sessionRows.Count; index++)
        {
            _sessionRows[index].Root.gameObject.SetActive(false);
        }
    }

    private void EnsureOverlayUi()
    {
        EnsureEventSystemExists();
        EnsureTemplateText();

        Canvas canvas = LanOverlayGuiText.EnsureOverlayCanvas(
            ref _overlayCanvasObject,
            "PeakLanMod-LanOverlayCanvas",
            sortingOrder: 4200);

        if (_panelRect != null)
        {
            return;
        }

        _panelRect = CreateUiRect("LanPanel", canvas.transform);
        _panelImage = _panelRect.gameObject.AddComponent<Image>();
        _panelImage.sprite = EnsureRoundedSprite();
        _panelImage.type = Image.Type.Sliced;
        _panelImage.color = UiPanelColor;
        AddFaintBorder(_panelImage);

        (_collapseButton, _collapseButtonText) = CreateButton(
            "CollapseButton",
            _panelRect,
            "-",
            16f,
            FontStyles.Normal,
            OnCollapseClicked);

        _serverListTitleText = CreateTmpText(
            "ServerListTitle",
            _panelRect,
            "SERVER LIST",
            TextAlignmentOptions.TopLeft,
            TitleFontSize,
            FontStyles.Normal);

        _roomNameLabelText = CreateTmpText(
            "RoomNameLabel",
            _panelRect,
            "ROOM NAME:",
            TextAlignmentOptions.TopLeft,
            LabelFontSize,
            FontStyles.Normal);

        _roomNameInput = CreateInputField(
            "RoomNameInput",
            _panelRect,
            _lanPreferredRoomNameInput,
            OnRoomNameInputChanged,
            out _roomNameInputText,
            out _roomNameInputPlaceholder);

        (_hostButton, _hostButtonText) = CreateButton(
            "HostButton",
            _panelRect,
            "HOST LAN",
            22f,
            FontStyles.Normal,
            null);

        (_joinButton, _joinButtonText) = CreateButton(
            "JoinButton",
            _panelRect,
            "JOIN SELECTED",
            22f,
            FontStyles.Normal,
            null);

        (_refreshButton, _refreshButtonText) = CreateButton(
            "RefreshButton",
            _panelRect,
            "REFRESH",
            22f,
            FontStyles.Normal,
            OnRefreshClicked);

        _hostUnavailableText = CreateTmpText(
            "HostUnavailableText",
            _panelRect,
            string.Empty,
            TextAlignmentOptions.TopLeft,
            14f,
            FontStyles.Normal);

        _emptyText = CreateTmpText(
            "EmptyText",
            _panelRect,
            string.Empty,
            TextAlignmentOptions.TopLeft,
            16f,
            FontStyles.Normal);

        (_sessionScrollRect, _sessionViewportRect, _sessionContentRect) = CreateScrollRegion(
            "SessionScroll",
            _panelRect);

        _lastRefreshText = CreateTmpText(
            "LastRefreshText",
            _panelRect,
            string.Empty,
            TextAlignmentOptions.TopLeft,
            FooterFontSize,
            FontStyles.Normal);

        _modVersionText = CreateTmpText(
            "VersionText",
            _panelRect,
            string.Empty,
            TextAlignmentOptions.TopRight,
            FooterFontSize,
            FontStyles.Normal);

        _statePanelRect = CreateUiRect("ClientStatePanel", canvas.transform);
        _statePanelImage = _statePanelRect.gameObject.AddComponent<Image>();
        _statePanelImage.sprite = EnsureRoundedSprite();
        _statePanelImage.type = Image.Type.Sliced;
        _statePanelImage.color = UiPanelSecondaryColor;
        AddFaintBorder(_statePanelImage);

        _stateTitleText = CreateTmpText(
            "StateTitle",
            _statePanelRect,
            "LOG",
            TextAlignmentOptions.TopLeft,
            TitleFontSize,
            FontStyles.Normal);

        _stateLatestText = CreateTmpText(
            "StateLatest",
            _statePanelRect,
            "Latest: waiting for status updates",
            TextAlignmentOptions.TopLeft,
            FooterFontSize + 2f,
            FontStyles.Normal);
        _stateLatestText.color = UiStateLatestTextColor;
        _stateLatestText.textWrappingMode = TextWrappingModes.NoWrap;

        (_stateLogScrollRect, _stateLogViewportRect, _stateLogContentRect) = CreateScrollRegion(
            "StateLogScroll",
            _statePanelRect);

        Image? stateLogRootImage = _stateLogScrollRect.GetComponent<Image>();

        if (stateLogRootImage != null)
        {
            stateLogRootImage.color = UiLogSurfaceColor;
            AddFaintBorder(stateLogRootImage, UiLogBorderColor, new Vector2(1f, -1f));
        }

        Image? stateLogViewportImage = _stateLogViewportRect.GetComponent<Image>();

        if (stateLogViewportImage != null)
        {
            stateLogViewportImage.color = UiLogViewportColor;
            AddFaintBorder(stateLogViewportImage, UiLogBorderColor, new Vector2(1f, -1f));
        }

        _stateLogBodyText = CreateTmpText(
            "StateLogBody",
            _stateLogContentRect,
            string.Empty,
            TextAlignmentOptions.TopLeft,
            14f,
            FontStyles.Normal);
        _stateLogBodyText.color = new Color(0.93f, 0.97f, 1f, 1f);
        _stateLogBodyText.textWrappingMode = TextWrappingModes.Normal;
        _stateLogBodyText.overflowMode = TextOverflowModes.Overflow;
        _stateLogBodyText.richText = false;

        _adminPanelRect = CreateUiRect("AdminPanel", canvas.transform);
        _adminPanelImage = _adminPanelRect.gameObject.AddComponent<Image>();
        _adminPanelImage.sprite = EnsureRoundedSprite();
        _adminPanelImage.type = Image.Type.Sliced;
        _adminPanelImage.color = UiPanelSecondaryColor;
        AddFaintBorder(_adminPanelImage);

        _adminTitleText = CreateTmpText(
            "AdminTitle",
            _adminPanelRect,
            "ADMIN TELEMETRY",
            TextAlignmentOptions.TopLeft,
            TitleFontSize,
            FontStyles.Normal);

        _adminBodyText = CreateTmpText(
            "AdminBody",
            _adminPanelRect,
            string.Empty,
            TextAlignmentOptions.TopLeft,
            AdminBodyFontSize,
            FontStyles.Normal);

        _adminBodyText.richText = true;
        _adminBodyText.textWrappingMode = TextWrappingModes.Normal;
        _adminBodyText.overflowMode = TextOverflowModes.Ellipsis;

        SetLocalTopLeftRect(_adminTitleText.GetComponent<RectTransform>(), PanelPaddingX, AdminPanelTopInset, 400f, HeaderHeight);
        SetLocalTopLeftRect(_adminBodyText.GetComponent<RectTransform>(), PanelPaddingX, AdminPanelTopInset + HeaderHeight + AdminTitleToBodyGap, 400f, 30f);
    }

    private void EnsureTemplateText()
    {
        if (_templateText != null)
        {
            return;
        }

        if (LanOverlayGuiText.TryFindExistingTmpText(
                "Timer & Height UI",
                out TMP_Text? template)
            && template != null)
        {
            _templateText = template;
            return;
        }

        TMP_Text[] texts = UnityEngine.Object.FindObjectsByType<TMP_Text>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);

        for (int index = 0; index < texts.Length; index++)
        {
            TMP_Text candidate = texts[index];

            if (candidate == null
                || !candidate.gameObject.activeInHierarchy
                || candidate.font == null)
            {
                continue;
            }

            _templateText = candidate;
            return;
        }
    }

    private static void EnsureEventSystemExists()
    {
        EventSystem? existing = UnityEngine.Object.FindFirstObjectByType<EventSystem>();

        if (existing != null)
        {
            return;
        }

        var go = new GameObject(
            "LanOverlayEventSystem",
            typeof(EventSystem),
            typeof(StandaloneInputModule));
        UnityEngine.Object.DontDestroyOnLoad(go);
    }

    private RectTransform CreateUiRect(
        string name,
        Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        return rt;
    }

    private TMP_Text CreateTmpText(
        string name,
        Transform parent,
        string initialText,
        TextAlignmentOptions alignment,
        float size,
        FontStyles style)
    {
        var go = new GameObject(
            name,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(TextMeshProUGUI));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);

        TMP_Text text = go.GetComponent<TMP_Text>();

        if (_templateText != null)
        {
            text.font = _templateText.font;
        }

        text.color = UiTextColor;

        LanOverlayGuiText.ApplyTmpStyle(
            text,
            LanOverlayGuiText.CreateDefaultStyle(alignment));

        text.fontSize = size;
        text.fontStyle = style;
        text.text = initialText;
        text.textWrappingMode = TextWrappingModes.NoWrap;

        return text;
    }

    private Text CreateLegacyUiText(
        string name,
        Transform parent,
        string initialText,
        Font font,
        int fontSize,
        FontStyle fontStyle,
        TextAnchor alignment)
    {
        var go = new GameObject(
            name,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Text));

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);

        Text text = go.GetComponent<Text>();
        text.font = font;
        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.alignment = alignment;
        text.color = UiTextColor;
        text.text = initialText;

        return text;
    }

    private Font ResolveLegacyInputFont()
    {
        TMP_Text? template = _templateText;

        if (template?.font != null)
        {
            Font? source = template.font.sourceFontFile;

            if (source != null)
            {
                return source;
            }
        }

        Font? arial = Resources.GetBuiltinResource<Font>("Arial.ttf");

        if (arial != null)
        {
            return arial;
        }

        // Last-resort fallback for environments where Arial alias differs.
        return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }

    private InputField CreateInputField(
        string name,
        Transform parent,
        string initialValue,
        Action<string> onChanged,
        out Text inputText,
        out Text placeholder)
    {
        RectTransform root = CreateUiRect(name, parent);
        Image bg = root.gameObject.AddComponent<Image>();
        bg.sprite = EnsureRoundedSprite();
        bg.type = Image.Type.Sliced;
        bg.color = UiFieldColor;
        AddFaintBorder(bg);

        InputField input = root.gameObject.AddComponent<InputField>();
        input.targetGraphic = bg;
        input.lineType = InputField.LineType.SingleLine;
        input.characterLimit = 64;
        input.customCaretColor = true;
        input.caretColor = new Color(0.08f, 0.05f, 0.03f, 1f);
        input.selectionColor = new Color(0.22f, 0.14f, 0.07f, 0.8f);
        input.caretWidth = 3;
        input.caretBlinkRate = 0.85f;

        RectTransform textViewport = CreateUiRect("TextViewport", root);
        Image viewportImage = textViewport.gameObject.AddComponent<Image>();
        viewportImage.color = new Color(0f, 0f, 0f, 0.002f);
        textViewport.gameObject.AddComponent<Mask>().showMaskGraphic = false;

        // Fill full input width while keeping a small inset for text legibility.
        textViewport.anchorMin = new Vector2(0f, 0f);
        textViewport.anchorMax = new Vector2(1f, 1f);
        textViewport.pivot = new Vector2(0.5f, 0.5f);
        textViewport.offsetMin = new Vector2(8f, 4f);
        textViewport.offsetMax = new Vector2(-8f, -4f);

        Font textFont = ResolveLegacyInputFont();

        inputText = CreateLegacyUiText(
            "Text",
            textViewport,
            initialValue,
            textFont,
            18,
            FontStyle.Normal,
            TextAnchor.MiddleLeft);
        inputText.raycastTarget = false;
        inputText.supportRichText = false;
        inputText.horizontalOverflow = HorizontalWrapMode.Overflow;
        inputText.verticalOverflow = VerticalWrapMode.Overflow;

        placeholder = CreateLegacyUiText(
            "Placeholder",
            textViewport,
            "Enter room name",
            textFont,
            18,
            FontStyle.Italic,
            TextAnchor.MiddleLeft);
        placeholder.color = new Color(0.33f, 0.26f, 0.19f, 0.72f);
        placeholder.raycastTarget = false;
        placeholder.supportRichText = false;

        SetFillRect(inputText.GetComponent<RectTransform>());
        SetFillRect(placeholder.GetComponent<RectTransform>());

        input.textComponent = inputText;
        input.placeholder = placeholder;
        input.text = initialValue;
        input.onValueChanged.AddListener(value => onChanged(value));
        input.onEndEdit.AddListener(value => OnRoomNameInputEndEdit(value));

        return input;
    }

    private (Button button, TMP_Text label) CreateButton(
        string name,
        Transform parent,
        string text,
        float fontSize,
        FontStyles style,
        Action? onClick)
    {
        RectTransform root = CreateUiRect(name, parent);
        Image bg = root.gameObject.AddComponent<Image>();
        bg.sprite = EnsureRoundedSprite();
        bg.type = Image.Type.Sliced;
        bg.color = UiButtonColor;
        AddFaintBorder(bg);

        Button button = root.gameObject.AddComponent<Button>();
        ConfigureButtonColors(button);

        if (onClick != null)
        {
            button.onClick.AddListener(() => onClick());
        }

        TMP_Text label = CreateTmpText(
            "Label",
            root,
            text,
            TextAlignmentOptions.Center,
            fontSize,
            style);
        label.color = new Color(0.17f, 0.13f, 0.08f, 1f);
        SetFillRect(label.GetComponent<RectTransform>());
        return (button, label);
    }

    private (ScrollRect scroll, RectTransform viewport, RectTransform content) CreateScrollRegion(
        string name,
        Transform parent)
    {
        RectTransform root = CreateUiRect(name, parent);

        Image bg = root.gameObject.AddComponent<Image>();
        bg.sprite = EnsureRoundedSprite();
        bg.type = Image.Type.Sliced;
        bg.color = UiPanelSecondaryColor;
        AddFaintBorder(bg);

        ScrollRect scroll = root.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal = false;

        RectTransform viewport = CreateUiRect("Viewport", root);
        Image viewportImage = viewport.gameObject.AddComponent<Image>();
        viewportImage.sprite = EnsureRoundedSprite();
        viewportImage.type = Image.Type.Sliced;
        viewportImage.color = new Color(1f, 1f, 1f, 0.06f);
        AddFaintBorder(viewportImage);
        viewport.gameObject.AddComponent<RectMask2D>();

        RectTransform content = CreateUiRect("Content", viewport);
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0f, 1f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = new Vector2(0f, 0f);

        scroll.viewport = viewport;
        scroll.content = content;
        scroll.movementType = ScrollRect.MovementType.Clamped;

        SetLocalTopLeftRect(viewport, 0f, 0f, 100f, 100f);

        return (scroll, viewport, content);
    }

    private void ConfigureButtonColors(Button button)
    {
        ColorBlock colors = button.colors;
        colors.normalColor = UiButtonColor;
        colors.highlightedColor = UiButtonHoverColor;
        colors.pressedColor = UiButtonPressedColor;
        colors.disabledColor = UiDisabledColor;
        colors.selectedColor = colors.highlightedColor;
        colors.fadeDuration = 0.1f;
        button.colors = colors;
    }

    private static string BuildSessionPrimaryLine(LanSessionInfo session)
    {
        return $"{session.RoomName} @ {session.NameServerAddress}:{session.NameServerPort}";
    }

    private static float CalculateSessionListHeight(int rowCount)
    {
        if (rowCount <= 0)
        {
            return 0f;
        }

        return (rowCount * SessionRowHeight) + ((rowCount - 1) * SessionRowGap);
    }

    private static string BuildSessionSecondaryLine(LanSessionInfo session)
    {
        string compatibility = session.IsCompatible
            ? "Compatible"
            : session.IncompatibilityReason;
        return $"{session.Transport} | {compatibility} | Scene: {session.Scene}";
    }

    private Sprite EnsureRoundedSprite()
    {
        if (_solidSprite != null)
        {
            return _solidSprite;
        }

        const int size = 24;
        const int radius = 5;

        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            hideFlags = HideFlags.HideAndDontSave
        };

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                bool opaque = IsInsideRoundedRect(x, y, size, radius);
                texture.SetPixel(x, y, opaque ? Color.white : Color.clear);
            }
        }

        texture.Apply(updateMipmaps: false, makeNoLongerReadable: false);

        _solidSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, size, size),
            new Vector2(0.5f, 0.5f),
            100f,
            0,
            SpriteMeshType.FullRect,
            new Vector4(radius, radius, radius, radius));

        _solidSprite.hideFlags = HideFlags.HideAndDontSave;
        return _solidSprite;
    }

    private static bool IsInsideRoundedRect(
        int x,
        int y,
        int size,
        int radius)
    {
        bool left = x < radius;
        bool right = x >= size - radius;
        bool bottom = y < radius;
        bool top = y >= size - radius;

        if ((!left && !right) || (!top && !bottom))
        {
            return true;
        }

        float centerX = left
            ? radius - 1
            : size - radius;
        float centerY = bottom
            ? radius - 1
            : size - radius;

        float dx = x - centerX;
        float dy = y - centerY;
        return (dx * dx) + (dy * dy) <= (radius * radius);
    }

    private static void AddFaintBorder(Graphic graphic)
    {
        AddFaintBorder(graphic, UiBorderColor, new Vector2(1f, -1f));
    }

    private static void AddFaintBorder(
        Graphic graphic,
        Color borderColor,
        Vector2 effectDistance)
    {
        Outline border = graphic.gameObject.GetComponent<Outline>()
            ?? graphic.gameObject.AddComponent<Outline>();
        border.effectColor = borderColor;
        border.effectDistance = effectDistance;
        border.useGraphicAlpha = true;
    }

    private static void SetAbsoluteTopLeftRect(
        RectTransform rect,
        float x,
        float y,
        float width,
        float height)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(width, height);
    }

    private static void SetLocalTopLeftRect(
        RectTransform rect,
        float x,
        float y,
        float width,
        float height)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(width, height);
    }

    private static void SetFillRect(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.anchoredPosition = Vector2.zero;
    }

    private void SetOverlayActive(bool active)
    {
        if (_overlayCanvasObject == null)
        {
            return;
        }

        _overlayCanvasObject.SetActive(active);

        if (_adminPanelRect != null)
        {
            _adminPanelRect.gameObject.SetActive(active && _adminPanelRect.gameObject.activeSelf);
        }

        if (_statePanelRect != null)
        {
            _statePanelRect.gameObject.SetActive(active && _statePanelRect.gameObject.activeSelf);
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

    private string BuildSessionIdentitySignature(
        LanSessionInfo session)
    {
        return _identityAndValidation.Fingerprint(
            $"{session.SourceAddress}|{session.HostDisplayName}");
    }

    private sealed class LanSessionRowUi
    {
        internal LanSessionRowUi(
            RectTransform root,
            Image background,
            Button button,
            TMP_Text primaryLabel,
            TMP_Text secondaryLabel)
        {
            Root = root;
            Background = background;
            Button = button;
            PrimaryLabel = primaryLabel;
            SecondaryLabel = secondaryLabel;
        }

        internal RectTransform Root { get; }

        internal Image Background { get; }

        internal Button Button { get; }

        internal TMP_Text PrimaryLabel { get; }

        internal TMP_Text SecondaryLabel { get; }
    }
}
