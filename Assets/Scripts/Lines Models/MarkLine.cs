using Gamelogic.Extensions;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MarkLine : Line
{
    public Vector3 StartOffsetPosition { get; protected set; }
    public Vector3 StartPosition { get; protected set; }
    public Vector3 EndOffsetPosition { get; protected set; }
    public Vector3 StartCurvePosition = Vector3.zero;
    public float ComputedLength;
    
    Vector3 lastPoint = Vector3.zero;

    public MarkLine(RoutePoint linkedPoint): base(linkedPoint) { }

    public void InitBeginning()
    {
        StartPosition = StartOffsetPosition = EndPosition = EndOffsetPosition = Vector3.zero;
    }

    public Vector3 GetNeededCurveOffset => (EndPosition - StartCurvePosition);

    protected void ComputeDistanceForPoint(int pointIndex)
    {
        var _point = Vertexes[pointIndex];
        if (ComputedLength == -1)
        {
            lastPoint = _point;
            ComputedLength = 0;
            return;
        }
        var _nextSegment = (_point - lastPoint).magnitude;

        ComputedLength += _nextSegment; // todo may take resources
        lastPoint = _point;
    }

    public void Init(Vector3 from, Vector3 offsetedFrom, RoutePoint nextPoint)
    {
        ComputedLength = -1;
        StartPosition = from;
        StartOffsetPosition = offsetedFrom;
        StartCurvePosition = EndPosition = EndOffsetPosition = Geometry.GetNextPosition(from, nextPoint.Distance, nextPoint.Degrees);

        var _lineLength = (EndPosition - StartOffsetPosition).magnitude;

        var _points = Mathf.CeilToInt(_lineLength / UnitLength);
        Vertexes = new Vector3[_points + 1];

        for (var i = 0; i <= _points; i++)
        {
            Vertexes[i] = Vector3.Lerp(StartOffsetPosition, EndPosition, UnitLength * i / _lineLength);
            ComputeDistanceForPoint(i);
        }
    }
}
