using System.Collections;
using System.Collections.Generic;
using Gamelogic.Extensions;
using Navigation;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

[System.Serializable]
public class Move : Singleton<Move>
{
    public static float Perpend, teta;

    public Dictionary<string, Vector2> ACPositions = new Dictionary<string, Vector2>();
    public Dictionary<string, string> ACTexts = new Dictionary<string, string>();

    public Image LOCIndex, GSIndex;

    public Text Atc1, Atc2, Atc3;
    public Text TimerText;


    private float ElapsedTime = 0f;

    private bool NewPoint = true;
    private Vector3 OncekiPos;
    private Vector2[] VirtualPtsPos = new Vector2[100];
    private long ATCAltitude, OncekiAlt;
    private int ATCVS, ATCSpeed;
    /// <summary>Last ATC Speed value that armed Atc3 / XFR3 UI (avoids RawSpeed↔ATCSpeed retrigger).</summary>
    private int _atc3IssuedSpeed;
    private int modD = 0, modS = 0;
    private int RawSpeed = 0;
    private int AltAbove, AltBelow, AltExact;

    /// <summary>ATC speed/altitude penalty already taken from remaining fuel.</summary>
    public double FuelPenaltyKg { get; private set; }
    const float CorrectionWindowSimSeconds = 15f;
    const float LevelStartQuietSimSeconds = 30f;
    const int SpeedToleranceKt = 10;
    const int AltitudeToleranceFt = 150;
    bool _speedLimitActive;
    float _speedWrongSim;
    bool _speedCorrectionSeen;
    double _prevPenaltySpeed;
    bool _prevPenaltySpeedValid;
    float _altWrongSim;
    bool _altCorrectionSeen;
    int _penaltyFrame = -1;
    int _trackedFix = -1;
    float _levelSimTime;

    private LevelDataScriptableObject _currentLevelData;
    private OtherAC[] _otherACs;
    float DistanceToPoint, PrvDistanceToPoint;
    int PrvPoint;

    float PrvTrackToPoint = 0, hyp;
    int prvWptIdx = -1;
    int point = 1, mode, Cmode = 1, VS, VS_nx, Speed, Speed_nx;
    long Altitude;
    string RawAlt = "";
    int _currentInstructionIndex = 0;
    int RW;
    float NextInstructionDistance = 1.3f;

    // BorderGuard: A→B from the last lateral clearance (or a non-early row).
    // Distance gates and reroute aim at _routeB, not the current ATC point.
    Vector2 _routeA;
    Vector2 _routeB;
    bool _routeValid;
    bool _openedByEarlyCall;
    bool _borderLegComplete;
    bool _borderBusy;
    bool _hardActive;
    bool _softClearanceIssued;
    /// <summary>Hard reroute: ATC1 red standby until the next clearance (NewPoint).</summary>
    bool _rerouteStandbyUntilNextClearance;

    bool _atc1GrayDone;
    bool _atc2GrayDone;
    bool _atc3GrayDone;
    bool _clearIlsAwaitingXfr;
    bool _ilsArmOutstanding;
    /// <summary>CLEAR ILS has been issued. Stays set after ARM is pressed.</summary>
    public bool IlsArmCommandIssued { get; private set; }
    Coroutine _armPulse;

    /// <summary>1 normal; 20 during hard catch-up toward B.</summary>
    public static float TurnRateMul { get; private set; } = 1f;
    public static bool IsOutsideBorder { get; private set; }
    /// <summary>Heading change sign that reduces XTE: -1 left, +1 right, 0 unknown.</summary>
    public static float XteReduceHeadingSign { get; private set; }

    public Button XFR1, XFR2, XFR3;
    public TextMeshProUGUI XFR1Txt, XFR2Txt, XFR3Txt;
    public static int XFRSpeed = 0;
    /// <summary>0 exact, 1 or greater, 2 or less. Values above 2 are an early-call distance, treated as exact.</summary>
    public static int XFRSpeedNx = 0;
    public static long XFRAltitude = 0;
    public static int XFRHdg = 0;

    Button _armButton;
    TextMeshProUGUI _armButtonTxt;

    public void Init(LevelDataScriptableObject levelData)
    {
        _currentLevelData = levelData;
        // var somePoint = GameManager.Instance.ActiveSet.Points[3].Clone();

        // Altitude computed: GameManager.Instance.ActiveSet.Points[4].Altitude.ComputedValue
        Vector2 Pos = Vector2.zero;
        var routePoints = Session.OriginalReferenceRoute != null ? Session.OriginalReferenceRoute.Points : null;
        if (routePoints != null && routePoints.Length > 1)
            Pos = Session.OriginalReferenceRoute.GetCartesianPosition(routePoints.Length - 1);

        var vps = _currentLevelData.VirtualPoints;
        if (vps == null || vps.Length == 0)
        {
            Debug.LogWarning("[Move.Init] VirtualPoints incomplete — skipping.");
        }
        else
        {
            float ox = 0f;
            float oy = 0f;
            if (vps.Length > 20)
            {
                ox = vps[20].x;
                oy = vps[20].y;
            }

            int n = vps.Length < 20 ? vps.Length : 20;
            for (int j = 1; j <= n; j++)
            {
                VirtualPtsPos[j].x = Pos.x + vps[j - 1].x - ox;
                VirtualPtsPos[j].y = Pos.y + vps[j - 1].y - oy;
            }
        }

        if (Atc1 != null) Atc1.text = "";
        if (Atc2 != null) Atc2.text = "";
        if (Atc3 != null) Atc3.text = "";
        _atc1GrayDone = false;
        _atc2GrayDone = false;
        _atc3GrayDone = false;
        _clearIlsAwaitingXfr = false;
        _ilsArmOutstanding = false;
        IlsArmCommandIssued = false;
        _atc3IssuedSpeed = 0;
        ResetBorderState();
        _routeValid = false;

        // Clear leftover TMP glow / button state from previous play (shared materials).
        if (XFR1 != null) XFR1.interactable = false;
        if (XFR2 != null) XFR2.interactable = false;
        if (XFR3 != null) XFR3.interactable = false;
        SetXfrGlow(1, false);
        SetXfrGlow(2, false);
        SetXfrGlow(3, false);
        XFRAltitude = 0;
        XFRSpeed = 0;
        XFRSpeedNx = 0;
        XFRHdg = 0;
        ATCAltitude = 0;
        FuelPenaltyKg = 0;
        _speedLimitActive = false;
        _speedWrongSim = 0f;
        _speedCorrectionSeen = false;
        _prevPenaltySpeedValid = false;
        _altWrongSim = 0f;
        _altCorrectionSeen = false;
        _penaltyFrame = -1;
        _trackedFix = -1;
        _levelSimTime = 0f;
        HideArmButton(true);

        _currentInstructionIndex = 0;
        var originPoints = Session.OriginalReferenceRoute != null ? Session.OriginalReferenceRoute.Points : null;
        RW = originPoints != null ? originPoints.Length - 1 : 0;
        OwnAtcRemainingAtStart = AtcInstructionsRemaining();

        _otherACs = new OtherAC[10];
        for (var i = 0; i < _otherACs.Length; i++)
        {
            _otherACs[i] = new OtherAC(i, _currentLevelData);
        }
        OtherAC.ApplyFormation(_otherACs);
    }


    public void Tick()
    {
        if (_otherACs != null)
        {
            for (int i = 0; i < _otherACs.Length; i++)
            {
                if (_otherACs[i] != null)
                    _otherACs[i].Tick(ACTexts, ACPositions);
            }
        }

        CheckAirplaneMove();
    }

