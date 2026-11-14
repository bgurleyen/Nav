using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Purchase prompt for levels beyond 1 and for FMC keys other than the side keys.
/// </summary>
public class PaywallDialog : MonoBehaviour
{
    static PaywallDialog _instance;

    CanvasGroup _canvasGroup;
    TMP_Text _body;
    TMP_Text _status;
    TMP_Text _buyLabel;
    Button _buyButton;
    float _savedTimeScale = 1f;
    bool _holdingTime;

    public static void Show()
    {
        if (_instance == null)
            _instance = Create();

        _instance.Present();
    }

    static PaywallDialog Create()
    {
        var root = new GameObject("Paywall Dialog", typeof(RectTransform), typeof(PaywallDialog));
        DontDestroyOnLoad(root);

        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 6000;
        root.AddComponent<GraphicRaycaster>();

        var scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        var dialog = root.GetComponent<PaywallDialog>();
        dialog.BuildUi();
        return dialog;
    }

    void OnEnable()
    {
        Entitlements.Changed += Refresh;
        PurchaseService.Updated += Refresh;
    }

    void OnDisable()
    {
        Entitlements.Changed -= Refresh;
        PurchaseService.Updated -= Refresh;
    }

    void BuildUi()
    {
        _canvasGroup = gameObject.AddComponent<CanvasGroup>();

        var dimGo = new GameObject("Dimmer", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        dimGo.transform.SetParent(transform, false);
        StretchFull(dimGo.GetComponent<RectTransform>());
        var dimImage = dimGo.GetComponent<Image>();
        dimImage.color = new Color(0f, 0f, 0f, 0.65f);
        dimImage.raycastTarget = true;

        var panelGo = new GameObject("Panel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        panelGo.transform.SetParent(transform, false);
        var panelRt = panelGo.GetComponent<RectTransform>();
        panelRt.anchorMin = new Vector2(0.5f, 0.5f);
        panelRt.anchorMax = new Vector2(0.5f, 0.5f);
        panelRt.pivot = new Vector2(0.5f, 0.5f);
        panelRt.sizeDelta = new Vector2(820f, 620f);
        panelGo.GetComponent<Image>().color = new Color32(28, 36, 48, 245);

        var accentGo = new GameObject("Accent", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        accentGo.transform.SetParent(panelGo.transform, false);
        var accentRt = accentGo.GetComponent<RectTransform>();
        accentRt.anchorMin = new Vector2(0f, 1f);
        accentRt.anchorMax = new Vector2(1f, 1f);
        accentRt.pivot = new Vector2(0.5f, 1f);
        accentRt.sizeDelta = new Vector2(0f, 4f);
        accentGo.GetComponent<Image>().color = new Color32(220, 96, 72, 255);

        CreateTmp(panelGo.transform, "Title", "FULL ACCESS", 42f, FontStyles.Bold,
            TextAlignmentOptions.Center, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -28f), new Vector2(740f, 52f), new Color32(245, 232, 214, 255));

        _body = CreateTmp(panelGo.transform, "Body",
            "Level 1 is free. The other levels and the FMC keys (except the side keys) unlock with one purchase.",
            28f, FontStyles.Normal, TextAlignmentOptions.Center,
            new Vector2(0.08f, 0.58f), new Vector2(0.92f, 0.82f),
            Vector2.zero, Vector2.zero, new Color32(210, 218, 228, 255), wordWrap: true);

        _status = CreateTmp(panelGo.transform, "Status", "", 24f, FontStyles.Italic,
            TextAlignmentOptions.Center, new Vector2(0.08f, 0.46f), new Vector2(0.92f, 0.58f),
            Vector2.zero, Vector2.zero, new Color32(180, 190, 200, 255), wordWrap: true);

        _buyButton = CreateButton(panelGo.transform, "Buy", new Vector2(-190f, 188f), new Color32(58, 87, 70, 255), OnBuy);
        _buyLabel = _buyButton.GetComponentInChildren<TMP_Text>();
        CreateButton(panelGo.transform, "Restore", new Vector2(190f, 188f), new Color32(56, 110, 130, 255), OnRestore);
        CreateButton(panelGo.transform, "Not now", new Vector2(0f, 108f), new Color32(70, 76, 86, 255), Close);

#if UNITY_EDITOR
        CreateButton(panelGo.transform, "Editor unlock", new Vector2(-190f, 28f), new Color32(90, 72, 40, 255), () =>
        {
            Entitlements.SetEditorGrant(true);
            Refresh();
        });
        CreateButton(panelGo.transform, "Editor lock", new Vector2(190f, 28f), new Color32(90, 72, 40, 255), () =>
        {
            Entitlements.SetEditorGrant(false);
            Refresh();
        });
#endif

        gameObject.SetActive(false);
    }

    void Present()
    {
        if (!_holdingTime)
        {
            _savedTimeScale = Time.timeScale;
            _holdingTime = true;
        }

        Time.timeScale = 0f;
        gameObject.SetActive(true);
        if (_canvasGroup != null)
        {
            _canvasGroup.alpha = 1f;
            _canvasGroup.blocksRaycasts = true;
            _canvasGroup.interactable = true;
        }

        Refresh();
        PurchaseService.RefreshFromStore();
    }

    void Refresh()
    {
        bool hasPrice = PurchaseService.TryGetLocalizedPrice(out var price);

        if (_buyLabel != null)
        {
            if (Entitlements.HasFullUnlock)
                _buyLabel.text = "Unlocked";
            else if (hasPrice)
                _buyLabel.text = "Unlock  " + price;
            else
                _buyLabel.text = "Unlock";
        }

        if (_buyButton != null)
            _buyButton.interactable = !Entitlements.HasFullUnlock;

        if (_status == null)
            return;

        if (Entitlements.HasFullUnlock)
            _status.text = "Full access is unlocked.";
        else if (hasPrice)
            _status.text = "";
        else if (PurchaseService.IsUsingFakeStore && !PurchaseService.IsRefreshing)
            _status.text = "The editor test store always reports $0.01. A device build shows the Play or App Store price.";
        else if (!PurchaseService.IsReady || PurchaseService.IsRefreshing)
            _status.text = "Loading price from the store…";
        else
            _status.text = "Store price is not available yet.";
    }

    void OnBuy()
    {
        if (Entitlements.HasFullUnlock)
            return;

        if (_status != null)
            _status.text = "Waiting for the store…";

        PurchaseService.Buy(message =>
        {
            Refresh();
            if (_status == null)
                return;

            if (!string.IsNullOrEmpty(message))
                _status.text = message;
            else if (Entitlements.HasFullUnlock)
                _status.text = "Full access is unlocked.";
        });
    }

    void OnRestore()
    {
        if (_status != null)
            _status.text = "Restoring…";

        PurchaseService.Restore(message =>
        {
            Refresh();
            if (_status != null && !string.IsNullOrEmpty(message))
                _status.text = message;
        });
    }

    void Close()
    {
        gameObject.SetActive(false);
        if (_holdingTime)
        {
            Time.timeScale = _savedTimeScale;
            _holdingTime = false;
        }
    }

    static Button CreateButton(Transform parent, string label, Vector2 position, Color32 color, UnityEngine.Events.UnityAction onClick)
    {
        var buttonGo = new GameObject(label, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        buttonGo.transform.SetParent(parent, false);
        var rt = buttonGo.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = position;
        rt.sizeDelta = new Vector2(340f, 64f);

        var image = buttonGo.GetComponent<Image>();
        image.color = color;
        var button = buttonGo.GetComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(onClick);

        CreateTmp(buttonGo.transform, "Label", label, 26f, FontStyles.Bold,
            TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(320f, 64f), Color.white);
        return button;
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
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;

        var tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.font = ResolveFont();
        tmp.fontSize = fontSize;
        tmp.fontStyle = style;
        tmp.alignment = align;
        tmp.color = color;
        tmp.enableWordWrapping = wordWrap;
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
        if (TMP_Settings.defaultFontAsset != null)
            return TMP_Settings.defaultFontAsset;

        return Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
    }
}
