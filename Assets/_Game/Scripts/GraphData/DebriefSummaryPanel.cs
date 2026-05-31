using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class DebriefSummaryPanel : MonoBehaviour
{
    const string LayoutRootName = "Layout Root";
    const string ContinueButtonName = "Continue Button";

    [Serializable]
    public struct RowBinding
    {
        public TMP_Text label;
        public TMP_Text me;
        public TMP_Text average;
        public TMP_Text best;
    }

    [SerializeField] private TMP_Text _title;
    [SerializeField] private TMP_Text _subtitle;
    [SerializeField] private RowBinding[] _rows = new RowBinding[4];
    [SerializeField] private TMP_Text _remainingFuel;
    [SerializeField] private TMP_Text _fuelRank;
    [SerializeField] private TMP_Text _rankHint;

    static readonly string[] DefaultLabels =
    {
        "Average Altitude",
        "LG Altitude",
        "Average Flaps Altitude",
        "Total S/B Usage",
    };

    void Awake()
    {
        EnsureLayoutBuilt();
        ApplyDefaultLabels();
    }

    public void EnsureLayoutBuilt()
    {
        if (Application.isPlaying)
        {
            if (!HasMinimumBindings())
                ClearGeneratedLayout(immediate: true);
        }
        else if (transform.Find(LayoutRootName) != null && HasMinimumBindings())
        {
            return;
        }

        BuildFixedLayout();
        AutoBindFromHierarchy();
    }

    public void PrepareForDebrief()
    {
        gameObject.SetActive(true);
        transform.SetAsFirstSibling();
        CleanupOrphanChildren(immediate: true);
        ClearGeneratedLayout(immediate: true);
        BuildFixedLayout();
        AutoBindFromHierarchy();
        BringLayoutRootBehindOverlayChildren();
        EnsureContinueButton();

        if (!HasMinimumBindings())
            Debug.LogError("[Debrief] Layout built but UI bindings are missing.");
    }

    public Button EnsureContinueButton()
    {
        Button button = FindContinueButton();
        if (button == null)
            button = CreateContinueButton();

        LayoutContinueButton(button);
        button.gameObject.SetActive(true);
        button.transform.SetAsLastSibling();
        return button;
    }

    Button FindContinueButton()
    {
        for (int i = 0; i < transform.childCount; i++)
        {
            Button button = transform.GetChild(i).GetComponent<Button>();
            if (button != null)
                return button;
        }

        return null;
    }

    Button CreateContinueButton()
    {
        GameObject buttonGo = new GameObject(
            ContinueButtonName,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Button));

        buttonGo.layer = gameObject.layer;
        buttonGo.transform.SetParent(transform, false);

        Image image = buttonGo.GetComponent<Image>();
        image.color = new Color32(58, 87, 70, 255);
        image.raycastTarget = true;

        GameObject labelGo = new GameObject(
            "Text",
            typeof(RectTransform),
            typeof(TextMeshProUGUI));
        labelGo.layer = gameObject.layer;
        labelGo.transform.SetParent(buttonGo.transform, false);

        TMP_Text label = labelGo.GetComponent<TextMeshProUGUI>();
        label.text = "Continue";
        label.font = ResolveDefaultFont();
        label.fontSize = 36f;
        label.color = Color.white;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        StretchFill(label.rectTransform, 0f, 0f, 0f, 0f);

        Button button = buttonGo.GetComponent<Button>();
        button.targetGraphic = image;
        return button;
    }

    static void LayoutContinueButton(Button button)
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

    void CleanupOrphanChildren(bool immediate)
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);
            if (child.name == LayoutRootName)
                continue;

            if (child.GetComponent<Button>() != null)
                continue;

            if (immediate || !Application.isPlaying)
                DestroyImmediate(child.gameObject);
            else
                Destroy(child.gameObject);
        }
    }

    void BringLayoutRootBehindOverlayChildren()
    {
        Transform layoutRoot = transform.Find(LayoutRootName);
        if (layoutRoot != null)
            layoutRoot.SetAsFirstSibling();
    }

#if UNITY_EDITOR
    public void EditorBuildLayout()
    {
        ClearGeneratedLayout(immediate: true);
        BuildFixedLayout();
        AutoBindFromHierarchy();
        UnityEditor.EditorUtility.SetDirty(this);
    }

    [ContextMenu("Rebuild Fixed Layout (1920x1080)")]
    void RebuildFixedLayoutMenu()
    {
        EditorBuildLayout();
    }
