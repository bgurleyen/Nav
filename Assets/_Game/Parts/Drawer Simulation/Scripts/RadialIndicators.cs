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

    private void Awake()
    {
        _onePivotHeight = _headingTop.localPosition.y;
    }

    public void RefreshScale()
    {
        Vector3 scale;

        switch (Session.State.MapMode)
        {
            case MapMode.Map:
                scale = _mapPivot.localScale;
                break;
            case MapMode.Center:
                scale = _centerPivot.localScale;
                break;
            case MapMode.Plan:
                scale = _planPivot.localScale;
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        _headingLine.transform.localScale = scale;
        _headingTop.SetLocalY(scale.x * _onePivotHeight);
        _windTop.SetLocalY(scale.x * _onePivotHeight);
        _hdgRangeTop.SetLocalY(scale.x * _onePivotHeight);
    }

    public void RefreshVisibility()
    {
        _headingLine.gameObject.SetActive( Session.State.MapMode != MapMode.Plan && Session.State.HDG);
        _headingTop.gameObject.SetActive(Session.State.MapMode != MapMode.Plan);
    }

    public void RefreshZoomAndHDG()
    {
        //var displayHeadingDegrees = Mathf.RoundToInt(Session.PlayerAircraft.DisplayHeadingDegrees);
        ////Debug.Log(Session.PlayerAircraft.DisplayHeadingDegrees);
        //if (displayHeadingDegrees > 359) displayHeadingDegrees -= 360;
        //if (displayHeadingDegrees < 0) displayHeadingDegrees += 360;

        //displayHeadingDegrees = Mathf.Abs(displayHeadingDegrees) % 360;

        _headingText.text = Mathf.RoundToInt(Geometry.AbsAngle(Session.PlayerAircraft.DisplayHeadingDegrees)).ToString("D3");

        //_headingText.text = Mathf.RoundToInt(displayHeadingDegrees).ToString("D3");
        _rangeText.text = $"RANGE\n{Math.Round(80f / Session.ZoomMultiplier, 1)}";
    }

    public void HeadingLineVisibility()
    {
        _headingLine.gameObject.SetActive(Session.State.MapMode != MapMode.Plan);
        _headingTop.gameObject.SetActive(Session.State.MapMode != MapMode.Plan);

        CancelInvoke(nameof(HideHeadingLine));
        Invoke(nameof(HideHeadingLine), 10f);
    }

    private void HideHeadingLine()
    {
        _headingLine.gameObject.SetActive(false);
    }
}
