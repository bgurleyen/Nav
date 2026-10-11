using System.Collections.Generic;
using Navigation;
using UnityEngine;

public class OtherAC
{
    public const float SpacingNm = 10f;
    const float PointCheckNm = 1.3f;

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

    public OtherAC(int acIndex, LevelDataScriptableObject levelData)
    {
        _acIndex = acIndex;
        int levelNo = levelData != null && levelData.levelInfo != null ? levelData.levelInfo.LevelNumber : 0;
        _acItems = BuildItemsFromTxt(levelNo, acIndex + 1);
        _aircraftKey = "AC (" + (acIndex + 1) + ")";
        _ac = FindAc(_aircraftKey);

        if (_acItems == null || _acItems.Length < 2)
        {
            Hide();
            return;
        }

        PlaceForSpacing();
        if (_hidden || _ac == null)
            return;

        _ac.SetActive(true);
        _ac.transform.localPosition = finalPosition;
        ApplyBlackNumber(_ac, (_acIndex + 1).ToString());
    }

    public void Tick(Dictionary<string, string> acTexts, Dictionary<string, Vector2> acPositions)
    {
        if (_hidden || _ac == null || _acItems == null)
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

        _ac.transform.localPosition = finalPosition;
        acPositions[_aircraftKey] = finalPosition;
        string num = (_acIndex + 1).ToString();
        ApplyBlackNumber(_ac, num);
        acTexts[_aircraftKey] = num;
    }

    /// <summary>
    /// AC1..AC7 are ahead of own, AC8..AC10 behind, each slot SpacingNm apart.
    /// Own sits between AC7 and AC8.
    /// </summary>
    private static ACInfo[] BuildItemsFromTxt(int levelNo, int acNumber)
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
                Speed = i == ids.Count - 1 ? 145 : 250
            };
        }
        return items;
    }

    private static float SlotDeltaNm(int acIndex)
    {
        if (acIndex < 7)
            return -SpacingNm * (7 - acIndex);
        return SpacingNm * (acIndex - 6);
    }

    /// <summary>
    /// Set remaining distance to the runway to own's start remaining plus the 10 NM slot.
    /// A short route is extended backward on the first-leg course. A long route starts farther along it.
    /// </summary>
    private void PlaceForSpacing()
    {
        int n = _acItems.Length;
        var pts = new Vector2[n];
        for (int i = 0; i < n; i++)
            pts[i] = Move.Instance.PointPos(_acItems[i].Point);

        var rem = new float[n];
        rem[n - 1] = 0f;
        for (int i = n - 2; i >= 0; i--)
            rem[i] = rem[i + 1] + Vector2.Distance(pts[i], pts[i + 1]);

        float pathLen = rem[0];
        float ownRem = Move.Instance.OwnFlownPathNm();
        if (ownRem < 1f)
            ownRem = Move.Instance.OwnRouteRemaining();
        float desired = ownRem + SlotDeltaNm(_acIndex);

        if (desired <= 0.05f)
        {
            Hide();
            return;
        }

        if (desired > pathLen + 0.05f)
        {
            float extra = desired - pathLen;
            finalPosition = pts[0] - FirstLegDirection(pts) * extra;
            _currentPointIndex = 0;
            AltitudeC = _acItems[0].Altitude;
            return;
        }

        for (int i = 0; i < n - 1; i++)
        {
            if (desired <= rem[i] + 0.05f && desired >= rem[i + 1] - 0.05f)
            {
                float span = rem[i] - rem[i + 1];
                float t = span < 0.0001f ? 0f : (rem[i] - desired) / span;
                finalPosition = Vector2.Lerp(pts[i], pts[i + 1], t);
                _currentPointIndex = i + 1;
                AltitudeC = Mathf.Lerp(_acItems[i].Altitude, _acItems[i + 1].Altitude, t);
                return;
            }
        }

        finalPosition = pts[n - 1];
        _currentPointIndex = n;
        AltitudeC = _acItems[n - 1].Altitude;
    }

    private static Vector2 FirstLegDirection(Vector2[] pts)
    {
        for (int i = 0; i < pts.Length - 1; i++)
        {
            Vector2 leg = pts[i + 1] - pts[i];
            if (leg.sqrMagnitude > 0.0001f)
                return leg.normalized;
        }
        return Vector2.up;
    }

    private static float OwnGroundSpeedKt()
    {
        float ownGs = Calculator.GS > 1f ? Calculator.GS : (float)Calculator.CSpeed;
        return ownGs < 1f ? 1f : ownGs;
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
