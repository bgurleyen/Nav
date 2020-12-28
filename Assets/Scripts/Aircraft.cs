using System;
using UnityEngine;

public class Aircraft
{
    const float DeltaTime = 0.00003f;
    const float MaxTurningSpeed = 1f;
    public const float ForwardThreshold = 2f; // @$# this has to be in sync with the minimum turn radius 
    
    // position that can be on the generated curved sections of the lines
    public Vector2 PositionFreeOrOnCurvedPath { get; private set; }
    
    public Vector2 PositionFreeOrOnRouteSegment { get; private set; }
    
    public float Heading { get; private set; } // Degrees based rotation
    public float TargetHeading { get; private set; }
    public float WalkedDistanceOnSegment { get; private set; }
    public PathPositionInfo RoutePathLocalization;
    public PathPositionInfo RejoinPathLocalization;

    public PathLines tempPathLines { get; private set; }

    int cachedExitSegmentOfHeadingRejoinIntersection;
    Vector2 cachedExitPointFromHeading;

    public float ComputedDistanceLeftOnSegment
    {
        get
        {
            if (!IsFreeFlight && IsOnRoute)
            {
                return GetWalkedDistanceLeftOnSegment();
            }

            var _nextViableNodeIndex = PositionVirtualNode.PassedNodeIndex + 1;
            while (GameManager.Instance.ActiveRoute.Points[_nextViableNodeIndex].IsSkippable)
            {
                _nextViableNodeIndex++;
            }
            return (GameManager.Instance.ActiveRoute.GetCartesianPosition(_nextViableNodeIndex) -
                    PositionFreeOrOnRouteSegment).magnitude;
        }
    }

    Vector2 CurrentDirection => Geometry.GetDirectionFromHeading(Heading);
    float FrameDistance => speed * DeltaTime;

    float speed;

    public void ResetOnActiveSet(float aircraftSpeed, float altitude)
    {
        speed = aircraftSpeed;
        PositionFreeOrOnCurvedPath = Vector2.zero;
        PositionFreeOrOnRouteSegment = Vector2.zero;
        WalkedDistanceOnSegment = 0;

        if (GameManager.Instance.ActiveRoute.PathLines.GetFirstDestination(out RoutePathLocalization))
        {
            Heading = RoutePathLocalization.HeadingBefore;
        }
        else
        {
            throw new Exception("Could not reset to data set");
        }
    }

    public bool IsFreeFlight;
    public bool IsOnRoute = true;

    public void StartHeadingMode()
    {
        IsFreeFlight = true;
        IsOnRoute = false;
    }

    public enum RejoinRouteMode { DirectIntersection, ClosestSegment, NextRouteNode}

