using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LevelsRanksPanel : MonoBehaviour
{
    const string LayoutRootName = "Layout Root";
    const string ContinueButtonName = "Continue Button";
    const string TitleText = "L  E  V  E  L  S";

    static readonly int[] DefaultRanks =
    {
        13, 17, 1, 7, 22, 4,
        9, 31, 2, 14, 6, 28,
        3, 19, 11, 5, 24, 8,
        16, 30, 12, 4, 21, 10,
        27, 15, 7, 18, 2, 25,
        6, 20, 9, 33, 14, 5,
        11, 8, 23,
    };

    [Serializable]
    struct CellBinding
    {
        public TMP_Text text;
        public Transform cell;
    }

    [SerializeField] CellBinding[] _cells = new CellBinding[LevelsLayoutSpec.LevelCount];

    public Action<int> CellClicked;
    bool _selectable;

    void Awake()
    {
        if (!Application.isPlaying)
        {
            EnsureLayoutBuilt();
            BindRanks(DefaultRanks);
        }
    }

    public void BindPlaceholder()
    {
        BindRanks(new int[LevelsLayoutSpec.LevelCount]);
    }

    public void PrepareForDisplay()
    {
        gameObject.SetActive(true);

        Transform layoutRoot = transform.Find(LayoutRootName);
        bool needsRebuild = layoutRoot == null
            || !HasMinimumBindings()
            || layoutRoot.Find("SideLabel") != null
            || layoutRoot.Find("TitleLabel") == null;

        if (needsRebuild)
        {
            CleanupOrphanChildren(immediate: true);
            ClearGeneratedLayout(immediate: true);
            BuildFixedLayout();
        }

        EnsureCellBindings();
        ApplyCellInteractivity();
        RefreshTitleLabel();
        BringLayoutRootBehindOverlayChildren();

        layoutRoot = transform.Find(LayoutRootName);
        DebriefLayoutScaler scaler = GetComponent<DebriefLayoutScaler>();
        if (scaler == null)
            scaler = gameObject.AddComponent<DebriefLayoutScaler>();
        if (layoutRoot != null)
        {
            scaler.SetLayoutRoot(layoutRoot as RectTransform);
            scaler.SetBottomReserved(
                DebriefLayoutSpec.ContinueButtonBottom
                + DebriefLayoutSpec.ContinueButtonHeight
                + 8f);
        }

        HideContinueButton();
        Canvas.ForceUpdateCanvases();
    }

    public void HideContinueButton()
    {
        for (int i = 0; i < transform.childCount; i++)
        {
            Button button = transform.GetChild(i).GetComponent<Button>();
            if (button != null)
                button.gameObject.SetActive(false);
        }
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

#if UNITY_EDITOR
    public void EditorBuildLayout()
    {
        ClearGeneratedLayout(immediate: true);
        BuildFixedLayout();
        AutoBindFromHierarchy();
        BindRanks(DefaultRanks);
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

        _cells = new CellBinding[LevelsLayoutSpec.LevelCount];
    }

    public bool HasValidBindings() => HasMinimumBindings();

    bool HasMinimumBindings()
    {
        return _cells != null
            && _cells.Length == LevelsLayoutSpec.LevelCount
            && _cells[0].text != null
            && _cells[LevelsLayoutSpec.LevelCount - 1].text != null;
    }

    [ContextMenu("Auto Bind From Hierarchy")]
    public void AutoBindFromHierarchy()
    {
        EnsureCellBindings();
    }

    public void EnsureCellBindings()
    {
        Transform root = transform.Find(LayoutRootName) ?? transform;

        if (_cells == null || _cells.Length != LevelsLayoutSpec.LevelCount)
            _cells = new CellBinding[LevelsLayoutSpec.LevelCount];

        for (int i = 0; i < LevelsLayoutSpec.LevelCount; i++)
        {
            Transform cell = FindChildDeep(root, $"LevelCell{i + 1}");
            if (cell == null)
                continue;

            _cells[i].cell = cell;

            TMP_Text text = FindTextIn(cell, "Value");
            if (text != null)
                _cells[i].text = text;
        }
    }

    public void SetSelectable(bool on)
    {
        _selectable = on;
        ApplyCellInteractivity();
    }

    void ApplyCellInteractivity()
    {
        if (_cells == null || _cells.Length != LevelsLayoutSpec.LevelCount)
            return;

        for (int i = 0; i < LevelsLayoutSpec.LevelCount; i++)
        {
            Transform cell = _cells[i].cell;
            if (cell == null)
                continue;

            Image image = cell.GetComponent<Image>();
            Button button = cell.GetComponent<Button>();

            if (_selectable)
            {
                if (image != null)
                    image.raycastTarget = true;

                if (button == null)
                    button = cell.gameObject.AddComponent<Button>();

                button.targetGraphic = image;
                button.transition = Selectable.Transition.ColorTint;

                int level = i + 1;
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => CellClicked?.Invoke(level));
                button.interactable = true;
                button.enabled = true;
            }
            else
            {
                if (button != null)
                {
                    button.onClick.RemoveAllListeners();
                    button.enabled = false;
                }

                if (image != null)
                    image.raycastTarget = false;
            }
        }
    }

    public void BindRanks(int[] ranks)
    {
        EnsureCellBindings();

        if (_cells == null || _cells.Length != LevelsLayoutSpec.LevelCount)
            return;

        for (int i = 0; i < LevelsLayoutSpec.LevelCount; i++)
        {
            TMP_Text text = _cells[i].text;
            if (text == null)
                continue;

            int rank = ranks != null && i < ranks.Length ? ranks[i] : 0;
            text.text = FormatCellRichText(i + 1, rank);
            text.fontSize = LevelsLayoutSpec.CellFont;
            text.alignment = TextAlignmentOptions.Center;
            text.richText = true;
            text.enabled = true;
            text.ForceMeshUpdate();
        }
    }

    static string FormatCellRichText(int level, int rank)
    {
        string levelHex = ColorUtility.ToHtmlStringRGB(LevelsLayoutSpec.TextMuted);
        string emptyHex = ColorUtility.ToHtmlStringRGB(LevelsLayoutSpec.RankEmpty);
        string rankHex = ColorUtility.ToHtmlStringRGB(LevelsLayoutSpec.RankBlue);

        if (rank <= 0)
        {
            return $"<color=#{levelHex}>{level}</color> <color=#{levelHex}>–</color> <color=#{emptyHex}>#...</color>";
        }

        return $"<color=#{levelHex}>{level}</color> <color=#{levelHex}>–</color> <color=#{rankHex}><b>#{rank}</b></color>";
    }

    void BuildFixedLayout()
    {
        StretchFullScreenBackground();

        RectTransform layoutRoot = CreateLayoutRoot();
        AttachScaler(layoutRoot);

        float bodyTop = LevelsLayoutSpec.Padding;
        float cellHeight = LevelsLayoutSpec.CellHeight;
        float gridCellWidth = LevelsLayoutSpec.GridCellWidth;
        float topCellWidth = LevelsLayoutSpec.TopCellWidth;
        float contentLeft = LevelsLayoutSpec.ContentLeft;
        float topRowLeft = LevelsLayoutSpec.TopRowLeft;

        CreateTitleLabel(layoutRoot, bodyTop);

        for (int i = 0; i < LevelsLayoutSpec.TopRowCount; i++)
        {
            float left = topRowLeft + i * (topCellWidth + LevelsLayoutSpec.CellGap);
            BuildCell(layoutRoot, i + 1, left, bodyTop, topCellWidth, cellHeight, i % 2 == 1);
        }

        float gridTop = bodyTop + cellHeight + LevelsLayoutSpec.TopGridGap;
        for (int row = 0; row < LevelsLayoutSpec.GridRows; row++)
        {
            for (int col = 0; col < LevelsLayoutSpec.GridColumns; col++)
            {
                int level = row * LevelsLayoutSpec.GridColumns + col + LevelsLayoutSpec.TopRowCount + 1;
                float left = contentLeft + col * (gridCellWidth + LevelsLayoutSpec.CellGap);
                float top = gridTop + row * (cellHeight + LevelsLayoutSpec.CellGap);
                bool alt = row % 2 == 1;
                BuildCell(layoutRoot, level, left, top, gridCellWidth, cellHeight, alt);
            }
        }
    }

    void CreateTitleLabel(Transform layoutRoot, float bodyTop)
    {
        float titleWidth = LevelsLayoutSpec.ContentWidth * (1f - LevelsLayoutSpec.TopRowWidthRatio)
            - LevelsLayoutSpec.CellGap;
        float titleLeft = LevelsLayoutSpec.ContentLeft;

        TMP_Text label = CreateText(
            layoutRoot,
            "TitleLabel",
            TitleText,
            LevelsLayoutSpec.TitleFont,
            LevelsLayoutSpec.TextPrimary,
            FontStyles.Bold,
            TextAlignmentOptions.MidlineLeft);
        ApplyTitleStyle(label);
        SetTopLeft(
            label.rectTransform,
            titleLeft,
            bodyTop,
            titleWidth,
            LevelsLayoutSpec.CellHeight);
    }

    void RefreshTitleLabel()
    {
        Transform root = transform.Find(LayoutRootName);
        if (root == null)
            return;

        Transform title = root.Find("TitleLabel");
        if (title == null)
            return;

        TMP_Text label = title.GetComponent<TMP_Text>();
        if (label == null)
            return;

        label.text = TitleText;
        label.fontSize = LevelsLayoutSpec.TitleFont;
        ApplyTitleStyle(label);
    }

    static void ApplyTitleStyle(TMP_Text label)
    {
        label.color = LevelsLayoutSpec.TextPrimary;
        label.fontStyle = FontStyles.Bold;
        label.alignment = TextAlignmentOptions.MidlineLeft;
        label.characterSpacing = LevelsLayoutSpec.TitleLetterSpacing;
    }

    void BuildCell(Transform layoutRoot, int level, float left, float top, float width, float height, bool alt)
    {
        Color32 bg = alt ? LevelsLayoutSpec.CellBgAlt : LevelsLayoutSpec.CellBg;
        Transform cell = CreatePanel(layoutRoot, $"LevelCell{level}", bg, left, top, width, height, false);

        TMP_Text value = CreateText(
            cell,
            "Value",
            FormatCellRichText(level, 0),
            LevelsLayoutSpec.CellFont,
            LevelsLayoutSpec.TextMuted,
            FontStyles.Normal,
            TextAlignmentOptions.Center);
        StretchFill(value.rectTransform, 6f, 4f, 6f, 4f);
        value.richText = true;

        if (_cells == null || _cells.Length != LevelsLayoutSpec.LevelCount)
            _cells = new CellBinding[LevelsLayoutSpec.LevelCount];
        _cells[level - 1].text = value;
        _cells[level - 1].cell = cell;
    }

    void StretchFullScreenBackground()
    {
        Image panelImage = GetComponent<Image>();
        if (panelImage == null)
            panelImage = gameObject.AddComponent<Image>();
        panelImage.color = LevelsLayoutSpec.PageBg;
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
        rect.sizeDelta = new Vector2(LevelsLayoutSpec.RefWidth, LevelsLayoutSpec.RefHeight);
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
            Debug.LogError("[Levels] TMP font asset not found. Text will not render.");

        return _defaultFont;
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

    static TMP_Text FindTextIn(Transform root, string objectName)
    {
        TMP_Text[] texts = root.GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            if (texts[i].name == objectName)
                return texts[i];
        }

        return null;
    }

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
}
