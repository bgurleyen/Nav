using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Gamelogic.Extensions;

public class Calculator : MonoBehaviour
{

    public  WindTableScriptableObject[] windTables;
    string result;
    public Text txtRSpeed, txtRAltitude, txtRVS, txtRHeading;
    public Text txtCSpeed, txtCAltitude, txtCVS;
    public Text txtRSpeed_overTape, txtRAltitude_overTape;
    public Text txtDTG;//DTG: Distance to go
    public Text txtMeter;//Meter Display
    public Text Qnh, txtMach;


    public Text txtN1, txtFF, txtTotalFuel;// N1( / 100) , Fuel Flow ( X 100) , Pitch attitude ( / 100)
    public Text VDI_Text;

    public Image VDI_Index, banana;

    int RVS; //R : Required(Selected)

    public static int RSpeed = 220, RHeading, RAltitude, CVS;
    public static double CSpeed = 250, CAltitude = 40000;
    public int CHeading,Track;
    double CMach,RMach, VNAV_VS;
    public static double  TAS, GS;

    public Toggle VNAV_Toggle, LNAV_Toggle, LC_Toggle, HS_Toggle, AH_Toggle, VS_Toggle;
    public Toggle LGToggle, SBToggle, co;//Landing Gear ,Speed Brake;
    public static bool LGDown = false;
    public Button FUP_Button, FDown_Button;
    int Flap_Idx, increasedSpeed;
    double DTG;
    public  Text windTxt;
    public static string CWind;
    public Text FMA1, FMA2, FMA3;
    public Image FlapNeedle,windArrow;
    public GameObject Progres;
    int N1, FF, dispN1 = 77;
    double dispFF = 270;
    public static bool isHDG;
    public static double totalFuel = 1000; // 10 tons *100
    //Speed:(NM per Hour = Knots)--> Show in PFD , map will move in this speed
    //Altitude:(Feet)--> Show in PFD ,no other effect
    //VS:(Feet per minute)--> Show in PFD ,no other effect
    //N1:(Engine Rotation percentage, divide by 100 )--> Show in N1 Guage ,no other effect
    //FF:(libres per hour per engine  X10, Sample: 230 means 2300 lb/hr per engine, total 4600 lb/hr )--> Show in FF Guage ,decrease the total fuel by this number
    //Pitch:(degrees, divide by 100)--> Show in PFD ,no other effect
    //LG:(on-off)--> Illustrate Landing Gear movement - Changes approach idle
    //SB:(on-off)--> Illustrate Speed Brake movement - no other effect
    //Flaps: (1-5-10-15-25-30-40)
    //


    //**************************************************************
    //**************************************************************

