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

        if ( !line.LinkedPoint.IsSkippable && !hiddenLabel)
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


    void OnDrawGizmosSelected()
    {
        const float radius = 0.01f;
        if (cacheLine == null) return;

        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position + cacheLine.StartPosition.To2DXY().ToDisplay(), radius * 1.8f);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position +cacheLine.EndPosition.To2DXY().ToDisplay(), radius * 2);

        Gizmos.color = Color.yellow;
        Gizmos.DrawSphere(transform.position +cacheLine.StartOffsetPosition.To2DXY().ToDisplay(), radius);

        Gizmos.color = Color.magenta;
        Gizmos.DrawSphere(transform.position +cacheLine.StartCurvePosition.To2DXY().ToDisplay(), radius);

        Gizmos.color = Color.blue;
        Gizmos.DrawSphere(transform.position +cacheLine.EndOffsetPosition.To2DXY().ToDisplay(), radius);

        if (!showGuides)
        {
            return;
        }

        if (cacheLine is DoubleCurve)
        {
            var c = cacheLine as DoubleCurve;
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position + c.O1.ToDisplay(), Drawer.GetMinRadius * Drawer.Instance.Zoom);
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(transform.position + c.o2.ToDisplay(), Drawer.RelaxedRadius * Drawer.Instance.Zoom);

            Gizmos.color = Color.gray;
            Gizmos.DrawSphere(transform.position + c.Ox.To2DXY().ToDisplay(), radius);
            Gizmos.color = Color.cyan;
            Gizmos.DrawSphere(transform.position + c.o1OnSecondProjection.ToDisplay(), radius);

            Gizmos.color = Color.red;
            Gizmos.DrawSphere(transform.position + c.Q.ToDisplay(), radius);
        }
        else if (cacheLine is Curve)
        {
            var c = cacheLine as Curve;
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position + c.O1.ToDisplay(), c.CachedRadius * Drawer.Instance.Zoom);
        }
    }
}