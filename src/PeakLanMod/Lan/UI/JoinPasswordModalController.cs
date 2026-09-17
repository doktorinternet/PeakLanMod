using System;
using PeakLanMod.Lan.Model;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PeakLanMod.Lan.UI;

internal sealed class JoinPasswordModalController
{
    private RectTransform? _root;
    private TMP_Text? _titleText;
    private TMP_Text? _subtitleText;
    private InputField? _passwordInput;
    private Button? _submitButton;
    private Button? _cancelButton;
    private LanSessionInfo? _pendingSession;
    private string _lastRoomName = string.Empty;

    internal LanSessionInfo? PendingSession => _pendingSession;
    internal bool IsVisible => _root != null && _root.gameObject.activeSelf;
    internal string CurrentPassword => _passwordInput != null ? _passwordInput.text : string.Empty;

    internal void EnsureUi(
        Transform parent,
        LanOverlayController widgetFactory)
    {
        if (_root != null)
        {
            return;
        }

        _root = widgetFactory.CreateUiRect("JoinPasswordModalRoot", parent);
        Image background = _root.gameObject.AddComponent<Image>();
        background.sprite = widgetFactory.EnsureRoundedSprite(12);
        background.type = Image.Type.Sliced;
        background.color = new Color(0.09f, 0.08f, 0.07f, 0.96f);
        LanOverlayController.AddBorder(background, new Color(0.74f, 0.62f, 0.46f, 0.8f), new Vector2(2f, -2f), useGraphicAlpha: false);
        _root.anchorMin = Vector2.zero;
        _root.anchorMax = Vector2.one;
        _root.offsetMin = Vector2.zero;
        _root.offsetMax = Vector2.zero;
        _root.gameObject.SetActive(false);

        _titleText = widgetFactory.CreateTmpText(
            "JoinPasswordTitle",
            _root,
            "PASSWORD REQUIRED",
            TextAlignmentOptions.TopLeft,
            18f,
            FontStyles.Bold);
        _titleText.color = new Color(0.94f, 0.82f, 0.66f, 1f);
        LanOverlayController.SetLocalTopLeftRect(_titleText.GetComponent<RectTransform>(), 20f, 16f, 260f, 24f);

        _subtitleText = widgetFactory.CreateTmpText(
            "JoinPasswordSubtitle",
            _root,
            "Enter the room password to continue.",
            TextAlignmentOptions.TopLeft,
            14f,
            FontStyles.Normal);
        _subtitleText.color = new Color(0.88f, 0.82f, 0.72f, 1f);
        LanOverlayController.SetLocalTopLeftRect(_subtitleText.GetComponent<RectTransform>(), 20f, 42f, 300f, 20f);

        _passwordInput = widgetFactory.CreateInputField(
            "JoinPasswordInput",
            _root,
            string.Empty,
            _ => { },
            out _,
            out _,
            placeholderText: "Room password",
            onEndEdit: _ => { });
        LanOverlayController.SetLocalTopLeftRect(_passwordInput.GetComponent<RectTransform>(), 20f, 72f, 320f, 34f);

        (_submitButton, _) = widgetFactory.CreateButton(
            "JoinPasswordSubmit",
            _root,
            "SUBMIT",
            16f,
            FontStyles.Bold,
            null);
        LanOverlayController.SetLocalTopLeftRect(_submitButton.GetComponent<RectTransform>(), 20f, 118f, 120f, 32f);

        (_cancelButton, _) = widgetFactory.CreateButton(
            "JoinPasswordCancel",
            _root,
            "CANCEL",
            16f,
            FontStyles.Normal,
            null);
        LanOverlayController.SetLocalTopLeftRect(_cancelButton.GetComponent<RectTransform>(), 156f, 118f, 120f, 32f);
    }

    internal void ShowForSession(
        LanSessionInfo session,
        Action onSubmit,
        Action onCancel)
    {
        if (_root == null)
        {
            return;
        }

        _pendingSession = session;
        _lastRoomName = session.RoomName;

        if (_subtitleText != null)
        {
            _subtitleText.text = $"Enter the password for {session.RoomName}.";
        }

        if (_passwordInput != null)
        {
            _passwordInput.text = string.Empty;
            _passwordInput.Select();
        }

        if (_submitButton != null)
        {
            _submitButton.onClick.RemoveAllListeners();
            _submitButton.onClick.AddListener(() => onSubmit());
        }

        if (_cancelButton != null)
        {
            _cancelButton.onClick.RemoveAllListeners();
            _cancelButton.onClick.AddListener(() => onCancel());
        }

        _root.gameObject.SetActive(true);
    }

    internal void Hide()
    {
        if (_root == null)
        {
            return;
        }

        _pendingSession = null;
        _lastRoomName = string.Empty;

        if (_passwordInput != null)
        {
            _passwordInput.text = string.Empty;
        }

        _root.gameObject.SetActive(false);
    }

    internal void ResetPendingSession()
    {
        _pendingSession = null;
        _lastRoomName = string.Empty;
    }

    internal bool TryGetPendingSession(out LanSessionInfo? session)
    {
        session = _pendingSession;
        return session != null;
    }
}
