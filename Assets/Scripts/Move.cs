
using System.Collections;
using System.Collections.Generic;
using Gamelogic.Extensions;
using UnityEngine;
using UnityEngine.UI;

[System.Serializable]
public class Move : Singleton<Move>
{

    int Level = Calculator.Level;
    float GameSpeed = 1;

    [System.Serializable]
    public struct otherACsStruct
    {
        public OtherACScriptableObject[] otherACnr;
    }
    public otherACsStruct[] otherACLevel;
    public VirtualPointsScriptableObject[] virtualPoints;
    public ATCInstructionsScriptableObject[] aTCs;


    public GameObject pt, AC, myAC;

    public Text Atc1, Atc2, Atc3;
    public string ATtc1; // maybe it's possible to use like this
    bool NewPoint = true;
    Vector3 OncekiPos, PrvPos;
    Vector2[] VirtualPtsPos = new Vector2[100];
    Vector2[] TempPtsPos = new Vector2[100];
    long ATCAltitude, OncekiAlt;
    int ATCVS, ATCSpeed;
    bool isDescentChecked, isSpeedChecked;
    int modD = 0, modS = 0;


    public Dictionary<string, Vector2> ACPositions = new Dictionary<string, Vector2>();
    public Dictionary<string, string> ACTexts = new Dictionary<string, string>();



    void Start()

    {
        // var somePoint = GameManager.Instance.ActiveSet.Points[3].Clone();

        // Altitude computed: GameManager.Instance.ActiveSet.Points[4].Altitude.ComputedValue

        Vector2 Pos = new Vector2(0, 0);

        for (int j = 1; j < 17; j++)                                                    // Locate the points on EditMap
        {
            Pos = GameManager.Instance.PathLines.ComputedLines[j].EndPosition;
            GameObject pt = GameObject.Find("pt (" + j + ")");

            pt.transform.localPosition = Pos;
            TempPtsPos[j] = Pos;
        }
        for (int j = 1; j < 13; j++)                                                      //Locate Virtual points on EditMap
        {
            pt = GameObject.Find("pt (" + (j + 50) + ")");
            VirtualPtsPos[j].x = virtualPoints[Level].VirtualPointsItems[j].x;
            VirtualPtsPos[j].y = virtualPoints[Level].VirtualPointsItems[j].y;
            pt.transform.localPosition = VirtualPtsPos[j];
        }
        for (int j = 0; j < 4; j++)
        {
            GameObject A = GameObject.Find("AC (" + (j) + ")");                            // Init Other ACs 
        }

        myAC = GameObject.Find("AC (0)");                                                  // Init my AC

        Atc1.text = "";
        Atc2.text = "";
        Atc3.text = "";

        StartCoroutine(MoveAC(0));
        StartCoroutine(MoveAC(1));
        StartCoroutine(MoveAC(2));
        StartCoroutine(MoveMyAC());
    }

