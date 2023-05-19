using System;
using Gamelogic.Extensions;
using Navigation;
using UnityEngine;

[Serializable]
public class Aircraft : MovingActor
{
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
    public RoutePosition PositionOnRejoinPath;
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
    private Vector2 _lastFoundSegmentVertex;

    private int _lastFoundSegmentVertexIndex = 0;
    private int _lastFoundSegmentIndex = 1;
    
    protected override void Awake()
    {
        base.Awake();

        _pilot = new Pilot(Vector2.zero, Vector2.up, false);
        AircraftSpeed = Session.Settings.AirplaneInitialSpeed;
        
        _turningHeaderLine = GetComponent<LineRenderer>();
        _turningHeaderLine.positionCount = 10;
    }

    public override void SimulateTick(float deltaTime)
    {
        DrawHeadingLine();
    
        if (IsFreeFlight)
        {
            TargetHeading = Calculator.RHeading;

            _pilot.TickSteerToTargetHeading(TargetHeading);
            
            _pilot.TickAdvance();

            PositionFreeOrClosestOnRouteSegment.NMPositionOnSegment = NMPosition;
            
            CheckAdvancePointOnHDGProximity();
            // for display only
            //GameManager.Instance.ActiveRoute.FindFreeFlightDirectExitScenario(out displayCenterOfTurn, out displayExitPoint, out _);
        }
        else
        {
            var foundClosePathDestination = Session.ActiveRoute.TracedRoute.FindCloseToRouteSegmentDestination(
                Session.Settings.PilotSeekDistancePathFollow,
                out _lastFoundSegmentVertex,
                out _lastFoundSegmentVertexIndex,
                out _lastFoundSegmentIndex, 
                out var foundAtDistanceOnSegmentLine,
                out var reachedEnd,
                startFromSegmentIndex: _lastFoundSegmentIndex,
                startFromVertexIndex: _lastFoundSegmentVertexIndex,
                breakOnFistSolution: true);

            if (IsOnRoute)
            {
                if (!foundClosePathDestination)
                {
                    Debug.LogError("No Intersection Point Found");
                    IsFreeFlight = true;

                    return;
                }

                _pilot.TickSteerToPathFoundVertex(_lastFoundSegmentVertex);
                _pilot.TickAdvance();
                
                // Session.ActiveRoute.TracedRoute.FindClosestRoutePoint(
                //     lastFoundSegmentIndex,
                //     Session.PlayerAircraft.NMPosition,
                //     out PositionFreeOrClosestOnRouteSegment);

                PositionFreeOrClosestOnRouteSegment.NMPositionOnSegment = _lastFoundSegmentVertex;
                PositionFreeOrClosestOnRouteSegment.CurrentNodeIndex = _lastFoundSegmentIndex;
                PositionFreeOrClosestOnRouteSegment.NMWalkedOnCurrentSegment = foundAtDistanceOnSegmentLine;
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
                            Session.Settings.PilotSeekDistancePathFollow,
                            out _lastFoundSegmentVertex,
                            out _lastFoundSegmentVertexIndex,
                            out _lastFoundSegmentIndex,
                            out  foundAtDistanceOnSegmentLine,
                            out  reachedEnd,
                            startFromSegmentIndex: _lastFoundSegmentIndex,
                            startFromVertexIndex: _lastFoundSegmentVertexIndex,
                            breakOnFistSolution: true))
                    {
                        Debug.LogError("No Intersection Point Found");
                        IsFreeFlight = true;
                        return;
                    }
                }

                _pilot.TickSteerToPathFoundVertex(_lastFoundSegmentVertex);
            }
        }
    }
    
    

    public void Init(float aircraftSpeed, float altitude)
    {
        ResetOnActiveSet(aircraftSpeed, altitude);
    }


    private void DrawHeadingLine()
    {
        var lerp = (float)1 / _turningHeaderLine.positionCount;
        var vectorStep = Vector2.Lerp(Vector2.zero, _upwardsHeaderLineTop, lerp);

        for (int i = 0; i < _turningHeaderLine.positionCount; i++)
        {
            var rotatedStep = Quaternion.Euler(0, 0, -_pilot.CachedDisplayLastAngleDiff * i) * vectorStep;
            if (i > 0)
            {
                rotatedStep += _turningHeaderLine.GetPosition(i - 1);
            }

            _turningHeaderLine.SetPosition(i, rotatedStep);
        }

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
    public float DisplayHeadingDegrees => _pilot.DisplayHeadingDegrees;


    public void ResetOnActiveSet(float aircraftSpeed, float altitude)
    {
        AircraftSpeed = aircraftSpeed;
        
        _pilot.NMPosition = Vector2.zero;
        _pilot.HeadingDegrees = -Session.ActiveRoute.Points[1].Degrees;
        
        PositionFreeOrClosestOnRouteSegment.Reset();

        ResetSeekProgress(1, 0);

    }

    public void ResetSeekProgress(int atLine, int atVertex)
    {
        _lastFoundSegmentVertexIndex = atVertex;
        _lastFoundSegmentIndex = atLine;
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
        }
    }

    private void ChooseAutoRejoinMethod()
    {
        // if (Session.ActiveRoute.TracedRoute.FindCloseToRouteSegmentDestination(
        //         Session.Settings.RejoinDistance,
        //         out var lastFoundSegmentVertex,
        //         out var lastFoundSegmentVertexIndex,
        //         out var lastFoundSegmentIndex,
        //         out var foundAtDistanceOnSegment,
        //         out var reachedRouteEnd))
        // {
        //     ComputeTempPathForCloseToPath(futurePosition, tipOfTurn);
        // }
        //
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

    private void ComputeTempPathForCloseToPath(Vector2 futurePosition, Vector2 intersectionPoint)
    {
        Debug.Log("start LNAV - rejoin close path");
        RejoinPathLines = new TracedRoute();
        
        var tempPoints = new RoutePoint[5];
        var lastPoint = RoutePoint.ConstructFromPosition(Vector2.zero, null);
        tempPoints[0] = lastPoint;
        lastPoint = RoutePoint.ConstructFromPosition(NMPosition, lastPoint);
        tempPoints[1] = lastPoint;
        lastPoint = RoutePoint.ConstructFromPosition(futurePosition, lastPoint);
        tempPoints[2] = lastPoint;
        lastPoint = RoutePoint.ConstructFromPosition(intersectionPoint, lastPoint);
        tempPoints[3] = lastPoint;
        lastPoint = RoutePoint.ConstructFromPosition(CachedExitPointFromHeading, lastPoint);
        tempPoints[4] = lastPoint;

        RejoinPathLines.Compute(tempPoints);
        //
        // GetFirstDestinationFromNode(RejoinPathLines, tempPoints[1], tempPoints,
        //     out RejoinPathLocalization);
        
        IsFreeFlight = false;
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

   

    private void IndicateTargetHeading(float heading)
    {
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
            Gizmos.DrawWireSphere(transform.position + _lastFoundSegmentVertex.ToDisplay(), 0.12f);
        }

        Gizmos.color = Color.green;
        
        Gizmos.DrawWireSphere(transform.position + PositionFreeOrClosestOnRouteSegment.NMPositionOnSegment.ToDisplay(), 0.05f);
    }
}
