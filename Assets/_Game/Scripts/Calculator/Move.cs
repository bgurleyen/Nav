using System.Collections.Generic;
using Gamelogic.Extensions;
using Navigation;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TMPro;
using System;

[System.Serializable]
public class Move : Singleton<Move>
{
    public static float Perpend, teta;

    public Dictionary<string, Vector2> ACPositions = new Dictionary<string, Vector2>();
    public Dictionary<string, string> ACTexts = new Dictionary<string, string>();

    public GameObject myAC;
    public Image LOCIndex, GSIndex;

    public Text Atc1, Atc2, Atc3;
    public Text TimerText;


    private float ElapsedTime = 0f;

    public string ATtc1; // maybe it's possible to use like this
    private bool NewPoint = true;
    private Vector3 OncekiPos, PrvPos;
    private Vector2[] VirtualPtsPos = new Vector2[100];
    private Vector2[] TempPtsPos = new Vector2[100];
    private long ATCAltitude, OncekiAlt;
    private int ATCVS, ATCSpeed;
    /// <summary>Last ATC Speed value that armed Atc3 / XFR3 UI (avoids RawSpeed↔ATCSpeed retrigger).</summary>
    private int _atc3IssuedSpeed;
    private int modD = 0, modS = 0;
    private int RawSpeed = 0;
    private int AltAbove, AltBelow, AltExact;
    public double FuelPenalty = 0;

    private LevelDataScriptableObject _currentLevelData;
    private OtherAC[] _otherACs;
    float DistanceToPoint, PrvDistanceToPoint;
    int PrvPoint;
    public static int PrvHdg;

    float PrvTrackToPoint = 0, hyp;
    int prvWptIdx = -1;
    int point = 1, mode, Cmode = 1, VS, VS_nx, Speed, Speed_nx;
    long Altitude;
    string RawAlt = "";
    int _currentInstructionIndex = 0;
    int RW;
    float NextInstructionDistance = 1.3f;

    // BorderGuard: route A→B after each clearance (A = AC at issue, B = clearance point).
    Vector2 _routeA;
    Vector2 _routeB;
    bool _routeValid;
    bool _borderBusy;
    bool _hardActive;
    bool _softClearanceIssued;
    /// <summary>Hard reroute: ATC1 red standby until the next clearance (NewPoint).</summary>
    bool _rerouteStandbyUntilNextClearance;

    bool _atc1GrayDone;
    bool _atc2GrayDone;
    bool _atc3GrayDone;

    /// <summary>1 normal; 20 during hard catch-up toward B.</summary>
    public static float TurnRateMul { get; private set; } = 1f;
    public static bool IsOutsideBorder { get; private set; }
    /// <summary>Heading change sign that reduces XTE: -1 left, +1 right, 0 unknown.</summary>
    public static float XteReduceHeadingSign { get; private set; }

    public Button XFR1, XFR2, XFR3;
    public TextMeshProUGUI XFR1Txt, XFR2Txt, XFR3Txt;
    public static int XFRSpeed = 0;
    public static long XFRAltitude = 0;
    public static int XFRHdg = 0;

    Button _armButton;
    TextMeshProUGUI _armButtonTxt;

