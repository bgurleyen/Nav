using System;
using Gamelogic.Extensions;
using Navigation;
using UnityEngine;

[Serializable]
public class Aircraft : MovingActor
{
    public float NMWalkedOnCurrentSegment;
    public int CurrentSegmentIndex;

    /// <summary>
    /// If used for geometry should be used with '-' . see other places
    /// </summary>
    public float TargetHeading { get; private set; }

    public int CachedExitSegmentOfHeadingRejoinIntersection { get; }
    public Vector2 CachedExitPointFromHeading { get; }

    [ReadOnly]
    public float Speed;

    public override Vector2 NMPosition => _pilot.NMPosition;
    
    public bool IsOnRoute = true;
    public bool IsJoining = false;

    private Pilot _pilot;

    private Vector3 _upwardsHeaderLineTop = new(0, 1.2f, 0);
    private LineRenderer _turningHeaderLine;
    
    private Vector2 _lastFoundSegmentVertex;
    private int _lastFoundSegmentVertexIndex;
    private int _lastFoundSegmentIndex = 1;
    
    protected override void Awake()
    {
        base.Awake();

        _pilot = new Pilot(Vector2.zero, Vector2.up, false);
        
        _turningHeaderLine = GetComponent<LineRenderer>();
        _turningHeaderLine.positionCount = 10;
    }

    private void Start()
    {
        Speed = Session.Settings.AirplaneDesignSpeed;
    }

    public void ResetPosition()
    {
        _pilot.NMPosition = Vector2.zero;
        NMWalkedOnCurrentSegment = 0;
    }

    public override void SimulateTick()
    {
        DrawHeadingLine();

        if (Session.State.LNAV)
        {
            var foundClosePathDestination = Session.ActiveRoute.TracedRoute.FindCloseToRouteSegmentDestination(
                Session.Settings.PilotSeekDistancePathFollow,
                out _lastFoundSegmentVertex,
                out _lastFoundSegmentVertexIndex,
                out _lastFoundSegmentIndex,
                out var foundAtDistanceOnSegment,
                out var reachedEnd,
                startFromSegmentIndex: _lastFoundSegmentIndex,
                startFromVertexIndex: _lastFoundSegmentVertexIndex,
                breakOnFistSolution: true);

            if (IsOnRoute)
            {
                if (!foundClosePathDestination)
                {
                    Debug.LogError("No Intersection Point Found");
                    Session.State.AutoSetHDG(true);

                    return;
                }

                _pilot.TickSteerToPathFoundVertex(_lastFoundSegmentVertex, out _);
                _pilot.TickAdvance();

                // Session.ActiveRoute.TracedRoute.FindClosestRoutePoint(
                //     lastFoundSegmentIndex,
                //     Session.PlayerAircraft.NMPosition,
                //     out PositionFreeOrClosestOnRouteSegment);

                CurrentSegmentIndex = _lastFoundSegmentIndex;
                NMWalkedOnCurrentSegment = foundAtDistanceOnSegment;
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


                _pilot.TickSteerToPathFoundVertex(_lastFoundSegmentVertex, out _);
            }
        }
        else
        {
            TargetHeading = Calculator.RHeading;

            _pilot.TickSteerToTargetHeading(TargetHeading);

            _pilot.TickAdvance();

            CheckAdvancePointOnHDGProximity();
            // for display only
            //GameManager.Instance.ActiveRoute.FindFreeFlightDirectExitScenario(out displayCenterOfTurn, out displayExitPoint, out _);
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

        var clampedAngleDiff = Mathf.Clamp(_pilot.CachedDisplayLastAngleDiff, -20, 20);

        for (int i = 0; i < _turningHeaderLine.positionCount; i++)
        {
            var rotatedStep = Quaternion.Euler(0, 0, -clampedAngleDiff * i) * vectorStep;
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
                return PositionVirtualNode.CurrentTracedLine.LinkedPoint.Distance - NMWalkedOnCurrentSegment;
            }

            var nextViableNodeIndex = PositionVirtualNode.PassedNodeIndex + 1;
            while (Session.ActiveRoute.Points[nextViableNodeIndex].IsSkippable)
            {
                nextViableNodeIndex++;
            }

            return (Session.ActiveRoute.GetCartesianPosition(nextViableNodeIndex) -
                    _pilot.NMPosition).magnitude;
        }
    }

    public float HeadingDegrees => _pilot.HeadingDegrees;
    public float DisplayHeadingDegrees => _pilot.DisplayHeadingDegrees;


    public void ResetOnActiveSet(float aircraftSpeed, float altitude)
    {
        Speed = aircraftSpeed;
        
        _pilot.NMPosition = Vector2.zero;
        _pilot.HeadingDegrees = -Session.ActiveRoute.Points[1].Degrees;
        
        ResetPosition();

        ResetSeekProgress(1, 0);

    }

    public void ResetSeekProgress(int atLine, int atVertexIndex)
    {
        _lastFoundSegmentVertexIndex = atVertexIndex;
        _lastFoundSegmentIndex = atLine;
    }

    public void StartHeadingMode()
    {
        IsOnRoute = false;
    }

    public void StartLNavMode()
    {
        if (Session.ActiveRoute.TracedRoute.FindCloseToRouteSegmentDestination(
                Session.Settings.HDGCloseRejoinDistance,
                out _,
                out var lastFoundSegmentVertexIndex,
                out var lastFoundSegmentIndex,
                out _,
                out _,
                segmentBeginningIsAlwaysValid: false))
        {
            ResetSeekProgress(lastFoundSegmentIndex, lastFoundSegmentVertexIndex);
            // todo daniel isonRoute to be set later
            IsOnRoute = true;
            IsJoining = true;
        }

        else if (Session.ActiveRoute.FindFreeFlightDirectExitScenario(out _,
                     out var intersectionSegmentIndex, out var intersectionVertexIndex))
        {
            ResetSeekProgress(intersectionSegmentIndex, intersectionVertexIndex);
            IsOnRoute = true;
            IsJoining = true;
        }
        else
        {
            Debug.LogWarning("No Intersection Point Found");
        }
    }

    private void CheckAdvancePointOnHDGProximity()
    {
        var firstNextPoint = Session.PlayerAircraft.CurrentSegmentIndex;
        if (Session.ActiveRoute.Points.Length-1 <= firstNextPoint)
        {
            return;
        }
        
        var nodePosition = Session.ActiveRoute.Points[firstNextPoint].CartesianPosition;
        if (Vector2.Distance(nodePosition, _pilot.NMPosition) <=
            Session.Settings.HDGProximityAdvanceDistance)
        {
            CurrentSegmentIndex = firstNextPoint + 1;
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        if (Session.State.LNAV)
        {
            Gizmos.DrawWireSphere(transform.position + _lastFoundSegmentVertex.ToDisplay(), 0.12f);
        }

        Gizmos.color = Color.green;
        
        Gizmos.DrawWireSphere(transform.position + _pilot.NMPosition.ToDisplay(), 0.05f);
    }
}