#endif

    public void ClearGeneratedLayout(bool immediate = false)
    {
        Transform root = transform.Find(LayoutRootName);
        if (root != null)
        {
            if (immediate || !Application.isPlaying)
                DestroyImmediate(root.gameObject);
            else
                Destroy(root.gameObject);
        }

        _title = null;
        _subtitle = null;
        _remainingFuel = null;
        _fuelRank = null;
        _rankHint = null;
        _rows = new RowBinding[4];
    }

    public bool HasValidBindings() => HasMinimumBindings();

    bool HasMinimumBindings()
    {
        return _rows != null
            && _rows.Length > 0
            && _rows[0].me != null
            && _remainingFuel != null;
    }

    [ContextMenu("Auto Bind From Hierarchy")]
    public void AutoBindFromHierarchy()
    {
        Transform root = transform.Find(LayoutRootName) ?? transform;

        _title = FindTextUnder(root, "Title") ?? _title;
        _subtitle = FindTextUnder(root, "Subtitle") ?? _subtitle;
        _remainingFuel = FindTextUnder(root, "ScoreValue") ?? _remainingFuel;
        _fuelRank = FindTextUnder(root, "RankValue") ?? _fuelRank;
        _rankHint = FindTextUnder(root, "RankHint") ?? _rankHint;

        if (_rows == null || _rows.Length != 4)
            _rows = new RowBinding[4];

        for (int i = 0; i < 4; i++)
        {
            Transform row = FindChildDeep(root, $"Row{i}");
            if (row == null)
                continue;

            _rows[i].label = FindTextIn(row, "Label") ?? _rows[i].label;
            _rows[i].me = FindTextIn(row, "Me") ?? _rows[i].me;
            _rows[i].average = FindTextIn(row, "Average") ?? _rows[i].average;
            _rows[i].best = FindTextIn(row, "Best") ?? _rows[i].best;
        }
    }

    static Transform FindChildDeep(Transform parent, string objectName)
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child.name == objectName)
                return child;

            Transform nested = FindChildDeep(child, objectName);
            if (nested != null)
                return nested;
        }

        return null;
    }

    void BuildFixedLayout()
    {
        StretchFullScreenBackground();

        RectTransform layoutRoot = CreateLayoutRoot();
        AttachScaler(layoutRoot);

        float y = DebriefLayoutSpec.Padding;

        _title = CreateText(layoutRoot, "Title", "POST-FLIGHT DEBRIEF", DebriefLayoutSpec.TitleFont,
            DebriefLayoutSpec.TextPrimary, FontStyles.Bold, TextAlignmentOptions.TopLeft);
        SetTopLeft(_title.rectTransform, DebriefLayoutSpec.Padding, y, 900f, DebriefLayoutSpec.TitleBlockHeight);
        y += DebriefLayoutSpec.TitleBlockHeight + DebriefLayoutSpec.TitleSubtitleGap;

        _subtitle = CreateText(layoutRoot, "Subtitle", "LEVEL 1", DebriefLayoutSpec.SubtitleFont,
            DebriefLayoutSpec.TextMuted, FontStyles.Normal, TextAlignmentOptions.TopLeft);
        SetTopLeft(_subtitle.rectTransform, DebriefLayoutSpec.Padding, y, 1200f, DebriefLayoutSpec.SubtitleBlockHeight);
        y += DebriefLayoutSpec.SubtitleBlockHeight + DebriefLayoutSpec.StackGap;

        CreateBadge(layoutRoot, y - DebriefLayoutSpec.StackGap - DebriefLayoutSpec.SubtitleBlockHeight);

        float tableTop = y;
        float tableHeight = DebriefLayoutSpec.HeaderRowHeight + DebriefLayoutSpec.DataRowHeight * 4f;
        Transform tableFrame = CreatePanel(layoutRoot, "Table Frame", DebriefLayoutSpec.TableBg,
            DebriefLayoutSpec.Padding, tableTop, DebriefLayoutSpec.ContentWidth, tableHeight);

        BuildTableHeader(tableFrame);
        BuildTableRows(tableFrame);

        y = tableTop + tableHeight + DebriefLayoutSpec.StackGap;

        Transform scoreBlock = CreatePanel(layoutRoot, "Score Block", DebriefLayoutSpec.ScoreBg,
            DebriefLayoutSpec.Padding, y, DebriefLayoutSpec.ContentWidth, DebriefLayoutSpec.ScoreBlockHeight);
        BuildScoreBlock(scoreBlock);
    }

    void StretchFullScreenBackground()
    {
        Image panelImage = GetComponent<Image>();
        if (panelImage == null)
            panelImage = gameObject.AddComponent<Image>();
        panelImage.color = DebriefLayoutSpec.PageBg;
        panelImage.raycastTarget = false;

        RectTransform rect = GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
    }

    RectTransform CreateLayoutRoot()
    {
        Transform existing = transform.Find(LayoutRootName);
        if (existing != null)
            return existing as RectTransform;

        GameObject go = new GameObject(LayoutRootName, typeof(RectTransform));
        go.transform.SetParent(transform, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(DebriefLayoutSpec.RefWidth, DebriefLayoutSpec.RefHeight);
        rect.anchoredPosition = Vector2.zero;
        rect.localScale = Vector3.one;
        return rect;
    }

    void AttachScaler(RectTransform layoutRoot)
    {
        DebriefLayoutScaler scaler = GetComponent<DebriefLayoutScaler>();
        if (scaler == null)
            scaler = gameObject.AddComponent<DebriefLayoutScaler>();
        scaler.SetLayoutRoot(layoutRoot);

        Canvas.ForceUpdateCanvases();
        scaler.SetLayoutRoot(layoutRoot);
    }

    void CreateBadge(Transform parent, float top)
    {
        Transform badge = CreatePanel(parent, "Badge", DebriefLayoutSpec.HeaderBg,
            DebriefLayoutSpec.RefWidth - DebriefLayoutSpec.Padding - 160f, top, 144f, DebriefLayoutSpec.BadgeHeight);
        TMP_Text text = CreateText(badge, "BadgeText", "NUMERIC SUMMARY", DebriefLayoutSpec.SubtitleFont,
            DebriefLayoutSpec.HeaderBlue, FontStyles.Bold, TextAlignmentOptions.Center);
        StretchFill(text.rectTransform, 0f, 0f, 0f, 0f);
    }

    void BuildTableHeader(Transform tableFrame)
    {
        Transform header = CreatePanel(tableFrame, "Table Header", DebriefLayoutSpec.HeaderBg,
            0f, 0f, DebriefLayoutSpec.ContentWidth, DebriefLayoutSpec.HeaderRowHeight, false);

        CreateColumnText(header, "HdrEmpty", "", DebriefLayoutSpec.HeaderFont, DebriefLayoutSpec.TextMuted,
            FontStyles.Normal, TextAlignmentOptions.MidlineLeft, 0);
        CreateColumnText(header, "HdrMe", "ME", DebriefLayoutSpec.HeaderFont, DebriefLayoutSpec.HeaderBlue,
            FontStyles.Bold, TextAlignmentOptions.MidlineRight, 1);
        CreateColumnText(header, "HdrAverage", "Average", DebriefLayoutSpec.HeaderFont, DebriefLayoutSpec.HeaderBlue,
            FontStyles.Bold, TextAlignmentOptions.MidlineRight, 2);
        CreateColumnText(header, "HdrBest", "Best", DebriefLayoutSpec.HeaderFont, DebriefLayoutSpec.HeaderBlue,
            FontStyles.Bold, TextAlignmentOptions.MidlineRight, 3);
    }

    void BuildTableRows(Transform tableFrame)
    {
        if (_rows == null || _rows.Length != 4)
            _rows = new RowBinding[4];

        for (int i = 0; i < 4; i++)
        {
            Color32 bg = i % 2 == 1 ? DebriefLayoutSpec.TableBgAlt : DebriefLayoutSpec.TableBg;
            float rowTop = DebriefLayoutSpec.HeaderRowHeight + i * DebriefLayoutSpec.DataRowHeight;
            Transform row = CreatePanel(tableFrame, $"Row{i}", bg,
                0f, rowTop, DebriefLayoutSpec.ContentWidth, DebriefLayoutSpec.DataRowHeight, false);

            _rows[i].label = CreateColumnText(row, "Label", DefaultLabels[i], DebriefLayoutSpec.RowFont,
                DebriefLayoutSpec.TextMuted, FontStyles.Normal, TextAlignmentOptions.MidlineLeft, 0);
            _rows[i].me = CreateColumnText(row, "Me", "—", DebriefLayoutSpec.RowFont,
                DebriefLayoutSpec.TextPrimary, FontStyles.Normal, TextAlignmentOptions.MidlineRight, 1);
            _rows[i].average = CreateColumnText(row, "Average", "—", DebriefLayoutSpec.RowFont,
                DebriefLayoutSpec.TextPrimary, FontStyles.Normal, TextAlignmentOptions.MidlineRight, 2);
            _rows[i].best = CreateColumnText(row, "Best", "—", DebriefLayoutSpec.RowFont,
                DebriefLayoutSpec.TextPrimary, FontStyles.Normal, TextAlignmentOptions.MidlineRight, 3);
        }
    }

    void BuildScoreBlock(Transform scoreBlock)
    {
        float padL = DebriefLayoutSpec.ScorePaddingH;
        float padT = DebriefLayoutSpec.ScorePaddingV;
        float halfW = DebriefLayoutSpec.ContentWidth * 0.5f - padL;

        CreateText(scoreBlock, "ScoreLabel", "SCORE", DebriefLayoutSpec.ScoreLabelFont,
            DebriefLayoutSpec.TextScoreMuted, FontStyles.Normal, TextAlignmentOptions.TopLeft);
        SetTopLeft(FindTextUnder(scoreBlock, "ScoreLabel").rectTransform, padL, padT, halfW, DebriefLayoutSpec.ScoreLabelHeight);

        _remainingFuel = CreateText(scoreBlock, "ScoreValue", "2.47 T", DebriefLayoutSpec.ScoreValueFont,
            DebriefLayoutSpec.TextPrimary, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
        SetTopLeft(_remainingFuel.rectTransform, padL, padT + DebriefLayoutSpec.ScoreValueOffset, halfW, DebriefLayoutSpec.ScoreValueHeight);

        CreateText(scoreBlock, "ScoreHint", "Remaining fuel at touchdown", DebriefLayoutSpec.ScoreLabelFont,
            DebriefLayoutSpec.TextScoreMuted, FontStyles.Normal, TextAlignmentOptions.BottomLeft);
        SetTopLeft(FindTextUnder(scoreBlock, "ScoreHint").rectTransform, padL,
            DebriefLayoutSpec.ScoreBlockHeight - padT - DebriefLayoutSpec.ScoreLabelHeight, halfW, DebriefLayoutSpec.ScoreLabelHeight);

        float rightX = DebriefLayoutSpec.ContentWidth * 0.5f;
        CreateText(scoreBlock, "RankLabel", "LEADERBOARD", DebriefLayoutSpec.ScoreLabelFont,
            DebriefLayoutSpec.TextScoreMuted, FontStyles.Normal, TextAlignmentOptions.TopRight);
        SetTopLeft(FindTextUnder(scoreBlock, "RankLabel").rectTransform, rightX, padT, halfW, DebriefLayoutSpec.ScoreLabelHeight);

        _fuelRank = CreateText(scoreBlock, "RankValue", "#23", DebriefLayoutSpec.ScoreValueFont,
            DebriefLayoutSpec.TextPrimary, FontStyles.Bold, TextAlignmentOptions.MidlineRight);
        SetTopLeft(_fuelRank.rectTransform, rightX, padT + DebriefLayoutSpec.ScoreValueOffset, halfW, DebriefLayoutSpec.ScoreValueHeight);

        _rankHint = CreateText(scoreBlock, "RankHint", "#1 = highest remaining fuel", DebriefLayoutSpec.ScoreLabelFont,
            DebriefLayoutSpec.TextScoreMuted, FontStyles.Normal, TextAlignmentOptions.BottomRight);
        SetTopLeft(_rankHint.rectTransform, rightX, DebriefLayoutSpec.ScoreBlockHeight - padT - DebriefLayoutSpec.ScoreLabelHeight, halfW, DebriefLayoutSpec.ScoreLabelHeight);
    }

    TMP_Text CreateColumnText(
        Transform row,
        string name,
        string text,
        float fontSize,
        Color32 color,
        FontStyles style,
        TextAlignmentOptions align,
        int columnIndex)
    {
        TMP_Text tmp = CreateText(row, name, text, fontSize, color, style, align);
        float rowHeight = row.name == "Table Header"
            ? DebriefLayoutSpec.HeaderRowHeight
            : DebriefLayoutSpec.DataRowHeight;
        SetTopLeft(
            tmp.rectTransform,
            DebriefLayoutSpec.ColumnXLocal(columnIndex),
            0f,
            DebriefLayoutSpec.ColumnWidth(columnIndex),
            rowHeight);
        return tmp;
    }

    static Transform CreatePanel(
        Transform parent,
        string name,
        Color32 color,
        float left,
        float top,
        float width,
        float height,
        bool usePagePadding = true)
    {
        Transform existing = parent.Find(name);
        GameObject go;
        if (existing != null)
        {
            go = existing.gameObject;
        }
        else
        {
            go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
        }

        Image image = go.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;

        float x = usePagePadding ? left : left;
        SetTopLeft(go.GetComponent<RectTransform>(), x, top, width, height);
        return go.transform;
    }

    static TMP_Text CreateText(
        Transform parent,
        string name,
        string text,
        float fontSize,
        Color32 color,
        FontStyles style,
        TextAlignmentOptions align)
    {
        Transform existing = parent.Find(name);
        GameObject go;
        if (existing != null)
        {
            go = existing.gameObject;
        }
        else
        {
            go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
        }

        TMP_Text tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.font = ResolveDefaultFont();
        tmp.fontSize = fontSize;
        tmp.color = color;
        tmp.fontStyle = style;
        tmp.alignment = align;
        tmp.enableWordWrapping = false;
        tmp.overflowMode = TextOverflowModes.Overflow;
        tmp.raycastTarget = false;
        return tmp;
    }

    static TMP_FontAsset _defaultFont;

    static TMP_FontAsset ResolveDefaultFont()
    {
        if (_defaultFont != null)
            return _defaultFont;

        _defaultFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
        if (_defaultFont == null)
            _defaultFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF - Fallback");
        if (_defaultFont == null && TMP_Settings.instance != null)
            _defaultFont = TMP_Settings.defaultFontAsset;

        if (_defaultFont == null)
            Debug.LogError("[Debrief] TMP font asset not found. Text will not render.");

        return _defaultFont;
    }

    static TMP_Text FindTextUnder(Transform root, string objectName)
    {
        TMP_Text[] texts = root.GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            if (texts[i].name == objectName)
                return texts[i];
        }

        return null;
    }

    static TMP_Text FindTextIn(Transform root, string objectName) => FindTextUnder(root, objectName);

    static void SetTopLeft(RectTransform rect, float left, float top, float width, float height)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(left, -top);
        rect.sizeDelta = new Vector2(width, height);
        rect.localScale = Vector3.one;
    }

    static void StretchFill(RectTransform rect, float left, float bottom, float right, float top)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, -top);
        rect.localScale = Vector3.one;
    }

    public void BindSubtitle(int level)
    {
        if (_subtitle != null)
            _subtitle.text = level <= PlayerPrefsHolder.TestLevel
                ? "TEST LEVEL"
                : $"LEVEL {level}";
    }

    public void BindAll(LevelStat me, LevelStat average, LevelStat best, int? rank, int totalPlayers)
    {
        BindColumn(me, average, best);
        BindScore(me?.remainingFuel ?? 0, rank, totalPlayers);
    }

    public void BindScore(double remainingFuel, int? rank, int totalPlayers)
    {
        if (_remainingFuel != null)
            _remainingFuel.text = DebriefMetrics.FormatFuel(remainingFuel);
        if (_fuelRank != null)
            _fuelRank.text = DebriefMetrics.FormatRank(rank, totalPlayers);
        if (_rankHint != null)
        {
            _rankHint.text = totalPlayers > 0
                ? $"#1 = highest remaining fuel · {totalPlayers} pilots"
                : "#1 = highest remaining fuel";
        }
    }

    void BindColumn(LevelStat me, LevelStat average, LevelStat best)
    {
        DebriefMetrics.ColumnValues[] values =
        {
            DebriefMetrics.BuildAverageAltitude(me, best, average),
            DebriefMetrics.BuildLgAltitude(me, best, average),
            DebriefMetrics.BuildAverageFlapsAltitude(me, best, average),
            DebriefMetrics.BuildSpeedBrakeUsage(me, best, average),
        };

        for (int i = 0; i < _rows.Length && i < values.Length; i++)
        {
            RowBinding row = _rows[i];
            DebriefMetrics.ColumnValues column = values[i];

            if (row.label != null)
                row.label.text = DefaultLabels[i];

            SetIfAssigned(row.me, column.Me);
            SetIfAssigned(row.average, column.Average);
            SetIfAssigned(row.best, column.Best);
        }
    }

    void ApplyDefaultLabels()
    {
        for (int i = 0; i < _rows.Length && i < DefaultLabels.Length; i++)
        {
            if (_rows[i].label != null)
                _rows[i].label.text = DefaultLabels[i];
        }
    }

    static void SetIfAssigned(TMP_Text target, string value)
    {
        if (target != null)
            target.text = value;
    }
}
