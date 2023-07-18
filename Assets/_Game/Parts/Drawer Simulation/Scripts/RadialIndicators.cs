using System;
using Gamelogic.Extensions;
using Navigation;
using UnityEngine;

public class RadialIndicators : MonoBehaviour
{
    [SerializeField] private Transform _mapPivot;
    [SerializeField] private Transform _centerPivot;
    [SerializeField] private Transform _planPivot;

    [SerializeField] private LineRenderer _headingLine;
    [SerializeField] private Transform _headingTop;
    [SerializeField] private Transform _windTop;

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
        
    }

    public void RefreshVisibility()
    {
        _headingLine.gameObject.SetActive( Session.State.MapMode != MapMode.Plan && Session.State.HDG);
        _headingTop.gameObject.SetActive(Session.State.MapMode != MapMode.Plan);
    }
}
