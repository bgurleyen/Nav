using System;
using Gamelogic.Extensions;
using Navigation;
using UnityEngine;

[Serializable]
public class Aircraft : MovingActor
{
    [SerializeField, ReadOnly] private float _currentTurningDegrees;
    private Pilot _pilot;
    
    /// <summary>
    /// If used for geometry should be used with '-' . see other places
    /// </summary>

    public float TargetHeading { get; private set; }
    public bool IsRejoining => !IsFreeFlight && !IsOnRoute;
    public TracedRoute RejoinPathLines { get; private set; }

    public int CachedExitSegmentOfHeadingRejoinIntersection { get; }
    public Vector2 CachedExitPointFromHeading { get; }

    // position that can be on the straight segment
    public RoutePosition PositionFreeOrClosestOnRouteSegment;
    [ReadOnly]
    public float AircraftSpeed;

    public override Vector2 NMPosition => _pilot.NMPosition;
    
    // public PathPositionInfo RoutePathLocalization;
    // public PathPositionInfo RejoinPathLocalization;

    public bool IsFreeFlight;
    public bool IsOnRoute = true;
    public bool IsJoining => !IsFreeFlight && !IsOnRoute;


    private Vector3 _upwardsHeaderLineTop = new(0, 1.2f, 0);
    private LineRenderer _turningHeaderLine;
    private Vector2 _segmentPathJoinFoundVertex;

    int lastFoundVertexIndex = 0;
    int lastFoundLineIndex = 1;
    
    protected override void Awake()
    {
        base.Awake();

        _pilot = new Pilot(Vector2.zero, Vector2.up, false);
        AircraftSpeed = Session.Settings.AirplaneInitialSpeed;
        
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
            
            _pilot.TickAdvance();

            PositionFreeOrClosestOnRouteSegment.NMPositionOnSegment = NMPosition;
            
            CheckAdvancePointOnHDGProximity();
            // for display only
            //GameManager.Instance.ActiveRoute.FindFreeFlightDirectExitScenario(out displayCenterOfTurn, out displayExitPoint, out _);
        }
        else
        {
            var foundClosePathDestination = Session.ActiveRoute.TracedRoute.FindCloseToRouteSegmentDestination(
                out _segmentPathJoinFoundVertex,
                out lastFoundVertexIndex,
                out lastFoundLineIndex, 
                out var foundAtDistanceOnSegmentLine,
                out var reachedEnd,
                startFromSegmentIndex: lastFoundLineIndex,
                startFromVertexIndex: lastFoundVertexIndex,
                breakOnFistSolution: true);

            if (IsOnRoute)
            {
                if (!foundClosePathDestination)
                {
                    Debug.LogError("No Intersection Point Found");
                    IsFreeFlight = true;

                    return;
                }

                _pilot.TickSteerToPathFoundVertex(_segmentPathJoinFoundVertex);
                _pilot.TickAdvance();
                
                Session.ActiveRoute.TracedRoute.FindClosestRoutePoint(
                    lastFoundLineIndex,
                    Session.PlayerAircraft.NMPosition,
                    out PositionFreeOrClosestOnRouteSegment);

                PositionFreeOrClosestOnRouteSegment.CurrentNodeIndex = lastFoundLineIndex;

            }
            else
            {
                var rejoined = foundClosePathDestination;

                if (rejoined)
                {
                    // IsOnRoute = true;
                    // Session.ActiveRoute.OnPathRejoined(RejoinPathLines.oldPositionVertex);
                    //
                    // if (Session.ActiveRoute.TransferPathToRoute(CachedExitPointFromHeading, exitLineIndex,
                    //         out var intersectionInfo,
                    //         out var walkedDistanceOnSegment))
                    // {
                    //
                    //     RoutePathLocalization = intersectionInfo;
                    // }
                    //
                    // Session.ActiveRoute.TracedRoute.FindClosestRoutePoint(
                    //                     exitLineIndex,
                    //                     Session.PlayerAircraft.NMPosition,
                    //                     out PositionFreeOrClosestOnRouteSegment);
                }
                else
                {
                    if (!RejoinPathLines.FindCloseToRouteSegmentDestination(
                            out _segmentPathJoinFoundVertex,
                            out lastFoundVertexIndex,
                            out lastFoundLineIndex,
                            out  foundAtDistanceOnSegmentLine,
                            out  reachedEnd,
                            startFromSegmentIndex: lastFoundLineIndex,
                            startFromVertexIndex: lastFoundVertexIndex,
                            breakOnFistSolution: true))
                    {
                        Debug.LogError("No Intersection Point Found");
                        IsFreeFlight = true;
                        return;
                    }
                }

                _pilot.TickSteerToPathFoundVertex(_segmentPathJoinFoundVertex);
            }
        }
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

   

    private void DrawHeadingLine()
    {
        var rotated = Quaternion.Euler(0, 0, -_currentTurningDegrees) * _upwardsHeaderLineTop;

        _turningHeaderLine.SetPosition(1, rotated);
    }

