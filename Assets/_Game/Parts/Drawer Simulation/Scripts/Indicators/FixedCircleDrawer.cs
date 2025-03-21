using System.Collections;
using System.Collections.Generic;
using Gamelogic.Extensions;
using Navigation;
using UnityEngine;

public class FixedCircleDrawer : MonoBehaviour
{
    public LabelText label;
    public LineRenderer drawer;

    private FixCircle cachedCircle;

    public void Display(FixCircle line)
    {
        cachedCircle = line;
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
        //label.transform.localPosition = line.Vertexes[4 * line.Vertexes.Length / 5].To2DXY().ToDisplay();
        //label.Init(line.LinkedInfo.NM.ToString());
    }

}