    public void Init(LevelDataScriptableObject levelData)
    {
        _currentLevelData = levelData;
        // var somePoint = GameManager.Instance.ActiveSet.Points[3].Clone();

        // Altitude computed: GameManager.Instance.ActiveSet.Points[4].Altitude.ComputedValue
        Vector2 Pos = new Vector2(0, 0);

        for (int j = 1; j < Session.OriginalReferenceRoute.Points.Length; j++) // Locate the points on EditMap
        {
            Pos = Session.OriginalReferenceRoute.GetCartesianPosition(j);
            var pt = GameObject.Find("pt (" + j + ")");
            if (pt == null)
            {
                Debug.LogWarning($"[Move.Init] Missing EditMap marker 'pt ({j})' — skipping (route has {Session.OriginalReferenceRoute.Points.Length} points).");
                TempPtsPos[j] = Pos;
                continue;
            }

            pt.transform.localPosition = Pos;
            TempPtsPos[j] = Pos;
        }

        for (int j = 1; j < 21; j++) //Locate Virtual points on EditMap
        {
            var pt = GameObject.Find("pt (" + (j + 50) + ")");
            if (_currentLevelData.VirtualPoints == null || j - 1 >= _currentLevelData.VirtualPoints.Length || 20 >= _currentLevelData.VirtualPoints.Length)
            {
                Debug.LogWarning($"[Move.Init] VirtualPoints incomplete for index {j} — skipping.");
                continue;
            }

            VirtualPtsPos[j].x = Pos.x + _currentLevelData.VirtualPoints[j - 1].x -
                                 _currentLevelData.VirtualPoints[20].x;
            VirtualPtsPos[j].y = Pos.y + _currentLevelData.VirtualPoints[j - 1].y -
                                 _currentLevelData.VirtualPoints[20].y;
            if (pt != null)
                pt.transform.localPosition = VirtualPtsPos[j];
        }

        myAC = GameObject.Find("AC (0)"); // Init my AC

        Atc1.text = "";
        Atc2.text = "";
        Atc3.text = "";
        _atc1GrayDone = false;
        _atc2GrayDone = false;
        _atc3GrayDone = false;
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
        XFRHdg = 0;
        ATCAltitude = 0;
        HideArmButton();

        var otherACsCount = _currentLevelData.otherACs.Length;
        _otherACs = new OtherAC[otherACsCount];
        for (var i = 0; i < otherACsCount; i++)
        {
            _otherACs[i] = new OtherAC(i, _currentLevelData);
        }


        _currentInstructionIndex = 0;
        RW = Session.OriginalReferenceRoute.Points.Length - 1;
    }


