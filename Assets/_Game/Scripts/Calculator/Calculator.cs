using System.Collections;
using Navigation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using Unyawn.Utils;


//GW :56.4,ZFW:45,Fuel:12,CI:0,CG:23.3

public class Calculator : MonoBehaviour
{

    public static int Level = 0;             // ***  Level

    private string result;
    public Text txtRSpeed, txtRAltitude, txtRVS;
    public Text txtCSpeed, txtCAltitude, txtCVS;
    public Text txtRSpeed_overTape, txtRAltitude_overTape;
    public Text txtDTG;//DTG: Distance to go
    public Text txtMeter;//Meter Display
    public Text Qnh, txtMach;

    public Text txtN1, txtFF, txtTotalFuel;// N1( / 100) , Fuel Flow ( X 100) , Pitch attitude ( / 100)
    public TMP_Text VDI_Text;

    public Transform VDI_Index;


    public static double CSpeed = 220, CAltitude = 13000; // Currenr Altitude*************************
    //                            ***              *****
    private int RVS;
    public static int RSpeed = (int)CSpeed, RHeading, RAltitude, CVS;
    public static int CHeading, Track;
    private double CMach, RMach, VNAV_VS;
    public static float TAS, GS;

    public Toggle co;//Landing Gear ,Speed Brake;
    public static bool LGDown = false;
    private bool SBDown = false;
    public Button FUP_Button, FDown_Button;
    private int Flap_Idx, increasedSpeed, excessSpeedCo = 0;
    private float SpeedTime;
    public Text windTxt;
    public static string CWind;
    public Text FMA1, FMA2, FMA3;
    public Image windArrow, VSline, SpeedTrend;
    public GameObject Progres, FlapNeedle, LGlever;
    private int N1, FF, dispN1 = 77;
    private double dispFF = 270;
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

    //**************************************************************

    private static WindTableScriptableObject CurrentWindTable => Session.CurrentLevel.WindTable;

    private static double[,,] M = new double[9, 4, 4] { //speed,pitch,n1,ff
        { {240, 300,9090,240 } , { -1700, 100, 4780,95 } , { 200, 600,8690,212 } , { -1500, 300, 4620,95 } },
        { { 280, 200,9200,306 } , { -2200, -100, 4640,95 } , { 220, 400,8060,220 } , { -1400, 300, 4320,95 } },
        { { 280, 200,8340,282 } , { -2100, -100, 4540,95 } , { 220, 400,7770,222 } , { -1300, 300, 4060,95 } },
        { { 280, 200,8010,282 } , { -1900, -100, 4000,95 } , { 220, 400,7520,221 } , { -1300, 300, 3710,95 } },
        //noflaps identical
        { { 280, 200,7820,282 } , { -1800, -100, 3850,95 } , { 220, 400,7180,224 } , { -1200, 200, 3530,95 } },
        { { 280, 200,7510,279 } , { -1700, -100, 3700,95 } , { 220, 400,6710,221 } , { -1100, 300, 3420,95 } },
        { { 280, 300,7100,276 } , { -1600, -100, 3570,95 } , { 220, 400,6440,222 } , {-1100, 300, 3300,95 } },
        { { 280, 200,6800,275 } , { -1500, 0, 3460,95 } , { 220, 500,6080,221 } , { -1100, 200, 3200,95 } },
        { { 280, 200,6530,274 } , { -1400, 0, 3340,95 } , { 220, 450,5550,220 } , { -1200, 100, 3120,95 } },
    };// Main Matrix

