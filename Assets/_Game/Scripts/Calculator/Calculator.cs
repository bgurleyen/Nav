using System;
using System.Collections;
using System.Text;
using System.Text.RegularExpressions;
using JetBrains.Annotations;
using MoreMountains.NiceVibrations;
using Navigation;
using Navigation.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Unyawn.Utils;

// GW :56.4,ZFW:45,Fuel:12,CI:0,CG:23.3

public class Calculator : MonoBehaviour
{

    public static int Level = 11;             // ***  Level

    public int prvIndex4Speed;

    private string result;
    public Text txtRSpeed, txtRAltitude, txtRVS, txtRHeading;
    public Text txtCSpeed, txtCAltitude, txtCVS;
    public Text txtRSpeed_overTape, txtRAltitude_overTape;
    public Text txtDTG;//DTG: Distance to go
    public Text txtMeter;//Meter Display
    public Text Qnh, txtMach;

    public Text txtN1, txtFF, txtTotalFuel;// N1( / 100) , Fuel Flow ( X 100) , Pitch attitude ( / 100)
    public TMP_Text VDI_Text;

    public Transform VDI_Index;
    public double StartAltitude;
    public static double CSpeed, CAltitude; // Current Altitude*************************
    //                            
    private int RVS;
    public static int RSpeed = (int)CSpeed, minimumSpeed = 110, RHeading, RAltitude, CVS, CHeading;

    public static int CTrack, Track, RTrack;
    private double CMach, RMach, VNAV_VS;
    private bool _fmcCruiseClearedOnVnavDescent;
    public static float TAS, GS;
    public Toggle co;//Landing Gear ,Speed Brake;
    public static bool LGDown = false;
    public static bool SBUp = false;
    public Button FUP_Button, FDown_Button;
    private int increasedSpeed, excessSpeedCo = 0;
    public static int Flap_Idx = 0;
    private const int MaxFlapIdx = 8; // Mf[8]=f40, Fps[8]=86°
    private float SpeedTime;
    public Text windTxt;
    public static string CWind;
    public static int HeadingWindAddition, CHeadingWindAddition;
    public Text FMA1, FMA2, FMA3, FMAarmed;
    public Image windArrow, VSline, SpeedTrend;
    public GameObject Progres, FlapNeedle, LGlever;
    private int N1, dispN1 = 77;
    public static int FF = 77;
    public static double dispFF = 270;
    public static double totalFuel = 1000; // 10 tons *100 
    public RouteScriptableObject routeMain;


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
        //Mf noflaps identical
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



    public static int CachedDisplayLastAngleDiff;

    public void PFD_Bank()
    {
        PFD_Animation PFDScript = FindObjectOfType<PFD_Animation>();
        int Angle = CachedDisplayLastAngleDiff;

        int Bank = Angle < -3 ? -27 : Angle > 3 ? 27 : 0;

        PFDScript.PFD_Bank(Bank);

        //Debug.Log("B  : " + Bank + "  a  : " + Angle);

    }

    private void Start()
    {
        // Force-parse route altitude tokens; RestrictionExact stays -1 until GetFlag runs.
        var startPoint = Session.ActiveRoute.Points[0];
        _ = startPoint.AltitudeRegulation;
        long cruiseAltitude = startPoint.Altitude.RestrictionExact;
        if (cruiseAltitude <= 0)
            cruiseAltitude = Session.CurrentLevel.levelInfo.CrzAltitude;
        if (cruiseAltitude <= 0)
            cruiseAltitude = 10000;

        Session.CurrentLevel.levelInfo.CrzAltitude = cruiseAltitude;
        StartAltitude = cruiseAltitude;

        CSpeed = Session.CurrentLevel.levelInfo.CrzSpeed;
        CAltitude = StartAltitude;
        RAltitude = (int)CAltitude;
        RSpeed = (int)CSpeed;
        RVS = CVS;
        _approachEndTriggered = false;
        txtRAltitude.text = "" + RAltitude;
        txtRAltitude_overTape.text = txtRAltitude.text;
        txtCAltitude.text = "" + (int)CAltitude;
        txtMeter.text = "" + (int)(CAltitude / 3.28084) + "M";
        txtCSpeed.text = "" + (int)CSpeed;
        txtCVS.text = "";
        FMAarmed.text = "";

        ApplyInitialSpeedDisplayForAltitude();

        Invoke(nameof(VS_Equalize), 1f);
        Invoke(nameof(Speed_Equalize), 0.1f);


        StartCoroutine(ExecuteEachSecond());
        StartCoroutine(ExecuteEachFrameSecond());
        StartCoroutine(Altitude_Equalize());
        Flap_Idx = 0;
        SetFlaps();

        Session.Settings.SpeedMultiplier = 1;

        // Score Test scene drives Main with its own dialog (pauses). Skip normal briefing.
        if (ScoreTestSceneBootstrap.ConsumePending())
            return;

        // Level Test scene: pick starting level, then fly 320 kt @ 10x with ATC ignored.
        if (LevelTestMode.IsActive)
        {
            if (LevelTestMode.ConsumeAwaitingLevelPick())
            {
                Time.timeScale = 0f;
                LevelTestDialog.ShowLevelPicker();
                return;
            }

            if (LevelTestMode.ConsumePendingApplyConfig())
            {
                LevelTestMode.ApplyFlightConfig();
                return;
            }
        }

        var levelInfo = FindFirstObjectByType<LevelStartInformation>();
        if (levelInfo != null)
        {
            // Keep time running until FMC is populated, then pause on the briefing panel.
            levelInfo.ShowInfo();
        }
        else
        {
            if (!PlayerPrefsHolder.ShowLevelSelectOnLoad)
                Time.timeScale = 1f;
            Debug.LogWarning("Calculator.Start: LevelStartInformation not found in scene.");
        }
    }

    /// <summary>
    /// MCP selected speed + PFD Mach window match start altitude (Mach at/above schedule CO).
    /// IAS values (CSpeed/RSpeed) stay knots for physics; only display / CO toggle change.
    /// </summary>
    private void ApplyInitialSpeedDisplayForAltitude()
    {
        CMach = Speed2Mach(CSpeed, CAltitude);
        if (txtMach != null)
            txtMach.text = CMach > 0.4 ? FormatMach(CMach) : "GS " + GS;

        double coAlt = GetCoAltitudeFeet();
        bool aboveCo = CAltitude >= coAlt;
        co.enabled = aboveCo;

        if (aboveCo)
        {
            RMach = Speed2Mach(RSpeed, CAltitude);
            if (!co.isOn)
            {
                // Fires co_Change → Mach window text + VS matrix
                co.isOn = true;
            }
            else
            {
                txtRSpeed.text = FormatMach(RMach);
                txtRSpeed_overTape.text = txtRSpeed.text;
            }
        }
        else
        {
            if (co.isOn)
            {
                co.isOn = false;
            }
            else
            {
                txtRSpeed.text = "" + RSpeed;
                txtRSpeed_overTape.text = txtRSpeed.text;
            }
        }
    }

    private IEnumerator ExecuteEachFrameSecond()
    {
        //GlideSlope = infoFMC.Instance.Fmc.Initref.GlideSlope;
        while (true)
        {
            CTrack = (int)Session.PlayerAircraft.DisplayHeadingDegrees;

            MatchAltitudes();
            SetN1FF();
            PFD_Bank();
            yield return new WaitForSeconds(0.1f);
        }

    }

    private IEnumerator ExecuteEachSecond()
    {
        while (true)
        {
            FlyVerticalPath();
            InterpolateVS();
            InterpolateLvlChg();
            SetFMA();
            FuelandMach();
            DrawVDI();
            DisplayWindElements();
            CheckStabilization();

            //Debug.Log(Move.Instance.FuelPenalty);
            yield return new WaitForSeconds(1);
        }
    }

