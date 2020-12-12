using System;
using UnityEngine;

public class Aircraft
{
    const float DeltaTime = 0.00003f;
    const float MaxTurningSpeed = 1f;
    public const float ForwardThreshold = 1.7f;
    
    // position that can be on the generated curved sections of the lines
    public Vector2 PositionFreeOrOnCurvedPath { get; private set; }
    
    public Vector2 PositionFreeOrOnRouteSegment { get; private set; }
    
    public float Heading { get; private set; } // Degrees based rotation
    public float TargetHeading { get; private set; }
    public float WalkedDistanceOnSegment { get; private set; }
    public PathPositionInfo RoutePathLocalization;
    public PathPositionInfo RejoinPathLocalization;

    PathLines tempPath;

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

    public void StartLNavMode()
    {
        if (GameManager.Instance.ActiveRoute.FindFreeFlightDirectExitScenario(out centerOfTurn, out exitPoint,
            out var _exitSegmentIndex))
        {
            //compute rejoin path

            IsFreeFlight = false;


            // if (GameManager.Instance.ActiveRoute.LinkToRoute(centerOfTurn, _exitSegmentIndex, out var _intersectionInfo,
            //     out var _distanceUntilLineIntersection))
            // {
            //     IsFreeFlight = false;
            //
            //     RoutePathLocalization = _intersectionInfo;
            //     WalkedDistanceOnSegment = _distanceUntilLineIntersection;
            // }
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

        // if on route, synchronize with the position of the straight segments
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

    void AdvanceOnRejoinPath(out bool rejoined, out float leftFrameDistanceToWalk)
    {
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
        if (tempPath.GetNextDestination(
            RejoinPathLocalization.CurrentNodeIndex,
            RejoinPathLocalization.UnreachedVertexIndex, out var _newUnreachedVertex))
        {
            // reset walked distance if the line has increased
            if (_newUnreachedVertex.CurrentNodeIndex != RejoinPathLocalization.CurrentNodeIndex)
            {
                WalkedDistanceOnSegment = 0;
                if (GameManager.Instance.ActiveRoute.Points[_newUnreachedVertex.CurrentNodeIndex].IsAfterDiscontinuity)
                {
                    GameManager.Instance.SwitchThroughHeading();
                }
            }

            RoutePathLocalization = _newUnreachedVertex;
            _distanceLeftToNextVertex =
                (RoutePathLocalization.UnreachedVertexPosition - PositionFreeOrOnCurvedPath).magnitude;

            // assume point heading
            TargetHeading = RoutePathLocalization.HeadingBefore;

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
        // update path relation as just left from the just added position
        if (!IsOnRoute)
        {
            var _lastAddedPositionNode = GameManager.Instance.ActiveRoute.LastAddedPositionNode;
            
            if (GetFirstDestinationFromNode(_lastAddedPositionNode, out var _newUnreachedVertex) )
            {
                RoutePathLocalization = _newUnreachedVertex;
            }
        }
    }

    static bool GetFirstDestinationFromNode(RoutePoint node, out PathPositionInfo positionInfo)
    {
        GameManager.Instance.ActiveRoute.PathLines.ResetOldPosition();

        var _currentNodeIndex = GameManager.Instance.ActiveRoute.GetIndex(node.ID);
        var _heading = node.Degrees; // Not sure if matters, but it's not correct

        positionInfo = new PathPositionInfo
        {
            CurrentNodeIndex = _currentNodeIndex + 1,
            UnreachedVertexIndex = 0,
            HeadingBefore = _heading,
            UnreachedVertexPosition = node.CartesianPosition
        };

        return true;
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
            GameManager.Instance.ActiveRoute.FindFreeFlightDirectExitScenario(out centerOfTurn, out exitPoint, out _);
        }
        else
        {
            if (IsOnRoute)
            {
                AdvanceOnRoutePath();
            }
            else
            {
                AdvanceOnRejoinPath(out var _rejoined, out var _leftFrameDistanceToWalk);
                if (_rejoined)
                {
                    IsOnRoute = true;
                    GameManager.Instance.ActiveRoute.OnPathRejoined();
                    
                    AdvanceOnRoutePath(_leftFrameDistanceToWalk);
                }
            }
        }
    }

    static Vector2 centerOfTurn;
    static Vector2 exitPoint;


    public void DrawGizmos()
    {
        Gizmos.color = Color.white;
        Gizmos.DrawSphere(Drawer.Instance.transform.position + centerOfTurn.ToDisplay(), 0.05f);
        Gizmos.color = Color.green;
        Gizmos.DrawSphere(Drawer.Instance.transform.position + exitPoint.ToDisplay(), 0.05f);
    }
}