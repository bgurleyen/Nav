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
    private float _trendAngle;
    /// <summary>
    /// Held while cross-track is still large so capture does not fall back
    /// onto the short seek point that swings across the nose.
    /// </summary>
    private bool _routeCapture;

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

    bool _gameFinished;

    public void FinishGame(bool isStable = false)
    {
        if (_gameFinished)
            return;

        _gameFinished = true;
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
        _pilot.ClearWholeTurn();

        int courseHdg = Calculator.NormalizeHeading360(Mathf.RoundToInt(course));
        Calculator.RHeading = courseHdg;
        Calculator.Instance?.AddWindEffectToRHeading();

        Move.Instance.CancelRerouteForLocCapture();
        Move.Instance.NotifyLocCaptured();
    }

    /// <summary>
    /// GS capture disengages VS/VNAV/ALT/FLCH. LOC capture disengages HDG/LNAV.
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
        // Do not keep a pre-capture latched turn: a leftover left intercept
        // would take the long way around instead of rolling out on the inbound course.
        _pilot.ClearWholeTurn();
        _pilot.TickSteerToTargetHeading(TargetHeading, true);
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

        var targetAngle = Mathf.Clamp(_pilot.CachedDisplayLastAngleDiff, -20f, 20f);
        // Sub-degree lag is a straight vector. Filtering keeps a one-tick
        // heading blip from flipping the polyline left and right.
        if (Mathf.Abs(targetAngle) < 0.75f)
            targetAngle = 0f;
        _trendAngle = Mathf.Lerp(_trendAngle, targetAngle, 0.2f);
        var clampedAngleDiff = _trendAngle;

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
        _routeCapture = false;
        _trendAngle = 0f;
        _pilot.ClearWholeTurn();
        Session.State.AutoSetLNAV(true, true);
        if (Session.CurrentLevel != null && Session.CurrentLevel.levelInfo != null
            && Session.CurrentLevel.levelInfo.LevelNumber == PlayerPrefsHolder.FreeFlightLevel)
            Session.State.AutoSetHDG(true);
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
        _routeCapture = false;
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
    /// On a course-intercept route, LNAV holds one intercept onto the nearest leg
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

        // Aiming at the nearest vertex swings as that point passes under the nose.
        // Hold a single intercept that shrinks with cross-track instead.
        var legFrom = vertices[0];
        var legTo = vertices[vertices.Length - 1];
        if ((legTo - legFrom).sqrMagnitude < 0.0001f)
            return false;

        var course = Geometry.GetHeadingOfDirection(legTo - legFrom);
        var heading = CaptureHeading(legFrom, legTo, course);
        _pilot.NotifyTargetCourse(course);
        _pilot.TickSteerToTargetHeading(heading, true);
        ResetSeekProgress(segment, vertex);
        CurrentSegmentIndex = segment;
        walkedNm = Mathf.Max(0, vertex - 1) * Session.Settings.SegmentGranularity;
        return true;
    }

    /// <summary>
    /// On the leg, follow the seek vertex. While still offset, a 1.2 NM seek point
    /// sits near the beam and its bearing flips across the nose, so the trend
    /// vector wags until cross-track collapses. Capture uses one intercept that
    /// only gets shallower as the aircraft settles.
    /// </summary>
    private void SteerLnavToSeekVertex(Vector2 seekVertex)
    {
        if (!TryGetActiveLeg(out var legFrom, out var legTo, out var legCourse))
        {
            _pilot.TickSteerToPathFoundVertex(seekVertex, out _);
            return;
        }

        var absXte = Mathf.Abs(SignedCrossTrack(legFrom, legTo, _pilot.NMPosition));
        if (absXte > 0.35f)
            _routeCapture = true;
        else if (absXte < 0.12f)
            _routeCapture = false;

        var toSeek = seekVertex - _pilot.NMPosition;
        var seekUnreliable = toSeek.sqrMagnitude < 0.0001f;
        if (!seekUnreliable)
        {
            var seekHeading = Geometry.GetHeadingOfDirection(toSeek);
            var forward = Geometry.GetDirectionFromHeading(_pilot.HeadingDegrees);
            var behind = Vector2.Dot(toSeek, forward) < 0f;
            var reverseOfLeg = Mathf.Abs(Geometry.AngleDelta(seekHeading, legCourse)) > 90f;
            seekUnreliable = behind || reverseOfLeg;
        }

        if (!_routeCapture && !seekUnreliable)
        {
            _pilot.TickSteerToPathFoundVertex(seekVertex, out _);
            return;
        }

        _pilot.TickSteerToTargetHeading(CaptureHeading(legFrom, legTo, legCourse));
    }

    /// <summary>
    /// Leg course plus a cross-track intercept, capped so the nose holds one side.
    /// </summary>
    private float CaptureHeading(Vector2 legFrom, Vector2 legTo, float legCourse)
    {
        const float lookAheadNm = 4f;
        var xte = SignedCrossTrack(legFrom, legTo, _pilot.NMPosition);
        var intercept = Mathf.Clamp(Mathf.Rad2Deg * Mathf.Atan2(-xte, lookAheadNm), -30f, 30f);
        return Mathf.Repeat(legCourse + intercept, 360f);
    }

    /// <summary>Positive when <paramref name="position"/> is right of the from→to track.</summary>
    private static float SignedCrossTrack(Vector2 from, Vector2 to, Vector2 position)
    {
        var ab = to - from;
        var len = ab.magnitude;
        if (len < 0.0001f)
            return 0f;

        var dir = ab / len;
        var ac = position - from;
        return ac.x * dir.y - ac.y * dir.x;
    }

    /// <summary>
    /// Inbound course of the next real (non-skippable) leg: previous point → that waypoint.
    /// Position stubs stay as the inbound start so intercept angle is the full turn.
    /// </summary>
    private float GetNextRealLegCourse()
    {
        if (TryGetActiveLeg(out _, out _, out var course))
            return course;
        return _pilot.HeadingDegrees;
    }

    private bool TryGetActiveLeg(out Vector2 from, out Vector2 to, out float course)
    {
        course = _pilot.HeadingDegrees;
        from = to = _pilot.NMPosition;

        var points = Session.ActiveRoute?.Points;
        if (points == null || points.Length < 2)
            return false;

        var dest = Mathf.Max(CurrentSegmentIndex, 1);
        while (dest < points.Length && (points[dest] == null || points[dest].IsSkippable))
            dest++;
        if (dest >= points.Length)
            dest = Mathf.Clamp(CurrentSegmentIndex, 1, points.Length - 1);
        if (dest < 0 || dest >= points.Length || points[dest] == null)
            return false;

        var fromIndex = Mathf.Clamp(dest - 1, 0, points.Length - 1);
        if (points[fromIndex] == null)
            return false;

        from = points[fromIndex].CartesianPosition;
        to = points[dest].CartesianPosition;
        var delta = to - from;
        if (delta.sqrMagnitude < 0.0001f)
            delta = to - _pilot.NMPosition;
        if (delta.sqrMagnitude < 0.0001f)
            return false;

        course = Geometry.GetHeadingOfDirection(delta);
        return true;
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
            var nextIndex = firstNextPoint + 1;
            if (nextIndex < Session.ActiveRoute.Points.Length)
            {
                var next = Session.ActiveRoute.Points[nextIndex];
                if (next != null && next.IsAfterDiscontinuity)
                    return;
            }
            CurrentSegmentIndex = nextIndex;
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
