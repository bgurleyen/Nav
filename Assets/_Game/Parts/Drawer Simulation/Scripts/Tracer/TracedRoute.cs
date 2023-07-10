using System;
using System.Collections.Generic;
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
        ComputedLines = new TracedLine[pointsArray.Length];
        // if (hasOtherMarkers)
        // {
        //     ComputedCircles = new List<FixCircle>();
        //     ComputedRays = new List<FixRay>();
        // }

        var pilot = new Pilot(
            nmPosition: pointsArray[0].CartesianPosition,
            initialOrientationTarget: pointsArray[1].CartesianPosition,
            true);

        for (int i = 1; i < pointsArray.Length; i++)
        {
            var line = new TracedLine(
                Session.Settings.PilotSeekDistancePathFollow,
                pilot,
                pointsArray[i - 1].CartesianPosition,
                pointsArray[i]);
            ComputedLines[i] = line;
        }
    }

    

    
    

    public bool FindClosestVertexToPositionOnLineActive(Vector2 position, int lineIndex, out int vertexIndex,
        out Vector2 vertexPosition)
    {
        var line = ComputedLines[lineIndex];
        var minSqrDist = 1000f;
        vertexIndex = -1;
        vertexPosition = Vector2.zero;
        for (var i = 1; i < line.Vertexes.Length; i++)
        {
            var sqrDist = (line.Vertexes[i] - position).sqrMagnitude;

            if (minSqrDist > sqrDist)
            {

                vertexIndex = i;
                vertexPosition = line.Vertexes[i];
                minSqrDist = sqrDist;
            }
        }

        if (vertexIndex >= 0)
        {
            if ((line.Vertexes[0] - position).sqrMagnitude >
                (line.Vertexes[0] - line.Vertexes[vertexIndex]).sqrMagnitude)
            {
                vertexIndex++;
                vertexIndex = Mathf.Min(vertexIndex, line.Vertexes.Length - 1);
            }

            return true;
        }

        return false;
    }

    public void FindClosestRoutePoint(int forLineIndex, Vector2 forNMPosition, out NodeRoutePosition foundVertexOnSegment)
    {
        var computedLine = ComputedLines[forLineIndex];
        foundVertexOnSegment = new NodeRoutePosition();

        var minFoundSqrDistance = computedLine.LinkedPoint.Distance * computedLine.LinkedPoint.Distance;
        foundVertexOnSegment.NMPosition = computedLine.SegmentVertices[0];
        
        for (int j = 0; j < computedLine.SegmentVertices.Length; j++)
        {
            var segmentVertex = computedLine.SegmentVertices[j];

            var sqrDistance = (segmentVertex - forNMPosition).sqrMagnitude;

            if (sqrDistance < minFoundSqrDistance)
            {
                minFoundSqrDistance = sqrDistance;
                foundVertexOnSegment.NMPosition = segmentVertex;
                foundVertexOnSegment.NMWalkedOnCurrentSegment = j * Session.Settings.SegmentGranularity;
            }
        }
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

        if (ComputedLines == null)
        {
            Debug.LogWarning("Computed lines null");
            return false;
        }

        // 0 = start line, empty
        for (int i = startFromSegmentIndex; i < ComputedLines.Length; i++)
        {
            var computedLine = ComputedLines[i];

            if (computedLine.LinkedPoint.IsHiddenLine)
            {
                startFromVertexIndex = 0;
                continue;
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

            // another solution was found 
            if (!reachedSegmentEnd)
            {
                lastFoundRoutePosition.SegmentIndex = i;

                if (breakOnFistSolution)
                {
                    break;
                }
            }

            startFromVertexIndex = 0;
        }

        if (lastFoundRoutePosition.SegmentIndex == ComputedLines.Length - 1 && reachedSegmentEnd)
        {
            reachedRouteEnd = true;
        }

        foundAtDistanceOnSegment = Mathf.Max(0, lastFoundRoutePosition.SegmentVertexIndex - 1) * Session.Settings.SegmentGranularity;
        return lastFoundRoutePosition.SegmentIndex > -1;

    }
}

[Serializable]
public struct NodeRoutePosition
{
    public Vector2 NMPosition;
    public float NMWalkedOnCurrentSegment;
    public int CurrentNodeIndex;
}