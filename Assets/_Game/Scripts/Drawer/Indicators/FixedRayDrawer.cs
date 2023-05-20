using System.Collections;
using System.Collections.Generic;
using Gamelogic.Extensions;
using Navigation;
using UnityEngine;

public class FixedRayDrawer : MonoBehaviour
{
    public LabelText label;
    public LineRenderer drawer;

    private FixRay cachedRay;

    public void Display(FixRay line)
    {
        // cachedRay = line;
        //
        // if (line.Vertexes == null || line.Vertexes.Length == 0)
        // {
        //     drawer.positionCount = 0;
        //     return;
        // }
        // drawer.positionCount = line.Vertexes.Length;
        // for (var i = 0; i < line.Vertexes.Length; i++)
        // {
        //     drawer.SetPosition(i, line.Vertexes[i].To2DXY().ToDisplay());
        // }
        // label.transform.localPosition = line.EndPosition.To2DXY().ToDisplay();
        // label.Init(line.LinkedInfo.NM.ToString());
    }
}