    void SlowDown()
    {
        if (Session.State != null)
            Session.State.Speed10X.Set(false);
        if (Session.Settings != null)
            Session.Settings.SpeedMultiplier = 1;
    }
    private string TurnDirection(float newHdg)
    {

        return (Mathf.DeltaAngle(Calculator.CTrack, newHdg) >= 0) ? "Right " : "Left ";
    }

    private string NxToString(int nx)
    {
        return (nx == 1) ? " or greater " : (nx == 2) ? " or less " : "";
    }

    /*
     * BORDERGUARD
     * -----------
     * 1) DCT, heading, or a row that was not opened early defines route A→B.
     *    An early call (Speed_nx > 2 on this row or the previous one) keeps the previous B
     *    unless this row itself has DCT or heading.
     * 2) Soft: cone from B abeam ±1.3 NM toward A at 13°.
     * 3) Hard: same abeam anchors, 26° cone toward A.
     * 4) Soft breach → one FactoredHeading toward _routeB.
     * 5) Hard breach (26° cone OR dist(_routeB) > |A-B|+5 NM) → dialog, one-shot track to _routeB at 20x.
     *    After dist(_routeB) ≤ 1.3 NM the cone stays quiet until the next capture.
     *    ATC1 red "Rerouting , Standby!!" until next clearance. No turn-in-progress skip.
     *    Turn direction always reduces XTE (Pilot).
     */
    void BorderGuard()
    {
        const float abeamNm = 1.3f;
        const float softDeg = 13f;
        const float hardDeg = 26f;
        const float hardGateNm = 1.3f;
        const float beyondAbExtraNm = 5f;

        if (Session.State != null && Session.State.LOCCaptured)
        {
            CancelRerouteForLocCapture();
            return;
        }

        if (_borderBusy || Session.PlayerAircraft == null || !_routeValid)
            return;

        if (!TryGetRouteXte(out float signedXte, out float alongFromBTowardA, out float routeBrg))
            return;

        float xte = Mathf.Abs(signedXte);
        float softHalf = abeamNm + Mathf.Max(0f, alongFromBTowardA) * Mathf.Tan(softDeg * Mathf.Deg2Rad);
        float hardHalf = abeamNm + Mathf.Max(0f, alongFromBTowardA) * Mathf.Tan(hardDeg * Mathf.Deg2Rad);
        float abLen = Vector2.Distance(_routeA, _routeB);
        float distToRouteB = Vector2.Distance(Session.PlayerAircraft.NMPosition, _routeB);
        bool beyondAb = distToRouteB > abLen + beyondAbExtraNm;

        if (distToRouteB <= hardGateNm)
            _borderLegComplete = true;

        // string modeName = _hardActive || xte > hardHalf || beyondAb
        //     ? "Hard"
        //     : (xte > softHalf ? "Soft" : "Normal");
        // Debug.Log($"Mode={modeName}, XTE={xte:F2}, DistB={DistanceToPoint:F2}");

        // signedXte > 0 (right of A→B) → turn left to reduce.
        XteReduceHeadingSign = signedXte > 0.05f ? -1f : signedXte < -0.05f ? 1f : 0f;
        Perpend = signedXte > 0f ? routeBrg + 90f : routeBrg - 90f;
        teta = Mathf.Atan2(xte - hardHalf, Mathf.Max(0.1f, alongFromBTowardA)) * Mathf.Rad2Deg;

        if (_rerouteStandbyUntilNextClearance)
            SetAtcReroutingStandby();

        // B has been reached. Stay quiet until the next cone, so the leg after an early call does not warn.
        if (_borderLegComplete)
        {
            if (_hardActive)
                EndHardMode();
            ClearOutsideBorderSteer();
            _softClearanceIssued = false;
            return;
        }

        if (_hardActive)
        {
            // Hard: keep 20x turn toward the TrackToPoint captured at hard start (no recompute).
            SlowDown();
            IsOutsideBorder = true;
            TurnRateMul = 20f;
            return;
        }

        if (xte > hardHalf || beyondAb)
        {
            int directHdg = Calculator.NormalizeHeading360((int)Mathf.Round(TrackToPosition(_routeB)));
            IssueHeadingCorrection(
                directHdg,
                title: "PILOT RESPONSE",
                body: "Excessive Deviation\nStandby for next clearence\nAuto Rerouting!!");
            return;
        }

        // Soft: one FactoredHeading clearance per soft-cone entry.
        if (xte > softHalf)
        {
            if (!_softClearanceIssued)
            {
                _softClearanceIssued = true;
                SlowDown();
                RefreshAtcFactoredClearance();
            }
        }
        else
        {
            _softClearanceIssued = false;
        }
    }

    /// <summary>
    /// Signed XTE relative to A→B (positive = right of track), distance from B toward A along route, route bearing.
    /// </summary>
    bool TryGetRouteXte(out float signedXte, out float alongFromBTowardA, out float routeBrg)
    {
        signedXte = 0f;
        alongFromBTowardA = 0f;
        routeBrg = 0f;

        Vector2 ab = _routeB - _routeA;
        float abLen = ab.magnitude;
        if (abLen < 0.01f)
            return false;

        Vector2 abDir = ab / abLen;
        Vector2 ac = Session.PlayerAircraft.NMPosition - _routeA;
        // Right-of-track: rotate abDir 90° clockwise in (x=East, y=North).
        signedXte = ac.x * abDir.y - ac.y * abDir.x;
        alongFromBTowardA = Vector2.Dot(Session.PlayerAircraft.NMPosition - _routeB, -abDir);
        routeBrg = Mathf.Atan2(ab.x, ab.y) * Mathf.Rad2Deg;
        if (routeBrg < 0f) routeBrg += 360f;
        return true;
    }

    void CaptureRouteAB()
    {
        if (Session.PlayerAircraft == null)
        {
            _routeValid = false;
            return;
        }

        _routeA = Session.PlayerAircraft.NMPosition;
        _routeB = PointPos(point);
        _routeValid = true;
    }

    void ResetBorderState()
    {
        _borderBusy = false;
        _hardActive = false;
        _softClearanceIssued = false;
        _borderLegComplete = false;
        _rerouteStandbyUntilNextClearance = false;
        ClearOutsideBorderSteer();
    }

    /// <summary>LOC capture cancels hard/soft reroute: no 20x turn, no standby ATC1, no reroute dialog callback.</summary>
    public void CancelRerouteForLocCapture()
    {
        bool dismissRerouteDialog = _borderBusy;
        bool wasRerouting = _hardActive || _rerouteStandbyUntilNextClearance || _borderBusy || IsOutsideBorder;

        ResetBorderState();

        if (dismissRerouteDialog)
            ApproachStatusDialog.DismissWithoutCallback();

        if (!wasRerouting || Atc1 == null || string.IsNullOrEmpty(Atc1.text))
            return;

        if (Atc1.text.IndexOf("Rerouting", StringComparison.OrdinalIgnoreCase) < 0)
            return;

        Atc1.text = "";
        Atc1.color = Color.gray;
        _atc1GrayDone = true;
        if (XFR1 != null) XFR1.interactable = false;
        SetXfrGlow(1, false);
    }

    void EndHardMode()
    {
        _hardActive = false;
        ClearOutsideBorderSteer();
    }

    void SetAtcReroutingStandby()
    {
        if (Atc1 == null)
            return;

        Atc1.color = Color.red;
        Atc1.text = "Rerouting , Standby!!";
        _atc1GrayDone = false;
        SetXfrGlow(1, false);
    }

