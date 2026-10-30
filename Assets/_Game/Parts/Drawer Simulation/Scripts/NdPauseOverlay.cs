using Navigation;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// ND flags: PAUSE while frozen, CHECK SPEED after a blocked flap/gear attempt.
/// </summary>
public class NdPauseOverlay : MonoBehaviour
{
    const string PauseText = "P A U S E";
    const string CheckSpeedText = "C H E C K   S P E E D";
    const string OverspeedTitle = "O V E R S P E E D";
    const float PulseSpeed = 3.5f;
    const float FontSize = 3.4f * 1.5f;
    const float CheckSpeedSeconds = 3f;

    static readonly Color NdCyan = new Color(0.22f, 1f, 0.82f, 1f);
    static readonly Color NdRed = new Color(1f, 0.12f, 0.12f, 1f);

    static float _checkSpeedUntilUnscaled;

    Transform _pivot;
    TMP_Text _fontSource;
    TextMeshPro _label;
    float _overspeedAccSec;
    int _overspeedPenaltyKg;

    public static void NotifyCheckSpeed()
    {
        _checkSpeedUntilUnscaled = Time.unscaledTime + CheckSpeedSeconds;
    }

    public void Initialize(Transform pivot, TMP_Text fontSource)
    {
        _pivot = pivot;
        _fontSource = fontSource;
        EnsureLabel();
        Refresh(true);
    }

    void Update()
    {
        Refresh(false);
    }

    void Refresh(bool force)
    {
        EnsureLabel();
        if (_label == null)
            return;

        var checkSpeed = Time.unscaledTime < _checkSpeedUntilUnscaled;
        var pause = ShouldShowPause();
        var overspeed = !pause && IsOverspeed();
        TickOverspeedFuel(overspeed);
        var show = overspeed || checkSpeed || pause;
        if (force || _label.gameObject.activeSelf != show)
            _label.gameObject.SetActive(show);

        if (!show)
            return;

        var pulse = (Mathf.Sin(Time.unscaledTime * PulseSpeed) + 1f) * 0.5f;
        if (overspeed)
        {
            _label.enableWordWrapping = true;
            _label.overflowMode = TextOverflowModes.Overflow;
            _label.rectTransform.sizeDelta = new Vector2(16f, 3.4f);
            var text = OverspeedTitle + "\nFuel Penalty  - " + _overspeedPenaltyKg + "kg";
            if (_label.text != text)
                _label.text = text;
            var color = NdRed;
            color.a = Mathf.Lerp(0.55f, 1f, pulse);
            _label.color = color;
        }
        else if (checkSpeed)
        {
            _label.enableWordWrapping = false;
            _label.rectTransform.sizeDelta = new Vector2(16f, 1.5f);
            if (_label.text != CheckSpeedText)
                _label.text = CheckSpeedText;
            var color = NdRed;
            color.a = Mathf.Lerp(0.55f, 1f, pulse);
            _label.color = color;
        }
        else
        {
            _label.enableWordWrapping = false;
            _label.rectTransform.sizeDelta = new Vector2(16f, 1.5f);
            if (_label.text != PauseText)
                _label.text = PauseText;
            var color = NdCyan;
            color.a = Mathf.Lerp(0.55f, 1f, pulse);
            _label.color = color;
        }
    }

    static bool IsOverspeed()
    {
        return PFD_Animation.LimitSpeed > 0 && Calculator.CSpeed >= PFD_Animation.LimitSpeed;
    }

    void TickOverspeedFuel(bool overspeed)
    {
        if (!overspeed)
        {
            _overspeedAccSec = 0f;
            _overspeedPenaltyKg = 0;
            return;
        }

        if (_overspeedPenaltyKg <= 0)
            ApplyOverspeedPenalty();

        float dt = Session.Settings != null ? Session.Settings.FlyingTickDuration : Time.deltaTime;
        if (dt < 0f)
            dt = 0f;
        _overspeedAccSec += dt;
        while (_overspeedAccSec >= 1f)
        {
            _overspeedAccSec -= 1f;
            ApplyOverspeedPenalty();
        }
    }

    void ApplyOverspeedPenalty()
    {
        int kg = Calculator.OverspeedFuelPenaltyKg;
        _overspeedPenaltyKg += kg;
        if (Calculator.Instance != null)
            Calculator.Instance.DeductFuelKg(kg);
        else
            Calculator.totalFuel = System.Math.Max(0, Calculator.totalFuel - kg / 10.0);
    }

    static bool ShouldShowPause()
    {
        return Time.timeScale <= 0.001f || LevelStartInformation.IsAwaitingRequestDescent;
    }

    void EnsureLabel()
    {
        if (_label != null)
            return;

        if (_pivot == null)
            _pivot = transform;

        var go = new GameObject("ND PAUSE");
        go.transform.SetParent(_pivot, false);
        go.transform.localPosition = new Vector3(0f, 0.28f, -0.08f);
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;

        _label = go.AddComponent<TextMeshPro>();
        _label.text = PauseText;
        _label.fontSize = FontSize;
        _label.fontStyle = FontStyles.Bold;
        _label.alignment = TextAlignmentOptions.Center;
        _label.enableWordWrapping = false;
        _label.overflowMode = TextOverflowModes.Overflow;
        _label.raycastTarget = false;
        _label.rectTransform.sizeDelta = new Vector2(16f, 1.5f);
        _label.color = NdCyan;

        if (_fontSource != null && _fontSource.font != null)
            _label.font = _fontSource.font;
        else if (TMP_Settings.instance != null && TMP_Settings.defaultFontAsset != null)
            _label.font = TMP_Settings.defaultFontAsset;

        go.SetActive(false);

        var mesh = go.GetComponent<MeshRenderer>();
        if (mesh == null)
            return;

        mesh.shadowCastingMode = ShadowCastingMode.Off;
        mesh.receiveShadows = false;
        mesh.sortingOrder = 200;
    }
}
