using Gamelogic.Extensions;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class Geometry
{
    public static Vector2 GetNextPosition(Vector2 start, float distance, float degrees)
    {
        var verticalVector = new Vector2(0, distance);
        var rotatedVector = verticalVector.Rotate(degrees);

        return start + rotatedVector;
    }

    public static Vector2 GetDirectionFromHeading(float degrees)
    {
        return GetNextPosition(Vector2.zero, 1, -degrees);
    }

    public static float GetHeadingOfDirection(Vector2 direction)
    {
        return  PositiveAngleBetween(direction, Vector2.up);
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
        var x1 = start.x;
        var x2 = end.x;
        var y1 = start.y;
        var y2 = end.y;
        var dot = x1 * x2 + y1 * y2; // dot product between [x1, y1] and [x2, y2]
        var det = x1 * y2 - y1 * x2; // determinant
        var angle = Mathf.Atan2(det, dot) * Mathf.Rad2Deg; // atan2(y, x) or atan2(sin, cos)

        return angle;

    }

    public static float PositiveAngleBetween(Vector2 start, Vector2 end)
    {
        var x1 = start.x;
        var x2 = end.x;
        var y1 = start.y;
        var y2 = end.y;
        var dot = x1 * x2 + y1 * y2; // dot product between [x1, y1] and [x2, y2]
        var det = x1 * y2 - y1 * x2; // determinant
        var angle = Mathf.Atan2(det, dot) * Mathf.Rad2Deg; // atan2(y, x) or atan2(sin, cos)


        
        return AbsAngle( angle);

    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="ofPosition"></param>
    /// <param name="fromPosition"></param>
    /// <returns>Raw degrees</returns>
    public static float AngleOfPosition(Vector2 ofPosition, Vector2 fromPosition)
    {
        return PositiveAngleBetween(ofPosition - fromPosition, Vector2.up);
    }

    private static float PositiveAngleBetween(float degreesA, float degreesB)
    {
        while (degreesA > 360)
        {
            degreesA -= 360;
        } // modulo , but it's float

        var dif = Mathf.Abs(degreesA - degreesB);
        if (dif > 180)
        {
            dif = 360 - dif;
        } // choose the smaller angle

        return dif;
    }

    public static float AbsAngle(float a)
    {
        return (360 + a % 360) % 360;
    }

    public static float AngleDelta(float target, float current)
    {
        var positiveDiff = AbsAngle(target - current);
        var minimisedDiff = positiveDiff > 180 ? positiveDiff - 360 : positiveDiff;

        return minimisedDiff;
    }

    public static float AngleBetweenNodes(float nodeADegrees, float nodeBDegrees)
    {
        return PositiveAngleBetween(nodeADegrees + 180, nodeBDegrees);
    }

    public static float ReverseParallelAngle(float rawDegrees)
    {
        return rawDegrees > 180 ? rawDegrees - 180 : rawDegrees + 180;

    }

    public static bool FindLineSegmentIntersection(Vector2 from, float dx, float dy, Vector2 segmentA, Vector2 segmentB,
        out Vector2 intersection, bool clamToSegment = true)
    {
        var x = from.x;
        var y = from.y;
        var x1 = segmentA.x;
        var y1 = segmentA.y;

        var x2 = segmentB.x;
        var y2 = segmentB.y;

        //Make sure the lines aren't parallel, can use an epsilon here instead
        // Division by zero in C# at run-time is infinity. In JS it's NaN
        if (dy / dx != (y2 - y1) / (x2 - x1))
        {
            var d = dx * (y2 - y1) - dy * (x2 - x1);
            if (d != 0)
            {
                var r = ((y - y1) * (x2 - x1) - (x - x1) * (y2 - y1)) / d;
                var s = ((y - y1) * dx - (x - x1) * dy) / d;
                if (r >= 0 && (!clamToSegment || s >= 0 && s <= 1))
                {
                    intersection = new Vector2(
                        x + r * dx,
                        y + r * dy);

                    return true;
                }
            }
        }

        intersection = Vector2.zero;
        return false;
    }

    public static bool IsWithinSegment(float x1, float y1, float x2, float y2, float x, float y)
    {
        var d1 = Mathf.Sqrt(Sq(x2 - x1) + Sq(y2 - y1)); // distance between end-points
        var d2 = Mathf.Sqrt(Sq(x - x1) + Sq(y - y1)); // distance from point to one end
        var d3 = Mathf.Sqrt(Sq(x2 - x) + Sq(y2 - y)); // distance from point to other end
        var delta = d1 - d2 - d3;
        return Mathf.Abs(delta) < Eps; // true if delta is less than a small tolerance
    }

    private static float Sq(float x)
    {
        return x * x;
    }

    private const float Eps = 0.1f;



    public static bool GetForwardCircleIntersects(Vector2 start, Vector2 end, Vector2 cp, float r, bool segment,
        out Vector2 intersection)
    {
        intersection = Vector2.zero;
        var count = CircleIntersects(start, end, cp, r, segment, out var intersection1, out var intersection2);
        if (count == 0)
        {
            return false;
        }

        // if both intersection are found we take the forward one
        if (count == 2)
        {
            // we are not sure of the order of the circle intersections on the segment 
            var firstIsBefore = Vector2.SqrMagnitude(start - intersection1) <
                                Vector2.SqrMagnitude(start - intersection2);

            // --> the center of turn can be the intersection since is on the same line with the exit
            intersection = firstIsBefore ? intersection2 : intersection1;
            return true;
        }

        // if only 1 intersection is found we check if is forward oriented
        bool isForward = Vector2.SqrMagnitude(start - intersection1) > Vector2.SqrMagnitude(start - cp);
        intersection = intersection1;
        return isForward;
    }

    // Prints the intersection points (if any) of a circle, center 'cp' with radius 'r',
    // and either an infinite line containing the points 'p1' and 'p2'
    // or a segment drawn between those points.
    public static int CircleIntersects(Vector2 p1, Vector2 p2, Vector2 cp, float r, bool segment, out Vector2 int1,
        out Vector2 int2)
    {

        float FX(float a, float b, float c, float x)
        {
            return -(a * x + c) / b;
        }

        float Fy(float a, float b, float c, float y)
        {
            return -(b * y + c) / a;
        }



        bool Rxy(float cx1, float cy1, float cx2, float cy2, float x, float y, bool isSegment)
        {
            return !isSegment || IsWithinSegment(cx1, cy1, cx2, cy2, x, y);
        }

        int1 = int2 = Vector2.zero;


        float x0 = cp.x, y0 = cp.y;
        float x1 = p1.x, y1 = p1.y;
        float x2 = p2.x, y2 = p2.y;
        var dy = y2 - y1;
        var dx = x1 - x2;
        var dC = x2 * y1 - x1 * y2;
        var da = Sq(dy) + Sq(dx);
        float db, dc, dd;
        var bnz = true;
        var cnt = 0;

        if (Mathf.Abs(dx) >= Eps)
        {
            // if B isn't zero or close to it
            db = 2 * (dy * dC + dy * dx * y0 - Sq(dx) * x0);
            dc = Sq(dC) + 2 * dx * dC * y0 - Sq(dx) * (Sq(r) - Sq(x0) - Sq(y0));
        }
        else
        {
            db = 2 * (dx * dC + dy * dx * x0 - Sq(dy) * y0);
            dc = Sq(dC) + 2 * dy * dC * x0 - Sq(dy) * (Sq(r) - Sq(x0) - Sq(y0));
            bnz = false;
        }

        dd = Sq(db) - 4 * da * dc; // discriminant
        if (dd < 0)
        {
            // line & circle don't intersect
            return 0;
        }

        if (dd == 0)
        {
            // line is tangent to circle, so just one intersect at most
            if (bnz)
            {
                var x = -db / (2 * da);
                var y = FX(dy, dx, dC, x);
                if (Rxy(x1, y1, x2, y2, x, y, segment))
                {
                    int1 = new Vector2(x, y);
                    return 1;
                }
            }
            else
            {
                var y = -db / (2 * da);
                var x = Fy(dy, dx, dC, y);
                if (Rxy(x1, y1, x2, y2, x, y, segment))
                {
                    int1 = new Vector2(x, y);
                    return 1;
                }
            }
        }
        else
        {
            // two intersects at most
            dd = Mathf.Sqrt(dd);
            if (bnz)
            {
                var x = (-db + dd) / (2 * da);
                var y = FX(dy, dx, dC, x);
                if (Rxy(x1, y1, x2, y2, x, y, segment))
                {
                    int1 = new Vector2(x, y);
                    cnt = 1;
                }

                x = (-db - dd) / (2 * da);
                y = FX(dy, dx, dC, x);
                if (Rxy(x1, y1, x2, y2, x, y, segment))
                {
                    if (cnt == 0)
                    {
                        int1 = new Vector2(x, y);
                    }
                    else
                    {
                        int2 = new Vector2(x, y);
                    }

                    cnt++;
                }

                return cnt;
            }
            else
            {
                var y = (-db + dd) / (2 * da);
                var x = Fy(dy, dx, dC, y);
                if (Rxy(x1, y1, x2, y2, x, y, segment))
                {
                    int1 = new Vector2(x, y);
                    cnt = 1;
                }

                y = (-db - dd) / (2 * da);
                x = Fy(dy, dx, dC, y);
                if (Rxy(x1, y1, x2, y2, x, y, segment))
                {
                    if (cnt == 0)
                    {
                        int1 = new Vector2(x, y);
                    }
                    else
                    {
                        int2 = new Vector2(x, y);
                    }

                    cnt++;
                }

                return cnt;
            }
        }

        return 0;
    }



    /// <summary>
    /// Calculate the distance between point pt and the segment p1 --> p2. 
    /// </summary>
    /// <param name="pt"></param>
    /// <param name="p1"></param>
    /// <param name="p2"></param>
    /// <param name="closest"></param>
    /// <param name="distance"></param>
    /// <returns>if the point is in segments limits</returns>
    public static bool FindDistanceToSegment(Vector2 pt, Vector2 p1, Vector2 p2, out Vector2 closest,
        out float distance)
    {
        var dx = p2.x - p1.x;
        var dy = p2.y - p1.y;
        if ((dx == 0) && (dy == 0))
        {
            // It's a point not a line segment.
            closest = p1;
            dx = pt.x - p1.x;
            dy = pt.y - p1.y;
            distance = Mathf.Sqrt(dx * dx + dy * dy);
            return false;
        }

        // Calculate the t that minimizes the distance.
        var t = ((pt.x - p1.x) * dx + (pt.y - p1.y) * dy) /
                (dx * dx + dy * dy);

        // See if this represents one of the segment's
        // end points or a point in the middle.
        if (t < 0)
        {
            closest = new Vector2(p1.x, p1.y);
            dx = pt.x - p1.x;
            dy = pt.y - p1.y;
        }
        else if (t > 1)
        {
            closest = new Vector2(p2.x, p2.y);
            dx = pt.x - p2.x;
            dy = pt.y - p2.y;
        }
        else
        {
            closest = new Vector2(p1.x + t * dx, p1.y + t * dy);
            dx = pt.x - closest.x;
            dy = pt.y - closest.y;
        }

        distance = Mathf.Sqrt(dx * dx + dy * dy);
        return t >= 0 && t <= 1;
    }
}

