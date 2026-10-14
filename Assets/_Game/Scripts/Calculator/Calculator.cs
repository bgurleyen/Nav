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
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Unyawn.Utils;

// GW :56.4,ZFW:45,Fuel:12,CI:0,CG:23.3

public class Calculator : MonoBehaviour
{

    public static int Level = 11;             // ***  Level

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
    // FMC target while VNAV speed window is closed. Pilot/ATC speed lives in RSpeed once the window is open.
    int _fmcScheduleSpeed;
    bool _speedWindowSynced;
    public bool SpeedWindowOpen { get; private set; }

    public static int CTrack, Track, RTrack;
    private double CMach, RMach, VNAV_VS;
    private bool _fmcCruiseClearedOnVnavDescent;
    private bool _resetMcpAltitudePosted;
    public static float TAS, GS;
    public Toggle co;//Landing Gear ,Speed Brake;
    // Single gear state. True = extended. Lever, drag, speed tape and stable check all read this.
    // Top-to-bottom slider: 0 is the raised detent (gear up), 1 is the lowered detent (gear down).
    public static bool LGDown = false;
    public static bool GearExtendedFromSlider(float value) => value >= 0.5f;
    public static float SliderValueForGear(bool extended) => extended ? 1f : 0f;
    public static bool SBUp = false;
    public Button FUP_Button, FDown_Button;
    private int increasedSpeed, excessSpeedCo = 0;
    int _captureLeadVs;
    public static int Flap_Idx = 0;
    private const int MaxFlapIdx = 8; // Mf[8]=f40, Fps[8]=86°
    private float SpeedTime;
    public Text windTxt;
    public static string CWind;
    public static int HeadingWindAddition, CHeadingWindAddition;
    public Text FMA1, FMA2, FMA3, FMAarmed;
    public Image windArrow, VSline, SpeedTrend;
    public GameObject Progres, FlapNeedle, LGlever;
    static readonly int[] FlapDetentZ = { -179, -145, -103, -64, -35, -3, 29, 57, 86 };
    private int N1, dispN1 = 77;
    public static int FF = 77;
    public static double dispFF = 270;
    // Chart reference is GW 56.4 t, ISA, packs on, anti-ice off, two engines.
    public static float IsaDeviationC;
    public static bool EngineAntiIce;
    public static bool PacksOn = true;
    public static int EnginesRunning = 2;
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

   
    static double[,,] M => PerformanceChart.M;
    static double[,,,] Mf => PerformanceChart.Flaps;

    private static float[] Pressure = new float[9]
    { 0.1852f, 0.2352f, 0.2968f, 0.3709f, 0.4594f, 0.5642f, 0.6856f, 0.8320f, 1f }; //40000 to 0

    // Engine anti-ice add, same altitude order. Larger in dense air.
    static readonly int[] AntiIceFf = { 12, 14, 16, 18, 22, 26, 30, 35, 40 };

    public static Calculator Instance;

    public float GetBananaPosition//Edit
    {
        get
        {
            // Level-off predictor only while descending toward a lower MCP altitude.
            bool levelFlight = CVS == 0;
            bool atOrBelowDescentAltitude = CAltitude <= RAltitude;
            bool gsCaptured = Session.State != null && Session.State.GSCaptured;
            if (levelFlight || atOrBelowDescentAltitude || gsCaptured)
                return 0;

            float oneNM = -Session.Zoom;
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
        totalFuel = 1000;
        if (txtTotalFuel != null)
            txtTotalFuel.text = "10";

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
        _fmcScheduleSpeed = RSpeed;
        RVS = CVS;
        _approachEndTriggered = false;
        _resetMcpAltitudePosted = false;
        txtRAltitude.text = "" + RAltitude;
        txtRAltitude_overTape.text = txtRAltitude.text;
        txtCAltitude.text = "" + (int)CAltitude;
        txtMeter.text = "" + (int)(CAltitude / 3.28084) + "M";
        txtCSpeed.text = "" + (int)CSpeed;
        txtCVS.text = "";
        FMAarmed.text = "";

        ApplyInitialSpeedDisplayForAltitude();
        BindSpeedWindowClick(txtRSpeed);
        BindSpeedWindowClick(txtRSpeed_overTape);

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
                RefreshSpeedWindow();
            }
        }

