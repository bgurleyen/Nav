using System;
using System.Collections.Generic;
using System.Linq;
using Navigation;
using UnityEngine;

/// <summary>
/// TracedLines will have the first element null and will be related to the end point ( at same index )
/// </summary>
public class TracedRoute
{
    public TracedLine[] ComputedLines { get; private set; }
    private static FixedPointsScriptableObject FixedPoints => Session.Routes.FixedPoints;

    public List<FixCircle> ComputedCircles;
    public List<FixRay> ComputedRays;

    public void Compute(RoutePoint[] pointsArray, bool hasOtherMarkers = false)
    {
        if (pointsArray == null || pointsArray.Length == 0)
        {
            ComputedLines = Array.Empty<TracedLine>();
            return;
        }

        ComputedLines = new TracedLine[pointsArray.Length];
        if (hasOtherMarkers)
        {
            ComputedCircles = new List<FixCircle>();
            ComputedRays = new List<FixRay>();
        }

        if (pointsArray.Length < 2 || pointsArray[0] == null || Session.Settings == null)
            return;

        var aim = pointsArray[1] != null ? pointsArray[1].CartesianPosition : pointsArray[0].CartesianPosition;
        var pilot = new Pilot(
            nmPosition: pointsArray[0].CartesianPosition,
            initialOrientationTarget: aim,
            true);

        for (int i = 1; i < pointsArray.Length; i++)
        {
            var point = pointsArray[i];
            var fromPoint = pointsArray[i - 1];
            if (point == null || fromPoint == null)
                continue;
            Vector2? vectorRayEnd = null;
            if (fromPoint.IsVector)
            {
                // A waypoint named VECTOR keeps its own outbound track for 200 NM.
                vectorRayEnd = Geometry.GetNextPosition(fromPoint.CartesianPosition, 200f, 360f - fromPoint.RawDegrees);
            }

            TracedLine line;
            if (point.IsLinearApproach)
            {
                // Inbound intercept course ends at the fix. It does not continue past it.
                line = TracedLine.ForInterceptCourse(fromPoint.CartesianPosition, point);
            }
            else
            {
                line = new TracedLine(
                    Session.Settings.PilotSeekDistancePathFollow,
                    pilot,
                    fromPoint.CartesianPosition,
                    point,
                    vectorRayEnd);
            }

            ComputedLines[i] = line;
            if (fromPoint.IsAfterDiscontinuity && fromPoint.Distance <= 0.05f)
                line.StartsAfterGap = true;

            if (vectorRayEnd.HasValue || point.IsLinearApproach)
                SnapTracerToPoint(pilot, pointsArray, i);

            if (point.IsHiddenLine)
            {
                // Hidden inbound from a passed waypoint must not pull the dashed MOD
                // trace from behind the aircraft. Jump the tracer to this node.
                SnapTracerToPoint(pilot, pointsArray, i);
            }


            // S.A. Code


            if (hasOtherMarkers && FixedPoints != null)
            {
                //var fixEntry = FixedPoints.Entries.FirstOrDefault(x => x.Name == line.LinkedPoint.Name);
                var fixEntry = FixedPoints.Entries.FirstOrDefault(x => x != null && x.Name == line.LinkedPoint.Name);
                if (fixEntry != null)
                {
                    //if (fixEntry <= 0)
                    {
                        if (Drawer.GetCircleFix(line.LinkedPoint, line.EndNMPosition, null,
                            out var circleDraw))
                        {
                            ComputedCircles.Add(circleDraw);
                        }
                    }

                    for (var j = 0; j < fixEntry.Infos.Length; j++)
                    {
                        if (Drawer.GetCircleFix(line.LinkedPoint, line.EndNMPosition, fixEntry.Infos[j],
                            out var circleDraw))
                        {
                            ComputedCircles.Add(circleDraw);
                        }

                        if (Drawer.GetRayFix(line.LinkedPoint, line.EndNMPosition, fixEntry.Infos[j],
                            out var rayDraw))
                        {
                            ComputedRays.Add(rayDraw);
                        }
                    }
                }
            }
        }
    }

    private static void SnapTracerToPoint(Pilot pilot, RoutePoint[] pointsArray, int index)
    {
        if (pilot == null || pointsArray == null || index < 0 || index >= pointsArray.Length)
            return;

        var point = pointsArray[index];
        if (point == null)
            return;

        pilot.NMPosition = point.CartesianPosition;

        var nextPoint = index + 1 < pointsArray.Length ? pointsArray[index + 1] : null;
        var next = nextPoint != null ? nextPoint.CartesianPosition : point.CartesianPosition;
        var dir = next - point.CartesianPosition;
        if (dir.sqrMagnitude < 0.0001f && Session.PlayerAircraft != null)
            dir = Geometry.GetDirectionFromHeading(Session.PlayerAircraft.HeadingDegrees);
        if (dir.sqrMagnitude > 0.0001f)
            pilot.HeadingDegrees = Geometry.GetHeadingOfDirection(dir);
    }

