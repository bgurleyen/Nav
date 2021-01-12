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

        Vector3 _secondEndPosition = Geometry.GetNextPosition(EndPosition, secondPoint.Distance, secondPoint.Degrees);

        StartCurvePosition = Vector3.Lerp(EndPosition, from, smallRadius / middle.Distance);

        //  FIRST TURN CENTER
        var _o1TestA = (EndPosition - StartCurvePosition).To2DXY().Rotate(-90).To3DXY() + StartCurvePosition;
        var _o1TestB = (EndPosition - StartCurvePosition).To2DXY().Rotate(90).To3DXY() + StartCurvePosition;

        var _testPointOnSecond = Vector2.Lerp(EndPosition, _secondEndPosition, smallRadius / secondPoint.Distance);
        int _rotateDirection;
        if (Vector2.Distance(_testPointOnSecond, _o1TestA) > Vector2.Distance(_testPointOnSecond, _o1TestB))
        {
            O1 = _o1TestB;
            _rotateDirection = -90;
        }
        else
        {
            O1 = _o1TestA;
            _rotateDirection = 90;
        }

        var _a = Mathf.Tan(Mathf.Deg2Rad * angleBetween) * smallRadius;
        var _b = _a + smallRadius;
        var _c = Mathf.Cos(Mathf.Deg2Rad * angleBetween) * _b;
        o1OnSecondProjection = Vector3.Lerp(EndPosition, _secondEndPosition, _c / secondPoint.Distance).To2DXY();

        //var o1OnSecondProjection = EndPoint + Vector3.Project(o1 - EndPoint, secondEndPosition - EndPoint);
        // ^ or like that is first method is slow

        Q = o1OnSecondProjection +
            (_secondEndPosition - EndPosition).normalized.To2DXY().Rotate(_rotateDirection) * bigRadius;

        var _o1Q = (O1 - Q).magnitude;


        var _qO2 = Mathf.Sqrt(Mathf.Pow(smallRadius + bigRadius, 2) - _o1Q * _o1Q);
        EndOffsetPosition = Vector3.Lerp(EndPosition, _secondEndPosition, (_c + _qO2) / secondPoint.Distance);

        // SECOND TURN CENTER
        o2 = Q + (EndOffsetPosition.To2DXY() - o1OnSecondProjection);

        // tangent between circles
        Ox = Vector3.Lerp(O1, o2, smallRadius / (smallRadius + bigRadius));



        // the curve will be made between startCurvePosition and EndOffsetPoint

        var _lineLength = (StartCurvePosition - StartOffsetPosition).magnitude;
        var _straightPoints = Mathf.CeilToInt(_lineLength / UnitLength);

        var _curveLength =
            ((EndOffsetPosition - StartCurvePosition) / 2).magnitude; //  refactor with real length of arc on EACH CURVE
        var _curvePoints = Mathf.CeilToInt(_curveLength / UnitLength);

        // compute firstCurve
        ComputeArcAngles(O1.x, O1.y, StartCurvePosition.x, StartCurvePosition.y, Ox.x, Ox.y, out var _startAngle,
            out var _endAngle);
        var _arcPoints1 = ComputeArcPoints(_startAngle, _endAngle, UnitLength, smallRadius, O1, EndPosition);

        // compute second
        ComputeArcAngles(o2.x, o2.y, Ox.x, Ox.y, EndOffsetPosition.x, EndOffsetPosition.y, out _startAngle,
            out _endAngle);
        var _arcPoints2 = ComputeArcPoints(_startAngle, _endAngle, UnitLength, bigRadius, o2, EndOffsetPosition);

        var _arcPointsCountWithoutLast = _arcPoints1.Length - 1;

        Vertexes = new Vector3[_straightPoints + _arcPointsCountWithoutLast + _arcPoints2.Length];

        for (var i = 0; i <= _straightPoints; i++)
        {
            Vertexes[i] = Vector3.Lerp(StartOffsetPosition, StartCurvePosition, UnitLength * i / _lineLength);
            ComputeDistanceForPoint(i);
        }

        for (var i = _straightPoints; i < _straightPoints + _arcPointsCountWithoutLast; i++)
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

            Vertexes[i] = (_arcPoints1[i - _straightPoints] + O1).To3DXY();
            ComputeDistanceForPoint(i);
        }

        for (var i = _straightPoints + _arcPointsCountWithoutLast; i < Vertexes.Length; i++)
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

            Vertexes[i] = (_arcPoints2[i - _straightPoints - _arcPointsCountWithoutLast] + o2).To3DXY();
            ComputeDistanceForPoint(i);
        }
    }

    public override string GetName => "double";
}