    /// <summary>
    /// Distance to show to the next Point
    /// </summary>
    public float ComputedDistanceLeftOnSegment
    {
        get
        {
            if (IsOnRoute)
            {
                return PositionVirtualNode.CurrentTracedLine.LinkedPoint.Distance - PositionFreeOrClosestOnRouteSegment.NMWalkedOnCurrentSegment;
            }

            var nextViableNodeIndex = PositionVirtualNode.PassedNodeIndex + 1;
            while (Session.ActiveRoute.Points[nextViableNodeIndex].IsSkippable)
            {
                nextViableNodeIndex++;
            }

            return (Session.ActiveRoute.GetCartesianPosition(nextViableNodeIndex) -
                    PositionFreeOrClosestOnRouteSegment.NMPositionOnSegment).magnitude;
        }
    }

    public float HeadingDegrees => _pilot.HeadingDegrees;


    public void ResetOnActiveSet(float aircraftSpeed, float altitude)
    {
        AircraftSpeed = aircraftSpeed;
        _pilot.NMPosition = Vector2.zero;
        PositionFreeOrClosestOnRouteSegment.Reset();

        ResetSeekProgress(1, 0);

        _pilot.HeadingDegrees = Session.ActiveRoute.Points[1].Degrees;
    }

    public void ResetSeekProgress(int atLine, int atVertex)
    {
        lastFoundVertexIndex = atVertex;
        lastFoundLineIndex = atLine;
    }

    public void StartHeadingMode()
    {
        IsFreeFlight = true;
        IsOnRoute = false;
    }

    public enum RejoinRouteMode
    {
        Manual,
        NextRouteNode
    }

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
        // @£$
        // RejoinPathLines = new PathLines(Session.Settings.DrawerUnitLength);
        //
        // var tempPoints = new RoutePoint[5];
        // var lastPoint = RoutePoint.ConstructFromPosition(Vector2.zero, null);
        // tempPoints[0] = lastPoint;
        // lastPoint = RoutePoint.ConstructFromPosition(NMPosition, lastPoint);
        // tempPoints[1] = lastPoint;
        // lastPoint = RoutePoint.ConstructFromPosition(futurePosition, lastPoint);
        // tempPoints[2] = lastPoint;
        // lastPoint = RoutePoint.ConstructFromPosition(tipOfTurn, lastPoint);
        // tempPoints[3] = lastPoint;
        // lastPoint = RoutePoint.ConstructFromPosition(CachedExitPointFromHeading, lastPoint);
        // tempPoints[4] = lastPoint;

        // RejoinPathLines.ComputeSet(tempPoints);
        //
        // GetFirstDestinationFromNode(RejoinPathLines, tempPoints[1], tempPoints,
        //     out RejoinPathLocalization);
        //
        // IsFreeFlight = false;

    }

    // when aircraft is in heading and user switches to LNav ( and the case is straight intersection with the path )
    private void ComputeRejoinPathForDirectIntersection(Vector2 tipOfTurn)
    {
        // //compute rejoin path
        // Debug.Log("start LNAV - rejoin direct intersection");
        // RejoinPathLines = new PathLines(Session.Settings.DrawerUnitLength);
        //
        // var tempPoints = new RoutePoint[4];
        // var lastPoint = RoutePoint.ConstructFromPosition(Vector2.zero, null);
        // tempPoints[0] = lastPoint;
        // lastPoint = RoutePoint.ConstructFromPosition(NMPosition, lastPoint);
        // tempPoints[1] = lastPoint;
        // lastPoint = RoutePoint.ConstructFromPosition(tipOfTurn, lastPoint);
        // tempPoints[2] = lastPoint;
        // lastPoint = RoutePoint.ConstructFromPosition(CachedExitPointFromHeading, lastPoint);
        // tempPoints[3] = lastPoint;
        //
        // RejoinPathLines.ComputeSet(tempPoints);
        //
        // GetFirstDestinationFromNode(RejoinPathLines, tempPoints[1], tempPoints,
        //     out RejoinPathLocalization);
        //
        // IsFreeFlight = false;
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
        if (Math.Abs(TargetHeading - _pilot.HeadingDegrees) > 0.01f)
        {
            _pilot.HeadingDegrees = Mathf.MoveTowardsAngle(_pilot.HeadingDegrees, TargetHeading,
                Session.Settings.MaxTurningSpeedPerUnitLength);
        }
    }

    private void IndicateTargetHeading(float heading)
    {
        TargetHeading = heading;
    }



    private void CheckAdvancePointOnHDGProximity()
    {
        for (int i = PositionVirtualNode.PassedNodeIndex + 1; i < Session.ActiveRoute.Points.Length; i++)
        {
            var nodePosition = Session.ActiveRoute.Points[i].CartesianPosition;
            if (Vector2.Distance(nodePosition, PositionFreeOrClosestOnRouteSegment.NMPositionOnSegment) <=
                Session.Settings.HGDAutoNextPointDistance)
            {
                // if (!Session.ActiveRoute.TracedRoute.GetNextDestination(
                //         i + 1,
                //         0, out var newUnreachedPositionInfo))
                // {
                //     Debug.LogError("No destination could be found");
                //     return;
                // }
                //
                // RoutePathLocalization = newUnreachedPositionInfo;
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        if (!IsFreeFlight)
        {
            Gizmos.DrawWireSphere(transform.position + _segmentPathJoinFoundVertex.ToDisplay(), 0.12f);
        }

        Gizmos.color = Color.green;
        
        Gizmos.DrawWireSphere(transform.position + PositionFreeOrClosestOnRouteSegment.NMPositionOnSegment.ToDisplay(), 0.05f);
    }
}
