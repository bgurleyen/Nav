using System;
using System.Collections.Generic;
using Gamelogic.Extensions;
using Navigation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[System.Serializable]
public class Move : Singleton<Move>
{
    public static float Perpendicular, TurnAngle;

    [Obsolete("Use Perpendicular")]
    public static float Perpend { get => Perpendicular; set => Perpendicular = value; }

    [Obsolete("Use TurnAngle")]
    public static float teta { get => TurnAngle; set => TurnAngle = value; }

    public Dictionary<string, Vector2> ACPositions = new Dictionary<string, Vector2>();
    public Dictionary<string, string> ACTexts = new Dictionary<string, string>();

    public GameObject myAC;
    public Image LOCIndex, GSIndex;

    public Text Atc1, Atc2, Atc3;
    public Text TimerText;

    float ElapsedTime;
    bool NewPoint = true;
    Vector3 OncekiPos, PrvPos;
    Vector2[] VirtualPtsPos = new Vector2[100];
    Vector2[] TempPtsPos = new Vector2[100];
    long ATCAltitude, OncekiAlt;
    int ATCVS, ATCSpeed;
    bool isDescentChecked, isSpeedChecked, isRouteChecked;
    int modD, modS;
    int RawSpeed;
    int AltAbove, AltBelow, AltExact;
    public double FuelPenalty;

    LevelDataScriptableObject _currentLevelData;
    OtherAC[] _otherACs;
    float DistanceToPoint, PrvDistanceToPoint;
    int PrvPoint;
    public static int PrvHdg;

    float PrvTrackToPoint, hyp;
    int prvWptIdx = -1;
    int point = 1, mode, Cmode = 1, VS, VS_nx, Speed, Speed_nx;
    long Altitude;
    string RawAlt = "";
    int _currentInstructionIndex;
    int RW;
    float NextInstructionDistance = 1.3f;

    public Button XFR1, XFR2, XFR3;
    public TextMeshProUGUI XFR1Txt, XFR2Txt, XFR3Txt;
    public static int XFRSpeed;
    public static long XFRAltitude;
    public static int XFRHdg;

    const float AtcGreenDuration = 10f;
    const float XfrHeadingTolerance = 5f;
    const int XfrSpeedTolerance = 5;
    static readonly Color AtcAcknowledgedColor = new Color(0.65f, 0.65f, 0.65f);

    struct AtcChannelState
    {
        public float ShownAt;
        public bool HasCommand;
        public bool XfrPressed;
        public bool RequiresXfr;
    }

    AtcChannelState _atc1State;
    AtcChannelState _atc2State;
    AtcChannelState _atc3State;
    bool _atc1Rerouting;

    public void Init(LevelDataScriptableObject levelData)
    {
        if (levelData == null)
        {
            Debug.LogError("Move.Init called with null level data.");
            return;
        }

        if (levelData.ATCs == null || levelData.ATCs.Length == 0)
            Debug.LogWarning($"Level '{levelData.name}' has no ATC instructions.");

        _currentLevelData = levelData;
        var mapRefs = MapSceneRefs.Instance;
        Vector2 pos = Vector2.zero;

        if (Session.OriginalReferenceRoute?.Points == null)
        {
            Debug.LogError("Move.Init: OriginalReferenceRoute is not initialized.");
            return;
        }

        for (int j = 1; j < Session.OriginalReferenceRoute.Points.Length; j++)
        {
            pos = Session.OriginalReferenceRoute.GetCartesianPosition(j);
            var pt = mapRefs != null ? mapRefs.GetRoutePoint(j) : GameObject.Find("pt (" + j + ")");
            if (pt == null)
                continue;

            pt.transform.localPosition = pos;
            TempPtsPos[j] = pos;
        }

        if (_currentLevelData.VirtualPoints != null)
        {
            for (int j = 1; j < 21 && j <= _currentLevelData.VirtualPoints.Length; j++)
            {
                var pt = mapRefs != null ? mapRefs.GetVirtualPoint(j) : GameObject.Find("pt (" + (j + 50) + ")");
                if (pt == null)
                    continue;

                VirtualPtsPos[j].x = pos.x + _currentLevelData.VirtualPoints[j - 1].x -
                                     _currentLevelData.VirtualPoints[20].x;
                VirtualPtsPos[j].y = pos.y + _currentLevelData.VirtualPoints[j - 1].y -
                                     _currentLevelData.VirtualPoints[20].y;
                pt.transform.localPosition = VirtualPtsPos[j];
            }
        }

        myAC = mapRefs != null ? mapRefs.PlayerAircraft : GameObject.Find("AC (0)");
        if (myAC == null)
            Debug.LogError("Move.Init: player aircraft map object not found.");

        if (Atc1 != null) Atc1.text = "";
        if (Atc2 != null) Atc2.text = "";
        if (Atc3 != null) Atc3.text = "";

        _atc1State = default;
        _atc2State = default;
        _atc3State = default;
        _atc1Rerouting = false;
        SetXfrButtonActive(1, false);
        SetXfrButtonActive(2, false);
        SetXfrButtonActive(3, false);

        var otherACsCount = _currentLevelData.otherACs?.Length ?? 0;
        _otherACs = new OtherAC[otherACsCount];
        for (var i = 0; i < otherACsCount; i++)
            _otherACs[i] = new OtherAC(i, _currentLevelData);

        _currentInstructionIndex = 0;
        RW = Session.OriginalReferenceRoute.Points.Length - 1;
    }

