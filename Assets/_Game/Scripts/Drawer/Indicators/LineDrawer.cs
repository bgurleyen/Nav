using Gamelogic.Extensions;
using Navigation;
using UnityEngine;

public class LineDrawer : MonoBehaviour
{
    [SerializeField] private LineRenderer mark;
    [SerializeField] private LabelText label;

    [SerializeField] private LineRenderer lineRenderer;
    [SerializeField] private bool showGuides;

    private TracedLine cacheLine;
    private Transform markTransform;
    private Transform labelTransform;

    private void Awake()
    {
        markTransform = mark.transform;
        labelTransform = label.transform;
    }

  

public void Display(TracedLine line, RoutePoint routePoint, bool hiddenLabel, bool hiddenLine, int fromPoint)
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
                lineRenderer.SetPosition(i - fromPoint, line.Vertexes[i].ToDisplay());
            }
        }
        else
        {
            lineRenderer.positionCount = 0;
        }

        if (!line.LinkedPoint.IsSkippable && !hiddenLabel)
        {
            markTransform.localPosition = line.EndNMPosition.ToDisplay();
            labelTransform.localPosition = line.EndNMPosition.ToDisplay();
            var color = !Session.IsMod && routePoint.GetIsDisplayCurrent
                ? Session.Settings.cMagenta
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
        Gizmos.DrawWireSphere(pos + cacheLine.StartNMPosition.ToDisplay(), radius * 1.8f);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(pos + cacheLine.EndNMPosition.ToDisplay(), radius * 2);

        // Gizmos.color = Color.yellow;
        // Gizmos.DrawSphere(pos + cacheLine.StartOffsetPosition.To2DXY().ToDisplay(), radius);
        //
        // Gizmos.color = Color.magenta;
        // Gizmos.DrawSphere(pos + cacheLine.StartCurvePosition.To2DXY().ToDisplay(), radius);
        //
        // Gizmos.color = Color.blue;
        // Gizmos.DrawSphere(pos + cacheLine.EndOffsetPosition.To2DXY().ToDisplay(), radius);

        if (!showGuides)
        {
            return;
        }

        
    }
}