    static double[,,] M = new double[9, 4, 4] { //speed,pitch,n1,ff
        { {247, 300,8910,226 }, { -2500, 0, 4800,95 } , { 216, 500,8540,200 }, { -1900, 200, 4620,95 } },
        { { 268, 250,8740,266 }, { -3000, -100, 4650,95 } , { 233, 300,8100,220 }, { -2100, 100, 4450,95 } },
        { { 290, 200,8670,316 }, { -3400, -250, 4610,95 } , { 254, 250,8080,256 }, { -2200, -100, 4380,95 } },
        { { 280, 200,8010,282 }, { -2000, -100, 3990,95 } , { 220, 400,7520,221 }, { -1300, 200, 3670,95 } },
        { { 280, 200,7820,282 }, { -1800, -100, 3820,95 } , { 220, 400,7180,224 }, { -1200, 200, 3500,95 } },
        { { 280, 200,7510,279 }, { -1700, -100, 3670,95 } , { 220, 400,6710,221 }, { -1100, 200, 3370,95 } },
        { { 240, 300,6620,236 }, { -1300, 100, 3350,95 } , { 220, 400,6440,222 }, {-1100, 200, 3250,95 } },
        { { 240, 400,6420,245 }, { -1300, 0, 3240,95 } , { 220, 500,6080,221 }, { -1200, 100, 3150,95 } },
        { { 240, 260,5890,226 }, { -1300, 0, 3140,95 } , { 220, 360,3740,220 }, { -1200, 100, 3060,95 } },
    };// Main Matrix
    double[,,,] Mf = new double[9, 5, 4, 4] {{
        //noflaps
        { { 280, 200,7820,282 }, { -1800, -100, 3820,95 } , { 220, 400,7180,224 }, { -1200, 200, 3500,95 } },
        { { 280, 200,7510,279 }, { -1700, -100, 3670,95 } , { 220, 400,6710,221 }, { -1100, 200, 3370,95 } },
        { { 240, 300,6620,236 }, { -1300, 100, 3350,95 } , { 220, 400,6440,222 }, {-1100, 200, 3250,95 } },
        { { 240, 400,6420,245 }, { -1300, 0, 3240,95 } , { 220, 500,6080,221 }, { -1200, 100, 3150,95 } },
        { { 240, 260,5890,226 }, { -1300, 0, 3140,95 } , { 220, 360,3740,220 }, { -1200, 100, 3060,95 } }},
        //f1
        {
        { { 240, 200,7110,251 }, { -1300, 100, 3670,95 } , { 180, 600,6600,236 }, { -1100, 400, 3370,95 } },
        { { 240, 200,7110,251 }, { -1300, 100, 3670,95 } , { 180, 600,6600,236 }, { -1100, 400, 3370,95 } },
        { { 240, 200,6700,247 }, { -1300, 100, 3350,95 } , { 180, 500,6220,227 }, {-1100, 400, 3250,95 } },
        { { 240, 200,6420,244 }, { -1300, 0, 3240,95 } , { 180, 600,5990,231 }, { -1100, 400, 3150,95 } },
        { { 240, 200,6150,244 }, { -1300, 0, 3140,95 } , { 180, 400,5640,236 }, { -1100, 400, 3060,95 } }},
        //f2
        {
        { { 240, 100,7250,268 }, { -1500, 0, 3670,95 } , { 180, 500,6580,234 }, { -1200, 300, 3370,95 } },
        { { 240, 100,7250,268 }, { -1500, 0, 3670,95 } , { 180, 500,6580,234 }, { -1200, 300, 3370,95 } },
        { { 240, 100,6800,266 }, { -1500, 0, 3350,95 } , { 180, 400,6340,242 }, {-1200, 300, 3250,95 } },
        { { 240, 100,6540,268 }, { -1500, 0, 3240,95 } , { 180, 400,5970,229 }, { -1200,300, 3150,95 } },
        { { 240, 100,6270,265 }, { -1500, 0, 3140,95 } , { 180, 150,5630,233 }, { -1200, 300, 3060,95 } } },
        //f5
        {
        { { 240, 0,7610,319 }, { -1700, 0, 3670,95 } , { 180, 400,6850,267 }, { -1300, 300, 3370,95 } },
        { { 240, 0,7610,319 }, { -1700, 0, 3670,95 } , { 180, 400,6850,267 }, { -1300, 300, 3370,95 } },
        { { 240, 0,7140,311 }, { -1700, -100, 3350,95 } , { 180, 400,6480,264 }, {-1300, 250, 3250,95 }},
        { { 240, 0,6760,310 }, { -1700, -100, 3240,95 } , { 180, 400,6190,260 }, { -1300,250, 3150,95 }},
        { { 240, 0,6600,314 }, { -1700, -100, 3140,95 } , { 180, 400,5920,255 }, { -1300, 250, 3060,95 } } },
        //f10
        {
        { { 200, 200,7500,329 }, { -1700, 0, 3670,95 } , { 160, 500,7210,315 }, { -1400, 400, 3370,95 } },
        { { 200, 200,7500,329 }, { -1700, 0, 3670,95 } , { 160, 500,7210,315 }, { -1400, 400, 3370,95 } },
        { { 200, 200,7010,327 }, { -1700, 0, 3350,95 } , { 160, 500,6480,312 }, {-1400, 250, 3250,95 }},
        { { 200, 200,6650,326 }, { -1700, 0, 3240,95 } , { 160, 500,6190,334 }, { -1400,250, 3150,95 }},
        { { 200, 200,6510,334 }, { -1700, 0, 3140,95 } , { 160, 600,5920,318 }, { -1400, 250, 3060,95 }}},
        //f15
        {
        { { 190, 200,7780,374 }, { -1200, 0, 3670,95 } , { 150, 600,7700,351 }, { -900, 500, 3370,95 } },
        { { 190, 200,7780,374 }, { -1200, 0, 3670,95 } , { 150, 600,7700,351 }, { -900, 500, 3370,95 } },
        { { 190, 200,7260,362 }, { -1200, 0, 3350,95 } , { 150, 600,7000,348 }, {-900, 500, 3250,95 }},
        { { 190, 200,6850,373 }, { -1200, 0, 3240,95 } , { 150, 700,6600,355 }, { -900,500, 3150,95 }},
        { { 190, 200,6600,358 }, { -1200, 0, 3140,95 } , { 150, 700,6380,356 }, { -900, 500, 3060,95 }}},
        //f25
        {
        { { 180, 100,8070,446 }, { -1500, -100, 3670,95 } , { 140, 600,7870,413 }, { -1200, 500, 3370,95 } },
        { { 180, 100,8070,446 }, { -1500, -100, 3670,95 } , { 140, 600,7870,413 }, { -1200, 500, 3370,95 } },
        { { 180, 100,7810,446 }, { -1500, -100, 3350,95 } , { 140, 600,7580,412 }, {-1200, 500, 3250,95 }},
        { { 180, 100,7460,438 }, { -1500, -100, 3240,95 } , { 140, 600,6790,403 }, { -1200,400, 3150,95 }},
        { { 180, 100,6950,441 }, { -1500, -100, 3140,95 } , { 140, 600,6580,405 }, { -1200, 400, 3060,95 }}},
        //f30
        {
        { { 170, -100,8150,469 }, { -1700, -200, 3670,95 } , { 130, 600,7940,447 }, { -1100, 500, 3370,95 } },
        { { 170, -100,8150,469 }, { -1700, -200, 3670,95 } , { 130, 600,7940,447 }, { -1100, 500, 3370,95 } },
        { { 170, -100,7950,476 }, { -1700, -200, 3350,95 } , { 130, 600,7800,455 }, {-1100, 400, 3250,95 }},
        { { 170, -100,7440,450 }, { -1700, -200, 3240,95 } , { 130, 600,6880,425 }, { -1100,400, 3150,95 }},
        { { 170, -100,6950,453 }, { -1700, -200, 3140,95 } , { 130, 600,6760,455 }, { -1100, 400, 3060,95 }}},
        //f40
        {
        { { 160, -0,8410,520 }, { -1800, -200, 3670,95 } , { 130, 500,8110,497 }, { -1300, 300, 3370,95 } },
        { { 160, -0,8410,520 }, { -1800, -200, 3670,95 } , { 130, 500,8110,497 }, { -1300, 300, 3370,95 } },
        { { 160, -0,8050,509 }, { -1800, -200, 3350,95 } , { 130, 500,7920,497 }, {-1300, 300, 3250,95 }},
        { { 160, -0,7940,530 }, { -1800, -200, 3240,95 } , { 130, 500,7510,496 }, { -1300,300, 3150,95 }},
        { { 160, -100,7300,508 }, { -1800, -200, 3140,95 } , { 130, 500,6920,488 }, { -1300, 300, 3060,95 }}}
    };
    private void Start()
    {

        RAltitude = (int)CAltitude;
        RSpeed = (int)CSpeed;
        RVS = CVS;
        txtRAltitude.text = "" + RAltitude; txtRAltitude_overTape.text = txtRAltitude.text;
        txtCAltitude.text = "" + (int)CAltitude; txtMeter.text = "" + (int)(CAltitude / 3.28084) + "M";
        txtCSpeed.text = "" + CSpeed;
        txtRSpeed.text = "" + RSpeed; txtRSpeed_overTape.text = txtRSpeed.text;

        txtCVS.text = "";
        DTG = 173.1;
        Invoke("VS_Equalize", 1f);
        Invoke("Speed_Equalize", 0.1f);
        InvokeRepeating("InterpolateLvlChg", 0, 1f);
        InvokeRepeating("InterpolateVS", 0, 1f);
        InvokeRepeating("MatchAltitudes", 0, 0.1f);
        InvokeRepeating("SetFMA", 0, 1f);
        InvokeRepeating("FuelandMach", 0, 1f);
        InvokeRepeating("SetN1FF", 0, 0.1f);
        InvokeRepeating("ToggleEnable", 0, 0.1f);
        InvokeRepeating("DrawVDI", 0, 1f);
        InvokeRepeating("DisplayWindElements", 0, 1f);
        Flap_Idx = 0;
        SetFlaps();
    }

