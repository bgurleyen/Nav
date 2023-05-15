using System;
using Legacy;
using UnityEngine;

[Serializable]
public class Aircraft
{
    private GameSettingsScriptableObject _settings;
    public Aircraft(GameConfigScriptableObject gameConfig)
    {
        _settings = gameConfig.Settings;
    }
    
    // position that can be on the generated curved sections of the lines
    public Vector2 PositionFreeOrOnCurvedPath;

    public Vector2 PositionFreeOrOnRouteSegment;
    
    /// <summary>
    /// If used for geometry should be used with '-' . see other places
    /// </summary>
    public float HeadingDegrees { get; private set; } 
    public float TargetHeading { get; private set; }
    public float WalkedDistanceOnSegment { get; private set; }
    public PathPositionInfo RoutePathLocalization;
    public PathPositionInfo RejoinPathLocalization;

    public PathLines RejoinPathLines { get; private set; }
    public bool IsFreeFlight;
    public bool IsOnRoute = true;

    public int CachedExitSegmentOfHeadingRejoinIntersection => _cachedExitSegmentOfHeadingRejoinIntersection;
    public Vector2 CachedExitPointFromHeading => _cachedExitPointFromHeading;

    public bool IsRejoining => !IsFreeFlight && !IsOnRoute;

    private int _cachedExitSegmentOfHeadingRejoinIntersection;
    private Vector2 _cachedExitPointFromHeading;

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

    private Vector2 CurrentDirection => Geometry.GetDirectionFromHeading(HeadingDegrees);
    private float FrameDistance => _speed * _settings.DeltaTime * Calculator.Acceleration(); //change

    private float _speed;