    private double[,,,] Mf = new double[9, 5, 4, 4] {{
        //noflaps
        { { 280, 200,7820,282 } , { -1800, -100, 3850,95 } , { 220, 400,7180,224 } , { -1200, 200, 3530,95 } },
        { { 280, 200,7510,279 } , { -1700, -100, 3700,95 } , { 220, 400,6710,221 } , { -1100, 300, 3420,95 } },
        { { 280, 300,7100,276 } , { -1600, -100, 3570,95 } , { 220, 400,6440,222 } , {-1100, 300, 3300,95 } },
        { { 280, 200,6800,275 } , { -1500, 0, 3460,95 } , { 220, 500,6080,221 } , { -1100, 200, 3200,95 } },
        { { 280, 200,6530,274 } , { -1400, 0, 3340,95 } , { 220, 450,5550,220 } , { -1200, 100, 3120,95 } },},
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
        { { 170, -100,7660,475 }, { -1700, -200, 3240,95 } , { 130, 600,7170,464 }, { -1100,400, 3150,95 }},
        { { 170, -100,6950,453 }, { -1700, -200, 3140,95 } , { 130, 600,6760,455 }, { -1100, 400, 3060,95 }}},
        //f40
        {
        { { 160, -0,8410,520 }, { -1800, -200, 3670,95 } , { 130, 500,8110,497 }, { -1300, 300, 3370,95 } },
        { { 160, -0,8410,520 }, { -1800, -200, 3670,95 } , { 130, 500,8110,497 }, { -1300, 300, 3370,95 } },
        { { 160, -0,8050,509 }, { -1800, -200, 3350,95 } , { 130, 500,7920,497 }, {-1300, 300, 3250,95 }},
        { { 160, -0,7940,530 }, { -1800, -200, 3240,95 } , { 130, 500,7510,496 }, { -1300,300, 3150,95 }},
        { { 160, -100,7300,508 }, { -1800, -200, 3140,95 } , { 130, 500,6920,488 }, { -1300, 300, 3060,95 }}}
    };

    private static float[] Pressure = new float[9]
    { 0.1852f, 0.2352f, 0.2968f, 0.3709f, 0.4594f, 0.5642f, 0.6856f, 0.8320f, 1f }; //40000 to 0

    private static float[] SoundSpeed = new float[9]
        { 575.34f, 576.22f, 589.2f, 601.77f, 614.1f, 626.27f, 638.17f, 649.13f, 663f }; //40000 to 0

    public static Calculator Instance;
    public float GetBananaPosition//Edit
    {
        get
        {
            float oneNM = -Session.Zoom;
            if (CVS == 0)
                return 0;
            else
                return (float)((CAltitude - RAltitude) / CVS * GS / 60 * oneNM);
        }
    }

    private void Awake()
    {
        Instance = this;
    }
    private void Start()
    {
        RAltitude = (int)CAltitude;
        RSpeed = (int)CSpeed;
        RVS = CVS;
        txtRAltitude.text = "" + RAltitude;
        txtRAltitude_overTape.text = txtRAltitude.text;
        txtCAltitude.text = "" + (int)CAltitude;
        txtMeter.text = "" + (int)(CAltitude / 3.28084) + "M";
        txtCSpeed.text = "" + CSpeed;
        txtRSpeed.text = "" + RSpeed;
        txtRSpeed_overTape.text = txtRSpeed.text;

        txtCVS.text = "";


        Invoke(nameof(VS_Equalize), 1f);
        Invoke(nameof(Speed_Equalize), 0.1f);


        StartCoroutine(ExecuteEachSecond());
        StartCoroutine(ExecuteEachFrameSecond());
        StartCoroutine(Altitude_Equalize());
        Flap_Idx = 0;
        SetFlaps();
    }

    private IEnumerator ExecuteEachFrameSecond()
    {
        while (true)
        {
            MatchAltitudes();
            SetN1FF();


            yield return new WaitForSeconds(0.1f);
        }
    }

    private IEnumerator ExecuteEachSecond()
    {
        while (true)
        {
            InterpolateLvlChg();
            InterpolateVS();
            SetFMA();
            FuelandMach();
            //DrawVDI();                 Remove // Causes error at DLE-5
            DisplayWindElements();

            yield return new WaitForSeconds(1);
        }
    }
    public void InterpolateVS()
    {
        double Altitude = CAltitude > 39900 ? 39900 : CAltitude;
        double vv1, vv2, vv3, vv4; // 4 interpolations for VS
        double[] a = new double[4];

        increasedSpeed = 0;

        if (Session.State.VS || Session.State.AH || Session.State.VNAV)
        {
            int F = 8 - Mathf.FloorToInt((float)Altitude / 5000);

            int i = 0;//Calculate limit VS
            double s1 = (M[F, 1, i] - M[F, 3, i]) / (M[F, 0, 0] - M[F, 2, 0]) * (RSpeed - M[F, 2, 0]) + M[F, 3, i];
            double s2 = (M[F - 1, 1, i] - M[F - 1, 3, i]) / (M[F - 1, 0, 0] - M[F - 1, 2, 0]) * (RSpeed - M[F - 1, 2, 0]) + M[F - 1, 3, i];
            double limitVS = (s1 - s2) / -5000f * (Altitude - (8 - F + 1) * 5000) + s2;
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
    private void InterpolateLvlChg()
    {
        double Altitude = CAltitude > 39900 ? 39900 : CAltitude;
        double s1, s2;             // 2 interpolation for Speed    
        double[] a = new double[4];                  // Final interpolation for Altitude

        if (Session.State.LC)
        {
            int F = 8 - Mathf.FloorToInt(((int)Altitude / 5000));
            for (int i = 0; i < 4; i++)
            {

                s1 = (M[F, 1, i] - M[F, 3, i]) / (M[F, 0, 0] - M[F, 2, 0]) * (CSpeed - M[F, 2, 0]) + M[F, 3, i];
                s2 = (M[F - 1, 1, i] - M[F - 1, 3, i]) / (M[F - 1, 0, 0] - M[F - 1, 2, 0]) * (CSpeed - M[F - 1, 2, 0]) + M[F - 1, 3, i];
                a[i] = (s1 - s2) / -5000f * (Altitude - (8 - F + 1) * 5000) + s2;
            }

            N1 = (int)a[2];
            FF = (int)a[3];

            if (SpeedTime > 1) excessSpeedCo += 100;
            if ((int)CSpeed == RSpeed) excessSpeedCo = 0;
            RVS = (int)CSpeed - RSpeed > 10 ? -300 :
                RSpeed - (int)CSpeed > 0 ? (int)a[0] - excessSpeedCo : (int)a[0]; // surat yuksekse 

        }
    }

    public void VS_Equalize()
    {
        void DrawVSline()
        {
            float[] VSlineY = new float[7] { 0, 50.9f, 64.4f, 66.0f, 67.7f, 69.0f, 70.5f };
            int aCVS = Mathf.Abs(CVS);
            float rotation = aCVS < 6000 ? Mathf.LerpUnclamped(VSlineY[aCVS / 1000], VSlineY[aCVS / 1000 + 1], (float)(aCVS % 1000) / 1000) * -Mathf.Sign(CVS) : 70.5f * -Mathf.Sign(CVS);
            float lenght = rotation != 0 ? 500 / Mathf.Cos(rotation * Mathf.Deg2Rad) : 500;
            VSline.transform.localEulerAngles = new Vector3(0, 0, rotation);
            VSline.rectTransform.sizeDelta = new Vector2(lenght, VSline.sprite.rect.height);
            txtCVS.transform.localPosition = CVS > 0 ? new Vector2(200, 90) : new Vector2(200, -100);
        }
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
            }
        }
        txtCVS.text = (Mathf.Abs(CVS) > 300) ? "" + CVS / 100 * 100 : "";

        DrawVSline();

        Invoke("VS_Equalize", (float)(12.75 - CSpeed * 0.025) / 100);

    }

    private IEnumerator Altitude_Equalize()
    {
        while (true)
        {
            if (CVS != 0)
            {
                CAltitude += (float)CVS / 60;
                if (Mathf.Abs(RAltitude - (int)CAltitude) < 10)
                {
                    CAltitude = RAltitude;
                    txtRAltitude.text = "" + RAltitude; txtRAltitude_overTape.text = txtRAltitude.text;
                    CVS = 0;
                    RVS = 0;
                    Session.State.AutoSetAH(true);
                }
                txtCAltitude.text = "" + (int)(CAltitude / 10) * 10;
                txtMeter.text = "" + (int)(CAltitude / 3.28084 / 10) * 10 + "M";
            }
            yield return new WaitForSeconds(1f);
        }
    }
    private void Speed_Equalize()
    {
        double C0Speed = CSpeed;
        void DrawSpeedTrend(float Time)
        {
            int TrendDirection = CSpeed < C0Speed ? -1 : (CSpeed == C0Speed ? 0 : 1);
            SpeedTrend.transform.localScale = new Vector3(1, TrendDirection * 10 / Time, 1);
        }
        int RRSpeed;

        float speedbandspeed = 1f;
        if (RSpeed > increasedSpeed) RRSpeed = RSpeed; else RRSpeed = increasedSpeed;


        PFD_Animation Script2 = FindObjectOfType<PFD_Animation>();

        if (RRSpeed != CSpeed)
        {
            if (RRSpeed < CSpeed) CSpeed -= speedbandspeed;
            else CSpeed += speedbandspeed;

            txtCSpeed.text = "" + (int)CSpeed;

        }
        txtDTG.text = Move.Instance.DME().ToString("F1");
        Script2.SpeedTapeUpdate();
        Script2.SpeedIndexUpdate_Click();

        int DeltaN1 = N1 - N1For(CAltitude, CVS, CSpeed);
        SpeedTime = (DeltaN1 == 0) ? 1 : (DeltaN1 > 0) ? 2000 / Mathf.Abs((float)DeltaN1) : 5380 / Mathf.Abs((float)DeltaN1);
        if (SpeedTime > 5) SpeedTime = 5;
        if (SpeedTime < 0.3) SpeedTime = 0.3f;
        DrawSpeedTrend(SpeedTime);

        Invoke("Speed_Equalize", SpeedTime * speedbandspeed);
    }
    private void SetAttPitch(int P)
    {
        PFD_Animation Script2 = FindObjectOfType<PFD_Animation>();
        Script2.AttUpdate((int)P);
    }
    // public void DrawVDI()
    // {
    //     double DeltaAlt, Alt1, Alt0, d, D;
    //     float posY;
    //
    //     RouteScriptableObject activePoints = Session.ActiveRoute;
    //     RouteScriptableObject modPoints = Session.ModRoute;
    //
    //     bool isMod = Session.IsMod;
    //     RouteScriptableObject _route = isMod ? modPoints : activePoints;
    //
    //
    //     var node0 = _route.Points[PositionVirtualNode.PassedNodeIndex];
    //     var node1 = _route.Points[PositionVirtualNode.PassedNodeIndex + 1];
    //
    //     //Debug.Log(node.DisplayAltitude.ToString());
    //
    //     //DeltaAlt =  activeCurrentPosition.ComputedDistanceLeft * 318.43 + double.Parse(node.DisplayAltitude);
    //     Alt0 = double.Parse(node0.DisplayAltitude);
    //     Alt1 = double.Parse(node1.DisplayAltitude);
    //     d = Session.PlayerAircraft.ComputedDistanceLeftOnSegment;
    //     D = (d + Session.PlayerAircraft.NMWalkedOnCurrentSegment); // Daniel: this will behave bad while free flight
    //
    //     DeltaAlt = CAltitude - (Alt1 + (d * (Alt0 - Alt1)) / D);
    //     VDI_Text.text = (((DeltaAlt) > 50) || ((DeltaAlt) < -50)) ? "" + (int)DeltaAlt : "";
    //     if (Session.State.VNAV)
    //     {
    //         VNAV_VS = -((CAltitude - Alt1) * GS) / (60 * d) - DeltaAlt * 2;
    //         RVS = (DeltaAlt < -50) ? -100 : (int)VNAV_VS;
    //     }
    //
    //     posY = -((float)DeltaAlt / 5);
    //     if (posY > 100) posY = 100;
    //     if (posY < -100) posY = -100;
    //
    //     VDI_Index.transform.localPosition = new Vector2(0.3f, posY / 125);
    //     if (DeltaAlt < 0) VDI_Text.transform.localPosition = new Vector2(0.1f, -1);
    //     else VDI_Text.transform.localPosition = new Vector2(0.1f, 1);
    // }
    public void SetFMA()
    {
        if (Session.State.HDG) isHDG = true; else isHDG = false; //For Move.cs Delete later
        if (Session.State.HDG) FMA2.text = "HDG"; else FMA2.text = "LNAV";
        if (Session.State.LC)
        {
            FMA1.text = "IDLE";
            FMA3.text = "MCP SPD";
        }
        if (Session.State.AH)
        {
            FMA1.text = "MCP SPD";
            FMA3.text = "ALT";
        }
        if (Session.State.VS)
        {
            FMA1.text = "MCP SPD";
            FMA3.text = "VS";
        }


    }
    private void MatchAltitudes()
    {
        PFD_Animation Script2 = FindObjectOfType<PFD_Animation>();
        Script2.AltUpdate((int)CAltitude);
        Script2.CheckAltitudeIndicator(RAltitude, (int)CAltitude);
        if (CAltitude >= 10000) Qnh.text = "STD";
        else Qnh.text = "1013";
    }
    public static int FuelFlowFor(double Altitude, double VS, double Speed)
    {
        if (Altitude > 39900) Altitude = 39900;
        if (Altitude < 0) Altitude = 0;

        int F = 8 - Mathf.FloorToInt((float)Altitude / 5000);
        int i = 3;// for fuel flow only

        double vv1 = (M[F, 0, i] - M[F, 1, i]) / -M[F, 1, 0] * (VS - M[F, 1, 0]) + M[F, 1, i];
        double vv2 = (M[F, 2, i] - M[F, 3, i]) / -M[F, 3, 0] * (VS - M[F, 3, 0]) + M[F, 3, i];
        double vv3 = (M[F - 1, 0, i] - M[F - 1, 1, i]) / -M[F - 1, 1, 0] * (VS - M[F - 1, 1, 0]) + M[F - 1, 1, i];
        double vv4 = (M[F - 1, 2, i] - M[F - 1, 3, i]) / -M[F - 1, 3, 0] * (VS - M[F - 1, 3, 0]) + M[F - 1, 3, i];
        double s1 = (vv1 - vv2) / (M[F, 0, 0] - M[F, 2, 0]) * (Speed - M[F, 2, 0]) + vv2;
        double s2 = (vv3 - vv4) / (M[F - 1, 0, 0] - M[F - 1, 2, 0]) * (Speed - M[F - 1, 2, 0]) + vv4;
        return (int)((s1 - s2) / -5000f * (Altitude - (8 - F + 1) * 5000) + s2);
    }
    public int N1For(double Altitude, double VS, double Speed)
    {
        if (Altitude > 39900) Altitude = 39900;
        if (Altitude < 0) Altitude = 0;

        int F = 8 - Mathf.FloorToInt((float)Altitude / 5000);
        int i = 2;// for N1 only

        double vv1 = (M[F, 0, i] - M[F, 1, i]) / -M[F, 1, 0] * (VS - M[F, 1, 0]) + M[F, 1, i];
        double vv2 = (M[F, 2, i] - M[F, 3, i]) / -M[F, 3, 0] * (VS - M[F, 3, 0]) + M[F, 3, i];
        double vv3 = (M[F - 1, 0, i] - M[F - 1, 1, i]) / -M[F - 1, 1, 0] * (VS - M[F - 1, 1, 0]) + M[F - 1, 1, i];
        double vv4 = (M[F - 1, 2, i] - M[F - 1, 3, i]) / -M[F - 1, 3, 0] * (VS - M[F - 1, 3, 0]) + M[F - 1, 3, i];
        double s1 = (vv1 - vv2) / (M[F, 0, 0] - M[F, 2, 0]) * (Speed - M[F, 2, 0]) + vv2;
        double s2 = (vv3 - vv4) / (M[F - 1, 0, 0] - M[F - 1, 2, 0]) * (Speed - M[F - 1, 2, 0]) + vv4;
        return (int)((s1 - s2) / -5000f * (Altitude - (8 - F + 1) * 5000) + s2);
    }
    private void SetN1FF()
    {
        if (N1 > 10000) N1 = 10000;
        int fark = N1 / 100 - dispN1;
        if (fark != 0) dispN1 += fark / Mathf.Abs(fark);
        Progres.transform.localPosition = new Vector2((int)(dispN1 * 1.86 - 100), 0);
        txtN1.text = "" + dispN1;

        fark = FF / 10 * 10 - (int)dispFF;
        int lastdigit = FF - FF / 10 * 10;
        if (fark != 0) dispFF += fark / Mathf.Abs(fark) * 10;
        txtFF.text = "" + ((dispFF + lastdigit) / 100).ToString("0.00");

    }
    public static double Speed2Mach(double Speed, double Altitude)
    {
        Altitude = Altitude > 39900 ? 39900 : Altitude;

        int F = 8 - Mathf.FloorToInt((float)Altitude / 5000);
        float p = Mathf.Lerp(Pressure[F], Pressure[F - 1], (float)(Altitude % 5000) / 5000);

        return Mathf.Sqrt(Mathf.Pow(1 / p * (Mathf.Pow((float)Speed * (float)Speed / 2187771 + 1, 3.5f) - 1) + 1, 0.2857f) - 1) * Mathf.Sqrt(5);
    }
    public static double Mach2Speed(double Mach, double Altitude)
    {
        Altitude = Altitude > 39900 ? 39900 : Altitude;
        int F = 8 - Mathf.FloorToInt((float)Altitude / 5000);
        float p = Mathf.Lerp(Pressure[F], Pressure[F - 1], (float)(Altitude % 5000) / 5000);
        float SS = Mathf.Lerp(SoundSpeed[F], SoundSpeed[F - 1], (float)(Altitude % 5000) / 5000);
        float TAS_ = (float)Mach * SS;
        return Mathf.Sqrt(Mathf.Pow(p * (Mathf.Pow(TAS_ * TAS_ / 1653125 + 1, 3.5f) - 1) + 1, 0.2857f) - 1) * Mathf.Sqrt(5) * 661.4787;

    }
    public static double CrossOverAltitude(double Speed, double Mach)
    {
        float ro = (Mathf.Pow(1 + 0.2f * Mathf.Pow((float)Speed / 661.48f, 2), 3.5f) - 1) / (Mathf.Pow((float)(1 + 0.2 * Mach * Mach), 3.5f) - 1);
        return Mathf.Floor(145442.16f * (1 - Mathf.Pow(ro, 0.1902631f)));
    }
    private void FuelandMach()
    {
        totalFuel -= (double)FF * 2 / 3600;                                                     //Fuel
        txtTotalFuel.text = "" + System.Math.Round(totalFuel / 100, 2);

        CMach = Speed2Mach(CSpeed, CAltitude);
        txtMach.text = CMach > 0.4 ? "." + System.Math.Round(CMach, 2) * 100 : "GS " + GS;

        if (co.isOn)
        {
            RSpeed = (int)Mach2Speed(RMach, CAltitude);
            if (CAltitude < 26400) co.isOn = false;
        }
        if (CAltitude < 26400) co.enabled = false; else co.enabled = true;

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
        int[] Fps = new int[9] { -179, -145, -103, -64, -35, -3, 29, 57, 86 };
        int i, j, k;
        for (i = 0; i < 5; i++)
            for (j = 0; j < 4; j++)
                for (k = 0; k < 4; k++)
                {
                    M[i + 4, j, k] = Mf[Flap_Idx, i, j, k];
                }
        if (LGDown) LG_Click();
        if (SBDown) SetSB(false);
        

        if (FlapNeedle != null)
        {
            FlapNeedle.transform.localEulerAngles = new Vector3(-90, 180, Fps[Flap_Idx]);
        }
        else
        {
            Debug.LogError("Assign flapNeedle");
        }

        PFD_Animation Script2 = FindObjectOfType<PFD_Animation>();
        Script2.Flaps_Indexchange(Flap_Idx);
    }
    public void LG_Click()
    {
        double[,] MVS = new double[9, 2] { { -2100, -1500 }, { -2700, -1500 }, { -2700, -1400 }, { -2600, -1300 }, { -2500, -1300 }, { -2300, -1200 }, { -1800, -600 }, { -1600, -500 }, { -1500, -300 } };
        double[,,] Wlg = new double[4, 2, 2] {
                                             { { 4300, 110 }, { 4170, 113 } },
                                             { { 4150, 125 }, { 4120, 128 } },
                                             { { 4100, 142 }, { 4070, 145 } },
                                             { { 4070, 158 }, { 4050, 165 } } };
        double[,,] WOlg = new double[4, 2, 2] {                                                 //IDLE
                                             { { 3700, 95 }, { 3420, 95 } },
                                             { { 3370, 95 }, { 3300, 95 } },
                                             { { 3460, 95 }, { 3200, 95 } },
                                             { { 3340, 95 }, { 3120, 95 } }};
        int i;
        LGDown = !LGDown;
        if (LGDown)
        {
            LGlever.transform.localEulerAngles = new Vector3(-45, 0, 0);

            for (i = 0; i < 9; i++)
            {
                M[i, 1, 0] += MVS[i, 0];
                M[i, 3, 0] += MVS[i, 1];
                M[i, 0, 2] += 1500;  //Duz ucus 
                M[i, 2, 2] += 1350;
                M[i, 0, 3] += 320;
                M[i, 2, 3] += 200;
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
            LGlever.transform.localEulerAngles = new Vector3(-90, 0, 0);

            for (i = 0; i < 9; i++)
            {
                M[i, 1, 0] -= MVS[i, 0];
                M[i, 3, 0] -= MVS[i, 1];
                M[i, 0, 2] -= 1500; //Duz ucus 
                M[i, 2, 2] -= 1350;
                M[i, 0, 3] -= 320;
                M[i, 2, 3] -= 200;

            }
            for (i = 5; i < 9; i++)   //Without LG
            {
                M[i, 1, 2] = WOlg[i - 5, 0, 0];//LVLCG N1 not in approach mode 
                M[i, 3, 2] = WOlg[i - 5, 1, 0];
                M[i, 1, 3] = WOlg[i - 5, 0, 1];//LVLCG ff not in approach mode 
                M[i, 3, 3] = WOlg[i - 5, 1, 1];
            }
        }
    } 
    public void SetSB(bool down, bool fromUI = false)
    {
        if (!fromUI)
        {
            UYServiceLocator.Get<McpUI>().SBLeverInteract(down, true);
        }
        
        double[,] Msb = new double[9, 2] { { -900, -600 }, { -900, -600 }, { -900, -500 }, { -900, -500 }, { -900, -400 }, { -900, -400 }, { -900, -400 }, { -900, -500 }, { -900, -400 } };
 
        int i;
        SBDown = down;
        if (SBDown)
        {
            for (i = 0; i < 9; i++)
            {
                M[i, 1, 0] += Msb[i, 0];
                M[i, 3, 0] += Msb[i, 1];
                M[i, 0, 2] += 750; //Duz ucus
                M[i, 2, 2] += 450;
                M[i, 0, 3] += 115;
                M[i, 2, 3] += 80;

            }
        }
        else
        {
            for (i = 0; i < 9; i++)
            {
                M[i, 1, 0] -= Msb[i, 0];
                M[i, 3, 0] -= Msb[i, 1];
                M[i, 0, 2] -= 750; //Duz ucus
                M[i, 2, 2] -= 450;
                M[i, 0, 3] -= 115;
                M[i, 2, 3] -= 80;
            }
        }
    }

    public static void Check_LimitSpeed()
    {
        if (Calculator.Instance.co.isOn)                                    //Mach
        {
            if (Calculator.Instance.RMach < 0.6) Calculator.Instance.RMach = 0.6;
            if (Calculator.Instance.RMach > Speed2Mach(PFD_Animation.LimitSpeed, CAltitude)) Calculator.Instance.RMach = Speed2Mach(PFD_Animation.LimitSpeed, CAltitude);
            RSpeed = (int)Mach2Speed(Calculator.Instance.RMach, CAltitude);
            Calculator.Instance.txtRSpeed.text = "" + System.Math.Round(Calculator.Instance.RMach, 2);
        }
        else
        {                                               //IAS 
            if (RSpeed < 110) RSpeed = 110;
            if (RSpeed > PFD_Animation.LimitSpeed) RSpeed = PFD_Animation.LimitSpeed;
            Calculator.Instance.txtRSpeed.text = "" + RSpeed;
        }
        Calculator.Instance.txtRSpeed_overTape.text = Calculator.Instance.txtRSpeed.text;
    }

    public void UpdatePFD()
    {
        
        PFD_Animation pfdAnimation = FindObjectOfType<PFD_Animation>();
        pfdAnimation.SpeedIndexUpdate_Click();
        pfdAnimation.CheckAltitudeIndicator(RAltitude, (int) CAltitude);
    }

    public void OnClick_HDG(bool positive)
    {
        if (positive) //Heading    
        {
            RHeading += 1;
            if (RHeading > 359) RHeading = 0;
            if (Mathf.Abs(Mathf.DeltaAngle(RHeading, Move.Perpend)) < 90)
            {
                Time.timeScale = 1;
            }
        }
        else
        {
            RHeading -= 1;
            if (RHeading < 0) RHeading = 359;

            if (Mathf.Abs(Mathf.DeltaAngle(RHeading, Move.Perpend)) < 90)
            {
                Time.timeScale = 1;
            }
        }

        UYServiceLocator.Get<McpUI>().RefreshHS();
        CHeading = RHeading;
        
        UpdatePFD();
    }

    public void OnClick_S(bool positive)
    {
        if (co.isOn) //Mach
        {
            if (positive) RMach += 0.01;
            if (!positive) RMach -= 0.01;
        }
        else
        {
            //IAS 
            if (positive) RSpeed += 1;
            if (!positive) RSpeed -= 1;
        }

        Check_LimitSpeed();
        txtRSpeed_overTape.text = txtRSpeed.text;
        
        
        UpdatePFD();
    }

    public void OnClick_A(bool positive)
    {
        if (positive) RAltitude += 1000; //Altitude
        if (!positive) RAltitude -= 1000;
        if (RAltitude < 0) RAltitude = 0;
        if (RAltitude > 43000) RAltitude = 43000;
        txtRAltitude.text = "" + RAltitude;
        txtRAltitude_overTape.text = txtRAltitude.text;
    }

    public void OnClick_V(bool positive)
    {
        if (Session.State.VS) //VS
        {
            if (positive) RVS += 100;
            if (!positive) RVS -= 100;
            if (RVS < -5000) RVS = -5000;
            if (RVS > 5000) RVS = 5000;
            txtRVS.text = "" + RVS;
        }


        UpdatePFD();
    }

    public void Toggle_Change()
    {
        if (!Session.State.VS) txtRVS.enabled = false;
        else
        {
            txtRVS.enabled = true;
            RVS = CVS / 100 * 100;
            txtRVS.text = "" + RVS;
        }
        if (Session.State.AH)
        {
            RVS = 0;
            txtRVS.text = "" + RVS;
        }

    }

   
    public void co_Change()
    {
        double[,] MVS = new double[5, 2] { { -800, -280 }, { -680, -520 }, { -500, -400 }, { -430, -140 }, { -420, -240 } };
        if (co.isOn) // on mach
        {
            RMach = Speed2Mach(RSpeed, CAltitude);
            txtRSpeed.text = "" + System.Math.Round(RMach, 2);
            txtRSpeed_overTape.text = txtRSpeed.text;
            for (int i = 0; i < 4; i++)
            {
                M[i, 1, 0] += MVS[i, 0];
                M[i, 3, 0] += MVS[i, 1];

            }
        }
        else
        {
            RSpeed = (int)Mach2Speed(RMach, CAltitude);
            txtRSpeed.text = "" + RSpeed;
            txtRSpeed_overTape.text = txtRSpeed.text;
            for (int i = 0; i < 4; i++)
            {
                M[i, 1, 0] -= MVS[i, 0];
                M[i, 3, 0] -= MVS[i, 1];

            }

        }
    }
    public void DisplayWindElements()
    {

        WindElements WE = CalculateWindElements(CAltitude, CSpeed, RHeading); // Change to current Heading
        windArrow.transform.localEulerAngles = new Vector3(0, 0, 180 - WE.relativeWindD);
        int Track = WE.Track;
        windTxt.text = "GS" + WE.GS + "   TAS" + WE.TAS + "\n" + WE.WindD + "° / " + WE.WindM;
        CWind = WE.WindD + "° / " + WE.WindM;
        GS = WE.GS;
        // Debug.Log(WE.GS + "   " + Track);
    }
    public class WindElements
    {
        public int GS, Track, relativeWindD, WindM, WindD, TAS;
    }

    private static int GetWindDirection(double altitude)
    {
        int baseAlt = 8 - (int)System.Math.Truncate(altitude / 5000);
        return (int)Mathf.LerpAngle((float)CurrentWindTable.WindInfoItems[baseAlt].Degrees,
                                           (float)CurrentWindTable.WindInfoItems[baseAlt - 1].Degrees,
                                           (float)(altitude % 5000) / 5000);
    }
    public static int GetWindMagnitude(double altitude)
    {
        int baseAlt = 8 - (int)System.Math.Truncate(altitude / 5000);
        return (int)Mathf.LerpUnclamped(CurrentWindTable.WindInfoItems[baseAlt].Knots,
                                            CurrentWindTable.WindInfoItems[baseAlt - 1].Knots,
                                            (float)(altitude % 5000) / 5000);
    }
    public static WindElements CalculateWindElements(double Altitude, double IAS, int Heading)
    {
        if (Altitude > 39900) Altitude = 39900;

        WindElements WE = new WindElements();

        int BaseAlt = 8 - (int)System.Math.Truncate(Altitude / 5000);

        WE.WindD = GetWindDirection(Altitude);
        WE.WindM = GetWindMagnitude(Altitude);

        WE.relativeWindD = WE.WindD + Heading;

        double HeadWind = Mathf.Cos(WE.relativeWindD * Mathf.Deg2Rad) * WE.WindM;
        double CrossWind = Mathf.Sin(WE.relativeWindD * Mathf.Deg2Rad) * WE.WindM;


        WE.TAS = (int)(IAS + (Altitude / 1000 * 0.02 * IAS));

        WE.GS = (int)(WE.TAS - HeadWind);
        WE.Track = Heading - (int)(Mathf.Atan((float)(CrossWind / WE.GS)) * Mathf.Rad2Deg);

        return WE;
    }
}
