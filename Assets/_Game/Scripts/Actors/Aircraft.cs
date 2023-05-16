using System;
using Gamelogic.Extensions;
using Navigation;
using UnityEngine;

[Serializable]
public class Aircraft : MovingActor
{
    [SerializeField, ReadOnly] private float _currentTurningDegrees;
    
    /// <summary>
    /// If used for geometry should be used with '-' . see other places
    /// </summary>
    public float HeadingDegrees { get; private set; } 
    public float TargetHeading { get; private set; }
    public float WalkedDistanceOnSegment { get; private set; }
    public bool IsRejoining => !IsFreeFlight && !IsOnRoute;
    public PathLines RejoinPathLines { get; private set; }
    
    public int CachedExitSegmentOfHeadingRejoinIntersection { get; }
    public Vector2 CachedExitPointFromHeading { get; }

    // position that can be on the generated curved sections of the lines
    public Vector2 PositionFreeOrOnRouteSegment;
    
    public PathPositionInfo RoutePathLocalization;
    public PathPositionInfo RejoinPathLocalization;

    public bool IsFreeFlight;
    public bool IsOnRoute = true;
    
    private Vector2 CurrentDirection => Geometry.GetDirectionFromHeading(HeadingDegrees);
    private float FrameDistance => _speed * Session.Settings.DeltaTime * Calculator.Acceleration(); //change
    
    private Vector3 _upwardsHeaderLineTop = new(0, 1.2f, 0);
    private LineRenderer _turningHeaderLine;
    private Vector2 _pathJoinFoundVertex;
    private float _speed;

    protected override void Awake()
    {
        base.Awake();

        _turningHeaderLine = GetComponent<LineRenderer>();
        _upwardsHeaderLineTop = Vector3.up * _turningHeaderLine.GetPosition(1).magnitude;
    }

    public override void SimulateTick(float deltaTime)
    {
        DrawHeadingLine();

        if (IsFreeFlight)
        {
            IndicateTargetHeading(Calculator.RHeading);

            SimulateTickHeadingCorrection();

            // for display only
            //GameManager.Instance.ActiveRoute.FindFreeFlightDirectExitScenario(out displayCenterOfTurn, out displayExitPoint, out _);
        }
        else
        {
            var foundClosePathDestination = Session.ActiveRoute.PathLines.FindCloseToPathDestination(
                    Session.Settings.RejoinDistance,
                    out _pathJoinFoundVertex, out var exitLineIndex, out var reachedEnd);

            if (IsOnRoute)
            {
                if (!foundClosePathDestination)
                {
                    Debug.LogError("No Intersection Point Found");
                    IsFreeFlight = true;

                    return;
                }

                SteerToPathFoundVertex(deltaTime);
            }
            else
            {
                var rejoined = foundClosePathDestination;

                if (rejoined)
                {
                    IsOnRoute = true;
                    Session.ActiveRoute.OnPathRejoined(RejoinPathLines.oldPositionVertex);

                    if (Session.ActiveRoute.TransferPathToRoute(CachedExitPointFromHeading, exitLineIndex,
                            out var intersectionInfo,
                            out var walkedDistanceOnSegment))
                    {

                        RoutePathLocalization = intersectionInfo;
                        WalkedDistanceOnSegment = walkedDistanceOnSegment;
                    }

                }
                else
                {
                    if (!RejoinPathLines.FindCloseToPathDestination(
                            Session.Settings.RejoinDistance,
                            out _pathJoinFoundVertex, out var foundLineIndex, out _))
                    {
                        Debug.LogError("No Intersection Point Found");
                        IsFreeFlight = true;
                        return;
                    }
                }
                
                SteerToPathFoundVertex(deltaTime);
            }
        }

        SimulateTickMove();
    }

    public void Init(float aircraftSpeed, float altitude)
    {
        ResetOnActiveSet(aircraftSpeed, altitude);
    }
    
    // exactDistance will be provided when rejoining from rejoining path and there is some distance left to walk
    
    //private bool TickOrientToRoutePath(PathLines pathLines, float deltaTime, bool )
  //  {
    
        
            // PositionFreeOrOnCurvedPath = 
            // WalkedDistanceOnSegment +=
            // PositionFreeOrOnRouteSegment =
            // PositionFreeOrOnRouteSegment = 
       

// ---
        // {
        //     Debug.LogError("No destination could be found");
        //     return;
        // }

        // // reset walked distance if the line has increased
        // if (newUnreachedPositionInfo.CurrentNodeIndex != RoutePathLocalization.CurrentNodeIndex)
        // {
        //     WalkedDistanceOnSegment = 0;
        //     if (Session.ActiveRoute.Points[newUnreachedPositionInfo.CurrentNodeIndex]
        //         .IsAfterDiscontinuity)
        //     {
        //         UYServiceLocator.Get<GameManager>().SwitchThroughHeading();
        //     }
        // }

