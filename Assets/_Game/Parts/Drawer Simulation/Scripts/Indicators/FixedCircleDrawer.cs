using System.Collections;
using System.Collections.Generic;
using Gamelogic.Extensions;
using Navigation;
using UnityEngine;

public class FixedCircleDrawer : MonoBehaviour
{
    const float MarkerRadius = 0.2f;
    const float MarkerWidth = 0.055f;
    const float DistanceWidth = 0.02f;
    const int MarkerSegments = 40;

    public LabelText label;
    public LineRenderer drawer;

    private FixCircle cachedCircle;

    public void Display(FixCircle line)
    {
        cachedCircle = line;
        if (drawer == null)
            return;

        if (line.LinkedInfo == null)
        {
            DrawMarker(line);
            return;
        }

        drawer.loop = false;
        drawer.widthMultiplier = DistanceWidth;
        if (line.Vertexes == null || line.Vertexes.Length == 0)
        {
            drawer.positionCount = 0;
            return;
        }
        drawer.positionCount = line.Vertexes.Length;
        for (var i = 0; i < line.Vertexes.Length; i++)
        {
            drawer.SetPosition(i, line.Vertexes[i].To2DXY().ToDisplay());
        }
    }

    void DrawMarker(FixCircle line)
    {
        drawer.loop = true;
        drawer.widthMultiplier = MarkerWidth;
        drawer.positionCount = MarkerSegments;
        var center = line.EndPosition.To2DXY().ToDisplay();
        for (var i = 0; i < MarkerSegments; i++)
        {
            var angle = i / (float)MarkerSegments * Mathf.PI * 2f;
            drawer.SetPosition(i, center + new Vector3(Mathf.Sin(angle) * MarkerRadius, Mathf.Cos(angle) * MarkerRadius, 0f));
        }
    }

}
