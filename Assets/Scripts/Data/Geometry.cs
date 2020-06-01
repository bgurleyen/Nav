using Gamelogic.Extensions;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class Geometry
{
    public static Vector2 GetNextPosition(Vector2 start, float distance, float degrees)
    {
        var _verticalVector = new Vector2(0, distance);
        var _rotatedVector = _verticalVector.Rotate(degrees);

        return start + _rotatedVector;
    }

    public static Vector2 GetDirectionFromHeading(float degrees)
    {
        return GetNextPosition(Vector2.zero, 1, -degrees);
    }

    public static float GetHeadingOfDirection(Vector2 direction)
    {
        return AngleBetween(direction, Vector2.up);
    }
    
    public static Vector2 GetPreviousPosition(Vector2 start, float distance, float degrees)
    {
        return GetNextPosition(start, distance, ReverseParallelAngle(degrees));
    }

    public static float AngleBetween(Vector2 start, Vector2 end)
    {
        var x1 = start.x;
        var x2 = end.x;
        var y1 = start.y;
        var y2 = end.y;
        var dot = x1 * x2 + y1 * y2;   // dot product between [x1, y1] and [x2, y2]
        var det = x1 * y2 - y1 * x2;    // determinant
        var angle = Mathf.Atan2(det, dot) * Mathf.Rad2Deg; // atan2(y, x) or atan2(sin, cos)
        
        if(angle <0)
        {
            angle = 360 + angle;
        }

        return angle;

    }

    public static float AngleBetween(float degreesA, float degreesB)
    {
        while (degreesA > 360)
        {
            degreesA -= 360;
        } // modulo , but it's float

        var _dif = Mathf.Abs(degreesA - degreesB);
        if (_dif > 180)
        {
            _dif = 360 - _dif;
        } // choose the smaller angle

        return _dif;
    }

    public static float AngleBetweenNodes(float nodeADegrees, float nodeBDegrees)
    {
        return AngleBetween(nodeADegrees + 180, nodeBDegrees);
    }

    public static float ReverseParallelAngle( float rawDegrees)
    {
        return  rawDegrees > 180 ? rawDegrees - 180 : rawDegrees + 180;

    }

    public static bool FindLineSegmentIntersection(Vector2 from, float dx, float dy, Vector2 segmentA, Vector2 segmentB, out Vector2 intersection)
    {
        var _x = from.x;
        var _y = from.y;
        var _x1 = segmentA.x;
        var _y1 = segmentA.y;

        var _x2 = segmentB.x;
        var _y2 = segmentB.y;

        //Make sure the lines aren't parallel, can use an epsilon here instead
        // Division by zero in C# at run-time is infinity. In JS it's NaN
        if (dy / dx != (_y2 - _y1) / (_x2 - _x1))
        {
            var _d = dx * (_y2 - _y1) - dy * (_x2 - _x1);
            if (_d != 0)
            {
                var _r = ((_y - _y1) * (_x2 - _x1) - (_x - _x1) * (_y2 - _y1)) / _d;
                var _s = ((_y - _y1) * dx - (_x - _x1) * dy) / _d;
                if (_r >= 0 && _s >= 0 && _s <= 1)
                {
                    intersection = new Vector2(
                        _x + _r * dx,
                        _y + _r * dy);

                    return true;
                }
            }
        }

        intersection = Vector2.zero;
        return false;
    }
}

