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
            initialOrientationTarget: pointsArray[1].CartesianPosition );

        for (int i = 1; i < pointsArray.Length; i++)
        {
            var line = new TracedLine(
                1f,
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

    public bool FindCloseToRouteDestination(
        float maxDistance, 
        out Vector2 foundVertex, 
        out int foundLineIndex,
        out float foundAtDistanceOnTracedLine,
        out bool reachedEnd)
    {
        int foundVertexIndex = -1;
        foundAtDistanceOnTracedLine = -1;
        foundLineIndex = -1;
        foundVertex = Vector2.zero;
        reachedEnd = false;
        float foundDistance = -1;

        var maxSqrDistance = maxDistance * maxDistance;

        // 0 = start line, empty
        for (int i = 1; i < ComputedLines.Length; i++)
        {
            var computedLine = ComputedLines[i];

            for (int j = 0; j < computedLine.Vertexes.Length; j++)
            {
                var vertex = computedLine.Vertexes[j];

                var sqrDistance = (vertex - Session.PlayerAircraft.NMPosition).sqrMagnitude;
                // if is further that max distance
                if (sqrDistance > maxSqrDistance)
                {
                    continue;
                }

                // if is within maxDistance limits, but more forward
                foundVertex = vertex;
                foundLineIndex = i;
                foundVertexIndex = j;
                foundDistance = sqrDistance;

                if (i == ComputedLines.Length - 1 && j == computedLine.Vertexes.Length - 1)
                {
                    reachedEnd = true;
                }
            }
        }

        if (foundDistance <= 0)
        {
            return false;
        }

        foundAtDistanceOnTracedLine = Mathf.Max(0, foundVertexIndex -1) * Session.Settings.DrawerUnitLength;
        return true;
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