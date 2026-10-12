using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// ND flag: PAUSE while the sim is frozen (FMC PAUSE or until REQUEST DESCENT).
/// </summary>
public class NdPauseOverlay : MonoBehaviour
{
    const string LabelText = "PAUSE";
    const float PulseSpeed = 3.5f;

    static readonly Color NdCyan = new Color(0.22f, 1f, 0.82f, 1f);

    Transform _pivot;
    TMP_Text _fontSource;
    TextMeshPro _label;

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

        var show = ShouldShow();
        if (force || _label.gameObject.activeSelf != show)
            _label.gameObject.SetActive(show);

        if (!show)
            return;

        var pulse = (Mathf.Sin(Time.unscaledTime * PulseSpeed) + 1f) * 0.5f;
        var color = NdCyan;
        color.a = Mathf.Lerp(0.55f, 1f, pulse);
        _label.color = color;
    }

    static bool ShouldShow()
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
        go.SetActive(false);

        _label = go.AddComponent<TextMeshPro>();
        _label.text = LabelText;
        _label.fontSize = 3.4f;
        _label.fontStyle = FontStyles.Bold;
        _label.alignment = TextAlignmentOptions.Center;
        _label.enableWordWrapping = false;
        _label.overflowMode = TextOverflowModes.Overflow;
        _label.raycastTarget = false;
        _label.rectTransform.sizeDelta = new Vector2(4.2f, 1f);
        _label.color = NdCyan;

        if (_fontSource != null && _fontSource.font != null)
            _label.font = _fontSource.font;
        else if (TMP_Settings.instance != null && TMP_Settings.defaultFontAsset != null)
            _label.font = TMP_Settings.defaultFontAsset;

        var mesh = go.GetComponent<MeshRenderer>();
        if (mesh == null)
            return;

        mesh.shadowCastingMode = ShadowCastingMode.Off;
        mesh.receiveShadows = false;
        mesh.sortingOrder = 200;
    }
}
