
using System.Collections;
using System.Collections.Generic;
using Gamelogic.Extensions;
using UnityEngine;
using UnityEngine.UI;

[System.Serializable]
public class Move : Singleton<Move>
{
    [System.Serializable]
    public struct otherACsStruct
    {
        public OtherACScriptableObject[] otherACnr;
    }
 
    public otherACsStruct[] otherACLevel ;
    public ATCInstructionsScriptableObject[] aTCs;
    int Level=0;
    public GameObject pt, AC,myAC;
    public Slider GameSpeed;
    int[] Route = new int[10];
    int[] SpeedArray = new int[10];
    int[] Alt = new int[10];
    public Text Atc1,Atc2,Atc3;
    public string ATtc1; // maybe it's possible to use like this
    public int R;
    public Dropdown DCT_Drop;
    bool NewPoint = true;
    Vector3 PrvPos,OncekiPos;
    Vector2[] VirtualPts = new Vector2[10];
    Vector2[] TempPts = new Vector2[30];
    long ATCAltitude, OncekiAlt;
    int ATCVS,ATCSpeed;
    bool DescentChecked, SpeedChecked;
    int modD = 0, modS=0;
     

    public Dictionary<string, Vector2> ACPositions =new Dictionary<string, Vector2>();
    
    
    
    void Start()