    public static Calculator Instance;

    void Awake()
    {
        Instance = this;
    }
    public void DrawVDI() 
    {
        double DeltaAlt, Alt1, Alt0, d, D;
        int posY;

        RouteScriptableObject activePoints = GameManager.Instance.ActiveRoute;
        RouteScriptableObject modPoints = GameManager.Instance.ModRoute;

        bool isMod = GameManager.Instance.IsMod;
        RouteScriptableObject _route = isMod ? modPoints : activePoints;


        var node0 = _route.Points[PositionVirtualNode.PassedNodeIndex];
        var node1 = _route.Points[PositionVirtualNode.PassedNodeIndex + 1];

        //Debug.Log(node.DisplayAltitude.ToString());

        //DeltaAlt =  activeCurrentPosition.ComputedDistanceLeft * 318.43 + double.Parse(node.DisplayAltitude);
        Alt0 = double.Parse(node0.DisplayAltitude);
        Alt1 = double.Parse(node1.DisplayAltitude);
        d = GameManager.Instance.Aircraft.ComputedDistanceLeft;
        D = (d + PositionVirtualNode.ComputedDistancePassed); // Daniel: this will behave bad while free flight

        DeltaAlt = CAltitude - (Alt1 + (d * (Alt0 - Alt1)) / D);
        VDI_Text.text = (((DeltaAlt) > 50) || ((DeltaAlt) < -50)) ? "" + (int)DeltaAlt : "";
        if (VNAV_Toggle.isOn)
        {
            VNAV_VS = -((CAltitude - Alt1) * CSpeed) / (60 * d) - DeltaAlt * 2;
            RVS = (DeltaAlt < -50) ? -100 : (int)VNAV_VS;
        }

        posY = -(int)(DeltaAlt / 5);
        if (posY > 100) posY = 100;
        if (posY < -100) posY = -100;

        VDI_Index.transform.localPosition = new Vector2(37, posY);
        if (DeltaAlt < 0) VDI_Text.transform.localPosition = new Vector2(-1, -120);
        else VDI_Text.transform.localPosition = new Vector2(-1, 120);

        int bananaPos = (int)((CAltitude - RAltitude) / CVS * CSpeed / 60 * 6);
        banana.transform.localPosition = new Vector3(-2438, -220 - bananaPos, 213);
    }
    public void SetFMA()
    {
        if (HS_Toggle.isOn) isHDG = true; else isHDG = false; //For Move.cs Delete later
        if (HS_Toggle.isOn) FMA2.text = "HDG"; else FMA2.text = "LNAV";
        if (LC_Toggle.isOn)
        {
            FMA1.text = "IDLE";
            FMA3.text = "MCP SPD";
        }
        if (AH_Toggle.isOn)
        {
            FMA1.text = "MCP SPD";
            FMA3.text = "ALT";
        }
        if (VS_Toggle.isOn)
        {
            FMA1.text = "MCP SPD";
            FMA3.text = "VS";
        }


    }
    public void VS_Equalize()
    {

        if ((CVS != 0) || (RVS != 0))
        {

            if (RAltitude != CAltitude)
            {
                if (RVS < CVS) CVS -= 10;
                if (CVS < RVS) CVS += 10;
                if (Mathf.Abs(RVS - CVS) < 10) CVS = RVS;
                if ((Mathf.Abs(RVS) > 1000) && (Mathf.Abs(RAltitude - (int)CAltitude) < Mathf.Abs(CVS / 2f) - 600))
                {
                    RVS = RVS / Mathf.Abs(RVS) * 1000;
                }

                CAltitude += (float)CVS / 600;
                if (Mathf.Abs(RAltitude - (int)CAltitude) < 10)
                {
                    CAltitude = RAltitude;
                    txtRAltitude.text = "" + RAltitude; txtRAltitude_overTape.text = txtRAltitude.text;
                    CVS = 0;
                    RVS = 0;
                    AH_Toggle.isOn = true;
                }
                txtCAltitude.text = "" + (int)(CAltitude / 10) * 10;
                txtMeter.text = "" + (int)(CAltitude / 3.28084 / 10) * 10 + "M";
            }
        }
        if (Mathf.Abs(CVS) > 300) txtCVS.text = "" + CVS / 100 * 100; else txtCVS.text = "";
        if (CVS > 0) txtCVS.transform.localPosition = new Vector2(200, 90);
        else txtCVS.transform.localPosition = new Vector2(200, -100);
        Invoke("VS_Equalize", (float)(12.75 - CSpeed * 0.025) / 100);

    }
    private void MatchAltitudes()
    {
        NewBehaviourScript Script2 = FindObjectOfType<NewBehaviourScript>();
        Script2.AltUpdate((int)CAltitude);
        Script2.CheckAltitudeIndicator(RAltitude, (int)CAltitude);
        if (CAltitude >= 10000) Qnh.text = "STD";
        else Qnh.text = "1013";
    }
    private void Speed_Equalize()
    {
        int RRSpeed;
        float speedbandspeed = 1f;
        if (RSpeed > increasedSpeed) RRSpeed = RSpeed; else RRSpeed = increasedSpeed;
        float[] M210 = new float[8] { 1.45f, 0.65f, 1.05f, 0.75f, 0.9f, 1f, 0.75f, 1.2f };//Dec - Acc Times
        float[] M270 = new float[8] { 1.25f, 0.6f, 0.85f, 0.8f, 0.55f, 1.95f, 0.5f, 6f }; //Clean,SB,Lg,Both

        NewBehaviourScript Script2 = FindObjectOfType<NewBehaviourScript>();

        if (RRSpeed != CSpeed)
        {
            if (RRSpeed < CSpeed) CSpeed -= speedbandspeed;
            else CSpeed += speedbandspeed;

            txtCSpeed.text = "" + (int)CSpeed;

        }
        DTG -= (double)CSpeed / 3600;
        txtDTG.text = "" + (int)DTG;
        Script2.SpeedTapeUpdate();
        Script2.SpeedIndexUpdate_Click();
        int x = 0;
        if (SBToggle.isOn) x += 2;
        if (LGToggle.isOn) x += 4;
        if (RSpeed > CSpeed) x += 1;

        float T = Mathf.LerpUnclamped(M210[x], M270[x], (float)(CSpeed - 210) / 60);
        if (RRSpeed > CSpeed) T += (float)CVS / 5000 + (float)Flap_Idx / 100; else T -= (float)CVS / 2000 + (float)Flap_Idx / 40;
        if (T < 0.1) T = 0.1f;
        Invoke("Speed_Equalize", T * speedbandspeed);
    }
    public void InterpolateVS()
    {
        double Altitude = CAltitude > 39900 ? 39900 : CAltitude;
        double vv1, vv2, vv3, vv4; // 4 interpolation for VS
        double s1, s2;             // 2 interpolation for Speed// Final interpolation for Altitude
        double limitVS;
        double[] a = new double[4];                  
        increasedSpeed = 0;
        if (VS_Toggle.isOn || AH_Toggle.isOn || VNAV_Toggle.isOn)
        {
            int F = 8 - Mathf.FloorToInt((float)Altitude / 5000);

            int i = 0;//Calculate limit VS
            s1 = (M[F, 1, i] - M[F, 3, i]) / (M[F, 0, 0] - M[F, 2, 0]) * (RSpeed - M[F, 2, 0]) + M[F, 3, i];
            s2 = (M[F - 1, 1, i] - M[F - 1, 3, i]) / (M[F - 1, 0, 0] - M[F - 1, 2, 0]) * (RSpeed - M[F - 1, 2, 0]) + M[F - 1, 3, i];
            limitVS = (s1 - s2) / -5000f * (Altitude - (8 - F + 1) * 5000) + s2;
            if (CVS > limitVS)
            {    // Normal VS mode
                for (i = 1; i < 4; i++)
                {
                    vv1 = (M[F, 0, i] - M[F, 1, i]) / -M[F, 1, 0] * (CVS - M[F, 1, 0]) + M[F, 1, i];
                    vv2 = (M[F, 2, i] - M[F, 3, i]) / -M[F, 3, 0] * (CVS - M[F, 3, 0]) + M[F, 3, i];
                    vv3 = (M[F - 1, 0, i] - M[F - 1, 1, i]) / -M[F - 1, 1, 0] * (CVS - M[F - 1, 1, 0]) + M[F - 1, 1, i];
                    vv4 = (M[F - 1, 2, i] - M[F - 1, 3, i]) / -M[F - 1, 3, 0] * (CVS - M[F - 1, 3, 0]) + M[F - 1, 3, i];
                    s1 = (vv1 - vv2) / (M[F, 0, 0] - M[F, 2, 0]) * (CSpeed - M[F, 2, 0]) + vv2;
                    s2 = (vv3 - vv4) / (M[F - 1, 0, 0] - M[F - 1, 2, 0]) * (CSpeed - M[F - 1, 2, 0]) + vv4;
                    a[i - 1] = (s1 - s2) / -5000f * (Altitude - (8 - F + 1) * 5000) + s2;

                }

            }
            else
            {  // Increased Speed due to excess vertical speed VS mode 
                for (i = 1; i < 4; i++)
                {
                    s1 = (M[F, 1, i] - M[F, 3, i]) / (M[F, 0, 0] - M[F, 2, 0]) * (RSpeed - M[F, 2, 0]) + M[F, 3, i];
                    s2 = (M[F - 1, 1, i] - M[F - 1, 3, i]) / (M[F - 1, 0, 0] - M[F - 1, 2, 0]) * (RSpeed - M[F - 1, 2, 0]) + M[F - 1, 3, i];
                    a[i - 1] = (s1 - s2) / -5000f * (Altitude - (8 - F + 1) * 5000) + s2;
                }
                i = 0; // Find speed for Current VS on idle
                vv1 = (M[F, 0, i] - M[F, 2, i]) / (M[F, 1, 0] - M[F, 3, 0]) * (CVS - M[F, 3, 0]) + M[F, 2, i];
                vv2 = (M[F - 1, 0, i] - M[F - 1, 2, i]) / (M[F - 1, 1, 0] - M[F - 1, 3, 0]) * (CVS - M[F - 1, 3, 0]) + M[F - 1, 2, i];
                increasedSpeed = (int)((vv1 - vv2) / -5000f * (Altitude - (8 - F + 1) * 5000) + vv2);
                //if (CSpeed < increasedSpeed) CSpeed += 2;
            }
            if (RSpeed < CSpeed - 5)
            {
                a[1] = M[8 - (int)(Altitude / 5000), 1, 2];
                a[2] = M[8 - (int)(Altitude / 5000), 1, 3];
            }
            if (RSpeed > CSpeed + 5)
            {
                a[1] = 9650;
                a[2] = 621;
            }
            N1 = (int)a[1];
            FF = (int)a[2];
            SetAttPitch((int)a[0]);

        }
    }
    public static int FuelFlowFor(double Altitude, double VS , double Speed)
    {
     
        int i;
        double vv1, vv2, vv3, vv4;                   // 4 interpolation for VS
        double s1, s2;                               // 2 interpolation for Speed, final one is for Alt
        double ff;

        if (Altitude > 39900) Altitude = 39900;
        if (Altitude <0 ) Altitude =0;

        int F = 8 - Mathf.FloorToInt((float)Altitude / 5000);

          i = 3;// for fuel flow only

                    vv1 = (M[F, 0, i] - M[F, 1, i]) / -M[F, 1, 0] * (VS - M[F, 1, 0]) + M[F, 1, i];
                    vv2 = (M[F, 2, i] - M[F, 3, i]) / -M[F, 3, 0] * (VS - M[F, 3, 0]) + M[F, 3, i];
                    vv3 = (M[F - 1, 0, i] - M[F - 1, 1, i]) / -M[F - 1, 1, 0] * (VS - M[F - 1, 1, 0]) + M[F - 1, 1, i];
                    vv4 = (M[F - 1, 2, i] - M[F - 1, 3, i]) / -M[F - 1, 3, 0] * (VS - M[F - 1, 3, 0]) + M[F - 1, 3, i];
                    s1 = (vv1 - vv2) / (M[F, 0, 0] - M[F, 2, 0]) * (Speed - M[F, 2, 0]) + vv2;
                    s2 = (vv3 - vv4) / (M[F - 1, 0, 0] - M[F - 1, 2, 0]) * (Speed - M[F - 1, 2, 0]) + vv4;
                    ff = (s1 - s2) / -5000f * (Altitude - (8 - F + 1) * 5000) + s2;

              
            return (int )ff;
     }
    private void InterpolateLvlChg()
    {
        double Altitude = CAltitude > 39900 ? 39900 : CAltitude;
        int F, i;
        double s1, s2;             // 2 interpolation for Speed    
        double[] a = new double[4];                  // Final interpolation for Altitude
        if (LC_Toggle.isOn)
        {

            F = 8 - Mathf.FloorToInt(((int)Altitude / 5000));
            for (i = 0; i < 4; i++)
            {

                s1 = (M[F, 1, i] - M[F, 3, i]) / (M[F, 0, 0] - M[F, 2, 0]) * (CSpeed - M[F, 2, 0]) + M[F, 3, i];
                s2 = (M[F - 1, 1, i] - M[F - 1, 3, i]) / (M[F - 1, 0, 0] - M[F - 1, 2, 0]) * (CSpeed - M[F - 1, 2, 0]) + M[F - 1, 3, i];
                a[i] = (s1 - s2) / -5000f * (Altitude - (8 - F + 1) * 5000) + s2;
            }

            N1 = (int)a[2];
            FF = (int)a[3];
            SetAttPitch((int)a[1]);
            if (((int)CSpeed - RSpeed) > 10) RVS = -300; else RVS = (int)a[0];
        }
    }
    private void SetAttPitch(int P)
    {
        NewBehaviourScript Script2 = FindObjectOfType<NewBehaviourScript>();
        Script2.AttUpdate((int)P);
    }
    private void SetN1FF()
    {
        if (N1 > 10000) N1 = 10000;
        int fark = N1 / 100 - dispN1;
        if (fark != 0) dispN1 += fark / Mathf.Abs(fark);
        Progres.transform.localPosition = new Vector2((int)(dispN1 * 1.86 - 100), 0);
        txtN1.text = "" + dispN1;

        int lastdigitFF = FF - FF / 10 * 10;
        fark = FF / 10 * 10 - (int)dispFF;
        if (fark != 0) dispFF += fark / Mathf.Abs(fark) * 10;
        txtFF.text = "" + (dispFF) / 100 + lastdigitFF;
    }
    private double Speed2Mach(double Speed)
    {
        return (Speed * (CAltitude / 1000 * 0.02 + 1) / (660 - 12 * CAltitude / 5000));     //Mach

    }
    private double Mach2Speed(double Mach)
    {
        return (Mach / (CAltitude / 1000 * 0.02 + 1) * (660 - 12 * CAltitude / 5000));     //Mach

    }
    private void FuelandMach()
    {
        totalFuel -= (double)FF * 2 / 3600;                                                     //Fuel
        txtTotalFuel.text = "" + System.Math.Round(totalFuel / 100, 2);

        CMach = Speed2Mach(CSpeed);
        txtMach.text = CMach>0.4 ? "." + System.Math.Round(CMach, 2) * 100: "GS "+GS;

        if (co.isOn)
        {
            RSpeed = (int)Mach2Speed(RMach);
            if (CAltitude < 22720) co.isOn = false;
        }
        if (CAltitude < 22720) co.enabled = false; else co.enabled = true;

    }
    public void FUP_Click()
    {
        if (CAltitude < 20000)
        {
            Flap_Idx += 1;
            SetFlaps();
            if (Flap_Idx > 7) Flap_Idx = 7;
        }
    }
    public void FDown_Click()
    {
        if (CAltitude < 20000)
        {
            Flap_Idx -= 1;
            if (Flap_Idx < 0) Flap_Idx = 0;
            SetFlaps();
        }
    }
    public void SetFlaps()
    {
        int[] Fps = new int[9] { 22, -17, -60, -101, -130, -158, -186, -213, -244 };
        int i, j, k;
        for (i = 0; i < 5; i++)
            for (j = 0; j < 4; j++)
                for (k = 0; k < 4; k++)
                {
                    M[i + 4, j, k] = Mf[Flap_Idx, i, j, k];
                }
        if (LGToggle.isOn) LGToggle_Change();
        if (SBToggle.isOn) SBToggle_Change();

        FlapNeedle.transform.localEulerAngles = new Vector3(0, 0, Fps[Flap_Idx]);

        NewBehaviourScript Script2 = FindObjectOfType<NewBehaviourScript>();
        Script2.Flaps_Indexchange(Flap_Idx);
    }
    public void LGToggle_Change()
    {
        double[,] MVS = new double[9, 2] { { -2500, -1900 }, { -3000, -2100 }, { -3400, -2200 }, { -2000, -1300 }, { -1800, -1200 }, { -1700, -1100 }, { -500, -500 }, { -500, -500 }, { -500, -500 } };
        double[,,] Wlg = new double[4, 2, 2] {
                                             { { 4300, 110 }, { 4170, 113 } },
                                             { { 4150, 125 }, { 4120, 128 } },
                                             { { 4100, 142 }, { 4070, 145 } },
                                             { { 4070, 158 }, { 4050, 165 } } };
        double[,,] WOlg = new double[4, 2, 2] {
                                             { { 3670, 95 }, { 3370, 95 } },
                                             { { 3350, 95 }, { 3250, 95 } },
                                             { { 3240, 95 }, { 3150, 95 } },
                                             { { 3170, 95 }, { 3090, 95 } }};
        int i;
        LGDown = LGToggle.isOn;
        if (LGToggle.isOn)
        {
            for (i = 0; i < 9; i++)
            {
                M[i, 1, 0] += MVS[i, 0];
                M[i, 3, 0] += MVS[i, 1];
                M[i, 0, 2] += 3000; //Duz ucus N1 10/30 arttir
                M[i, 2, 2] += 1000;
                M[i, 0, 3] += 230; //Duz ucus ff 2.3/1.6 arttir
                M[i, 2, 3] += 160;
            }
            for (i = 5; i < 9; i++)  //With LG
            {
                M[i, 1, 2] = Wlg[i - 5, 0, 0];//LVLCG N1 approach mode 
                M[i, 3, 2] = Wlg[i - 5, 1, 0];
                M[i, 1, 3] = Wlg[i - 5, 0, 1];//LVLCG ff approach mode 
                M[i, 3, 3] = Wlg[i - 5, 1, 1];
            }
        }

        else
        {
            for (i = 0; i < 9; i++)
            {
                M[i, 1, 0] -= MVS[i, 0];
                M[i, 3, 0] -= MVS[i, 1];
                M[i, 0, 2] -= 3000; //Duz ucus N1 10/30 azalt
                M[i, 2, 2] -= 1000;
                M[i, 0, 3] -= 230; //Duz ucus ff 2.3/1.6 azalt
                M[i, 2, 3] -= 160;

            }
            for (i = 5; i < 9; i++)   //Without LG
            {
                M[i, 1, 2] = WOlg[i - 5, 0, 0];//LVLCG N1 approach mode 
                M[i, 3, 2] = WOlg[i - 5, 1, 0];
                M[i, 1, 3] = WOlg[i - 5, 0, 1];//LVLCG ff approach mode 
                M[i, 3, 3] = WOlg[i - 5, 1, 1];
            }
        }
    }
    public void SBToggle_Change()
    {
        double[,] Msb = new double[9, 2] { { -1100, -700 }, { -1000, -700 }, { -1400, -1000 }, { -800, -500 }, { -900, -500 }, { -800, -500 }, { -500, -500 }, { -500, -500 }, { -500, -500 } };
        //Sil
        DTG -= 1;
        int i;
        if (SBToggle.isOn)
        {
            for (i = 0; i < 9; i++)
            {
                M[i, 1, 0] += Msb[i, 0];
                M[i, 3, 0] += Msb[i, 1];
                M[i, 0, 2] += 2000; //Duz ucus N1 5/20 arttir
                M[i, 2, 2] += 500;
                M[i, 0, 3] += 80; //Duz ucus ff 0.8/0.6 arttir
                M[i, 2, 3] += 60;

            }
        }
        else
        {
            for (i = 0; i < 9; i++)
            {
                M[i, 1, 0] -= Msb[i, 0];
                M[i, 3, 0] -= Msb[i, 1];
                M[i, 0, 2] -= 2000; //Duz ucus N1 5/20 azalt
                M[i, 2, 2] -= 500;
                M[i, 0, 3] -= 80; //Duz ucus ff 0.8/0.6 azalt
                M[i, 2, 3] -= 60;
            }

        }
    }
    public void Button_Click()

