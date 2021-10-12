
using System.Collections;
using System.Collections.Generic;
using Gamelogic.Extensions;
using UnityEngine;
using UnityEngine.UI;

[System.Serializable]
public class Move : Singleton<Move>
{
    RouteScriptableObject Route;
    int Level = Calculator.Level;
    float GameSpeed = 1;
    public LevelInfoScriptableObject[] levelsInfoData;


    [System.Serializable]
    public struct otherACsStruct
    {
        public OtherACScriptableObject[] otherACnr;
    }
    public otherACsStruct[] otherACLevel;
    public VirtualPointsScriptableObject[] virtualPoints;
    public ATCInstructionsScriptableObject[] aTCs;


    public GameObject pt, AC, myAC;
    public Image LOCIndex, GSIndex;

    public Text Atc1, Atc2, Atc3;
    public Text TimerText;
    int ElapsedTime = 0;

    public string ATtc1; // maybe it's possible to use like this
    bool NewPoint = true;
    Vector3 OncekiPos, PrvPos;
    Vector2[] VirtualPtsPos = new Vector2[100];
    Vector2[] TempPtsPos = new Vector2[100];
    long ATCAltitude, OncekiAlt;
    int ATCVS, ATCSpeed;
    bool isDescentChecked, isSpeedChecked;
    int modD = 0, modS = 0;

    public static float Perpend;

    public Dictionary<string, Vector2> ACPositions = new Dictionary<string, Vector2>();
    public Dictionary<string, string> ACTexts = new Dictionary<string, string>();



    void Start()

    {
        // var somePoint = GameManager.Instance.ActiveSet.Points[3].Clone();

        // Altitude computed: GameManager.Instance.ActiveSet.Points[4].Altitude.ComputedValue

        Vector2 Pos = new Vector2(0, 0);
        Route = GameManager.Instance.ActiveRoute.Clone();
    
        for (int j = 1; j < Route.Points.Length; j++)                                                    // Locate the points on EditMap
        {
            Pos = Route.GetCartesianPosition(j);
            GameObject pt = GameObject.Find("pt (" + j + ")");

            pt.transform.localPosition = Pos;

            TempPtsPos[j] = Pos;
        } 
        
        for (int j = 1; j < 21; j++)                                                      //Locate Virtual points on EditMap
        {
        
             pt = GameObject.Find("pt (" + (j + 50) + ")");
            VirtualPtsPos[j].x = Pos.x + virtualPoints[Level].VirtualPointsItems[j - 1].x - virtualPoints[Level].VirtualPointsItems[20].x;
            VirtualPtsPos[j].y = Pos.y + virtualPoints[Level].VirtualPointsItems[j - 1].y - virtualPoints[Level].VirtualPointsItems[20].y;
            pt.transform.localPosition =  VirtualPtsPos[j];
        }

        for (int j = 0; j < 11; j++)
        {
            GameObject A = GameObject.Find("AC (" + (j) + ")");                            // Init Other ACs 
        }

        myAC = GameObject.Find("AC (0)");                                                  // Init my AC

        Atc1.text = "";
        Atc2.text = "";
        Atc3.text = "";

        //StartCoroutine(MoveAC(0));
        //StartCoroutine(MoveAC(1));
        //StartCoroutine(MoveAC(2));
        //StartCoroutine(MoveAC(3));
        //StartCoroutine(MoveAC(4));
        //StartCoroutine(MoveAC(5));
        //StartCoroutine(MoveAC(6));


        StartCoroutine(MoveMyAC());

    }