    public void InterpolateVS()
    {
        double Altitude = CAltitude > 39900 ? 39900 : CAltitude;
        double vv1, vv2, vv3, vv4; // 4 interpolations for VS
        double[] a = new double[4];

        increasedSpeed = 0;

        if (Session.State.VS || Session.State.AH || Session.State.VNAV || Session.State.GSCaptured)
        {
            int F = 8 - Mathf.FloorToInt((float)Altitude / 5000);

            // Path modes command pitch from RVS; other modes follow lagged CVS.
            double vsRef = (Session.State.GSCaptured || Session.State.VNAV) ? RVS : CVS;

            int i = 0;//Calculate limit VS
            double s1 = (M[F, 1, i] - M[F, 3, i]) / (M[F, 0, 0] - M[F, 2, 0]) * (RSpeed - M[F, 2, 0]) + M[F, 3, i];
            double s2 = (M[F - 1, 1, i] - M[F - 1, 3, i]) / (M[F - 1, 0, 0] - M[F - 1, 2, 0]) * (RSpeed - M[F - 1, 2, 0]) + M[F - 1, 3, i];
            double limitVS = (s1 - s2) / -5000f * (Altitude - (8 - F + 1) * 5000) + s2;
            bool pathWithinIdle = vsRef > limitVS;
            if (pathWithinIdle)
            {    // Normal VS mode
                for (i = 1; i < 4; i++)
                {
                    vv1 = (M[F, 0, i] - M[F, 1, i]) / -M[F, 1, 0] * (vsRef - M[F, 1, 0]) + M[F, 1, i];
                    vv2 = (M[F, 2, i] - M[F, 3, i]) / -M[F, 3, 0] * (vsRef - M[F, 3, 0]) + M[F, 3, i];
                    vv3 = (M[F - 1, 0, i] - M[F - 1, 1, i]) / -M[F - 1, 1, 0] * (vsRef - M[F - 1, 1, 0]) + M[F - 1, 1, i];
                    vv4 = (M[F - 1, 2, i] - M[F - 1, 3, i]) / -M[F - 1, 3, 0] * (vsRef - M[F - 1, 3, 0]) + M[F - 1, 3, i];
                    s1 = (vv1 - vv2) / (M[F, 0, 0] - M[F, 2, 0]) * (CSpeed - M[F, 2, 0]) + vv2;
                    s2 = (vv3 - vv4) / (M[F - 1, 0, 0] - M[F - 1, 2, 0]) * (CSpeed - M[F - 1, 2, 0]) + vv4;
                    a[i - 1] = (s1 - s2) / -5000f * (Altitude - (8 - F + 1) * 5000) + s2;
                }
            }
            else
            {  // Path/VS steeper than idle: raise speed target — path over deceleration
                for (i = 1; i < 4; i++)
                {
                    s1 = (M[F, 1, i] - M[F, 3, i]) / (M[F, 0, 0] - M[F, 2, 0]) * (RSpeed - M[F, 2, 0]) + M[F, 3, i];
                    s2 = (M[F - 1, 1, i] - M[F - 1, 3, i]) / (M[F - 1, 0, 0] - M[F - 1, 2, 0]) * (RSpeed - M[F - 1, 2, 0]) + M[F - 1, 3, i];
                    a[i - 1] = (s1 - s2) / -5000f * (Altitude - (8 - F + 1) * 5000) + s2;
                }
                i = 0; // Find speed for Current VS on idle
                vv1 = (M[F, 0, i] - M[F, 2, i]) / (M[F, 1, 0] - M[F, 3, 0]) * (vsRef - M[F, 3, 0]) + M[F, 2, i];
                vv2 = (M[F - 1, 0, i] - M[F - 1, 2, i]) / (M[F - 1, 1, 0] - M[F - 1, 3, 0]) * (vsRef - M[F - 1, 3, 0]) + M[F - 1, 2, i];
                increasedSpeed = (int)((vv1 - vv2) / -5000f * (Altitude - (8 - F + 1) * 5000) + vv2);
            }
            // Idle only when decelerating AND path still within idle capability (path first).
            if (RSpeed < CSpeed - 5 && pathWithinIdle)
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

            if ((RAltitude != CAltitude) || (Session.State.GSCaptured))
            {
                int step = 10 * Session.Settings.SpeedMultiplier;
                // VNAV path capture: close large VS errors faster so deviation correction bites sooner.
                // Not during GS — aggressive step hunts with the path loop.
                if (Session.State.VNAV && !Session.State.GSCaptured && Mathf.Abs(RVS - CVS) > 400)
                    step = 40 * Session.Settings.SpeedMultiplier;

                if (RVS < CVS) CVS -= step;
                if (CVS < RVS) CVS += step;
                if (Mathf.Abs(RVS - CVS) < step) CVS = RVS;

                // MCP altitude capture flare — must not fight GS commanded VS.
                if (!Session.State.GSCaptured
                    && (Mathf.Abs(RVS) > 1000)
                    && (Mathf.Abs(RAltitude - (int)CAltitude) < Mathf.Abs(CVS / 2f) - 600))
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
                var previousAltitude = CAltitude;

                CAltitude += (float)CVS / 60 * Session.Settings.SpeedMultiplier;

                if (!Session.State.GSCaptured)

                {
                    bool crossedTargetAltitude =
                      (previousAltitude <= RAltitude && CAltitude >= RAltitude) ||
                      (previousAltitude >= RAltitude && CAltitude <= RAltitude);

                    if (crossedTargetAltitude || (Mathf.Abs(RAltitude - (int)CAltitude) < 10))
                    {
                        CAltitude = RAltitude;
                        txtRAltitude.text = "" + RAltitude;
                        txtRAltitude_overTape.text = txtRAltitude.text;
                        CVS = 0;
                        RVS = 0;
                        // VNAV: hold MCP as VNAV ALT (keep VNAV). Other modes → ALT HOLD.
                        if (!Session.State.VNAV)
                            Session.State.AutoSetAH(true);
                    }
                }
                txtCAltitude.text = "" + (int)(CAltitude / 10) * 10;
                txtMeter.text = "" + (int)(CAltitude / 3.28084 / 10) * 10 + "M";
            }
            yield return new WaitForSeconds(1f);
        }
    }
    private void Speed_Equalize()
    {
        if (LevelTestMode.IsActive)
            LevelTestMode.EnforceFlightConfig();

        double C0Speed = CSpeed;
        void DrawSpeedTrend(float Time)
        {
            int TrendDirection = CSpeed < C0Speed ? -1 : (CSpeed == C0Speed ? 0 : 1);
            SpeedTrend.transform.localScale = new Vector3(1, TrendDirection * 10 / Time, 1);
        }
        int RRSpeed;

        float speedbandspeed = 1f;// Session.Settings.SpeedMultiplier;
        if (RSpeed > increasedSpeed) RRSpeed = RSpeed; else RRSpeed = increasedSpeed;

        RSpeed = ApplyVnavPointSpeedLimit(RSpeed);

        PFD_Animation PFDScript = FindObjectOfType<PFD_Animation>();

        if (RRSpeed != CSpeed)
        {
            if (RRSpeed < CSpeed) CSpeed -= speedbandspeed;
            else CSpeed += speedbandspeed;

            txtCSpeed.text = "" + (int)CSpeed;

        }
        txtDTG.text = Move.Instance.DME().ToString("F1");
        PFDScript.SpeedTapeUpdate();
        PFDScript.SpeedIndexUpdate_Click();

        int DeltaN1 = N1 - N1For(CAltitude, CVS, CSpeed);
        SpeedTime = (DeltaN1 == 0) ? 1 : (DeltaN1 > 0) ? 2000 / Mathf.Abs((float)DeltaN1) : 5380 / Mathf.Abs((float)DeltaN1);
        if (SpeedTime > 5) SpeedTime = 5;
        if (SpeedTime < 0.3) SpeedTime = 0.3f;
        DrawSpeedTrend(SpeedTime);

        Invoke("Speed_Equalize", SpeedTime / Session.Settings.SpeedMultiplier);
    }
    private void SetAttPitch(int P)
    {
        PFD_Animation PFDScript = FindObjectOfType<PFD_Animation>();
        PFDScript.AttUpdate((int)P);
    }