    {
        NewBehaviourScript Script2 = FindObjectOfType<NewBehaviourScript>();

        result = EventSystem.current.currentSelectedGameObject.name; ;      //Speed
        if (co.isOn)                                    //Mach
        {
            if (result == "Sup") RMach += 0.01;
            if (result == "Sdown") RMach -= 0.01;
            if (RMach < 0.6) RMach = 0.6;
            if (RMach > 0.82) RMach = 0.82;
            RSpeed = (int)Mach2Speed(RMach);
            txtRSpeed.text = "" + System.Math.Round(RMach,2);
        }
        else
        {                                               //IAS 
            if (result == "Sup") RSpeed += 1;          
            if (result == "Sdown") RSpeed -= 1;
            if (RSpeed < 110) RSpeed = 110;
            if (RSpeed > 340) RSpeed = 340;
            txtRSpeed.text = "" + RSpeed;
        }
        txtRSpeed_overTape.text = txtRSpeed.text;

        if (result == "Aup") RAltitude += 100;                              //Altitude
        if (result == "Adown") RAltitude -= 100;
        if (RAltitude < 0) RAltitude = 0;
        if (RAltitude > 43000) RAltitude = 43000;
        txtRAltitude.text = "" + RAltitude; txtRAltitude_overTape.text = txtRAltitude.text;

        if (VS_Toggle.isOn)                                                 //VS
        {
            if (result == "Vup") RVS += 100;
            if (result == "Vdown") RVS -= 100;
            if (RVS < -5000) RVS = -5000;
            if (RVS > 5000) RVS = 5000;
            txtRVS.text = "" + RVS;
        }
        if (result == "RHeading")                                           //Heading    
        {
            RHeading += 1;
            if (RHeading > 359) RHeading = 0;
            txtRHeading.text = RHeading.ToString();
        }
        if (result == "LHeading")
        {
            RHeading -= 1;
            if (RHeading < 0) RHeading = 359;
            txtRHeading.text = RHeading.ToString();
        }
        CHeading = RHeading;
        Script2.SpeedIndexUpdate_Click();
        Script2.CheckAltitudeIndicator(RAltitude, (int)CAltitude);
    }
    public void Button_Long_Hold()

