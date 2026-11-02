using System.Collections.Generic;
using Navigation;
using UnityEngine;

public class OtherAC
{
    const float PointCheckNm = 1.3f;
    const float OwnHideNm = 3f;
    const float OwnHideFt = 1000f;
    const float OwnShortFinalNm = 15f;
    const float TrafficNearRunwayNm = 3f;
    const float TcasMaxRangeNm = 40f;
    const float PositionPeriodSec = 1f;
    const float AltitudeFtPerNm = 300f;
    const float RwyGapMinSec = 60f;
    const float RwyGapMaxSec = 120f;
    const float TrafficGsMinKt = 120f;
    const float TrafficGsMaxKt = 450f;

    private ACInfo[] _acItems;
    private string _aircraftKey;
    private int _acIndex;
    private int _currentPointIndex = 1;
    private bool _hidden;
    private Vector2 finalPosition;
    private float AltitudeC;
    private int PointIndex;
    private Vector2 PtPos;
    private float dx, dy, hyp, x, y;
    private Vector2[] _pts;
    private float[] _rem;
    private bool _released;
    private float _releaseAtHours;
    private int _queueIndex = -1;
    private int _lastPosSeq = -1;
    private static float _simHours;
    private static float _posAccSec;
    private static float _posStepSec;
    private static bool _posDue;
    private static int _posSeq;
    private static int _clockFrame = -1;
    private static float[] _landingGapSec;
    private static int _ownQueueIndex = -1;

    public OtherAC(int acIndex, LevelDataScriptableObject levelData)
    {
        _acIndex = acIndex;
        int levelNo = levelData != null && levelData.levelInfo != null ? levelData.levelInfo.LevelNumber : 0;
        _acItems = BuildItemsFromTxt(levelNo, acIndex + 1);
        _aircraftKey = "AC (" + (acIndex + 1) + ")";

        if (_acItems == null || _acItems.Length < 2)
            Hide();
    }

    struct LandingParticipant
    {
        public int AcIndex;
        public float RemNm;
    }

    public static void ApplyFormation(OtherAC[] all)
    {
        _simHours = 0f;
        _clockFrame = -1;
        _posAccSec = 0f;
        _posStepSec = 0f;
        _posDue = false;
        _posSeq = 0;
        _landingGapSec = null;
        _ownQueueIndex = -1;
        if (all == null)
            return;

        for (int i = 0; i < all.Length; i++)
        {
            OtherAC ac = all[i];
            if (ac == null || !ac.HasRoute)
                continue;
            ac.PlaceForDesired(ac._rem[0]);
            ac.Hold();
            ac._releaseAtHours = 0f;
        }

        BuildLandingQueue(all);
    }

