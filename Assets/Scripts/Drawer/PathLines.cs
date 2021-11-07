using System.Collections.Generic;
using System.Linq;
using Gamelogic.Extensions;
using UnityEngine;

public class PathLines
{
    public MarkLine[] ComputedLines { get; private set; }
    public List<FixCircle> ComputedCircles;
    public List<FixRay> ComputedRays;
    public Vector2 CenteredPosition;
    
    static FixedPointsScriptableObject FixedPoints => GameManager.Instance.FixedPoints;
    private GameSettingsScriptableObject _settings;

    public PathLines(GameSettingsScriptableObject settings)
    {
        _settings = settings;
    }

    public void ComputeSet(RoutePoint[] pointsArray, bool hasOtherMarkers = false)
    {
        ComputedLines = new MarkLine[pointsArray.Length];
        if (hasOtherMarkers)
        {
            ComputedCircles = new List<FixCircle>();
            ComputedRays = new List<FixRay>();
        }

        var lastLine = new MarkLine(null, _settings.UnitLength);
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

 

    Vector3 oldPositionVertex = Vector3.zero;

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
            var heading = Geometry.GetHeadingOfDirection(vertex - oldPositionVertex);
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

    bool GetNextComputedVertex(MarkLine[] lines,int lineIndex, int pointIndex, out int nextPoint, out int nextLineIndex)
    {
        if (lines[lineIndex].Vertexes.Length > pointIndex + 1)
        {
            nextPoint = pointIndex + 1;
            nextLineIndex = lineIndex;
            return true;
        }

        if (lines.Length > lineIndex + 1)
        {
            nextPoint = 1;
            nextLineIndex = lineIndex + 1;
            return true;
        }

        nextPoint = pointIndex;
        nextLineIndex = lineIndex;
        return false;
    }

    public bool FindClosestVertexToDistanceOnLineActive(float distance, int lineIndex, out int vertexIndex, out Vector2 vertexPosition)
    {
        var line = ComputedLines[lineIndex];
        var accumulated = 0f;
        for (var i = 1; i < line.Vertexes.Length; i++)
        {
            accumulated += (line.Vertexes[i] - line.Vertexes[i - 1]).magnitude;

            if (accumulated > distance)
            {
                vertexIndex = i;
                vertexPosition = line.Vertexes[i];
                return true;
            }
        }

        vertexIndex = 0;
        vertexPosition = Vector2.zero;
        return false;
    }
    
}

public struct PathPositionInfo
{
    public int CurrentNodeIndex;
    public int UnreachedVertexIndex;
    public float HeadingBefore;
    public Vector2 UnreachedVertexPosition;
}