    public void Tick()
    {
        if (_otherACs == null)
            return;

        for (int i = 0; i < _otherACs.Length; i++)
            _otherACs[i].Tick(ACTexts, ACPositions);

        CheckAirplaneMove();
        UpdateIlsNeedles();
    }

    void SlowDown()
    {
        Session.State.Speed10X.Set(false);
        Session.Settings.SpeedMultiplier = 1;
    }

    string TurnDirection(float newHdg) =>
        Mathf.DeltaAngle(Calculator.CTrack, newHdg) >= 0 ? "Right " : "Left ";

    string NxToString(int nx) =>
        nx == 1 ? " or greater " : nx == 2 ? " or less " : "";

    float DistanceFromRoute() =>
        Mathf.Abs(Mathf.Sin(Mathf.Abs(TrackToPoint(point) - PrvTrackToPoint) * Mathf.Deg2Rad)) * hyp;

    public Vector2 PointPos(int pt) =>
        pt < 50 ? Session.OriginalReferenceRoute.GetCartesianPosition(pt) : VirtualPtsPos[pt - 50];

    float TrackToPoint(int pt)
    {
        float x1 = Session.PlayerAircraft.NMPosition.x;
        float y1 = Session.PlayerAircraft.NMPosition.y;
        float x2 = PointPos(pt).x;
        float y2 = PointPos(pt).y;
        float angle = Mathf.Atan2(x2 - x1, y2 - y1) * Mathf.Rad2Deg;
        return angle < 0 ? angle + 360 : angle;
    }

    float TrackToPoint(float x1, float y1, int pt)
    {
        float x2 = PointPos(pt).x;
        float y2 = PointPos(pt).y;
        float angle = Mathf.Atan2(x2 - x1, y2 - y1) * Mathf.Rad2Deg;
        return angle < 0 ? angle + 360 : angle;
    }

    int TrackToPointFactored(int pt)
    {
        float x1 = Session.PlayerAircraft.NMPosition.x;
        float y1 = Session.PlayerAircraft.NMPosition.y;
        float alfa = Mathf.DeltaAngle(Calculator.CTrack, TrackToPoint(pt)) * Mathf.Deg2Rad;
        float track = Calculator.CTrack * Mathf.Deg2Rad;
        float turnRadius = 1.6f * Calculator.GS / 280;
        int sign = Mathf.DeltaAngle(Calculator.CTrack, TrackToPoint(pt)) >= 0 ? 1 : -1;

        float h = turnRadius * (1 - Mathf.Cos(alfa));
        float v = Mathf.Sin(alfa) * turnRadius;
        float x2 = x1 + sign * (h * Mathf.Cos(track) + v * Mathf.Sin(track));
        float y2 = y1 + sign * (v * Mathf.Cos(track) - h * Mathf.Sin(track));

        var we = Calculator.CalculateWindElements(Calculator.CAltitude, Calculator.CSpeed, (int)TrackToPoint(x2, y2, pt));
        return (int)Mathf.Round(TrackToPoint(x2, y2, pt) + we.HeadingWindAddition);
    }