    public void Tick()
    {
        for (int i = 0; i < _otherACs.Length; i++)
        {
            _otherACs[i].Tick(ACTexts, ACPositions);
        }

        CheckAirplaneMove();


    }
    void SlowDown()
    {
        Session.State.Speed10X.Set(false);
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

    private float DistanceFromRoute()
    {
        if (!TryGetRouteXte(out float signedXte, out _, out _))
            return 0f;
        return Mathf.Abs(signedXte);
    }

    /*
     * BORDERGUARD
     * -----------
     * 1) Each new clearance defines route A→B (A = AC position at issue, B = clearance point).
     * 2) Soft: cone from B abeam ±1.3 NM toward A at 7°.
     * 3) Hard: same abeam anchors, 15° cone toward A.
     * 4) Soft breach → one FactoredHeading clearance.
     * 5) Hard breach (15° cone OR Dist(B) > |A-B|+5 NM) → dialog, one-shot TrackToPoint(B) at 20x.
     *    Hard mode until Dist(B) ≤ 1.3 NM. No per-tick heading recompute.
     *    ATC1 red "Rerouting , Standby!!" until next clearance. No turn-in-progress skip.
     *    Turn direction always reduces XTE (Pilot).
     */
    void BorderGuard()
    {
        const float abeamNm = 1.3f;
        const float softDeg = 7f;
        const float hardDeg = 15f;
        const float hardGateNm = 1.3f;
        const float beyondAbExtraNm = 5f;

        if (_borderBusy || Session.PlayerAircraft == null || !_routeValid)
            return;

        if (!TryGetRouteXte(out float signedXte, out float alongFromBTowardA, out float routeBrg))
            return;

        float xte = Mathf.Abs(signedXte);
        float softHalf = abeamNm + Mathf.Max(0f, alongFromBTowardA) * Mathf.Tan(softDeg * Mathf.Deg2Rad);
        float hardHalf = abeamNm + Mathf.Max(0f, alongFromBTowardA) * Mathf.Tan(hardDeg * Mathf.Deg2Rad);
        float abLen = Vector2.Distance(_routeA, _routeB);
        bool beyondAb = DistanceToPoint > abLen + beyondAbExtraNm;

        string modeName = _hardActive || xte > hardHalf || beyondAb
            ? "Hard"
            : (xte > softHalf ? "Soft" : "Normal");
        Debug.Log($"Mode={modeName}, XTE={xte:F2}, DistB={DistanceToPoint:F2}");

        // signedXte > 0 (right of A→B) → turn left to reduce.
        XteReduceHeadingSign = signedXte > 0.05f ? -1f : signedXte < -0.05f ? 1f : 0f;
        Perpend = signedXte > 0f ? routeBrg + 90f : routeBrg - 90f;
        teta = Mathf.Atan2(xte - hardHalf, Mathf.Max(0.1f, alongFromBTowardA)) * Mathf.Rad2Deg;

        if (_rerouteStandbyUntilNextClearance)
            SetAtcReroutingStandby();

        // Near B: hard mode ends; sequencing owns the aircraft.
        if (DistanceToPoint <= hardGateNm)
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
            int directHdg = Calculator.NormalizeHeading360((int)Mathf.Round(TrackToPoint(point)));
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
        _rerouteStandbyUntilNextClearance = false;
        ClearOutsideBorderSteer();
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
        int hdg = Calculator.NormalizeHeading360(TrackToPointFactored(point));
        XFRHdg = hdg;
        mode = 2;
        XFR1.interactable = true;

        if (Atc1 == null)
            return;

        Atc1.text = "Turn " + TurnDirection(TrackToPoint(point))
                    + "Heading " + hdg;
        Atc1.color = Color.green;
        _atc1GrayDone = false;
    }

    void SetXfrGlow(int channel, bool glow)
    {
        TextMeshProUGUI txt = channel == 1 ? XFR1Txt : channel == 2 ? XFR2Txt : XFR3Txt;
        if (txt != null)
            txt.fontMaterial.SetFloat(ShaderUtilities.ID_GlowPower, glow ? 1f : 0f);
    }

    /// <summary>ATC text gray + XFR matte — only from XFR click.</summary>
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

    public void AcknowledgeAtc2()
    {
        if (_atc2GrayDone || Atc2 == null || string.IsNullOrEmpty(Atc2.text))
            return;
        _atc2GrayDone = true;
        Atc2.color = Color.gray;
        if (XFR2 != null) XFR2.interactable = false;
        SetXfrGlow(2, false);
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
            rt.anchoredPosition = new Vector2(365f, 0f);

        _armButton = go.GetComponent<Button>();
        _armButton.onClick = new Button.ButtonClickedEvent();
        _armButton.onClick.AddListener(OnArmApproachClicked);
        _armButton.interactable = true;

        _armButtonTxt = go.GetComponentInChildren<TextMeshProUGUI>(true);
        if (_armButtonTxt != null)
            _armButtonTxt.text = "ARM";

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
        SlowDown();
    }

    void HideArmButton()
    {
        if (_armButton == null)
            return;

        if (_armButtonTxt != null)
            _armButtonTxt.fontMaterial.SetFloat(ShaderUtilities.ID_GlowPower, 0f);
        _armButton.gameObject.SetActive(false);
    }

    public void OnArmApproachClicked()
    {
        if (Session.State == null || Session.State.AppArmed)
            return;

        Session.State.AppArmed = true;
        HideArmButton();

        if (Atc2 != null)
        {
            Atc2.text = "LOC/GS Armed , MA altitude 5000 ft set";
            Atc2.color = Color.white;
            _atc2GrayDone = true;
        }

        if (Calculator.Instance != null)
        {
            Calculator.RAltitude = 5000;
            if (Calculator.Instance.txtRAltitude != null)
                Calculator.Instance.txtRAltitude.text = "5000";
            if (Calculator.Instance.txtRAltitude_overTape != null)
                Calculator.Instance.txtRAltitude_overTape.text = "5000";
            Calculator.Instance.SetFMA();
        }
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
            _borderBusy = false;

            Calculator.RHeading = Calculator.NormalizeHeading360(hdg);
            Calculator.Instance.AddWindEffectToRHeading();

            if (Session.State != null)
                Session.State.AutoSetHDG(true);

            Calculator.Instance.UpdatePFD();
            mode = 2;
            XFRHdg = hdg;
            XFR1.interactable = false;
            SetAtcReroutingStandby();
        });
    }


    public Vector2 PointPos(int pt)
    {
        //int ptCount =  virtualPoints[Level].VirtualPointsItems.Length;

        return pt < 50 ? Session.OriginalReferenceRoute.GetCartesianPosition(pt) : VirtualPtsPos[pt - 50];
    }


    private float TrackToPoint(int pt)
    {
        float x1 = Session.PlayerAircraft.NMPosition.x;
        float y1 = Session.PlayerAircraft.NMPosition.y;

        float x2 = PointPos(pt).x;
        float y2 = PointPos(pt).y;


        float Angle = Mathf.Atan2(x2 - x1, y2 - y1) * Mathf.Rad2Deg;
        if (Angle < 0) Angle += 360;

        return Angle;
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

        float x1 = Session.PlayerAircraft.NMPosition.x;
        float y1 = Session.PlayerAircraft.NMPosition.y;
        float alfa = Mathf.DeltaAngle(Calculator.CTrack, TrackToPoint(pt)) * Mathf.Deg2Rad;
        float Track = Calculator.CTrack * Mathf.Deg2Rad;


        float TurnRadius = 1.6f * Calculator.GS / 280;


        int Sign = Mathf.DeltaAngle(Calculator.CTrack, TrackToPoint(pt)) >= 0 ? 1 : -1;

        float H = TurnRadius * (1 - Mathf.Cos(alfa)); //Horizantal
        float V = Mathf.Sin(alfa) * TurnRadius;  //Vertical
        float x2 = x1 + Sign * (H * Mathf.Cos(Track) + V * Mathf.Sin(Track));
        float y2 = y1 + Sign * (V * Mathf.Cos(Track) - H * Mathf.Sin(Track));


        Calculator.WindElements WE = Calculator.CalculateWindElements(Calculator.CAltitude, Calculator.CSpeed, (int)TrackToPoint(x2, y2, pt));
        return (int)(Mathf.Round(TrackToPoint(x2, y2, pt)) + WE.HeadingWindAddition);
    }

    public float DME()
    {
        return Vector2.Distance(Session.PlayerAircraft.NMPosition, PointPos(RW));
    } // Distance from RW

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

        // Must be flying generally toward the runway (not outbound).
        float hdgToCourse = Mathf.Abs(Mathf.DeltaAngle(course, Session.PlayerAircraft.HeadingDegrees));
        if (hdgToCourse > 90f)
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

    private void SpeedCheck()
    {

        if
            (((modS == 0) && ((Calculator.CSpeed > ATCSpeed + 10) || (Calculator.CSpeed < ATCSpeed - 10))) ||

             ((modS == 1) && (Calculator.CSpeed < ATCSpeed - 10)) ||

             ((modS == 2) && (Calculator.CSpeed > ATCSpeed + 10)))
        {
            FuelPenalty += 0.001;
        }

    }

    private void FuelPenaltyAtFMCAltConstain()
    {
        int CAltitude = (int)Calculator.CAltitude;

        if (((AltBelow > 0) && (CAltitude > AltBelow + 300)) ||
            ((AltAbove > 0) && (CAltitude < AltAbove - 300)) ||
            ((AltExact > 0) && (Mathf.Abs(CAltitude - AltExact) > 300))) FuelPenalty += 0.1;

    }

    void ATCCall()
    //mode              pt  Alt VS  nx          Speed   nx
    //0..NoChg				0..exact	        0..exact
    //1..DCT				1..min	   	        1..min
    //2..HDG				2..max		        2..max
    //                      3..CLEAR ILS       >2..NextInstructionDistance

    {

        var currentInstruction = _currentLevelData.ATCs[_currentInstructionIndex];

        int oncemode = mode;

        point = currentInstruction.point;
        mode = (mode == 11) ? 1 : currentInstruction.mode;
        Altitude = currentInstruction.Altitude;
        VS = currentInstruction.VS;
        VS_nx = currentInstruction.VS_nx;
        Speed = currentInstruction.Speed;
        Speed_nx = currentInstruction.Speed_nx;

        hyp = Vector2.Distance(Session.PlayerAircraft.NMPosition, PointPos(point));
        PrvDistanceToPoint = Vector2.Distance(Session.PlayerAircraft.NMPosition, PointPos(PrvPoint));
        DistanceToPoint = Vector2.Distance(Session.PlayerAircraft.NMPosition, PointPos(point));

        if (point < 50 && point > 1) RawSpeed = Session.OriginalReferenceRoute.Points[point - 1].RawSpeed;

        if (point < 50 && point > 1) RawAlt = (Session.OriginalReferenceRoute.Points[point - 1].RawAltitude);

        DataHandler.ParseAltRegulation(RawAlt, out AltAbove, out AltBelow, out AltExact); // FMS Altitude Limit


        // Debug.Log(point + ".   " + RawSpeed + "   /  " + AltExact + "   " + AltAbove + "A  " + AltBelow + "B" +
        //           "    FP:" + FuelPenalty + "   MxSpd: " + ATCSpeed);

        //   Debug.Log(" N:  " + LegsScreen.VisibleRoute.FirstSpeedRegulationNodeId); // Correct this

        if (NewPoint)
        {


            Atc1.text = mode == 1 ? "Proceed direct to  " + Session.OriginalReferenceRoute.Points[point].Name :
                mode == 2 ? "Turn " + TurnDirection(TrackToPoint(point))
                          + "Heading " + Calculator.NormalizeHeading360(TrackToPointFactored(point)) : "";


            string s = VS < 0 ? ", ROD " + (-VS) + " fpm" + NxToString(VS_nx) : "";

            if (Altitude > 0) Atc2.text = "Descent altitude " + Altitude + " feet" + s;

            if (VS_nx == 3)
            {
                Atc2.text += " CLEAR ILS APPROACH ";
                ShowArmButton();
            }

            Atc3.text = Speed > 0 ? "Speed " + Speed + " knots " + NxToString(Speed_nx) :
                Speed == 0 ? Atc3.text : "";

            NextInstructionDistance = Speed_nx > 2 ? Speed_nx : 1.3f;

            XFR1.interactable = mode == 2 ? true : false;
            XFR2.interactable = Altitude > 0;
            XFR3.interactable = Speed > 0;

            if (Speed > 0) XFRSpeed = Speed;
            if (mode == 2) XFRHdg = TrackToPointFactored(point);
            if (Altitude > 0) XFRAltitude = Altitude;

            // Keep glow in sync with arming (do not leave glow on a disabled button).
            SetXfrGlow(1, XFR1.interactable);
            SetXfrGlow(2, XFR2.interactable);
            SetXfrGlow(3, XFR3.interactable);

            PrvTrackToPoint = TrackToPoint(point);
            if (mode > 0)
            {
                Atc1.color = Color.green;
                _atc1GrayDone = false;
                if (mode != 2)
                    SetXfrGlow(1, false);
            }
            else
                SetXfrGlow(1, false);

            if (mode > 0) Cmode = mode;
            if (Cmode == 1) FuelPenaltyAtFMCAltConstain(); // Check  Alt constrains on point for penalty

            // Re-arm Atc3/XFR3 UI when this instruction includes a speed (even if value repeats)
            if (Speed > 0)
                _atc3IssuedSpeed = 0;

            NewPoint = false;

            CaptureRouteAB();
            ResetBorderState();

            if (mode > 0 || XFR2.interactable || XFR3.interactable) SlowDown();

        }
        else // Not New
        {
            // Debug.Log(XFRHdg +"H"+ Calculator.RHeading+  "     "+ XFRAltitude +"A"+ Calculator.RAltitude + "   " + Speed +"S"+ Calculator.RSpeed);

            LocDeviation(Session.CurrentLevel.levelInfo.Course);
            GsDeviation(Session.CurrentLevel.levelInfo.GlideSlope);

            BorderGuard();
        }

        if ((Altitude > 0) && (Altitude != ATCAltitude)) //Descent clr changed
        {
            Atc2.color = Color.green;
            XFRAltitude = Altitude;
            XFR2.interactable = true;
            SetXfrGlow(2, true);
            _atc2GrayDone = false;
            ATCAltitude = Altitude;
            ATCVS = VS;
            modD = VS == 0 ? -1 : VS_nx;
            CancelInvoke(nameof(DescentCheck));
            InvokeRepeating(nameof(DescentCheck), 10f, 1f);
        }

        if ((Speed > 0) && (Speed != _atc3IssuedSpeed)) // New ATC speed clearance (once)
        {
            _atc3IssuedSpeed = Speed;
            XFRSpeed = Speed;
            Atc3.color = Color.green;
            _atc3GrayDone = false;
            ATCSpeed = Speed;
            if ((RawSpeed > 0) && (RawSpeed < ATCSpeed) && (Speed_nx != 1) && (Cmode == 1))
                ATCSpeed = RawSpeed;
            modS = ((RawSpeed > 0) && (RawSpeed < Speed) && (Speed_nx != 1) && (Cmode == 1)) ? 2 : Speed_nx;
            CancelInvoke(nameof(SpeedCheck));

            XFR3.interactable = true;
            SetXfrGlow(3, true);

            InvokeRepeating(nameof(SpeedCheck), Mathf.Abs((float)Calculator.CSpeed - ATCSpeed) * 2.5f,
                1f); //  secs before warning
        }
        else if ((RawSpeed > 0) && (RawSpeed < ATCSpeed) && (Speed_nx != 1) && (Cmode == 1))
        {
            // FMC max below clearance: monitoring only (do not re-glow XFR3)
            ATCSpeed = RawSpeed;
            modS = 2;
        }

        if (Speed == -1) CancelInvoke(nameof(SpeedCheck));

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


        if ((_currentInstructionIndex < _currentLevelData.ATCs.Length - 1))
        {


            ElapsedTime += Session.Settings.FlyingTickDuration;
            TimerText.text = "" + ElapsedTime;

            OncekiPos = Session.PlayerAircraft.NMPosition;
            OncekiAlt = (int)Calculator.CAltitude;
            myAC.transform.localPosition = Session.PlayerAircraft.NMPosition; //move AC on EditMap

            DistanceToPoint = Vector2.Distance(Session.PlayerAircraft.NMPosition, PointPos(point));

            if ((point != prvWptIdx) && ((DistanceToPoint < NextInstructionDistance)))  // next instruction NextInstructionDistance nm before next pt
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
        if ((point != prvWptIdx) && (DistanceToPoint < NextInstructionDistance))
        {
            MoveOnNextInstruction();
            PrvPoint = point;
        }
    }

    void MoveOnNextInstruction()
    {
        _currentInstructionIndex += 1;
        NewPoint = true;
        // Force altitude arming to re-evaluate on next ATCCall (do not store VS here).
        ATCAltitude = 0;
        myAC.GetComponent<UnityEngine.UI.Text>().text = "#";
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

        if (Session.VisibleRoute != null)
        {
            if (Session.ActiveRoute.GetPoint(Session.VisibleRoute.FirstAltRegulationNodeId, out var altRegulationNode, out _))
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
            Atc2.text = "";
        }

    }
}


[System.Serializable]
public struct LevelData
{
    [SerializeField] private VirtualPointsScriptableObject virtualPoints;
    [SerializeField] private ATCInstructionsScriptableObject aTCs;
    public LevelInfoScriptableObject levelInfo;
    public OtherACScriptableObject[] otherACnr;

    public VirtualPoints[] VirtualPoints => virtualPoints.VirtualPointsItems;
    public ATCInstructionInfo[] ATCs => aTCs.ATCInstrucitonItems;
}

