using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Persistent top-right level picker for Level Test. Stays open while flying;
/// BAŞLAT starts or aborts+restarts the chosen level.
/// </summary>
public class LevelTestDialog : MonoBehaviour
{
    static LevelTestDialog _instance;

    CanvasGroup _canvasGroup;
    TMP_Text _titleTmp;
    TMP_Text _statusTmp;
    TMP_Text _levelTmp;
    GameObject _controlsRoot;
    GameObject _startButtonGo;
    int _selectedLevel = PlayerPrefsHolder.FirstRankedLevel;
    bool _completedMode;
    bool _flightStarted;

    public static void ShowLevelPicker()
    {
        if (_instance == null)
            _instance = Create();

        _instance.PresentPicker(pauseUntilStart: !_instance._flightStarted);
    }

    public static void ShowCompleted(int lastLevel)
    {
        if (_instance == null)
            _instance = Create();

        _instance.PresentCompleted(lastLevel);
    }

    /// <summary>Called after Main reloads and flight config is applied — keep panel open.</summary>
    public static void NotifyLevelRunning(int level)
    {
        if (_instance == null)
            _instance = Create();

        _instance.OnLevelRunning(level);
    }

    static LevelTestDialog Create()
    {
        var root = new GameObject("Level Test Dialog", typeof(RectTransform), typeof(LevelTestDialog));
        DontDestroyOnLoad(root);

        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5200;
        root.AddComponent<GraphicRaycaster>();

        var scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        var dialog = root.GetComponent<LevelTestDialog>();
        dialog.BuildUi();
        return dialog;
    }

    void BuildUi()
    {
        _canvasGroup = gameObject.AddComponent<CanvasGroup>();

        var panelGo = new GameObject("Panel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        panelGo.transform.SetParent(transform, false);
        var panelRt = panelGo.GetComponent<RectTransform>();
        panelRt.anchorMin = new Vector2(1f, 1f);
        panelRt.anchorMax = new Vector2(1f, 1f);
        panelRt.pivot = new Vector2(1f, 1f);
        panelRt.anchoredPosition = new Vector2(-16f, -16f);
        panelRt.sizeDelta = new Vector2(340f, 220f);
        panelGo.GetComponent<Image>().color = new Color32(28, 36, 48, 230);

        var accentGo = new GameObject("Accent", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        accentGo.transform.SetParent(panelGo.transform, false);
        var accentRt = accentGo.GetComponent<RectTransform>();
        accentRt.anchorMin = new Vector2(0f, 1f);
        accentRt.anchorMax = new Vector2(1f, 1f);
        accentRt.pivot = new Vector2(0.5f, 1f);
        accentRt.sizeDelta = new Vector2(0f, 3f);
        accentRt.anchoredPosition = Vector2.zero;
        accentGo.GetComponent<Image>().color = new Color32(72, 168, 220, 255);

        _titleTmp = CreateTmp(panelGo.transform, "Title", "LEVEL TEST", 22f, FontStyles.Bold,
            TextAlignmentOptions.Center, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -12f), new Vector2(310f, 28f), new Color32(245, 232, 214, 255));

        _statusTmp = CreateTmp(panelGo.transform, "Status", "", 16f, FontStyles.Normal,
            TextAlignmentOptions.Center, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -42f), new Vector2(310f, 36f), new Color32(180, 198, 214, 255),
            wordWrap: true);

        _controlsRoot = new GameObject("Controls", typeof(RectTransform));
        _controlsRoot.transform.SetParent(panelGo.transform, false);
        var controlsRt = _controlsRoot.GetComponent<RectTransform>();
        controlsRt.anchorMin = new Vector2(0.5f, 0.5f);
        controlsRt.anchorMax = new Vector2(0.5f, 0.5f);
        controlsRt.pivot = new Vector2(0.5f, 0.5f);
        controlsRt.anchoredPosition = new Vector2(0f, -8f);
        controlsRt.sizeDelta = new Vector2(300f, 56f);

        CreateButton(_controlsRoot.transform, "Prev", "<", new Vector2(-110f, 0f), new Vector2(56f, 48f),
            () => ChangeLevel(-1));
        CreateButton(_controlsRoot.transform, "Next", ">", new Vector2(110f, 0f), new Vector2(56f, 48f),
            () => ChangeLevel(1));

        _levelTmp = CreateTmp(_controlsRoot.transform, "Level Value", "1", 36f, FontStyles.Bold,
            TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(120f, 48f), new Color32(240, 192, 64, 255));

        _startButtonGo = CreateButton(panelGo.transform, "Start Button", "BAŞLAT",
            new Vector2(0f, 14f), new Vector2(200f, 42f), OnStartClicked,
            anchorMin: new Vector2(0.5f, 0f), anchorMax: new Vector2(0.5f, 0f), pivot: new Vector2(0.5f, 0f));

        gameObject.SetActive(false);
    }