        SyncSpeedWindowToMode();
    }

    bool FmcOwnsSpeed()
    {
        return !LevelTestMode.IsActive
            && Session.State != null
            && Session.State.VNAV
            && !SpeedWindowOpen;
    }

    void SyncSpeedWindowToMode()
    {
        if (Session.State == null)
            return;

        _speedWindowSynced = true;
        if (LevelTestMode.IsActive || !Session.State.VNAV)
            ShowSpeedWindow();
        else
            CloseSpeedWindow();
    }

    void EnsureSpeedWindowSynced()
    {
        if (_speedWindowSynced)
            return;

        SyncSpeedWindowToMode();
    }

    public void OnVerticalModeSelected()
    {
        SyncSpeedWindowToMode();
    }

    public void ShowSpeedWindow()
    {
        SpeedWindowOpen = true;
        _speedWindowSynced = true;
        RefreshSpeedWindow();
        if (FMA1 != null)
            SetFMA();
    }

    void CloseSpeedWindow()
    {
        SpeedWindowOpen = false;
        _speedWindowSynced = true;
        ApplyFmcSpeed();
        RefreshSpeedWindow();
        if (FMA1 != null)
            SetFMA();
    }

    public void OnSpeedWindowClicked()
    {
        if (LevelTestMode.IsActive)
            return;
        if (Session.State == null || !Session.State.VNAV || !SpeedWindowOpen)
            return;

        CloseSpeedWindow();
    }

    void ApplyFmcSpeed()
    {
        if (LevelTestMode.IsActive || Session.State == null || !Session.State.VNAV)
            return;

        _fmcScheduleSpeed = ApplyVnavPointSpeedLimit(_fmcScheduleSpeed);
        int commanded = _fmcScheduleSpeed;
        if (commanded < minimumSpeed)
            commanded = minimumSpeed;
        if (PFD_Animation.LimitSpeed > 0 && commanded > PFD_Animation.LimitSpeed)
            commanded = PFD_Animation.LimitSpeed;

        RSpeed = commanded;
        if (co != null && co.isOn)
            RMach = Speed2Mach(RSpeed, CAltitude);
    }

    void RefreshSpeedWindow()
    {
        string text = "";
        if (!FmcOwnsSpeed())
        {
            if (co != null && co.isOn)
                text = FormatMach(RMach);
            else
                text = RSpeed.ToString();
        }

        if (txtRSpeed != null)
            txtRSpeed.text = text;
        if (txtRSpeed_overTape != null)
            txtRSpeed_overTape.text = text;
    }

    static void BindSpeedWindowClick(Text label)
    {
        if (label == null || label.GetComponent<SpeedWindowClickRelay>() != null)
            return;

        label.raycastTarget = true;
        label.gameObject.AddComponent<SpeedWindowClickRelay>();
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
            CheckResetMcpAltitudeScratchpad();
            InterpolateVS();
            InterpolateLvlChg();
            EnsureSpeedWindowSynced();
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
            {  // Path/VS steeper than idle: raise speed on the chart line, continued linearly past the steep corner.
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
            bool idleFuel = !pathWithinIdle || (RSpeed < CSpeed - 5 && pathWithinIdle);
            if (!idleFuel)
                a[2] = ApplyOperationalFf((int)a[2], Altitude);
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
            ApplyAltitudeCaptureFlare();

        }
    }

    /// <summary>
    /// ALT ACQ: bleed VS at about 1.5 ft/s² (~90 fpm each second), same shape as a jet level-off.
    /// 2000 fpm arrives in ~20 s. Stay at or above 200 fpm until the altitude is captured.
    /// </summary>
    const float CaptureAccelFtPerSec2 = 1.5f;
    const int CaptureMinVs = 200;

    static float CaptureLeadFeet(int vsFpm)
    {
        float v0 = vsFpm / 60f;
        float vEnd = CaptureMinVs / 60f;
        return Mathf.Max((v0 * v0 - vEnd * vEnd) / (2f * CaptureAccelFtPerSec2), 0f);
    }

    void ApplyAltitudeCaptureFlare()
    {
        if (Session.State == null || Session.State.GSCaptured || Session.State.VNAV)
        {
            _captureLeadVs = 0;
            return;
        }

        int remaining = Mathf.Abs(RAltitude - (int)CAltitude);
        int live = Mathf.Max(Mathf.Abs(RVS), Mathf.Abs(CVS));

        if (_captureLeadVs == 0)
        {
            if (live <= CaptureMinVs || remaining >= CaptureLeadFeet(live))
                return;
            _captureLeadVs = live;
        }

        if (remaining >= CaptureLeadFeet(_captureLeadVs))
        {
            _captureLeadVs = 0;
            return;
        }

        int sign = (int)CAltitude > RAltitude ? -1 : 1;
        float vEnd = CaptureMinVs / 60f;
        float v = Mathf.Sqrt(vEnd * vEnd + 2f * CaptureAccelFtPerSec2 * remaining);
        RVS = sign * Mathf.Max(Mathf.RoundToInt(v * 60f), CaptureMinVs);
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

                // ALT ACQ flare. LVL CHG rewrites RVS once a second; this runs every pitch tick.
                ApplyAltitudeCaptureFlare();
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
                        _captureLeadVs = 0;
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
        EnsureSpeedWindowSynced();
        if (FmcOwnsSpeed())
            ApplyFmcSpeed();
        else if (!LevelTestMode.IsActive && Session.State != null && Session.State.VNAV)
            _fmcScheduleSpeed = ApplyVnavPointSpeedLimit(_fmcScheduleSpeed);
        if (RSpeed > increasedSpeed) RRSpeed = RSpeed; else RRSpeed = increasedSpeed;

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


    /// <summary>NM → ft (same factor as ILS GS path).</summary>
    public const double NmToFeet = 6076.12;

    /// <summary>Geometric 3° path (tan(3°) × NM). ~318.4 ft/NM, matches GsAltitudeDeviation.</summary>
    public static readonly double VdiPathFtPerNm = Math.Tan(3.0 * Math.PI / 180.0) * NmToFeet;

    /// <summary>
    /// Ceiling used as a descent path target. ABOVE-only is a floor, not a TOD/VDI aim.
    /// </summary>
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
                return -1;
            case RoutePoint.AltitudeFlags.AboveBelow:
                if (alt.RestrictionBelow > 0)
                {
                    return alt.RestrictionBelow;
                }

                if (alt.RestrictionExact > 0)
                {
                    return alt.RestrictionExact;
                }

                return alt.ComputedValue;
            default:
                return alt.ComputedValue;
        }
    }

    /// <summary>Along-track NM of the drawn magenta leg that ends at destIndex.</summary>
    public static double AlongTrackDistanceOnSegment(RouteScriptableObject route, int destIndex)
    {
        var lines = route?.TracedRoute?.ComputedLines;
        if (lines != null && destIndex >= 1 && destIndex < lines.Length)
        {
            var line = lines[destIndex];
            if (line != null && line.TracedNMLength > 0.05f)
            {
                return line.TracedNMLength;
            }
        }

        if (route?.Points != null && destIndex >= 0 && destIndex < route.Points.Length)
        {
            return Math.Max(0, route.Points[destIndex].Distance);
        }

        return 0;
    }

    /// <summary>
    /// Remaining NM on the current magenta leg. Prefer LNAV walk on the traced line;
    /// otherwise scale published remaining by traced/published so turns are counted.
    /// </summary>
    public static double RemainingAlongCurrentSegmentNm(RouteScriptableObject route, int passedNodeIndex,
        double cartesianLeft)
    {
        var dest = passedNodeIndex + 1;
        var alongSeg = AlongTrackDistanceOnSegment(route, dest);
        var aircraft = Session.PlayerAircraft;
        if (aircraft != null && aircraft.IsOnRoute && aircraft.CurrentSegmentIndex == dest)
        {
            return Math.Max(0, alongSeg - aircraft.NMWalkedOnCurrentSegment);
        }

        if (cartesianLeft <= 0)
        {
            return 0;
        }

        var published = route?.Points != null && dest >= 0 && dest < route.Points.Length
            ? Math.Max(1e-4, route.Points[dest].Distance)
            : alongSeg;
        if (published < 1e-4)
        {
            return cartesianLeft;
        }

        return Math.Max(0, cartesianLeft) * (alongSeg / published);
    }

    /// <summary>
    /// Extra NM before the 3° intercept so energy can bleed (speed drop). Uses GS so
    /// headwind shortens the pad and tailwind lengthens it. Does not change the 3° FPA.
    /// </summary>
    public static double EnergyPadNm(RouteScriptableObject route, int constraintIndex)
    {
        if (route?.Points == null || constraintIndex < 0 || constraintIndex >= route.Points.Length)
        {
            return 0;
        }

        var cruiseIas = Session.CurrentLevel?.levelInfo != null
            ? Session.CurrentLevel.levelInfo.CrzSpeed
            : 0;
        if (cruiseIas <= 0)
        {
            cruiseIas = (int)CSpeed;
        }

        var restrict = route.Points[constraintIndex].RawSpeed;
        if (restrict <= 0)
        {
            restrict = cruiseIas;
        }

        var delta = Mathf.Max(0, cruiseIas - restrict);
        if (delta < 10)
        {
            return 0;
        }

        var gs = GS > 1 ? GS : Mathf.Max(cruiseIas, 120);
        var pad = delta * gs / 3600f + 0.8f;
        return Mathf.Clamp(pad, 0f, 12f);
    }

    static bool IsDescentConstraintAltitude(double constraintAlt, double cruiseAltitude)
    {
        return constraintAlt >= 0 && constraintAlt < cruiseAltitude - 50.0;
    }

    /// <summary>INIT calculated altitude at a waypoint (ComputedValue, else accepted/restriction).</summary>
    public static double GetPointCalculatedAltitude(RoutePoint point)
    {
        if (point == null)
            return -1;
        if (point.Altitude != null && point.Altitude.ComputedValue > 0)
            return point.Altitude.ComputedValue;
        var accepted = point.GetAcceptedAltitude;
        if (accepted > 0)
            return accepted;
        return GetVdiConstraintAltitude(point);
    }

    /// <summary>
    /// Next waypoint calculated altitude and remaining NM to it (current magenta leg).
    /// </summary>
    public static bool TryGetNextVdiAnchor(RouteScriptableObject route, int passedNodeIndex,
        double distanceLeftOnSegment, out double constraintAltitude, out double distanceToConstraintNm)
    {
        return TryGetNextVdiAnchor(route, passedNodeIndex, distanceLeftOnSegment,
            out constraintAltitude, out distanceToConstraintNm, out _);
    }

    public static bool TryGetNextVdiAnchor(RouteScriptableObject route, int passedNodeIndex,
        double distanceLeftOnSegment, out double constraintAltitude, out double distanceToConstraintNm,
        out int anchorIndex)
    {
        constraintAltitude = -1;
        distanceToConstraintNm = 0;
        anchorIndex = -1;
        if (route?.Points == null || passedNodeIndex < 0 || passedNodeIndex >= route.Points.Length - 1)
        {
            return false;
        }

        var dest = passedNodeIndex + 1;
        var alt = GetPointCalculatedAltitude(route.Points[dest]);
        if (alt < 0)
        {
            return false;
        }

        constraintAltitude = alt;
        distanceToConstraintNm = RemainingAlongCurrentSegmentNm(route, passedNodeIndex, distanceLeftOnSegment);
        anchorIndex = dest;
        return true;
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

        var alongSeg = AlongTrackDistanceOnSegment(route, passedNodeIndex + 1);
        var alongLeft = RemainingAlongCurrentSegmentNm(route, passedNodeIndex, distanceLeftOnSegment);
        distFromPrevToAircraftNm = Math.Max(0.0, alongSeg - alongLeft);

        for (var i = passedNodeIndex; i >= 0; i--)
        {
            if (i < passedNodeIndex)
            {
                distFromPrevToAircraftNm += AlongTrackDistanceOnSegment(route, i + 1);
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
    /// Path at the aircraft: next waypoint calculated altitude plus a 3° rise back
    /// to the current along-track position (ft = alt_next + 318 × NM_to_next).
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

        slopeFtPerNm = VdiPathFtPerNm;
        pathTarget = nextConstraintAlt + VdiPathFtPerNm * Math.Max(0.0, distToNext);
        return true;
    }

    /// <summary>
    /// VDI / VNAV path: 3° back from the next point's calculated altitude.
    /// </summary>
    public static double ComputeVdiTarget(RouteScriptableObject route, int passedNodeIndex,
        double distanceLeftOnSegment, double alt0, double alt1, double segmentLength, double startAltitude)
    {
        if (TryComputeVnavPath(route, passedNodeIndex, distanceLeftOnSegment, segmentLength, startAltitude,
                out var pathTarget, out _, out _))
        {
            return pathTarget;
        }

        if (alt1 <= 0)
            alt1 = alt0 > 0 ? alt0 : startAltitude;
        var dist = RemainingAlongCurrentSegmentNm(route, passedNodeIndex, distanceLeftOnSegment);
        return alt1 + VdiPathFtPerNm * Math.Max(0.0, dist);
    }

    /// <summary>
    /// First along-route TOD: cruise ∩ 3° path into the first AT/below that is below cruise,
    /// plus an energy pad. ABOVE-only is skipped. If 3° needs more NM than the route has,
    /// TOD is clamped to the start of the route (already late) instead of hidden.
    /// Distances follow the magenta curve. segmentIndex is the destination node index.
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

        if (!TryPickTodConstraint(route, cruiseAltitude, out var anchorIndex, out var constraintAlt,
                out var distFromStart))
        {
            return false;
        }

        var dToConstraintAtTod = (cruiseAltitude - constraintAlt) / VdiPathFtPerNm
                                 + EnergyPadNm(route, anchorIndex);
        if (dToConstraintAtTod < 0)
        {
            return false;
        }

        dToConstraintAtTod = Math.Clamp(dToConstraintAtTod, 0, distFromStart);
        var distAlongFromStart = distFromStart - dToConstraintAtTod;
        return TryGetAlongTrackPosition(route, distAlongFromStart, out onTrackPosition, out segmentIndex,
            out segmentT);
    }

    static bool TryPickTodConstraint(RouteScriptableObject route, double cruiseAltitude,
        out int anchorIndex, out double constraintAlt, out double distFromStart)
    {
        anchorIndex = -1;
        constraintAlt = -1;
        distFromStart = 0;

        double walked = 0;
        for (var i = 1; i < route.Points.Length; i++)
        {
            walked += AlongTrackDistanceOnSegment(route, i);
            var alt = GetPointCalculatedAltitude(route.Points[i]);
            if (!IsDescentConstraintAltitude(alt, cruiseAltitude))
            {
                continue;
            }

            anchorIndex = i;
            constraintAlt = alt;
            distFromStart = walked;
            return true;
        }

        return false;
    }

    public static bool TryGetAlongTrackPosition(RouteScriptableObject route, double distAlongFromStart,
        out Vector2 onTrackPosition, out int segmentIndex, out float segmentT)
    {
        onTrackPosition = default;
        segmentIndex = -1;
        segmentT = 0f;
        if (route?.Points == null || route.Points.Length < 2)
        {
            return false;
        }

        distAlongFromStart = Math.Max(0, distAlongFromStart);
        double walked = 0;
        for (var i = 0; i < route.Points.Length - 1; i++)
        {
            var node0 = route.Points[i];
            var node1 = route.Points[i + 1];
            var segmentLength = AlongTrackDistanceOnSegment(route, i + 1);
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
            if (route.TracedRoute != null &&
                route.TracedRoute.TryGetPositionOnLine(segmentIndex, segmentT, out var onDrawn))
            {
                onTrackPosition = onDrawn;
            }
            else
            {
                onTrackPosition = Vector2.Lerp(node0.CartesianPosition, node1.CartesianPosition, segmentT);
            }

            return true;
        }

        return false;
    }

    /// <summary>
    /// 0 = VNAV path, 1 = ILS GS. Blends in after LOC capture as GS error shrinks,
    /// so VDI does not jump at GS capture.
    /// </summary>
    public static float GsPathBlend()
    {
        if (Session.State == null)
        {
            return 0f;
        }

        if (Session.State.GSCaptured)
        {
            return 1f;
        }

        if (!Session.State.LOCCaptured || Move.Instance == null || Session.CurrentLevel?.levelInfo == null)
        {
            return 0f;
        }

        var gsDev = Move.Instance.GsAltitudeDeviation(Session.CurrentLevel.levelInfo.GlideSlope);
        return Mathf.Clamp01(Mathf.InverseLerp(800f, 150f, Mathf.Abs(gsDev)));
    }

    public static bool TryGetIlsGsPathAltitude(out double gsPathAlt)
    {
        gsPathAlt = 0;
        if (Move.Instance == null || Session.CurrentLevel?.levelInfo == null)
        {
            return false;
        }

        var gsDev = Move.Instance.GsAltitudeDeviation(Session.CurrentLevel.levelInfo.GlideSlope);
        gsPathAlt = CAltitude - gsDev;
        return true;
    }

    public void DrawVDI()
    {
        double DeltaAlt;
        float posY;
        var haveVnav = false;
        double Target = CAltitude;

        RouteScriptableObject activePoints = Session.ActiveRoute;
        RouteScriptableObject modPoints = Session.ModRoute;
        RouteScriptableObject _route = Session.IsMod ? modPoints : activePoints;
        if (_route == null) _route = activePoints;

        if (_route?.Points != null && PositionVirtualNode.PassedNodeIndex >= 0 &&
            PositionVirtualNode.PassedNodeIndex < _route.Points.Length - 1)
        {
            var node0 = _route.Points[PositionVirtualNode.PassedNodeIndex];
            var node1 = _route.Points[PositionVirtualNode.PassedNodeIndex + 1];
            var d = Session.PlayerAircraft.ComputedDistanceLeftOnSegment;
            var D = node1.Distance;
            var alt0 = (double)node0.Altitude.ComputedValue;
            var alt1 = (double)node1.Altitude.ComputedValue;
            Target = ComputeVdiTarget(_route, PositionVirtualNode.PassedNodeIndex, d, alt0, alt1, D, StartAltitude);
            haveVnav = true;
            if (D == 0)
            {
                Target = CAltitude;
            }
        }

        var blend = GsPathBlend();
        if (blend > 0f && TryGetIlsGsPathAltitude(out var gsAlt))
        {
            Target = haveVnav ? Target * (1.0 - blend) + gsAlt * blend : gsAlt;
        }
        else if (!haveVnav)
        {
            return;
        }

        DeltaAlt = CAltitude - Target;

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

        var points = Session.ActiveRoute?.Points;
        if (!Session.State.VNAV || points == null || points.Length == 0)
        {
            return targetSpeed;
        }

        // Speed at the next waypoint is an AT restriction: cross that fix at RawSpeed.
        // Start slowing only when close enough to arrive at that speed — not the whole inbound leg.
        var pointIndex = Mathf.Clamp(PositionVirtualNode.NextNodeIndex, 0, points.Length - 1);
        var pointSpeed = points[pointIndex].RawSpeed;
        if (pointSpeed <= 0 || pointSpeed >= targetSpeed)
        {
            return targetSpeed;
        }

        var aircraft = Session.PlayerAircraft;
        if (aircraft == null)
        {
            return targetSpeed;
        }

        float distLeftNm = aircraft.ComputedDistanceLeftOnSegment;
        int deltaKts = Mathf.Max(0, (int)CSpeed - pointSpeed);
        if (deltaKts < 5)
            deltaKts = targetSpeed - pointSpeed;

        float gsKts = Mathf.Max(GS, 120f);
        float decelNm = deltaKts * gsKts / 3600f + 0.8f;
        if (decelNm < 1.5f)
            decelNm = 1.5f;
        if (decelNm > 12f)
            decelNm = 12f;

        if (distLeftNm > decelNm)
        {
            return targetSpeed;
        }

        return pointSpeed;
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

        TryGetNextVdiAnchor(route, PositionVirtualNode.PassedNodeIndex, d,
            out _, out var distToNext, out var nextAnchorIndex);

        var gsBlend = GsPathBlend();
        if (gsBlend > 0f && TryGetIlsGsPathAltitude(out var gsAlt) && Session.CurrentLevel?.levelInfo != null)
        {
            pathTarget = pathTarget * (1.0 - gsBlend) + gsAlt * gsBlend;
            var glide = Session.CurrentLevel.levelInfo.GlideSlope;
            var gsSlope = Math.Tan(glide * Math.PI / 180.0) * NmToFeet;
            slopeFtPerNm = slopeFtPerNm * (1.0 - gsBlend) + gsSlope * gsBlend;
        }

        bool mcpDescentWindow = RAltitude < (int)CAltitude;
        double deviation = CAltitude - pathTarget; // + above path, - below

        // Hold below the 3° path until the energy TOD. After that, descend to intercept.
        const double belowPathHoldBand = 300;
        var energyTodNm = (nextConstraintAlt > 0)
            ? Math.Max(0.0, (StartAltitude - nextConstraintAlt) / VdiPathFtPerNm)
              + EnergyPadNm(route, nextAnchorIndex)
            : 0;
        var pastEnergyTod = nextConstraintAlt <= 0 || distToNext <= energyTodNm;
        if (mcpDescentWindow && deviation < -belowPathHoldBand && !pastEnergyTod && gsBlend < 0.25f)
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
    /// Item 6 stays in force: VNAV will not descend until MCP is below cruise.
    /// If TOD is passed with MCP still at cruise, remind on the CDU scratchpad.
    /// </summary>
    void CheckResetMcpAltitudeScratchpad()
    {
        const string msg = MainScreen.Keywords.ResetMcpAltitude;
        var pastTod = false;
        var shouldShow = false;

        var route = Session.ActiveRoute;
        if (route?.Points != null
            && PositionVirtualNode.PassedNodeIndex >= 0
            && PositionVirtualNode.PassedNodeIndex < route.Points.Length - 1
            && Session.PlayerAircraft != null)
        {
            var d = Session.PlayerAircraft.ComputedDistanceLeftOnSegment;
            if (TryGetNextVdiAnchor(route, PositionVirtualNode.PassedNodeIndex, d,
                    out var nextAlt, out var distToNext, out var nextAnchor)
                && nextAlt > 0)
            {
                var todNm = Math.Max(0.0, (StartAltitude - nextAlt) / VdiPathFtPerNm)
                            + EnergyPadNm(route, nextAnchor);
                pastTod = distToNext <= todNm;
                var stillAtCruise = CAltitude >= StartAltitude - 300.0;
                var mcpUnchanged = RAltitude >= (int)StartAltitude - 100;
                shouldShow = pastTod && stillAtCruise && mcpUnchanged
                             && Session.State != null && Session.State.VNAV && !Session.State.GSCaptured;
            }
        }

        if (!pastTod)
            _resetMcpAltitudePosted = false;

        var pad = MainScreen.Instance != null ? MainScreen.Instance.scratchPadText : null;
        if (pad == null)
            return;

        var showingOurs = string.Equals(pad.GetText() ?? "", msg, StringComparison.Ordinal);
        if (shouldShow && !_resetMcpAltitudePosted)
        {
            pad.SetAsDefault(msg);
            _resetMcpAltitudePosted = true;
            return;
        }

        if (!shouldShow && showingOurs)
            pad.Clear();
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
        _resetMcpAltitudePosted = false;

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
                FMA1.text = FmcOwnsSpeed() ? "FMC SPD" : "MCP SPD";
                FMA3.text = "VNAV";
            }
        }

        else
        {
            txtRVS.text = "";
            FMA1.text = "MCP SPD";
            FMA3.text = "GS";
        }

        // Lateral capture does not replace the active vertical mode.
        if (Session.State.LOCCaptured)
            FMA2.text = "LOC";

        if (Session.State.GSCaptured)
            FMAarmed.text = "";
        else if (Session.State.LOCCaptured)
            FMAarmed.text = "                         GS";
        else if (Session.State.AppArmed)
            FMAarmed.text = "LOC                 GS";

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
        int ff = (int)((s1 - s2) / -5000f * (Altitude - (8 - F + 1) * 5000) + s2);
        return ApplyOperationalFf(ff, Altitude);
    }

    static int GearOrBrakeFf(int seaLevelAdd, int altRow)
    {
        altRow = Mathf.Clamp(altRow, 0, Pressure.Length - 1);
        return (int)Math.Round(seaLevelAdd * Pressure[altRow]);
    }

    static double AltBandLerp(double atBand, double atBandAbove, int band, double altitude)
    {
        return (atBand - atBandAbove) / -5000.0 * (altitude - (8 - band + 1) * 5000) + atBandAbove;
    }

    static int ApplyOperationalFf(int ff, double altitude)
    {
        if (ff <= 210)
            return ff;

        double factor = WeightFactor() * (1.0 + 0.03 * (IsaDeviationC / 10.0));
        if (EnginesRunning <= 1)
            factor *= 1.15;

        int add = 0;
        if (!PacksOn)
            add -= 15;
        if (EngineAntiIce)
            add += AntiIceAt(altitude);

        int result = (int)Math.Round(ff * factor) + add;
        return result < 211 ? 211 : result;
    }

    static double WeightFactor()
    {
        var info = Session.CurrentLevel != null ? Session.CurrentLevel.levelInfo : null;
        if (info == null || info.ZFW < 35f || info.ZFW > 70f)
            return 1;
        double gw = info.ZFW + totalFuel / 100.0;
        double factor = gw / 56.4;
        if (factor < 0.85) factor = 0.85;
        if (factor > 1.15) factor = 1.15;
        return factor;
    }

    static int AntiIceAt(double altitude)
    {
        int F = 8 - Mathf.FloorToInt((float)altitude / 5000);
        F = Mathf.Clamp(F, 1, 8);
        return (int)Math.Round(AltBandLerp(AntiIceFf[F], AntiIceFf[F - 1], F, altitude));
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
        int engines = EnginesRunning <= 1 ? 1 : 2;
        totalFuel -= (double)FF * engines / 3600 * Session.Settings.SpeedMultiplier;                                                     //Fuel
        txtTotalFuel.text = "" + System.Math.Round(totalFuel / 100, 2);

        CMach = Speed2Mach(CSpeed, CAltitude);
        txtMach.text = CMach > 0.4 ? FormatMach(CMach) : "GS " + GS;

        double coAlt = GetCoAltitudeFeet();
        if (co.isOn)
        {
            if (FmcOwnsSpeed())
                RMach = Speed2Mach(RSpeed, CAltitude);
            else
                RSpeed = (int)Mach2Speed(RMach, CAltitude);
            if (CAltitude < coAlt) co.isOn = false;
        }
        if (CAltitude < coAlt) co.enabled = false; else co.enabled = true;

    }
    public void FUP_Click()
    {
        if (Flap_Idx >= MaxFlapIdx)
            return;

        if (CAltitude < 20000 && CanSelectFlap(Flap_Idx + 1))
        {
            Flap_Idx += 1;
            SetFlaps();

            MMVibrationManager.Haptic(HapticTypes.MediumImpact);
            return;
        }

        NdPauseOverlay.NotifyCheckSpeed();
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
            FlapNeedle.transform.localEulerAngles = new Vector3(-90, 180, FlapDetentZ[Flap_Idx]);
        }
        else
        {
            Debug.LogError("Assign flapNeedle");
        }

        PFD_Animation PFDScript = FindObjectOfType<PFD_Animation>();
        PFDScript.Flaps_Indexchange(Flap_Idx);
        minimumSpeed = FlapMinSpeeds[Flap_Idx];
        if (FmcOwnsSpeed())
        {
            ApplyFmcSpeed();
            RefreshSpeedWindow();
        }

        // Flap copy wipes FL200-and-below. Put the current gear state back on those rows.
        if (LGDown)
            ApplyGearModel(true, 4, 8);
    }

    bool CanSelectFlap(int flapIdx)
    {
        return CSpeed <= PFD_Animation.FlapSelectLimitKnots(flapIdx);
    }
    public static bool GearLeverLocked()
    {
        return CSpeed > 270 || Speed2Mach(CSpeed, CAltitude) > 0.82;
    }

    // Extra idle VS, fpm, FL400 down to sea level. Gear drag CD +0.020 at GW 56.4 t, ISA.
    static readonly double[,] GearIdleVs = {
        { -2060, -1190 }, { -2920, -1420 }, { -2650, -1290 }, { -2430, -1180 }, { -2220, -1080 },
        { -2050, -990 }, { -1890, -920 }, { -1750, -850 }, { -1620, -790 } };
    static readonly double[,,] GearOnIdle = {
        { { 4300, 140 }, { 4170, 144 } },
        { { 4150, 160 }, { 4120, 164 } },
        { { 4100, 176 }, { 4070, 180 } },
        { { 4070, 190 }, { 4050, 196 } } };
    static readonly double[,,] GearOffIdle = {
        { { 3700, 114 }, { 3420, 114 } },
        { { 3370, 126 }, { 3300, 126 } },
        { { 3460, 136 }, { 3200, 136 } },
        { { 3340, 146 }, { 3120, 146 } } };

    public void SetLG(bool down, bool fromUI)
    {
        if (fromUI && down && GearLeverLocked())
        {
            if (UYServiceLocator.Has<McpUI>())
                UYServiceLocator.Get<McpUI>().LGLeverInteract(false, true);
            NdPauseOverlay.NotifyCheckSpeed();
            return;
        }

        if (LGDown == down) return;

        LGDown = down;
        ApplyGearModel(down, 0, 8);
    }

    static void ApplyGearModel(bool down, int fromRow, int toRow)
    {
        double sign = down ? 1d : -1d;
        for (int i = fromRow; i <= toRow && i < GearIdleVs.GetLength(0); i++)
        {
            M[i, 1, 0] += sign * GearIdleVs[i, 0];
            M[i, 3, 0] += sign * GearIdleVs[i, 1];
            M[i, 0, 2] += sign * 1500;
            M[i, 2, 2] += sign * 1350;
            M[i, 0, 3] += sign * GearOrBrakeFf(320, i);
            M[i, 2, 3] += sign * GearOrBrakeFf(200, i);

            if (i < 5)
                continue;

            int w = i - 5;
            var idle = down ? GearOnIdle : GearOffIdle;
            M[i, 1, 2] = idle[w, 0, 0];
            M[i, 3, 2] = idle[w, 1, 0];
            int flapRow = i - 4;
            double flapIdleHi = idle[w, 0, 1];
            double flapIdleLo = idle[w, 1, 1];
            if (Instance != null && flapRow >= 0 && flapRow < 5
                && Flap_Idx >= 0 && Flap_Idx < Instance.Mf.GetLength(0))
            {
                flapIdleHi = Instance.Mf[Flap_Idx, flapRow, 1, 3];
                flapIdleLo = Instance.Mf[Flap_Idx, flapRow, 3, 3];
            }
            if (down)
            {
                M[i, 1, 3] = Math.Max(flapIdleHi, idle[w, 0, 1]);
                M[i, 3, 3] = Math.Max(flapIdleLo, idle[w, 1, 1]);
            }
            else
            {
                M[i, 1, 3] = flapIdleHi;
                M[i, 3, 3] = flapIdleLo;
            }
        }
    }
    public void SetSB(bool Up, bool fromUI)
    {
        if (SBUp == !Up) return;

        // Extra idle VS, fpm, FL400 down to sea level. 737 FCTM +900 fpm at 280 kt / 10,000 ft, scaled by dynamic pressure and TAS.
        double[,] Msb = new double[9, 2] {
            { -980, -570 }, { -1390, -670 }, { -1260, -610 }, { -1160, -560 }, { -1060, -510 },
            { -980, -470 }, { -900, -440 }, { -830, -400 }, { -770, -380 } };

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
                M[i, 0, 3] += GearOrBrakeFf(115, i);
                M[i, 2, 3] += GearOrBrakeFf(80, i);

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
                M[i, 0, 3] -= GearOrBrakeFf(115, i);
                M[i, 2, 3] -= GearOrBrakeFf(80, i);
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
        // or less: leave MCP if it is already below the clearance.
        // or greater: leave MCP if it is already above the clearance.
        bool apply = Move.XFRSpeedNx != 2 && Move.XFRSpeedNx != 1
            || (Move.XFRSpeedNx == 2 && RSpeed > Move.XFRSpeed)
            || (Move.XFRSpeedNx == 1 && RSpeed < Move.XFRSpeed);

        if (apply)
        {
            RSpeed = Move.XFRSpeed;
            if (co.isOn)
                RMach = Speed2Mach(RSpeed, CAltitude);
            SpeedWindowOpen = true;
            RefreshSpeedWindow();
            SetFMA();
        }
        if (Move.Instance != null)
            Move.Instance.AcknowledgeAtc3();
    }

    public static void Check_LimitSpeed()
    {
        if (Instance != null && Instance.FmcOwnsSpeed())
        {
            Instance.ApplyFmcSpeed();
            Instance.RefreshSpeedWindow();
            return;
        }

        if (Calculator.Instance.co.isOn)                                    //Mach
        {
            if (Calculator.Instance.RMach < 0.6) Calculator.Instance.RMach = 0.6;
            if (Calculator.Instance.RMach > Speed2Mach(PFD_Animation.LimitSpeed, CAltitude)) Calculator.Instance.RMach = Speed2Mach(PFD_Animation.LimitSpeed, CAltitude);
            RSpeed = (int)Mach2Speed(Calculator.Instance.RMach, CAltitude);
        }
        else
        {                                               //IAS 
            if (RSpeed < minimumSpeed) RSpeed = minimumSpeed;
            if (PFD_Animation.LimitSpeed > 0 && RSpeed > PFD_Animation.LimitSpeed) RSpeed = PFD_Animation.LimitSpeed;
        }
        Calculator.Instance.RefreshSpeedWindow();
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
        if (FmcOwnsSpeed())
            SpeedWindowOpen = true;

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
        SetFMA();
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
        else
        {
            txtRVS.text = "";
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
            for (int i = 0; i < 4; i++)
            {
                M[i, 1, 0] += MVS[i, 0];
                M[i, 3, 0] += MVS[i, 1];

            }
        }
        else
        {
            RSpeed = (int)Mach2Speed(RMach, CAltitude);
            for (int i = 0; i < 4; i++)
            {
                M[i, 1, 0] -= MVS[i, 0];
                M[i, 3, 0] -= MVS[i, 1];

            }

        }

        if (FmcOwnsSpeed())
            ApplyFmcSpeed();
        RefreshSpeedWindow();
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

        // Overflying RW / short final on a heading other than inbound final: keep flying.
        if (!Move.Instance.IsWithinFinalApproachCourse(20f))
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
        // Short-final ROD: up to 1300 fpm stable, steeper is unstable.
        bool verticalSpeedOk = CVS <= -200 && CVS >= -1300;
        int flapSetting = GetFlapSettingDegrees(Flap_Idx);
        bool flapsOk = flapSetting == 30 || flapSetting == 40;
        bool speedOk = IsApproachSpeedStable(flapSetting, levelInfo);
        bool landingGearOk = LGDown;
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
            FormatStatusLine("Landing Gear", landingGearOk ? "Down" : "Up", landingGearOk) + lf +
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

    static bool IsApproachSpeedStable(int flapSetting, LevelInfoScriptableObject levelInfo)
    {
        // Flaps 30/40: Vref is F30 (F40 = Vref+10), but MCP/tape cannot go below flap min (145).
        // Use the speed you can actually fly, otherwise Vref+20 (141) is below min and Speed is always "no".
        if (flapSetting == 30 || flapSetting == 40)
        {
            int vref = levelInfo != null && levelInfo.F30Speed > 0 ? levelInfo.F30Speed : 120;
            if (flapSetting == 40)
                vref += 10;
            int target = Mathf.Max(vref, minimumSpeed);
            return CSpeed >= target - 5 && CSpeed <= target + 20;
        }

        return CSpeed >= minimumSpeed && CSpeed <= minimumSpeed + 20;
    }

    static int GetFlapSettingDegrees(int flapIdx)
    {
        // UP, 1, 2, 5, 10, 15, 25, 30, 40
        int[] settings = { 0, 1, 2, 5, 10, 15, 25, 30, 40 };
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

public class SpeedWindowClickRelay : MonoBehaviour, IPointerClickHandler
{
    public void OnPointerClick(PointerEventData eventData)
    {
        if (Calculator.Instance != null)
            Calculator.Instance.OnSpeedWindowClicked();
    }
}