    public float DME() => Vector2.Distance(Session.PlayerAircraft.NMPosition, PointPos(RW));

    void SpeedCheck()
    {
        if (((modS == 0) && (Calculator.CSpeed > ATCSpeed + 10 || Calculator.CSpeed < ATCSpeed - 10)) ||
            (modS == 1 && Calculator.CSpeed < ATCSpeed - 10) ||
            (modS == 2 && Calculator.CSpeed > ATCSpeed + 10))
            FuelPenalty += 0.001;

        isSpeedChecked = true;
    }

    void FuelPenaltyAtFMCAltConstain()
    {
        int currentAltitude = (int)Calculator.CAltitude;
        if ((AltBelow > 0 && currentAltitude > AltBelow + 300) ||
            (AltAbove > 0 && currentAltitude < AltAbove - 300) ||
            (AltExact > 0 && Mathf.Abs(currentAltitude - AltExact) > 300))
            FuelPenalty += 0.1;
    }

    AtcChannelState GetChannelState(int lane)
    {
        switch (lane)
        {
            case 1: return _atc1State;
            case 2: return _atc2State;
            case 3: return _atc3State;
            default: throw new ArgumentOutOfRangeException(nameof(lane));
        }
    }

    void SetChannelState(int lane, AtcChannelState state)
    {
        switch (lane)
        {
            case 1: _atc1State = state; break;
            case 2: _atc2State = state; break;
            case 3: _atc3State = state; break;
        }
    }

    void SetXfrButtonActive(int lane, bool active)
    {
        switch (lane)
        {
            case 1:
                if (XFR1 != null) XFR1.interactable = active;
                if (XFR1Txt != null)
                    XFR1Txt.fontMaterial.SetFloat(ShaderUtilities.ID_GlowPower, Convert.ToInt32(active));
                break;
            case 2:
                if (XFR2 != null) XFR2.interactable = active;
                if (XFR2Txt != null)
                    XFR2Txt.fontMaterial.SetFloat(ShaderUtilities.ID_GlowPower, Convert.ToInt32(active));
                break;
            case 3:
                if (XFR3 != null) XFR3.interactable = active;
                if (XFR3Txt != null)
                    XFR3Txt.fontMaterial.SetFloat(ShaderUtilities.ID_GlowPower, Convert.ToInt32(active));
                break;
        }
    }

    void IssueAtcCommand(int lane, bool requiresXfr)
    {
        var state = GetChannelState(lane);
        state.ShownAt = Time.time;
        state.HasCommand = true;
        state.RequiresXfr = requiresXfr;
        state.XfrPressed = !requiresXfr;
        SetChannelState(lane, state);
        SetXfrButtonActive(lane, requiresXfr);
    }

    void ClearAtcCommand(int lane)
    {
        SetChannelState(lane, default);
        SetXfrButtonActive(lane, false);
    }

    public void NotifyXfrPressed(int lane)
    {
        var state = GetChannelState(lane);
        if (!state.HasCommand || !state.RequiresXfr)
            return;

        state.XfrPressed = true;
        SetChannelState(lane, state);
        SetXfrButtonActive(lane, false);
    }

    bool IsXfrValueAlreadyEntered(int lane)
    {
        switch (lane)
        {
            case 1:
                return Mathf.Abs(Mathf.DeltaAngle(Calculator.RHeading, XFRHdg)) <= XfrHeadingTolerance;
            case 2:
                return Calculator.RAltitude == (int)XFRAltitude;
            case 3:
                return Mathf.Abs(Calculator.RSpeed - XFRSpeed) <= XfrSpeedTolerance;
            default:
                return false;
        }
    }

    void CompleteXfrIfAlreadyEntered(int lane)
    {
        var state = GetChannelState(lane);
        if (!state.HasCommand || !state.RequiresXfr || state.XfrPressed)
            return;

        if (!IsXfrValueAlreadyEntered(lane))
            return;

        state.XfrPressed = true;
        SetChannelState(lane, state);
        SetXfrButtonActive(lane, false);
    }

    void SyncAllXfrFromFmc()
    {
        CompleteXfrIfAlreadyEntered(1);
        CompleteXfrIfAlreadyEntered(2);
        CompleteXfrIfAlreadyEntered(3);
    }

