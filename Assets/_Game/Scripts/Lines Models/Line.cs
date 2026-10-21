using Gamelogic.Extensions;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Line
{
    public Vector3 EndPosition { get; protected set; }
    public RoutePoint LinkedPoint { get; private set; }
    public Vector3[] Vertexes;

    protected readonly float UnitLength;

    protected Line(RoutePoint linkedPoint, float unitLength)
    {
        UnitLength = unitLength;
        LinkedPoint = linkedPoint;
    }

    protected static Vector2[] ComputeArcPoints(float startAngle, float endAngle, float unitLength, float radius, Vector3 o, Vector3 towardsPoint)
    {
        var arcPoints = new List<Vector2>();
        var angle = startAngle;

        var rawSweep = endAngle - startAngle;
        var angleBetween = WrapSignedDegrees(rawSweep);
        // A full turn wraps to 0 and would leave the fix ring with a single point.
        if (Mathf.Abs(Mathf.Abs(rawSweep) - 360f) <= 0.5f)
            angleBetween = rawSweep >= 0f ? 360f : -360f;
        else
        {
            var reversedAngle = (360 - Mathf.Abs(angleBetween)) * (angleBetween < 0 ? 1 : -1);

            // determine the direction of the arc based on if it gets away of towards the target by a small angle
            var firstNextPoint = o + GetPointOnArc(angle + (angleBetween < 0 ? -1 : 1), radius).To3DXY();
            var reversedNextPoint = o + GetPointOnArc(angle + (reversedAngle < 0 ? -1 : 1), radius).To3DXY();

            var dist = (firstNextPoint - towardsPoint).sqrMagnitude;
            var reversedDist = (reversedNextPoint - towardsPoint).sqrMagnitude;

            angleBetween = dist < reversedDist ? angleBetween : reversedAngle;
        }

        var arcLength = radius * Mathf.Deg2Rad * angleBetween;
        int segments = 0;
        if (Mathf.Abs(angleBetween) >= 0.01f && unitLength >= 1e-4f && !float.IsNaN(arcLength) && !float.IsInfinity(arcLength))
        {
            float count = Mathf.Abs(arcLength / unitLength);
            if (float.IsNaN(count) || float.IsInfinity(count))
                count = 1f;
            segments = Mathf.Clamp(Mathf.CeilToInt(count), 0, 720);
        }
        var fraction = segments > 0 ? angleBetween / segments : 0f;

        for (var i = 0; i <= segments; i++)
        {
            if (i == segments)
            {
                angle = startAngle + angleBetween;
            }
            var x = Mathf.Sin(Mathf.Deg2Rad * angle) * radius;
            var y = Mathf.Cos(Mathf.Deg2Rad * angle) * radius;

            arcPoints.Add(new Vector2(x, y));

            angle += fraction;
        }

        return arcPoints.ToArray();
    }


    static float WrapSignedDegrees(float degrees)
    {
        if (float.IsNaN(degrees) || float.IsInfinity(degrees))
            return 0f;
        degrees %= 360f;
        if (degrees > 180f)
            degrees -= 360f;
        else if (degrees < -180f)
            degrees += 360f;
        return degrees;
    }

    private static Vector2 GetPointOnArc(float angle, float radius)
    {
        var x = Mathf.Sin(Mathf.Deg2Rad * angle) * radius;
        var y = Mathf.Cos(Mathf.Deg2Rad * angle) * radius;

        return new Vector2(x, y);
    }

    public virtual string GetName => "line";

}