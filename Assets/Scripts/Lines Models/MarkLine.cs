using UnityEngine;

public class MarkLine : Line
{
    public Vector3 StartOffsetPosition { get; protected set; }
    public Vector3 StartPosition { get; protected set; }
    public Vector3 EndOffsetPosition { get; protected set; }
    public Vector3 StartCurvePosition = Vector3.zero;
    public float ComputedVertexLength;
    
    Vector3 lastPoint = Vector3.zero;

    public MarkLine(RoutePoint linkedPoint): base(linkedPoint) { }

    public void InitBeginning()
    {
        StartPosition = StartOffsetPosition = EndPosition = EndOffsetPosition = Vector3.zero;
    }

    protected void ComputeDistanceForPoint(int pointIndex)
    {
        var _point = Vertexes[pointIndex];
        if (ComputedVertexLength == -1)
        {
            lastPoint = _point;
            ComputedVertexLength = 0;
            return;
        }
        var _nextSegment = (_point - lastPoint).magnitude;

        ComputedVertexLength += _nextSegment; // todo may take resources
        lastPoint = _point;
    }

    public bool Init(Vector3 from, Vector3 offsetedFrom, RoutePoint nextPoint)
    {
        ComputedVertexLength = -1;
        StartPosition = from;
        StartOffsetPosition = offsetedFrom;
        StartCurvePosition = EndPosition = EndOffsetPosition = Geometry.GetNextPosition(from, nextPoint.Distance, nextPoint.Degrees);

        var _lineLength = (EndPosition - StartOffsetPosition).magnitude;

        if (_lineLength == 0)
        {
            Debug.LogError("line has length 0");
            return false;
        }

        var _points = Mathf.CeilToInt(_lineLength / UnitLength);
        Vertexes = new Vector3[_points + 1];

        for (var i = 0; i <= _points; i++)
        {
            Vertexes[i] = Vector3.Lerp(StartOffsetPosition, EndPosition, UnitLength * i / _lineLength);
            ComputeDistanceForPoint(i);
        }

        return true;
    }
}
