using System;
using System.Collections.Generic;
using System.Linq;
using Gamelogic.Extensions;
using Navigation;
using UnityEngine;

public class PathLines
{
    public MarkLine[] ComputedLines { get; private set; }
    public List<FixCircle> ComputedCircles;
    public List<FixRay> ComputedRays;
    public Vector2 CenteredPosition;

    private static FixedPointsScriptableObject FixedPoints => Session.Routes.FixedPoints;
    private readonly float _drawerUnitLength;

    public PathLines(float drawerUnitLength)
    {
        _drawerUnitLength = drawerUnitLength;
    }

    public void ComputeSet(RoutePoint[] pointsArray, bool hasOtherMarkers = false)
    {
        ComputedLines = new MarkLine[pointsArray.Length];
        if (hasOtherMarkers)
        {
            ComputedCircles = new List<FixCircle>();
            ComputedRays = new List<FixRay>();
        }

        var lastLine = new MarkLine(null, _drawerUnitLength);
        lastLine.InitBeginning();
        var currentIndex = 0;

        while (currentIndex < pointsArray.Length - 1)
        {
            if (!LinesComputer.GetNextLine(lastLine, currentIndex, pointsArray, out var line, out currentIndex))
            {
                continue;
            }

            if (line == null)
            {
                continue;
            }

            ComputedLines[currentIndex] = line;
            lastLine = line;

            if (line.LinkedPoint.IsCenter)
            {
                CenteredPosition = line.EndPosition;
            }

            if (hasOtherMarkers && FixedPoints != null)
            {
                var fixEntry = FixedPoints.Entries.FirstOrDefault(x => x.Name == line.LinkedPoint.Name);
                if (fixEntry != null)
                {
                    for (var i = 0; i < fixEntry.Infos.Length; i++)
                    {
                        if (Drawer.GetCircleFix(line.LinkedPoint, line.EndPosition, fixEntry.Infos[i],
                            out var circleDraw))
                        {
                            ComputedCircles.Add(circleDraw);
                        }

                        if (Drawer.GetRayFix(line.LinkedPoint, line.EndPosition, fixEntry.Infos[i],
                            out var rayDraw))
                        {
                            ComputedRays.Add(rayDraw);
                        }

                    }
                }
            }
        }
    }


    
    
    /// <summary>
    ///  On Active Set
    /// </summary>
    /// <returns></returns>
    public bool GetFirstDestination(out PathPositionInfo positionInfo)
    {
        oldPositionVertex = Vector3.zero;
        return GetNextDestination(1, 0, out positionInfo);
    }

 

    public Vector3 oldPositionVertex = Vector3.zero;

    public void ResetOldPosition()
    {
        oldPositionVertex = Vector3.zero;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="computedLines"></param>
    /// <param name="currentLineIndex">First line [0] is with no vertexes, [1] starts form (0,0)</param>
    /// <param name="currentPointIndex"></param>
    /// <param name="positionInfo"></param>
    /// <returns></returns>
    public bool GetNextDestination( int currentLineIndex, int currentPointIndex, out PathPositionInfo positionInfo)
    {
        if (GetNextComputedVertex(ComputedLines, currentLineIndex, currentPointIndex, out var nextPoint, out var nextLine))
        {
            
            var vertex = ComputedLines[nextLine].Vertexes[nextPoint];
            var direction = vertex - oldPositionVertex;
            var heading = direction == Vector3.zero
                ? Session.PlayerAircraft.HeadingDegrees
                : Geometry.GetHeadingOfDirection(direction);
            oldPositionVertex = vertex;
            
            positionInfo = new PathPositionInfo
            {
                CurrentNodeIndex = nextLine,
                UnreachedVertexIndex = nextPoint,
                HeadingBefore = heading,
                UnreachedVertexPosition = vertex.To2DXY()
            };
            return true;
        }

        positionInfo = new PathPositionInfo();
        return false;
    }

    private bool GetNextComputedVertex(MarkLine[] lines,int lineIndex, int pointIndex, out int nextPoint, out int nextLineIndex)
    {
        if (lines[lineIndex].Vertexes.Length > pointIndex + 1)
        {
            nextPoint = pointIndex + 1;
            nextLineIndex = lineIndex;
            if (ComputedLines[nextLineIndex] == null)
            {
                Debug.LogError("Found null line. skipping");
                return false;
            }

            return true;
        }

        if (lines.Length > lineIndex + 1)
        {
            nextPoint = 1;
            nextLineIndex = lineIndex + 1;
            
            if (ComputedLines[nextLineIndex] == null)
            {
                Debug.LogError("Found null line. skipping");
                return false;
            }
            return true;
        }

        nextPoint = pointIndex;
        nextLineIndex = lineIndex;
        return false;
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
            var sqrDist = ((Vector2) line.Vertexes[i] - position).sqrMagnitude;

            if (minSqrDist > sqrDist)
            {

                vertexIndex = i;
                vertexPosition = line.Vertexes[i];
                minSqrDist = sqrDist;
            }
        }

        if (vertexIndex >= 0)
        {
            if (((Vector2) line.Vertexes[0] - position).sqrMagnitude >
                ((Vector2) line.Vertexes[0] - (Vector2) line.Vertexes[vertexIndex]).sqrMagnitude)
            {
                vertexIndex++;
                vertexIndex = Mathf.Min(vertexIndex, line.Vertexes.Length - 1);
            }

            return true;
        }

        return false;
    }

    public bool FindCloseToPathDestination(float maxDistance, out Vector2 foundVertex, out int foundLineIndex, out bool reachedEnd)
    {
        int foundVertexIndex = -1;
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
                var vertex = (Vector2)computedLine.Vertexes[j];

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

        return true;
    }

}

[Serializable]
public struct PathPositionInfo
{
    public int CurrentNodeIndex;
    public int UnreachedVertexIndex;
    public float HeadingBefore;
    public Vector2 UnreachedVertexPosition;
}