    static int NormalizeInstructionMode(int instructionMode) =>
        instructionMode == 11 ? 1 : instructionMode;

    int GetPreviousNonZeroMode()
    {
        if (_currentLevelData?.ATCs == null)
            return 0;

        for (int i = _currentInstructionIndex - 1; i >= 0; i--)
        {
            int previousMode = NormalizeInstructionMode(_currentLevelData.ATCs[i].mode);
            if (previousMode != 0)
                return previousMode;
        }

        return 0;
    }

    void ApplyAtc1ForCurrentMode()
    {
        if (mode == 0)
        {
            int previousNonZeroMode = GetPreviousNonZeroMode();
            if (previousNonZeroMode == 1)
            {
                Atc1.text = "";
                _atc1Rerouting = false;
                ClearAtcCommand(1);
            }
            return;
        }

        if (mode != 1 && mode != 2)
            return;

        Atc1.text = mode switch
        {
            1 => "Proceed direct to  " + Session.OriginalReferenceRoute.Points[point].Name,
            2 => "Turn " + TurnDirection(TrackToPoint(point)) + "Heading " +
                 Calculator.NormalizeHeading360(TrackToPointFactored(point)),
            _ => Atc1.text
        };
        _atc1Rerouting = false;
        IssueAtcCommand(1, mode == 2);
    }

    Color GetAtcChannelColor(AtcChannelState state, bool forceRed = false)
    {
        if (!state.HasCommand)
            return Color.white;

        // Yeni komut: 10 sn yesil
        if (Time.time - state.ShownAt < AtcGreenDuration)
            return Color.green;

        // XFR basildi, hedef esitlendi veya XFR gerektirmiyor (DCT vb.) -> gri
        if (state.XfrPressed)
            return AtcAcknowledgedColor;

        // Rota sapmasi, henuz onaylanmadi -> kirmizi
        if (forceRed)
            return Color.red;

        // XFR bekleniyor -> kirmizi
        return Color.red;
    }

    void UpdateAtcTextColors()
    {
        if (Atc1 != null && !string.IsNullOrEmpty(Atc1.text))
            Atc1.color = GetAtcChannelColor(_atc1State, _atc1Rerouting);

        if (Atc2 != null && !string.IsNullOrEmpty(Atc2.text))
            Atc2.color = GetAtcChannelColor(_atc2State);

        if (Atc3 != null && !string.IsNullOrEmpty(Atc3.text))
            Atc3.color = GetAtcChannelColor(_atc3State);
    }

