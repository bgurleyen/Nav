using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// TEMPORARY: level-start score test overlay. Remove when debrief UI is finalized.
/// Picks random LevelStat values, treats approach as stable, jumps to debrief on OK.
/// </summary>
public class ScoreTestDialog : MonoBehaviour
{
    static readonly string[] TestUsers = { "Alice", "Bob", "Birol" };

    static ScoreTestDialog _instance;

    Action _onClosed;
    LevelStat _pending;
    string _pendingUser;
    CanvasGroup _canvasGroup;

    public static void ShowIfEnabled()
    {
        if (PlayerPrefsHolder.ShowLevelSelectOnLoad)
            return;

        var result = RollRandomResult();
        string user = TestUsers[UnityEngine.Random.Range(0, TestUsers.Length)];
        if (_instance == null)
            _instance = Create();

        _instance.Present(result, user);
    }

    static LevelStat RollRandomResult()
    {
        return new LevelStat
        {
            averageAltitude = UnityEngine.Random.Range(2500, 14001),
            lgAltitude = UnityEngine.Random.Range(1200, 6001),
            averageFlapAltitude = UnityEngine.Random.Range(800, 4501),
            speedBrakeSeconds = UnityEngine.Random.Range(0, 241),
            remainingFuel = UnityEngine.Random.Range(1500, 14501),
        };
    }

    static ScoreTestDialog Create()
    {
        var root = new GameObject("Score Test Dialog", typeof(RectTransform), typeof(ScoreTestDialog));
        DontDestroyOnLoad(root);

        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5100;
        root.AddComponent<GraphicRaycaster>();

        var scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        var dialog = root.GetComponent<ScoreTestDialog>();
        dialog.BuildUi();
        return dialog;
    }

    void BuildUi()
    {
        _canvasGroup = gameObject.AddComponent<CanvasGroup>();

        var dimGo = new GameObject("Dimmer", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        dimGo.transform.SetParent(transform, false);
        StretchFull(dimGo.GetComponent<RectTransform>());
        var dimImage = dimGo.GetComponent<Image>();
        dimImage.color = new Color(0f, 0f, 0f, 0.7f);
        dimImage.raycastTarget = true;

        var panelGo = new GameObject("Panel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        panelGo.transform.SetParent(transform, false);
        var panelRt = panelGo.GetComponent<RectTransform>();
        panelRt.anchorMin = new Vector2(0.5f, 0.5f);
        panelRt.anchorMax = new Vector2(0.5f, 0.5f);
        panelRt.pivot = new Vector2(0.5f, 0.5f);
        panelRt.sizeDelta = new Vector2(740, 560);
        panelGo.GetComponent<Image>().color = new Color32(28, 36, 48, 250);

        var accentGo = new GameObject("Accent", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        accentGo.transform.SetParent(panelGo.transform, false);
        var accentRt = accentGo.GetComponent<RectTransform>();
        accentRt.anchorMin = new Vector2(0f, 1f);
        accentRt.anchorMax = new Vector2(1f, 1f);
        accentRt.pivot = new Vector2(0.5f, 1f);
        accentRt.sizeDelta = new Vector2(0f, 4f);
        accentRt.anchoredPosition = Vector2.zero;
        accentGo.GetComponent<Image>().color = new Color32(220, 160, 48, 255);

        CreateTmp(panelGo.transform, "Title", "SCORE TEST", 42f, FontStyles.Bold,
            TextAlignmentOptions.Center, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -28f), new Vector2(680f, 52f), new Color32(245, 232, 214, 255));

        CreateTmp(panelGo.transform, "Body", "", 30f, FontStyles.Normal,
            TextAlignmentOptions.Left, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -96f), new Vector2(640f, 340f), new Color32(210, 218, 228, 255),
            wordWrap: true);

        var buttonGo = new GameObject("Ok Button", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        buttonGo.transform.SetParent(panelGo.transform, false);
        var buttonRt = buttonGo.GetComponent<RectTransform>();
        buttonRt.anchorMin = new Vector2(0.5f, 0f);
        buttonRt.anchorMax = new Vector2(0.5f, 0f);
        buttonRt.pivot = new Vector2(0.5f, 0f);
        buttonRt.anchoredPosition = new Vector2(0f, 28f);
        buttonRt.sizeDelta = new Vector2(260f, 58f);
        buttonGo.GetComponent<Image>().color = new Color32(56, 110, 130, 255);
        buttonGo.GetComponent<Button>().onClick.AddListener(Close);

        CreateTmp(buttonGo.transform, "Label", "OK → Score", 28f, FontStyles.Bold,
            TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(260f, 58f), Color.white);

        gameObject.SetActive(false);
    }

    void Present(LevelStat result, string user)
    {
        _pending = result;
        _pendingUser = user;
        _onClosed = JumpToScore;

        var titleTmp = transform.Find("Panel/Title")?.GetComponent<TMP_Text>();
        var bodyTmp = transform.Find("Panel/Body")?.GetComponent<TMP_Text>();

        if (titleTmp != null)
            titleTmp.text = "<color=#F0C040>SCORE TEST</color>  <size=70%>(TEMP)</size>";

        if (bodyTmp != null)
        {
            int sbMin = result.speedBrakeSeconds / 60;
            int sbSec = result.speedBrakeSeconds % 60;
            bodyTmp.text =
                $"User: <color=#F0C040><b>{user}</b></color>\n\n" +
                $"Stable: <color=#5CDB7A>YES</color>\n\n" +
                $"Average Altitude:  <b>{result.averageAltitude:N0} ft</b>\n" +
                $"LG Altitude:       <b>{result.lgAltitude:N0} ft</b>\n" +
                $"Avg Flaps Altitude:<b>{result.averageFlapAltitude:N0} ft</b>\n" +
                $"Total S/B Usage:   <b>{sbMin}:{sbSec:00}</b>\n" +
                $"Remaining Fuel:    <b>{result.remainingFuel:N0} kg</b>\n\n" +
                "<size=80%><color=#9AA8B8>OK → open debrief with these values</color></size>";
            bodyTmp.fontSize = 30f;
            bodyTmp.alignment = TextAlignmentOptions.Left;
            bodyTmp.enableWordWrapping = true;
            bodyTmp.lineSpacing = 8f;
        }

        transform.SetAsLastSibling();
        gameObject.SetActive(true);
        if (_canvasGroup != null)
        {
            _canvasGroup.alpha = 1f;
            _canvasGroup.blocksRaycasts = true;
            _canvasGroup.interactable = true;
        }

        Time.timeScale = 0f;
        Debug.Log(
            $"[ScoreTest] User={user} avgAlt={result.averageAltitude} lgAlt={result.lgAltitude} " +
            $"flapAlt={result.averageFlapAltitude} sb={result.speedBrakeSeconds}s fuel={result.remainingFuel} kg");
    }

    void Close()
    {
        gameObject.SetActive(false);
        var callback = _onClosed;
        _onClosed = null;
        Time.timeScale = 1f;
        callback?.Invoke();
    }

    void JumpToScore()
    {
        var result = _pending;
        var user = _pendingUser;
        _pending = null;
        _pendingUser = null;
        if (result == null)
            return;

        if (!string.IsNullOrEmpty(user))
            PlayerPrefsHolder.UserName = user;

        var graph = FindAnyObjectByType<GraphManage>();
        if (graph == null)
        {
            Debug.LogError("[ScoreTest] GraphManage not found.");
            return;
        }

        graph.PresentTestDebrief(result);
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
