using System;
using UnityEngine;

public class Aircraft
{
    const float DeltaTime = 0.00003f;
    const float MaxTurningSpeed = 0.1f;
    public const float ForwardThreshold = 2f; // @$# this has to be in sync with the minimum turn radius 
    public const float RejoinDistance = 2.8f; 
    
    
    // position that can be on the generated curved sections of the lines
    public Vector2 PositionFreeOrOnCurvedPath { get; private set; }
    
    public Vector2 PositionFreeOrOnRouteSegment { get; private set; }
    
    public float Heading { get; private set; } // Degrees based rotation
    public float TargetHeading { get; private set; }
    public float WalkedDistanceOnSegment { get; private set; }
    public PathPositionInfo RoutePathLocalization;
    public PathPositionInfo RejoinPathLocalization;

    public PathLines TempPathLines { get; private set; }
    public bool IsFreeFlight;
    public bool IsOnRoute = true;

    public int CachedExitSegmentOfHeadingRejoinIntersection => cachedExitSegmentOfHeadingRejoinIntersection;
    public Vector2 CachedExitPointFromHeading => cachedExitPointFromHeading;

    public bool IsRejoining => !IsFreeFlight && !IsOnRoute;

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

            var nextViableNodeIndex = PositionVirtualNode.PassedNodeIndex + 1;
            while (GameManager.Instance.ActiveRoute.Points[nextViableNodeIndex].IsSkippable)
            {
                nextViableNodeIndex++;
            }
            return (GameManager.Instance.ActiveRoute.GetCartesianPosition(nextViableNodeIndex) -
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


    public void StartHeadingMode()
    {
        IsFreeFlight = true;
        IsOnRoute = false;
    }

    public enum RejoinRouteMode { Manual, NextRouteNode}

    public void StartLNavMode(RejoinRouteMode mode)
    {
        switch (mode)
        {
            case RejoinRouteMode.Manual:
                ChooseAutoRejoinMethod();
                break;
            case RejoinRouteMode.NextRouteNode:
                ComputeTempPathForNextNode();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(mode), mode, null);
        }
    }

    void ChooseAutoRejoinMethod()
    {
        if (GameManager.Instance.ActiveRoute.FindFreeFlightCloseToPathExitScenario(RejoinDistance,
            out var futurePosition, out var centerOfTurn,
            out cachedExitPointFromHeading, out cachedExitSegmentOfHeadingRejoinIntersection))
        {
            ComputeTempPathForCloseToPath(futurePosition,centerOfTurn);
        }
        else
        {
            Debug.Log("Close scenario not found, proceed to direct intersection");
            ComputeTempPathForDirectIntersection();
        }
    }
    
    // when aircraft is in HDG and user applies a MOD
    void ComputeTempPathForNextNode()
    {

        GameManager.Instance.ActiveRoute.FindFreeFlightNextNodeExitScenario(out var futurePosition, out var centerOfTurn, out cachedExitPointFromHeading,
            out cachedExitSegmentOfHeadingRejoinIntersection);
      
            //compute rejoin path

            Debug.Log("start LNAV - rejoin next node");
            TempPathLines = new PathLines();

            var tempPoints = new RoutePoint[5];
            var lastPoint = RoutePoint.ConstructFromPosition(Vector2.zero, null);
            tempPoints[0] = lastPoint;
            lastPoint = RoutePoint.ConstructFromPosition(PositionFreeOrOnCurvedPath, lastPoint);
            tempPoints[1] = lastPoint;
            lastPoint = RoutePoint.ConstructFromPosition(futurePosition, lastPoint);
            tempPoints[2] = lastPoint;
            lastPoint = RoutePoint.ConstructFromPosition(centerOfTurn, lastPoint);
            tempPoints[3] = lastPoint;
            lastPoint = RoutePoint.ConstructFromPosition(cachedExitPointFromHeading, lastPoint);
            tempPoints[4] = lastPoint;

            TempPathLines.ComputeSet(tempPoints);

            GetFirstDestinationFromNode(TempPathLines, tempPoints[1], tempPoints,
                out RejoinPathLocalization);

            IsFreeFlight = false;
    }

