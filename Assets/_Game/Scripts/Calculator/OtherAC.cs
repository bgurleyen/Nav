using System.Collections.Generic;
using Navigation;
using UnityEngine;

public class OtherAC
{
    const float PointCheckNm = 1.3f;
    const float StarGsKt = 400f;
    const float RwyGsKt = 140f;
    const float FinalSpacingNm = 5f;
    const float GateNm = 20f;
    const float GateAboveRwyFt = 6000f;
    const float OwnHideNm = 3f;
    const float OwnHideFt = 1000f;
    const float OwnShortFinalNm = 15f;
    const float TrafficNearRunwayNm = 3f;
    const float TcasMaxRangeNm = 40f;
    const float PositionPeriodSec = 1f;
    const float TrafficSepNm = 5f;
    const float TrafficSepFt = 1000f;

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
    private float _rwyAlt;
    private float _initialRemaining;
    private bool _released;
    private float _releaseAtHours;
    private int _lastPosSeq = -1;
    private static OtherAC[] _all;
    private static float _simHours;
    private static int _clockFrame = -1;
    private static float _posAccSec;
    private static float _posStepSec;
    private static bool _posDue;
    private static int _posSeq;

    public OtherAC(int acIndex, LevelDataScriptableObject levelData)
    {
        _acIndex = acIndex;
        int levelNo = levelData != null && levelData.levelInfo != null ? levelData.levelInfo.LevelNumber : 0;
        _acItems = BuildItemsFromTxt(levelNo, acIndex + 1);
        _aircraftKey = "AC (" + (acIndex + 1) + ")";

        if (_acItems == null || _acItems.Length < 2)
        {
            Hide();
            return;
        }
    }

    public static void ApplyFormation(OtherAC[] all)
    {
        _all = all;
        _simHours = 0f;
        _clockFrame = -1;
        _posAccSec = 0f;
        _posStepSec = 0f;
        _posDue = false;
        _posSeq = 0;
        if (all == null)
            return;

        var flying = new List<OtherAC>();
        for (int i = 0; i < all.Length; i++)
        {
            OtherAC ac = all[i];
            if (ac == null || !ac.HasRoute)
                continue;
            ac.PlaceForDesired(ac._rem[0]);
            ac.Hold();
            flying.Add(ac);
        }

        flying.Sort(CompareStarArrival);
        float prevArrival = 0f;
        float gapHours = FinalSpacingNm / RwyGsKt;
        for (int s = 0; s < flying.Count; s++)
        {
            float path = flying[s]._rem[0];
            float fromStar = TimeToRwyHours(path, path);
            float arrival = s == 0 ? fromStar : Mathf.Max(fromStar, prevArrival + gapHours);
            prevArrival = arrival;
            flying[s]._releaseAtHours = arrival - fromStar;
        }
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

        if (_hidden || !_released)
        {
            SuppressDisplay(acTexts, acPositions);
            return;
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

        AltitudeC = ProfileAltitude(RemainingNm());

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
            if (acPositions.ContainsKey(_aircraftKey))
                acPositions.Remove(_aircraftKey);
            if (acTexts.ContainsKey(_aircraftKey))
                acTexts.Remove(_aircraftKey);
            return;
        }

        if (!acPositions.ContainsKey(_aircraftKey))
        {
            acPositions.Add(_aircraftKey, Vector2.zero);
            acTexts.Add(_aircraftKey, "");
        }
        acPositions[_aircraftKey] = finalPosition;
        acTexts[_aircraftKey] = RelativeAltitudeLabel();
    }

    public static void ApplyVerticalSeparation(
        Dictionary<string, string> acTexts, Dictionary<string, Vector2> acPositions)
    {
        if (_all == null)
            return;

        for (int i = 0; i < _all.Length; i++)
        {
            OtherAC ac = _all[i];
            if (ac == null || !ac.IsFlying())
                continue;
            ac.AltitudeC = ac.ProfileAltitude(ac.RemainingNm());
        }

        bool changed = true;
        int guard = 0;
        while (changed && guard < 20)
        {
            changed = false;
            guard++;
            for (int i = 0; i < _all.Length; i++)
            {
                OtherAC a = _all[i];
                if (a == null || !a.IsFlying())
                    continue;
                for (int j = i + 1; j < _all.Length; j++)
                {
                    OtherAC b = _all[j];
                    if (b == null || !b.IsFlying())
                        continue;
                    if (Vector2.Distance(a.finalPosition, b.finalPosition) >= TrafficSepNm)
                        continue;
                    if (Mathf.Abs(a.AltitudeC - b.AltitudeC) >= TrafficSepFt)
                        continue;
                    if (a.AltitudeC > b.AltitudeC || (a.AltitudeC == b.AltitudeC && a._acIndex < b._acIndex))
                        a.AltitudeC += TrafficSepFt;
                    else
                        b.AltitudeC += TrafficSepFt;
                    changed = true;
                }
            }
        }

        if (acTexts == null)
            return;
        for (int i = 0; i < _all.Length; i++)
        {
            OtherAC ac = _all[i];
            if (ac == null || !ac.IsFlying() || !acTexts.ContainsKey(ac._aircraftKey))
                continue;
            acTexts[ac._aircraftKey] = ac.RelativeAltitudeLabel();
        }
    }

