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
    public Vector2 CenteredPosition;
        
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

    public void FindClosestRoutePoint(int forLineIndex, Vector2 forNMPosition, out RoutePosition foundVertexOnSegment)
    {
        var computedLine = ComputedLines[forLineIndex];
        foundVertexOnSegment = new RoutePosition();

        var minFoundSqrDistance = computedLine.LinkedPoint.Distance * computedLine.LinkedPoint.Distance;
        foundVertexOnSegment.NMPositionOnSegment = computedLine.SegmentVertices[0];
        
        for (int j = 0; j < computedLine.SegmentVertices.Length; j++)
        {
            var segmentVertex = computedLine.SegmentVertices[j];

            var sqrDistance = (segmentVertex - forNMPosition).sqrMagnitude;

            if (sqrDistance < minFoundSqrDistance)
            {
                minFoundSqrDistance = sqrDistance;
                foundVertexOnSegment.NMPositionOnSegment = segmentVertex;
                foundVertexOnSegment.NMWalkedOnCurrentSegment = j * Session.Settings.SegmentGranularity;
            }
        }
    }

    public bool FindCloseToRouteSegmentDestination(
        float seekDistance,
        out Vector2 lastFoundSegmentVertex,
        out int lastFoundSegmentVertexIndex,
        out int lastFoundSegmentIndex,
        out float foundAtDistanceOnSegment,
        out bool reachedRouteEnd,
        int startFromSegmentIndex = 1,
        int startFromVertexIndex = 0,
        bool breakOnFistSolution = false,
        bool segmentBeginningIsAlwaysValid = true)
    {
        lastFoundSegmentVertexIndex = -1;
        foundAtDistanceOnSegment = -1;
        lastFoundSegmentIndex = -1;
        lastFoundSegmentVertex = Vector2.zero;
        var reachedSegmentEnd = false;
        reachedRouteEnd = false;


        // 0 = start line, empty
        for (int i = startFromSegmentIndex; i < ComputedLines.Length; i++)
        {
            var computedLine = ComputedLines[i];

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
                continue;
            }

            lastFoundSegmentVertex = foundSegmentVertex;
            lastFoundSegmentVertexIndex = foundSegmentVertexIndex;

            // another solution was found 
            if (!reachedSegmentEnd)
            {
                lastFoundSegmentIndex = i;

                if (breakOnFistSolution)
                {
                    break;
                }
            }

            startFromVertexIndex = 0;
        }

        if (lastFoundSegmentIndex == ComputedLines.Length - 1 && reachedSegmentEnd)
        {
            reachedRouteEnd = true;
        }

        foundAtDistanceOnSegment = Mathf.Max(0, lastFoundSegmentVertexIndex - 1) * Session.Settings.SegmentGranularity;
        return lastFoundSegmentIndex > -1;

    }
}

[Serializable]
public struct RoutePosition
{
    public Vector2 NMPositionOnSegment;
    public float NMWalkedOnCurrentSegment;
    public int CurrentNodeIndex;

    public void Reset()
    {
        NMPositionOnSegment = Vector2.zero;
        NMWalkedOnCurrentSegment = 0;
    }
}