    public void ResetOnActiveSet(float aircraftSpeed, float altitude)
    {
        _speed = aircraftSpeed;
        PositionFreeOrOnCurvedPath = Vector2.zero;
        PositionFreeOrOnRouteSegment = Vector2.zero;
        WalkedDistanceOnSegment = 0;

        if (GameManager.Instance.ActiveRoute.PathLines.GetFirstDestination(out RoutePathLocalization))
        {
            HeadingDegrees = RoutePathLocalization.HeadingBefore;
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
            // when needed rejoin: ex. after apply MOD
            case RejoinRouteMode.NextRouteNode:
                ComputeTempPathForNextNode();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(mode), mode, null);
        }
    }

    private void ChooseAutoRejoinMethod()
    {
        if (GameManager.Instance.ActiveRoute.FindFreeFlightCloseToPathExitScenario(_settings.RejoinDistance,
                out var futurePosition, out var tipOfTurn,
                out _cachedExitPointFromHeading, out _cachedExitSegmentOfHeadingRejoinIntersection))
        {
            ComputeTempPathForCloseToPath(futurePosition, tipOfTurn);
        }
        else if (GameManager.Instance.ActiveRoute.FindFreeFlightDirectExitScenario(out tipOfTurn,
                     out _cachedExitPointFromHeading,
                     out _cachedExitSegmentOfHeadingRejoinIntersection))
        {
            ComputeRejoinPathForDirectIntersection(tipOfTurn);
        }
        else
        {
            Debug.LogError("No Intersection Point Found");
        }

        _displayExitPoint = _cachedExitPointFromHeading;
        _displayCenterOfTurn = tipOfTurn;
    }

    // when aircraft is in HDG and user applies a MOD
    private void ComputeTempPathForNextNode()
    {

        GameManager.Instance.ActiveRoute.FindFreeFlightNextNodeExitScenario(out var futurePosition, out var centerOfTurn, out _cachedExitPointFromHeading,
            out _cachedExitSegmentOfHeadingRejoinIntersection);
      
            //compute rejoin path

            Debug.Log("start LNAV - rejoin next node");
            RejoinPathLines = new PathLines(_settings.DrawerUnitLength);

            var tempPoints = new RoutePoint[5];
            var lastPoint = RoutePoint.ConstructFromPosition(Vector2.zero, null);
            tempPoints[0] = lastPoint;
            lastPoint = RoutePoint.ConstructFromPosition(PositionFreeOrOnCurvedPath, lastPoint);
            tempPoints[1] = lastPoint;
            lastPoint = RoutePoint.ConstructFromPosition(futurePosition, lastPoint);
            tempPoints[2] = lastPoint;
            lastPoint = RoutePoint.ConstructFromPosition(centerOfTurn, lastPoint);
            tempPoints[3] = lastPoint;
            lastPoint = RoutePoint.ConstructFromPosition(_cachedExitPointFromHeading, lastPoint);
            tempPoints[4] = lastPoint;

            RejoinPathLines.ComputeSet(tempPoints);

            GetFirstDestinationFromNode(RejoinPathLines, tempPoints[1], tempPoints,
                out RejoinPathLocalization);

            IsFreeFlight = false;
    }

    private void ComputeTempPathForCloseToPath(Vector2 futurePosition, Vector2 tipOfTurn)
    {
        Debug.Log("start LNAV - rejoin close path");
        RejoinPathLines = new PathLines(_settings.DrawerUnitLength);

        var tempPoints = new RoutePoint[5];
        var lastPoint = RoutePoint.ConstructFromPosition(Vector2.zero, null);
        tempPoints[0] = lastPoint;
        lastPoint = RoutePoint.ConstructFromPosition(PositionFreeOrOnCurvedPath, lastPoint);
        tempPoints[1] = lastPoint;
        lastPoint = RoutePoint.ConstructFromPosition(futurePosition, lastPoint);
        tempPoints[2] = lastPoint;
        lastPoint = RoutePoint.ConstructFromPosition(tipOfTurn, lastPoint);
        tempPoints[3] = lastPoint;
        lastPoint = RoutePoint.ConstructFromPosition(_cachedExitPointFromHeading, lastPoint);
        tempPoints[4] = lastPoint;

        RejoinPathLines.ComputeSet(tempPoints);

        GetFirstDestinationFromNode(RejoinPathLines, tempPoints[1], tempPoints,
            out RejoinPathLocalization);

        IsFreeFlight = false;

    }

    // when aircraft is in heading and user switches to LNav ( and the case is straight intersection with the path )
    private void ComputeRejoinPathForDirectIntersection(Vector2 tipOfTurn)
    {
            //compute rejoin path
            Debug.Log("start LNAV - rejoin direct intersection");
            RejoinPathLines = new PathLines(_settings.DrawerUnitLength);

            var tempPoints = new RoutePoint[4];
            var lastPoint = RoutePoint.ConstructFromPosition(Vector2.zero, null);
            tempPoints[0] = lastPoint;
            lastPoint = RoutePoint.ConstructFromPosition(PositionFreeOrOnCurvedPath, lastPoint);
            tempPoints[1] = lastPoint;
            lastPoint = RoutePoint.ConstructFromPosition(tipOfTurn, lastPoint);
            tempPoints[2] = lastPoint;
            lastPoint = RoutePoint.ConstructFromPosition(_cachedExitPointFromHeading, lastPoint);
            tempPoints[3] = lastPoint;

            RejoinPathLines.ComputeSet(tempPoints);

            GetFirstDestinationFromNode(RejoinPathLines, tempPoints[1], tempPoints,
                out RejoinPathLocalization);

            IsFreeFlight = false;
       
    }


    // on free flight
    private void ExecuteStepMove()
    {
        PositionFreeOrOnCurvedPath += CurrentDirection * FrameDistance;
        PositionFreeOrOnRouteSegment = PositionFreeOrOnCurvedPath;
        
        CheckAdvancePointOnHDGProximity();
    }

    private void ExecuteLerpMove(PathPositionInfo pathLocalisation ,float stepDistance, float distanceLeftToNextVertex)
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

    private void ExecuteStepHeadingCorrection()
    {
        if (Math.Abs(TargetHeading - HeadingDegrees) > 0.01f)
        {
            HeadingDegrees = Mathf.MoveTowardsAngle(HeadingDegrees, TargetHeading, _settings.MaxTurningSpeedPerUnitLength);
        }
    }

    private void IndicateTargetHeading(float heading)
    {
        TargetHeading = heading;
    }

    private void AdvanceOnRejoinPath(out bool rejoined,out int exitSegmentIndex, out float leftFrameDistanceToWalk)
    {
        exitSegmentIndex = _cachedExitSegmentOfHeadingRejoinIntersection;
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
        if (RejoinPathLines.GetNextDestination(
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
    private void AdvanceOnRoutePath(float exactDistance = -1)
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

        // 1: move to the corner
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

    private void CheckAdvancePointOnHDGProximity()
    {
        for (int i = PositionVirtualNode.PassedNodeIndex+1; i < GameManager.Instance.ActiveRoute.Points.Length; i++)
        {
            var nodePosition = GameManager.Instance.ActiveRoute.Points[i].CartesianPosition;
            if (Vector2.Distance(nodePosition, PositionFreeOrOnRouteSegment) <= _settings.HGDAutoNextPointDistance)
            {
                if (!GameManager.Instance.ActiveRoute.PathLines.GetNextDestination(
                        i+1,
                        0, out var newUnreachedPositionInfo))
                {
                    Debug.LogError("No destination could be found");
                    return;
                }

                WalkedDistanceOnSegment = 0;

                RoutePathLocalization = newUnreachedPositionInfo;
            }
        }
    }

    private static void GetFirstDestinationFromNode(PathLines lines, RoutePoint node, RoutePoint[] nodes, out PathPositionInfo positionInfo)
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

    private void AdvanceFreeFlight()
    {
        ExecuteStepHeadingCorrection();
        ExecuteStepMove();
    }

    private float GetWalkedDistanceLeftOnSegment()
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
            //GameManager.Instance.ActiveRoute.FindFreeFlightDirectExitScenario(out displayCenterOfTurn, out displayExitPoint, out _);

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
                    GameManager.Instance.ActiveRoute.OnPathRejoined(RejoinPathLines.oldPositionVertex);

                    if (GameManager.Instance.ActiveRoute.TransferPathToRoute(_cachedExitPointFromHeading, exitSegmentIndex,
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

    private static Vector2 _displayCenterOfTurn;
    private static Vector2 _displayExitPoint;


    public void DrawGizmos()
    {
        Gizmos.color = Color.white;
        Gizmos.DrawSphere(Drawer.Instance.transform.position + _displayCenterOfTurn.ToDisplay(), 0.05f);
        Gizmos.color = Color.green;
        Gizmos.DrawSphere(Drawer.Instance.transform.position + _displayExitPoint.ToDisplay(), 0.05f);
    }
}
