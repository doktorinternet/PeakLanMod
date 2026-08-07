using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PeakLanMod.Lan.UI;

internal static class LanOverlayGuiText
{
    private static readonly Vector2 TopRightAnchor = new(1f, 1f);

    internal readonly struct TmpTextStyle
    {
        public TmpTextStyle(
            TextAlignmentOptions alignment,
            float fontSize,
            Color outlineColor,
            float outlineWidth,
            TextWrappingModes wrappingMode,
            bool autoSizeTextContainer)
        {
            Alignment = alignment;
            FontSize = fontSize;
            OutlineColor = outlineColor;
            OutlineWidth = outlineWidth;
            WrappingMode = wrappingMode;
            AutoSizeTextContainer = autoSizeTextContainer;
        }

        public TextAlignmentOptions Alignment { get; }

        public float FontSize { get; }

        public Color OutlineColor { get; }

        public float OutlineWidth { get; }

        public TextWrappingModes WrappingMode { get; }

        public bool AutoSizeTextContainer { get; }
    }

    public static TmpTextStyle CreateDefaultStyle(
        TextAlignmentOptions alignment)
    {
        return new TmpTextStyle(
            alignment,
            fontSize: 24f,
            outlineColor: new Color32(0, 0, 0, byte.MaxValue),
            outlineWidth: 0.045f,
            wrappingMode: TextWrappingModes.NoWrap,
            autoSizeTextContainer: false);
    }

    public static bool TryFindExistingTmpText(
        string objectName,
        out TMP_Text? text)
    {
        text = null;

        if (string.IsNullOrWhiteSpace(objectName))
        {
            return false;
        }

        GameObject source = GameObject.Find(objectName);

        if (source is null)
        {
            return false;
        }

        text = source.GetComponent<TMP_Text>();
        return text is not null;
    }

    public static bool TryCreateTopRightTextPairFromTemplate(
        string templateObjectName,
        string siblingObjectName,
        out TMP_Text? templateText,
        out TMP_Text? siblingText)
    {
        templateText = null;
        siblingText = null;

        if (!TryFindExistingTmpText(
                templateObjectName,
                out TMP_Text? resolvedTemplate)
            || resolvedTemplate is null)
        {
            return false;
        }

        templateText = resolvedTemplate;

        ApplyTmpStyle(
            templateText,
            CreateDefaultStyle((TextAlignmentOptions)257));

        ApplyTopRightLayout(
            templateText,
            new Vector2(300f, 0f),
            new Vector2(-10f, -10f));

        siblingText = CreateSiblingTmpText(
            templateText,
            siblingObjectName);

        ApplyTmpStyle(
            siblingText,
            CreateDefaultStyle((TextAlignmentOptions)260));

        ApplyTopRightLayout(
            siblingText,
            new Vector2(300f, 0f),
            new Vector2(-10f, -10f));

        return true;
    }

    public static TMP_Text CreateSiblingTmpText(
        TMP_Text template,
        string objectName)
    {
        var created = new GameObject(
            objectName,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(TextMeshProUGUI));

        created.transform.SetParent(template.transform.parent, false);
        created.layer = template.gameObject.layer;

        TMP_Text text = created.GetComponent<TMP_Text>();
        text.font = template.font;
        text.color = template.color;

        return text;
    }

    public static void ApplyTmpStyle(
        TMP_Text text,
        TmpTextStyle style)
    {
        text.autoSizeTextContainer = style.AutoSizeTextContainer;
        text.textWrappingMode = style.WrappingMode;
        text.alignment = style.Alignment;
        text.fontSize = style.FontSize;
        text.outlineColor = style.OutlineColor;
        text.outlineWidth = style.OutlineWidth;
    }

    public static void ApplyTopRightLayout(
        TMP_Text text,
        Vector2 sizeDelta,
        Vector2 anchoredPosition)
    {
        RectTransform rectTransform = text.GetComponent<RectTransform>();
        rectTransform.sizeDelta = sizeDelta;
        rectTransform.anchorMin = TopRightAnchor;
        rectTransform.anchorMax = TopRightAnchor;
        rectTransform.pivot = TopRightAnchor;
        rectTransform.anchoredPosition = anchoredPosition;
    }

    public static Canvas EnsureOverlayCanvas(
        ref GameObject? canvasObject,
        string objectName,
        int sortingOrder)
    {
        if (canvasObject == null)
        {
            canvasObject = new GameObject(
                objectName,
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));

            Object.DontDestroyOnLoad(canvasObject);
        }

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        scaler.scaleFactor = 1f;
        scaler.referencePixelsPerUnit = 100f;

        return canvas;
    }

    public static TMP_Text EnsureCanvasText(
        Transform parent,
        ref TMP_Text? text,
        string objectName,
        TMP_Text? styleTemplate,
        TmpTextStyle style)
    {
        if (text == null)
        {
            var go = new GameObject(
                objectName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI));

            go.transform.SetParent(parent, false);
            text = go.GetComponent<TMP_Text>();

            if (styleTemplate is not null)
            {
                text.font = styleTemplate.font;
                text.color = styleTemplate.color;
            }
        }

        ApplyTmpStyle(text, style);
        return text;
    }

    public static void ApplyScreenRect(
        TMP_Text text,
        Rect rect,
        TextAlignmentOptions alignment)
    {
        RectTransform rt = text.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.zero;
        rt.pivot = Vector2.zero;
        rt.anchoredPosition = new Vector2(rect.x, Screen.height - rect.y - rect.height);
        rt.sizeDelta = new Vector2(rect.width, rect.height);
        text.alignment = alignment;
    }

    public static void SetVisibleText(
        TMP_Text? text,
        bool visible,
        string value)
    {
        if (text == null)
        {
            return;
        }

        text.gameObject.SetActive(visible);

        if (visible)
        {
            text.text = value ?? string.Empty;
        }
    }

    public static void Label(
        Rect rect,
        string text,
        GUIStyle? style = null)
    {
        GUI.Label(
            rect,
            text ?? string.Empty,
            style ?? GUI.skin.label);
    }

    public static bool Button(
        Rect rect,
        string text,
        GUIStyle? style = null)
    {
        return GUI.Button(
            rect,
            text ?? string.Empty,
            style ?? GUI.skin.button);
    }

    public static string TextField(
        Rect rect,
        string value,
        GUIStyle? style = null)
    {
        return GUI.TextField(
            rect,
            value ?? string.Empty,
            style ?? GUI.skin.textField);
    }
}