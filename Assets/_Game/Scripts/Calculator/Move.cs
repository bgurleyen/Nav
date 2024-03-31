using System.Collections.Generic;
using Gamelogic.Extensions;
using Navigation;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;

[System.Serializable]
public class Move : Singleton<Move>
{
    public static float Perpend,teta;

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
    private bool isDescentChecked, isSpeedChecked,isRouteChecked;
    private int modD = 0, modS = 0;
    private int RawSpeed = 0;
    private int AltAbove, AltBelow, AltExact;
    private double FuelPenalty = 0;

    private LevelDataScriptableObject _currentLevelData;
    private OtherAC[] _otherACs;
    float DistanceToPoint;

    float PrvTrackToPoint = 0, hyp;
    int prvWptIdx = -1;
    int point = 1, mode, Cmode = 1, VS, VS_nx, Speed, Speed_nx;
    long Altitude;
    string RawAlt = "";
    int _currentInstructionIndex = 0;
    int RW ;
    float NextInstructionDistance = 1.3f;

    public Button XFR1, XFR2, XFR3;
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
        return (nx == 1) ? " or greater " : (nx == 2) ? " or less " :  ""; 
    }

    private float DistanceFromRoute()
    {
        return (Mathf.Abs(Mathf.Sin(Mathf.Abs(TrackToPoint(point) - PrvTrackToPoint) * Mathf.Deg2Rad)) * hyp);
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
        float Track = Calculator.CTrack*Mathf.Deg2Rad;


        float TurnRadius = 1.6f * Calculator.GS/280;
        

        int Sign = Mathf.DeltaAngle(Calculator.CTrack, TrackToPoint(pt)) >= 0 ? 1 : -1;

        float H = TurnRadius * (1 - Mathf.Cos(alfa)); //Horizantal
        float V = Mathf.Sin(alfa) * TurnRadius;  //Vertical
        float x2 = x1 + Sign * (H * Mathf.Cos(Track) + V * Mathf.Sin(Track));
        float y2 = y1 + Sign * (V * Mathf.Cos(Track) - H * Mathf.Sin(Track));


        Calculator.WindElements WE = Calculator.CalculateWindElements(Calculator.CAltitude,Calculator.CSpeed, (int)TrackToPoint(x2, y2, pt));
        return (int) (Mathf.Round(TrackToPoint(x2, y2, pt)) + WE.HeadingWindAddition);
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
        //mode      pt  Alt VS  nx          Speed   nx
        //0..NoChg				0..exact	        0..exact
        //1..DCT				1..min	   	        1..min
        //2..HDG				2..max		        2..max
        //                      3..CLEAR ILS       >2..NextInstructionDistance

    {
        var currentInstruction = _currentLevelData.ATCs[_currentInstructionIndex];

        int oncemode = mode;

        point = currentInstruction.point;
        mode = (mode ==11) ? 1 : currentInstruction.mode;
        Altitude = currentInstruction.Altitude;
        VS = currentInstruction.VS;
        VS_nx = currentInstruction.VS_nx;
        Speed = currentInstruction.Speed;
        Speed_nx = currentInstruction.Speed_nx;

        hyp = Vector2.Distance(Session.PlayerAircraft.NMPosition, PointPos(point));


        if (point < 50 && point > 1) RawSpeed = Session.OriginalReferenceRoute.Points[point - 1].RawSpeed;

        if (point < 50 && point > 1) RawAlt = (Session.OriginalReferenceRoute.Points[point - 1].RawAltitude);

        DataHandler.ParseAltRegulation(RawAlt, out AltAbove, out AltBelow, out AltExact); // FMS Altitude Limit


        // Debug.Log(point + ".   " + RawSpeed + "   /  " + AltExact + "   " + AltAbove + "A  " + AltBelow + "B" +
        //           "    FP:" + FuelPenalty + "   MxSpd: " + ATCSpeed);

        //   Debug.Log(" N:  " + LegsScreen.VisibleRoute.FirstSpeedRegulationNodeId); // Correct this

        if (NewPoint)
        {
        

            Atc1.text = mode == 1 ? "Proceed direct to  " + Session.OriginalReferenceRoute.Points[point].Name :
                mode == 2 ? "Turn " + TurnDirection(TrackToPoint(point)) + "Heading " +TrackToPointFactored(point) : "";

      


            string s = VS < 0 ? ", ROD " + (-VS) + " fpm" + NxToString(VS_nx) : "";

            if (Altitude > 0) Atc2.text = "Descent altitude " + Altitude + " feet" + s;

            if (VS_nx == 3)
            {
                Atc2.text += " CLEAR ILS APPROACH ";
                Session.State.AppArmed=true;
     
            }

            Atc3.text = Speed > 0 ? "Speed " + Speed + " knots " + NxToString(Speed_nx) :
                Speed == 0 ? Atc3.text : "";

            NextInstructionDistance = Speed_nx > 2 ? Speed_nx : 1.3f;

            XFR1.interactable = mode == 2 ? true : false;
            XFR2.interactable = Altitude > 0 ? true : false;
            XFR3.interactable = (Speed > 0) ? true : false;

            if (Speed > 0) XFRSpeed = Speed;
            if (mode == 2) XFRHdg = TrackToPointFactored(point);
            if (Altitude > 0) XFRAltitude = Altitude;


            PrvTrackToPoint = TrackToPoint(point);
            Atc1.color = Color.green;
            if (mode > 0) Cmode = mode;
            if (Cmode == 1) FuelPenaltyAtFMCAltConstain(); // Check  Alt constrains on point for penalty

            NewPoint = false;

           // if (mode > 0 || XFR2.interactable || XFR3.interactable) SlowDown(); Remove//
        }
        else // Not New
        {
            if (XFRHdg == Calculator.RHeading) XFR1.interactable = false;
            if (XFRAltitude == Calculator.RAltitude) XFR2.interactable = false;
            if (XFRSpeed == Calculator.RSpeed) XFR3.interactable = false;

            // Debug.Log(XFRHdg +"H"+ Calculator.RHeading+  "     "+ XFRAltitude +"A"+ Calculator.RAltitude + "   " + Speed +"S"+ Calculator.RSpeed);

            Perpend = Mathf.Sin((TrackToPoint(point) - PrvTrackToPoint) * Mathf.Deg2Rad) > 0
                ? PrvTrackToPoint + 90
                : PrvTrackToPoint - 90;

            
            float dev = LocDeviation(272);
            float gsD = GsDeviation(3);

            float x = hyp * Mathf.Cos(Mathf.DeltaAngle(TrackToPoint(point), PrvTrackToPoint) * Mathf.Deg2Rad);

            teta = Mathf.Atan2(DistanceFromRoute()-1.3f,x) * Mathf.Rad2Deg;//noktanin 1.3 nm uzerine aci

            //Debug.Log(DistanceFromRoute() + "   Pp: " + Perpend + "  Tp: " + TrackToPoint(point) + 
            //       " prv:" + PrvTrackToPoint + " hyp: " + hyp + " x: " + x + " Pt: " + point);

            if (DistanceFromRoute() > Mathf.Tan(20 * Mathf.Deg2Rad) * x + 1.3) //(20 degrees koni) Warning 
            {

                if (Mathf.Abs(Mathf.DeltaAngle(Calculator.RTrack, Perpend)) >= 90 - teta) // Hdg rota tracki ve +-70 arasinda
                {
                    //SlowDown();
                    //Time.timeScale = 0;  //Stop at Borders
                    Atc1.color = Color.red;

                    int FactoredAngleToPoint = TrackToPointFactored(point);
                    int FactoredAngleDifference = Mathf.Abs((int)Mathf.DeltaAngle(Calculator.CTrack, TrackToPointFactored(point)));

                    if (Calculator.RHeading != FactoredAngleToPoint)
                    {
                        if (FactoredAngleDifference < 20 * DistanceToPoint)

                        {
                            string WarningString = mode < 2 ? " Proceed Direct to " + Session.OriginalReferenceRoute.Points[point].Name
                                                            : "Fly Heading " + FactoredAngleToPoint;
                          //  EditorUtility.DisplayDialog("PILOT RESPONSE", "Please comply with instructions" + WarningString, "OK");
                            Calculator.RHeading = FactoredAngleToPoint;
                        }
                        else
                        {
                           // EditorUtility.DisplayDialog("PILOT RESPONSE ,FUEL PENALTY!! ",
                           //                             "An instruction was missed, follow the new clearance with 100kg fuel penalty ", "OK");
                            if (mode < 2) mode = 11;//if next mode zero 

                            Calculator.RHeading = TrackToPointFactored(_currentLevelData.ATCs[_currentInstructionIndex + 1].point);
                            MoveOnNextInstruction();

                            FuelPenalty += 0.01; //100 kg FuelPenalty for shortcut
                        }

                    }
                }

            }
        //    else  Atc1.color = Color.white;
         
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


        if ((_currentInstructionIndex < _currentLevelData.ATCs.Length - 1))
        {


            ElapsedTime += Session.Settings.FlyingTickDuration;
            TimerText.text = "" + ElapsedTime;

            ATCCall();

            OncekiPos = Session.PlayerAircraft.NMPosition;
            OncekiAlt = (int)Calculator.CAltitude;
            myAC.transform.localPosition = Session.PlayerAircraft.NMPosition; //move AC on EditMap

             DistanceToPoint = Vector2.Distance(Session.PlayerAircraft.NMPosition, PointPos(point));

            if ((point != prvWptIdx) && ((DistanceToPoint < NextInstructionDistance)))  // next instruction NextInstructionDistance nm before next pt
            {
                MoveOnNextInstruction();
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
    public float LocDeviation(float course)
    {
        float Deviation = Mathf.DeltaAngle(course, TrackToPoint(RW));

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

    public  float GsDeviation(float GS)
    {
        float DescentAngle = Mathf.Atan2((float)Calculator.CAltitude, DME() * 6076.12f) * Mathf.Rad2Deg;
        float Deviation = Mathf.DeltaAngle(GS, DescentAngle);

        if (Session.State.GSCaptured) Deviation = 0;

        if ((Mathf.Abs(LocDeviation(272)) < 5) && (DME() < 20))
        {
            GSIndex.enabled = true;
            GSIndex.transform.localPosition = new Vector2(1373, Mathf.Clamp(-Deviation * 1500, -541, 541));
        }
        else
        {
            GSIndex.enabled = false;
        }
       // Debug.Log(Deviation + "   L" + LocDeviation(272) + "  " + "   G" + Deviation + "  " );
  
        return Deviation;

    }

    private void DescentCheck()
    {
        int AltAbove, AltBelow, AltExact, AltRef; // First Altitude Restriction

        // todo birol : there is another variable RawAlt in the begining of this class - should they be the same ?
        var rawAlt = "0";
        if (Session.ActiveRoute.GetPoint(Session.VisibleRoute.FirstAltRegulationNodeId, out var altRegulationNode, out _))
        {
            rawAlt = altRegulationNode.RawAltitude;
        }

        DataHandler.ParseAltRegulation(rawAlt, out AltAbove, out AltBelow, out AltExact);

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