    public void StartLNavMode(RejoinRouteMode mode)
    {
        switch (mode)
        {
            case RejoinRouteMode.DirectIntersection:
                ComputeTempPathForDirectIntersection();
                break;
            case RejoinRouteMode.ClosestSegment:
                break;
            case RejoinRouteMode.NextRouteNode:
                ComputeTempPathForNextNode();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(mode), mode, null);
        }
    }

    void ComputeTempPathForNextNode()
    {

        GameManager.Instance.ActiveRoute.FindFreeFlightNextNodeExitScenario(out var _futurePosition, out var _centerOfTurn, out cachedExitPointFromHeading,
            out cachedExitSegmentOfHeadingRejoinIntersection);
      
            //compute rejoin path

            Debug.Log("start LNAV");
            tempPathLines = new PathLines();

            var _tempPoints = new RoutePoint[5];
            var _lastPoint = RoutePoint.ConstructFromPosition(Vector2.zero, null);
            _tempPoints[0] = _lastPoint;
            _lastPoint = RoutePoint.ConstructFromPosition(PositionFreeOrOnCurvedPath, _lastPoint);
            _tempPoints[1] = _lastPoint;
            _lastPoint = RoutePoint.ConstructFromPosition(_futurePosition, _lastPoint);
            _tempPoints[2] = _lastPoint;
            _lastPoint = RoutePoint.ConstructFromPosition(_centerOfTurn, _lastPoint);
            _tempPoints[3] = _lastPoint;
            _lastPoint = RoutePoint.ConstructFromPosition(cachedExitPointFromHeading, _lastPoint);
            _tempPoints[4] = _lastPoint;

            tempPathLines.ComputeSet(_tempPoints);

            GetFirstDestinationFromNode(tempPathLines, _tempPoints[1], _tempPoints,
                out RejoinPathLocalization);

            IsFreeFlight = false;
    }

    void ComputeTempPathForDirectIntersection()
    {
        if (GameManager.Instance.ActiveRoute.FindFreeFlightDirectExitScenario(out var _centerOfTurn, out cachedExitPointFromHeading,
            out cachedExitSegmentOfHeadingRejoinIntersection))
        {
            //compute rejoin path

            Debug.Log("start LNAV");
            tempPathLines = new PathLines();

            var _tempPoints = new RoutePoint[4];
            var _lastPoint = RoutePoint.ConstructFromPosition(Vector2.zero, null);
            _tempPoints[0] = _lastPoint;
            _lastPoint = RoutePoint.ConstructFromPosition(PositionFreeOrOnCurvedPath, _lastPoint);
            _tempPoints[1] = _lastPoint;
            _lastPoint = RoutePoint.ConstructFromPosition(_centerOfTurn, _lastPoint);
            _tempPoints[2] = _lastPoint;
            _lastPoint = RoutePoint.ConstructFromPosition(cachedExitPointFromHeading, _lastPoint);
            _tempPoints[3] = _lastPoint;

            tempPathLines.ComputeSet(_tempPoints);

            GetFirstDestinationFromNode(tempPathLines, _tempPoints[1], _tempPoints,
                out RejoinPathLocalization);

            IsFreeFlight = false;
            
            

        }
        else
        {
            Debug.LogError("No Intersection Point Found");
        }
    }


    // on free flight
    void ExecuteStepMove()
    {
        PositionFreeOrOnCurvedPath += CurrentDirection * FrameDistance;
        PositionFreeOrOnRouteSegment = PositionFreeOrOnCurvedPath;
    }

    void ExecuteLerpMove(PathPositionInfo pathLocalisation ,float stepDistance, float distanceLeftToNextVertex)
    {
        if (distanceLeftToNextVertex > 0)
        {
            PositionFreeOrOnCurvedPath = Vector2.Lerp(PositionFreeOrOnCurvedPath,
                pathLocalisation.UnreachedVertexPosition, stepDistance / distanceLeftToNextVertex);
        }

        if (IsOnRoute)
        {
            WalkedDistanceOnSegment += stepDistance;
            PositionFreeOrOnRouteSegment = Vector2.Lerp(
                PositionVirtualNode.CurrentSegment.StartPosition,
                PositionVirtualNode.CurrentSegment.EndPosition,
                WalkedDistanceOnSegment / PositionVirtualNode.GetNodeTo.Distance);
        }
        else
        {
            PositionFreeOrOnRouteSegment = PositionFreeOrOnCurvedPath;
        }
    }

    void ExecuteStepHeadingCorrection()
    {
        if (Math.Abs(TargetHeading - Heading) > 0.01f)
        {
            Heading = Mathf.MoveTowardsAngle(Heading, TargetHeading, MaxTurningSpeed);
        }
    }

    void IndicateTargetHeading(float heading)
    {
        TargetHeading = heading;
    }

    void AdvanceOnRejoinPath(out bool rejoined,out int exitSegmentIndex, out float leftFrameDistanceToWalk)
    {
        exitSegmentIndex = cachedExitSegmentOfHeadingRejoinIntersection;
        leftFrameDistanceToWalk = 0;
        rejoined = false;
        ExecuteStepHeadingCorrection();

        var _distanceLeftToNextVertex =
            (RejoinPathLocalization.UnreachedVertexPosition - PositionFreeOrOnCurvedPath).magnitude;

        var _goesOver = _distanceLeftToNextVertex <= FrameDistance;

        // while within current segment there will be no change on state - just advance
        if (!_goesOver)
        {
            ExecuteLerpMove(RejoinPathLocalization, FrameDistance, _distanceLeftToNextVertex);
            return;
        }


        // Will break motion in two: corner, after corner

        // move to the corner
        ExecuteLerpMove(RejoinPathLocalization, _distanceLeftToNextVertex, _distanceLeftToNextVertex);
        var _leftToAdvance = FrameDistance - _distanceLeftToNextVertex;

        // advance to next point
        if (tempPathLines.GetNextDestination(
            RejoinPathLocalization.CurrentNodeIndex,
            RejoinPathLocalization.UnreachedVertexIndex, out var _newUnreachedPathLocalisation))
        {
            // reset walked distance if the line has increased
            if (_newUnreachedPathLocalisation.CurrentNodeIndex != RejoinPathLocalization.CurrentNodeIndex)
            {
                // is different than de other method here !
                WalkedDistanceOnSegment = 0;
            }

            RejoinPathLocalization = _newUnreachedPathLocalisation;
            _distanceLeftToNextVertex =
                (RejoinPathLocalization.UnreachedVertexPosition - PositionFreeOrOnCurvedPath).magnitude;

            // assume point heading
            TargetHeading = RejoinPathLocalization.HeadingBefore;

            // move the rest of the frameDistance
            ExecuteLerpMove(RejoinPathLocalization, _leftToAdvance, _distanceLeftToNextVertex);
        }
        else
        {
            // no next segment exists on current path
            // rejoining path is ended - signal route path,
            leftFrameDistanceToWalk = _leftToAdvance;
            rejoined = true;
        }
    }

    // exactDistance will be provided when rejoining from rejoining path and there is some distance left to walk
    void AdvanceOnRoutePath(float exactDistance = -1)
    {
        ExecuteStepHeadingCorrection();

        var _distanceToWalk = exactDistance > -1
            ? exactDistance
            : FrameDistance;

        var _distanceLeftToNextVertex =
            (RoutePathLocalization.UnreachedVertexPosition - PositionFreeOrOnCurvedPath).magnitude;

        var _goesOver = _distanceLeftToNextVertex <= _distanceToWalk;

        // while within current segment there will be no change on state - just advance
        if (!_goesOver)
        {
            ExecuteLerpMove(RoutePathLocalization, _distanceToWalk, _distanceLeftToNextVertex);
            return;
        }

        // Will break motion in two: corner, after corner:

        // move to the corner
        ExecuteLerpMove(RoutePathLocalization,_distanceLeftToNextVertex, _distanceLeftToNextVertex);
        var _leftToAdvance = _distanceToWalk - _distanceLeftToNextVertex;

        // advance to next point
        if (!GameManager.Instance.ActiveRoute.PathLines.GetNextDestination(
            RoutePathLocalization.CurrentNodeIndex,
            RoutePathLocalization.UnreachedVertexIndex, out var _newUnreachedPositionInfo))
        {
            Debug.LogError("No destination could be found");
            return;
        }

        // reset walked distance if the line has increased
        if (_newUnreachedPositionInfo.CurrentNodeIndex != RoutePathLocalization.CurrentNodeIndex)
        {
            WalkedDistanceOnSegment = 0;
            if (GameManager.Instance.ActiveRoute.Points[_newUnreachedPositionInfo.CurrentNodeIndex]
                .IsAfterDiscontinuity)
            {
                GameManager.Instance.SwitchThroughHeading();
            }
        }

        RoutePathLocalization = _newUnreachedPositionInfo;
        _distanceLeftToNextVertex = (RoutePathLocalization.UnreachedVertexPosition - PositionFreeOrOnCurvedPath).magnitude;

        // assume point heading
        TargetHeading = RoutePathLocalization.HeadingBefore;

        // move the rest of the frameDistance
        ExecuteLerpMove(RoutePathLocalization,_leftToAdvance, _distanceLeftToNextVertex);
    }

    public void OnAppliedMod()
    {
        if (!IsOnRoute)
        {
            StartLNavMode(RejoinRouteMode.NextRouteNode);
        }
    }

    static void GetFirstDestinationFromNode(PathLines lines, RoutePoint node, RoutePoint[] nodes, out PathPositionInfo positionInfo)
    {
        lines.ResetOldPosition();

        var _nodeIndex = nodes.GetNodeIndex(node.ID);
        var _heading = node.Degrees; // Not sure if matters, but it's not correct

        positionInfo = new PathPositionInfo
        {
            // set destination as next node
            CurrentNodeIndex = _nodeIndex + 1,
            UnreachedVertexIndex = 0,
            HeadingBefore = _heading,
            // set position in the just passed node
            UnreachedVertexPosition = lines.ComputedLines[_nodeIndex+1].Vertexes[0] 
        };
    }

    void AdvanceFreeFlight()
    {
        ExecuteStepHeadingCorrection();
        ExecuteStepMove();
    }

    float GetWalkedDistanceLeftOnSegment()
    {
        return PositionVirtualNode.CurrentSegment.ComputedVertexLength -
               WalkedDistanceOnSegment;
    }

    /// <summary>
    /// This is called on FixedUpdate from GameManager
    /// </summary>
    public void Advance()
    {
        if (IsFreeFlight)
        {
            IndicateTargetHeading(Calculator.RHeading);
            AdvanceFreeFlight();

            // for display only
            GameManager.Instance.ActiveRoute.FindFreeFlightDirectExitScenario(out displayCenterOfTurn, out displayExitPoint, out _);

        }
        else
        {
            if (IsOnRoute)
            {
                AdvanceOnRoutePath();
            }
            else
            {
                AdvanceOnRejoinPath(out var _rejoined, out var _exitSegmentIndex, out var _leftFrameDistanceToWalk);
                if (_rejoined)
                {
                    IsOnRoute = true;
                    GameManager.Instance.ActiveRoute.OnPathRejoined();

                    if (GameManager.Instance.ActiveRoute.TransferPathToRoute(cachedExitPointFromHeading, _exitSegmentIndex,
                        out var _intersectionInfo,
                        out var _walkedDistanceOnSegment))
                    {

                        RoutePathLocalization = _intersectionInfo;
                        WalkedDistanceOnSegment = _walkedDistanceOnSegment;
                    }

                    AdvanceOnRoutePath(_leftFrameDistanceToWalk);
                }
            }
        }
    }

    static Vector2 displayCenterOfTurn;
    static Vector2 displayExitPoint;


    public void DrawGizmos()
    {
        Gizmos.color = Color.white;
        Gizmos.DrawSphere(Drawer.Instance.transform.position + displayCenterOfTurn.ToDisplay(), 0.05f);
        Gizmos.color = Color.green;
        Gizmos.DrawSphere(Drawer.Instance.transform.position + displayExitPoint.ToDisplay(), 0.05f);
    }
}