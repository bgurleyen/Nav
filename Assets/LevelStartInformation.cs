using Navigation;
using System;
using System.Collections;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LevelStartInformation : MonoBehaviour
{
    public Text msgText;
    public Text msgText1;
    public Image LevelStartInfo;
    public Button okButton;

    public Action<MapMode> OnMapModeSet;

    const float VisibleLocalZ = -200f;
    const float PanelShiftX = 100f;

    static readonly Color PanelColor = new Color(0.05f, 0.08f, 0.12f, 0.96f);
    static readonly Color AccentNormal = new Color(0.15f, 0.62f, 0.38f, 1f);
    static readonly Color AccentDim = new Color(0.10f, 0.42f, 0.26f, 1f);
    static readonly Color TextPrimary = new Color(0.88f, 0.92f, 0.96f, 1f);
    static readonly Color TextMuted = new Color(0.58f, 0.66f, 0.74f, 1f);
    static readonly Color OutlineColor = new Color(0.20f, 0.55f, 0.35f, 0.55f);

    Coroutine _blinkRoutine;
    Image _buttonImage;
    bool _styled;

    void Awake()
    {
        if (okButton != null)
            _buttonImage = okButton.targetGraphic as Image;
    }

    public void ShowInfo()
    {
        if (LevelStartInfo == null)
        {
            Debug.LogError("LevelStartInformation.ShowInfo: LevelStartInfo is not assigned.");
            return;
        }

        if (!_styled)
        {
            ApplyLayoutAndStyle();
            _styled = true;
        }

        var panelTransform = LevelStartInfo.transform;
        panelTransform.gameObject.SetActive(true);
        panelTransform.SetAsLastSibling();

        var localPos = panelTransform.localPosition;
        panelTransform.localPosition = new Vector3(localPos.x, localPos.y, VisibleLocalZ);

        LevelStartInfo.enabled = true;
        SetChildVisibility(true);

        if (msgText != null)
            msgText.text = BuildMessage();
        if (msgText1 != null)
            msgText1.text = BuildMessage1();

        if (_blinkRoutine != null)
            StopCoroutine(_blinkRoutine);
        _blinkRoutine = StartCoroutine(BlinkReadyButton());
    }

    void SetChildVisibility(bool visible)
    {
        if (msgText != null)
            msgText.enabled = visible;
        if (msgText1 != null)
            msgText1.enabled = visible;
        if (okButton != null)
            okButton.gameObject.SetActive(visible);
    }

    void ApplyLayoutAndStyle()
    {
        var panel = LevelStartInfo.rectTransform;
        panel.sizeDelta = new Vector2(1100f, 420f);
        panel.anchoredPosition = new Vector2(panel.anchoredPosition.x + PanelShiftX, panel.anchoredPosition.y);
        LevelStartInfo.color = PanelColor;

        var outline = LevelStartInfo.GetComponent<Outline>() ?? LevelStartInfo.gameObject.AddComponent<Outline>();
        outline.effectColor = OutlineColor;
        outline.effectDistance = new Vector2(2f, -2f);

        if (okButton != null)
        {
            okButton.transform.SetAsFirstSibling();

            var btnRect = okButton.GetComponent<RectTransform>();
            btnRect.anchorMin = new Vector2(0.06f, 1f);
            btnRect.anchorMax = new Vector2(0.94f, 1f);
            btnRect.pivot = new Vector2(0.5f, 1f);
            btnRect.anchoredPosition = new Vector2(0f, -18f);
            btnRect.sizeDelta = new Vector2(0f, 64f);

            if (_buttonImage == null)
                _buttonImage = okButton.targetGraphic as Image;
            if (_buttonImage != null)
                _buttonImage.color = AccentNormal;

            var buttonLabel = okButton.GetComponentInChildren<TextMeshProUGUI>();
            if (buttonLabel != null)
            {
                buttonLabel.text = "REQUEST DESCENT";
                buttonLabel.fontStyle = FontStyles.Bold;
                buttonLabel.fontSize = 32f;
                buttonLabel.color = Color.white;
                buttonLabel.alignment = TextAlignmentOptions.Center;
            }
            else
            {
                var legacyLabel = okButton.GetComponentInChildren<Text>();
                if (legacyLabel != null)
                {
                    legacyLabel.text = "REQUEST DESCENT";
                    legacyLabel.fontStyle = FontStyle.Bold;
                    legacyLabel.fontSize = 32;
                    legacyLabel.color = Color.white;
                    legacyLabel.alignment = TextAnchor.MiddleCenter;
                }
            }
        }

        ConfigureInfoColumn(msgText, 0.04f, 0.48f);
        ConfigureInfoColumn(msgText1, 0.52f, 0.96f);
    }

    static void ConfigureInfoColumn(Text text, float minX, float maxX)
    {
        if (text == null)
            return;

        var rect = text.rectTransform;
        rect.localScale = Vector3.one;
        rect.anchorMin = new Vector2(minX, 0f);
        rect.anchorMax = new Vector2(maxX, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.offsetMin = new Vector2(12f, 16f);
        rect.offsetMax = new Vector2(-12f, -96f);

        text.alignment = TextAnchor.UpperLeft;
        text.color = TextPrimary;
        text.fontSize = 26;
        text.lineSpacing = 1.35f;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.supportRichText = true;
    }

    IEnumerator BlinkReadyButton()
    {
        if (_buttonImage == null)
            yield break;

        while (true)
        {
            float pulse = (Mathf.Sin(Time.time * 5f) + 1f) * 0.5f;
            _buttonImage.color = Color.Lerp(AccentDim, AccentNormal, pulse);
            yield return null;
        }
    }

    static string BuildMessage()
    {
        var builder = new StringBuilder();
        builder.AppendLine("<b><size=32>FLIGHT BRIEFING</size></b>");
        builder.AppendLine();
        builder.AppendLine($"Level {Session.CurrentLevel.levelInfo.LevelNumber}");
        AppendIfNotEmpty(builder, "Destination", Session.CurrentLevel.levelInfo.Destination);
        AppendIfNotEmpty(builder, "STAR", Session.CurrentLevel.levelInfo.Star);
        AppendIfNotEmpty(builder, "Transition", Session.CurrentLevel.levelInfo.Transition);
        AppendIfNotEmpty(builder, "Runway", Session.CurrentLevel.levelInfo.Runway);
        return builder.ToString();
    }

    static string BuildMessage1()
    {
        var builder = new StringBuilder();
        builder.AppendLine("<b><size=32>FLIGHT DATA</size></b>");
        builder.AppendLine();
        builder.AppendLine($"Course: {Session.CurrentLevel.levelInfo.Course}°");
        builder.AppendLine(
            $"Cruise: {Calculator.FormatFmcAltitude(Session.CurrentLevel.levelInfo.CrzAltitude)} / {Session.CurrentLevel.levelInfo.CrzSpeed} KT");
        builder.AppendLine(
            $"ZFW/Fuel: {Session.CurrentLevel.levelInfo.ZFW:0.#} / {Session.CurrentLevel.levelInfo.Fuel:0.#}");
        builder.AppendLine();
        builder.AppendLine($"<color=#{ColorUtility.ToHtmlStringRGB(TextMuted)}><i>Validate FMC, then request descent.</i></color>");
        return builder.ToString();
    }

    static void AppendIfNotEmpty(StringBuilder builder, string label, string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            builder.AppendLine($"{label}: {value}");
    }

    public void ButtonClick()
    {
        if (_blinkRoutine != null)
        {
            StopCoroutine(_blinkRoutine);
            _blinkRoutine = null;
        }

        if (_buttonImage != null)
            _buttonImage.color = AccentNormal;

        if (LevelStartInfo != null)
            LevelStartInfo.gameObject.SetActive(false);
    }
}