    void ATCCall()
    {
        if (_currentLevelData?.ATCs == null || _currentLevelData.ATCs.Length == 0)
            return;

        var currentInstruction = _currentLevelData.ATCs[Mathf.Clamp(_currentInstructionIndex, 0, _currentLevelData.ATCs.Length - 1)];

        point = currentInstruction.point;
        mode = mode == 11 ? 1 : currentInstruction.mode;
        Altitude = currentInstruction.Altitude;
        VS = currentInstruction.VS;
        VS_nx = currentInstruction.VS_nx;
        Speed = currentInstruction.Speed;
        Speed_nx = currentInstruction.Speed_nx;

        hyp = Vector2.Distance(Session.PlayerAircraft.NMPosition, PointPos(point));
        PrvDistanceToPoint = Vector2.Distance(Session.PlayerAircraft.NMPosition, PointPos(PrvPoint));
        DistanceToPoint = hyp;

        if (point is > 0 and < 50)
        {
            RawSpeed = Session.OriginalReferenceRoute.Points[point - 1].RawSpeed;
            RawAlt = Session.OriginalReferenceRoute.Points[point - 1].RawAltitude;
        }

        DataHandler.ParseAltRegulation(RawAlt, out AltAbove, out AltBelow, out AltExact);

        if (NewPoint)
        {
            ApplyAtc1ForCurrentMode();

            string descentSuffix = VS < 0 ? ", ROD " + (-VS) + " fpm" + NxToString(VS_nx) : "";
            if (Altitude > 0)
                Atc2.text = "Descent altitude " + Altitude + " feet" + descentSuffix;

            if (VS_nx == 3)
            {
                Atc2.text += " CLEAR ILS APPROACH ";
                Session.State.AppArmed = true;
            }

            if (!string.IsNullOrEmpty(Atc2.text))
                IssueAtcCommand(2, Altitude > 0);

            if (Speed > 0)
            {
                Atc3.text = "Speed " + Speed + " knots " + NxToString(Speed_nx);
                IssueAtcCommand(3, true);
            }
            else if (Speed == 0)
            {
                Atc3.text = Atc3.text;
            }

            NextInstructionDistance = Speed_nx > 2 ? Speed_nx : 1.3f;

            if (Speed > 0) XFRSpeed = Speed;
            if (mode == 2) XFRHdg = TrackToPointFactored(point);
            if (Altitude > 0) XFRAltitude = Altitude;

            PrvTrackToPoint = TrackToPoint(point);
            if (mode > 0) Cmode = mode;
            if (Cmode == 1) FuelPenaltyAtFMCAltConstain();

            NewPoint = false;
        }
        else
        {
            Perpendicular = Mathf.Sin((TrackToPoint(point) - PrvTrackToPoint) * Mathf.Deg2Rad) > 0
                ? PrvTrackToPoint + 90
                : PrvTrackToPoint - 90;

            float x = hyp * Mathf.Cos(Mathf.DeltaAngle(TrackToPoint(point), PrvTrackToPoint) * Mathf.Deg2Rad);
            TurnAngle = Mathf.Atan2(DistanceFromRoute() - 1.3f, x) * Mathf.Rad2Deg;
            bool noTurn = Calculator.CHeading == PrvHdg;

            float warningDistance = Atc1.text == "ATC Rerouting" ? 7f : 1.3f;

            if (noTurn && DistanceFromRoute() > warningDistance && PrvDistanceToPoint > 4)
            {
                int factoredAngleToPoint = TrackToPointFactored(point);
                if (Atc1.text != "ATC Rerouting" && Calculator.RHeading != factoredAngleToPoint)
                {
                    Atc1.text = "ATC Rerouting";
                    _atc1Rerouting = true;
                    isRouteChecked = false;
                    XFRHdg = factoredAngleToPoint;
                    IssueAtcCommand(1, true);
                    mode = 2;
                    Calculator.Instance.OnClick_HDG(false);
                    Calculator.Instance.OnClick_HDG(true);
                }
            }
            else
            {
                _atc1Rerouting = false;
                isRouteChecked = true;
            }
        }

        if (Altitude > 0 && Altitude != ATCAltitude)
        {
            isDescentChecked = false;
            ATCAltitude = Altitude;
            ATCVS = VS;
            modD = VS == 0 ? -1 : VS_nx;
            IssueAtcCommand(2, true);
            CancelInvoke(nameof(DescentCheck));
            InvokeRepeating(nameof(DescentCheck), 10f, 1f);
        }

        if ((Speed > 0 && Speed != ATCSpeed) || (RawSpeed > 0 && RawSpeed < ATCSpeed && Cmode == 1))
        {
            isSpeedChecked = false;
            if (Speed > 0 && Speed != ATCSpeed) ATCSpeed = Speed;
            if (RawSpeed > 0 && RawSpeed < ATCSpeed && Speed_nx != 1 && Cmode == 1) ATCSpeed = RawSpeed;
            modS = RawSpeed > 0 && RawSpeed < Speed && Speed_nx != 1 && Cmode == 1 ? 2 : Speed_nx;
            IssueAtcCommand(3, Speed > 0);
            CancelInvoke(nameof(SpeedCheck));
            InvokeRepeating(nameof(SpeedCheck), Mathf.Abs((float)Calculator.CSpeed - ATCSpeed) * 2.5f, 1f);
        }

        if (Speed == -1)
            CancelInvoke(nameof(SpeedCheck));

        SyncAllXfrFromFmc();
        UpdateAtcTextColors();
    }