    void RefreshAtcFactoredClearance()
    {
        int hdg = Calculator.NormalizeHeading360(TrackToPositionFactored(_routeB));
        XFRHdg = hdg;
        mode = 2;
        XFR1.interactable = true;

        if (Atc1 == null)
            return;

        Atc1.text = "Turn " + TurnDirection(TrackToPosition(_routeB))
                    + "Heading " + hdg;
        Atc1.color = Color.green;
        _atc1GrayDone = false;
    }

    void SetXfrGlow(int channel, bool glow)
    {
        TextMeshProUGUI txt = channel == 1 ? XFR1Txt : channel == 2 ? XFR2Txt : XFR3Txt;
        if (txt != null && txt.fontMaterial != null)
            txt.fontMaterial.SetFloat(ShaderUtilities.ID_GlowPower, glow ? 1f : 0f);
    }

    /// <summary>ATC text gray + XFR matte — XFR click, or DCT already first on LEGS.</summary>
    public void AcknowledgeAtc1()
    {
        if (_atc1GrayDone || Atc1 == null || string.IsNullOrEmpty(Atc1.text))
            return;
        // Keep "Rerouting , Standby!!" red until the next clearance.
        if (_rerouteStandbyUntilNextClearance)
            return;
        _atc1GrayDone = true;
        Atc1.color = Color.gray;
        if (XFR1 != null) XFR1.interactable = false;
        SetXfrGlow(1, false);
    }

    void TryGrayAtc1IfLegsMatches()
    {
        if (_atc1GrayDone || _rerouteStandbyUntilNextClearance || mode != 1)
            return;
        if (Atc1 == null || string.IsNullOrEmpty(Atc1.text))
            return;

        var atcName = GetAtc1WaypointName();
        if (string.IsNullOrEmpty(atcName))
            return;

        if (WaypointNamesEqual(atcName, FirstLegsWaypointName(Session.VisibleRoute))
            || WaypointNamesEqual(atcName, FirstLegsWaypointName(Session.ActiveRoute)))
            AcknowledgeAtc1();
    }

    string GetAtc1WaypointName()
    {
        if (Atc1 != null && !string.IsNullOrEmpty(Atc1.text))
        {
            const string prefix = "Proceed direct to";
            var text = Atc1.text.Trim();
            if (text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return text.Substring(prefix.Length).Trim();
        }

        var points = Session.OriginalReferenceRoute?.Points;
        if (points == null || point < 0 || point >= points.Length)
            return "";

        return (points[point].Name ?? "").Trim();
    }

    static string FirstLegsWaypointName(RouteScriptableObject route)
    {
        if (route?.Points == null || route.Points.Length == 0)
            return "";

        int start = 0;
        if (Session.PlayerAircraft != null)
            start = Mathf.Clamp(PositionVirtualNode.NextNodeIndex, 0, route.Points.Length - 1);

        for (int i = start; i < route.Points.Length; i++)
        {
            var p = route.Points[i];
            if (!IsDisplayedLegsWaypoint(p))
                continue;
            return p.Name.Trim();
        }

        return "";
    }

    static bool IsDisplayedLegsWaypoint(RoutePoint p)
    {
        if (p == null || p.IsSkippable || string.IsNullOrEmpty(p.Name))
            return false;
        if (p.Name == "_START_" || p.Name == "_Position_")
            return false;
        return true;
    }

    static bool WaypointNamesEqual(string a, string b)
    {
        if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b))
            return false;
        return string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    public void AcknowledgeAtc2()
    {
        if (_atc2GrayDone || Atc2 == null || string.IsNullOrEmpty(Atc2.text))
            return;
        _atc2GrayDone = true;
        Atc2.color = Color.gray;
        if (XFR2 != null) XFR2.interactable = false;
        SetXfrGlow(2, false);

        if (_clearIlsAwaitingXfr)
        {
            _clearIlsAwaitingXfr = false;
            ShowArmButton();
        }
    }

    public void AcknowledgeAtc3()
    {
        if (_atc3GrayDone || Atc3 == null || string.IsNullOrEmpty(Atc3.text))
            return;
        _atc3GrayDone = true;
        Atc3.color = Color.gray;
        if (XFR3 != null) XFR3.interactable = false;
        SetXfrGlow(3, false);
    }

    void EnsureArmButton()
    {
        if (_armButton != null)
            return;
        if (XFR3 == null || Atc2 == null)
            return;

        var go = Instantiate(XFR3.gameObject, Atc2.transform);
        go.name = "ARM";
        var rt = go.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.anchoredPosition = new Vector2(365f, 0f);
            rt.sizeDelta = new Vector2(
                Mathf.Max(rt.sizeDelta.x, 120f),
                Mathf.Max(rt.sizeDelta.y, 48f));
        }

        _armButton = go.GetComponent<Button>();
        _armButton.onClick = new Button.ButtonClickedEvent();
        _armButton.onClick.AddListener(OnArmApproachClicked);
        _armButton.interactable = true;

        var colors = _armButton.colors;
        colors.normalColor = new Color(1f, 0.78f, 0.08f, 1f);
        colors.highlightedColor = new Color(1f, 0.92f, 0.25f, 1f);
        colors.pressedColor = new Color(1f, 0.55f, 0f, 1f);
        colors.selectedColor = colors.highlightedColor;
        _armButton.colors = colors;

        var img = go.GetComponent<Image>();
        if (img != null)
        {
            img.enabled = true;
            img.color = new Color(1f, 0.78f, 0.08f, 1f);
        }

        _armButtonTxt = go.GetComponentInChildren<TextMeshProUGUI>(true);
        if (_armButtonTxt != null)
        {
            _armButtonTxt.text = "ARM";
            _armButtonTxt.fontStyle = FontStyles.Bold;
            _armButtonTxt.fontSize = Mathf.Max(_armButtonTxt.fontSize * 1.35f, 30f);
            _armButtonTxt.color = Color.black;
            _armButtonTxt.enableVertexGradient = false;

            var mat = new Material(_armButtonTxt.fontMaterial);
            mat.EnableKeyword("GLOW_ON");
            mat.SetFloat(ShaderUtilities.ID_GlowPower, 1f);
            mat.SetColor(ShaderUtilities.ID_GlowColor, new Color(1f, 0.95f, 0.2f, 1f));
            mat.SetFloat(ShaderUtilities.ID_GlowOffset, 0.2f);
            mat.SetFloat(ShaderUtilities.ID_GlowOuter, 0.75f);
            _armButtonTxt.fontMaterial = mat;
        }

