using System;
using Gamelogic.Extensions;
using UnityEngine;
using UnityEngine.Serialization;

public class LineDrawer : MonoBehaviour
{
    [SerializeField] private LineRenderer mark;
    [SerializeField] private LabelText label;

    [SerializeField] private LineRenderer lineRenderer;
    [SerializeField] private bool showGuides;

    private MarkLine cacheLine;
    private Transform markTransform;
    private Transform labelTransform;

    private void Awake()
    {
        markTransform = mark.transform;
        labelTransform = label.transform;
    }

    public void Display(MarkLine line, RoutePoint routePoint, bool hiddenLabel, bool hiddenLine, int fromPoint)
    {
        if (!hiddenLine && !line.LinkedPoint.IsAfterDiscontinuity && !line.LinkedPoint.IsHiddenLine)
        {
            cacheLine = line;
            if (line.Vertexes == null || line.Vertexes.Length == 0)
            {
                lineRenderer.positionCount = 0;
                return;
            }

            lineRenderer.positionCount = line.Vertexes.Length - fromPoint;
            for (var i = fromPoint; i < line.Vertexes.Length; i++)
            {
                lineRenderer.SetPosition(i - fromPoint, line.Vertexes[i].To2DXY().ToDisplay());
            }
        }
        else
        {
            lineRenderer.positionCount = 0;
        }

        if (!line.LinkedPoint.IsSkippable && !hiddenLabel)
        {
            markTransform.localPosition = line.EndPosition.To2DXY().ToDisplay();
            labelTransform.localPosition = line.EndPosition.To2DXY().ToDisplay();
            var color = !GameManager.Instance.IsMod && routePoint.GetIsDisplayCurrent
                ? Drawer.Instance.CMagenta
                : Color.white;
            label.Init(routePoint.Name, color);
            mark.startColor = mark.endColor = color;
            mark.gameObject.SetActive(true);
            label.gameObject.SetActive(true);

        }
        else
        {
            mark.gameObject.SetActive(false);
            label.gameObject.SetActive(false);
        }
    }


    private void OnDrawGizmosSelected()
    {
        const float radius = 0.01f;
        var pos = transform.position;
        if (cacheLine == null) return;

        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(pos + cacheLine.StartPosition.To2DXY().ToDisplay(), radius * 1.8f);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(pos + cacheLine.EndPosition.To2DXY().ToDisplay(), radius * 2);

        Gizmos.color = Color.yellow;
        Gizmos.DrawSphere(pos + cacheLine.StartOffsetPosition.To2DXY().ToDisplay(), radius);

        Gizmos.color = Color.magenta;
        Gizmos.DrawSphere(pos + cacheLine.StartCurvePosition.To2DXY().ToDisplay(), radius);

        Gizmos.color = Color.blue;
        Gizmos.DrawSphere(pos + cacheLine.EndOffsetPosition.To2DXY().ToDisplay(), radius);

        if (!showGuides)
        {
            return;
        }

        switch (cacheLine)
        {
            case DoubleCurve doubleCurve:
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(pos + doubleCurve.O1.ToDisplay(),
                    GameSettingsScriptableObject.GetMinRadius * Drawer.Instance.Zoom);
                Gizmos.color = Color.blue;
                Gizmos.DrawWireSphere(pos + doubleCurve.o2.ToDisplay(),
                    GameSettingsScriptableObject.GetRelaxedRadius * Drawer.Instance.Zoom);

                Gizmos.color = Color.gray;
                Gizmos.DrawSphere(pos + doubleCurve.Ox.To2DXY().ToDisplay(), radius);
                Gizmos.color = Color.cyan;
                Gizmos.DrawSphere(pos + doubleCurve.o1OnSecondProjection.ToDisplay(), radius);

                Gizmos.color = Color.red;
                Gizmos.DrawSphere(pos + doubleCurve.Q.ToDisplay(), radius);
                break;
            case Curve curve:
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(pos + curve.O1.ToDisplay(), curve.CachedRadius * Drawer.Instance.Zoom);
                break;
        }
    }
}