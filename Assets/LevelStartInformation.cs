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
    const float GlowPulseSpeed = 5f;
    const float DimAlpha = 0.35f;

    Coroutine _glowRoutine;
    TextMeshProUGUI _buttonLabel;
    Color _buttonLabelColor;
    bool _buttonLabelColorCached;

    public void ShowInfo()
    {
        if (LevelStartInfo == null)
        {
            Debug.LogError("LevelStartInformation.ShowInfo: LevelStartInfo is not assigned.");
            return;
        }

        var panelTransform = LevelStartInfo.transform;
        panelTransform.gameObject.SetActive(true);
        panelTransform.SetAsLastSibling();

        var localPos = panelTransform.localPosition;
        panelTransform.localPosition = new Vector3(localPos.x, localPos.y, VisibleLocalZ);

        LevelStartInfo.enabled = true;

        if (msgText != null)
        {
            msgText.enabled = true;
            msgText.text = BuildMessage();
        }

        if (msgText1 != null)
        {
            msgText1.enabled = true;
            msgText1.text = BuildMessage1();
        }

        if (okButton != null)
            okButton.gameObject.SetActive(true);

        Time.timeScale = 0f;

        if (_glowRoutine != null)
            StopCoroutine(_glowRoutine);
        _glowRoutine = StartCoroutine(PulseButtonTextGlow());
    }

    IEnumerator PulseButtonTextGlow()
    {
        if (_buttonLabel == null && okButton != null)
            _buttonLabel = okButton.GetComponentInChildren<TextMeshProUGUI>(true);

        if (_buttonLabel == null)
            yield break;

        if (!_buttonLabelColorCached)
        {
            _buttonLabelColor = _buttonLabel.color;
            _buttonLabelColorCached = true;
        }

        while (true)
        {
            float pulse = (Mathf.Sin(Time.unscaledTime * GlowPulseSpeed) + 1f) * 0.5f;
            _buttonLabel.fontMaterial.SetFloat(ShaderUtilities.ID_GlowPower, pulse);

            var color = _buttonLabelColor;
            color.a = Mathf.Lerp(DimAlpha, Mathf.Max(_buttonLabelColor.a, 1f), pulse);
            _buttonLabel.color = color;

            yield return null;
        }
    }

    void StopButtonTextGlow()
    {
        if (_glowRoutine != null)
        {
            StopCoroutine(_glowRoutine);
            _glowRoutine = null;
        }

        if (_buttonLabel == null || !_buttonLabelColorCached)
            return;

        _buttonLabel.fontMaterial.SetFloat(ShaderUtilities.ID_GlowPower, 0f);
        _buttonLabel.color = _buttonLabelColor;
    }

    static string BuildMessage()
    {
        var builder = new StringBuilder();
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
        builder.AppendLine($"Course: {Session.CurrentLevel.levelInfo.Course}°");
        builder.AppendLine(
            $"Cruise: {Calculator.FormatFmcAltitude(Session.CurrentLevel.levelInfo.CrzAltitude)} / {Session.CurrentLevel.levelInfo.CrzSpeed} KT");
        builder.AppendLine(
            $"ZFW/Fuel: {Session.CurrentLevel.levelInfo.ZFW:0.#} / {Session.CurrentLevel.levelInfo.Fuel:0.#}");
        builder.AppendLine();
        builder.Append("Validate FMC, call when Ready for Descent");
        return builder.ToString();
    }

    static void AppendIfNotEmpty(StringBuilder builder, string label, string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            builder.AppendLine($"{label}: {value}");
    }

    public void ButtonClick()
    {
        StopButtonTextGlow();

        Time.timeScale = 1f;

        if (LevelStartInfo != null)
            LevelStartInfo.gameObject.SetActive(false);
    }

    void OnDisable()
    {
        StopButtonTextGlow();
    }
}
