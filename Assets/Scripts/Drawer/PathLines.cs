using System.Collections.Generic;
using System.Linq;
using Gamelogic.Extensions;
using UnityEngine;
using UnityEngine.Profiling.Experimental;

public class PathLines
{
    public MarkLine[] ComputedLines { get; private set; }
    public MarkLine[] ComputedLinesMod { get; private set; }
    public List<FixCircle> ComputedCircles;
    public List<FixRay> ComputedRays;
    public Vector2 CenteredPosition;
    
    static FixedPointsScriptableObject FixedPoints => GameManager.Instance.FixedPoints;
    
    public void ComputeSet(RouteScriptableObject route, bool isMod)
    {
        if (route == null)
        {
            return;
        }

        if (isMod)
        {
            ComputedLinesMod = new MarkLine[route.Points.Length];
        }
        else
        {
            ComputedCircles = new List<FixCircle>();
            ComputedRays = new List<FixRay>();
            ComputedLines = new MarkLine[route.Points.Length];
        }

        var _computedLines = isMod ? ComputedLinesMod : ComputedLines;

        var _lastLine = new MarkLine(null);
        _lastLine.InitBeginning();
        var _currentIndex = 0;

        while (_currentIndex < route.Points.Length - 1)
        {
            if (!Drawer.GetNextLine(_lastLine, _currentIndex, route.Points, out var _line, out _currentIndex))
            {
                continue;
            }

            if (_line == null)
            {
                continue;
            }
            _computedLines[_currentIndex] = _line;
            _lastLine = _line;

            if (_line.LinkedPoint.IsCenter)
            {
                CenteredPosition = _line.EndPosition;
            }

            if (!isMod && FixedPoints != null)
            {
                var _fixEntry = FixedPoints.Entries.FirstOrDefault(x => x.Name == _line.LinkedPoint.Name);
                if (_fixEntry != null)
                {
                    for (var i = 0; i < _fixEntry.Infos.Length; i++)
                    {
                        if (Drawer.GetCircleFix(_line.LinkedPoint, _line.EndPosition, _fixEntry.Infos[i],
                            out var _circleDraw))
                        {
                            ComputedCircles.Add(_circleDraw);
                        }

                        if (Drawer.GetRayFix(_line.LinkedPoint, _line.EndPosition, _fixEntry.Infos[i],
                            out var _rayDraw))
                        {
                            ComputedRays.Add(_rayDraw);
                        }

                    }
                }
            }
        }
    }
    
    
    public Vector2 GetNodePosition(int lineIndex)
    {
        if (ComputedLines.Length <= lineIndex)
        {
            return Vector2.zero;
        }
        return ComputedLines[lineIndex].EndPosition;
    }
    
    /// <summary>
    ///  On Active Set
    /// </summary>
    /// <returns></returns>
    public bool GetFirstDestination(out PathVertexIndex vertexIndex)
    {
        oldPositionVertex = Vector3.zero;
        return GetNextDestination(1, 0, out vertexIndex);
    }

    public bool GetFirstDestinationFromNode(RoutePoint node, out PathVertexIndex vertexIndex)
    {
        oldPositionVertex = Vector3.zero;

        var _currentNodeIndex = GameManager.Instance.ActiveRoute.GetIndex(node.ID);
        var _heading = node.Degrees; // Not sure if matters, but it's not correct

        vertexIndex = new PathVertexIndex
        {
            CurrentNodeIndex = _currentNodeIndex + 1,
            UnreachedPoint = 0,
            HeadingBefore = _heading,
            VertexPosition = node.CartesianPosition
        };

        return true;
    }

    Vector3 oldPositionVertex = Vector3.zero;
    /// <summary>
    /// 
    /// </summary>
    /// <param name="currentLineIndex">First line [0] is with no vertexes, [1] starts form (0,0)</param>
    /// <param name="currentPointIndex"></param>
    /// <param name="vertexIndex"></param>
    /// <returns></returns>
    public bool GetNextDestination(int currentLineIndex, int currentPointIndex, out PathVertexIndex vertexIndex)
    {
        if (GetNextComputedVertex(ComputedLines, currentLineIndex, currentPointIndex, out var _nextPoint, out var _nextLine))
        {
            var _vertex = ComputedLines[_nextLine].Vertexes[_nextPoint];
            var _heading = Geometry.GetHeadingOfDirection(_vertex - oldPositionVertex);
            oldPositionVertex = _vertex;
            
            vertexIndex = new PathVertexIndex
            {
                CurrentNodeIndex = _nextLine,
                UnreachedPoint = _nextPoint,
                HeadingBefore = _heading,
                VertexPosition = _vertex.To2DXY()
            };
            return true;
        }

        vertexIndex = new PathVertexIndex();
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
        var _line = ComputedLines[lineIndex];
        var _accumulated = 0f;
        for (var i = 1; i < _line.Vertexes.Length; i++)
        {
            _accumulated += (_line.Vertexes[i] - _line.Vertexes[i - 1]).magnitude;

            if (_accumulated > distance)
            {
                vertexIndex = i;
                vertexPosition = _line.Vertexes[i];
                return true;
            }
        }

        vertexIndex = 0;
        vertexPosition = Vector2.zero;
        return false;
    }
    
}

public struct PathVertexIndex
{
    public int CurrentNodeIndex;
    public int UnreachedPoint;
    public float HeadingBefore;
    public Vector2 VertexPosition;
}