    /// <summary>Geometric vertical path slope used by VDI / TOD (~3° ⇒ 318 ft/NM).</summary>
    public const double VdiPathFtPerNm = 318.0;

    /// <summary>Altitude a VDI geometric path aims at for a regulated waypoint.</summary>
    public static double GetVdiConstraintAltitude(RoutePoint point)
    {
        if (point == null)
        {
            return -1;
        }

        var alt = point.Altitude;
        switch (point.AltitudeRegulation)
        {
            case RoutePoint.AltitudeFlags.Exact:
                return alt.RestrictionExact;
            case RoutePoint.AltitudeFlags.Below:
                return alt.RestrictionBelow;
            case RoutePoint.AltitudeFlags.Above:
                return alt.RestrictionAbove > 0 ? alt.RestrictionAbove : alt.ComputedValue;
            case RoutePoint.AltitudeFlags.AboveBelow:
                if (alt.RestrictionBelow > 0)
                {
                    return alt.RestrictionBelow;
                }

                if (alt.RestrictionAbove > 0)
                {
                    return alt.RestrictionAbove;
                }

                return alt.ComputedValue;
            default:
                return alt.ComputedValue;
        }
    }

    /// <summary>
    /// Next altitude anchor ahead of the aircraft and along-track distance to it (NM).
    /// Prefers real altitude restrictions; falls back to first ComputedValue &gt;= 0.
    /// </summary>
    public static bool TryGetNextVdiAnchor(RouteScriptableObject route, int passedNodeIndex,
        double distanceLeftOnSegment, out double constraintAltitude, out double distanceToConstraintNm)
    {
        constraintAltitude = -1;
        distanceToConstraintNm = 0;
        if (route?.Points == null || passedNodeIndex < 0 || passedNodeIndex >= route.Points.Length - 1)
        {
            return false;
        }

        if (TryFindAnchor(route, passedNodeIndex, distanceLeftOnSegment, requireRegulation: true,
                out constraintAltitude, out distanceToConstraintNm))
        {
            return true;
        }

        return TryFindAnchor(route, passedNodeIndex, distanceLeftOnSegment, requireRegulation: false,
            out constraintAltitude, out distanceToConstraintNm);
    }