    {
        // var somePoint = GameManager.Instance.ActiveSet.Points[3].Clone();

        // Altitude computed: GameManager.Instance.ActiveSet.Points[4].Altitude.ComputedValue

        Vector2 Pos = new Vector2(0,0);

        int j;
        GameObject pt,A;
            for (j = 1; j < 17; j++)
            {
            //      Pos = GameManager.Instance.PathLines.ComputedLines[j].EndPosition;//  TODO to review
            //      pt = GameObject.Find("pt (" + j + ")");
            //      pt.transform.localPosition = Pos;
                   pt = GameObject.Find("pt (" + j + ")");
                   TempPts[j] = pt.transform.localPosition;
        }
        for (j = 1; j < 4; j++)
        {
            pt = GameObject.Find("pt (" + (j+50) + ")");
            VirtualPts[j]=pt.transform.localPosition ;
        }
        for (j = 0; j < 4; j++)
        {
            A = GameObject.Find("AC (" + (j) + ")");
        }

        myAC = GameObject.Find("AC (0)"); //my AC

        Atc1.text = "";
        Atc2.text = "";
        Atc3.text = "";

        StartCoroutine(MoveAC(0));
        StartCoroutine(MoveAC(1));
        StartCoroutine(MoveAC(2));

        StartCoroutine(MoveMyAC());// Our Aircraft
    }
    public void DCT_Drop_Changed()
    {
        R = DCT_Drop.value;
    }
    private IEnumerator MoveMyAC()
    {//Moves our Aircraft
        float dx, dy, h = 0, A = 0, A0 = 0;
        float x1 = 0, y1 = 0, x2 = 0, y2 = 0;
        int nm = 0, i = 0,SpeedCo;
        string s1 = "", s2 = "", s3 = "", ss = "";

        int point = aTCs[Level].ATCInstrucitonItems[i].point;
        int mode = aTCs[Level].ATCInstrucitonItems[i].mode;
        long Altitude = aTCs[Level].ATCInstrucitonItems[i].Altitude;
        int VS = aTCs[Level].ATCInstrucitonItems[i].VS;
        int VS_nx = aTCs[Level].ATCInstrucitonItems[i].VS_nx;
        int Speed = aTCs[Level].ATCInstrucitonItems[i].Speed;
        int Speed_nx = aTCs[Level].ATCInstrucitonItems[i].Speed_nx;

        void Calculate_point(int K)
        {
            Vector2 PtPos;
            // computedLines start from 1. (0 is an added empty line)
            if (K < 50) PtPos = GameManager.Instance.PathLines.ComputedLines[K+1].EndPosition;
            else PtPos = VirtualPts[K-50];

            x2 = PtPos.x;
            y2 = PtPos.y;
            dx = x2 - x1;
            dy = y2 - y1;
            h = Mathf.Sqrt(dx * dx + dy * dy);
            A = (Mathf.Atan2(dx, dy) * Mathf.Rad2Deg);
            if (A < 0) A += 360;

        }
        void Calculate_AC()
        {

            x1 = myAC.transform.localPosition.x;
            y1 = myAC.transform.localPosition.y;
        }
        void Atc1TurnHdg(int newHdg)
        {
            {
                ss = Mathf.DeltaAngle(Calculator.RHeading, newHdg) >= 0 ? "Right " : "Left ";
                s1 = "Turn " + ss + "Heading " + (int)A;
            }

        }
        void ATCCall()    // *************************************************ATC  Window***********************************************

        {



           
            if (NewPoint)
            {
                if (mode == 1) 
                    s1 = "Continue direct Waypoint  " + mode;
                if (mode == 2) Atc1TurnHdg((int) A);
                NewPoint = false;

                if (Altitude > 0) 
                    s2 = "Descent altitude " + Altitude + " feet"; ss = "";
                if (VS_nx == 1) ss = " or greater "; 
                if (VS_nx == 2) ss = " or less ";
                if (VS < 0) 
                     s2 += ", ROD " + (-VS) + " fpm" + ss; ss = "";
                if (Speed_nx == 1) ss = " or greater "; 
                if (Speed_nx == 2) ss = " or less";
                if (Speed > 0) 
                     s3 = "Speed " + Speed + " knots " + ss;
                A0 = A;
                Atc1.color = Color.green;
            }
            else  // Not New
            {
                if ((Mathf.Abs(Mathf.Sin(Mathf.Abs((A - A0)) * Mathf.Deg2Rad) * h) > 20)) //Warning
                  {
                    if (mode == 2) Atc1TurnHdg((int)A);

                    if ((Mathf.Abs(Mathf.Sin(Mathf.Abs((A - A0)) * Mathf.Deg2Rad) * h) < 30)) //Distance from Route>20
                      {
                         PrvPos = myAC.transform.position;
                      }
                    if (Atc1.color == Color.red) Atc1.color = Color.white; else Atc1.color = Color.red;
                    if ((Mathf.Sin((A - A0) * Mathf.Deg2Rad) * h) > 0) //if hdg is correcting
                    {
                        if (Mathf.DeltaAngle(Calculator.RHeading , A)<2) Atc1.color = Color.white;
                    }
                    if ((Mathf.Sin((A - A0) * Mathf.Deg2Rad) * h) < 0) //Change Calculator.RHeading to Cheading
                    {
                        if (Mathf.DeltaAngle(Calculator.RHeading, A) >-2) Atc1.color = Color.white;
                    }
                } 
                else Atc1.color = Color.white; 

            }
            Atc1.text = s1;
            
            if ((Altitude > 0) && (Altitude != ATCAltitude) ) //Descent clr changed
            {
                Atc2.color = Color.green;
                Atc2.text = s2;
                DescentChecked = false;
                ATCAltitude = Altitude;
                ATCVS = VS;
                modD = VS== 0 ? -1 : VS_nx;
                CancelInvoke("Descent_Check");
                InvokeRepeating("Descent_Check", 10f, 1f);
            }
            if (DescentChecked) Atc2.color = Color.white;

            if ((Speed > 0) && (Speed != ATCSpeed)) //Speed clr changed
            {
                Atc3.color = Color.green;
                Atc3.text = s3;
                SpeedChecked = false;
                ATCSpeed = Speed;
                modS = Speed_nx;
                CancelInvoke("Speed_Check");
                InvokeRepeating("Speed_Check", 10f, 1f);
            }
            if (SpeedChecked) Atc3.color = Color.white;

            if (Speed == -1)
            {
                Atc3.text = "";
                CancelInvoke("Speed_Check");
            }


        }

        while ((mode != -1))
        {
            yield return new WaitForSeconds(GameSpeed.value);
            Calculate_AC();
            Calculate_point(point);
            ATCCall();
            SpeedCo = (int)Calculator.CSpeed / 30;
            //********** *******************************Move AirCraft based on Instruction Set***********************************************

            if (1==1)//((Mathf.Abs(Mathf.Sin(Mathf.Abs(A - A0) * Mathf.Deg2Rad) * h) < 40))  //Not to continue if out of game borders
            {
                nm += 1;
                OncekiPos = myAC.transform.localPosition;
                OncekiAlt = (int)Calculator.CAltitude;
                if (!Calculator.isHDG)                                                   // LNAV
                {
                   Calculate_point(Route[R]);
                   myAC.transform.localPosition = GameManager.Instance.Aircraft.Position;
                    if (h <= 40) R += 1;
                }
                else                                                                    //  HDG
                {
                    Calculate_point(point);

                    dx = Mathf.Sin((Calculator.RHeading) * Mathf.Deg2Rad);
                    dy = Mathf.Cos((Calculator.RHeading) * Mathf.Deg2Rad);

                    myAC.transform.localPosition = new Vector2(x1 + dx * SpeedCo, y1 + dy * SpeedCo);
                }
            }
            else
            {
                Atc1.color = Color.red;
                myAC.transform.localPosition = PrvPos;
            }
            Calculate_AC();
            Calculate_point(point);

            if (h <= 40)
            {
                i += 1;
                nm = 0;
                NewPoint = true;

                ATCAltitude = VS;
                myAC.GetComponent<UnityEngine.UI.Text>().text = "" + i; //(int) Altitude;
            }
        }
    }
    private IEnumerator MoveAC( int ACnr)
    { //Moves  other ACs based on the Route and Alt(Altitude) arrays.

        float dx, dy, h;
        float x, y,EscapeX=0,EscapeY=0;
        int i = 0, SpeedCo;
        string s;
        float Altitude = otherACLevel[Level].otherACnr[ACnr].ACItems[0].Altitude;
        Vector2 PtPos;
       void CollisionCheck()
        {
            int j;
            float myACAlt, ACAlt,Angle;
            Vector3 myACpos, ACpos;
            float D = Vector2.Distance(AC.transform.localPosition, myAC.transform.localPosition);
            myACpos = myAC.transform.localPosition;
            myACAlt = (int)Calculator.CAltitude;
            ACAlt = Altitude;
            
                for (j = 1; j < 10; j++)
                {
                    myACpos += (myAC.transform.localPosition - OncekiPos);
                    ACpos = new Vector3(x + dx / h * SpeedCo*j, y + dy / h *SpeedCo*j,0);
                     myACAlt -= (OncekiAlt- (int)Calculator.CAltitude);
                if (myACAlt < Calculator.RAltitude) myACAlt = (int)Calculator.RAltitude;
                     ACAlt  -= (Altitude - otherACLevel[Level].otherACnr[ACnr].ACItems[i+1].Altitude) / (h / 10);
                       if ((Vector3.Distance(myACpos, ACpos) < 50) && Mathf.Abs(myACAlt- ACAlt)<800)
                    {
                    Angle = (Vector2.Angle(AC.transform.localPosition, myAC.transform.localPosition)+1.57f);
                    EscapeX = Mathf.Cos( Angle)*SpeedCo;
                    EscapeY = Mathf.Sin(Angle)*SpeedCo;

                       AC.GetComponent<UnityEngine.UI.Text>().color = Color.cyan;
                        break;
                    } 
                }
           
        }
         while ((otherACLevel[Level].otherACnr[ACnr].ACItems[i].Point != -1))
        {

            yield return new WaitForSeconds(GameSpeed.value);

            if   (otherACLevel[Level].otherACnr[ACnr].ACItems[i].Point < 50) 
                 PtPos = TempPts[otherACLevel[Level].otherACnr[ACnr].ACItems[i].Point];//GameManager.Instance.PathLines.ComputedLines[otherACs[ACnr].ACItems[i].Point].EndPosition;// ; TOTO review
            else PtPos = VirtualPts[otherACLevel[Level].otherACnr[ACnr].ACItems[i].Point - 50];
            
            var _aircraftKey = "AC (" + (ACnr+1) + ")";
            
            if (!ACPositions.ContainsKey(_aircraftKey))
            {
                ACPositions.Add(_aircraftKey, Vector2.zero);
            }

            Vector2 finalPosition;
            
            AC = GameObject.Find(_aircraftKey);
            
            SpeedCo = otherACLevel[Level].otherACnr[ACnr].ACItems[i].Speed / 30; 
            
            x = AC.transform.localPosition.x; 
            y = AC.transform.localPosition.y;
            
            
            dx = PtPos.x - x;
            dy =PtPos.y - y;
            h = Mathf.Sqrt(dx * dx + dy * dy);
            
            if (EscapeX == 0)
            {
                Altitude -= ((Altitude - otherACLevel[Level].otherACnr[ACnr].ACItems[i+1].Altitude)) / (h / 10);
                finalPosition = new Vector2(x + dx / h * SpeedCo/10, y + dy / h * SpeedCo/10); //Advance
            }
            else
            {
                Altitude += 100;
                finalPosition = new Vector2(x + EscapeX, y + EscapeY);
            }

            AC.transform.localPosition = finalPosition;
            ACPositions[_aircraftKey] = finalPosition;
           
            if (Altitude - Calculator.CAltitude < 0) s = ""; else s = "+";
            if (Mathf.Abs((Altitude - (int)Calculator.CAltitude)) < 6000)
               {
                AC.GetComponent<UnityEngine.UI.Text>().text = s + (int)((Altitude - Calculator.CAltitude) / 100);
               }
            if ((h <= 10))
            {
                i += 1;
                Altitude = otherACLevel[Level].otherACnr[ACnr].ACItems[i].Altitude;
            }
            if (EscapeX == 0) CollisionCheck();
        }
       AC.GetComponent<UnityEngine.UI.Text>().text = "";
 

    }
    void Descent_Check()
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
            CancelInvoke("Descent_Check");
            Atc2.text = "";
        }
        DescentChecked = true;
    }
    void Speed_Check()
    {

            if
                   (((modS == 0) && ((Calculator.CSpeed > ATCSpeed + 10) || (Calculator.CSpeed < ATCSpeed - 10))) ||

                   ((modS == 1) && (Calculator.CSpeed < ATCSpeed - 10)) ||

                   ((modS == 2) && (Calculator.CSpeed > ATCSpeed + 10))) Atc3.color = Color.red;
        
         SpeedChecked = true;
    }

}