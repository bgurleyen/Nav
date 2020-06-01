using UnityEngine;
using Gamelogic.Extensions;

public class Curve : MarkLine
{
    public Vector2 O1;
    public float CachedRadius;

    public Curve(RoutePoint linkedPoint) : base(linkedPoint) { }

    public void Init( Vector3 from, Vector3 offsetedFrom, RoutePoint middle, RoutePoint secondPoint, float tangentToMiddle, float radius)
    {
        ComputedLength = -1;
        CachedRadius = radius;
        StartPosition = from;
        StartOffsetPosition = offsetedFrom;

        EndPosition = Geometry.GetNextPosition(from, middle.Distance, middle.Degrees);

        StartCurvePosition = Vector3.Lerp(EndPosition, from, tangentToMiddle / middle.Distance);

        var secondEndPosition = Geometry.GetNextPosition(EndPosition, secondPoint.Distance, secondPoint.Degrees);
        EndOffsetPosition = Vector3.Lerp(EndPosition, secondEndPosition, tangentToMiddle / secondPoint.Distance);


        // the curve will be made between startCurvePosition and EndOffsetPoint

        var lineLength = (StartCurvePosition - StartOffsetPosition).magnitude;
        var points = Mathf.CeilToInt(lineLength / UnitLength);

        //var curveLength = (EndOffsetPosition - StartCurvePosition).magnitude; //  refactor with arclength
        //var curvePoints = Mathf.CeilToInt(curveLength / UnitLength);

        O1 = GetFurtherCircle(StartCurvePosition.x, StartCurvePosition.y, EndOffsetPosition.x, EndOffsetPosition.y, radius, EndPosition.To2DXY());
        ComputeArcAngles(O1.x, O1.y, StartCurvePosition.x, StartCurvePosition.y, EndOffsetPosition.x, EndOffsetPosition.y, out var startAngle, out var endAngle);
        var arcPoints = ComputeArcPoints(startAngle, endAngle, UnitLength, radius, O1, EndPosition);

        Vertexes = new Vector3[points  + arcPoints.Length];

        for (var i = 0; i <= points; i++)
        {
            Vertexes[i] = Vector3.Lerp(StartOffsetPosition, StartCurvePosition, UnitLength * i / lineLength);
            ComputeDistanceForPoint(i);

        }

        for (var i = points; i < Vertexes.Length; i++)
        {
            if (i < 0)
            {
                Debug.Log("mai mic");
                continue;
            }
            if (i >= Vertexes.Length)
            {
                Debug.Log("mai mare");
                continue;
            }
            Vertexes[i] = (arcPoints[i - points] + O1).To3DXY();
            ComputeDistanceForPoint(i);
        }
    }

    public override string GetName => "relaxed";
}
