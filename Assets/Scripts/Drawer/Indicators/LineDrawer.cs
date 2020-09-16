using System;
using Gamelogic.Extensions;
using UnityEngine;
using UnityEngine.Serialization;

public class LineDrawer : MonoBehaviour
{
    [SerializeField] LineRenderer mark;
    [SerializeField] LabelText label;

    [SerializeField] LineRenderer lineRenderer;
    [SerializeField] bool showGuides;

    MarkLine cacheLine;
    Transform markTransform;
    Transform labelTransform;

    void Awake()
    {
        markTransform = mark.transform;
        labelTransform = label.transform;
    }

    public void Display(MarkLine line, RoutePoint routePoint, bool hiddenLabel, int fromPoint = 0)
    {
        if (!line.LinkedPoint.IsAfterDiscontinuity && !line.LinkedPoint.IsHiddenLine)
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

        if ( !line.LinkedPoint.IsSkippable && !hiddenLabel)
        {
            markTransform.localPosition = line.EndPosition.To2DXY().ToDisplay();
            labelTransform.localPosition = line.EndPosition.To2DXY().ToDisplay();
            var _color = !GameManager.Instance.IsMod && routePoint.GetIsDisplayCurrent
                ? Drawer.Instance.CMagenta 
                : Color.white;
            label.Init(routePoint.Name, _color);
            mark.startColor = mark.endColor = _color;
            mark.gameObject.SetActive(true);
            label.gameObject.SetActive(true);
            
        }
        else
        {
            mark.gameObject.SetActive(false);
            label.gameObject.SetActive(false);
        }
    }


    void OnDrawGizmosSelected()
    {
        const float radius = 0.01f;
        if (cacheLine == null) return;

        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(cacheLine.StartPosition * Drawer.Instance.Zoom, radius * 1.8f);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(cacheLine.EndPosition * Drawer.Instance.Zoom, radius * 2);

        Gizmos.color = Color.yellow;
        Gizmos.DrawSphere(cacheLine.StartOffsetPosition * Drawer.Instance.Zoom, radius);

        Gizmos.color = Color.magenta;
        Gizmos.DrawSphere(cacheLine.StartCurvePosition * Drawer.Instance.Zoom, radius);

        Gizmos.color = Color.blue;
        Gizmos.DrawSphere(cacheLine.EndOffsetPosition * Drawer.Instance.Zoom, radius);

        if (!showGuides)
        {
            return;
        }

        if (cacheLine is DoubleCurve)
        {
            var c = cacheLine as DoubleCurve;
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(c.O1 * Drawer.Instance.Zoom, Drawer.GetMinRadius * Drawer.Instance.Zoom);
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(c.o2 * Drawer.Instance.Zoom, Drawer.RelaxedRadius * Drawer.Instance.Zoom);

            Gizmos.color = Color.gray;
            Gizmos.DrawSphere(c.Ox * Drawer.Instance.Zoom, radius);
            Gizmos.color = Color.cyan;
            Gizmos.DrawSphere(c.o1OnSecondProjection * Drawer.Instance.Zoom, radius);

            Gizmos.color = Color.red;
            Gizmos.DrawSphere(c.Q * Drawer.Instance.Zoom, radius);
        }
        else if (cacheLine is Curve)
        {
            var c = cacheLine as Curve;
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(c.O1 * Drawer.Instance.Zoom, c.CachedRadius * Drawer.Instance.Zoom);
        }
    }
}