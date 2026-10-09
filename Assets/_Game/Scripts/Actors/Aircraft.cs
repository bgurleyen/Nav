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

    public void FinishGame(bool isStable = false)
    {
        Debug.Log(isStable ? "Finish GAME (stable)" : "Finish GAME (unstable)");
        GraphManage.OnGameFinish?.Invoke(isStable);
        Session.State.AutoSetHDG(true);
    }
    public override void SimulateTick()
    {
        DrawHeadingLine();

        TryCaptureLoc();
        TryCaptureGs();

        // Route progress feeds the vertical path. LOC only replaces lateral steering.
        CheckAdvancePointOnHDGProximity(out DistanceToNextPoint);

        if (Session.State.LOCCaptured)
        {
            SteerLocalizer();
            return;
        }

        if (Session.State.LNAV)
        {
            if (TrySteerTowardCourseIntercept(out var interceptWalked))
            {
                _pilot.TickAdvance();
                NMWalkedOnCurrentSegment = interceptWalked;
                return;
            }

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
                    _pendingJoinRoutePosition = null;
            }

            _pilot.NotifyTargetCourse(GetNextRealLegCourse());
            SteerLnavToSeekVertex(_lastFoundRoutePosition.SegmentVertex);
            _pilot.TickAdvance();

            CurrentSegmentIndex = _lastFoundRoutePosition.SegmentIndex;
            NMWalkedOnCurrentSegment = foundAtDistanceOnSegment;
        }
        else if (Session.State.HDG)
        {
            TargetHeading = Calculator.RTrack;
            _pilot.NotifyTargetCourse(TargetHeading);
            _pilot.TickSteerToTargetHeading(TargetHeading);
            _pilot.TickAdvance();
        }
        else
        {
            _pilot.TickAdvance();
        }
    }

    private void TryCaptureLoc()
    {
        if (Session.State.LOCCaptured || Move.Instance == null)
            return;

        float course = Session.CurrentLevel.levelInfo.Course;
        if (!Move.Instance.CanCaptureLoc(course))
            return;

        Session.State.LOCCaptured = true;
        _pendingJoinRoutePosition = null;
        Session.State.AutoSetHDG(true);

        // Snap MCP track to inbound course so HDG mode starts on the localizer.
        int courseHdg = Calculator.NormalizeHeading360(Mathf.RoundToInt(course));
        Calculator.RHeading = courseHdg;
        Calculator.Instance?.AddWindEffectToRHeading();

        Move.Instance.CancelRerouteForLocCapture();
        Move.Instance.NotifyLocCaptured();
    }

    /// <summary>
    /// Vertical capture is independent of LOC. GS locks VS/VNAV/ALT/FLCH;
    /// LOC only locks HDG/LNAV.
    /// </summary>
    private void TryCaptureGs()
    {
        if (Session.State.GSCaptured || Move.Instance == null || Session.CurrentLevel?.levelInfo == null)
            return;

        if (!Move.Instance.CanCaptureGs(Session.CurrentLevel.levelInfo.GlideSlope))
            return;

        Session.State.GSCaptured = true;
        Move.Instance.NotifyGsCaptured();
    }

    /// <summary>
    /// Follow ILS course and close remaining cross-track toward centerline.
    /// Does not touch ActiveRoute indices or vertical (GS) path.
    /// </summary>
    private void SteerLocalizer()
    {
        float course = Session.CurrentLevel.levelInfo.Course;
        float intercept = 0f;

        if (Move.Instance != null)
        {
            Move.Instance.ComputeLocTrackErrors(course, out _, out float crossTrackNm);
            // Short look-ahead → decisive intercept (often near ±30° while offset), rolls out as XTK → 0.
            const float lookAheadNm = 0.75f;
            intercept = Mathf.Rad2Deg * Mathf.Atan2(-crossTrackNm, lookAheadNm);
            intercept = Mathf.Clamp(intercept, -30f, 30f);
        }

        float desiredTrack = course + intercept;
        if (desiredTrack < 0f) desiredTrack += 360f;
        if (desiredTrack >= 360f) desiredTrack -= 360f;

        TargetHeading = desiredTrack;
        _pilot.TickSteerToTargetHeading(TargetHeading);
        _pilot.TickAdvance();

        if (Move.Instance != null && Move.Instance.DME() < 0.3f
            && Move.Instance.IsWithinFinalApproachCourse(20f))
            FinishGame();
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
                var points = Session.ActiveRoute?.Points;
                if (points == null || points.Length == 0)
                    return DistanceToNextPoint;

                var nextViableNodeIndex = PositionVirtualNode.PassedNodeIndex + 1;
                while (nextViableNodeIndex < points.Length && points[nextViableNodeIndex].IsSkippable)
                    nextViableNodeIndex++;

                if (nextViableNodeIndex >= points.Length)
                    return DistanceToNextPoint;

                return (Session.ActiveRoute.GetCartesianPosition(nextViableNodeIndex) -
                        _pilot.NMPosition).magnitude;
            }

            return DistanceToNextPoint;
        }
    }

    public float HeadingDegrees => Calculator.CTrack;
    public float DisplayHeadingDegrees => _pilot.DisplayHeadingDegrees;
    public float TrackDegrees => _pilot.HeadingDegrees;

    /// <summary>Instantly set aircraft track (no turn lerp).</summary>
    public void SnapToTrack(float trackDegrees)
    {
        _pilot.HeadingDegrees = trackDegrees;
        _pilot.DisplayHeadingDegrees = trackDegrees;
    }


    public void ResetOnActiveSet(float aircraftSpeed, float altitude)
    {
        _pilot.NMPosition = Vector2.zero;

        // Course authored on the start waypoint (route point 0). Snap track and
        // compass together so the level does not open on 000 and turn onto it.
        float startTrack = 0f;
        var points = Session.ActiveRoute?.Points;
        if (points != null && points.Length > 0)
            startTrack = Mathf.Repeat(points[0].RawDegrees, 360f);

        _pilot.HeadingDegrees = startTrack;
        _pilot.DisplayHeadingDegrees = startTrack;

        int startHdg = Calculator.NormalizeHeading360(Mathf.RoundToInt(startTrack));
        Calculator.RHeading = startHdg;
        Calculator.RTrack = startHdg;

        NMWalkedOnCurrentSegment = 0;
        _pendingJoinRoutePosition = null;
        _pilot.ClearWholeTurn();
        Session.State.AutoSetLNAV(true, true);
        ResetSeekProgress(1, 0);
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
        var startSegment = Session.ActiveRoute.FindForwardPositionNodeIndex();
        if (startSegment < 1)
            startSegment = 1;

        if (Session.ActiveRoute.TracedRoute.FindCloseToRouteSegmentDestination(
                Session.Settings.HDGCloseRejoinDistance,
                out var routeIntersection,
                out _,
                out _,
                startFromSegmentIndex: startSegment,
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

    /// <summary>
    /// On a course-intercept route, LNAV turns straight at the nearest drawn point
    /// until the aircraft is close enough to follow the path forward.
    /// </summary>
    private bool TrySteerTowardCourseIntercept(out float walkedNm)
    {
        walkedNm = 0f;
        var route = Session.ActiveRoute;
        if (route == null || !route.IsCourseInterceptRoute)
            return false;

        route.FindClosestPointOnRoute(_pilot.NMPosition, out var segment, out var vertex);
        var lines = route.TracedRoute?.ComputedLines;
        if (lines == null || segment < 1 || segment >= lines.Length)
            return false;

        var vertices = lines[segment].SegmentVertices;
        if (vertices == null || vertices.Length == 0)
            return false;

        vertex = Mathf.Clamp(vertex, 0, vertices.Length - 1);
        var nearest = vertices[vertex];
        var toNearest = nearest - _pilot.NMPosition;
        if (toNearest.sqrMagnitude <= Session.Settings.PilotSeekDistancePathFollow * Session.Settings.PilotSeekDistancePathFollow)
            return false;

        var heading = Geometry.GetHeadingOfDirection(toNearest);
        _pilot.NotifyTargetCourse(heading);
        _pilot.TickSteerToTargetHeading(heading, true);
        ResetSeekProgress(segment, vertex);
        CurrentSegmentIndex = segment;
        walkedNm = Mathf.Max(0, vertex - 1) * Session.Settings.SegmentGranularity;
        return true;
    }

    /// <summary>
    /// Follow the seek vertex unless it is behind or would reverse vs the inbound course.
    /// In those cases steer the leg course so LNAV does not turn 180° the wrong way.
    /// </summary>
    private void SteerLnavToSeekVertex(Vector2 seekVertex)
    {
        var legCourse = GetNextRealLegCourse();
        var toSeek = seekVertex - _pilot.NMPosition;
        if (toSeek.sqrMagnitude < 0.0001f)
        {
            _pilot.TickSteerToTargetHeading(legCourse);
            return;
        }

        var seekHeading = Geometry.GetHeadingOfDirection(toSeek);
        var forward = Geometry.GetDirectionFromHeading(_pilot.HeadingDegrees);
        var behind = Vector2.Dot(toSeek, forward) < 0f;
        var reverseOfLeg = Mathf.Abs(Geometry.AngleDelta(seekHeading, legCourse)) > 90f;
        if (behind || reverseOfLeg)
        {
            _pilot.TickSteerToTargetHeading(legCourse);
            return;
        }

        _pilot.TickSteerToPathFoundVertex(seekVertex, out _);
    }

    /// <summary>
    /// Inbound course of the next real (non-skippable) leg: previous point → that waypoint.
    /// Position stubs stay as the inbound start so intercept angle is the full turn.
    /// </summary>
    private float GetNextRealLegCourse()
    {
        var points = Session.ActiveRoute?.Points;
        if (points == null || points.Length < 2)
            return _pilot.HeadingDegrees;

        var dest = Mathf.Max(CurrentSegmentIndex, 1);
        while (dest < points.Length && (points[dest] == null || points[dest].IsSkippable))
            dest++;
        if (dest >= points.Length)
            dest = Mathf.Clamp(CurrentSegmentIndex, 1, points.Length - 1);

        var from = dest - 1;
        if (from < 0)
            from = 0;

        var delta = points[dest].CartesianPosition - points[from].CartesianPosition;
        if (delta.sqrMagnitude < 0.0001f)
            delta = points[dest].CartesianPosition - _pilot.NMPosition;
        if (delta.sqrMagnitude < 0.0001f)
            return _pilot.HeadingDegrees;

        return Geometry.GetHeadingOfDirection(delta);
    }

    private void CheckAdvancePointOnHDGProximity(out float proximityDistance)
    {
        proximityDistance = -1;
        var firstNextPoint = Session.PlayerAircraft.CurrentSegmentIndex;
        if (Session.ActiveRoute.Points.Length - 1 <= firstNextPoint)
        {
            return;
        }
        if (firstNextPoint < 0) firstNextPoint = 0;
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
