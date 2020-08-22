using UnityEngine;
using Gamelogic.Extensions;
using System.Collections.Generic;

public class DoubleCurve : Curve
{
    // smallCurveCenter
    public Vector2 o2;
    public Vector3 Ox;

    public Vector2 o1OnSecondProjection;
    public Vector2 Q;

    public DoubleCurve(RoutePoint linkedPoint) : base(linkedPoint) { }

    public void Init(Vector3 from, Vector3 offsetedFrom, RoutePoint middle, RoutePoint secondPoint, float angleBetween,
        float smallRadius, float bigRadius)
    {
        ComputedVertexLength = -1;
        StartPosition = from;
        StartOffsetPosition = offsetedFrom;
        EndPosition = Geometry.GetNextPosition(from, middle.Distance, middle.Degrees);

        Vector3 secondEndPosition = Geometry.GetNextPosition(EndPosition, secondPoint.Distance, secondPoint.Degrees);

        StartCurvePosition = Vector3.Lerp(EndPosition, from, smallRadius / middle.Distance);

        //  FIRST TURN CENTER
        var o1_testA = (EndPosition - StartCurvePosition).To2DXY().Rotate(-90).To3DXY() + StartCurvePosition;
        var o1_testB = (EndPosition - StartCurvePosition).To2DXY().Rotate(90).To3DXY() + StartCurvePosition;

        var testPointOnSecond = Vector2.Lerp(EndPosition, secondEndPosition, smallRadius / secondPoint.Distance);
        int rotateDirection;
        if (Vector2.Distance(testPointOnSecond, o1_testA) > Vector2.Distance(testPointOnSecond, o1_testB))
        {
            O1 = o1_testB;
            rotateDirection = -90;
        }
        else
        {
            O1 = o1_testA;
            rotateDirection = 90;
        }

        var A = Mathf.Tan(Mathf.Deg2Rad * angleBetween) * smallRadius;
        var B = A + smallRadius;
        var C = Mathf.Cos(Mathf.Deg2Rad * angleBetween) * B;
        o1OnSecondProjection = Vector3.Lerp(EndPosition, secondEndPosition, C / secondPoint.Distance).To2DXY();

        //var o1OnSecondProjection = EndPoint + Vector3.Project(o1 - EndPoint, secondEndPosition - EndPoint);
        // ^ or like that is first method is slow

        Q = o1OnSecondProjection +
            (secondEndPosition - EndPosition).normalized.To2DXY().Rotate(rotateDirection) * bigRadius;

        var o1_Q = (O1 - Q).magnitude;


        var Q_o2 = Mathf.Sqrt(Mathf.Pow(smallRadius + bigRadius, 2) - o1_Q * o1_Q);
        EndOffsetPosition = Vector3.Lerp(EndPosition, secondEndPosition, (C + Q_o2) / secondPoint.Distance);

        // SECOND TURN CENTER
        o2 = Q + (EndOffsetPosition.To2DXY() - o1OnSecondProjection);

        // tangent between circles
        Ox = Vector3.Lerp(O1, o2, smallRadius / (smallRadius + bigRadius));



        // the curve will be made between startCurvePosition and EndOffsetPoint

        var lineLength = (StartCurvePosition - StartOffsetPosition).magnitude;
        var straightPoints = Mathf.CeilToInt(lineLength / UnitLength);

        var curveLength =
            ((EndOffsetPosition - StartCurvePosition) / 2).magnitude; //  refactor with real length of arc on EACH CURVE
        var curvePoints = Mathf.CeilToInt(curveLength / UnitLength);

        // compute firstCurve
        ComputeArcAngles(O1.x, O1.y, StartCurvePosition.x, StartCurvePosition.y, Ox.x, Ox.y, out var startAngle,
            out var endAngle);
        var arcPoints1 = ComputeArcPoints(startAngle, endAngle, UnitLength, smallRadius, O1, EndPosition);

        // compute second
        ComputeArcAngles(o2.x, o2.y, Ox.x, Ox.y, EndOffsetPosition.x, EndOffsetPosition.y, out startAngle,
            out endAngle);
        var arcPoints2 = ComputeArcPoints(startAngle, endAngle, UnitLength, bigRadius, o2, EndOffsetPosition);

        var arcPointsCountWithoutLast = arcPoints1.Length - 1;

        Vertexes = new Vector3[straightPoints + arcPointsCountWithoutLast + arcPoints2.Length];

        for (var i = 0; i <= straightPoints; i++)
        {
            Vertexes[i] = Vector3.Lerp(StartOffsetPosition, StartCurvePosition, UnitLength * i / lineLength);
            ComputeDistanceForPoint(i);
        }

        for (var i = straightPoints; i < straightPoints + arcPointsCountWithoutLast; i++)
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

            Vertexes[i] = (arcPoints1[i - straightPoints] + O1).To3DXY();
            ComputeDistanceForPoint(i);
        }

        for (var i = straightPoints + arcPointsCountWithoutLast; i < Vertexes.Length; i++)
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

            Vertexes[i] = (arcPoints2[i - straightPoints - arcPointsCountWithoutLast] + o2).To3DXY();
            ComputeDistanceForPoint(i);
        }
    }

    public override string GetName => "double";
}
