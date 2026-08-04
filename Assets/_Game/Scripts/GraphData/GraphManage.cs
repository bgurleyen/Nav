using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GraphManage : MonoBehaviour
{
    public DataManage dataManage;
    public DebriefSummaryPanel debriefPanel;
    public LevelsRanksPanel levelsPanel;
    public Button continueButton;
    public Button levelsContinueButton;

    public static System.Action<bool /* isStable */> OnGameFinish;

    public CanvasGroup mainGroup;
    public CanvasGroup graphGroup;

    const string LevelsContinueButtonName = "Levels Continue Button";

    static readonly string[] LegacyGraphNames =
    {
        "FLIGHT PROFILE",
        "FUEL FLOW",
        "LEVEL PROFILE",
        "LineChart_Smooth",
    };

    LevelStat _meData;
    LevelStat _averageData;
    LevelStat _bestData;
    int _rank;
    int _totalPlayers;
    bool _debriefLogged;
    bool _showingLevelsPanel;

    void Awake()
    {
        EnsureGraphCanvasVisible();
        EnsureDebriefPanelExists();
        EnsureLevelsPanelExists();
    }

    void OnEnable() => OnGameFinish += OnGameFinished;

    void OnDisable() => OnGameFinish -= OnGameFinished;

    IEnumerator Start()
    {
        if (!PlayerPrefsHolder.ShowLevelSelectOnLoad)
            yield break;

        Time.timeScale = 0f;
        yield return null;
        EnterLevelSelectMode();
    }

    void EnterLevelSelectMode()
    {
        if (mainGroup != null)
        {
            mainGroup.alpha = 0f;
            mainGroup.blocksRaycasts = false;
            mainGroup.interactable = false;
        }

        if (graphGroup != null)
        {
            graphGroup.alpha = 1f;
            graphGroup.blocksRaycasts = true;
            graphGroup.interactable = true;
        }

        Time.timeScale = 0f;
        ShowLevelSelect();
    }

    void EnsureGraphCanvasVisible()
    {
        if (graphGroup == null)
            return;

        Transform canvasTransform = graphGroup.transform;
        if (canvasTransform.localScale.sqrMagnitude < 0.0001f)
            canvasTransform.localScale = Vector3.one;
    }

    void EnsureDebriefPanelExists()
    {
        if (debriefPanel != null)
            return;

        if (graphGroup != null)
            debriefPanel = graphGroup.GetComponentInChildren<DebriefSummaryPanel>(true);

        if (debriefPanel == null && graphGroup != null)
        {
            Transform host = FindDebriefHost();
            Transform existing = host.Find("Debrief Panel");
            if (existing != null)
            {
                debriefPanel = existing.GetComponent<DebriefSummaryPanel>();
                if (debriefPanel == null)
                    debriefPanel = existing.gameObject.AddComponent<DebriefSummaryPanel>();
            }
            else
            {
                debriefPanel = CreateDebriefPanel(host);
            }
        }

        if (debriefPanel == null)
        {
            Debug.LogWarning("[GraphManage] debriefPanel could not be created.");
            return;
        }

        debriefPanel.EnsureLayoutBuilt();
    }

    void EnsureLevelsPanelExists()
    {
        if (levelsPanel != null)
            return;

        Transform host = FindDebriefHost();
        if (host != null)
        {
            Transform existing = host.Find("LevelsPanel");
            if (existing != null)
                levelsPanel = existing.GetComponent<LevelsRanksPanel>();
        }

        if (levelsPanel == null && graphGroup != null)
            levelsPanel = graphGroup.GetComponentInChildren<LevelsRanksPanel>(true);

        if (levelsPanel == null && graphGroup != null && host != null)
            levelsPanel = CreateLevelsPanel(host);
    }

    LevelsRanksPanel CreateLevelsPanel(Transform parent)
    {
        GameObject panelGo = new GameObject(
            "LevelsPanel",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(DebriefLayoutScaler),
            typeof(LevelsRanksPanel));

        panelGo.layer = parent.gameObject.layer;
        panelGo.transform.SetParent(parent, false);
        StretchRect(panelGo.GetComponent<RectTransform>());

        Image image = panelGo.GetComponent<Image>();
        image.color = LevelsLayoutSpec.PageBg;
        image.raycastTarget = false;

        return panelGo.GetComponent<LevelsRanksPanel>();
    }

    Transform FindDebriefHost()
    {
        Transform graphUi = graphGroup.transform.Find("Graph UI") ?? graphGroup.transform;
        Transform nestedGraphUi = graphUi.Find("Graph UI");
        return nestedGraphUi != null ? nestedGraphUi : graphUi;
    }

    DebriefSummaryPanel CreateDebriefPanel(Transform parent)
    {
        GameObject panelGo = new GameObject(
            "Debrief Panel",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(DebriefLayoutScaler),
            typeof(DebriefSummaryPanel));

        panelGo.layer = parent.gameObject.layer;
        panelGo.transform.SetParent(parent, false);
        StretchRect(panelGo.GetComponent<RectTransform>());

        Image image = panelGo.GetComponent<Image>();
        image.color = DebriefLayoutSpec.PageBg;
        image.raycastTarget = false;

        return panelGo.GetComponent<DebriefSummaryPanel>();
    }

    void ShowDebriefUi()
    {
        _showingLevelsPanel = false;
        HideLegacyGraphUi();
        HideLevelsPanel();
        HideLevelsContinueOverlay();
        EnsureGraphCanvasVisible();
        EnsureDebriefPanelExists();

        if (debriefPanel != null)
        {
            debriefPanel.gameObject.SetActive(true);
            debriefPanel.PrepareForDebrief();
            continueButton = debriefPanel.EnsureContinueButton();
            WireDebriefContinueButton();
        }
        else
        {
            Debug.LogWarning("[GraphManage] Continue button unavailable because debriefPanel is missing.");
        }
    }

    public void ShowLevelSelect()
    {
        ShowLevelsPanel(asSelector: true);
    }

    void ShowLevelsPanel(bool asSelector = false)
    {
        _showingLevelsPanel = true;
        EnsureGraphCanvasVisible();
        EnsureLevelsPanelExists();

        Transform host = FindDebriefHost();
        if (host != null)
            host.gameObject.SetActive(true);

        if (debriefPanel != null)
            debriefPanel.gameObject.SetActive(false);

        if (continueButton != null)
            continueButton.gameObject.SetActive(false);

        if (levelsPanel == null)
        {
            Debug.LogError("[GraphManage] LevelsPanel could not be found or created.");
            return;
        }

        if (host != null && levelsPanel.transform.parent != host)
            levelsPanel.transform.SetParent(host, false);

        StretchRect(levelsPanel.GetComponent<RectTransform>());
        levelsPanel.PrepareForDisplay();
        levelsPanel.gameObject.SetActive(true);
        levelsPanel.transform.SetAsLastSibling();

        if (asSelector)
        {
            HideLevelsContinueOverlay();
            levelsPanel.CellClicked = OnLevelChosen;
            levelsPanel.SetSelectable(true);
        }
        else if (host != null)
        {
            levelsPanel.SetSelectable(false);
            levelsContinueButton = EnsureLevelsContinueOverlay();
            WireLevelsContinueButton();
            StartCoroutine(BringLevelsContinueToFront());
        }

        RefreshLevelsPanelRanks();
    }

    void OnLevelChosen(int level)
    {
        PlayerPrefsHolder.Level = level;
        PlayerPrefsHolder.ShowLevelSelectOnLoad = false;
        Time.timeScale = 1f;
        SceneManager.LoadScene("Main");
    }

    IEnumerator BringLevelsContinueToFront()
    {
        Transform overlayParent = ResolveLevelsContinueParent();
        for (int i = 0; i < 4; i++)
        {
            yield return null;
            Canvas.ForceUpdateCanvases();
            if (levelsContinueButton == null)
                yield break;

            levelsContinueButton.gameObject.SetActive(true);
            levelsContinueButton.transform.SetParent(overlayParent, false);
            LayoutLevelsContinueButton(levelsContinueButton);
            levelsContinueButton.transform.SetAsLastSibling();
        }
    }

    Transform ResolveLevelsContinueParent()
    {
        if (graphGroup != null)
            return graphGroup.transform;

        return FindDebriefHost();
    }

    Button EnsureLevelsContinueOverlay()
    {
        Transform overlayParent = ResolveLevelsContinueParent();

        Button button = ResolveLevelsContinueButton();
        if (button != null)
        {
            ReparentLevelsContinueButton(button, overlayParent);
            return button;
        }

        Transform existing = overlayParent.Find(LevelsContinueButtonName);
        if (existing != null)
        {
            button = existing.GetComponent<Button>();
            if (button != null)
            {
                ReparentLevelsContinueButton(button, overlayParent);
                return button;
            }
        }

        return CreateLevelsContinueOverlay(overlayParent);
    }

    Button ResolveLevelsContinueButton()
    {
        if (levelsContinueButton != null)
            return levelsContinueButton;

        if (levelsPanel == null)
            return null;

        for (int i = 0; i < levelsPanel.transform.childCount; i++)
        {
            Button button = levelsPanel.transform.GetChild(i).GetComponent<Button>();
            if (button != null)
                return button;
        }

        return levelsPanel.GetComponentInChildren<Button>(true);
    }

    void ReparentLevelsContinueButton(Button button, Transform overlayParent)
    {
        if (button == null)
            return;

        button.transform.SetParent(overlayParent, false);
        LayoutLevelsContinueButton(button);
        button.gameObject.SetActive(true);
        button.transform.SetAsLastSibling();
    }

    static void LayoutLevelsContinueButton(Button button)
    {
        if (button == null)
            return;

        RectTransform rect = button.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = new Vector2(0f, DebriefLayoutSpec.ContinueButtonBottom);
        rect.sizeDelta = new Vector2(
            DebriefLayoutSpec.ContinueButtonWidth,
            DebriefLayoutSpec.ContinueButtonHeight);
        rect.localScale = Vector3.one;
    }

    Button CreateLevelsContinueOverlay(Transform overlayParent)
    {
        GameObject buttonGo = new GameObject(
            LevelsContinueButtonName,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Button));
        buttonGo.layer = overlayParent.gameObject.layer;
        buttonGo.transform.SetParent(overlayParent, false);

        Image image = buttonGo.GetComponent<Image>();
        image.color = new Color32(58, 87, 70, 255);
        image.raycastTarget = true;

        GameObject labelGo = new GameObject(
            "Text",
            typeof(RectTransform),
            typeof(TextMeshProUGUI));
        labelGo.layer = overlayParent.gameObject.layer;
        labelGo.transform.SetParent(buttonGo.transform, false);

        TMP_Text label = labelGo.GetComponent<TextMeshProUGUI>();
        label.text = "Continue";
        label.font = ResolveOverlayFont();
        label.fontSize = 36f;
        label.color = Color.white;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        StretchRect(label.GetComponent<RectTransform>());

        Button button = buttonGo.GetComponent<Button>();
        button.targetGraphic = image;
        LayoutLevelsContinueButton(button);
        buttonGo.SetActive(true);
        buttonGo.transform.SetAsLastSibling();
        return button;
    }

    static TMP_FontAsset _overlayFont;

    static TMP_FontAsset ResolveOverlayFont()
    {
        if (_overlayFont != null)
            return _overlayFont;

        _overlayFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
        if (_overlayFont == null && TMP_Settings.instance != null)
            _overlayFont = TMP_Settings.defaultFontAsset;

        return _overlayFont;
    }

    void HideLevelsContinueOverlay()
    {
        Transform overlayParent = ResolveLevelsContinueParent();
        if (overlayParent != null)
        {
            Transform overlay = overlayParent.Find(LevelsContinueButtonName);
            if (overlay != null)
                overlay.gameObject.SetActive(false);
        }

        if (levelsContinueButton != null)
            levelsContinueButton.gameObject.SetActive(false);
    }

    static void ReplaceButtonListener(Button button, UnityAction listener)
    {
        if (button == null)
            return;

        button.onClick = new Button.ButtonClickedEvent();
        button.onClick.AddListener(listener);
    }

    void WireDebriefContinueButton()
    {
        ReplaceButtonListener(continueButton, OnDebriefContinueClick);
    }

    void WireLevelsContinueButton()
    {
        ReplaceButtonListener(levelsContinueButton, OnContinueButtonClick);
    }

    void RefreshLevelsPanelRanks()
    {
        if (levelsPanel == null || dataManage?.firestoreController == null)
            return;

        double currentFuel = _meData?.remainingFuel ?? 0;
        dataManage.firestoreController.FetchAllLevelRanks(
            PlayerPrefsHolder.UiLevelIndex,
            currentFuel,
            ApplyLevelsPanelRanks);
    }

    void OnProgressStatsSaved()
    {
        if (_showingLevelsPanel)
            RefreshLevelsPanelRanks();
    }

    void ApplyLevelsPanelRanks(int[] ranks)
    {
        if (levelsPanel == null || ranks == null)
            return;

        if (!levelsPanel.gameObject.activeInHierarchy)
            return;

        levelsPanel.EnsureCellBindings();
        levelsPanel.BindRanks(ranks);
    }

    public void OnDebriefContinueClick()
    {
        ShowLevelSelect();
    }

    void HideLevelsPanel()
    {
        if (levelsPanel != null)
            levelsPanel.gameObject.SetActive(false);

        HideLevelsContinueOverlay();
    }

    void HideLegacyGraphUi()
    {
        if (graphGroup == null)
            return;

        Transform[] transforms = graphGroup.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < transforms.Length; i++)
        {
            string name = transforms[i].name;
            for (int j = 0; j < LegacyGraphNames.Length; j++)
            {
                if (name != LegacyGraphNames[j])
                    continue;

                transforms[i].gameObject.SetActive(false);
                break;
            }
        }
    }

    void OnGameFinished(bool isStable)
    {
        if (mainGroup != null)
            mainGroup.alpha = 0;

        if (graphGroup != null)
        {
            graphGroup.alpha = 1;
            graphGroup.blocksRaycasts = true;
            graphGroup.interactable = true;
        }

        // Unstable approach: no debrief, no cloud save — go straight to level select.
        if (!isStable)
        {
            Time.timeScale = 0f;
            ShowLevelSelect();
            return;
        }

        if (dataManage == null)
        {
            Debug.LogError("[GraphManage] dataManage is not assigned.");
            ShowLevelSelect();
            return;
        }

        LevelStat flightResult = dataManage.CaptureFlightResult();
        dataManage.PushToCloud(flightResult, OnProgressStatsSaved);
        PresentDebrief(flightResult);
    }

    void PresentDebrief(LevelStat flightResult)
    {
        _meData = flightResult;
        _averageData = null;
        _bestData = null;
        _rank = 0;
        _totalPlayers = 0;
        _debriefLogged = false;

        ShowDebriefUi();

        if (flightResult == null)
        {
            Debug.LogWarning($"[Debrief] {PlayerPrefsHolder.LevelLabel}: invalid flight data.");
            return;
        }

        RefreshDebriefPanel();

        if (!PlayerPrefsHolder.TryGetFirestoreLevelKey(out string levelKey))
        {
            RefreshDebriefPanel(preferLog: true);
            return;
        }

        dataManage.firestoreController.FetchLevelAggregate(levelKey, flightResult.remainingFuel, aggregate =>
        {
            _bestData = aggregate.best;
            _averageData = aggregate.average;
            _rank = aggregate.rank;
            _totalPlayers = aggregate.totalPlayers;
            RefreshDebriefPanel(preferLog: true);
        });
    }

    void RefreshDebriefPanel(bool preferLog = false)
    {
        if (_showingLevelsPanel || debriefPanel == null || _meData == null)
            return;

        if (!debriefPanel.HasValidBindings())
            debriefPanel.PrepareForDebrief();

        debriefPanel.BindSubtitle(PlayerPrefsHolder.ActiveLevel);

        int? rank = _rank > 0 ? _rank : (int?)null;
        debriefPanel.BindAll(_meData, _averageData, _bestData, rank, _totalPlayers);

        continueButton = debriefPanel.EnsureContinueButton();
        WireDebriefContinueButton();

        if (!_debriefLogged && (preferLog || _averageData != null))
        {
            DebriefMetrics.LogPanel(_meData, _averageData, _bestData, rank, _totalPlayers);
            _debriefLogged = true;
        }
    }

    public void OnContinueButtonClick()
    {
        if (PlayerPrefsHolder.Level < PlayerPrefsHolder.MaxLevel)
            PlayerPrefsHolder.Level += 1;

        SceneManager.LoadScene("EMPTY");
    }

    static void StretchRect(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
        rect.localPosition = Vector3.zero;
    }
}
