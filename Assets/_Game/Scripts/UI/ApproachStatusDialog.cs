using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Runtime modal for approach stabilization result (replaces EditorUtility.DisplayDialog).
/// </summary>
public class ApproachStatusDialog : MonoBehaviour
{
    static ApproachStatusDialog _instance;

    Action _onClosed;
    CanvasGroup _canvasGroup;

    public static void Show(string title, string body, string buttonLabel = "Exit", Action onClosed = null)
    {
        if (_instance == null)
            _instance = Create();

        _instance.Present(title, body, buttonLabel, onClosed);
    }

    static ApproachStatusDialog Create()
    {
        var root = new GameObject("Approach Status Dialog", typeof(RectTransform), typeof(ApproachStatusDialog));
        DontDestroyOnLoad(root);

        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5000;
        root.AddComponent<GraphicRaycaster>();

        var scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        var dialog = root.GetComponent<ApproachStatusDialog>();
        dialog.BuildUi();
        return dialog;
    }

    void BuildUi()
    {
        _canvasGroup = gameObject.AddComponent<CanvasGroup>();

        var dimGo = new GameObject("Dimmer", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        dimGo.transform.SetParent(transform, false);
        var dimRt = dimGo.GetComponent<RectTransform>();
        StretchFull(dimRt);
        var dimImage = dimGo.GetComponent<Image>();
        dimImage.color = new Color(0f, 0f, 0f, 0.65f);
        dimImage.raycastTarget = true;
        var dimButton = dimGo.GetComponent<Button>();
        dimButton.transition = Selectable.Transition.None;
        dimButton.onClick.AddListener(Close);

        var panelGo = new GameObject("Panel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        panelGo.transform.SetParent(transform, false);
        var panelRt = panelGo.GetComponent<RectTransform>();
        panelRt.anchorMin = new Vector2(0.5f, 0.5f);
        panelRt.anchorMax = new Vector2(0.5f, 0.5f);
        panelRt.pivot = new Vector2(0.5f, 0.5f);
        panelRt.sizeDelta = new Vector2(720, 540);
        panelGo.GetComponent<Image>().color = new Color32(28, 36, 48, 245);

        var accentGo = new GameObject("Accent", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        accentGo.transform.SetParent(panelGo.transform, false);
        var accentRt = accentGo.GetComponent<RectTransform>();
        accentRt.anchorMin = new Vector2(0f, 1f);
        accentRt.anchorMax = new Vector2(1f, 1f);
        accentRt.pivot = new Vector2(0.5f, 1f);
        accentRt.sizeDelta = new Vector2(0f, 4f);
        accentRt.anchoredPosition = Vector2.zero;
        accentGo.GetComponent<Image>().color = new Color32(220, 96, 72, 255);

        CreateTmp(panelGo.transform, "Title", "NOT STABLE", 46f, FontStyles.Bold,
            TextAlignmentOptions.Center, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -32f), new Vector2(660f, 56f), new Color32(245, 232, 214, 255));

        CreateTmp(panelGo.transform, "Body", "", 42f, FontStyles.Normal,
            TextAlignmentOptions.Center, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -100f), new Vector2(640f, 320f), new Color32(210, 218, 228, 255),
            wordWrap: true);

        var buttonGo = new GameObject("Exit Button", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        buttonGo.transform.SetParent(panelGo.transform, false);
        var buttonRt = buttonGo.GetComponent<RectTransform>();
        buttonRt.anchorMin = new Vector2(0.5f, 0f);
        buttonRt.anchorMax = new Vector2(0.5f, 0f);
        buttonRt.pivot = new Vector2(0.5f, 0f);
        buttonRt.anchoredPosition = new Vector2(0f, 32f);
        buttonRt.sizeDelta = new Vector2(220f, 58f);
        buttonGo.GetComponent<Image>().color = new Color32(56, 110, 130, 255);
        var button = buttonGo.GetComponent<Button>();
        button.onClick.AddListener(Close);

        CreateTmp(buttonGo.transform, "Label", "Exit", 30f, FontStyles.Bold,
            TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(220f, 58f), Color.white);

        gameObject.SetActive(false);
    }

    void Present(string title, string body, string buttonLabel, Action onClosed)
    {
        _onClosed = onClosed;

        var titleTmp = transform.Find("Panel/Title")?.GetComponent<TMP_Text>();
        var bodyTmp = transform.Find("Panel/Body")?.GetComponent<TMP_Text>();
        var buttonLabelTmp = transform.Find("Panel/Exit Button/Label")?.GetComponent<TMP_Text>();

        if (titleTmp != null)
        {
            titleTmp.richText = true;
            if (string.Equals(title, "STABLE", StringComparison.OrdinalIgnoreCase))
                titleTmp.text = "<color=#5CDB7A>STABLE</color>";
            else if (string.Equals(title, "NOT STABLE", StringComparison.OrdinalIgnoreCase))
                titleTmp.text = "<color=#E85D5D>NOT STABLE</color>";
            else
                titleTmp.text = $"<color=#E85D5D>{title}</color>";
        }

        if (bodyTmp != null)
        {
            bodyTmp.richText = true;
            bodyTmp.text = body;
            bodyTmp.fontSize = 42f;
            bodyTmp.fontStyle = FontStyles.Normal;
            bodyTmp.alignment = TextAlignmentOptions.Center;
            bodyTmp.enableWordWrapping = true;
            bodyTmp.lineSpacing = 16f;
        }

        if (buttonLabelTmp != null)
        {
            buttonLabelTmp.text = buttonLabel;
            buttonLabelTmp.fontSize = 30f;
        }

        if (titleTmp != null)
            titleTmp.fontSize = 46f;

        transform.SetAsLastSibling();
        gameObject.SetActive(true);
        if (_canvasGroup != null)
        {
            _canvasGroup.alpha = 1f;
            _canvasGroup.blocksRaycasts = true;
            _canvasGroup.interactable = true;
        }

        Time.timeScale = 0f;
    }

    void Close()
    {
        gameObject.SetActive(false);
        var callback = _onClosed;
        _onClosed = null;
        Time.timeScale = 1f;
        callback?.Invoke();
    }

    static TMP_Text CreateTmp(
        Transform parent,
        string name,
        string text,
        float fontSize,
        FontStyles style,
        TextAlignmentOptions align,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 anchoredPos,
        Vector2 size,
        Color color,
        bool wordWrap = false)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = new Vector2(0.5f, anchorMin.y > 0.9f ? 1f : 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;

        var tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.font = ResolveFont();
        tmp.fontSize = fontSize;
        tmp.fontStyle = style;
        tmp.alignment = align;
        tmp.color = color;
        tmp.richText = true;
        tmp.enableWordWrapping = wordWrap;
        tmp.overflowMode = TextOverflowModes.Overflow;
        tmp.raycastTarget = false;
        return tmp;
    }

    static void StretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    static TMP_FontAsset ResolveFont()
    {
        var font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
        if (font == null)
            font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF - Fallback");
        if (font == null && TMP_Settings.instance != null)
            font = TMP_Settings.defaultFontAsset;
        return font;
    }
}