    public void CheckAirplaneMove()
    {
        ATCCall();

        if (_currentLevelData?.ATCs == null ||
            _currentInstructionIndex >= _currentLevelData.ATCs.Length - 1)
            return;

        ElapsedTime += Session.Settings.FlyingTickDuration;
        if (TimerText != null)
            TimerText.text = "" + ElapsedTime;

        OncekiPos = Session.PlayerAircraft.NMPosition;
        OncekiAlt = (int)Calculator.CAltitude;

        if (myAC != null)
            myAC.transform.localPosition = Session.PlayerAircraft.NMPosition;

        DistanceToPoint = Vector2.Distance(Session.PlayerAircraft.NMPosition, PointPos(point));

        if (point != prvWptIdx && DistanceToPoint < NextInstructionDistance)
        {
            MoveOnNextInstruction();
            PrvPoint = point;
        }
    }

    void MoveOnNextInstruction()
    {
        _currentInstructionIndex += 1;
        NewPoint = true;
        ATCAltitude = 0;
        if (myAC != null)
        {
            var label = myAC.GetComponent<Text>();
            if (label != null)
                label.text = "#";
        }
        prvWptIdx = point;
    }

    public float ILSDeviation(float course)
    {
        float deviation = Mathf.DeltaAngle(course, TrackToPoint(RW));

        if (Mathf.Abs(Mathf.DeltaAngle(course, Session.PlayerAircraft.HeadingDegrees)) > 90)
            deviation *= -1;

        if (DME() > 3f)
        {
            if (Mathf.Abs(deviation) < 3f && DME() < 23f)
                Session.State.ILSCapture = true;
        }
        else
        {
            Session.State.ILSCapture = false;
        }

        return deviation;
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

    public float LocDeviation(float course) => ComputeLocDeviationDegrees(course);

    public float GsAltitudeDeviation(float gs) =>
        (float)Calculator.CAltitude - Mathf.Tan(gs * Mathf.Deg2Rad) * DME() * 6076.12f;

    /// <summary>Signed glide-slope angular deviation in degrees. Positive = above GS.</summary>
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

    public float GsDeviation(float gs) => ComputeGsDeviationDegrees(gs);

    void UpdateIlsNeedles()
    {
        if (_currentLevelData?.levelInfo == null || LOCIndex == null || GSIndex == null)
            return;

        float course = _currentLevelData.levelInfo.Course;
        float glideSlope = _currentLevelData.levelInfo.GlideSlope;
        float dme = DME();

        bool ilsActive = Session.State.AppArmed || Session.State.ILSCapture || Session.State.LOCCaptured;
        if (!ilsActive)
        {
            LOCIndex.enabled = false;
            GSIndex.enabled = false;
            return;
        }

        ILSDeviation(course);
        float locDev = ComputeLocDeviationDegrees(course);
        float gsDev = ComputeGsDeviationDegrees(glideSlope);

        const float locDisplayMaxDme = 25f;
        const float gsDisplayMaxDme = 20f;
        const float locFullScaleDegrees = 2.5f;

        if (dme <= locDisplayMaxDme)
        {
            LOCIndex.enabled = true;
            float locNeedleX = Mathf.Clamp(locDev / locFullScaleDegrees * 1243f, -1243f, 1243f);
            LOCIndex.transform.localPosition = new Vector2(locNeedleX, -645);
        }
        else
        {
            LOCIndex.enabled = false;
        }

        if (dme <= gsDisplayMaxDme && Mathf.Abs(locDev) < 10f)
        {
            GSIndex.enabled = true;
            GSIndex.transform.localPosition = new Vector2(1373, Mathf.Clamp(-gsDev * 1500f, -541f, 541f));
        }
        else
        {
            GSIndex.enabled = false;
        }
    }

    void DescentCheck()
    {
        var rawAlt = "0";
        if (Session.VisibleRoute != null &&
            Session.ActiveRoute.GetPoint(Session.VisibleRoute.FirstAltRegulationNodeId, out var altRegulationNode, out _))
            rawAlt = altRegulationNode.RawAltitude;

        DataHandler.ParseAltRegulation(rawAlt, out var altAbove, out var altBelow, out var altExact);
        int altRef = altBelow > altExact ? altBelow : altExact;

        if (Mathf.Abs((int)Calculator.CAltitude - ATCAltitude) > 300 &&
            Mathf.Abs((int)Calculator.CAltitude - altRef) > 300)
        {
            // Descent compliance is tracked; ATC color follows XFR channel state.
        }
        else
        {
            CancelInvoke(nameof(DescentCheck));
            Atc2.text = "";
            ClearAtcCommand(2);
        }

        isDescentChecked = true;
    }
}