    public bool FindCloseToRouteSegmentDestination(
        float seekDistance,
        out RoutePosition lastFoundRoutePosition,
        out float foundAtDistanceOnSegment,
        out bool reachedRouteEnd,
        int startFromSegmentIndex = 1,
        int startFromVertexIndex = 0,
        bool breakOnFistSolution = false,
        bool segmentBeginningIsAlwaysValid = true)
    {
        lastFoundRoutePosition = new RoutePosition() { SegmentIndex = -1, SegmentVertexIndex = 1, SegmentVertex = Vector2.zero };
        foundAtDistanceOnSegment = -1;
        var reachedSegmentEnd = false;
        reachedRouteEnd = false;

        if (ComputedLines == null || Session.PlayerAircraft == null)
        {
            Debug.LogWarning("Computed lines null");
            return false;
        }

        if (startFromSegmentIndex < 0)
            startFromSegmentIndex = 0;

        var routePoints = Session.ActiveRoute?.Points;

        // 0 = start line, empty
        for (int i = startFromSegmentIndex; i < ComputedLines.Length; i++)
        {
            var computedLine = ComputedLines[i];
            if (computedLine?.LinkedPoint == null)
            {
                startFromVertexIndex = 0;
                continue;
            }

            if (computedLine.LinkedPoint.IsHiddenLine)
            {
                startFromVertexIndex = 0;
                continue;
            }

            if (computedLine.LinkedPoint.IsAfterDiscontinuity &&
                i > 0 &&
                routePoints != null &&
                i - 1 < routePoints.Length &&
                routePoints[i - 1] != null &&
                routePoints[i - 1].IsLinearApproach)
            {
                // 737 INTC CRS does not sequence through the discontinuity after the fix.
                startFromVertexIndex = 0;
                continue;
            }

            if (computedLine.LinkedPoint.IsAfterDiscontinuity || computedLine.StartsAfterGap)
            {
                // After the last fix, keep LNAV on that leg's track. Do not cross the gap.
                int legIndex = i - 1;
                var points = routePoints;
                while (legIndex > 0 && points != null && legIndex < points.Length)
                {
                    var legPoint = points[legIndex];
                    if (legPoint != null && !legPoint.IsAfterDiscontinuity && legPoint.Distance > 0.05f)
                        break;
                    legIndex--;
                }

                if (legIndex > 0 && points != null)
                {
                    var legLine = ComputedLines[legIndex];
                    var coursePoint = points[legIndex];
                    if (coursePoint == null)
                        break;
                    var dir = Geometry.GetNextPosition(Vector2.zero, 1f, coursePoint.Degrees);
                    if (dir.sqrMagnitude < 0.0001f)
                        dir = Vector2.up;
                    dir.Normalize();

                    var ahead = Session.PlayerAircraft.NMPosition + dir * 20f;
                    lastFoundRoutePosition.SegmentIndex = legIndex;
                    lastFoundRoutePosition.SegmentVertex = ahead;
                    lastFoundRoutePosition.SegmentVertexIndex = legLine != null && legLine.Vertexes != null && legLine.Vertexes.Length > 0
                        ? legLine.Vertexes.Length - 1
                        : 0;
                }
                break;
            }

            if (!computedLine.FindFurthestSeekTargetOnSegment(
                    Session.PlayerAircraft.NMPosition,
                    seekDistance,
                    out var foundSegmentVertex,
                    out var foundSegmentVertexIndex,
                    out reachedSegmentEnd,
                    startFromVertexIndex,
                    breakOnFistSolution,
                    segmentBeginningIsAlwaysValid))
            {
                if (segmentBeginningIsAlwaysValid)
                {
                    Debug.LogError("not found destination on segment with constraint");
                }
                startFromVertexIndex = 0;
                continue;
            }

            lastFoundRoutePosition.SegmentVertex = foundSegmentVertex;
            lastFoundRoutePosition.SegmentVertexIndex = foundSegmentVertexIndex;

            var pastRouteEnd = routePoints == null || i + 1 >= routePoints.Length;
            var nextPoint = !pastRouteEnd ? routePoints[i + 1] : null;
            var interceptEndsHere = computedLine.LinkedPoint.IsLinearApproach &&
                                    (pastRouteEnd || (nextPoint != null && nextPoint.IsAfterDiscontinuity));

            // another solution was found 
            if (!reachedSegmentEnd)
            {
                lastFoundRoutePosition.SegmentIndex = i;

                if (breakOnFistSolution)
                {
                    break;
                }
            }
            else if (interceptEndsHere)
            {
                lastFoundRoutePosition.SegmentIndex = i;
                break;
            }

            startFromVertexIndex = 0;
        }

        if (lastFoundRoutePosition.SegmentIndex == ComputedLines.Length - 1 && reachedSegmentEnd)
        {
            reachedRouteEnd = true;
        }

        var granularity = Session.Settings != null ? Session.Settings.SegmentGranularity : 1f;
        foundAtDistanceOnSegment = Mathf.Max(0, lastFoundRoutePosition.SegmentVertexIndex - 1) * granularity;
        return lastFoundRoutePosition.SegmentIndex > -1;

    }

    /// <summary>
    /// Position at fraction t (0..1) along the drawn vertices of one computed line.
    /// </summary>
    public bool TryGetPositionOnLine(int lineIndex, float t, out Vector2 position)
    {
        position = Vector2.zero;
        if (ComputedLines == null || lineIndex < 1 || lineIndex >= ComputedLines.Length)
        {
            return false;
        }

        var line = ComputedLines[lineIndex];
        if (line?.Vertexes == null || line.Vertexes.Length == 0)
        {
            return false;
        }

        if (line.Vertexes.Length == 1)
        {
            position = line.Vertexes[0];
            return true;
        }

        t = Mathf.Clamp01(t);
        var target = t * (line.Vertexes.Length - 1);
        var i0 = Mathf.FloorToInt(target);
        var i1 = Mathf.Min(i0 + 1, line.Vertexes.Length - 1);
        position = Vector2.Lerp(line.Vertexes[i0], line.Vertexes[i1], target - i0);
        return true;
    }
}