        go.SetActive(false);
    }

    void ShowArmButton()
    {
        if (Session.State != null && Session.State.AppArmed)
            return;

        EnsureArmButton();
        if (_armButton == null)
            return;

        _armButton.gameObject.SetActive(true);
        _armButton.interactable = true;
        if (_armButtonTxt != null)
            _armButtonTxt.fontMaterial.SetFloat(ShaderUtilities.ID_GlowPower, 1f);

        if (_armPulse != null)
            StopCoroutine(_armPulse);
        _armPulse = StartCoroutine(PulseArmButton());
        SlowDown();
    }

    void HideArmButton(bool force = false)
    {
        if (!force && _armButton != null && _armButton.gameObject.activeSelf && (Session.State == null || !Session.State.AppArmed))
            return;

        if (_armPulse != null)
        {
            StopCoroutine(_armPulse);
            _armPulse = null;
        }

        if (_armButton == null)
            return;

        if (_armButtonTxt != null)
            _armButtonTxt.fontMaterial.SetFloat(ShaderUtilities.ID_GlowPower, 0f);
        _armButton.gameObject.SetActive(false);
    }

    IEnumerator PulseArmButton()
    {
        var rt = _armButton != null ? _armButton.GetComponent<RectTransform>() : null;
        while (_armButton != null && _armButton.gameObject.activeSelf)
        {
            float t = (Mathf.Sin(Time.unscaledTime * 7f) + 1f) * 0.5f;
            if (rt != null)
                rt.localScale = Vector3.one * Mathf.Lerp(1.25f, 1.55f, t);
            if (_armButtonTxt != null)
                _armButtonTxt.color = Color.Lerp(Color.black, new Color(0.45f, 0.12f, 0f), t);
            yield return null;
        }
    }

    public void OnArmApproachClicked()
    {
        if (Session.State == null || Session.State.AppArmed)
            return;

        Session.State.AppArmed = true;
        _ilsArmOutstanding = false;
        CancelInvoke(nameof(DescentCheck));
        HideArmButton(true);

        if (Atc2 != null)
        {
            Atc2.text = "LOC/GS Armed";
            Atc2.color = Color.white;
            _atc2GrayDone = true;
        }
    }

    public void NotifyLocCaptured()
    {
        if (Atc2 == null)
            return;
        Atc2.text = "LOC captured";
        Atc2.color = Color.white;
        _atc2GrayDone = true;
    }

    public void NotifyGsCaptured()
    {
        if (Atc2 == null)
            return;
        Atc2.text = "LOC/GS Captured , set MA altitude " + MissedApproachAltitudeFeet();
        Atc2.color = Color.white;
        _atc2GrayDone = true;
    }

    static void ClearOutsideBorderSteer()
    {
        TurnRateMul = 1f;
        IsOutsideBorder = false;
        XteReduceHeadingSign = 0f;
    }

    void IssueHeadingCorrection(int hdg, string title, string body)
    {
        _borderBusy = true;
        _hardActive = true;
        _rerouteStandbyUntilNextClearance = true;
        IsOutsideBorder = true;
        TurnRateMul = 20f;
        SetAtcReroutingStandby();
        SlowDown();

        ApproachStatusDialog.Show(title, body, "OK", () =>
        {
            if (Session.State != null && Session.State.LOCCaptured)
                return;

            _borderBusy = false;

            Calculator.RHeading = Calculator.NormalizeHeading360(hdg);
            Calculator.Instance?.AddWindEffectToRHeading();

            if (Session.State != null)
                Session.State.AutoSetHDG(true);

            Calculator.Instance?.UpdatePFD();
            mode = 2;
            XFRHdg = hdg;
            if (XFR1 != null)
                XFR1.interactable = false;
            SetAtcReroutingStandby();
        });
    }


    public Vector2 PointPos(int pt)
    {
        if (pt < 50)
        {
            if (Session.OriginalReferenceRoute == null)
                return Vector2.zero;
            return Session.OriginalReferenceRoute.GetCartesianPosition(pt);
        }

        int virtualIndex = pt - 50;
        if (virtualIndex < 0 || virtualIndex >= VirtualPtsPos.Length)
            return Vector2.zero;
        return VirtualPtsPos[virtualIndex];
    }

    /// <summary>Own remaining NM at game start along ATC instruction points.</summary>
    public float OwnAtcRemainingAtStart { get; private set; }

    /// <summary>NM left along ATC instruction waypoints to the runway.</summary>
    public float AtcInstructionsRemaining()
    {
        if (_currentLevelData?.ATCs == null || _currentLevelData.ATCs.Length == 0)
            return OwnRouteRemaining();

        Vector2 prev = Session.PlayerAircraft != null ? Session.PlayerAircraft.NMPosition : Vector2.zero;
        float rem = 0f;
        int lastPt = -1;
        int start = Mathf.Max(0, _currentInstructionIndex);
        for (int i = start; i < _currentLevelData.ATCs.Length; i++)
        {
            int pt = _currentLevelData.ATCs[i].point;
            if (pt <= 0 || pt == lastPt)
                continue;
            Vector2 p = PointPos(pt);
            rem += Vector2.Distance(prev, p);
            prev = p;
            lastPt = pt;
        }
        if (RW > 0 && lastPt != RW)
            rem += Vector2.Distance(prev, PointPos(RW));
        return rem;
    }

    /// <summary>NM own flies from route point 0 to the runway on the clearance path.</summary>
    public float OwnFlownPathNm()
    {
        var route = Session.OriginalReferenceRoute;
        if (route?.Points == null || route.Points.Length < 2)
            return 0f;
        List<int> ids = BuildOwnPathIds(route);
        Vector2 prev = route.GetCartesianPosition(0);
        float len = 0f;
        for (int i = 0; i < ids.Count; i++)
        {
            Vector2 p = PointPos(ids[i]);
            float d = Vector2.Distance(prev, p);
            if (d >= 0.08f)
                len += d;
            prev = p;
        }
        return len;
    }

    private List<int> BuildOwnPathIds(RouteScriptableObject route)
    {
        var ids = new List<int>();
        int rwId = route.Points.Length - 1;
        var atc = _currentLevelData != null ? _currentLevelData.ATCs : null;
        if (atc == null || atc.Length == 0)
        {
            for (int i = 1; i <= rwId; i++)
                ids.Add(i);
            return ids;
        }
        for (int ai = 0; ai < atc.Length; ai++)
        {
            ATCInstructionInfo a = atc[ai];
            bool shortcut = a.mode == 1 || a.mode == 2;
            if (!shortcut && a.point < 50)
            {
                int cur = -1;
                for (int k = ids.Count - 1; k >= 0; k--)
                {
                    if (ids[k] < 50)
                    {
                        cur = ids[k];
                        break;
                    }
                }
                int curIdx = cur >= 0 && cur < route.Points.Length ? cur : -1;
                int tgtIdx = a.point >= 0 && a.point < route.Points.Length ? a.point : -1;
                if (curIdx >= 0 && tgtIdx > curIdx)
                {
                    for (int i = curIdx + 1; i <= tgtIdx; i++)
                        PushOwnId(ids, i);
                }
                else
                    PushOwnId(ids, a.point);
            }
            else
                PushOwnId(ids, a.point);

            if (a.mode == 3 || a.VS_nx == 3)
            {
                int last = ids.Count > 0 ? ids[ids.Count - 1] : -1;
                if (last >= 0 && last < 50 && last < route.Points.Length && !RemainingHasReversal(route, last))
                {
                    for (int i = last + 1; i < route.Points.Length; i++)
                        PushOwnId(ids, i);
                }
                else
                    PushOwnId(ids, rwId);
                break;
            }
        }
        if (ids.Count == 0 || ids[ids.Count - 1] != rwId)
            PushOwnId(ids, rwId);
        return ids;
    }

    private static void PushOwnId(List<int> ids, int id)
    {
        if (id == 0)
            return;
        if (ids.Count == 0 || ids[ids.Count - 1] != id)
            ids.Add(id);
    }

    private static bool RemainingHasReversal(RouteScriptableObject route, int fromIdx)
    {
        int last = route.Points.Length - 1;
        Vector2 rw = route.GetCartesianPosition(last);
        for (int i = fromIdx + 1; i < last; i++)
        {
            Vector2 a = route.GetCartesianPosition(i - 1);
            Vector2 b = route.GetCartesianPosition(i);
            float along = Mathf.Atan2(b.x - a.x, b.y - a.y) * Mathf.Rad2Deg;
            float toRw = Mathf.Atan2(rw.x - b.x, rw.y - b.y) * Mathf.Rad2Deg;
            if (Mathf.Abs(Mathf.DeltaAngle(along, toRw)) > 50f)
                return true;
        }
        return false;
    }

    /// <summary>NM left along the flown STAR/active route to the runway.</summary>
    public float OwnRouteRemaining()
    {
        var route = Session.ActiveRoute ?? Session.OriginalReferenceRoute;
        if (route?.Points == null || route.Points.Length < 2)
            return 0f;

        Vector2 pos = Session.PlayerAircraft != null ? Session.PlayerAircraft.NMPosition : Vector2.zero;
        float bestSqr = float.MaxValue;
        int bestSeg = 1;
        float bestT = 0f;
        for (int i = 1; i < route.Points.Length; i++)
        {
            Vector2 a = route.GetCartesianPosition(i - 1);
            Vector2 b = route.GetCartesianPosition(i);
            Vector2 ab = b - a;
            float abLenSq = ab.sqrMagnitude;
            float t = abLenSq < 1e-8f ? 0f : Mathf.Clamp01(Vector2.Dot(pos - a, ab) / abLenSq);
            Vector2 proj = a + ab * t;
            float sqr = (pos - proj).sqrMagnitude;
            if (sqr < bestSqr)
            {
                bestSqr = sqr;
                bestSeg = i;
                bestT = t;
            }
        }

        Vector2 segA = route.GetCartesianPosition(bestSeg - 1);
        Vector2 segB = route.GetCartesianPosition(bestSeg);
        Vector2 at = Vector2.Lerp(segA, segB, bestT);
        float rem = Vector2.Distance(at, segB);
        for (int i = bestSeg + 1; i < route.Points.Length; i++)
            rem += Vector2.Distance(route.GetCartesianPosition(i - 1), route.GetCartesianPosition(i));
        return rem;
    }

    private float TrackToPoint(int pt)
    {
        return TrackToPosition(PointPos(pt));
    }

    private float TrackToPosition(Vector2 dest)
    {
        return Bearing(Session.PlayerAircraft.NMPosition, dest);
    }

    private static float Bearing(Vector2 from, Vector2 dest)
    {
        float angle = Mathf.Atan2(dest.x - from.x, dest.y - from.y) * Mathf.Rad2Deg;
        if (angle < 0f) angle += 360f;
        return angle;
    }
    private float TrackToPoint(float x1, float y1, int pt)
    {

        float x2 = PointPos(pt).x;
        float y2 = PointPos(pt).y;

        float Angle = Mathf.Atan2(x2 - x1, y2 - y1) * Mathf.Rad2Deg;
        if (Angle < 0) Angle += 360;
        return Angle;
    }

    private int TrackToPointFactored(int pt)
    {
        return TrackToPositionFactored(PointPos(pt));
    }

    private int TrackToPositionFactored(Vector2 dest)
    {
        Vector2 from = Session.PlayerAircraft.NMPosition;
        float direct = Bearing(from, dest);
        float alfa = Mathf.DeltaAngle(Calculator.CTrack, direct) * Mathf.Deg2Rad;
        float track = Calculator.CTrack * Mathf.Deg2Rad;
        float turnRadius = 1.6f * Calculator.GS / 280f;
        int sign = Mathf.DeltaAngle(Calculator.CTrack, direct) >= 0 ? 1 : -1;

        float h = turnRadius * (1f - Mathf.Cos(alfa));
        float v = Mathf.Sin(alfa) * turnRadius;
        float x2 = from.x + sign * (h * Mathf.Cos(track) + v * Mathf.Sin(track));
        float y2 = from.y + sign * (v * Mathf.Cos(track) - h * Mathf.Sin(track));

        float leadTrack = Bearing(new Vector2(x2, y2), dest);
        Calculator.WindElements we = Calculator.CalculateWindElements(Calculator.CAltitude, Calculator.CSpeed, (int)leadTrack);
        return (int)(Mathf.Round(leadTrack) + we.HeadingWindAddition);
    }

    public float DME()
    {
        return Vector2.Distance(Session.PlayerAircraft.NMPosition, PointPos(RW));
    } // Distance from RW

    /// <summary>Lateral distance from the final approach course, NM.</summary>
    public float FinalApproachCrossTrackNm()
    {
        var levelInfo = Session.CurrentLevel?.levelInfo;
        if (levelInfo == null || Session.PlayerAircraft == null)
            return 0f;

        ComputeLocTrackErrors(levelInfo.Course, out _, out float crossTrackNm);
        return Mathf.Abs(crossTrackNm);
    }

    /// <summary>
    /// True when aircraft heading is within ±tolerance of inbound final (levelInfo.Course).
    /// Overflying the runway off-axis must not end the level.
    /// </summary>
    public bool IsWithinFinalApproachCourse(float toleranceDeg = 20f)
    {
        var levelInfo = Session.CurrentLevel?.levelInfo;
        if (levelInfo == null || Session.PlayerAircraft == null)
            return false;

        return Mathf.Abs(Mathf.DeltaAngle(levelInfo.Course, Session.PlayerAircraft.HeadingDegrees)) <= toleranceDeg;
    }

    /// <summary>Runway threshold elevation (ft MSL) from the last route point.</summary>
    public float RunwayAltitudeFeet()
    {
        var points = Session.OriginalReferenceRoute?.Points;
        if (points == null || RW < 0 || RW >= points.Length)
            return 0f;

        float alt = (float)points[RW].Altitude.ComputedValue;
        if (alt < 0f
            && !string.IsNullOrEmpty(points[RW].RawAltitude)
            && float.TryParse(points[RW].RawAltitude, out float raw))
            alt = raw;

        return alt < 0f ? 0f : alt;
    }

    /// <summary>Missed-approach altitude: runway elevation + 3000 ft, floored to 1000 ft.</summary>
    public int MissedApproachAltitudeFeet()
    {
        return Mathf.FloorToInt((RunwayAltitudeFeet() + 3000f) / 1000f) * 1000;
    }

    // LOC/GS capture thresholds (simple, level-independent)
    public const float LocCaptureDegrees = 2.5f;
    public const float LocCaptureDmeMinNm = 3f;
    public const float LocCaptureDmeMaxNm = 25f;
    public const float GsCaptureAltBandFt = 150f;

    /// <summary>True when APP armed and aircraft is within LOC capture window.</summary>
    public bool CanCaptureLoc(float course)
    {
        if (!Session.State.AppArmed || Session.State.LOCCaptured)
            return false;

        float dme = DME();
        if (dme < LocCaptureDmeMinNm || dme > LocCaptureDmeMaxNm)
            return false;

        float hdgToCourse = Mathf.Abs(Mathf.DeltaAngle(course, Session.PlayerAircraft.HeadingDegrees));
        if (hdgToCourse > 135f)
            return false;

        return Mathf.Abs(ComputeLocDeviationDegrees(course)) <= LocCaptureDegrees;
    }

    /// <summary>True when LOC captured and within GS altitude capture band.</summary>
    public bool CanCaptureGs(float glideSlopeDegrees)
    {
        if (!Session.State.LOCCaptured || Session.State.GSCaptured)
            return false;

        float dme = DME();
        if (dme < 0.5f || dme > LocCaptureDmeMaxNm)
            return false;

        return Mathf.Abs(GsAltitudeDeviation(glideSlopeDegrees)) <= GsCaptureAltBandFt;
    }

    void DisarmSpeedPenalty()
    {
        _speedLimitActive = false;
        _speedWrongSim = 0f;
        _speedCorrectionSeen = false;
        _prevPenaltySpeedValid = false;
    }

    void BeginSpeedGrace()
    {
        _speedLimitActive = true;
        _speedWrongSim = 0f;
        _speedCorrectionSeen = false;
        _prevPenaltySpeedValid = false;
    }

    void BeginAltitudeGrace()
    {
        _altWrongSim = 0f;
        _altCorrectionSeen = false;
    }

    /// <summary>
    /// No penalty for the first 30s of 1x time. After that, wrong speed or altitude
    /// has 15s to start correcting the right way. If that does not start or does not continue, 1 kg per 1x second.
    /// </summary>
    void AdvancePenaltyGrace()
    {
        if (Time.frameCount == _penaltyFrame)
            return;
        _penaltyFrame = Time.frameCount;
        if (LevelTestMode.IsActive || Session.Settings == null)
            return;

        float simDt = Time.deltaTime * Session.Settings.SpeedMultiplier;
        if (simDt <= 0f)
            return;

        _levelSimTime += simDt;
        if (_levelSimTime < LevelStartQuietSimSeconds)
            return;

        bool penalize = TickSpeedPenalty(simDt) || TickAltitudePenalty(simDt);
        if (penalize)
            ChargePenaltyKg(simDt);
    }

    bool TickSpeedPenalty(float simDt)
    {
        bool wrong = SpeedWrong(out int direction);
        bool correcting = false;
        if (wrong && _prevPenaltySpeedValid)
        {
            double delta = Calculator.CSpeed - _prevPenaltySpeed;
            correcting = (direction > 0 && delta > 0d) || (direction < 0 && delta < 0d);
        }

        _prevPenaltySpeed = Calculator.CSpeed;
        _prevPenaltySpeedValid = true;
        return TrackCorrection(wrong, correcting, ref _speedWrongSim, ref _speedCorrectionSeen, simDt);
    }

    bool TickAltitudePenalty(float simDt)
    {
        bool wrong = AltitudeWrong(out int direction);
        bool correcting = wrong && ((direction > 0 && Calculator.CVS > 0) || (direction < 0 && Calculator.CVS < 0));
        return TrackCorrection(wrong, correcting, ref _altWrongSim, ref _altCorrectionSeen, simDt);
    }

    static bool TrackCorrection(bool wrong, bool correcting, ref float wrongSim, ref bool seen, float simDt)
    {
        if (!wrong)
        {
            wrongSim = 0f;
            seen = false;
            return false;
        }

        wrongSim += simDt;
        if (correcting)
            seen = true;

        bool startedTooLate = wrongSim >= CorrectionWindowSimSeconds && !seen;
        bool stopped = seen && !correcting;
        return startedTooLate || stopped;
    }

    void ChargePenaltyKg(double kilograms)
    {
        if (kilograms <= 0)
            return;

        double fuelKg = Calculator.totalFuel * 10.0;
        if (fuelKg <= 0)
            return;
        if (kilograms > fuelKg)
            kilograms = fuelKg;

        FuelPenaltyKg += kilograms;
        Calculator.totalFuel -= kilograms / 10.0;
    }

    int SpeedMode()
    {
        if (modS == 1 || modS == 2)
            return modS;
        return 0;
    }

    bool SpeedWrong(out int direction)
    {
        direction = 0;
        if (!_speedLimitActive || ATCSpeed <= 0)
            return false;

        int speed = (int)Calculator.CSpeed;
        int limit = ATCSpeed;
        if (SpeedMode() == 1)
        {
            if (speed >= limit - SpeedToleranceKt)
                return false;
            direction = 1;
            return true;
        }

        if (SpeedMode() == 2)
        {
            if (speed <= limit + SpeedToleranceKt)
                return false;
            direction = -1;
            return true;
        }

        if (Math.Abs(speed - limit) <= SpeedToleranceKt)
            return false;

        direction = speed < limit ? 1 : -1;
        return true;
    }

    bool AltitudeWrong(out int direction)
    {
        direction = 0;
        int altitude = (int)Calculator.CAltitude;
        bool tooHigh = false;
        bool tooLow = false;
        NoteAltitudeSide(altitude, -1, -1, ATCAltitude > 0 ? (int)ATCAltitude : -1, ref tooHigh, ref tooLow);
        if (TryReadFixLimits(point, out int above, out int below, out int exact))
            NoteAltitudeSide(altitude, above, below, exact, ref tooHigh, ref tooLow);

        if (tooHigh && !tooLow)
        {
            direction = -1;
            return true;
        }

        if (tooLow && !tooHigh)
        {
            direction = 1;
            return true;
        }

        return tooHigh;
    }

    static void NoteAltitudeSide(int altitude, int above, int below, int exact, ref bool tooHigh, ref bool tooLow)
    {
        if (below > 0 && altitude > below + AltitudeToleranceFt)
            tooHigh = true;
        if (above > 0 && altitude < above - AltitudeToleranceFt)
            tooLow = true;
        if (exact > 0 && altitude > exact + AltitudeToleranceFt)
            tooHigh = true;
        if (exact > 0 && altitude < exact - AltitudeToleranceFt)
            tooLow = true;
    }

    bool TryReadFixLimits(int fix, out int above, out int below, out int exact)
    {
        above = below = exact = -1;
        if (fix <= 1 || fix >= 50)
            return false;

        var points = Session.OriginalReferenceRoute?.Points;
        if (points == null || fix >= points.Length)
            return false;

        DataHandler.ParseAltRegulation(points[fix].RawAltitude ?? "", out above, out below, out exact);
        return above > 0 || below > 0 || exact > 0;
    }

    void TrackAltitudeFix()
    {
        if (point <= 1 || point >= 50 || point == _trackedFix)
            return;

        _trackedFix = point;
        if (TryReadFixLimits(point, out _, out _, out _))
            BeginAltitudeGrace();
    }

    void ATCCall()
    //mode              pt  Alt VS  nx          Speed   nx
    //0..NoChg				0..exact	        0..exact
    //1..DCT				1..min	   	        1..min
    //2..HDG				2..max		        2..max
    //                      3..CLEAR ILS       >2..NextInstructionDistance

    {
        if (_currentLevelData?.ATCs == null || _currentLevelData.ATCs.Length == 0)
            return;
        if (_currentInstructionIndex < 0 || _currentInstructionIndex >= _currentLevelData.ATCs.Length)
            return;

        var route = Session.OriginalReferenceRoute;
        if (route?.Points == null || route.Points.Length == 0 || Session.PlayerAircraft == null)
            return;

        var currentInstruction = _currentLevelData.ATCs[_currentInstructionIndex];

        int oncemode = mode;

        point = currentInstruction.point;
        if (point < 50 && (point < 0 || point >= route.Points.Length))
            point = Mathf.Clamp(point, 0, route.Points.Length - 1);
        mode = (mode == 11) ? 1 : currentInstruction.mode;
        Altitude = currentInstruction.Altitude;
        VS = currentInstruction.VS;
        VS_nx = currentInstruction.VS_nx;
        Speed = currentInstruction.Speed;
        Speed_nx = currentInstruction.Speed_nx;

        hyp = Vector2.Distance(Session.PlayerAircraft.NMPosition, PointPos(point));
        PrvDistanceToPoint = Vector2.Distance(Session.PlayerAircraft.NMPosition, PointPos(PrvPoint));
        DistanceToPoint = Vector2.Distance(Session.PlayerAircraft.NMPosition, PointPos(point));

        if (point < 50 && point > 1 && point - 1 < route.Points.Length)
        {
            RawSpeed = route.Points[point - 1].RawSpeed;
            RawAlt = route.Points[point - 1].RawAltitude;
        }

        DataHandler.ParseAltRegulation(RawAlt, out AltAbove, out AltBelow, out AltExact); // FMS Altitude Limit


        // Debug.Log(point + ".   " + RawSpeed + "   /  " + AltExact + "   " + AltAbove + "A  " + AltBelow + "B" +
        //           "    FP:" + FuelPenaltyKg + "   MxSpd: " + ATCSpeed);

        //   Debug.Log(" N:  " + LegsScreen.VisibleRoute.FirstSpeedRegulationNodeId); // Correct this

        if (NewPoint)
        {


            string pointName = point >= 0 && point < route.Points.Length ? route.Points[point].Name : "";
            if (Atc1 != null)
                Atc1.text = mode == 1 ? "Proceed direct to  " + pointName :
                    mode == 2 ? "Turn " + TurnDirection(TrackToPoint(point))
                              + "Heading " + Calculator.NormalizeHeading360(TrackToPointFactored(point)) : "";


            string s = VS < 0 ? ", ROD " + (-VS) + " fpm" + NxToString(VS_nx) : "";

            if (!_ilsArmOutstanding)
            {
                if (Altitude > 0 && Atc2 != null) Atc2.text = "Descent altitude " + Altitude + " feet" + s;

                if (VS_nx == 3 && Atc2 != null)
                {
                    Atc2.text += " CLEAR ILS APPROACH ";
                    _clearIlsAwaitingXfr = true;
                    _ilsArmOutstanding = true;
                    IlsArmCommandIssued = true;
                    _atc2GrayDone = false;
                    Atc2.color = Color.green;
                    HideArmButton();
                }
            }
        

            if (Atc3 != null)
                Atc3.text = Speed > 0 ? "Speed " + Speed + " knots " + NxToString(Speed_nx) :
                    Speed == 0 ? Atc3.text : "";

            NextInstructionDistance = Speed_nx > 2 ? Speed_nx : 1.3f;

            bool xfr1On = mode == 2;
            bool xfr2On = _ilsArmOutstanding ? !_atc2GrayDone : Altitude > 0 || VS_nx == 3;
            bool xfr3On = Speed > 0;
            if (XFR1 != null) XFR1.interactable = xfr1On;
            if (XFR2 != null) XFR2.interactable = xfr2On;
            if (XFR3 != null) XFR3.interactable = xfr3On;

            if (Speed > 0)
            {
                XFRSpeed = Speed;
                XFRSpeedNx = Speed_nx;
            }
            if (mode == 2) XFRHdg = TrackToPointFactored(point);
            if (Altitude > 0) XFRAltitude = Altitude;

            // Keep glow in sync with arming (do not leave glow on a disabled button).
            SetXfrGlow(1, xfr1On);
            if (_ilsArmOutstanding)
            {
                if (!_atc2GrayDone)
                    SetXfrGlow(2, true);
            }
            else
                SetXfrGlow(2, xfr2On);
            SetXfrGlow(3, xfr3On);

            PrvTrackToPoint = TrackToPoint(point);
            if (mode > 0)
            {
                if (Atc1 != null)
                    Atc1.color = Color.green;
                _atc1GrayDone = false;
                if (mode != 2)
                    SetXfrGlow(1, false);
            }
            else
                SetXfrGlow(1, false);

            if (mode > 0) Cmode = mode;

            // Re-arm Atc3/XFR3 UI when this instruction includes a speed (even if value repeats)
            if (Speed > 0)
                _atc3IssuedSpeed = 0;

            NewPoint = false;

            // A row opened early (previous Speed_nx > 2), or this row's own Speed_nx > 2,
            // does not move the cone unless it also has DCT or heading.
            bool lateralClearance = mode == 1 || mode == 2;
            bool earlyDistance = _openedByEarlyCall || Speed_nx > 2;
            if (lateralClearance || !earlyDistance)
            {
                CaptureRouteAB();
                ResetBorderState();
            }
            _openedByEarlyCall = false;

            if (mode > 0 || xfr2On || xfr3On) SlowDown();

        }
        else // Not New
        {
            if (Session.CurrentLevel?.levelInfo != null)
            {
                LocDeviation(Session.CurrentLevel.levelInfo.Course);
                GsDeviation(Session.CurrentLevel.levelInfo.GlideSlope);
            }

            BorderGuard();
        }

        TryGrayAtc1IfLegsMatches();

        if ((Altitude > 0) && (Altitude != ATCAltitude)) //Descent clr changed
        {
            if (!_ilsArmOutstanding)
            {
                if (Atc2 != null)
                    Atc2.color = Color.green;
                if (XFR2 != null)
                    XFR2.interactable = true;
                SetXfrGlow(2, true);
                _atc2GrayDone = false;
            }
            XFRAltitude = Altitude;
            ATCAltitude = Altitude;
            ATCVS = VS;
            modD = VS == 0 ? -1 : VS_nx;
            BeginAltitudeGrace();
            CancelInvoke(nameof(DescentCheck));
            InvokeRepeating(nameof(DescentCheck), 10f, 1f);
        }

        if ((Speed > 0) && (Speed != _atc3IssuedSpeed)) // New ATC speed clearance (once)
        {
            _atc3IssuedSpeed = Speed;
            XFRSpeed = Speed;
            XFRSpeedNx = Speed_nx;
            if (Atc3 != null)
                Atc3.color = Color.green;
            _atc3GrayDone = false;
            ATCSpeed = Speed;
            if ((RawSpeed > 0) && (RawSpeed < ATCSpeed) && (Speed_nx != 1) && (Cmode == 1))
                ATCSpeed = RawSpeed;
            modS = ((RawSpeed > 0) && (RawSpeed < Speed) && (Speed_nx != 1) && (Cmode == 1)) ? 2 : Speed_nx;
            BeginSpeedGrace();

            if (XFR3 != null)
                XFR3.interactable = true;
            SetXfrGlow(3, true);
        }
        else if ((RawSpeed > 0) && (RawSpeed < ATCSpeed) && (Speed_nx != 1) && (Cmode == 1))
        {
            // FMC max below clearance: monitoring only (do not re-glow XFR3)
            ATCSpeed = RawSpeed;
            modS = 2;
        }

        if (Speed == -1) DisarmSpeedPenalty();

    }

    public void CheckAirplaneMove()
    {
        // Level Test: skip ATC clearances / border / SlowDown, but still advance
        // instruction points so APP arms at VS_nx==3 (CLEAR ILS) like normal play.
        if (LevelTestMode.IsActive)
        {
            TickLevelTestApproachArm();
            return;
        }

        ATCCall();
        TrackAltitudeFix();
        AdvancePenaltyGrace();

        if (_currentLevelData?.ATCs == null || Session.Settings == null || Session.PlayerAircraft == null)
            return;

        if ((_currentInstructionIndex < _currentLevelData.ATCs.Length - 1))
        {


            ElapsedTime += Session.Settings.FlyingTickDuration;
            if (TimerText != null)
                TimerText.text = "" + ElapsedTime;

            OncekiPos = Session.PlayerAircraft.NMPosition;
            OncekiAlt = (int)Calculator.CAltitude;

            DistanceToPoint = Vector2.Distance(Session.PlayerAircraft.NMPosition, PointPos(point));

            // Heading calls must arrive before LNAV's 1.2 NM seek walks onto the next-leg curve.
            if ((point != prvWptIdx) && (DistanceToPoint < DistanceToIssueNextCall()))
            {
                MoveOnNextInstruction();
                PrvPoint = point;
            }
        }
    }

    /// <summary>
    /// Level-test only: walk ATC waypoints silently; arm APP when VS_nx==3 becomes active.
    /// Does not issue clearances, SlowDown, BorderGuard, or XFR UI.
    /// </summary>
    void TickLevelTestApproachArm()
    {
        if (_currentLevelData?.ATCs == null || _currentLevelData.ATCs.Length == 0)
            return;

        if (_currentInstructionIndex < 0 || _currentInstructionIndex >= _currentLevelData.ATCs.Length)
            return;
        if (Session.PlayerAircraft == null || Session.State == null)
            return;

        var currentInstruction = _currentLevelData.ATCs[_currentInstructionIndex];
        point = currentInstruction.point;

        if (NewPoint)
        {
            if (currentInstruction.VS_nx == 3)
                Session.State.AppArmed = true;

            NextInstructionDistance = currentInstruction.Speed_nx > 2
                ? currentInstruction.Speed_nx
                : 1.3f;
            NewPoint = false;
        }

        if (_currentInstructionIndex >= _currentLevelData.ATCs.Length - 1)
            return;

        DistanceToPoint = Vector2.Distance(Session.PlayerAircraft.NMPosition, PointPos(point));
        if ((point != prvWptIdx) && (DistanceToPoint < DistanceToIssueNextCall()))
        {
            MoveOnNextInstruction();
            PrvPoint = point;
        }
    }

    int PeekNextInstructionMode()
    {
        if (_currentLevelData?.ATCs == null)
            return 0;
        int next = _currentInstructionIndex + 1;
        if (next < 0 || next >= _currentLevelData.ATCs.Length)
            return 0;
        return _currentLevelData.ATCs[next].mode;
    }

    float DistanceToIssueNextCall()
    {
        float dist = NextInstructionDistance;
        if (PeekNextInstructionMode() == 2)
            dist = Mathf.Max(dist, HeadingClearanceLeadNm());
        return dist;
    }

    static float HeadingClearanceLeadNm()
    {
        float seek = Session.Settings != null ? Session.Settings.PilotSeekDistancePathFollow : 1.2f;
        float gs = Calculator.GS > 1f ? Calculator.GS : 280f;
        float turnNm = 1.6f * gs / 280f;
        return Mathf.Max(2.5f, seek + turnNm + 0.5f);
    }

    void MoveOnNextInstruction()
    {
        _openedByEarlyCall = NextInstructionDistance > 2.5f;
        _currentInstructionIndex += 1;
        NewPoint = true;
        // Force altitude arming to re-evaluate on next ATCCall (do not store VS here).
        ATCAltitude = 0;
        prvWptIdx = point;
    }
    /// <summary>
    /// Localizer geometry relative to runway threshold.
    /// alongFromThresholdNm: NM before threshold along inbound course (positive on approach).
    /// crossTrackNm: NM right of course (positive = right).
    /// </summary>
    public void ComputeLocTrackErrors(float course, out float alongFromThresholdNm, out float crossTrackNm)
    {
        Vector2 rwPos = PointPos(RW);
        Vector2 acPos = Session.PlayerAircraft.NMPosition;
        Vector2 courseDir = new Vector2(Mathf.Sin(course * Mathf.Deg2Rad), Mathf.Cos(course * Mathf.Deg2Rad));
        Vector2 rightDir = new Vector2(courseDir.y, -courseDir.x);
        Vector2 toAircraft = acPos - rwPos;

        alongFromThresholdNm = -Vector2.Dot(toAircraft, courseDir);
        crossTrackNm = Vector2.Dot(toAircraft, rightDir);
    }

    /// <summary>Signed localizer angular deviation in degrees. Positive = right of course.</summary>
    public float ComputeLocDeviationDegrees(float course)
    {
        ComputeLocTrackErrors(course, out float alongFromThreshold, out float crossTrackNm);

        if (alongFromThreshold < 0.5f)
            return Mathf.DeltaAngle(course, TrackToPoint(RW));

        return Mathf.Rad2Deg * Mathf.Atan2(crossTrackNm, alongFromThreshold);
    }

    public float LocDeviation(float course)
    {
        float Deviation = ComputeLocDeviationDegrees(course);

        if (LOCIndex == null)
            return Deviation;

        if (((Mathf.Abs(Deviation) < 35) && (DME() < 10)) || ((Mathf.Abs(Deviation) < 10) && (DME() < 25)))
        {
            LOCIndex.enabled = true;
            float locFullScaleDegrees = LocCaptureDegrees;
            float locNeedleX = Mathf.Clamp((Deviation / locFullScaleDegrees) * 1243f, -1243f, 1243f);
            LOCIndex.transform.localPosition = new Vector2(locNeedleX, -645);
        }
        else
        {
            LOCIndex.enabled = false;
        }

        return Deviation;
    }

    /// <summary>Signed GS angular deviation in degrees (raw; not forced to 0 after capture).</summary>
    public float ComputeGsDeviationDegrees(float gs)
    {
        float dme = DME();
        if (dme < 0.5f)
            return 0f;

        float heightAboveRw = (float)Calculator.CAltitude - RunwayAltitudeFeet();
        float descentAngle = Mathf.Atan2(heightAboveRw, dme * 6076.12f) * Mathf.Rad2Deg;
        return Mathf.DeltaAngle(gs, descentAngle);
    }

    /// <summary>Altitude above / below GS path (ft). Positive = above path.</summary>
    public float GsAltitudeDeviation(float GS)
    {
        float dme = DME();
        float GSAltitude = RunwayAltitudeFeet() + Mathf.Tan(GS * Mathf.Deg2Rad) * dme * 6076.12f;
        return (float)Calculator.CAltitude - GSAltitude;
    }

    public float GsDeviation(float GS)
    {
        float Deviation = ComputeGsDeviationDegrees(GS);

        // After capture, needle stays centered (display only — path still uses real deviation).
        float needleDev = Session.State.GSCaptured ? 0f : Deviation;

        if (GSIndex == null)
            return Deviation;

        if ((Mathf.Abs(ComputeLocDeviationDegrees(_currentLevelData.levelInfo.Course)) < 5) && (DME() < 20))
        {
            GSIndex.enabled = true;
            GSIndex.transform.localPosition = new Vector2(1373, Mathf.Clamp(-needleDev * 1500, -541, 541));
        }
        else
        {
            GSIndex.enabled = false;
        }

        return Deviation;
    }


    private void DescentCheck()
    {
        int AltAbove, AltBelow, AltExact, AltRef; // First Altitude Restriction

        // todo birol : there is another variable RawAlt in the begining of this class - should they be the same ?
        var rawAlt = "0";

        if (Session.VisibleRoute != null && Session.ActiveRoute != null)
        {
            if (Session.ActiveRoute.GetPoint(Session.VisibleRoute.FirstAltRegulationNodeId, out var altRegulationNode, out _)
                && altRegulationNode != null
                && altRegulationNode.RawAltitude != null)
            {
                rawAlt = altRegulationNode.RawAltitude;
            }
        }

        DataHandler.ParseAltRegulation(rawAlt, out AltAbove, out AltBelow, out AltExact);

        AltRef = AltBelow > AltExact ? AltBelow : AltExact;

        if (Mathf.Abs((int)Calculator.CAltitude - ATCAltitude) <= 300 ||
            Mathf.Abs((int)Calculator.CAltitude - AltRef) <= 300)
        {
            CancelInvoke(nameof(DescentCheck));
            if (!_ilsArmOutstanding && Atc2 != null)
                Atc2.text = "";
        }

    }
}

