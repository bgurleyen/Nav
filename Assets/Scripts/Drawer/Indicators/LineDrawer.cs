using Gamelogic.Extensions;
using UnityEngine;

public class LineDrawer : MonoBehaviour
{
    [SerializeField] Transform mark;
    [SerializeField] LabelText label;

    [SerializeField] LineRenderer drawer;
    [SerializeField] bool showGuides;

    MarkLine cacheLine;

    public void Display(MarkLine line, RoutePoint routePoint, bool hiddenLabel, int fromPoint = 0)
    {
        if (!line.LinkedPoint.IsAfterDiscontinuity && !line.LinkedPoint.IsHiddenLine)
        {
            cacheLine = line;
            if (line.Vertexes == null || line.Vertexes.Length == 0)
            {
                drawer.positionCount = 0;
                return;
            }

            drawer.positionCount = line.Vertexes.Length - fromPoint;
            for (var i = fromPoint; i < line.Vertexes.Length; i++)
            {
                drawer.SetPosition(i - fromPoint, line.Vertexes[i].To2DXY().ToDisplay());
            }
        }
        else
        {
            drawer.positionCount = 0;
        }

        if (!line.LinkedPoint.IsAfterDiscontinuity && !line.LinkedPoint.IsSkippable && !hiddenLabel)
        {
            mark.localPosition = line.EndPosition.To2DXY().ToDisplay();
            label.transform.localPosition = line.EndPosition.To2DXY().ToDisplay();
            label.Init(routePoint.Name);
            label.gameObject.SetActive(true);
        }
        else
        {
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