    void ComputeTempPathForCloseToPath(Vector2 futurePosition, Vector2 centerOfTurn)
    {
        Debug.Log("start LNAV - rejoin close path");
        TempPathLines = new PathLines();

        var tempPoints = new RoutePoint[5];
        var lastPoint = RoutePoint.ConstructFromPosition(Vector2.zero, null);
        tempPoints[0] = lastPoint;
        lastPoint = RoutePoint.ConstructFromPosition(PositionFreeOrOnCurvedPath, lastPoint);
        tempPoints[1] = lastPoint;
        lastPoint = RoutePoint.ConstructFromPosition(futurePosition, lastPoint);
        tempPoints[2] = lastPoint;
        lastPoint = RoutePoint.ConstructFromPosition(centerOfTurn, lastPoint);
        tempPoints[3] = lastPoint;
        lastPoint = RoutePoint.ConstructFromPosition(cachedExitPointFromHeading, lastPoint);
        tempPoints[4] = lastPoint;

        TempPathLines.ComputeSet(tempPoints);

        GetFirstDestinationFromNode(TempPathLines, tempPoints[1], tempPoints,
            out RejoinPathLocalization);

        IsFreeFlight = false;

    }

    // when aircraft is in heading and user switches to LNav ( and the case is straight intersection with the path )
    void ComputeTempPathForDirectIntersection()
    {
        if (GameManager.Instance.ActiveRoute.FindFreeFlightDirectExitScenario(out var centerOfTurn, out cachedExitPointFromHeading,
            out cachedExitSegmentOfHeadingRejoinIntersection))
        {
            //compute rejoin path

            Debug.Log("start LNAV - rejoin direct intersection");
            TempPathLines = new PathLines();

            var tempPoints = new RoutePoint[4];
            var lastPoint = RoutePoint.ConstructFromPosition(Vector2.zero, null);
            tempPoints[0] = lastPoint;
            lastPoint = RoutePoint.ConstructFromPosition(PositionFreeOrOnCurvedPath, lastPoint);
            tempPoints[1] = lastPoint;
            lastPoint = RoutePoint.ConstructFromPosition(centerOfTurn, lastPoint);
            tempPoints[2] = lastPoint;
            lastPoint = RoutePoint.ConstructFromPosition(cachedExitPointFromHeading, lastPoint);
            tempPoints[3] = lastPoint;

            TempPathLines.ComputeSet(tempPoints);

            GetFirstDestinationFromNode(TempPathLines, tempPoints[1], tempPoints,
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

        var distanceLeftToNextVertex =
            (RejoinPathLocalization.UnreachedVertexPosition - PositionFreeOrOnCurvedPath).magnitude;

        var goesOver = distanceLeftToNextVertex <= FrameDistance;

        // while within current segment there will be no change on state - just advance
        if (!goesOver)
        {
            ExecuteLerpMove(RejoinPathLocalization, FrameDistance, distanceLeftToNextVertex);
            return;
        }


        // Will break motion in two: corner, after corner

        // move to the corner
        ExecuteLerpMove(RejoinPathLocalization, distanceLeftToNextVertex, distanceLeftToNextVertex);
        var leftToAdvance = FrameDistance - distanceLeftToNextVertex;

        // advance to next point
        if (TempPathLines.GetNextDestination(
            RejoinPathLocalization.CurrentNodeIndex,
            RejoinPathLocalization.UnreachedVertexIndex, out var newUnreachedPathLocalisation))
        {
            // reset walked distance if the line has increased
            if (newUnreachedPathLocalisation.CurrentNodeIndex != RejoinPathLocalization.CurrentNodeIndex)
            {
                // is different than de other method here !
                WalkedDistanceOnSegment = 0;
            }

            RejoinPathLocalization = newUnreachedPathLocalisation;
            distanceLeftToNextVertex =
                (RejoinPathLocalization.UnreachedVertexPosition - PositionFreeOrOnCurvedPath).magnitude;

            // assume point heading
            TargetHeading = RejoinPathLocalization.HeadingBefore;

            // move the rest of the frameDistance
            ExecuteLerpMove(RejoinPathLocalization, leftToAdvance, distanceLeftToNextVertex);
        }
        else
        {
            // no next segment exists on current path
            // rejoining path is ended - signal route path,
            leftFrameDistanceToWalk = leftToAdvance;
            rejoined = true;
        }
    }

    // exactDistance will be provided when rejoining from rejoining path and there is some distance left to walk
    void AdvanceOnRoutePath(float exactDistance = -1)
    {
        ExecuteStepHeadingCorrection();

        var distanceToWalk = exactDistance > -1
            ? exactDistance
            : FrameDistance;

        var distanceLeftToNextVertex =
            (RoutePathLocalization.UnreachedVertexPosition - PositionFreeOrOnCurvedPath).magnitude;

        var goesOver = distanceLeftToNextVertex <= distanceToWalk;

        // while within current segment there will be no change on state - just advance
        if (!goesOver)
        {
            ExecuteLerpMove(RoutePathLocalization, distanceToWalk, distanceLeftToNextVertex);
            return;
        }

        // Will break motion in two: corner, after corner:

        // move to the corner
        ExecuteLerpMove(RoutePathLocalization,distanceLeftToNextVertex, distanceLeftToNextVertex);
        var leftToAdvance = distanceToWalk - distanceLeftToNextVertex;

        // advance to next point
        if (!GameManager.Instance.ActiveRoute.PathLines.GetNextDestination(
            RoutePathLocalization.CurrentNodeIndex,
            RoutePathLocalization.UnreachedVertexIndex, out var newUnreachedPositionInfo))
        {
            Debug.LogError("No destination could be found");
            return;
        }

        // reset walked distance if the line has increased
        if (newUnreachedPositionInfo.CurrentNodeIndex != RoutePathLocalization.CurrentNodeIndex)
        {
            WalkedDistanceOnSegment = 0;
            if (GameManager.Instance.ActiveRoute.Points[newUnreachedPositionInfo.CurrentNodeIndex]
                .IsAfterDiscontinuity)
            {
                GameManager.Instance.SwitchThroughHeading();
            }
        }

        RoutePathLocalization = newUnreachedPositionInfo;
        distanceLeftToNextVertex = (RoutePathLocalization.UnreachedVertexPosition - PositionFreeOrOnCurvedPath).magnitude;

        // assume point heading
        TargetHeading = RoutePathLocalization.HeadingBefore;

        // move the rest of the frameDistance
        ExecuteLerpMove(RoutePathLocalization,leftToAdvance, distanceLeftToNextVertex);
    }

    static void GetFirstDestinationFromNode(PathLines lines, RoutePoint node, RoutePoint[] nodes, out PathPositionInfo positionInfo)
    {
        lines.ResetOldPosition();

        var nodeIndex = nodes.GetNodeIndex(node.ID);
        var heading = node.Degrees; // Not sure if matters, but it's not correct

        positionInfo = new PathPositionInfo
        {
            // set destination as next node
            CurrentNodeIndex = nodeIndex + 1,
            UnreachedVertexIndex = 0,
            HeadingBefore = heading,
            // set position in the just passed node
            UnreachedVertexPosition = lines.ComputedLines[nodeIndex+1].Vertexes[0] 
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
                AdvanceOnRejoinPath(out var rejoined, out var exitSegmentIndex, out var leftFrameDistanceToWalk);
                if (rejoined)
                {
                    IsOnRoute = true;
                    GameManager.Instance.ActiveRoute.OnPathRejoined();

                    if (GameManager.Instance.ActiveRoute.TransferPathToRoute(cachedExitPointFromHeading, exitSegmentIndex,
                        out var intersectionInfo,
                        out var walkedDistanceOnSegment))
                    {

                        RoutePathLocalization = intersectionInfo;
                        WalkedDistanceOnSegment = walkedDistanceOnSegment;
                    }

                    AdvanceOnRoutePath(leftFrameDistanceToWalk);
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