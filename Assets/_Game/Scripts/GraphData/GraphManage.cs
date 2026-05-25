using System.Collections.Generic;
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

    public static System.Action<bool> OnGameFinish;

    public CanvasGroup mainGroup;
    public CanvasGroup graphGroup;

    static readonly string[] LegacyGraphNames =
    {
        "FLIGHT PROFILE",
        "FUEL FLOW",
        "LEVEL PROFILE",
        "LineChart_Smooth",
    };

    DDL_data _meData;
    L_data _averageData;
    L_data _bestData;
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

    void ShowLevelsPanel()
    {
        _showingLevelsPanel = true;
        EnsureGraphCanvasVisible();
        EnsureLevelsPanelExists();
        ResolveLevelsContinueButton();

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
        levelsPanel.BindPlaceholder();
        levelsPanel.gameObject.SetActive(true);
        levelsPanel.transform.SetAsLastSibling();

        if (levelsContinueButton != null)
        {
            levelsContinueButton.gameObject.SetActive(true);
            levelsContinueButton.transform.SetAsLastSibling();
            WireLevelsContinueButton();
        }

        RefreshLevelsPanelRanks();
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

    void ResolveLevelsContinueButton()
    {
        if (levelsContinueButton != null)
            return;

        EnsureLevelsPanelExists();
        if (levelsPanel == null)
            return;

        Button[] buttons = levelsPanel.GetComponentsInChildren<Button>(true);
        if (buttons.Length > 0)
            levelsContinueButton = buttons[0];
    }

    void RefreshLevelsPanelRanks()
    {
        if (levelsPanel == null || dataManage?.firestoreController == null)
            return;

        double currentFuel = _meData?.remainingFuel ?? 0;
        dataManage.firestoreController.FetchAllLevelRanks(
            PlayerPrefsHolder.Level,
            currentFuel,
            ranks => levelsPanel.BindRanks(ranks));
    }

    public void OnDebriefContinueClick()
    {
        ShowLevelsPanel();
    }

    void HideLevelsPanel()
    {
        if (levelsPanel != null)
            levelsPanel.gameObject.SetActive(false);

        if (levelsContinueButton != null)
            levelsContinueButton.gameObject.SetActive(false);
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

    void OnGameFinished(bool isFinish)
    {
        if (dataManage == null)
        {
            Debug.LogError("[GraphManage] dataManage is not assigned.");
            return;
        }

        DDL_data flightData = dataManage.CaptureFlightSnapshot();
        dataManage.PushFlightToCloud(flightData);

        mainGroup.alpha = 0;
        graphGroup.alpha = 1;
        graphGroup.blocksRaycasts = true;
        graphGroup.interactable = true;
        PresentDebrief(flightData);
    }

    void PresentDebrief(DDL_data flightData)
    {
        _meData = flightData;
        _averageData = null;
        _bestData = null;
        _totalPlayers = 0;
        _debriefLogged = false;

        ShowDebriefUi();

        if (!DataManage.HasValidDistanceData(flightData))
        {
            Debug.LogWarning($"[Debrief] LEVEL {PlayerPrefsHolder.Level + 1}: invalid flight data.");
            return;
        }

        RefreshDebriefPanel();

        dataManage.firestoreController.UpdateBestStats(flightData, _ =>
        {
            _bestData = ResolveBestProfile(flightData);
            RefreshDebriefPanel();
        });

        dataManage.firestoreController.UpdateAverageStats(flightData, _ =>
        {
            List<S_data> averageRawData = dataManage.firestoreController.averagePlayer;
            _averageData = DebriefMetrics.BuildAverageProfile(flightData, averageRawData);
            _totalPlayers = dataManage.firestoreController.myUserData?.average_stats?.another?.count ?? 0;
            RefreshDebriefPanel(preferLog: true);
        });
    }

    void RefreshDebriefPanel(bool preferLog = false)
    {
        if (_showingLevelsPanel || debriefPanel == null || _meData == null)
            return;

        if (!debriefPanel.HasValidBindings())
            debriefPanel.PrepareForDebrief();

        debriefPanel.BindSubtitle(PlayerPrefsHolder.Level);

        int? rank = TryEstimateRank(_meData.remainingFuel, _bestData, _totalPlayers);
        debriefPanel.BindAll(_meData, _averageData, _bestData, rank, _totalPlayers);

        continueButton = debriefPanel.EnsureContinueButton();
        WireDebriefContinueButton();

        if (!_debriefLogged && (preferLog || _averageData != null))
        {
            DebriefMetrics.LogPanel(_meData, _averageData, _bestData, rank, _totalPlayers);
            _debriefLogged = true;
        }
    }

    static int? TryEstimateRank(double remainingFuel, L_data best, int totalPlayers)
    {
        if (totalPlayers <= 0)
            return null;

        if (best == null)
            return null;

        if (remainingFuel >= best.remainingFuel - 0.01)
            return 1;

        return null;
    }

    L_data ResolveBestProfile(DDL_data flightData)
    {
        L_data best = dataManage.firestoreController.GetBestLevelProfile();
        if (DataManage.HasValidDistanceData(best))
            return best;

        L_data fromBestPlayer = dataManage.firestoreController.GetBestPlayerLevelData(PlayerPrefsHolder.Level);
        DataManage.TryNormalizeProfile(fromBestPlayer);
        if (DataManage.HasValidDistanceData(fromBestPlayer))
            return fromBestPlayer;

        double bestFuel = dataManage.firestoreController.myUserData?.best_stats?.remainingFuel ?? 0;
        if (flightData != null && bestFuel > 0 && flightData.remainingFuel >= bestFuel - 0.01)
            return DataManage.ToLData(flightData);

        return best;
    }

    public void OnContinueButtonClick()
    {
        if (PlayerPrefsHolder.Level < DataManage.DefaultProgressLevelCount - 1)
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
