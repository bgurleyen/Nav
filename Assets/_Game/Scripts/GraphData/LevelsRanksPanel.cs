using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LevelsRanksPanel : MonoBehaviour
{
    const string LayoutRootName = "Layout Root";

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
    }

    [SerializeField] CellBinding[] _cells = new CellBinding[LevelsLayoutSpec.LevelCount];

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
        ClearGeneratedLayout(immediate: true);
        BuildFixedLayout();
        AutoBindFromHierarchy();

        Transform layoutRoot = transform.Find(LayoutRootName);
        DebriefLayoutScaler scaler = GetComponent<DebriefLayoutScaler>();
        if (scaler == null)
            scaler = gameObject.AddComponent<DebriefLayoutScaler>();
        if (layoutRoot != null)
            scaler.SetLayoutRoot(layoutRoot as RectTransform);

        Canvas.ForceUpdateCanvases();
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
        Transform root = transform.Find(LayoutRootName) ?? transform;

        if (_cells == null || _cells.Length != LevelsLayoutSpec.LevelCount)
            _cells = new CellBinding[LevelsLayoutSpec.LevelCount];

        for (int i = 0; i < LevelsLayoutSpec.LevelCount; i++)
        {
            Transform cell = FindChildDeep(root, $"LevelCell{i + 1}");
            if (cell == null)
                continue;

            _cells[i].text = FindTextIn(cell, "Value") ?? _cells[i].text;
        }
    }

    public void BindRanks(int[] ranks)
    {
        if (_cells == null || _cells.Length != LevelsLayoutSpec.LevelCount)
            return;

        for (int i = 0; i < LevelsLayoutSpec.LevelCount; i++)
        {
            if (_cells[i].text == null)
                continue;

            int rank = ranks != null && i < ranks.Length ? ranks[i] : 0;
            _cells[i].text.text = FormatCellRichText(i + 1, rank);
        }
    }

    static string FormatCellRichText(int level, int rank)
    {
        string levelHex = ColorUtility.ToHtmlStringRGB(LevelsLayoutSpec.TextMuted);
        string rankHex = ColorUtility.ToHtmlStringRGB(LevelsLayoutSpec.RankBlue);

        if (rank <= 0)
        {
            return $"<color=#{levelHex}>{level}</color> <color=#{levelHex}>–</color> <color=#{rankHex}>#...</color>";
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

        CreateSideLabel(layoutRoot, bodyTop);

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

    void CreateSideLabel(Transform layoutRoot, float bodyTop)
    {
        Transform labelFrame = CreatePanel(
            layoutRoot,
            "SideLabel",
            LevelsLayoutSpec.PageBg,
            LevelsLayoutSpec.Padding,
            bodyTop,
            LevelsLayoutSpec.SideLabelWidth,
            LevelsLayoutSpec.BodyHeight,
            false);

        TMP_Text label = CreateText(
            labelFrame,
            "LabelText",
            "LEVELS",
            LevelsLayoutSpec.SideLabelFont,
            LevelsLayoutSpec.RankBlue,
            FontStyles.Bold,
            TextAlignmentOptions.Center);
        label.characterSpacing = LevelsLayoutSpec.SideLabelSpacing;

        RectTransform labelRect = label.rectTransform;
        labelRect.anchorMin = new Vector2(0.5f, 0.5f);
        labelRect.anchorMax = new Vector2(0.5f, 0.5f);
        labelRect.pivot = new Vector2(0.5f, 0.5f);
        labelRect.sizeDelta = new Vector2(LevelsLayoutSpec.BodyHeight, LevelsLayoutSpec.SideLabelWidth);
        labelRect.anchoredPosition = Vector2.zero;
        labelRect.localEulerAngles = new Vector3(0f, 0f, 90f);
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
            TextAlignmentOptions.MidlineRight);
        StretchFill(value.rectTransform, 10f, 8f, 10f, 8f);
        value.richText = true;

        if (_cells == null || _cells.Length != LevelsLayoutSpec.LevelCount)
            _cells = new CellBinding[LevelsLayoutSpec.LevelCount];
        _cells[level - 1].text = value;
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
