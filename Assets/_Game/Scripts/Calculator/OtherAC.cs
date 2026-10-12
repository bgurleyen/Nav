using System.Collections.Generic;
using Navigation;
using UnityEngine;

public class OtherAC
{
    const float PointCheckNm = 1.3f;
    const float SpacingNm = 10f;
    const float OwnHideNm = 3f;
    const float OwnHideFt = 1000f;
    const float OwnShortFinalNm = 15f;
    const float TrafficNearRunwayNm = 3f;

    private ACInfo[] _acItems;
    private string _aircraftKey;
    private int _acIndex;
    private int _currentPointIndex = 1;
    private GameObject _ac;
    private bool _hidden;
    private Vector2 finalPosition;
    private float AltitudeC;
    private int AltitudeR;
    private int PointIndex;
    private Vector2 PtPos;
    private float dx, dy, hyp, x, y;
    private Vector2[] _pts;
    private float[] _rem;
    private float _rwyAlt;
    private float _initialRemaining;

    public OtherAC(int acIndex, LevelDataScriptableObject levelData)
    {
        _acIndex = acIndex;
        int levelNo = levelData != null && levelData.levelInfo != null ? levelData.levelInfo.LevelNumber : 0;
        _acItems = BuildItemsFromTxt(levelNo, acIndex + 1);
        _aircraftKey = "AC (" + (acIndex + 1) + ")";
        _ac = FindAc(_aircraftKey);

        if (_acItems == null || _acItems.Length < 2 || _ac == null)
        {
            Hide();
            return;
        }
    }

    public static void ApplyFormation(OtherAC[] all)
    {
        if (all == null)
            return;

        var flying = new List<OtherAC>();
        int paceSlot = -1;
        for (int i = 0; i < all.Length; i++)
        {
            OtherAC ac = all[i];
            if (ac == null || !ac.HasRoute)
                continue;
            if (ac._acIndex == 6)
                paceSlot = flying.Count;
            flying.Add(ac);
        }

        float own = Move.Instance != null ? Move.Instance.OwnFlownPathNm() : 0f;
        for (int s = 0; s < flying.Count; s++)
        {
            float desired = flying[s]._rem[0];
            if (paceSlot >= 0 && own >= 1f)
                desired = own + SpacingNm * (s - paceSlot);
            if (desired < 0.5f)
                desired = 0.5f;
            flying[s].PlaceForDesired(desired);
            flying[s].Show();
        }
    }

    public void Tick(Dictionary<string, string> acTexts, Dictionary<string, Vector2> acPositions)
    {
        if (_hidden || _acItems == null)
            return;

        if (_currentPointIndex >= _acItems.Length)
        {
            Hide(acTexts, acPositions);
            return;
        }

        float stepLeft = Session.Settings.AircraftTickDistance(OwnGroundSpeedKt());

        if (!acPositions.ContainsKey(_aircraftKey))
        {
            acPositions.Add(_aircraftKey, Vector2.zero);
            acTexts.Add(_aircraftKey, "");
        }

        while (stepLeft > 0.0001f && !_hidden && _currentPointIndex < _acItems.Length)
        {
            PointIndex = _acItems[_currentPointIndex].Point;
            AltitudeR = _acItems[_currentPointIndex].Altitude;
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
                    Hide(acTexts, acPositions);
                    return;
                }
                _currentPointIndex += 1;
                continue;
            }

            float toCheck = hyp - PointCheckNm;
            if (toCheck <= stepLeft)
            {
                finalPosition = new Vector2(x + dx / hyp * toCheck, y + dy / hyp * toCheck);
                AltitudeC -= (AltitudeC - AltitudeR) / hyp * toCheck;
                stepLeft -= toCheck;
                if (_currentPointIndex >= _acItems.Length - 1)
                {
                    Hide(acTexts, acPositions);
                    return;
                }
                _currentPointIndex += 1;
                continue;
            }

