using Gamelogic.Extensions;
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
        degreesA = WrapDegrees(degreesA);
        degreesB = WrapDegrees(degreesB);

        var dif = Mathf.Abs(degreesA - degreesB);
        if (dif > 180f)
            dif = 360f - dif;

        return dif;
    }

    static float WrapDegrees(float degrees)
    {
        if (float.IsNaN(degrees) || float.IsInfinity(degrees))
            return 0f;
        return Mathf.Repeat(degrees, 360f);
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

    /// <summary>
    /// Where a heading meets the inbound course to a waypoint.
    /// The course is the track flown toward the fix. The intercept is before the fix, never past it.
    /// Present track is used first. If it does not meet the course, a 45° intercept is used.
    /// </summary>
    public static bool TryInboundIntercept(Vector2 from, Vector2 waypoint, float inboundCourse, float trackHeading,
        out Vector2 intercept)
    {
        if (TryInterceptOnHeading(from, waypoint, inboundCourse, trackHeading, out intercept))
            return true;

        var left = Mathf.Repeat(inboundCourse - 45f, 360f);
        var right = Mathf.Repeat(inboundCourse + 45f, 360f);
        var leftOk = TryInterceptOnHeading(from, waypoint, inboundCourse, left, out var leftHit);
        var rightOk = TryInterceptOnHeading(from, waypoint, inboundCourse, right, out var rightHit);
        if (leftOk && rightOk)
        {
            var leftTurn = Mathf.Abs(Mathf.DeltaAngle(trackHeading, left));
            var rightTurn = Mathf.Abs(Mathf.DeltaAngle(trackHeading, right));
            intercept = leftTurn <= rightTurn ? leftHit : rightHit;
            return true;
        }

        if (leftOk)
        {
            intercept = leftHit;
            return true;
        }

        if (rightOk)
        {
            intercept = rightHit;
            return true;
        }

        intercept = waypoint;
        return false;
    }

    /// <summary>
    /// Forward intersection of a heading ray with the inbound course, strictly before the fix.
    /// </summary>
    public static bool TryInterceptOnHeading(Vector2 from, Vector2 waypoint, float inboundCourse, float heading,
        out Vector2 intercept)
    {
        intercept = waypoint;
        var courseDir = GetDirectionFromHeading(inboundCourse);
        var trackDir = GetDirectionFromHeading(heading);
        if (courseDir.sqrMagnitude < 0.0001f || trackDir.sqrMagnitude < 0.0001f)
            return false;

        courseDir.Normalize();
        trackDir.Normalize();

        // from + track * s = waypoint - course * d, d > 0 means before the fix.
        var rhs = waypoint - from;
        var det = trackDir.x * courseDir.y - trackDir.y * courseDir.x;
        if (Mathf.Abs(det) < 0.0001f)
            return false;

        var s = (rhs.x * courseDir.y - rhs.y * courseDir.x) / det;
        var d = (trackDir.x * rhs.y - trackDir.y * rhs.x) / det;
        if (s < 0.15f || d < 0.25f || d > 250f)
            return false;

        intercept = waypoint - courseDir * d;
        return true;
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
}