    private static bool TryFindAnchor(RouteScriptableObject route, int passedNodeIndex,
        double distanceLeftOnSegment, bool requireRegulation,
        out double constraintAltitude, out double distanceToConstraintNm)
    {
        constraintAltitude = -1;
        distanceToConstraintNm = 0;
        var dist = distanceLeftOnSegment;

        for (var i = passedNodeIndex + 1; i < route.Points.Length; i++)
        {
            if (i > passedNodeIndex + 1)
            {
                dist += route.Points[i].Distance;
            }

            var point = route.Points[i];
            if (requireRegulation && point.AltitudeRegulation == RoutePoint.AltitudeFlags.NotSet)
            {
                continue;
            }

            var cAlt = requireRegulation
                ? GetVdiConstraintAltitude(point)
                : point.Altitude.ComputedValue;
            if (cAlt < 0)
            {
                continue;
            }

            constraintAltitude = cAlt;
            distanceToConstraintNm = dist;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Previous altitude regulation at or before passedNodeIndex, and along-track distance
    /// from that waypoint to the aircraft (NM).
    /// </summary>
    public static bool TryGetPreviousVdiAnchor(RouteScriptableObject route, int passedNodeIndex,
        double distanceLeftOnSegment, double segmentLength,
        out double prevAltitude, out double distFromPrevToAircraftNm)
    {
        prevAltitude = -1;
        distFromPrevToAircraftNm = 0;
        if (route?.Points == null || passedNodeIndex < 0 || passedNodeIndex >= route.Points.Length)
        {
            return false;
        }

        var walked = Math.Max(0.0, segmentLength - distanceLeftOnSegment);
        distFromPrevToAircraftNm = walked;

        for (var i = passedNodeIndex; i >= 0; i--)
        {
            if (i < passedNodeIndex)
            {
                distFromPrevToAircraftNm += route.Points[i + 1].Distance;
            }

            if (route.Points[i].AltitudeRegulation == RoutePoint.AltitudeFlags.NotSet)
            {
                continue;
            }

            var alt = GetVdiConstraintAltitude(route.Points[i]);
            if (alt < 0)
            {
                continue;
            }

            prevAltitude = alt;
            return true;
        }

        return false;
    }

    /// <summary>
    /// VNAV/VDI path at aircraft: geometric 3° into the next constraint from cruise (TOD),
    /// or a continuous connecting slope after a lower constraint was passed
    /// (avoids pathTarget jumping above the aircraft and killing descent).
    /// </summary>
    public static bool TryComputeVnavPath(RouteScriptableObject route, int passedNodeIndex,
        double distanceLeftOnSegment, double segmentLength, double startAltitude,
        out double pathTarget, out double slopeFtPerNm, out double nextConstraintAlt)
    {
        pathTarget = -1;
        slopeFtPerNm = VdiPathFtPerNm;
        nextConstraintAlt = -1;

        if (!TryGetNextVdiAnchor(route, passedNodeIndex, distanceLeftOnSegment,
                out nextConstraintAlt, out var distToNext))
        {
            return false;
        }

        var useConnecting = TryGetPreviousVdiAnchor(route, passedNodeIndex, distanceLeftOnSegment, segmentLength,
                                  out var prevAlt, out var distFromPrev)
                              && prevAlt < startAltitude - 100.0
                              && prevAlt > nextConstraintAlt;

        if (useConnecting)
        {
            var totalPrevToNext = distFromPrev + distToNext;
            if (totalPrevToNext < 1e-4)
            {
                pathTarget = nextConstraintAlt;
                slopeFtPerNm = 0;
                return true;
            }

            slopeFtPerNm = Math.Min(VdiPathFtPerNm, (prevAlt - nextConstraintAlt) / totalPrevToNext);
            pathTarget = nextConstraintAlt + slopeFtPerNm * distToNext;
            return true;
        }

        // Cruise → first descent constraint: fixed ~3° geometric path (TOD / early hold).
        slopeFtPerNm = VdiPathFtPerNm;
        pathTarget = nextConstraintAlt + VdiPathFtPerNm * distToNext;
        return true;
    }

    /// <summary>
    /// VDI path target: geometric 3° into next constraint from cruise, or continuous
    /// constraint-to-constraint slope after a descent constraint was passed.
    /// Falls back to segment linear.
    /// </summary>
    public static double ComputeVdiTarget(RouteScriptableObject route, int passedNodeIndex,
        double distanceLeftOnSegment, double alt0, double alt1, double segmentLength, double startAltitude)
    {
        if (TryComputeVnavPath(route, passedNodeIndex, distanceLeftOnSegment, segmentLength, startAltitude,
                out var pathTarget, out _, out _))
        {
            return pathTarget;
        }

        if (alt0 == -1)
        {
            alt0 = startAltitude;
        }

        if (alt1 == -1)
        {
            alt1 = alt0;
        }

        if (segmentLength <= 0)
        {
            return alt1;
        }

        return alt1 + (distanceLeftOnSegment * (alt0 - alt1)) / segmentLength;
    }

    /// <summary>
    /// First along-route distance/position where VDI is centered at cruise on the geometric path:
    /// before = below path, after = above path when staying level.
    /// d_to_constraint = (cruise - constraintAlt) / 318.
    /// segmentIndex is the destination node index (Points[i+1] / ComputedLines[i+1]).
    /// </summary>
    public static bool TryFindVdiCenterCrossing(RouteScriptableObject route, double cruiseAltitude,
        out Vector2 onTrackPosition, out int segmentIndex, out float segmentT)
    {
        onTrackPosition = default;
        segmentIndex = -1;
        segmentT = 0f;
        if (route?.Points == null || route.Points.Length < 2 || cruiseAltitude <= 0)
        {
            return false;
        }

        double distFromStart = 0;
        var anchorIndex = -1;
        double constraintAlt = -1;

        for (var i = 1; i < route.Points.Length; i++)
        {
            distFromStart += route.Points[i].Distance;
            if (route.Points[i].AltitudeRegulation == RoutePoint.AltitudeFlags.NotSet)
            {
                continue;
            }

            constraintAlt = GetVdiConstraintAltitude(route.Points[i]);
            if (constraintAlt < 0)
            {
                continue;
            }

            anchorIndex = i;
            break;
        }

        if (anchorIndex < 0)
        {
            distFromStart = 0;
            for (var i = 1; i < route.Points.Length; i++)
            {
                distFromStart += route.Points[i].Distance;
                if (route.Points[i].Altitude.ComputedValue < 0)
                {
                    continue;
                }

                constraintAlt = route.Points[i].Altitude.ComputedValue;
                anchorIndex = i;
                break;
            }
        }

        if (anchorIndex < 0 || constraintAlt < 0 || distFromStart <= 0.0001)
        {
            return false;
        }

        var dToConstraintAtTod = (cruiseAltitude - constraintAlt) / VdiPathFtPerNm;
        if (dToConstraintAtTod < -0.05 || dToConstraintAtTod > distFromStart + 0.05)
        {
            return false;
        }

        dToConstraintAtTod = Math.Clamp(dToConstraintAtTod, 0, distFromStart);
        var distAlongFromStart = distFromStart - dToConstraintAtTod;

        double walked = 0;
        for (var i = 0; i < route.Points.Length - 1; i++)
        {
            var node0 = route.Points[i];
            var node1 = route.Points[i + 1];
            var segmentLength = node1.Distance;
            if (segmentLength <= 0.0001)
            {
                continue;
            }

            if (walked + segmentLength < distAlongFromStart - 0.0001)
            {
                walked += segmentLength;
                continue;
            }

            segmentT = Mathf.Clamp01((float)((distAlongFromStart - walked) / segmentLength));
            segmentIndex = i + 1;
            onTrackPosition = Vector2.Lerp(node0.CartesianPosition, node1.CartesianPosition, segmentT);
            return true;
        }

        return false;
    }

    public void DrawVDI()
    {
        double DeltaAlt, Alt1, Alt0, d, D;
        float posY;

        if (Session.State.LOCCaptured && Move.Instance != null)
        {
            DeltaAlt = Move.Instance.GsAltitudeDeviation(Session.CurrentLevel.levelInfo.GlideSlope);
            VDI_Text.text = (Mathf.Abs((float)DeltaAlt) >= 50) ? "" + (int)DeltaAlt : "";
            posY = -((float)DeltaAlt / 5);
            if (posY > 100) posY = 100;
            if (posY < -100) posY = -100;
            VDI_Index.transform.localPosition = new Vector2(0.3f, posY / 125);
            if (DeltaAlt < 0) VDI_Text.transform.localPosition = new Vector2(0.1f, -1);
            else VDI_Text.transform.localPosition = new Vector2(0.1f, 1);
            return;
        }

        RouteScriptableObject activePoints = Session.ActiveRoute;
        RouteScriptableObject modPoints = Session.ModRoute;

        bool isMod = Session.IsMod;
        RouteScriptableObject _route = isMod ? modPoints : activePoints;
        if (_route == null) _route = activePoints;
        if (PositionVirtualNode.PassedNodeIndex < 0) return;
        if (PositionVirtualNode.PassedNodeIndex >= _route.Points.Length - 1) return;
        var node0 = _route.Points[PositionVirtualNode.PassedNodeIndex];
        var node1 = _route.Points[PositionVirtualNode.PassedNodeIndex + 1];

        Alt1 = (double)(node1.Altitude.ComputedValue);
        d = Session.PlayerAircraft.ComputedDistanceLeftOnSegment;
        D = node1.Distance;

        Alt0 = (double)(node0.Altitude.ComputedValue);
        double Target = ComputeVdiTarget(_route, PositionVirtualNode.PassedNodeIndex, d, Alt0, Alt1, D, StartAltitude);

        if (D == 0) DeltaAlt = 0;
        else DeltaAlt = CAltitude - Target;

        VDI_Text.text = (Mathf.Abs((float)DeltaAlt) >= 50) ? "" + (int)DeltaAlt : "";

        posY = -((float)DeltaAlt / 5);
        if (posY > 100) posY = 100;
        if (posY < -100) posY = -100;

        VDI_Index.transform.localPosition = new Vector2(0.3f, posY / 125);
        if (DeltaAlt < 0) VDI_Text.transform.localPosition = new Vector2(0.1f, -1);
        else VDI_Text.transform.localPosition = new Vector2(0.1f, 1);
    }
    private int ApplyVnavPointSpeedLimit(int targetSpeed)
    {
        // Level test: pilot sets IAS manually; do not clamp to route RawSpeed.
        if (LevelTestMode.IsActive)
            return targetSpeed;

        if (!Session.State.VNAV || Session.ActiveRoute?.Points == null || Session.ActiveRoute.Points.Length == 0)
        {
            return targetSpeed;
        }

        var pointIndex = Mathf.Clamp(PositionVirtualNode.NextNodeIndex, 0, Session.ActiveRoute.Points.Length - 1);
        var pointSpeed = Session.ActiveRoute.Points[pointIndex].RawSpeed;

        pointSpeed = (prvIndex4Speed != pointIndex) ? pointSpeed : 0;
        prvIndex4Speed = pointIndex;

        return pointSpeed > 0 ? Mathf.Min(targetSpeed, pointSpeed) : targetSpeed;



    }
    public void FlyVerticalPath()
    {
        if (Session.State.GSCaptured)
        {
            FlyGlideSlope();
            return;
        }

        RouteScriptableObject route = Session.ActiveRoute;
        if (route?.Points == null || PositionVirtualNode.PassedNodeIndex < 0)
            return;
        if (PositionVirtualNode.PassedNodeIndex >= route.Points.Length - 1)
            return;

        var node0 = route.Points[PositionVirtualNode.PassedNodeIndex];
        var node1 = route.Points[PositionVirtualNode.PassedNodeIndex + 1];

        double alt0 = node0.Altitude.ComputedValue;
        if (alt0 < 0) alt0 = StartAltitude;
        double alt1 = node1.Altitude.ComputedValue;
        if (alt1 < 0) alt1 = alt0;

        double d = Session.PlayerAircraft.ComputedDistanceLeftOnSegment;
        double D = node1.Distance;
        if (d < 0.1) d = 0.1;

        if (!Session.State.VNAV)
            return;

        double pathTarget;
        double slopeFtPerNm = VdiPathFtPerNm;
        double nextConstraintAlt = -1;
        if (!TryComputeVnavPath(route, PositionVirtualNode.PassedNodeIndex, d, D, StartAltitude,
                out pathTarget, out slopeFtPerNm, out nextConstraintAlt))
        {
            pathTarget = ComputeVdiTarget(route, PositionVirtualNode.PassedNodeIndex, d, alt0, alt1, D, StartAltitude);
        }

        bool mcpDescentWindow = RAltitude < (int)CAltitude;
        double deviation = CAltitude - pathTarget; // + above path, - below

        // Early TOD hold only when well below path. Near-path undershoot uses VS correction instead.
        const double belowPathHoldBand = 300;
        if (mcpDescentWindow && deviation < -belowPathHoldBand)
        {
            VNAV_VS = 0;
            RVS = 0;
            return;
        }

        // No open descent window → do not command vertical speed (MCP is altitude floor/target).
        if (!mcpDescentWindow)
        {
            VNAV_VS = 0;
            RVS = 0;
            return;
        }

        // Path slope + strong vertical capture (close ~1000 ft error in ~20 s).
        double baseVS = -slopeFtPerNm * GS / 60.0;
        const double captureTauSec = 20.0;
        double correction = deviation * (60.0 / captureTauSec);
        correction = Math.Clamp(correction, -1500.0, 3500.0);
        VNAV_VS = baseVS - correction;

        // Soft capture into the higher of MCP and the next FMC constraint.
        const double flareBand = 400;
        double floorAlt = RAltitude;
        if (nextConstraintAlt > 0)
            floorAlt = Math.Max(floorAlt, nextConstraintAlt);
        double altAboveFloor = CAltitude - floorAlt;
        if (altAboveFloor < flareBand && VNAV_VS < 0)
        {
            double t = Math.Clamp(Math.Max(altAboveFloor, 0) / flareBand, 0, 1);
            VNAV_VS *= t * t;
        }

        // Path first; while decelerating keep at least 300 fpm down — but not through the floor.
        bool decelerating = CSpeed > RSpeed + 5;
        if (decelerating && altAboveFloor > 100)
            VNAV_VS = Math.Min(VNAV_VS, -300.0);

        // Hold at next AT/below constraint until that waypoint is passed (then path retargets).
        if (nextConstraintAlt > 0 && CAltitude <= nextConstraintAlt + 10 && RAltitude < (int)nextConstraintAlt)
        {
            VNAV_VS = 0;
            RVS = 0;
            return;
        }

        // Allow brief positive VS only to recapture from a shallow undershoot; otherwise descent-only.
        double maxVs = deviation < 0 ? 500.0 : 0.0;
        VNAV_VS = Mathf.Clamp((float)VNAV_VS, -4000, (float)maxVs);
        RVS = (int)VNAV_VS;

        if (RVS < 0)
            ClearFmcCruiseAltitudeOnVnavDescentStart();
    }

    /// <summary>
    /// Soft GS track after capture: geometric slope + mild altitude correction,
    /// deadband and RVS rate-limit to avoid hunting.
    /// </summary>
    private void FlyGlideSlope()
    {
        float glide = Session.CurrentLevel.levelInfo.GlideSlope;
        float geometricVs = -6076f * Mathf.Tan(glide * Mathf.Deg2Rad) * (GS / 60f);

        float altErr = Move.Instance != null
            ? Move.Instance.GsAltitudeDeviation(glide)
            : 0f; // + above path

        // Ignore small errors; correct the rest slowly (~45 s) with a tight fpm cap.
        const float deadbandFt = 60f;
        const float tauSec = 45f;
        const float maxCorrectionFpm = 180f;
        float err = 0f;
        if (Mathf.Abs(altErr) > deadbandFt)
            err = altErr - Mathf.Sign(altErr) * deadbandFt;

        float correction = Mathf.Clamp(err * (60f / tauSec), -maxCorrectionFpm, maxCorrectionFpm);
        float desired = geometricVs - correction;

        // 1 Hz path loop: limit how fast commanded VS can change.
        const float maxDeltaFpmPerSec = 60f;
        float next = Mathf.MoveTowards(RVS, desired, maxDeltaFpmPerSec);
        VNAV_VS = next;
        RVS = Mathf.RoundToInt(next);
    }

    /// <summary>
    /// FMC CRZ altitude only — cleared when VNAV descent begins (page shows 00000).
    /// Does not clear <see cref="StartAltitude"/> used by VNAV path math.
    /// </summary>
    public void ClearFmcCruiseAltitudeOnVnavDescentStart()
    {
        if (_fmcCruiseClearedOnVnavDescent)
            return;

        _fmcCruiseClearedOnVnavDescent = true;
        if (Session.CurrentLevel?.levelInfo != null)
            Session.CurrentLevel.levelInfo.CrzAltitude = 0;

        if (infoFMC.Instance?.Fmc?.Crz != null)
            infoFMC.Instance.Fmc.Crz.Altitude = FormatFmcCruiseAltitude(0);

        var drawer = FindFirstObjectByType<Drawer>();
        if (drawer != null)
        {
            drawer.Clear();
            drawer.Display();
        }
    }

    /// <summary>
    /// New FMC cruise altitude (feet). Updates TOD source and VNAV cruise reference, redraws route.
    /// </summary>
    public void ApplyFmcCruiseAltitude(int altitudeFeet)
    {
        altitudeFeet = Mathf.Clamp(altitudeFeet, 0, 41000);
        StartAltitude = altitudeFeet;
        _fmcCruiseClearedOnVnavDescent = altitudeFeet <= 0;

        if (Session.CurrentLevel?.levelInfo != null)
            Session.CurrentLevel.levelInfo.CrzAltitude = altitudeFeet;

        if (infoFMC.Instance?.Fmc?.Crz != null)
            infoFMC.Instance.Fmc.Crz.Altitude = FormatFmcCruiseAltitude(altitudeFeet);

        var drawer = FindFirstObjectByType<Drawer>();
        if (drawer != null)
        {
            drawer.Clear();
            drawer.Display();
        }
    }

    /// <summary>
    /// QNH is fixed at 1013 (STD above this), so FL and altitude feet are interchangeable:
    /// FL = feet / 100. At/above transition show FLxxx; below show feet.
    /// </summary>
    public const int TransitionAltitudeFeet = 10000;

    /// <summary>
    /// Cruise-style entry: FL shorthand 10–410 → feet, or feet 1000–41000. Invalid → 0.
    /// </summary>
    public static int NormalizeAltitudeEntryToFeet(int entered)
    {
        if (entered >= 10 && entered <= 410)
            return entered * 100;
        if (entered >= 1000 && entered <= 41000)
            return entered;
        return 0;
    }

    /// <summary>
    /// Legs regulation number: FL shorthand 10–410 → feet; otherwise keep as feet (allows 700, etc.).
    /// </summary>
    public static int NormalizeAltitudeRegulationNumberToFeet(int entered)
    {
        if (entered >= 10 && entered <= 410)
            return entered * 100;
        if (entered >= 1 && entered <= 41000)
            return entered;
        return 0;
    }

    /// <summary>
    /// Normalize a Legs/DES altitude regulation string to feet storage
    /// (e.g. "350"→"35000", "350A"→"35000A", "7000B" unchanged).
    /// </summary>
    public static string NormalizeAltitudeRegulationToFeet(string regulation)
    {
        if (string.IsNullOrEmpty(regulation))
            return regulation;

        return Regex.Replace(regulation, @"(\d+)([AaBb]?)", match =>
        {
            var number = int.Parse(match.Groups[1].Value);
            var suffix = match.Groups[2].Value.ToUpperInvariant();
            var feet = NormalizeAltitudeRegulationNumberToFeet(number);
            if (feet <= 0)
                return match.Value;
            return feet + suffix;
        });
    }

    /// <summary>FMS altitude display. Cleared cruise stays "00000".</summary>
    public static string FormatFmcCruiseAltitude(long altitudeFeet)
    {
        return altitudeFeet <= 0 ? "00000" : FormatFmcAltitude(altitudeFeet);
    }

    /// <summary>Format feet as FLxxx at/above transition, else plain feet. QNH 1013 → FL ≡ altitude/100.</summary>
    public static string FormatFmcAltitude(long altitudeFeet)
    {
        if (altitudeFeet <= 0)
            return "-----";
        if (altitudeFeet >= TransitionAltitudeFeet)
            return $"FL{altitudeFeet / 100}";
        return altitudeFeet.ToString();
    }

    /// <summary>
    /// Format entered cruise value for pending display, or null if invalid.
    /// </summary>
    public static string FormatFmcAltitudeFromEntry(int? entered)
    {
        if (entered == null)
            return null;
        var feet = NormalizeAltitudeEntryToFeet(entered.Value);
        return feet > 0 ? FormatFmcAltitude(feet) : null;
    }

    /// <summary>
    /// Format raw/computed altitude for FMS UI including A/B restrictions
    /// (e.g. "20000"→"FL200", "15000B"→"FL150B", "7000A"→"7000A").
    /// </summary>
    public static string FormatFmcAltitudeDisplay(string rawOrFeet)
    {
        if (string.IsNullOrEmpty(rawOrFeet))
            return rawOrFeet;

        if (long.TryParse(rawOrFeet, out var plainFeet))
            return FormatFmcAltitude(plainFeet);

        var sb = new StringBuilder();
        foreach (Match match in Regex.Matches(rawOrFeet, @"(\d+)([AaBb]?)"))
        {
            var number = long.Parse(match.Groups[1].Value);
            var suffix = match.Groups[2].Value.ToUpperInvariant();
            if (number >= TransitionAltitudeFeet)
                sb.Append($"FL{number / 100}{suffix}");
            else
                sb.Append($"{number}{suffix}");
        }

        return sb.Length > 0 ? sb.ToString() : rawOrFeet;
    }

    public void SetFMA()
    {

        if (Session.State.HDG) FMA2.text = "HDG";
        if (Session.State.LNAV) FMA2.text = "LNAV";
        if (Session.State.LNAVArmed) FMAarmed.text = "LNAV";


        if (!Session.State.GSCaptured)
        {
            if (Session.State.LC)
            {
                txtRVS.text = "";
                FMA1.text = "IDLE";
                FMA3.text = "MCP SPD";
            }
            if (Session.State.AH)
            {
                txtRVS.text = "";
                FMA1.text = "MCP SPD";
                FMA3.text = "ALT";
            }
            if (Session.State.VS)
            {
                FMA1.text = "MCP SPD";
                FMA3.text = "VS";
            }
            if (Session.State.VNAV)
            {
                txtRVS.text = "";
                FMA1.text = "FMC SPD";
                FMA3.text = "VNAV";
            }
        }

        // Approach modes own lateral/vertical FMA once armed or captured.
        if (Session.State.AppArmed || Session.State.LOCCaptured)
        {
            if (Session.State.LOCCaptured)
            {
                FMA2.text = "LOC";

                if (Session.State.GSCaptured
                    || (Move.Instance != null
                        && Move.Instance.CanCaptureGs(Session.CurrentLevel.levelInfo.GlideSlope)))
                {
                    Session.State.GSCaptured = true;
                    FMA1.text = "MCP SPD";
                    FMA3.text = "GS";
                    FMAarmed.text = "";
                }
                else
                {
                    FMAarmed.text = "                         GS";
                }
            }
            else
            {
                FMAarmed.text = "LOC                 GS";
            }
        }

    }
    private void MatchAltitudes()
    {
        PFD_Animation PFDScript = FindObjectOfType<PFD_Animation>();
        PFDScript.AltUpdate((int)CAltitude);
        PFDScript.CheckAltitudeIndicator(RAltitude, (int)CAltitude);
        if (CAltitude >= TransitionAltitudeFeet) Qnh.text = "STD";
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
        Altitude = Mathf.Clamp((float)Altitude, 0f, 39900f);

        int F = Mathf.Clamp(8 - Mathf.FloorToInt((float)Altitude / 5000), 1, Pressure.Length - 1);
        float p = Mathf.Lerp(Pressure[F], Pressure[F - 1], (float)(Altitude % 5000) / 5000);

        return Mathf.Sqrt(Mathf.Pow(1 / p * (Mathf.Pow((float)Speed * (float)Speed / 2187771 + 1, 3.5f) - 1) + 1, 0.2857f) - 1) * Mathf.Sqrt(5);
    }

    /// <summary>CAS (kt) from Mach — inverse of Speed2Mach (pressure ratio only; no TAS path).</summary>
    public static double Mach2Speed(double Mach, double Altitude)
    {
        Altitude = Mathf.Clamp((float)Altitude, 0f, 39900f);
        int F = Mathf.Clamp(8 - Mathf.FloorToInt((float)Altitude / 5000), 1, Pressure.Length - 1);
        float p = Mathf.Lerp(Pressure[F], Pressure[F - 1], (float)(Altitude % 5000) / 5000);
        return Mathf.Sqrt(Mathf.Pow(p * (Mathf.Pow(1f + 0.2f * (float)(Mach * Mach), 3.5f) - 1) + 1, 0.2857f) - 1) * Mathf.Sqrt(5) * 661.4787f;
    }

    /// <summary>
    /// Pressure altitude (ft) where CAS and Mach share the same TAS (ISA troposphere).
    /// </summary>
    public static double CrossOverAltitude(double Speed, double Mach)
    {
        float ro = (Mathf.Pow(1 + 0.2f * Mathf.Pow((float)Speed / 661.48f, 2), 3.5f) - 1) / (Mathf.Pow((float)(1 + 0.2 * Mach * Mach), 3.5f) - 1);
        return Mathf.Floor(145442.16f * (1 - Mathf.Pow(ro, 0.1902631f)));
    }

    /// <summary>
    /// Schedule CO altitude from level DesEcon IAS/Mach (fallback 280/.78 ≈ FL325).
    /// </summary>
    public static double GetCoAltitudeFeet()
    {
        var levelInfo = Session.CurrentLevel != null ? Session.CurrentLevel.levelInfo : null;
        if (levelInfo == null || levelInfo.DesEconSpeed <= 0 || levelInfo.DesEconMach <= 0)
            return CrossOverAltitude(280, 0.78);

        return CrossOverAltitude(levelInfo.DesEconSpeed, levelInfo.DesEconMach / 100.0);
    }

    /// <summary>Formats Mach as .78 (digits=2) or .780 (digits=3).</summary>
    public static string FormatMach(double mach, int digits = 2)
    {
        int scale = digits == 3 ? 1000 : 100;
        int value = (int)System.Math.Round(mach * scale);
        return "." + value.ToString(digits == 3 ? "D3" : "D2");
    }

    /// <summary>Formats FMC Mach/IAS pair from level data (e.g. machHundredths=79, ias=270 → .79/270).</summary>
    public static string FormatMachIas(int machHundredths, int ias)
    {
        return "." + machHundredths.ToString("D2") + "/" + ias;
    }

    /// <summary>
    /// FMS display only: IAS stays in data; at/above schedule CO altitude show Mach.
    /// </summary>
    public static string FormatFmcSpeedDisplay(int ias, double altitudeFeet, int machDigits = 2)
    {
        if (ias <= 0)
            return ias.ToString();
        if (altitudeFeet >= GetCoAltitudeFeet())
            return FormatMach(Speed2Mach(ias, altitudeFeet), machDigits);
        return ias.ToString();
    }

    private void FuelandMach()
    {
        totalFuel -= (double)FF * 2 / 3600 * Session.Settings.SpeedMultiplier;                                                     //Fuel
        txtTotalFuel.text = "" + System.Math.Round(totalFuel / 100, 2);

        CMach = Speed2Mach(CSpeed, CAltitude);
        txtMach.text = CMach > 0.4 ? FormatMach(CMach) : "GS " + GS;

        double coAlt = GetCoAltitudeFeet();
        if (co.isOn)
        {
            RSpeed = (int)Mach2Speed(RMach, CAltitude);
            if (CAltitude < coAlt) co.isOn = false;
        }
        if (CAltitude < coAlt) co.enabled = false; else co.enabled = true;

    }
    public void FUP_Click()
    {
        if (CAltitude < 20000 && Flap_Idx < MaxFlapIdx)
        {
            Flap_Idx += 1;
            SetFlaps();

            MMVibrationManager.Haptic(HapticTypes.MediumImpact);
        }
    }
    public void FDown_Click()
    {
        if (CAltitude < 20000)
        {
            Flap_Idx -= 1;
            if (Flap_Idx < 0) Flap_Idx = 0;
            SetFlaps();

            MMVibrationManager.Haptic(HapticTypes.MediumImpact);
        }
    }
    public void SetFlaps()
    {
        int[] Fps = new int[9] { -179, -145, -103, -64, -35, -3, 29, 57, 86 };
        int[] FlapMinSpeeds = new int[9] { 206, 187, 187, 167, 167, 157, 145, 145, 145 };

        int i, j, k;
        for (i = 0; i < 5; i++)
            for (j = 0; j < 4; j++)
                for (k = 0; k < 4; k++)
                {
                    M[i + 4, j, k] = Mf[Flap_Idx, i, j, k];
                }



        if (FlapNeedle != null)
        {
            FlapNeedle.transform.localEulerAngles = new Vector3(-90, 180, Fps[Flap_Idx]);
        }
        else
        {
            Debug.LogError("Assign flapNeedle");
        }

        PFD_Animation PFDScript = FindObjectOfType<PFD_Animation>();
        PFDScript.Flaps_Indexchange(Flap_Idx);
        minimumSpeed = FlapMinSpeeds[Flap_Idx];
    }
    public void SetLG(bool down, bool fromUI)
    {

        if (LGDown == down) return;

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
        if (fromUI) LGDown = down;

        if (down)

        {


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
    public void SetSB(bool Up, bool fromUI)
    {
        if (SBUp == !Up) return;

        double[,] Msb = new double[9, 2] { { -900, -600 }, { -900, -600 }, { -900, -500 }, { -900, -500 }, { -900, -400 }, { -900, -400 }, { -900, -400 }, { -900, -500 }, { -900, -400 } };

        int i;
        if (fromUI) SBUp = !Up;
        if (!Up)
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
    public static int NormalizeHeading360(int heading)
    {
        heading %= 360;
        if (heading < 0)
        {
            heading += 360;
        }

        return heading;
    }

    public void XFR1_Click()
    {
        RHeading = NormalizeHeading360(Move.XFRHdg);
        AddWindEffectToRHeading();
        UYServiceLocator.Get<McpUI>().RefreshHS();
        if (Move.Instance != null)
            Move.Instance.AcknowledgeAtc1();
    }
    public void XFR2_Click()
    {
        RAltitude = (int)Move.XFRAltitude;
        txtRAltitude.text = RAltitude.ToString();
        txtRAltitude_overTape.text = txtRAltitude.text;
        if (Move.Instance != null)
            Move.Instance.AcknowledgeAtc2();
    }
    public void XFR3_Click()
    {
        RSpeed = Move.XFRSpeed;
        if (co.isOn)
        {
            RMach = Speed2Mach(RSpeed, CAltitude);
            txtRSpeed.text = FormatMach(RMach);
        }
        else
        {
            txtRSpeed.text = RSpeed.ToString();
        }
        txtRSpeed_overTape.text = txtRSpeed.text;
        if (Move.Instance != null)
            Move.Instance.AcknowledgeAtc3();
    }

    public static void Check_LimitSpeed()
    {
        if (Calculator.Instance.co.isOn)                                    //Mach
        {
            if (Calculator.Instance.RMach < 0.6) Calculator.Instance.RMach = 0.6;
            if (Calculator.Instance.RMach > Speed2Mach(PFD_Animation.LimitSpeed, CAltitude)) Calculator.Instance.RMach = Speed2Mach(PFD_Animation.LimitSpeed, CAltitude);
            RSpeed = (int)Mach2Speed(Calculator.Instance.RMach, CAltitude);
            Calculator.Instance.txtRSpeed.text = FormatMach(Calculator.Instance.RMach);
        }
        else
        {                                               //IAS 
            if (RSpeed < minimumSpeed) RSpeed = minimumSpeed;
            if (RSpeed > PFD_Animation.LimitSpeed) RSpeed = PFD_Animation.LimitSpeed;
            Calculator.Instance.txtRSpeed.text = "" + RSpeed;
        }
        Calculator.Instance.txtRSpeed_overTape.text = Calculator.Instance.txtRSpeed.text;
    }

    public void UpdatePFD()
    {

        PFD_Animation pfdAnimation = FindObjectOfType<PFD_Animation>();
        pfdAnimation.SpeedIndexUpdate_Click();
        pfdAnimation.CheckAltitudeIndicator(RAltitude, (int)CAltitude);
    }

    public void OnClick_HDG(bool positive)
    {
        if (positive) //Heading    
        {
            RHeading += 1;
            if (RHeading > 359) RHeading = 0;
            if (Mathf.Abs(Mathf.DeltaAngle(RHeading, Move.Perpend)) < 90 - Move.teta)
            {
                Time.timeScale = 1;
            }
        }
        else
        {
            RHeading -= 1;
            if (RHeading < 0) RHeading = 359;

            if (Mathf.Abs(Mathf.DeltaAngle(RHeading, Move.Perpend)) < 90 - Move.teta)
            {
                Time.timeScale = 1;
            }
        }

        AddWindEffectToRHeading();

        UpdatePFD();
    }

    public void AddWindEffectToRHeading()
    {
        WindElements we = CalculateWindElements(CAltitude, CSpeed, RHeading);
        HeadingWindAddition = we.HeadingWindAddition;
        RTrack = NormalizeHeading360(RHeading - we.HeadingWindAddition);

        UYServiceLocator.Get<McpUI>().RefreshHS();
    }

    /// <summary>
    /// Snap MCP selected heading to current magnetic heading so HDG after LNAV/LOC continues straight.
    /// </summary>
    public void SyncRHeadingToCurrent()
    {
        if (Session.PlayerAircraft == null)
            return;

        int currentTrack = NormalizeHeading360(Mathf.RoundToInt(Session.PlayerAircraft.DisplayHeadingDegrees));
        WindElements we = CalculateWindElements(CAltitude, CSpeed, currentTrack);
        int currentHeading = NormalizeHeading360(currentTrack + we.HeadingWindAddition);
        if (RHeading == currentHeading)
            return;

        RHeading = currentHeading;
        AddWindEffectToRHeading();
    }
    public void AddWindEffectToCHeading()
    {
        WindElements we = CalculateWindElements(CAltitude, CSpeed, CHeading);
        CHeadingWindAddition = we.HeadingWindAddition;
    }
    private object Distancefromroute()
    {
        throw new NotImplementedException();
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
        if (RAltitude > 40000) RAltitude = 40000;
        txtRAltitude.text = "" + RAltitude;
        txtRAltitude_overTape.text = txtRAltitude.text;
    }

    public void OnClick_V(bool positive)
    {
        int value;
        if (int.TryParse(txtRVS.text, out value))
        {
            RVS = value;
        }
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
        if (Session.State.VS)
        {
            RVS = CVS / 100 * 100;
            txtRVS.text = "" + RVS;
        }
        if (Session.State.AH)
        {
            RVS = 0;
        }

    }


    public void co_Change()
    {
        double[,] MVS = new double[5, 2] { { -800, -280 }, { -680, -520 }, { -500, -400 }, { -430, -140 }, { -420, -240 } };
        if (co.isOn) // on mach
        {
            RMach = Speed2Mach(RSpeed, CAltitude);
            txtRSpeed.text = FormatMach(RMach);
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
        WindElements we = CalculateWindElements(CAltitude, CSpeed, CTrack);
        windArrow.transform.localEulerAngles = new Vector3(0, 0, 180 - we.relativeWindD);
        windTxt.text = "GS" + we.GS + "   TAS" + we.TAS + "\n" + we.WindD + "° / " + we.WindM;
        CWind = we.WindD + "° / " + we.WindM;
        GS = we.GS;
        CHeading = NormalizeHeading360(CTrack + we.HeadingWindAddition);
        CHeadingWindAddition = we.HeadingWindAddition;
    }

    public class WindElements
    {
        public int GS, HeadingWindAddition, relativeWindD, WindM, WindD, TAS;
    }

    private static int GetWindDirection(double altitude)
    {
        altitude = Math.Max(0, altitude);

        int maxIndex = CurrentWindTable.WindInfoItems.Length - 1;

        int baseAlt = maxIndex - (int)(altitude / 5000.0);
        baseAlt = Mathf.Clamp(baseAlt, 1, maxIndex);

        double frac = (altitude % 5000.0) / 5000.0;

        float deg1 = Mathf.Repeat((float)CurrentWindTable.WindInfoItems[baseAlt].Degrees, 360f);
        float deg2 = Mathf.Repeat((float)CurrentWindTable.WindInfoItems[baseAlt - 1].Degrees, 360f);

        float result = Mathf.LerpAngle(deg1, deg2, (float)frac);

        return (int)result;
    }
    public static int GetWindMagnitude(double altitude)
    {
        altitude = Math.Max(0, altitude);

        int maxIndex = CurrentWindTable.WindInfoItems.Length - 1;

        int baseAlt = maxIndex - (int)(altitude / 5000.0);
        baseAlt = Mathf.Clamp(baseAlt, 1, maxIndex);

        double frac = (altitude % 5000.0) / 5000.0;

        float kts1 = CurrentWindTable.WindInfoItems[baseAlt].Knots;
        float kts2 = CurrentWindTable.WindInfoItems[baseAlt - 1].Knots;

        float result = Mathf.Lerp(kts1, kts2, (float)frac);

        result = Mathf.Max(0, result);

        return (int)result;
    }
    public static WindElements CalculateWindElements(double Altitude, double IAS, int Track)
    {
        if (Altitude > 39900) Altitude = 39900;

        WindElements WE = new WindElements();

        int BaseAlt = 8 - (int)System.Math.Truncate(Altitude / 5000);

        WE.WindD = GetWindDirection(Altitude);
        WE.WindM = GetWindMagnitude(Altitude);

        WE.relativeWindD = WE.WindD - Track;

        double HeadWind = Mathf.Cos(WE.relativeWindD * Mathf.Deg2Rad) * WE.WindM;
        double CrossWind = Mathf.Sin(WE.relativeWindD * Mathf.Deg2Rad) * WE.WindM;


        WE.TAS = (int)(IAS + (Altitude / 1000 * 0.02 * IAS));

        WE.GS = (int)(WE.TAS - HeadWind);
        WE.HeadingWindAddition = (int)(Mathf.Atan((float)(CrossWind / WE.GS)) * Mathf.Rad2Deg);


        return WE;
    }
    private bool _approachEndTriggered;

    public void CheckStabilization()
    {
        if (_approachEndTriggered)
            return;

        // Level test finishes at RW (DME), not via the normal short-final dialog.
        if (LevelTestMode.IsActive)
            return;

        float dme = Move.Instance.DME();
        if (dme >= 3f && CAltitude > 1000)
            return;

        _approachEndTriggered = true;

        var report = BuildStabilizationReport(out bool allOk);
        ApproachStatusDialog.Show(allOk ? "STABLE" : "NOT STABLE", report, "Exit", () =>
        {
            Aircraft aircraft = FindAnyObjectByType<Aircraft>();
            if (aircraft != null)
            {
                aircraft.FinishGame(isStable: allOk);
                CAltitude = 10000;
            }
        });
    }

    string BuildStabilizationReport(out bool allOk)
    {
        var levelInfo = Session.CurrentLevel?.levelInfo;
        float course = levelInfo != null ? levelInfo.Course : 0f;
        float glideSlope = levelInfo != null ? levelInfo.GlideSlope : 3f;

        float locDev = Move.Instance != null ? Move.Instance.ComputeLocDeviationDegrees(course) : 99f;
        float gsAltDev = Move.Instance != null ? Move.Instance.GsAltitudeDeviation(glideSlope) : 9999f;

        bool localizerOk = Session.State.LOCCaptured || Mathf.Abs(locDev) <= 0.5f;
        // ~0.5° at typical short final ≈ 150 ft band already used for capture; use 150 ft here too.
        bool glideSlopeOk = Session.State.GSCaptured || Mathf.Abs(gsAltDev) <= Move.GsCaptureAltBandFt;
        // Typical short-final ROD band
        bool verticalSpeedOk = CVS <= -200 && CVS >= -1200;
        bool speedOk = CSpeed >= minimumSpeed && CSpeed <= minimumSpeed + 20;
        bool landingGearOk = LGDown;
        int flapSetting = GetFlapSettingDegrees(Flap_Idx);
        bool flapsOk = flapSetting >= 30;
        // On approach before touchdown, speed brake should be
        // 
        bool speedBrakeOk = !SBUp;

        allOk = localizerOk && glideSlopeOk && verticalSpeedOk && speedOk
                && landingGearOk && flapsOk && speedBrakeOk;

        string lf = Environment.NewLine;
        return
            FormatStatusLine(lf + "Localizer", OkNo(localizerOk), localizerOk) + lf +
            FormatStatusLine("Glide Slope", OkNo(glideSlopeOk), glideSlopeOk) + lf +
            FormatStatusLine("Vertical Speed", OkNo(verticalSpeedOk), verticalSpeedOk) + lf +
            FormatStatusLine("Speed", OkNo(speedOk), speedOk) + lf +
            FormatStatusLine("Landing Gear", landingGearOk ? "/Down" : "Up", landingGearOk) + lf +
            FormatStatusLine("Flaps", flapSetting + "°", flapsOk) + lf +
            FormatStatusLine("Speed Brake", speedBrakeOk ? "Armed" : "Extended", speedBrakeOk);
    }

    static string OkNo(bool ok) => ok ? "ok" : "no";

    static string FormatStatusLine(string label, string value, bool ok)
    {
        const int width = 20;
        string dots = label.Length >= width ? "" : new string('.', width - label.Length);
        string color = ok ? "#5CDB7A" : "#E85D5D";
        return $"<color={color}>{label}{dots}{value}</color>";
    }

    static int GetFlapSettingDegrees(int flapIdx)
    {
        // UP, 1, 5, 10, 15, 25, 30, 40, 40
        int[] settings = { 0, 1, 5, 10, 15, 25, 30, 40, 40 };
        if (flapIdx < 0 || flapIdx >= settings.Length)
            return 0;
        return settings[flapIdx];
    }

    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}