        //RoutePathLocalization = newUnreachedPositionInfo;
        //distanceLeftToNextVertex = (RoutePathLocalization.UnreachedVertexPosition - PositionFreeOrOnCurvedPath).magnitude;

  //  }

    private void SteerToPathFoundVertex(float deltaTime)
    {
        var difDegrees = Geometry.AngleBetween(_pathJoinFoundVertex - NMPosition, CurrentDirection);

        var lerpDirection = _currentTurningDegrees / 2f < difDegrees ? 1 : -1;
        _currentTurningDegrees += lerpDirection * 0.2f;

        HeadingDegrees += _currentTurningDegrees * deltaTime * 0.1f;
    }

    private void DrawHeadingLine()
    {
        var rotated = Quaternion.Euler(0, 0, -_currentTurningDegrees) * _upwardsHeaderLineTop;

        _turningHeaderLine.SetPosition(1, rotated);
    }

    public float ComputedDistanceLeftOnSegment
    {
        get
        {
            if (!IsFreeFlight && IsOnRoute)
            {
                return GetWalkedDistanceLeftOnSegment();
            }

            var nextViableNodeIndex = PositionVirtualNode.PassedNodeIndex + 1;
            while (Session.ActiveRoute.Points[nextViableNodeIndex].IsSkippable)
            {
                nextViableNodeIndex++;
            }
            return (Session.ActiveRoute.GetCartesianPosition(nextViableNodeIndex) -
                    PositionFreeOrOnRouteSegment).magnitude;
        }
    }

  
    public void ResetOnActiveSet(float aircraftSpeed, float altitude)
    {
        _speed = aircraftSpeed;
        NMPosition = Vector2.zero;
        PositionFreeOrOnRouteSegment = Vector2.zero;
        WalkedDistanceOnSegment = 0;

        if (Session.ActiveRoute.PathLines.GetFirstDestination(out RoutePathLocalization))
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
        // @£$
        // if (Session.ActiveRoute.FindFreeFlightCloseToPathExitScenario(_settings.RejoinDistance,
        //         out var futurePosition, out var tipOfTurn,
        //         out _cachedExitPointFromHeading, out _cachedExitSegmentOfHeadingRejoinIntersection))
        // {
        //     ComputeTempPathForCloseToPath(futurePosition, tipOfTurn);
        // }
        // else if (GameManager.Instance.ActiveRoute.FindFreeFlightDirectExitScenario(out tipOfTurn,
        //              out _cachedExitPointFromHeading,
        //              out _cachedExitSegmentOfHeadingRejoinIntersection))
        // {
        //     ComputeRejoinPathForDirectIntersection(tipOfTurn);
        // }
        // else
        // {
        //     Debug.LogError("No Intersection Point Found");
        // }
        //
        // _displayExitPoint = _cachedExitPointFromHeading;
        // _displayCenterOfTurn = tipOfTurn;
    }

    // when aircraft is in HDG and user applies a MOD
    private void ComputeTempPathForNextNode()
    {

        // £@$
        // Session.ActiveRoute.FindFreeFlightNextNodeExitScenario(out var futurePosition, out var centerOfTurn, out _cachedExitPointFromHeading,
        //     out _cachedExitSegmentOfHeadingRejoinIntersection);
        //
        //     //compute rejoin path
        //
        //     Debug.Log("start LNAV - rejoin next node");
        //     RejoinPathLines = new PathLines(_settings.DrawerUnitLength);
        //
        //     var tempPoints = new RoutePoint[5];
        //     var lastPoint = RoutePoint.ConstructFromPosition(Vector2.zero, null);
        //     tempPoints[0] = lastPoint;
        //     lastPoint = RoutePoint.ConstructFromPosition(PositionFreeOrOnCurvedPath, lastPoint);
        //     tempPoints[1] = lastPoint;
        //     lastPoint = RoutePoint.ConstructFromPosition(futurePosition, lastPoint);
        //     tempPoints[2] = lastPoint;
        //     lastPoint = RoutePoint.ConstructFromPosition(centerOfTurn, lastPoint);
        //     tempPoints[3] = lastPoint;
        //     lastPoint = RoutePoint.ConstructFromPosition(_cachedExitPointFromHeading, lastPoint);
        //     tempPoints[4] = lastPoint;
        //
        //     RejoinPathLines.ComputeSet(tempPoints);
        //
        //     GetFirstDestinationFromNode(RejoinPathLines, tempPoints[1], tempPoints,
        //         out RejoinPathLocalization);
        //
        //     IsFreeFlight = false;
    }

