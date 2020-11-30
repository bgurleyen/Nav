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
    
    public static Vector2 GetPreviousPositionOfNode(RoutePoint node)
    {
        return GetPreviousPosition(node.CartesianPosition, node.Distance, ReverseParallelAngle(node.Degrees));
    }

    public static float AngleBetween(Vector2 start, Vector2 end)
    {
        var _x1 = start.x;
        var _x2 = end.x;
        var _y1 = start.y;
        var _y2 = end.y;
        var _dot = _x1 * _x2 + _y1 * _y2;   // dot product between [x1, y1] and [x2, y2]
        var _det = _x1 * _y2 - _y1 * _x2;    // determinant
        var _angle = Mathf.Atan2(_det, _dot) * Mathf.Rad2Deg; // atan2(y, x) or atan2(sin, cos)
        
        if(_angle <0)
        {
            _angle = 360 + _angle;
        }

        return _angle;

    }

    public static float AngleOfPosition(Vector2 ofPosition, Vector2 fromPosition)
    {
        return AngleBetween(ofPosition - fromPosition , Vector2.up);
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
    
    
    // Prints the intersection points (if any) of a circle, center 'cp' with radius 'r',
// and either an infinite line containing the points 'p1' and 'p2'
// or a segment drawn between those points.
    public static int CircleIntersects(Vector2 p1, Vector2 p2, Vector2 cp, float r, bool segment, out Vector2 int1,
        out Vector2 int2)
    {

        const float eps = 1e-5f;

        float sq(float x)
        {
            return x * x;
        }

        float fx(float A, float B, float C, float x)
        {
            return -(A * x + C) / B;
        }

        float fy(float A, float B, float C, float y)
        {
            return -(B * y + C) / A;
        }

        bool within(float x1, float y1, float x2, float y2, float x, float y)
        {
            float d1 = Mathf.Sqrt(sq(x2 - x1) + sq(y2 - y1)); // distance between end-points
            float d2 = Mathf.Sqrt(sq(x - x1) + sq(y - y1)); // distance from point to one end
            float d3 = Mathf.Sqrt(sq(x2 - x) + sq(y2 - y)); // distance from point to other end
            float delta = d1 - d2 - d3;
            return Mathf.Abs(delta) < eps; // true if delta is less than a small tolerance
        }

        bool rxy(float x1, float y1, float x2, float y2, float x, float y, bool isSegment)
        {
            return !isSegment || within(x1, y1, x2, y2, x, y);
        }

        int1 = int2 = Vector2.zero;


        float _x0 = cp.x, _y0 = cp.y;
        float _x1 = p1.x, _y1 = p1.y;
        float _x2 = p2.x, _y2 = p2.y;
        float _A = _y2 - _y1;
        float _B = _x1 - _x2;
        float _C = _x2 * _y1 - _x1 * _y2;
        float _a = sq(_A) + sq(_B);
        float _b, _c, _d;
        bool _bnz = true;
        int _cnt = 0;

        if (Mathf.Abs(_B) >= eps)
        {
            // if B isn't zero or close to it
            _b = 2 * (_A * _C + _A * _B * _y0 - sq(_B) * _x0);
            _c = sq(_C) + 2 * _B * _C * _y0 - sq(_B) * (sq(r) - sq(_x0) - sq(_y0));
        }
        else
        {
            _b = 2 * (_B * _C + _A * _B * _x0 - sq(_A) * _y0);
            _c = sq(_C) + 2 * _A * _C * _x0 - sq(_A) * (sq(r) - sq(_x0) - sq(_y0));
            _bnz = false;
        }

        _d = sq(_b) - 4 * _a * _c; // discriminant
        if (_d < 0)
        {
            // line & circle don't intersect
            return 0;
        }

        if (_d == 0)
        {
            // line is tangent to circle, so just one intersect at most
            if (_bnz)
            {
                float x = -_b / (2 * _a);
                float y = fx(_A, _B, _C, x);
                if (rxy(_x1, _y1, _x2, _y2, x, y, segment))
                {
                    int1 = new Vector2(x, y);
                    return 1;
                }
            }
            else
            {
                float y = -_b / (2 * _a);
                float x = fy(_A, _B, _C, y);
                if (rxy(_x1, _y1, _x2, _y2, x, y, segment))
                {
                    int1 = new Vector2(x, y);
                    return 1;
                }
            }
        }
        else
        {
            // two intersects at most
            _d = Mathf.Sqrt(_d);
            if (_bnz)
            {
                float x = (-_b + _d) / (2 * _a);
                float y = fx(_A, _B, _C, x);
                if (rxy(_x1, _y1, _x2, _y2, x, y, segment))
                {
                    int1 = new Vector2(x, y);
                    _cnt = 1;
                }

                x = (-_b - _d) / (2 * _a);
                y = fx(_A, _B, _C, x);
                if (rxy(_x1, _y1, _x2, _y2, x, y, segment))
                {
                    if (_cnt == 0)
                    {
                        int1 = new Vector2(x, y);
                    }
                    else
                    {
                        int2 = new Vector2(x, y);
                    }

                    _cnt++;
                }

                return _cnt;
            }
            else
            {
                float y = (-_b + _d) / (2 * _a);
                float x = fy(_A, _B, _C, y);
                if (rxy(_x1, _y1, _x2, _y2, x, y, segment))
                {
                    int1 = new Vector2(x, y);
                    _cnt = 1;
                }

                y = (-_b - _d) / (2 * _a);
                x = fy(_A, _B, _C, y);
                if (rxy(_x1, _y1, _x2, _y2, x, y, segment))
                {
                    if (_cnt == 0)
                    {
                        int1 = new Vector2(x, y);
                    }
                    else
                    {
                        int2 = new Vector2(x, y);
                    }

                    _cnt++;
                }

                return _cnt;
            }
        }

        return 0;
    }
}