    {

        if (LongPressEventTrigger.held2)
        {
            Button_Click();
            Invoke("Button_Long_Hold", 0.1f);
        }
    }
    public void Toggle_Change()
    {
        if (!VS_Toggle.isOn) txtRVS.enabled = false;
        else
        {
            txtRVS.enabled = true;
            RVS = ((int)(CVS / 100)) * 100;
            txtRVS.text = "" + RVS;
        }
        if (AH_Toggle.isOn)
        {
            RVS = 0;
            txtRVS.text = "" + RVS;
        }

    }
    private void ToggleEnable()
    {
        if (RAltitude != CAltitude)
        {
            LC_Toggle.enabled = true;
            VS_Toggle.enabled = true;
        }
        else
        {
            LC_Toggle.enabled = false;
            VS_Toggle.enabled = false;
        }

    }
    public void co_Change() 
    {
       if (co.isOn) // on mach
        {
            RMach = Speed2Mach(RSpeed);
            txtRSpeed.text = ""+System.Math.Round(RMach,2);
            txtRSpeed_overTape.text = txtRSpeed.text;
        }
        else
        {
            RSpeed = (int) Mach2Speed(RMach);
            txtRSpeed.text = "" + RSpeed;
            txtRSpeed_overTape.text = txtRSpeed.text;

        }

    }
    public void DisplayWindElements()
    {

        WindElements WE = CalculateWindElements(CAltitude,CSpeed,RHeading); // Change to current Heading
        windArrow.transform.localEulerAngles= new Vector3(0,0,180-WE.relativeWindD);
        int Track = WE.Track;
        windTxt.text = "GS" + WE.GS + "   TAS" + WE.TAS + "\n" + WE.WindD + "° / " + WE.WindM;
        CWind = WE.WindD + "° / " + WE.WindM;
        GS = WE.GS;
       // Debug.Log(WE.GS + "   " + Track);
    }
    public class WindElements
    {
        public int GS, Track,relativeWindD, WindM, WindD,TAS;
    }
    public int GetWindDirection(double Altitude)
    {
        int BaseAlt = 8 - (int)System.Math.Truncate(Altitude / 5000);
        return (int)Mathf.LerpAngle((float)windTables[0].WindInfoItems[BaseAlt].Degrees,
                                           (float)windTables[0].WindInfoItems[BaseAlt - 1].Degrees,
                                           (float)(Altitude % 5000) / 5000);
    }
    public int GetWindMagnitude(double Altitude)
    {
        int BaseAlt = 8 - (int)System.Math.Truncate(Altitude / 5000);
        return (int)Mathf.LerpUnclamped(windTables[0].WindInfoItems[BaseAlt].Knots,
                                            windTables[0].WindInfoItems[BaseAlt - 1].Knots, 
                                            (float)(Altitude % 5000) / 5000);
    }

    public  static WindElements CalculateWindElements(double Altitude , double IAS,int Heading)
    {
        if (Altitude > 39900) Altitude = 39900;
     
        WindElements WE = new WindElements();
      
        int BaseAlt = 8 - (int)System.Math.Truncate(Altitude / 5000);

        WE.WindD = Calculator.Instance.GetWindDirection(Altitude);
        WE.WindM = Calculator.Instance.GetWindMagnitude(Altitude);

        WE.relativeWindD = WE.WindD + Heading;
        
        double HeadWind = Mathf.Cos(WE.relativeWindD * Mathf.Deg2Rad) * WE.WindM;
        double CrossWind = Mathf.Sin(WE.relativeWindD * Mathf.Deg2Rad) * WE.WindM;
    
        WE.TAS =(int) (IAS + (Altitude / 1000 * 0.02 * IAS));        
        
         WE.GS = (int)(WE.TAS - HeadWind);
         WE.Track = Heading - (int)(Mathf.Atan((float)(CrossWind / WE.GS)) * Mathf.Rad2Deg);

        return WE;
    }

}