    Vector2 pointPos(int pt)
    {
        int ptCount = virtualPoints[Level].VirtualPointsItems.Length;
        return  pt < 50 ? Route.GetCartesianPosition(pt) : VirtualPtsPos[pt - 50] ;
    }
    float TrackToPoint(int pt)
    {



        float x1 = GameManager.Instance.Aircraft.PositionFreeOrOnCurvedPath.x;
        float y1 = GameManager.Instance.Aircraft.PositionFreeOrOnCurvedPath.y;
        float x2 = pointPos(pt).x;
        float y2 = pointPos(pt).y;

      
        float Angle = Mathf.Atan2(x2 - x1, y2 - y1) * Mathf.Rad2Deg;
        if (Angle < 0) Angle += 360;
        return Angle;
    }
    public float LocDeviation(float course)
    {
        float  Deviation = Mathf.DeltaAngle(course, TrackToPoint(16));

        if (Mathf.Abs(Mathf.DeltaAngle(course, GameManager.Instance.Aircraft.HeadingDegrees)) > 90) Deviation *=-1;
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
    public float GsDeviation(float GS)
    {
           float DescentAngle = Mathf.Atan2((float)Calculator.CAltitude,DME() * 6076.12f) * Mathf.Rad2Deg;
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
    public float DME()
    {
        return Vector2.Distance(GameManager.Instance.Aircraft.PositionFreeOrOnCurvedPath, pointPos(16));
    } // Distance from RW
    public IEnumerator MoveMyAC()
    {
        float PrvTrackToPoint = 0, hyp;
        int i = 0, prvWptIdx = -1;
        int point=1, mode, VS, VS_nx, Speed, Speed_nx;
        long Altitude;

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
        void ATCCall()

        {
            void LocateSlidingPoint()
            {
            
                 int nextPoint = aTCs[Level].ATCInstrucitonItems[i + 1].point;
                int lastPoint = aTCs[Level].ATCInstrucitonItems[i + 2].point;
                Vector2 Now = GameManager.Instance.Aircraft.PositionFreeOrOnCurvedPath;

                float NM;
                int CheckPointTime= levelsInfoData[Level].CheckPointTime; 
                //int CheckPointTime = 680;
                int ptCount = virtualPoints[Level].VirtualPointsItems.Length; 

                if (nextPoint == 92)
                {
                    float d = Vector2.Distance(Now, pointPos(point)) +
                              Vector2.Distance(pointPos(point), pointPos(nextPoint))+
                              Vector2.Distance(pointPos(nextPoint), pointPos(lastPoint));
                    float a = Vector2.Distance(Now, pointPos(point + 1)) +
                              Vector2.Distance(pointPos(point + 1), pointPos(nextPoint + 1))+
                              Vector2.Distance(pointPos(nextPoint + 1), pointPos(lastPoint));

                    float B1 = Vector2.Distance(pointPos(point), pointPos(point + 1));
                    float B2 = Vector2.Distance(pointPos(nextPoint), pointPos(nextPoint + 1));

                    NM = (float) (d + (CheckPointTime - ElapsedTime) * Calculator.GS / 3600);

                    float x1 = (NM - d) / ((a - d)) * B1;
                    if (Mathf.Abs(x1) > Mathf.Abs(B1)) x1 = B1 * Mathf.Sign(x1);  // Do not ecxeed half distance
                    float x2 = (NM - d) / ((a - d)) * B2;
                    if (Mathf.Abs(x2) > Mathf.Abs(B2)) x2 = B2 * Mathf.Sign(x2);



                    VirtualPtsPos[ptCount-4] = Vector2.MoveTowards(pointPos(point), pointPos(point + 1), x1);
                    VirtualPtsPos[ptCount-2] = Vector2.MoveTowards(pointPos(nextPoint), pointPos(nextPoint + 1), x2);

                    GameObject pt = GameObject.Find("pt (" + 90 + ")");
                    pt.transform.localPosition = VirtualPtsPos[ptCount-4];
                    pt = GameObject.Find("pt (" + 92 + ")");
                    pt.transform.localPosition = VirtualPtsPos[ptCount-2];                    
                    
                }
                else
                {
                    float d = Vector2.Distance(Now, pointPos(point))+
                              Vector2.Distance(pointPos(point), pointPos(nextPoint));
                    float a = Vector2.Distance(Now, pointPos(point + 1))+
                               Vector2.Distance(pointPos(point + 1), pointPos(nextPoint));
                  
                    float B1 = Vector2.Distance(pointPos(point), pointPos(point + 1));

                    NM = (float)(d + (CheckPointTime - ElapsedTime) * Calculator.GS / 3600);

                    float x1 = (NM - d) / ((a - d)) * B1;
                    if (Mathf.Abs(x1) > Mathf.Abs(B1)) x1 = B1 * Mathf.Sign(x1);  // Do not ecxeed half distance

                    VirtualPtsPos[ptCount - 4] = Vector2.MoveTowards(pointPos(point), pointPos(point + 1), x1);

                    GameObject pt = GameObject.Find("pt (" + 90 + ")");
                    pt.transform.localPosition = VirtualPtsPos[ptCount - 4];
                }
            }


            point = aTCs[Level].ATCInstrucitonItems[i].point;
            mode = aTCs[Level].ATCInstrucitonItems[i] .mode;
            Altitude = aTCs[Level].ATCInstrucitonItems[i].Altitude;
            VS = aTCs[Level].ATCInstrucitonItems[i].VS;
            VS_nx = aTCs[Level].ATCInstrucitonItems[i].VS_nx;
            Speed = aTCs[Level].ATCInstrucitonItems[i].Speed;
            Speed_nx = aTCs[Level].ATCInstrucitonItems[i].Speed_nx;

            hyp = Vector2.Distance(GameManager.Instance.Aircraft.PositionFreeOrOnCurvedPath, pointPos(point));

            //Debug.Log("Point :" + point + "  coo: " + pointPos(90) + "Mode " + mode);
            if (NewPoint)
            {
                if (point == 90) LocateSlidingPoint();

                Atc1.text = mode == 1 ? "Proceed direct to  " + Route.Points[point].Name :
                            mode == 2 ? "Turn " + turnDirection(TrackToPoint(point)) + "Heading " + TrackToPoint(point) : "";

                

                string s = VS < 0 ? ", ROD " + (-VS) + " fpm" + nxTostring(VS_nx) : "";
                Atc2.text = Altitude > 0 ? "Descent altitude " + Altitude + " feet" + s : "";

                Atc3.text = Speed > 0 ? "Speed " + Speed + " knots " + nxTostring(Speed_nx) : Speed == 0 ? Atc3.text : "";

                PrvTrackToPoint = TrackToPoint(point);
                Atc1.color = Color.green;
                NewPoint = false;
            }
            else  // Not New
            {
              
                Perpend = Mathf.Sin((TrackToPoint(point) - PrvTrackToPoint)*Mathf.Deg2Rad)>0 ? PrvTrackToPoint+90 : PrvTrackToPoint-90;

                //Debug.Log(DistanceFromRoute() + "   Pp: " + Perpend + "  Tp: " + TrackToPoint(point) + " prv:" + PrvTrackToPoint + " hyp: " + hyp + " Point: " + point );
                //Debug.Log("Loc : "+ LocDeviation(272) + "  G/S : " + GsDeviation(3) + "  D: " + DME());

                float dev = LocDeviation(272);
                float gsD = GsDeviation(3);
                if (DistanceFromRoute() > 1) //Warning
                {
                  
                    if (Mathf.Abs(Mathf.DeltaAngle(Calculator.RHeading, Perpend)) >= 90) // Hdg rota tracki ve +-90 arasinda
                    {
                       // Time.timeScale = 0;
                    }

                    if (mode == 2) Atc1.text =  "Turn " + turnDirection(TrackToPoint(point)) + "Heading " + (int)TrackToPoint(point) ;
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
                CancelInvoke("DescentCheck");
                InvokeRepeating("DescentCheck", 10f, 1f);
            }
            if (isDescentChecked) Atc2.color = Color.white;

            if ((Speed > 0) && (Speed != ATCSpeed)) //Speed clr changed
            {
                Atc3.color = Color.green;
                isSpeedChecked = false;
                ATCSpeed = Speed;
                modS = Speed_nx;
                CancelInvoke("SpeedCheck");
                InvokeRepeating("SpeedCheck", 10f, 1f);
            }
            if (isSpeedChecked) Atc3.color = Color.white;

            if (Speed == -1) CancelInvoke("SpeedCheck");

        }

        while ((i < aTCs[Level].ATCInstrucitonItems.Length - 1))
        {
            yield return new WaitForSeconds(GameSpeed);

            ElapsedTime += Calculator.Acceleration();
            TimerText.text = "" + ElapsedTime;

            ATCCall();

            OncekiPos = GameManager.Instance.Aircraft.PositionFreeOrOnCurvedPath;
            OncekiAlt = (int)Calculator.CAltitude;
            myAC.transform.localPosition = GameManager.Instance.Aircraft.PositionFreeOrOnCurvedPath; //move AC on EditMap

            float V = Vector2.Distance( GameManager.Instance.Aircraft.PositionFreeOrOnCurvedPath, pointPos(point));

            //Debug.Log(" V :" + V+ " P :" +point);
             if ((point != prvWptIdx) && ((V < 1)))
                {
                    i += 1;
                NewPoint = true;

                ATCAltitude = VS;
                myAC.GetComponent<UnityEngine.UI.Text>().text = "#";
                prvWptIdx =point;
            }

        }
    }
    private IEnumerator MoveAC(int ACnr)
    { //Moves  other ACs based on the Route and Alt(Altitude) arrays.

        float dx, dy, hyp;
        float x, y;
        int i = 1;
        float Speed, SpeedCo;
        string s;
        Vector2 PtPos;
        int Point, AltitudeR;
        float AltitudeC = otherACLevel[Level].otherACnr[ACnr].ACItems[0].Altitude;
        Vector2 finalPosition = pointPos(otherACLevel[Level].otherACnr[ACnr].ACItems[0].Point);  //intial pos and alt
        int HeadingW = 0;

        var _aircraftKey = "AC (" + (ACnr + 1) + ")";

        void CollisionCheck()
        {

            float D = Vector2.Distance(GameManager.Instance.Aircraft.PositionFreeOrOnCurvedPath, finalPosition);

            int myACAlt = (int)Calculator.CAltitude;
            int ACAlt = (int)AltitudeC;

            if ((D < 2) && (Mathf.Abs(myACAlt - ACAlt) < 700))
            {
              //  AC.GetComponent<UnityEngine.UI.Text>().color = Color.clear;
              //  ACTexts[_aircraftKey] = "";

            }

        }

        while (i < otherACLevel[Level].otherACnr[ACnr].ACItems.Length)
        {

            yield return new WaitForSeconds(GameSpeed);


            Point = otherACLevel[Level].otherACnr[ACnr].ACItems[i].Point;
            AltitudeR = otherACLevel[Level].otherACnr[ACnr].ACItems[i].Altitude;
            Speed = otherACLevel[Level].otherACnr[ACnr].ACItems[i].Speed;

            PtPos = pointPos(Point);

            Calculator.WindElements WE = Calculator.CalculateWindElements(AltitudeR, Speed, HeadingW);

            float GS = WE.GS; // Convert the speed to Ground Speed

           // if (ACnr  == 3 ) Debug.Log("   A: " + AltitudeR +"    S: " + Speed + "    GS: " + GS + "  H:" + HeadingW);
            //if (ACnr == 3) Debug.Log("   F: " + finalPosition + "    pt: " + PtPos + "  Aci: " + HeadingW);

            if (!ACPositions.ContainsKey(_aircraftKey))
            {
                ACPositions.Add(_aircraftKey, Vector2.zero);
                ACTexts.Add(_aircraftKey, "");
            }


            AC = GameObject.Find(_aircraftKey);
            SpeedCo = GS / 360 * Calculator.Acceleration()*1.06f;

            x = finalPosition.x;
            y = finalPosition.y;

            dx = PtPos.x - x;
            dy = PtPos.y - y;

            HeadingW =  90- (int)(Mathf.Atan2(dy, dx)*Mathf.Rad2Deg);

            hyp = Mathf.Sqrt(dx * dx + dy * dy);

     
                AltitudeC -= ((AltitudeC - AltitudeR)) / hyp * SpeedCo / 10;
                finalPosition = new Vector2(x + dx / hyp * SpeedCo / 10, y + dy / hyp * SpeedCo / 10); //Advance
      
            AC.transform.localPosition = finalPosition;
            ACPositions[_aircraftKey] = finalPosition;

            s = (AltitudeC - Calculator.CAltitude < 0) ? "-" : "+";
            if (Mathf.Abs(AltitudeC - (float)Calculator.CAltitude) < 1000) s += "0";
            if (Mathf.Abs((AltitudeC - (int)Calculator.CAltitude)) < 6000)
            {
                s += (int)(Mathf.Abs(AltitudeC - (float)Calculator.CAltitude) / 100);
                AC.GetComponent<UnityEngine.UI.Text>().text = s;
            }
            ACTexts[_aircraftKey] = s;
            if ((hyp <= 1.5))
            {
                i += 1;
                AltitudeC = AltitudeR;
            }
            CollisionCheck();
        }
        AC.GetComponent<UnityEngine.UI.Text>().text = "";

    }
    void DescentCheck()
    {

        if (Mathf.Abs((int)Calculator.CAltitude - ATCAltitude) > 200)
        {
            if ((Calculator.CVS > -300) ||

                   ((modD == 0) && ((Calculator.CVS > ATCVS + 200) || (Calculator.CVS < ATCVS - 200))) ||

                   ((modD == 1) && (Calculator.CVS > ATCVS + 200)) ||

                   ((modD == 2) && (Calculator.CVS < ATCVS - 200))) Atc2.color = Color.red;
        }
        else
        {
            CancelInvoke("DescentCheck");
            Atc2.text = "";
        }
        isDescentChecked = true;
    }
    void SpeedCheck()
    {

        if
               (((modS == 0) && ((Calculator.CSpeed > ATCSpeed + 10) || (Calculator.CSpeed < ATCSpeed - 10))) ||

               ((modS == 1) && (Calculator.CSpeed < ATCSpeed - 10)) ||

               ((modS == 2) && (Calculator.CSpeed > ATCSpeed + 10))) Atc3.color = Color.red;

        isSpeedChecked = true;
    }

}

