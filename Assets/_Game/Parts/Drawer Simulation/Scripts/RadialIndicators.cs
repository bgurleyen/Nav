using System;
using Gamelogic.Extensions;
using Navigation;
using TMPro;
using UnityEngine;
using UnityEngine.UIElements;

public class RadialIndicators : MonoBehaviour
{
    [SerializeField] private Transform _mapPivot;
    [SerializeField] private Transform _centerPivot;
    [SerializeField] private Transform _planPivot;

    [SerializeField] private LineRenderer _headingLine;
    [SerializeField] private Transform _headingTop;
    [SerializeField] private Transform _windTop;
    [SerializeField] private Transform _hdgRangeTop;

    [Space]
    [SerializeField] TextMeshPro _headingText;
    [SerializeField] TextMeshPro _rangeText;

    private float _onePivotHeight;
    private float _rangeBaseX;
    private bool _rangeBaseReady;

    private void Awake()
    {
        if (_headingTop != null)
            _onePivotHeight = _headingTop.localPosition.y;
        PlaceRangeBox();
    }

    public void RefreshScale()
    {
        if (Session.State == null)
            return;

        Transform pivot = Session.State.MapMode switch
        {
            MapMode.Map => _mapPivot,
            MapMode.Center => _centerPivot,
            MapMode.Plan => _planPivot,
            _ => null
        };
        if (pivot == null)
            return;

        var scale = pivot.localScale;
        if (_headingLine != null)
            _headingLine.transform.localScale = scale;
        _headingTop?.SetLocalY(scale.x * _onePivotHeight);
        _windTop?.SetLocalY(scale.x * _onePivotHeight);
        _hdgRangeTop?.SetLocalY(scale.x * _onePivotHeight);
    }

    public void RefreshZoomAndHDG()
    {
        if (Session.PlayerAircraft == null)
            return;

        if (_headingText != null)
            _headingText.text = Mathf.RoundToInt(Geometry.AbsAngle(Session.PlayerAircraft.DisplayHeadingDegrees)).ToString("D3");

        if (_rangeText != null)
            _rangeText.text = $"RANGE\n{Math.Round(80f / Math.Max(0.01f, Session.ZoomMultiplier), 1)}";

        PlaceRangeBox();
    }

    void PlaceRangeBox()
    {
        if (_rangeText == null)
            return;

        var range = _rangeText.transform;
        while (range != null && range.name != "RANGE")
            range = range.parent;
        if (range == null)
            return;

        if (!_rangeBaseReady)
        {
            _rangeBaseX = range.localPosition.x;
            _rangeBaseReady = true;
        }

        _rangeText.ForceMeshUpdate();
        float width = 0f;
        int count = 0;
        var characters = _rangeText.textInfo.characterInfo;
        int limit = Mathf.Min(_rangeText.textInfo.characterCount, characters.Length);
        for (int i = 0; i < limit; i++)
        {
            if (!characters[i].isVisible)
                continue;
            width += characters[i].xAdvance - characters[i].origin;
            count++;
        }

        if (count == 0)
            return;

        var position = range.localPosition;
        position.x = _rangeBaseX + (width / count) * 2f;
        range.localPosition = position;
    }
}