            AltitudeC -= (AltitudeC - AltitudeR) / hyp * stepLeft;
            finalPosition = new Vector2(x + dx / hyp * stepLeft, y + dy / hyp * stepLeft);
            stepLeft = 0f;
        }

        if (_hidden)
            return;

        // ND-only filters (TooClose / short final). Keep flying so the editor overlay still shows them.
        if (TooCloseToOwn() || HideTrafficNearRunway())
        {
            SuppressDisplay(acTexts, acPositions);
            return;
        }

        if (!_ac.activeSelf)
            _ac.SetActive(true);
        _ac.transform.localPosition = finalPosition;
        ApplyBlackNumber(_ac, (_acIndex + 1).ToString());
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
        float rangeNm = 80f / zoom;
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
        if (ids.Count < 2)
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
                Altitude = Mathf.Max(0, Mathf.RoundToInt(rwyAlt + rem[i] * 300f)),
                Speed = 0
            };
        }
        _pts = pts;
        _rem = rem;
        _rwyAlt = rwyAlt;
        return items;
    }

    public bool CanReportLive
    {
        get { return _acItems != null && _acItems.Length >= 2; }
    }

    public Vector2 LivePosition
    {
        get { return finalPosition; }
    }

    private bool HasRoute
    {
        get { return _ac != null && _acItems != null && _pts != null && _rem != null && _rem.Length >= 2; }
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
        AltitudeC = _rwyAlt + _initialRemaining * 300f;
    }

    private void Show()
    {
        if (_hidden || _ac == null)
            return;
        _ac.SetActive(true);
        _ac.transform.localPosition = finalPosition;
        ApplyBlackNumber(_ac, (_acIndex + 1).ToString());
        if (TooCloseToOwn() || HideTrafficNearRunway())
            SuppressDisplay();
    }

    private static float OwnGroundSpeedKt()
    {
        float ownGs = Calculator.GS > 1f ? Calculator.GS : (float)Calculator.CSpeed;
        return ownGs < 1f ? 1f : ownGs;
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
        if (_ac == null)
            return;
        var txt = _ac.GetComponent<UnityEngine.UI.Text>();
        if (txt != null)
            txt.text = "";
        if (_ac.activeSelf)
            _ac.SetActive(false);
    }

    private float RemainingNm()
    {
        if (_acItems == null || _currentPointIndex >= _acItems.Length)
            return 0f;
        float rem = Vector2.Distance(finalPosition, Move.Instance.PointPos(_acItems[_currentPointIndex].Point));
        for (int i = _currentPointIndex; i < _acItems.Length - 1; i++)
        {
            rem += Vector2.Distance(
                Move.Instance.PointPos(_acItems[i].Point),
                Move.Instance.PointPos(_acItems[i + 1].Point));
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

    private void Hide(Dictionary<string, string> acTexts = null, Dictionary<string, Vector2> acPositions = null)
    {
        _hidden = true;
        if (acPositions != null)
            acPositions.Remove(_aircraftKey);
        if (acTexts != null)
            acTexts.Remove(_aircraftKey);
        if (_ac == null)
            return;
        var txt = _ac.GetComponent<UnityEngine.UI.Text>();
        if (txt != null)
            txt.text = "";
        if (_ac.activeSelf)
            _ac.SetActive(false);
    }

    private static void ApplyBlackNumber(GameObject go, string num)
    {
        if (go == null)
            return;
        var ui = go.GetComponent<UnityEngine.UI.Text>();
        if (ui != null)
        {
            ui.enabled = true;
            ui.text = num;
            ui.color = Color.black;
            ui.raycastTarget = false;
        }

        var tmps = go.GetComponentsInChildren<TMPro.TMP_Text>(true);
        for (int i = 0; i < tmps.Length; i++)
        {
            var tmp = tmps[i];
            tmp.gameObject.SetActive(true);
            tmp.enabled = true;
            tmp.text = num;
            tmp.color = Color.black;
            tmp.overflowMode = TMPro.TextOverflowModes.Overflow;
            tmp.enableWordWrapping = false;
        }
    }

    private static GameObject FindAc(string name)
    {
        var live = GameObject.Find(name);
        if (live != null)
            return live;
        var all = Resources.FindObjectsOfTypeAll<Transform>();
        for (int i = 0; i < all.Length; i++)
        {
            var t = all[i];
            if (t != null && t.name == name && t.gameObject.scene.IsValid())
                return t.gameObject;
        }
        return null;
    }
}
