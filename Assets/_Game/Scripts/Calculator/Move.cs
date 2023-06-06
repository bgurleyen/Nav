
using System.Collections;
using System.Collections.Generic;
using Gamelogic.Extensions;
using Navigation;
using UnityEngine;
using UnityEngine.UI;

[System.Serializable]
public class Move : Singleton<Move>
{
    private int Level = Calculator.Level;
    private float GameSpeed = 1;

    public LevelData[] otherACLevel;

    public static float Perpend;

    public Dictionary<string, Vector2> ACPositions = new Dictionary<string, Vector2>();
    public Dictionary<string, string> ACTexts = new Dictionary<string, string>();

    public GameObject  myAC;
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
    private bool isDescentChecked, isSpeedChecked;
    private int modD = 0, modS = 0;
    private int RawSpeed = 0;
    private int AltAbove, AltBelow, AltExact;
    private double FuelPenalty = 0;


    private OtherAC[] _otherACs;

    private LevelData CurrentLevel => otherACLevel[Level];

    float PrvTrackToPoint = 0, hyp;
    int _currentInstructionIndex = 0;
    int prvWptIdx = -1;
    int point = 1, mode, Cmode = 1, VS, VS_nx, Speed, Speed_nx;
    long Altitude;
    string RawAlt = "";

    private void Start()
    {
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
            VirtualPtsPos[j].x = Pos.x + CurrentLevel.VirtualPoints[j - 1].x -
                                 CurrentLevel.VirtualPoints[20].x;
            VirtualPtsPos[j].y = Pos.y + CurrentLevel.VirtualPoints[j - 1].y -
                                 CurrentLevel.VirtualPoints[20].y;
            pt.transform.localPosition = VirtualPtsPos[j];
        }

        myAC = GameObject.Find("AC (0)"); // Init my AC

        Atc1.text = "";
        Atc2.text = "";
        Atc3.text = "";


        var otherACsCount = CurrentLevel.otherACnr.Length;
        _otherACs = new OtherAC[otherACsCount];
        for (var i = 0; i < otherACsCount; i++)
        {
            _otherACs[i] = new OtherAC(i, CurrentLevel);
        }