    private bool IsFlying()
    {
        return !_hidden && _released && HasRoute;
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

        float rwyAlt = 0f;
        if (Session.OriginalReferenceRoute != null && Session.OriginalReferenceRoute.Points != null
            && Session.OriginalReferenceRoute.Points.Length > 0)
        {
            var last = Session.OriginalReferenceRoute.Points[Session.OriginalReferenceRoute.Points.Length - 1];
            if (last != null && last.Altitude != null && last.Altitude.ComputedValue > 0f)
                rwyAlt = last.Altitude.ComputedValue;
        }

        var items = new ACInfo[ids.Count];
        for (int i = 0; i < ids.Count; i++)
        {
            items[i] = new ACInfo
            {
                Point = ids[i],
                Altitude = Mathf.Max(0, Mathf.RoundToInt(ProfileAltitude(rem[i], rem[0], rwyAlt))),
                Speed = 0
            };
        }
        _pts = pts;
        _rem = rem;
        _rwyAlt = rwyAlt;
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

        _initialRemaining = RemainingNm();
        AltitudeC = ProfileAltitude(_initialRemaining);
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
        float path = PathNm();
        float rem = RemainingNm();
        float gs = ProfileGs(rem, path);
        return gs < 1f ? 1f : gs;
    }

    private float PathNm()
    {
        if (_rem == null || _rem.Length == 0)
            return 1f;
        return _rem[0] < 0.01f ? 1f : _rem[0];
    }

    private static int CompareStarArrival(OtherAC a, OtherAC b)
    {
        float ta = TimeToRwyHours(a._rem[0], a._rem[0]);
        float tb = TimeToRwyHours(b._rem[0], b._rem[0]);
        int c = ta.CompareTo(tb);
        if (c != 0)
            return c;
        return a._acIndex.CompareTo(b._acIndex);
    }

    private float ProfileAltitude(float rem)
    {
        return ProfileAltitude(rem, PathNm(), _rwyAlt);
    }

    private static float ProfileAltitude(float rem, float path, float rwyAlt)
    {
        rem = Mathf.Max(0f, rem);
        float gateAlt = rwyAlt + GateAboveRwyFt;
        if (rem <= GateNm)
            return rwyAlt + GateAboveRwyFt * (rem / GateNm);

        float span = path - GateNm;
        if (span < 0.01f)
            return gateAlt;
        float startAlt = StarStartAltitudeFt(gateAlt);
        float t = Mathf.Clamp01((rem - GateNm) / span);
        return Mathf.Lerp(gateAlt, startAlt, t);
    }

    private static float StarStartAltitudeFt(float gateAlt)
    {
        float start = 0f;
        if (Calculator.Instance != null && Calculator.Instance.StartAltitude > 0)
            start = (float)Calculator.Instance.StartAltitude;
        else if (Session.OriginalReferenceRoute?.Points != null && Session.OriginalReferenceRoute.Points.Length > 0)
        {
            var alt = Session.OriginalReferenceRoute.Points[0].Altitude;
            if (alt != null && alt.ComputedValue > 0f)
                start = alt.ComputedValue;
        }
        if (start < gateAlt)
            start = gateAlt;
        return start;
    }

    private static float ProfileGs(float rem, float path)
    {
        if (path < 0.01f)
            return RwyGsKt;
        return Mathf.Lerp(RwyGsKt, StarGsKt, Mathf.Clamp01(rem / path));
    }

    private static float TimeToRwyHours(float rem, float path)
    {
        if (rem <= 0f)
            return 0f;
        if (path < 0.01f)
            return rem / RwyGsKt;
        float span = StarGsKt - RwyGsKt;
        float starHours = path * Mathf.Log(StarGsKt / RwyGsKt) / span;
        if (rem <= path)
        {
            float gs = ProfileGs(rem, path);
            if (gs <= RwyGsKt + 0.01f)
                return rem / RwyGsKt;
            return path * Mathf.Log(gs / RwyGsKt) / span;
        }
        return starHours + (rem - path) / StarGsKt;
    }

    private float NextReleaseAtHours()
    {
        float path = PathNm();
        float fromStar = TimeToRwyHours(path, path);
        float gapHours = FinalSpacingNm / RwyGsKt;
        float arrival = _simHours + fromStar;
        if (_all == null)
            return _simHours;
        for (int i = 0; i < _all.Length; i++)
        {
            OtherAC ac = _all[i];
            if (ac == null || ac == this || !ac.HasRoute)
                continue;
            float otherArrival;
            if (ac._released)
                otherArrival = _simHours + TimeToRwyHours(ac.RemainingNm(), ac.PathNm());
            else
                otherArrival = ac._releaseAtHours + TimeToRwyHours(ac.PathNm(), ac.PathNm());
            if (otherArrival + gapHours > arrival)
                arrival = otherArrival + gapHours;
        }
        return arrival - fromStar;
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
        _initialRemaining = _rem[0];
        AltitudeC = ProfileAltitude(_initialRemaining);
        _hidden = false;
        _released = false;
        _releaseAtHours = NextReleaseAtHours();
    }

    private void Hide(Dictionary<string, string> acTexts = null, Dictionary<string, Vector2> acPositions = null)
    {
        _hidden = true;
        if (acPositions != null)
            acPositions.Remove(_aircraftKey);
        if (acTexts != null)
            acTexts.Remove(_aircraftKey);
    }
}