    private IEnumerator MoveMyAC()
    {
        float A0 = 0, h;
        int i = 0, prvWptIdx = 0;

        int point, mode = 0, VS, VS_nx, Speed, Speed_nx;
        long Altitude;

        int TrackToPoint(int K)
        {
            // computedLines start from 1. (0 is an added empty line)


            Vector2 PtPos = (K < 50) ? (Vector2)GameManager.Instance.PathLines.ComputedLines[K + 1].EndPosition :
                                        VirtualPtsPos[K - 50];


            float x1 = GameManager.Instance.Aircraft.PositionFreeOrOnCurvedPath.x;
            float y1 = GameManager.Instance.Aircraft.PositionFreeOrOnCurvedPath.y;
            float x2 = PtPos.x;
            float y2 = PtPos.y;
            float dx = x2 - x1;
            float dy = y2 - y1;
            h = Mathf.Sqrt(dx * dx + dy * dy);
            int Angle = (int)(Mathf.Atan2(x2 - x1, y2 - y1) * Mathf.Rad2Deg);
            if (Angle < 0) Angle += 360;
            return Angle;
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
            return (Mathf.Abs(Mathf.Sin(Mathf.Abs(TrackToPoint(point) - A0)) * Mathf.Deg2Rad) * h);
        }
        void ATCCall()

        {
            RouteScriptableObject activePoints = GameManager.Instance.ActiveRoute;

            point = aTCs[Level].ATCInstrucitonItems[i].point;
            mode = aTCs[Level].ATCInstrucitonItems[i].mode;
            Altitude = aTCs[Level].ATCInstrucitonItems[i].Altitude;
            VS = aTCs[Level].ATCInstrucitonItems[i].VS;
            VS_nx = aTCs[Level].ATCInstrucitonItems[i].VS_nx;
            Speed = aTCs[Level].ATCInstrucitonItems[i].Speed;
            Speed_nx = aTCs[Level].ATCInstrucitonItems[i].Speed_nx;

            if (NewPoint)
            {
                Atc1.text = mode == 1 ? "Proceed direct to  " + activePoints.Points[point].Name :
                            mode == 2 ? "Turn " + turnDirection(TrackToPoint(point)) + "Heading " + TrackToPoint(point) : "";

                string s = VS < 0 ? ", ROD " + (-VS) + " fpm" + nxTostring(VS_nx) : "";
                Atc2.text = Altitude > 0 ? "Descent altitude " + Altitude + " feet" + s : "";


                Atc3.text = Speed > 0 ? "Speed " + Speed + " knots " + nxTostring(Speed_nx) : Speed == 0 ? Atc3.text : "";

                A0 = TrackToPoint(point);
                Atc1.color = Color.green;
                NewPoint = false;
            }
            else  // Not New
            {
                if (DistanceFromRoute() > 20) //Warning
                {
                    Atc1.text = mode == 2 ? "Turn " + turnDirection(TrackToPoint(point)) + "Heading " + TrackToPoint(point) : "";

                    if (DistanceFromRoute() < 30) PrvPos = GameManager.Instance.Aircraft.PositionFreeOrOnCurvedPath;

                    if (Atc1.color == Color.red) Atc1.color = Color.white; else Atc1.color = Color.red;

                    float sinus = Mathf.Sin((TrackToPoint(point) - A0) * Mathf.Deg2Rad) * h; // h calculated in TrackToPoint
                    float delta = Mathf.DeltaAngle(Calculator.RHeading, TrackToPoint(point));//Change Calculator.RHeading to Cheading

                    if ((sinus > 0 && delta < 2) || (sinus < 0 && delta > -2)) Atc1.color = Color.white; //if hdg is correcting

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

            ATCCall();

            OncekiPos = GameManager.Instance.Aircraft.PositionFreeOrOnCurvedPath;
            OncekiAlt = (int)Calculator.CAltitude;
            myAC.transform.localPosition = GameManager.Instance.Aircraft.PositionFreeOrOnCurvedPath; //move AC on EditMap

            if (PositionVirtualNode.PassedNodeIndex != prvWptIdx)
            {
                i += 1;
                NewPoint = true;

                ATCAltitude = VS;
                myAC.GetComponent<UnityEngine.UI.Text>().text = "#";
                prvWptIdx = PositionVirtualNode.PassedNodeIndex;
            }


            //(int) Altitude;
            //((DistanceToRoute < 40))  //Not to continue if out of game borders
            //else
            //{
            //    Atc1.color = Color.red;
            //    myAC.transform.localPosition = PrvPos;
            // }
        }
    }
    private IEnumerator MoveAC(int ACnr)
    { //Moves  other ACs based on the Route and Alt(Altitude) arrays.

        float dx, dy, h;
        float x, y, EscapeX = 0, EscapeY = 0;
        int i = 1;
        float Speed, SpeedCo;
        string s;
        Vector2 PtPos;
        int Point, AltitudeR;
        float AltitudeC = otherACLevel[Level].otherACnr[ACnr].ACItems[0].Altitude;
        Vector2 finalPosition = PositionOfPoint(otherACLevel[Level].otherACnr[ACnr].ACItems[0].Point);  //intial pos and alt
        void CollisionCheck()
        {
            int j;
            float myACAlt, ACAlt, Angle;
            Vector3 myACpos, ACpos;
            float D = Vector2.Distance(AC.transform.localPosition, myAC.transform.localPosition);
            myACpos = myAC.transform.localPosition;
            myACAlt = (int)Calculator.CAltitude;
            ACAlt = AltitudeC;

            for (j = 1; j < 10; j++)
            {
                myACpos += (myAC.transform.localPosition - OncekiPos);
                ACpos = new Vector3(x + dx / h * SpeedCo * j, y + dy / h * SpeedCo * j, 0);
                myACAlt -= (OncekiAlt - (int)Calculator.CAltitude);
                if (myACAlt < Calculator.RAltitude) myACAlt = (int)Calculator.RAltitude;
                // ACAlt  -= (Altitude0 - Altitude) / (h / 10); Change
                if ((Vector3.Distance(myACpos, ACpos) < 50) && Mathf.Abs(myACAlt - ACAlt) < 800)
                {
                    Angle = (Vector2.Angle(AC.transform.localPosition, myAC.transform.localPosition) + 1.57f);
                    EscapeX = Mathf.Cos(Angle) * SpeedCo;
                    EscapeY = Mathf.Sin(Angle) * SpeedCo;

                    AC.GetComponent<UnityEngine.UI.Text>().color = Color.cyan;
                    break;
                }
            }

        }

        Vector2 PositionOfPoint(int Pt)
        {
            Vector2 V;
            V = Pt < 50 ? (Vector2)GameManager.Instance.PathLines.ComputedLines[Pt].EndPosition : //TempPtsPos[Pt] :
                           new Vector2(virtualPoints[Level].VirtualPointsItems[Pt - 50].x,  // VirtualPtsPos[Pt-50]; // ; TOTO review
                                       virtualPoints[Level].VirtualPointsItems[Pt - 50].y);
            return V;
        }



        while (i < otherACLevel[Level].otherACnr[ACnr].ACItems.Length)
        {

            yield return new WaitForSeconds(GameSpeed);


            Point = otherACLevel[Level].otherACnr[ACnr].ACItems[i].Point;
            AltitudeR = otherACLevel[Level].otherACnr[ACnr].ACItems[i].Altitude;
            Speed = otherACLevel[Level].otherACnr[ACnr].ACItems[i].Speed;

            PtPos = PositionOfPoint(Point);

            var _aircraftKey = "AC (" + (ACnr + 1) + ")";
            if (!ACPositions.ContainsKey(_aircraftKey))
            {
                ACPositions.Add(_aircraftKey, Vector2.zero);
                ACTexts.Add(_aircraftKey, "");
            }


            AC = GameObject.Find(_aircraftKey);
            AC.transform.localPosition = finalPosition;
            SpeedCo = Speed / 360;

            x = finalPosition.x;
            y = finalPosition.y;


            dx = PtPos.x - x;
            dy = PtPos.y - y;
            h = Mathf.Sqrt(dx * dx + dy * dy);

            if (EscapeX == 0)
            {
                AltitudeC -= ((AltitudeC - AltitudeR)) / h * SpeedCo / 10;
                finalPosition = new Vector2(x + dx / h * SpeedCo / 10, y + dy / h * SpeedCo / 10); //Advance
            }
            else
            {
                AltitudeC += 100;
                finalPosition = new Vector2(x + EscapeX, y + EscapeY);
            }

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
            if ((h <= 1))
            {
                i += 1;
                AltitudeC = AltitudeR;
            }
            if (EscapeX == 0) CollisionCheck();
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

