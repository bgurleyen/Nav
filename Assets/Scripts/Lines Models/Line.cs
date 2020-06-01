using Gamelogic.Extensions;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Line 
{
    public Vector3 EndPosition { get; protected set; }
    public RoutePoint LinkedPoint { get; private set; }
    public Vector3[] Vertexes;
   
    // frequency of points
    protected const float UnitLength = 0.6f;


    protected Line(RoutePoint linkedPoint)
    {
        LinkedPoint = linkedPoint;
    }

    protected static Vector2 GetFurtherCircle(float x1, float y1, float x2, float y2, float r, Vector2 intersectionPoint)
    {
        var q = Mathf.Sqrt(Mathf.Pow((x2 - x1), 2) + Mathf.Pow((y2 - y1), 2));

        var y3 = (y1 + y2) / 2;

        var x3 = (x1 + x2) / 2;

        var baseX = Mathf.Sqrt(Mathf.Pow(r, 2) - Mathf.Pow((q / 2), 2)) * (y1 - y2) / q; //calculate once
        var baseY = Mathf.Sqrt(Mathf.Pow(r, 2) - Mathf.Pow((q / 2), 2)) * (x2 - x1) / q; //calculate once

        var centerX1 = x3 + baseX; //center x of circle 1
        var centerY1 = y3 + baseY; //center y of circle 1
        var centerX2 = x3 - baseX; //center x of circle 2
        var centerY2 = y3 - baseY; //center y of circle 2

        var center1 = new Vector2(centerX1, centerY1);
        var center2 = new Vector2(centerX2, centerY2);
        return (intersectionPoint - center1).sqrMagnitude > (intersectionPoint - center2).sqrMagnitude
            ? center1
            : center2;
    }

    public static float GetTangentToMiddle(float radius, float angleBetween)
    {
        return radius / Mathf.Tan(Mathf.Deg2Rad * angleBetween / 2);
    }
    protected static void ComputeArcAngles(float x0, float y0, float x1, float y1, float x2, float y2, out float startAngle, out float endAngle)
    {
        startAngle = Mathf.RoundToInt(180 / Mathf.PI * Mathf.Atan2(x1 - x0, y1 - y0));
        endAngle = Mathf.RoundToInt(180 / Mathf.PI * Mathf.Atan2(x2 - x0, y2 - y0));
    }

    protected static Vector2[] ComputeArcPoints(float startAngle, float endAngle, float unitLength, float radius, Vector3 o, Vector3 towardsPoint)
    {
        var _arcPoints = new List<Vector2>();
        var _angle = startAngle;

        var _angleBetween = endAngle - startAngle;


        while (_angleBetween > 180)
            _angleBetween -= 360;

        while (_angleBetween < -180)
            _angleBetween += 360;

        while (_angleBetween > 360)
            _angleBetween -= 360;

        while (_angleBetween < -360)
            _angleBetween += 360;

        var _reversedAngle = (360 - Mathf.Abs(_angleBetween)) * (_angleBetween < 0 ? 1 : -1);


        // determine the direction of the arc based on if it gets away of towards the target by a small angle
        var _firstNextPoint = o + GetPointOnArc(_angle + (_angleBetween < 0 ? -1 : 1), radius).To3DXY();
        var _reversedNextPoint = o + GetPointOnArc(_angle + (_reversedAngle < 0 ? -1 : 1), radius).To3DXY();

        var _dist = (_firstNextPoint - towardsPoint).sqrMagnitude;
        var _reversedDist = (_reversedNextPoint - towardsPoint).sqrMagnitude;

        _angleBetween = _dist < _reversedDist ? _angleBetween : _reversedAngle;

        var _arcLength = radius * Mathf.Deg2Rad * _angleBetween;
        var _segments = Mathf.CeilToInt(Mathf.Abs(_arcLength / unitLength));
        var _fraction = _angleBetween / _segments;
        
        for (var i = 0; i <= _segments; i++)
        {
            if (i == _segments)
            {
                _angle = startAngle + _angleBetween;
            }
            var _x = Mathf.Sin(Mathf.Deg2Rad * _angle) * radius;
            var _y = Mathf.Cos(Mathf.Deg2Rad * _angle) * radius;

            _arcPoints.Add(new Vector2(_x, _y));

            _angle += _fraction;
        }
        return _arcPoints.ToArray();
    }


    static Vector2 GetPointOnArc(float angle, float radius)
    {
        var _x = Mathf.Sin(Mathf.Deg2Rad * angle) * radius;
        var _y = Mathf.Cos(Mathf.Deg2Rad * angle) * radius;

        return new Vector2(_x, _y);
    }

    public virtual string GetName => "line";

}