    private void ComputeTempPathForCloseToPath(Vector2 futurePosition, Vector2 tipOfTurn)
    {
        Debug.Log("start LNAV - rejoin close path");
        RejoinPathLines = new PathLines(Session.Settings.DrawerUnitLength);

        var tempPoints = new RoutePoint[5];
        var lastPoint = RoutePoint.ConstructFromPosition(Vector2.zero, null);
        tempPoints[0] = lastPoint;
        lastPoint = RoutePoint.ConstructFromPosition(NMPosition, lastPoint);
        tempPoints[1] = lastPoint;
        lastPoint = RoutePoint.ConstructFromPosition(futurePosition, lastPoint);
        tempPoints[2] = lastPoint;
        lastPoint = RoutePoint.ConstructFromPosition(tipOfTurn, lastPoint);
        tempPoints[3] = lastPoint;
        lastPoint = RoutePoint.ConstructFromPosition(CachedExitPointFromHeading, lastPoint);
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
            RejoinPathLines = new PathLines(Session.Settings.DrawerUnitLength);

            var tempPoints = new RoutePoint[4];
            var lastPoint = RoutePoint.ConstructFromPosition(Vector2.zero, null);
            tempPoints[0] = lastPoint;
            lastPoint = RoutePoint.ConstructFromPosition(NMPosition, lastPoint);
            tempPoints[1] = lastPoint;
            lastPoint = RoutePoint.ConstructFromPosition(tipOfTurn, lastPoint);
            tempPoints[2] = lastPoint;
            lastPoint = RoutePoint.ConstructFromPosition(CachedExitPointFromHeading, lastPoint);
            tempPoints[3] = lastPoint;

            RejoinPathLines.ComputeSet(tempPoints);

            GetFirstDestinationFromNode(RejoinPathLines, tempPoints[1], tempPoints,
                out RejoinPathLocalization);

            IsFreeFlight = false;
    }


    // on free flight
    private void SimulateTickMove()
    {
        NMPosition += CurrentDirection * FrameDistance;
        PositionFreeOrOnRouteSegment = NMPosition;
        
        CheckAdvancePointOnHDGProximity();
    }

    // private void ExecuteLerpMove(PathPositionInfo pathLocalisation ,float stepDistance, float distanceLeftToNextVertex)
    // {
    //     if (distanceLeftToNextVertex > 0)
    //     {
    //         PositionFreeOrOnCurvedPath = Vector2.Lerp(PositionFreeOrOnCurvedPath,
    //             pathLocalisation.UnreachedVertexPosition, stepDistance / distanceLeftToNextVertex);
    //     }
    //
    //     if (IsOnRoute)
    //     {
    //         WalkedDistanceOnSegment += stepDistance;
    //         PositionFreeOrOnRouteSegment = Vector2.Lerp(
    //             PositionVirtualNode.CurrentSegment.StartPosition,
    //             PositionVirtualNode.CurrentSegment.EndPosition,
    //             WalkedDistanceOnSegment / PositionVirtualNode.GetNodeTo.Distance);
    //     }
    //     else
    //     {
    //         PositionFreeOrOnRouteSegment = PositionFreeOrOnCurvedPath;
    //     }
    // }

    // @£$
    private void SimulateTickHeadingCorrection()
    {
        if (Math.Abs(TargetHeading - HeadingDegrees) > 0.01f)
        {
            HeadingDegrees = Mathf.MoveTowardsAngle(HeadingDegrees, TargetHeading, Session.Settings.MaxTurningSpeedPerUnitLength);
        }
    }

    private void IndicateTargetHeading(float heading)
    {
        TargetHeading = heading;
    }



    private void CheckAdvancePointOnHDGProximity()
    {
        for (int i = PositionVirtualNode.PassedNodeIndex+1; i < Session.ActiveRoute.Points.Length; i++)
        {
            var nodePosition = Session.ActiveRoute.Points[i].CartesianPosition;
            if (Vector2.Distance(nodePosition, PositionFreeOrOnRouteSegment) <= Session.Settings.HGDAutoNextPointDistance)
            {
                if (!Session.ActiveRoute.PathLines.GetNextDestination(
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
    }

    private float GetWalkedDistanceLeftOnSegment()
    {
        return PositionVirtualNode.CurrentSegment.ComputedVertexLength -
               WalkedDistanceOnSegment;
    }
    private static Vector2 _displayCenterOfTurn;
    private static Vector2 _displayExitPoint;

    private void OnDrawGizmos()
    {
        if (!Application.isPlaying)
        {
            return;
        }
        
        if (!IsFreeFlight)
        {
            Gizmos.DrawSphere(transform.position + _pathJoinFoundVertex.ToDisplay(), 0.2f);
        }

        Gizmos.color = Color.white;
        Gizmos.DrawSphere(transform.position + _displayCenterOfTurn.ToDisplay(), 0.05f);
        Gizmos.color = Color.green;
        Gizmos.DrawSphere(transform.position + _displayExitPoint.ToDisplay(), 0.05f);
    }
}