        float PrvTrackToPoint = 0, hyp;
        _currentInstructionIndex = 0;
        int prvWptIdx = -1;
        int point = 1, mode, Cmode = 1, VS, VS_nx, Speed, Speed_nx;
        long Altitude;
    }



    string turnDirection(float newHdg)
    {
        return (Mathf.DeltaAngle(Calculator.RHeading, newHdg) >= 0) ? "Right " : "Left "; //change Rheading to C
    }

    string nxTostring(int nx)
    {
        return (nx == 1) ? " or greater " : (nx == 2) ? " or less " : "";
    }

    float DistanceFromRoute()
    {
        return (Mathf.Abs(Mathf.Sin(Mathf.Abs(TrackToPoint(point) - PrvTrackToPoint) * Mathf.Deg2Rad)) * hyp);
    }


    public void Tick()
    {
        for (int i = 0; i < _otherACs.Length; i++)
        {
            _otherACs[i].Tick(ACTexts, ACPositions);
        }

        CheckAirplaneMove();
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

    public float LocDeviation(float course)
    {
        float Deviation = Mathf.DeltaAngle(course, TrackToPoint(16));

        if (Mathf.Abs(Mathf.DeltaAngle(course, Session.PlayerAircraft.HeadingDegrees)) > 90) Deviation *= -1;
        if (((Mathf.Abs(Deviation) < 35) && (DME() < 10)) || ((Mathf.Abs(Deviation) < 10) && (DME() < 25)))
        {
            LOCIndex.enabled = true;
            LOCIndex.transform.localPosition = new Vector2(Mathf.Clamp(Deviation * 1000, -1243, 1243), -645);
        }
        else
        {
            LOCIndex.enabled = false;
        }

        return Deviation;
    }

    public float DME()
    {
        return Vector2.Distance(Session.PlayerAircraft.NMPosition, PointPos(16));
    } // Distance from RW

    private void SpeedCheck()
    {

        if
            (((modS == 0) && ((Calculator.CSpeed > ATCSpeed + 10) || (Calculator.CSpeed < ATCSpeed - 10))) ||

             ((modS == 1) && (Calculator.CSpeed < ATCSpeed - 10)) ||

             ((modS == 2) && (Calculator.CSpeed > ATCSpeed + 10)))
        {

            Atc3.color = Color.red;
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
    {
        var currentInstruction = CurrentLevel.ATCs[_currentInstructionIndex];
        
        point = currentInstruction.point;
        mode = currentInstruction.mode;
        Altitude = currentInstruction.Altitude;
        VS = currentInstruction.VS;
        VS_nx = currentInstruction.VS_nx;
        Speed = currentInstruction.Speed;
        Speed_nx = currentInstruction.Speed_nx;



        hyp = Vector2.Distance(Session.PlayerAircraft.NMPosition, PointPos(point));


        if (point < 50 && point > 1) RawSpeed = Session.ActiveRoute.Points[point - 1].RawSpeed;

        if (point < 50 && point > 1) RawAlt = (Session.ActiveRoute.Points[point - 1].RawAltitude);
        DataHandler.ParseAltRegulation(RawAlt, out AltAbove, out AltBelow, out AltExact); // FMS Altitude Limit


        Debug.Log(point + ".   " + RawSpeed + "   /  " + AltExact + "   " + AltAbove + "A  " + AltBelow + "B" +
                  "    FP:" + FuelPenalty + "   MxSpd: " + ATCSpeed);

        //   Debug.Log(" N:  " + LegsScreen.VisibleRoute.FirstSpeedRegulationNodeId); // Correct this

        if (NewPoint)
        {

            Atc1.text = mode == 1 ? "Proceed direct to  " + Session.OriginalReferenceRoute.Points[point].Name :
                mode == 2 ? "Turn " + turnDirection(TrackToPoint(point)) + "Heading " + TrackToPoint(point) :
                "";


            string s = VS < 0 ? ", ROD " + (-VS) + " fpm" + nxTostring(VS_nx) : "";

            if (Altitude > 0) Atc2.text = "Descent altitude " + Altitude + " feet" + s;

            Atc3.text = Speed > 0 ? "Speed " + Speed + " knots " + nxTostring(Speed_nx) :
                Speed == 0 ? Atc3.text : "";

            PrvTrackToPoint = TrackToPoint(point);
            Atc1.color = Color.green;
            if (mode > 0) Cmode = mode;
            if (Cmode == 1) FuelPenaltyAtFMCAltConstain(); // Check  Alt constrains on point for penalty

            NewPoint = false;
        }
        else // Not New
        {

            Perpend = Mathf.Sin((TrackToPoint(point) - PrvTrackToPoint) * Mathf.Deg2Rad) > 0
                ? PrvTrackToPoint + 90
                : PrvTrackToPoint - 90;

            //Debug.Log(DistanceFromRoute() + "   Pp: " + Perpend + "  Tp: " + TrackToPoint(point) + " prv:" + PrvTrackToPoint + " hyp: " + hyp + " Point: " + point );
            //Debug.Log("Loc : "+ LocDeviation(272) + "  G/S : " + GsDeviation(3) + "  D: " + DME());

            float dev = LocDeviation(272);
            float gsD = GsDeviation(3);

            if (DistanceFromRoute() > 1) //Warning   
            {

                if (Mathf.Abs(Mathf.DeltaAngle(Calculator.RHeading, Perpend)) >=
                    90) // Hdg rota tracki ve +-90 arasinda
                {
                    //Time.timeScale = 0;
                }

                if (mode == 2)
                    Atc1.text = "Turn " + turnDirection(TrackToPoint(point)) + "Heading " +
                                (int)TrackToPoint(point);
                Atc1.color = Color.red;

            }
            else Atc1.color = Color.white;

        }

        if ((Altitude > 0) && (Altitude != ATCAltitude)) //Descent clr changed
        {
            Atc2.color = Color.green;
            isDescentChecked = false;
            ATCAltitude = Altitude;
            ATCVS = VS;
            modD = VS == 0 ? -1 : VS_nx;
            CancelInvoke(nameof(DescentCheck));
            InvokeRepeating(nameof(DescentCheck), 10f, 1f);
        }

        if (isDescentChecked) Atc2.color = Color.white;

        if (((Speed > 0) && (Speed != ATCSpeed))
            || ((RawSpeed > 0) && (RawSpeed < ATCSpeed) && Cmode == 1)) //Speed clr changed
        {
            Atc3.color = Color.green;
            isSpeedChecked = false;
            if ((Speed > 0) && (Speed != ATCSpeed)) ATCSpeed = Speed;
            if ((RawSpeed > 0) && (RawSpeed < ATCSpeed) && (Speed_nx != 1) && (Cmode == 1)) ATCSpeed = RawSpeed;
            modS = ((RawSpeed > 0) && (RawSpeed < Speed) && (Speed_nx != 1) && (Cmode == 1)) ? 2 : Speed_nx;
            CancelInvoke(nameof(SpeedCheck));
            InvokeRepeating(nameof(SpeedCheck), Mathf.Abs((float)Calculator.CSpeed - ATCSpeed) * 2.5f,
                1f); //  secs before warning
        }

        if (isSpeedChecked) Atc3.color = Color.white;

        if (Speed == -1) CancelInvoke(nameof(SpeedCheck));

    }

    public void CheckAirplaneMove()
    {

        ATCCall();


        if ((_currentInstructionIndex < CurrentLevel.ATCs.Length - 1))
        {


            ElapsedTime += Calculator.Acceleration();
            TimerText.text = "" + ElapsedTime;

            ATCCall();

            OncekiPos = Session.PlayerAircraft.NMPosition;
            OncekiAlt = (int)Calculator.CAltitude;
            myAC.transform.localPosition =
                Session.PlayerAircraft.NMPosition; //move AC on EditMap

            float V = Vector2.Distance(Session.PlayerAircraft.NMPosition, PointPos(point));

            //Debug.Log(" V :" + V+ " P :" +point);
            if ((point != prvWptIdx) && ((V < 1)))
            {
                _currentInstructionIndex += 1;
                NewPoint = true;

                ATCAltitude = VS;
                myAC.GetComponent<UnityEngine.UI.Text>().text = "#";
                prvWptIdx = point;
            }
        }
    }

    public float GsDeviation(float GS)
    {
        float DescentAngle = Mathf.Atan2((float)Calculator.CAltitude, DME() * 6076.12f) * Mathf.Rad2Deg;
        float Deviation = -Mathf.DeltaAngle(GS, DescentAngle);


        if ((Mathf.Abs(LocDeviation(272)) < 5) && (DME() < 20))
        {
            GSIndex.enabled = true;
            GSIndex.transform.localPosition = new Vector2(1373, Mathf.Clamp(Deviation * 1500, -541, 541));
        }
        else
        {
            GSIndex.enabled = false;
        }

        return Deviation;

        // Calt- RW alt  , pos 16 -->> Rw point

    }

    private void DescentCheck()
    {
        int AltAbove, AltBelow, AltExact, AltRef; // First Altitude Restriction
        // Todo Birol, fix for when there are no alt regulation nodes left
        string RawAlt = (Session.ActiveRoute.Points[Session.VisibleRoute.FirstAltRegulationNodeId].RawAltitude);
        DataHandler.ParseAltRegulation(RawAlt, out AltAbove, out AltBelow, out AltExact);

        AltRef = AltBelow > AltExact ? AltBelow : AltExact;

        if ((Mathf.Abs((int)Calculator.CAltitude - ATCAltitude) > 300) &&
            (Mathf.Abs((int)Calculator.CAltitude - AltRef) > 300))
        {
            if ((Calculator.CVS > -300) ||

                ((modD == 0) && ((Calculator.CVS > ATCVS + 300) || (Calculator.CVS < ATCVS - 300))) ||

                ((modD == 1) && (Calculator.CVS > ATCVS + 300)) ||

                ((modD == 2) && (Calculator.CVS < ATCVS - 300))) Atc2.color = Color.red;
        }
        else
        {
            CancelInvoke(nameof(DescentCheck));
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