    static void BuildLandingQueue(OtherAC[] all)
    {
        var queue = new List<LandingParticipant>();
        if (Move.Instance != null)
        {
            queue.Add(new LandingParticipant
            {
                AcIndex = -1,
                RemNm = Move.Instance.AtcInstructionsRemaining()
            });
        }

        for (int i = 0; i < all.Length; i++)
        {
            OtherAC ac = all[i];
            if (ac == null || !ac.HasRoute)
                continue;
            queue.Add(new LandingParticipant { AcIndex = i, RemNm = ac._rem[0] });
        }

        queue.Sort((a, b) =>
        {
            int c = a.RemNm.CompareTo(b.RemNm);
            if (c != 0)
                return c;
            return a.AcIndex.CompareTo(b.AcIndex);
        });

        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null)
                all[i]._queueIndex = -1;
        }

        int n = queue.Count;
        if (n < 2)
            return;

        _landingGapSec = new float[n - 1];
        for (int g = 0; g < _landingGapSec.Length; g++)
            _landingGapSec[g] = Random.Range(RwyGapMinSec, RwyGapMaxSec);

        _ownQueueIndex = -1;
        for (int i = 0; i < queue.Count; i++)
        {
            LandingParticipant p = queue[i];
            if (p.AcIndex == -1)
                _ownQueueIndex = i;
            else if (p.AcIndex >= 0 && p.AcIndex < all.Length && all[p.AcIndex] != null)
                all[p.AcIndex]._queueIndex = i;
        }
    }

    static float OwnEtaHours()
    {
        if (Move.Instance == null)
            return 1f;
        float rem = Move.Instance.AtcInstructionsRemaining();
        if (rem < 0.001f)
            rem = 0.001f;
        return rem / OwnGroundSpeedKt();
    }

    static float TargetRwyAtHours(int queueIndex)
    {
        float tOwn = _simHours + OwnEtaHours();
        if (_ownQueueIndex < 0 || _landingGapSec == null || queueIndex < 0)
            return tOwn;
        if (queueIndex == _ownQueueIndex)
            return tOwn;
        if (queueIndex < _ownQueueIndex)
        {
            float t = tOwn;
            for (int i = queueIndex; i < _ownQueueIndex; i++)
                t -= _landingGapSec[i] / 3600f;
            return t;
        }
        float after = tOwn;
        for (int i = _ownQueueIndex; i < queueIndex; i++)
            after += _landingGapSec[i] / 3600f;
        return after;
    }

    public void Tick(Dictionary<string, string> acTexts, Dictionary<string, Vector2> acPositions)
    {
        if (_hidden || _acItems == null || Session.Settings == null || Move.Instance == null || acTexts == null || acPositions == null)
            return;

        AdvanceClock();
        if (!_released)
        {
            if (_simHours < _releaseAtHours)
            {
                SuppressDisplay(acTexts, acPositions);
                return;
            }
            _released = true;
        }

        if (!_posDue || _lastPosSeq == _posSeq)
            return;
        _lastPosSeq = _posSeq;

        if (_currentPointIndex >= _acItems.Length)
            RestartRoute();
        if (_hidden || !_released)
            return;

        float stepLeft = GroundSpeedKt() / 3600f * _posStepSec;

        if (!acPositions.ContainsKey(_aircraftKey))
        {
            acPositions.Add(_aircraftKey, Vector2.zero);
            acTexts.Add(_aircraftKey, "");
        }

        int hops = 0;
        while (stepLeft > 0.0001f && !_hidden && _released && _currentPointIndex < _acItems.Length && hops < 200)
        {
            hops++;
            PointIndex = _acItems[_currentPointIndex].Point;
            PtPos = Move.Instance.PointPos(PointIndex);

            x = finalPosition.x;
            y = finalPosition.y;
            dx = PtPos.x - x;
            dy = PtPos.y - y;
            hyp = Mathf.Sqrt(dx * dx + dy * dy);

            if (hyp < PointCheckNm)
            {
                if (_currentPointIndex >= _acItems.Length - 1)
                {
                    RestartRoute();
                    continue;
                }
                _currentPointIndex += 1;
                continue;
            }

            float toCheck = hyp - PointCheckNm;
            if (toCheck <= stepLeft)
            {
                finalPosition = new Vector2(x + dx / hyp * toCheck, y + dy / hyp * toCheck);
                stepLeft -= toCheck;
                if (_currentPointIndex >= _acItems.Length - 1)
                {
                    RestartRoute();
                    continue;
                }
                _currentPointIndex += 1;
                continue;
            }

            finalPosition = new Vector2(x + dx / hyp * stepLeft, y + dy / hyp * stepLeft);
            stepLeft = 0f;
        }

        AltitudeC = TrafficAltitudeFt(RemainingNm());

        if (_hidden || !_released)
        {
            SuppressDisplay(acTexts, acPositions);
            return;
        }

        if (TooCloseToOwn() || HideTrafficNearRunway())
        {
            RestartRoute();
            SuppressDisplay(acTexts, acPositions);
            return;
        }

        if (!InsideTcasWindow())
        {
            SuppressDisplay(acTexts, acPositions);
            return;
        }

        acPositions[_aircraftKey] = finalPosition;
        acTexts[_aircraftKey] = RelativeAltitudeLabel();
    }

    private bool InsideTcasWindow()
    {
        if (Session.State == null || Session.PlayerAircraft == null || Session.State.MapMode == MapMode.Plan)
            return false;
        float relFt = AltitudeC - (float)Calculator.CAltitude;
        if (relFt > 2700f || relFt < -9900f)
            return false;
        float zoom = Session.ZoomMultiplier;
        if (zoom < 0.01f)
            zoom = Session.Settings != null ? Session.Settings.StartingZoom : 1f;
        if (zoom < 0.01f)
            zoom = 1f;
        float mapRangeNm = 80f / zoom;
        float rangeNm = Mathf.Min(TcasMaxRangeNm, mapRangeNm);
        return Vector2.Distance(finalPosition, Session.PlayerAircraft.NMPosition) <= rangeNm;
    }

    private string RelativeAltitudeLabel()
    {
        float relFt = AltitudeC - (float)Calculator.CAltitude;
        string s = relFt < 0f ? "-" : "+";
        if (Mathf.Abs(relFt) < 1000f)
            s += "0";
        s += (int)(Mathf.Abs(relFt) / 100f);
        return s;
    }

    private ACInfo[] BuildItemsFromTxt(int levelNo, int acNumber)
    {
        var ids = AcRoutesLoader.GetPoints(levelNo, acNumber);
        if (ids == null || ids.Count == 0)
            return null;

        int rw = 0;
        if (Session.OriginalReferenceRoute != null && Session.OriginalReferenceRoute.Points != null)
            rw = Session.OriginalReferenceRoute.Points.Length - 1;
        if (rw > 0 && ids[ids.Count - 1] != rw)
            ids.Add(rw);
        if (ids.Count < 2 || Move.Instance == null)
            return null;

        var pts = new Vector2[ids.Count];
        for (int i = 0; i < ids.Count; i++)
            pts[i] = Move.Instance.PointPos(ids[i]);

        var rem = new float[ids.Count];
        rem[ids.Count - 1] = 0f;
        for (int i = ids.Count - 2; i >= 0; i--)
            rem[i] = rem[i + 1] + Vector2.Distance(pts[i], pts[i + 1]);

        var items = new ACInfo[ids.Count];
        for (int i = 0; i < ids.Count; i++)
        {
            items[i] = new ACInfo
            {
                Point = ids[i],
                Altitude = Mathf.Max(0, Mathf.RoundToInt(TrafficAltitudeFt(rem[i]))),
                Speed = 0
            };
        }
        _pts = pts;
        _rem = rem;
        return items;
    }

    private bool HasRoute
    {
        get { return _acItems != null && _pts != null && _rem != null && _rem.Length >= 2; }
    }

    private void PlaceForDesired(float desired)
    {
        float path = _rem[0];
        if (desired > path + 0.05f)
        {
            Vector2 leg = _pts[1] - _pts[0];
            float legLen = leg.magnitude;
            Vector2 u = legLen > 0.0001f ? leg / legLen : Vector2.up;
            finalPosition = _pts[0] - u * (desired - path);
            _currentPointIndex = 0;
        }
        else if (desired < path - 0.05f)
        {
            int seg = SegmentFor(_rem, desired);
            float span = _rem[seg] - _rem[seg + 1];
            float t = span < 0.0001f ? 0f : (_rem[seg] - desired) / span;
            t = Mathf.Clamp01(t);
            finalPosition = Vector2.Lerp(_pts[seg], _pts[seg + 1], t);
            _currentPointIndex = seg + 1;
        }
        else
        {
            finalPosition = _pts[0];
            _currentPointIndex = 1;
        }

        AltitudeC = TrafficAltitudeFt(RemainingNm());
    }

    private void Hold()
    {
        _released = false;
        _releaseAtHours = 0f;
        _hidden = false;
    }

    private static void AdvanceClock()
    {
        int frame = Time.frameCount;
        if (frame == _clockFrame)
            return;
        _clockFrame = frame;
        float dt = Session.Settings != null ? Session.Settings.FlyingTickDuration : 0f;
        if (dt < 0f)
            dt = 0f;
        _simHours += dt / 3600f;
        _posAccSec += dt;
        _posStepSec = 0f;
        _posDue = false;
        while (_posAccSec >= PositionPeriodSec)
        {
            _posAccSec -= PositionPeriodSec;
            _posStepSec += PositionPeriodSec;
        }
        if (_posStepSec > 0f)
        {
            _posDue = true;
            _posSeq++;
        }
    }

    private float GroundSpeedKt()
    {
        if (!_released || _queueIndex < 0)
            return OwnGroundSpeedKt();

        float target = TargetRwyAtHours(_queueIndex);
        float hrs = target - _simHours;
        if (hrs < 1f / 3600f)
            hrs = 1f / 3600f;
        float rem = RemainingNm();
        if (rem < 0.001f)
            return OwnGroundSpeedKt();
        float gs = rem / hrs;
        return Mathf.Clamp(gs, TrafficGsMinKt, TrafficGsMaxKt);
    }

    private static float OwnGroundSpeedKt()
    {
        float gs = Calculator.GS > 1f ? Calculator.GS : (float)Calculator.CSpeed;
        return gs < 1f ? 1f : gs;
    }

    private static float TrafficAltitudeFt(float remainingNm)
    {
        return Mathf.Max(0f, remainingNm) * AltitudeFtPerNm;
    }

    private bool TooCloseToOwn()
    {
        if (Session.PlayerAircraft == null)
            return false;
        float distNm = Vector2.Distance(finalPosition, Session.PlayerAircraft.NMPosition);
        if (distNm > OwnHideNm)
            return false;
        return Mathf.Abs(AltitudeC - (float)Calculator.CAltitude) <= OwnHideFt;
    }

    private bool HideTrafficNearRunway()
    {
        if (Move.Instance == null || Session.PlayerAircraft == null)
            return false;
        if (Move.Instance.DME() > OwnShortFinalNm)
            return false;
        var route = Session.OriginalReferenceRoute;
        if (route?.Points == null || route.Points.Length == 0)
            return false;
        Vector2 rw = Move.Instance.PointPos(route.Points.Length - 1);
        return Vector2.Distance(finalPosition, rw) <= TrafficNearRunwayNm;
    }

    private void SuppressDisplay(Dictionary<string, string> acTexts = null, Dictionary<string, Vector2> acPositions = null)
    {
        if (acPositions != null)
            acPositions.Remove(_aircraftKey);
        if (acTexts != null)
            acTexts.Remove(_aircraftKey);
    }

    private float RemainingNm()
    {
        if (Move.Instance == null || _acItems == null || _currentPointIndex < 0 || _currentPointIndex >= _acItems.Length)
            return 0f;
        var current = _acItems[_currentPointIndex];
        if (current == null)
            return 0f;
        float rem = Vector2.Distance(finalPosition, Move.Instance.PointPos(current.Point));
        for (int i = _currentPointIndex; i < _acItems.Length - 1; i++)
        {
            var from = _acItems[i];
            var to = _acItems[i + 1];
            if (from == null || to == null)
                continue;
            rem += Vector2.Distance(
                Move.Instance.PointPos(from.Point),
                Move.Instance.PointPos(to.Point));
        }
        return rem;
    }

    private static int SegmentFor(float[] rem, float desired)
    {
        for (int i = 0; i < rem.Length - 1; i++)
        {
            if (desired <= rem[i] + 0.05f && desired >= rem[i + 1] - 0.05f)
                return i;
        }
        return rem.Length - 2;
    }

    private void RestartRoute()
    {
        if (_pts == null || _pts.Length < 2 || _acItems == null || _acItems.Length < 2 || _rem == null)
        {
            Hide();
            return;
        }

        finalPosition = _pts[0];
        _currentPointIndex = 1;
        AltitudeC = TrafficAltitudeFt(_rem[0]);
        _hidden = false;
        _released = false;
        _releaseAtHours = 0f;
    }

    private void Hide(Dictionary<string, string> acTexts = null, Dictionary<string, Vector2> acPositions = null)
    {
        _hidden = true;
        SuppressDisplay(acTexts, acPositions);
    }
}
