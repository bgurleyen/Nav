using UnityEngine;

public class MarkLine : Line
{
    public Vector3 StartOffsetPosition { get; protected set; }
    public Vector3 StartPosition { get; protected set; }
    public Vector3 EndOffsetPosition { get; protected set; }
    public Vector3 StartCurvePosition = Vector3.zero;
    public float ComputedVertexLength;
    
    Vector3 lastPoint = Vector3.zero;

    public MarkLine(RoutePoint linkedPoint, float unitLength): base(linkedPoint, unitLength) { }

    public void InitBeginning()
    {
        StartPosition = StartOffsetPosition = EndPosition = EndOffsetPosition = Vector3.zero;
    }

    protected void ComputeDistanceForPoint(int pointIndex)
    {
        var point = Vertexes[pointIndex];
        if (ComputedVertexLength == -1)
        {
            lastPoint = point;
            ComputedVertexLength = 0;
            return;
        }
        var nextSegment = (point - lastPoint).magnitude;

        ComputedVertexLength += nextSegment; // todo may take resources
        lastPoint = point;
    }

    public bool Init(Vector3 from, Vector3 offsetedFrom, RoutePoint nextPoint)
    {
        ComputedVertexLength = -1;
        StartPosition = from;
        StartOffsetPosition = offsetedFrom;
        StartCurvePosition = EndPosition = EndOffsetPosition = Geometry.GetNextPosition(from, nextPoint.Distance, nextPoint.Degrees);

        var lineLength = (EndPosition - StartOffsetPosition).magnitude;

        if (lineLength == 0)
        {
            Debug.LogError("line has length 0");
            return false;
        }

        var points = Mathf.CeilToInt(lineLength / UnitLength);
        Vertexes = new Vector3[points + 1];

        for (var i = 0; i <= points; i++)
        {
            Vertexes[i] = Vector3.Lerp(StartOffsetPosition, EndPosition, UnitLength * i / lineLength);
            ComputeDistanceForPoint(i);
        }

        return true;
    }
}