    void PresentPicker(bool pauseUntilStart)
    {
        _completedMode = false;
        _selectedLevel = Mathf.Clamp(
            PlayerPrefsHolder.Level < PlayerPrefsHolder.FirstRankedLevel
                ? PlayerPrefsHolder.FirstRankedLevel
                : PlayerPrefsHolder.Level,
            PlayerPrefsHolder.FirstRankedLevel,
            PlayerPrefsHolder.MaxLevel);

        if (_titleTmp != null)
            _titleTmp.text = "<color=#48A8DC>LEVEL TEST</color>";

        RefreshStatus();
        if (_controlsRoot != null)
            _controlsRoot.SetActive(true);
        if (_startButtonGo != null)
        {
            _startButtonGo.SetActive(true);
            RefreshStartButtonLabel();
        }

        RefreshLevelLabel();
        ShowOverlay(pause: pauseUntilStart);
    }

    void PresentCompleted(int lastLevel)
    {
        _completedMode = true;
        _flightStarted = false;

        if (_titleTmp != null)
            _titleTmp.text = "<color=#5CDB7A>BİTTİ</color>";
        if (_statusTmp != null)
            _statusTmp.text = $"Son: Level {lastLevel}";

        if (_controlsRoot != null)
            _controlsRoot.SetActive(true);
        if (_startButtonGo != null)
        {
            _startButtonGo.SetActive(true);
            var label = _startButtonGo.transform.Find("Label")?.GetComponent<TMP_Text>();
            if (label != null)
                label.text = "BAŞLAT";
        }

        _selectedLevel = Mathf.Clamp(lastLevel, PlayerPrefsHolder.FirstRankedLevel, PlayerPrefsHolder.MaxLevel);
        RefreshLevelLabel();
        ShowOverlay(pause: true);
    }

    void OnLevelRunning(int level)
    {
        _completedMode = false;
        _flightStarted = true;
        _selectedLevel = Mathf.Clamp(level, PlayerPrefsHolder.FirstRankedLevel, PlayerPrefsHolder.MaxLevel);
        RefreshLevelLabel();
        RefreshStatus();
        RefreshStartButtonLabel();
        ShowOverlay(pause: false);
    }

    void RefreshStatus()
    {
        if (_statusTmp == null)
            return;

        if (_flightStarted)
            _statusTmp.text = $"Uçuyor: <color=#F0C040>L{PlayerPrefsHolder.Level}</color>  ·  seç → BAŞLAT = yeniden";
        else
            _statusTmp.text = "Level seç → BAŞLAT";
    }

    void RefreshStartButtonLabel()
    {
        var label = _startButtonGo != null
            ? _startButtonGo.transform.Find("Label")?.GetComponent<TMP_Text>()
            : null;
        if (label == null)
            return;

        label.text = _flightStarted ? "YENİDEN" : "BAŞLAT";
    }

    void ShowOverlay(bool pause)
    {
        transform.SetAsLastSibling();
        gameObject.SetActive(true);
        if (_canvasGroup != null)
        {
            _canvasGroup.alpha = 1f;
            _canvasGroup.blocksRaycasts = true;
            _canvasGroup.interactable = true;
        }

        Time.timeScale = pause ? 0f : 1f;
    }

    void ChangeLevel(int delta)
    {
        _selectedLevel = Mathf.Clamp(
            _selectedLevel + delta,
            PlayerPrefsHolder.FirstRankedLevel,
            PlayerPrefsHolder.MaxLevel);
        RefreshLevelLabel();
    }

    void RefreshLevelLabel()
    {
        if (_levelTmp != null)
            _levelTmp.text = _selectedLevel.ToString();
    }

    void OnStartClicked()
    {
        // From completed state, re-enter test session before loading.
        if (_completedMode)
            LevelTestMode.BeginSession();

        _completedMode = false;
        // Keep dialog open across scene reload (DontDestroyOnLoad).
        Time.timeScale = 1f;
        LevelTestMode.StartFromLevel(_selectedLevel);
    }

    static GameObject CreateButton(
        Transform parent,
        string name,
        string label,
        Vector2 anchoredPos,
        Vector2 size,
        UnityEngine.Events.UnityAction onClick,
        Vector2? anchorMin = null,
        Vector2? anchorMax = null,
        Vector2? pivot = null)
    {
        var buttonGo = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        buttonGo.transform.SetParent(parent, false);
        var buttonRt = buttonGo.GetComponent<RectTransform>();
        buttonRt.anchorMin = anchorMin ?? new Vector2(0.5f, 0.5f);
        buttonRt.anchorMax = anchorMax ?? new Vector2(0.5f, 0.5f);
        buttonRt.pivot = pivot ?? new Vector2(0.5f, 0.5f);
        buttonRt.anchoredPosition = anchoredPos;
        buttonRt.sizeDelta = size;
        buttonGo.GetComponent<Image>().color = new Color32(56, 110, 130, 255);
        buttonGo.GetComponent<Button>().onClick.AddListener(onClick);

        CreateTmp(buttonGo.transform, "Label", label, 22f, FontStyles.Bold,
            TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, size, Color.white);

        return buttonGo;
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
