using System;
using Navigation;
using UnityEngine;

[Serializable]
public class Aircraft : MovingActor
{
    [SerializeField] private LineRenderer _turningHeaderLine;
    
    public float NMWalkedOnCurrentSegment;
    public float DistanceToNextPoint;
    public int CurrentSegmentIndex;

    /// <summary>
    /// If used for geometry should be used with '-' . see other places
    /// </summary>
    public float TargetHeading { get; private set; }

    public override Vector2 NMPosition => _pilot.NMPosition;

    public bool IsJoining => _pendingJoinRoutePosition != null;
    public bool IsOnRoute => Session.State.LNAV && !IsJoining;

    private Pilot _pilot;

    private Vector3 _upwardsHeaderLineTop = new(0, 1.2f, 0);

    private RoutePosition _lastFoundRoutePosition = new() { SegmentIndex = 1 };
    private RoutePosition? _pendingJoinRoutePosition = new() { SegmentIndex = 1 };

    private AircraftDebugHelper debugHelper = null;
    
    protected override void Awake()
    {
        base.Awake();

        _pilot = new Pilot(Vector2.zero, Vector2.up, false);
        
        _turningHeaderLine.positionCount = 10;

        debugHelper = GetComponent<AircraftDebugHelper>();
    }

    public override void SimulateTick()
    {
        DrawHeadingLine();
        CheckAdvancePointOnHDGProximity(out DistanceToNextPoint);

        if (Session.State.LNAV)
        {
            var foundClosePathDestination = Session.ActiveRoute.TracedRoute.FindCloseToRouteSegmentDestination(
                Session.Settings.PilotSeekDistancePathFollow,
                out _lastFoundRoutePosition,
                out var foundAtDistanceOnSegment,
                out var reachedEnd, // Must use this to end the route
                startFromSegmentIndex: _lastFoundRoutePosition.SegmentIndex,
                startFromVertexIndex: _lastFoundRoutePosition.SegmentVertexIndex,
                breakOnFistSolution: true);

            if (debugHelper != null)
            {
                debugHelper.SegmentVertex = _lastFoundRoutePosition.SegmentVertex;
                debugHelper.SegmentVertexIndex = _lastFoundRoutePosition.SegmentVertexIndex;
                debugHelper.SegmentIndex = _lastFoundRoutePosition.SegmentIndex;
            }

            if (!foundClosePathDestination)
            {
                Debug.LogError("No Intersection Point Found");
                Session.State.AutoSetHDG(true);

                return;
            }

            if (_pendingJoinRoutePosition != null)
            {
                if (_pendingJoinRoutePosition.Value != _lastFoundRoutePosition)
                {
                    _pendingJoinRoutePosition = null;
                }
            }

            _pilot.TickSteerToPathFoundVertex(_lastFoundRoutePosition.SegmentVertex, out _);
            _pilot.TickAdvance();

            CurrentSegmentIndex = _lastFoundRoutePosition.SegmentIndex;
            NMWalkedOnCurrentSegment = foundAtDistanceOnSegment;
        }
        else if(Session.State.HDG)
        {
            // airplane is on free flight
            
            TargetHeading = Calculator.RTrack;

            _pilot.TickSteerToTargetHeading(TargetHeading);

            _pilot.TickAdvance();

            
            // for display only
            //GameManager.Instance.ActiveRoute.FindFreeFlightDirectExitScenario(out displayCenterOfTurn, out displayExitPoint, out _);
        }
        else 
        {
            // both lnav and hg are off
            
            _pilot.TickAdvance();
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
            if (IsJoining)
            {
                var nextViableNodeIndex = PositionVirtualNode.PassedNodeIndex + 1;
                while (Session.ActiveRoute.Points[nextViableNodeIndex].IsSkippable)
                {
                    nextViableNodeIndex++;
                }

                return (Session.ActiveRoute.GetCartesianPosition(nextViableNodeIndex) -
                        _pilot.NMPosition).magnitude;
            }

            return DistanceToNextPoint;
        }
    }

    public float HeadingDegrees => _pilot.HeadingDegrees;
    public float DisplayHeadingDegrees => _pilot.DisplayHeadingDegrees;


    public void ResetOnActiveSet(float aircraftSpeed, float altitude)
    {
        _pilot.NMPosition = Vector2.zero;
        _pilot.HeadingDegrees = -Session.ActiveRoute.Points[1].Degrees;
        
         _pilot.NMPosition = Vector2.zero;
        NMWalkedOnCurrentSegment = 0;
        _pendingJoinRoutePosition = null;
        Session.State.AutoSetLNAV(true,true);
        ResetSeekProgress(1,0);
    }

    public void ResetSeekProgress(int atLine, int atVertexIndex)
    {
        _lastFoundRoutePosition.SegmentVertexIndex = atVertexIndex;
        _lastFoundRoutePosition.SegmentIndex = atLine;
        CurrentSegmentIndex = atLine;
    }

    public void StartHeadingMode()
    {
        _pendingJoinRoutePosition = null;
    }

    public bool TryRejoinRoute()
    {
        Debug.Log("Try-Re-Join-Route");
        if (Session.ActiveRoute.TracedRoute.FindCloseToRouteSegmentDestination(
                Session.Settings.HDGCloseRejoinDistance,
                out var routeIntersection,
                out _,
                out _,
                segmentBeginningIsAlwaysValid: false) ||
            Session.ActiveRoute.FindFreeFlightDirectExitScenario(out routeIntersection))
        {
            ResetSeekProgress(routeIntersection.SegmentIndex, routeIntersection.SegmentVertexIndex);
            _pendingJoinRoutePosition = routeIntersection;
            return true;
        }
        else
        {
            Debug.LogWarning("No Intersection Point Found");
            Session.State.AutoSetLNAV(false, true);
            return false;
        }
    }

    private void CheckAdvancePointOnHDGProximity(out float proximityDistance)
    {
        proximityDistance = -1;
        var firstNextPoint = Session.PlayerAircraft.CurrentSegmentIndex;
        if (Session.ActiveRoute.Points.Length - 1 <= firstNextPoint)
        {
            return;
        }

        var nodePosition = Session.ActiveRoute.Points[firstNextPoint].CartesianPosition;
        proximityDistance = Vector2.Distance(nodePosition, _pilot.NMPosition);
        if (proximityDistance <= Session.Settings.HDGProximityAdvanceDistance)
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
            Gizmos.DrawWireSphere(transform.position + _lastFoundRoutePosition.SegmentVertex.ToDisplay(), 0.12f);
        }

        Gizmos.color = Color.green;
        
        Gizmos.DrawWireSphere(transform.position + _pilot.NMPosition.ToDisplay(), 0.05f);
    }
}
