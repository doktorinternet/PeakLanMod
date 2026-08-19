using System;
using ExitGames.Client.Photon.StructWrapping;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace PeakLanMod;

internal sealed class MainMenuBouncingTaglineOverlay : MonoBehaviour
{
    private const string TitleSceneName = "Title";
    private const string OverlayObjectName = "PeakLanMod_MainMenuTagline";
    private const string DisplayedText = "Internyet edition!";
    private Color DisplayedTextColor = new(0.83f, 0.73f, 0.32f, 1.00f);
    private const float PulseSpeed = 2.2f;
    private const float BaseFontSize = 45f;
    private const float ScaleMin = 0.82f;
    private const float ScaleMax = 1.18f;
    private const float HorizontalOffset = -44f;
    private const float VerticalOffset = 18f;

    private TMP_Text? _overlayText;
    private RectTransform? _overlayRect;
    private GameObject? _titleLogo;
    private RectTransform? _titleRect;
    private float _lastTitleSearchAt = -999f;
    private readonly Vector3[] _titleWorldCorners = new Vector3[4];

    private void Update()
    {
        if (!IsTitleSceneLoaded())
        {
            SetOverlayVisible(false);
            _titleLogo = null;
            _titleRect = null;
            return;
        }

        if (!TryEnsureTitleTarget())
        {
            SetOverlayVisible(false);
            return;
        }

        EnsureOverlayText();

        if (_overlayText is null || _overlayRect is null || _titleRect is null)
        {
            return;
        }

        // RepositionOverlay();
        _overlayRect.anchoredPosition = _titleRect.anchoredPosition + new Vector2(300f, -75f);
        _overlayRect.localEulerAngles = new Vector3(0f, 0f, 20f);
        AnimatePulse(); 
        SetOverlayVisible(true);
    }

    private static bool IsTitleSceneLoaded()
    {
        Scene scene = SceneManager.GetActiveScene();
        return scene.isLoaded
            && string.Equals(scene.name, TitleSceneName, StringComparison.OrdinalIgnoreCase);
    }

    private bool TryEnsureTitleTarget()
    {
        if (_titleLogo != null
            && _titleRect != null
            && _titleLogo.gameObject.activeInHierarchy)
        {
            return true;
        }

        float now = Time.unscaledTime;

        if (now - _lastTitleSearchAt < 0.5f)
        {
            return _titleLogo != null && _titleRect != null;
        }

        _lastTitleSearchAt = now;

        var logo = GameObject.Find("MainMenu/Canvas/MainPage/Logo/Logo").gameObject;

        _titleLogo = logo;
        _titleRect = logo?.GetComponent<RectTransform>();

        return _titleLogo != null && _titleRect != null;
    }

    private void EnsureOverlayText()
    {
        if (_overlayText != null && _overlayRect != null)
        {
            return;
        }

        if (_titleLogo == null)
        {
            return;
        }

        Transform parent = _titleLogo.transform.parent;
        GameObject? existing = GameObject.Find(OverlayObjectName);
        GameObject overlayObject;

        if (existing != null)
        {
            overlayObject = existing;
        }
        else
        {
            overlayObject = new GameObject(
                OverlayObjectName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI));
            overlayObject.transform.SetParent(parent, false);
        }

        if (overlayObject.transform.parent != parent)
        {
            overlayObject.transform.SetParent(parent, false);
        }

        _overlayText = overlayObject.GetComponent<TMP_Text>();
        _overlayRect = overlayObject.GetComponent<RectTransform>();

        if (_overlayText == null || _overlayRect == null)
        {
            return;
        }

        var versionText = GameObject.Find("MainMenu/Canvas/MainPage/Menu/Buttons/Button_Credits/Hinge/Text")?.GetComponent<TMP_Text>();
        _overlayText.font = versionText?.font;
        _overlayText.fontSharedMaterial = versionText?.fontSharedMaterial;
        _overlayText.outlineColor = versionText?.outlineColor ?? Color.black;
        _overlayText.outlineWidth = versionText?.outlineWidth ?? 0f;
        _overlayText.fontStyle = FontStyles.Normal;
        _overlayText.alignment = TextAlignmentOptions.Center;
        _overlayText.textWrappingMode = TextWrappingModes.NoWrap;
        _overlayText.overflowMode = TextOverflowModes.Overflow;
        _overlayText.raycastTarget = false;
        _overlayText.color = DisplayedTextColor;
        _overlayText.fontSize = BaseFontSize;
        _overlayText.text = DisplayedText;

        _overlayRect.anchorMin = new Vector2(0.5f, 0.5f);
        _overlayRect.anchorMax = new Vector2(0.5f, 0.5f);
        _overlayRect.pivot = new Vector2(0.5f, 0.5f);
        _overlayRect.sizeDelta = new Vector2(420f, 96f);
    }

    private void RepositionOverlay()
    {
        if (_overlayRect == null || _titleRect == null || _overlayText == null)
        {
            return;
        }

        if (_overlayRect.parent is not RectTransform parentRect)
        {
            return;
        }

        _titleRect.GetWorldCorners(_titleWorldCorners);
        Vector3 bottomRightWorld = _titleWorldCorners[3];
        Vector3 targetWorld = bottomRightWorld + new Vector3(HorizontalOffset, VerticalOffset, 0f);

        Canvas? canvas = parentRect.GetComponentInParent<Canvas>();
        Camera? camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvas.worldCamera
            : null;

        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(camera, targetWorld);

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parentRect,
                screenPoint,
                camera,
                out Vector2 localPoint))
        {
            return;
        }

        _overlayRect.anchoredPosition = localPoint;
    }

    private void AnimatePulse()
    {
        if (_overlayRect == null)
        {
            return;
        }

        float normalized = (Mathf.Sin(Time.unscaledTime * PulseSpeed) + 1f) * 0.5f;
        float scale = Mathf.Lerp(ScaleMin, ScaleMax, normalized);
        _overlayRect.localScale = new Vector3(scale, scale, 1f);
    }

    private void SetOverlayVisible(bool visible)
    {
        if (_overlayText == null)
        {
            return;
        }

        if (_overlayText.gameObject.activeSelf != visible)
        {
            _overlayText.gameObject.SetActive(visible);
        }
    }
}