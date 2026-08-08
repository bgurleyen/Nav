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
    private bool isDescentChecked, isSpeedChecked, isRouteChecked;
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
    bool _borderBusy;
    bool _borderLatched;
    /// <summary>Hard border: keep ATC1 standby text until DistanceToPoint ≤ 1.3 NM.</summary>
    bool _rerouteStandbyUntilGate;
    /// <summary>Soft band: factored heading clearance + XFR armed once per XTE &gt; 1.3 entry.</summary>
    bool _softClearanceIssued;
    float _prevXte = -1f;

    static readonly Color AtcOrange = new Color(1f, 0.55f, 0f);
    bool _atc1AwaitingResponse;
    bool _atc1GrayDone;
    float _atc1IssueHeadingErr;
    bool _atc2AwaitingResponse;
    bool _atc2GrayDone;
    bool _atc3AwaitingResponse;
    bool _atc3GrayDone;
    float _atc3IssueSpeed;

    /// <summary>1 for normal turns; 10 only for post-warning catch-up while outside the border.</summary>
    public static float TurnRateMul { get; private set; } = 1f;
    public static bool IsOutsideBorder { get; private set; }
    /// <summary>Heading change sign that reduces XTE: -1 left, +1 right, 0 unknown.</summary>
    public static float XteReduceHeadingSign { get; private set; }

    public Button XFR1, XFR2, XFR3;
    public TextMeshProUGUI XFR1Txt, XFR2Txt, XFR3Txt;
    public static int XFRSpeed = 0;
    public static long XFRAltitude = 0;
    public static int XFRHdg = 0;

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

            pt.transform.localPosition = Pos;

            TempPtsPos[j] = Pos;
        }

        for (int j = 1; j < 21; j++) //Locate Virtual points on EditMap
        {

            var pt = GameObject.Find("pt (" + (j + 50) + ")");
            VirtualPtsPos[j].x = Pos.x + _currentLevelData.VirtualPoints[j - 1].x -
                                 _currentLevelData.VirtualPoints[20].x;
            VirtualPtsPos[j].y = Pos.y + _currentLevelData.VirtualPoints[j - 1].y -
                                 _currentLevelData.VirtualPoints[20].y;
            pt.transform.localPosition = VirtualPtsPos[j];
        }

        myAC = GameObject.Find("AC (0)"); // Init my AC

        Atc1.text = "";
        Atc2.text = "";
        Atc3.text = "";
        CancelAtc1ResponseWatch();
        CancelAtc2ResponseWatch();
        CancelAtc3ResponseWatch();
        _atc3IssuedSpeed = 0;
        _borderBusy = false;
        _borderLatched = false;
        _rerouteStandbyUntilGate = false;
        _softClearanceIssued = false;
        _prevXte = -1f;
        TurnRateMul = 1f;
        IsOutsideBorder = false;
        XteReduceHeadingSign = 0f;

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
        return Mathf.Abs(Mathf.Sin(Mathf.DeltaAngle(TrackToPoint(point), PrvTrackToPoint) * Mathf.Deg2Rad)) * hyp;
    }

    /*
     * BORDER RULES
     * ------------
     * Geometry: XTE vs clearance track (PrvTrackToPoint → Point). Sequence gate = DistanceToPoint < 1.3 NM.
     *
     * 1) DistanceToPoint ≤ 1.3 NM
     *    Sequencing owns the aircraft. Border idle (no warn / no boost).
     *
     * 2) Soft band — XTE crosses above 1.3 NM
     *    Once per entry: ATC factored heading clearance + XFR1 armed. No dialog.
     *
     * 3) Hard band — XTE > 2.6 NM
     *    Outside border. Warnings / catch-up apply (unless skipped below).
     *    Reroute always starts with PILOT RESPONSE dialog; ATC1 standby only after dialog.
     *    Standby text stays until DistanceToPoint ≤ 1.3 NM.
     *
     * 4) After waypoint sync
     *    No warnings until PrvPoint has been left (≥ max(NextInstructionDistance, 1.3) NM).
     *
     * 5) Skip warn if already turning toward the XTE-reducing side.
     *
     * 6) Hard + not turning to reduce XTE
     *    Dialog "PILOT RESPONSE" / Fly Heading (factored). Latches warning.
     *
     * 7) After warning (latched) while still hard-outside
     *    Force unfactored heading to point, turn rate 10x. Clear latch when XTE ≤ 1.3.
     *
     * 8) Normal turns (not latched catch-up): turn rate 1x.
     *    Outside catch-up turns after warning: only XTE-reducing turn direction allowed (Pilot).
     */
    void BorderGuard()
    {
        const float softNm = 1.3f;
        const float hardNm = 2.6f;
        const float gateNm = 1.3f;

        if (_borderBusy || Session.PlayerAircraft == null)
            return;

        if (DistanceToPoint <= gateNm)
        {
            ClearOutsideBorderSteer();
            _rerouteStandbyUntilGate = false;
            _softClearanceIssued = false;
            return;
        }

        float signedXte = Mathf.Sin(Mathf.DeltaAngle(TrackToPoint(point), PrvTrackToPoint) * Mathf.Deg2Rad) * hyp;
        float xte = Mathf.Abs(signedXte);
        IsOutsideBorder = xte > hardNm;
        // Normal turns stay 1x. Post-warning catch-up while outside hard band is 10x toward unfactored target heading.
        TurnRateMul = (IsOutsideBorder && _borderLatched) ? 10f : 1f;
        // signedXte > 0 → prefer heading decrease; < 0 → prefer heading increase.
        XteReduceHeadingSign = signedXte > 0.05f ? -1f : signedXte < -0.05f ? 1f : 0f;

        int directHdg = Calculator.NormalizeHeading360((int)Mathf.Round(TrackToPoint(point)));

        // After warning: 10x turn onto unfactored heading to the point.
        if (IsOutsideBorder && _borderLatched)
        {
            Calculator.RHeading = directHdg;
            Calculator.Instance.AddWindEffectToRHeading();
            XFRHdg = directHdg;
            mode = 2;
        }

        float turnToTarget = Mathf.Abs(Mathf.DeltaAngle(Calculator.CTrack, directHdg));
        Debug.Log("XTE: " + xte + "  turn: " + turnToTarget);

        Perpend = signedXte > 0f
            ? PrvTrackToPoint + 90f
            : PrvTrackToPoint - 90f;
        float along = hyp * Mathf.Cos(Mathf.DeltaAngle(TrackToPoint(point), PrvTrackToPoint) * Mathf.Deg2Rad);
        teta = Mathf.Atan2(xte - hardNm, Mathf.Max(0.1f, along)) * Mathf.Rad2Deg;

        // Soft band: one-shot factored clearance + XFR on XTE > 1.3 entry.
        // Hard reroute standby only after dialog (IssueHeadingCorrection).
        if (_rerouteStandbyUntilGate)
        {
            SetAtcReroutingStandby();
        }
        else if (xte > softNm)
        {
            if (!_softClearanceIssued)
            {
                _softClearanceIssued = true;
                RefreshAtcFactoredClearance();
            }
        }
        else
        {
            _softClearanceIssued = false;
        }

        // After a correction, wait until back inside soft band before allowing another dialog.
        if (_borderLatched)
        {
            if (xte <= softNm)
                _borderLatched = false;
            return;
        }

        // After sequencing: no warnings until the new PrvPoint has been left behind.
        if (!HasPassedPrvPoint())
            return;

        // Already turning toward the side that reduces XTE → do not warn.
        float turnDir = Mathf.DeltaAngle(Calculator.CHeading, Calculator.RHeading);
        if (Mathf.Abs(turnDir) <= 5f)
            turnDir = Mathf.DeltaAngle(PrvHdg, Calculator.CHeading);
        PrvHdg = Calculator.CHeading;

        if (XteReduceHeadingSign != 0f && Mathf.Abs(turnDir) > 0.5f
            && Mathf.Sign(turnDir) == Mathf.Sign(XteReduceHeadingSign))
            return;

        _prevXte = xte;

        if (xte <= hardNm)
            return;

        int hdg = TrackToPointFactored(point);
        IssueHeadingCorrection(
            hdg,
            title: "PILOT RESPONSE",
            body: "Excessive Deviation\nStandby for next clearence\nAuto Rerouting!!");
    }

    void SetAtcReroutingStandby()
    {
        CancelAtc1ResponseWatch();
        if (Atc1 == null)
            return;

        Atc1.color = Color.red;
        Atc1.text = "Rerouting , Standby!!";
        SetXfrGlow(1, false);
    }

    void RefreshAtcFactoredClearance()
    {
        int hdg = Calculator.NormalizeHeading360(TrackToPointFactored(point));
        XFRHdg = hdg;
        mode = 2;
        XFR1.interactable = true;
        SetXfrGlow(1, true);
        ArmAtc1ResponseWatch();

        if (Atc1 == null)
            return;

        Atc1.text = "Turn " + TurnDirection(TrackToPoint(point))
                    + "Heading " + hdg;
        Atc1.color = AtcOrange;
    }

    void SetXfrGlow(int channel, bool glow)
    {
        TextMeshProUGUI txt = channel == 1 ? XFR1Txt : channel == 2 ? XFR2Txt : XFR3Txt;
        if (txt != null)
            txt.fontMaterial.SetFloat(ShaderUtilities.ID_GlowPower, glow ? 1f : 0f);
    }

    void ArmAtc1ResponseWatch()
    {
        CancelInvoke(nameof(Atc1ResponseTimeout));
        _atc1AwaitingResponse = true;
        _atc1GrayDone = false;
        _atc1IssueHeadingErr = Mathf.Abs(Mathf.DeltaAngle(Calculator.CHeading, XFRHdg));
        Invoke(nameof(Atc1ResponseTimeout), 10f);
    }

    void CancelAtc1ResponseWatch()
    {
        CancelInvoke(nameof(Atc1ResponseTimeout));
        _atc1AwaitingResponse = false;
    }

    void Atc1ResponseTimeout()
    {
        _atc1AwaitingResponse = false;
    }

    public void AcknowledgeAtc1()
    {
        CancelAtc1ResponseWatch();
        if (_atc1GrayDone || Atc1 == null || Atc1.text == "Rerouting , Standby!!")
            return;
        _atc1GrayDone = true;
        Atc1.color = Color.gray;
        SetXfrGlow(1, false);
    }

    void CheckAtc1TurnResponse()
    {
        if (!_atc1AwaitingResponse || mode != 2)
            return;

        float err = Mathf.Abs(Mathf.DeltaAngle(Calculator.CHeading, XFRHdg));
        if (_atc1IssueHeadingErr - err >= 5f)
            AcknowledgeAtc1();
    }

    void ArmAtc2ResponseWatch()
    {
        CancelInvoke(nameof(Atc2ResponseTimeout));
        _atc2AwaitingResponse = true;
        _atc2GrayDone = false;
        Invoke(nameof(Atc2ResponseTimeout), 10f);
    }

    void CancelAtc2ResponseWatch()
    {
        CancelInvoke(nameof(Atc2ResponseTimeout));
        _atc2AwaitingResponse = false;
    }

    void Atc2ResponseTimeout()
    {
        _atc2AwaitingResponse = false;
    }

    public void AcknowledgeAtc2()
    {
        CancelAtc2ResponseWatch();
        if (_atc2GrayDone || Atc2 == null || string.IsNullOrEmpty(Atc2.text))
            return;
        _atc2GrayDone = true;
        Atc2.color = Color.gray;
        SetXfrGlow(2, false);
    }

    void CheckAtc2DescentResponse()
    {
        if (!_atc2AwaitingResponse)
            return;
        if (Calculator.CVS < -300)
            AcknowledgeAtc2();
    }

    void ArmAtc3ResponseWatch()
    {
        CancelInvoke(nameof(Atc3ResponseTimeout));
        _atc3AwaitingResponse = true;
        _atc3GrayDone = false;
        _atc3IssueSpeed = (float)Calculator.CSpeed;
        Invoke(nameof(Atc3ResponseTimeout), 10f);
    }

    void CancelAtc3ResponseWatch()
    {
        CancelInvoke(nameof(Atc3ResponseTimeout));
        _atc3AwaitingResponse = false;
    }

    void Atc3ResponseTimeout()
    {
        _atc3AwaitingResponse = false;
    }

    public void AcknowledgeAtc3()
    {
        CancelAtc3ResponseWatch();
        if (_atc3GrayDone || Atc3 == null || string.IsNullOrEmpty(Atc3.text))
            return;
        _atc3GrayDone = true;
        Atc3.color = Color.gray;
        SetXfrGlow(3, false);
    }

    void CheckAtc3SpeedResponse()
    {
        if (!_atc3AwaitingResponse)
            return;
        if (_atc3IssueSpeed - (float)Calculator.CSpeed >= 5f)
            AcknowledgeAtc3();
    }

    static void ClearOutsideBorderSteer()
    {
        TurnRateMul = 1f;
        IsOutsideBorder = false;
        XteReduceHeadingSign = 0f;
    }

    /// <summary>
    /// True once we have left the sequence bubble around PrvPoint (set when the previous point synced).
    /// </summary>
    bool HasPassedPrvPoint()
    {
        if (PrvPoint <= 0)
            return true;

        float distFromPrv = Vector2.Distance(Session.PlayerAircraft.NMPosition, PointPos(PrvPoint));
        float gate = Mathf.Max(NextInstructionDistance, 1.3f);
        return distFromPrv >= gate;
    }

    void IssueHeadingCorrection(int hdg, string title, string body)
    {
        _borderBusy = true;
        _borderLatched = true;
        _rerouteStandbyUntilGate = true;
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

    private void SpeedCheck()
    {

        if
            (((modS == 0) && ((Calculator.CSpeed > ATCSpeed + 10) || (Calculator.CSpeed < ATCSpeed - 10))) ||

             ((modS == 1) && (Calculator.CSpeed < ATCSpeed - 10)) ||

             ((modS == 2) && (Calculator.CSpeed > ATCSpeed + 10)))
        {

            Atc3.color = Color.red;
            SetXfrGlow(3, false);
            FuelPenalty += 0.001;

        }

        isSpeedChecked = true;
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
                Session.State.AppArmed = true;

            }

            Atc3.text = Speed > 0 ? "Speed " + Speed + " knots " + NxToString(Speed_nx) :
                Speed == 0 ? Atc3.text : "";

            NextInstructionDistance = Speed_nx > 2 ? Speed_nx : 1.3f;

            XFR1.interactable = mode == 2 ? true : false;
            XFR2.interactable = Altitude > 0 ? true : false;

            if (Speed > 0) XFRSpeed = Speed;
            if (mode == 2) XFRHdg = TrackToPointFactored(point);
            if (Altitude > 0) XFRAltitude = Altitude;

            // XFR3 only clickable when MCP speed differs from clearance
            XFR3.interactable = Speed > 0 && XFRSpeed != Calculator.RSpeed;


            PrvTrackToPoint = TrackToPoint(point);
            if (mode > 0)
            {
                Atc1.color = Color.green;
                _atc1GrayDone = false;
                if (mode == 2)
                {
                    SetXfrGlow(1, true);
                    ArmAtc1ResponseWatch();
                }
                else
                {
                    SetXfrGlow(1, false);
                    CancelAtc1ResponseWatch();
                }

                // ATC2: ATC1 clearance sonrası 10 sn içinde alçalış varsa gri
                if (Altitude > 0)
                    ArmAtc2ResponseWatch();
            }
            else
            {
                SetXfrGlow(1, false);
                CancelAtc1ResponseWatch();
            }

            if (mode > 0) Cmode = mode;
            if (Cmode == 1) FuelPenaltyAtFMCAltConstain(); // Check  Alt constrains on point for penalty

            // Re-arm Atc3/XFR3 UI when this instruction includes a speed (even if value repeats)
            if (Speed > 0)
                _atc3IssuedSpeed = 0;

            NewPoint = false;

            _borderBusy = false;
            _borderLatched = false;
            _rerouteStandbyUntilGate = false;
            _softClearanceIssued = false;
            _prevXte = -1f;
            TurnRateMul = 1f;
            IsOutsideBorder = false;
            XteReduceHeadingSign = 0f;

            if (mode > 0 || XFR2.interactable || XFR3.interactable) SlowDown();

        }
        else // Not New
        {
            CheckAtc1TurnResponse();
            CheckAtc2DescentResponse();
            CheckAtc3SpeedResponse();

            if (XFRHdg == Calculator.RHeading)
            {
                XFR1.interactable = false;
                if (mode == 2)
                    AcknowledgeAtc1();
            }
            if (XFRAltitude == Calculator.RAltitude)
            {
                XFR2.interactable = false;
                if (Altitude > 0 || _atc2AwaitingResponse)
                    AcknowledgeAtc2();
            }
            if (XFRSpeed == Calculator.RSpeed)
            {
                XFR3.interactable = false;
                SetXfrGlow(3, false);
                if (Speed > 0 || _atc3AwaitingResponse)
                    AcknowledgeAtc3();
            }

            // Debug.Log(XFRHdg +"H"+ Calculator.RHeading+  "     "+ XFRAltitude +"A"+ Calculator.RAltitude + "   " + Speed +"S"+ Calculator.RSpeed);

            float dev = LocDeviation(Session.CurrentLevel.levelInfo.Course);
            float gsD = GsDeviation(Session.CurrentLevel.levelInfo.GlideSlope);
            float ils = Session.ILSRoute != null ? ILSDeviation(Session.CurrentLevel.levelInfo.Course) : 0;

            BorderGuard();
        }

        if ((Altitude > 0) && (Altitude != ATCAltitude)) //Descent clr changed
        {
            Atc2.color = Color.green;
            SetXfrGlow(2, true);
            isDescentChecked = false;
            ATCAltitude = Altitude;
            ATCVS = VS;
            modD = VS == 0 ? -1 : VS_nx;
            CancelInvoke(nameof(DescentCheck));
            ArmAtc2ResponseWatch();
            InvokeRepeating(nameof(DescentCheck), 10f, 1f);
        }

        if ((Speed > 0) && (Speed != _atc3IssuedSpeed)) // New ATC speed clearance (once)
        {
            _atc3IssuedSpeed = Speed;
            XFRSpeed = Speed;
            Atc3.color = Color.green;
            isSpeedChecked = false;
            ATCSpeed = Speed;
            if ((RawSpeed > 0) && (RawSpeed < ATCSpeed) && (Speed_nx != 1) && (Cmode == 1))
                ATCSpeed = RawSpeed;
            modS = ((RawSpeed > 0) && (RawSpeed < Speed) && (Speed_nx != 1) && (Cmode == 1)) ? 2 : Speed_nx;
            CancelInvoke(nameof(SpeedCheck));

            bool needsTransfer = XFRSpeed != Calculator.RSpeed;
            XFR3.interactable = needsTransfer;
            SetXfrGlow(3, needsTransfer);
            if (needsTransfer)
                ArmAtc3ResponseWatch();
            else
            {
                CancelAtc3ResponseWatch();
                _atc3GrayDone = true;
            }

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

    void MoveOnNextInstruction()
    {
        _currentInstructionIndex += 1;
        NewPoint = true;
        ATCAltitude = VS;
        myAC.GetComponent<UnityEngine.UI.Text>().text = "#";
        prvWptIdx = point;
    }
    public float ILSDeviation(float course)
    {
        float Deviation = Mathf.DeltaAngle(course, TrackToPoint(RW));



        if (Mathf.Abs(Mathf.DeltaAngle(course, Session.PlayerAircraft.HeadingDegrees)) > 90) Deviation *= -1;

        if (DME() > 3)
        {
            if ((Mathf.Abs(Deviation) < 3) && (DME() < 23))
            {
                Session.State.ILSCapture = true;
            }
        }
        else
        {
            Session.State.ILSCapture = false;
        }

        return Deviation;
    }
    /// <summary>Signed localizer angular deviation in degrees. Positive = right of course.</summary>
    public float ComputeLocDeviationDegrees(float course)
    {
        Vector2 rwPos = PointPos(RW);
        Vector2 acPos = Session.PlayerAircraft.NMPosition;
        Vector2 courseDir = new Vector2(Mathf.Sin(course * Mathf.Deg2Rad), Mathf.Cos(course * Mathf.Deg2Rad));
        Vector2 rightDir = new Vector2(courseDir.y, -courseDir.x);
        Vector2 toAircraft = acPos - rwPos;

        float alongFromThreshold = -Vector2.Dot(toAircraft, courseDir);
        float crossTrackNm = Vector2.Dot(toAircraft, rightDir);

        if (alongFromThreshold < 0.5f)
            return Mathf.DeltaAngle(course, TrackToPoint(RW));

        return Mathf.Rad2Deg * Mathf.Atan2(crossTrackNm, alongFromThreshold);
    }

    public float LocDeviation(float course)
    {
        // float Deviation = Mathf.DeltaAngle(course, TrackToPoint(RW))  ;
        Vector2 aircraftPosition = Session.PlayerAircraft.NMPosition;
        Vector2 courseDirection = new Vector2(Mathf.Sin(course * Mathf.Deg2Rad), Mathf.Cos(course * Mathf.Deg2Rad));

        // ILS hattına yakın bir referans noktası (varsa aktif ILS noktası, yoksa RW) alıyoruz.
        int ilsReferencePoint = point >= 50 ? point : RW;
        Vector2 ilsReferencePosition = PointPos(ilsReferencePoint);

        float alongTrack = Vector2.Dot(aircraftPosition - ilsReferencePosition, courseDirection);
        Vector2 closestPointOnCourse = ilsReferencePosition + (alongTrack * courseDirection);

        // Sabit look-ahead ile (NM), paralel ofsette mesafeye bağlı yalancı drift'i azaltıyoruz.
        const float lookAheadNm = 10f;
        Vector2 aimPointOnCourse = closestPointOnCourse + (courseDirection * lookAheadNm);

        float bearingToAimPoint = Mathf.Atan2(aimPointOnCourse.x - aircraftPosition.x, aimPointOnCourse.y - aircraftPosition.y) *
                                  Mathf.Rad2Deg;
        if (bearingToAimPoint < 0) bearingToAimPoint += 360;

        float Deviation = Mathf.DeltaAngle(course, bearingToAimPoint);


        // Debug.Log(Deviation);

        if (((Mathf.Abs(Deviation) < 35) && (DME() < 10)) || ((Mathf.Abs(Deviation) < 10) && (DME() < 25)))
        {
            LOCIndex.enabled = true;
            float locFullScaleDegrees = 2.5f;
            float locNeedleX = Mathf.Clamp((Deviation / locFullScaleDegrees) * 1243f, -1243f, 1243f);
            LOCIndex.transform.localPosition = new Vector2(locNeedleX, -645);
        }
        else
        {
            LOCIndex.enabled = false;
        }

        return Deviation;
    }
    public float ComputeGsDeviationDegrees(float gs)
    {
        if (Session.State.GSCaptured)
            return 0f;

        float dme = DME();
        if (dme < 0.5f)
            return 0f;

        float descentAngle = Mathf.Atan2((float)Calculator.CAltitude, dme * 6076.12f) * Mathf.Rad2Deg;
        return Mathf.DeltaAngle(gs, descentAngle);
    }

    public float GsAltitudeDeviation(float GS)
    {
        float GSAltitude = Mathf.Tan(GS * Mathf.Deg2Rad) * DME() * 6076.12f;
        float Difference = (float)Calculator.CAltitude - GSAltitude;

        return Difference;

    }
    public float GsDeviation(float GS)
    {
        float DescentAngle = Mathf.Atan2((float)Calculator.CAltitude, DME() * 6076.12f) * Mathf.Rad2Deg;
        float Deviation = Mathf.DeltaAngle(GS, DescentAngle);

        if (Session.State.GSCaptured) Deviation = 0;

        if ((Mathf.Abs(LocDeviation(_currentLevelData.levelInfo.Course)) < 5) && (DME() < 20))
        {
            GSIndex.enabled = true;
            GSIndex.transform.localPosition = new Vector2(1373, Mathf.Clamp(-Deviation * 1500, -541, 541));
        }
        else
        {
            GSIndex.enabled = false;
        }

        //Debug.Log(Deviation);
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

        if ((Mathf.Abs((int)Calculator.CAltitude - ATCAltitude) > 300) &&
            (Mathf.Abs((int)Calculator.CAltitude - AltRef) > 300))
        {
            if ((Calculator.CVS > -300) ||

                ((modD == 0) && ((Calculator.CVS > ATCVS + 300) || (Calculator.CVS < ATCVS - 300))) ||

                ((modD == 1) && (Calculator.CVS > ATCVS + 300)) ||

                ((modD == 2) && (Calculator.CVS < ATCVS - 300)))
            {
                Atc2.color = Color.red;
                SetXfrGlow(2, false);
            }
        }
        else
        {
            CancelInvoke(nameof(DescentCheck));
            CancelAtc2ResponseWatch();
            Atc2.text = "";
        }

        isDescentChecked = true;
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

