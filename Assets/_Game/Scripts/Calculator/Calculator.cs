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

// Performance weight is fixed at 75 t. CI:0, CG:23.3

[DefaultExecutionOrder(100)]
public class Calculator : MonoBehaviour
{

    public static int Level = 11;             // ***  Level

    private string result;
    public Text txtRSpeed, txtRAltitude, txtRVS;
    public Text txtCSpeed, txtCAltitude, txtCVS;
    public Text txtRSpeed_overTape, txtRAltitude_overTape;
    public Text txtDTG;//DTG: Distance to go
    public Text txtMeter;//Meter Display
    public Text Qnh, txtMach;

    public Text txtN1, txtFF, txtTotalFuel;// N1( / 100) , Fuel Flow ( X 100) , Pitch attitude ( / 100)
    Text _fuelPenaltyLabel;
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
    private int increasedSpeed, excessSpeedCo = 0;
    int _captureLeadVs;
    public static int Flap_Idx = 0;
    private const int MaxFlapIdx = 8; // Mf[8]=f40, Fps[8]=86°
    private float SpeedTime;
    // Knots earned toward the next 1 kt step. The energy rate is recomputed at least once a second,
    // so a 30 s idle-path sample cannot freeze IAS after LVL CHG has already pitched to the bleed VS.
    float _speedKnotCredit;
    float _speedStepDelay = 1f;
    public Text windTxt;
    public static string CWind;
    public static int HeadingWindAddition, CHeadingWindAddition;
    public Text FMA1, FMA2, FMA3, FMAarmed;
    public Image windArrow, VSline, SpeedTrend;
    public GameObject Progres, FlapNeedle;
    static readonly int[] FlapDetentZ = { -179, -145, -103, -64, -35, -3, 29, 57, 86 };
    private int N1, dispN1 = 77;
    public static int FF = 77;
    public static double dispFF = 270;
    // PerformanceChart is the 75 t table. Fuel burn does not change that weight. ISA, packs on, anti-ice off, two engines.
    public static float IsaDeviationC;
    public static bool EngineAntiIce;
    public static bool PacksOn = true;
    public static int EnginesRunning = 2;
    public static double totalFuel = 1000; // 10 tons *100


    //Speed:(NM per Hour = Knots)--> Show in PFD , map will move in this speed
    //Altitude:(Feet)--> Show in PFD ,no other effect
    //VS:(Feet per minute)--> Show in PFD ,no other effect
    //N1:(Engine Rotation percentage, divide by 100 )--> Show in N1 Guage ,no other effect
    //FF:(libres per hour per engine  X10, Sample: 230 means 2300 lb/hr per engine, total 4600 lb/hr )--> Show in FF Guage ,decrease the total fuel by this number
    //Pitch:(degrees, divide by 100)--> Show in PFD ,no other effect
    //LG:(on-off)--> Illustrate Landing Gear movement
    //SB:(on-off)--> Illustrate Speed Brake movement
    //Flaps: (1-5-10-15-25-30-40)

    //**************************************************************


    private static WindTableScriptableObject CurrentWindTable =>
        Session.CurrentLevel != null ? Session.CurrentLevel.WindTable : null;

   
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

            if (Session.State == null || Session.Settings == null)
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
        PFD_Animation PFDScript = FindPfd();
        if (PFDScript == null)
            return;

        int Angle = CachedDisplayLastAngleDiff;

        int Bank = Angle < -3 ? -27 : Angle > 3 ? 27 : 0;

        PFDScript.PFD_Bank(Bank);
    }

    private void Start()
    {
        BeginFlight();
    }

    /// <summary>
    /// GameManager runs at an earlier execution order and has already built the route.
    /// </summary>
    private void BeginFlight()
    {
        totalFuel = 1000;
        SetLabel(txtTotalFuel, "10");
        RefreshFuelPenaltyLabel();

        var points = Session.ActiveRoute?.Points;
        if (points == null || points.Length == 0 || Session.CurrentLevel == null)
        {
            Debug.LogError("[Calculator] Flight session was not ready. PFD loops were not started.");
            return;
        }

        long cruiseAltitude = RouteIdZeroAltitude(Session.ActiveRoute);
        if (cruiseAltitude <= 0)
        {
            cruiseAltitude = 10000;
            SetRouteIdZeroAltitude(Session.ActiveRoute, (int)cruiseAltitude);
        }

        var info = Session.CurrentLevel.levelInfo;
        StartAltitude = cruiseAltitude;

        CSpeed = info != null && info.CrzSpeed > 0 ? info.CrzSpeed : 250;
        CAltitude = StartAltitude;
        RAltitude = (int)CAltitude;
        RSpeed = (int)CSpeed;
        _fmcScheduleSpeed = RSpeed;
        RVS = CVS;
        _approachEndTriggered = false;
        _resetMcpAltitudePosted = false;
        SetLabel(txtRAltitude, "" + RAltitude);
        SetLabel(txtRAltitude_overTape, txtRAltitude != null ? txtRAltitude.text : "" + RAltitude);
        SetLabel(txtCAltitude, "" + (int)CAltitude);
        SetLabel(txtMeter, "" + (int)(CAltitude / 3.28084) + "M");
        SetLabel(txtCSpeed, "" + (int)CSpeed);
        SetLabel(txtCVS, "");
        SetLabel(FMAarmed, "");

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

        if (Session.Settings != null)
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

    void SetLabel(Text label, string value)
    {
        if (label != null)
            label.text = value;
    }

    void RefreshFuelPenaltyLabel()
    {
        if (txtTotalFuel == null)
            return;

        if (_fuelPenaltyLabel == null)
        {
            Transform existing = txtTotalFuel.transform.parent != null
                ? txtTotalFuel.transform.parent.Find("Penalty")
                : null;
            if (existing != null)
                _fuelPenaltyLabel = existing.GetComponent<Text>();
        }

        if (_fuelPenaltyLabel == null)
        {
            var source = txtTotalFuel.rectTransform;
            var go = new GameObject("Penalty", typeof(RectTransform));
            go.transform.SetParent(source.parent, false);

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = source.anchorMin;
            rt.anchorMax = source.anchorMax;
            rt.pivot = new Vector2(0f, source.pivot.y);
            rt.localRotation = source.localRotation;
            rt.localScale = Vector3.one;
            rt.sizeDelta = new Vector2(180f, source.sizeDelta.y);
            float rightEdge = source.anchoredPosition.x + source.sizeDelta.x * (1f - source.pivot.x);
            rt.anchoredPosition = new Vector2(rightEdge + 8f, source.anchoredPosition.y);

            _fuelPenaltyLabel = go.AddComponent<Text>();
            _fuelPenaltyLabel.font = txtTotalFuel.font;
            _fuelPenaltyLabel.fontSize = txtTotalFuel.fontSize;
            _fuelPenaltyLabel.fontStyle = txtTotalFuel.fontStyle;
            _fuelPenaltyLabel.alignment = TextAnchor.MiddleLeft;
            _fuelPenaltyLabel.color = new Color(0.30f, 0.66f, 0.79f, 0.78f);
            _fuelPenaltyLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
            _fuelPenaltyLabel.verticalOverflow = VerticalWrapMode.Overflow;
            _fuelPenaltyLabel.raycastTarget = false;
        }

        int kg = Move.Instance != null ? Mathf.RoundToInt((float)Move.Instance.FuelPenaltyKg) : 0;
        _fuelPenaltyLabel.color = new Color(0.30f, 0.66f, 0.79f, 0.78f);
        _fuelPenaltyLabel.text = "pty: " + kg + " kg";
    }

    PFD_Animation FindPfd()
    {
        return FindObjectOfType<PFD_Animation>();
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
        if (co == null)
        {
            RefreshSpeedWindow();
            SyncSpeedWindowToMode();
            return;
        }
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
                SetLabel(txtRSpeed, FormatMach(RMach));
                SetLabel(txtRSpeed_overTape, txtRSpeed != null ? txtRSpeed.text : FormatMach(RMach));
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
            if (Session.PlayerAircraft == null || Session.State == null)
            {
                yield return new WaitForSeconds(0.1f);
                continue;
            }

            CTrack = (int)Session.PlayerAircraft.DisplayHeadingDegrees;

            try
            {
                MatchAltitudes();
                SetN1FF();
                PFD_Bank();
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
            yield return new WaitForSeconds(0.1f);
        }

    }

    private IEnumerator ExecuteEachSecond()
    {
        while (true)
        {
            if (Session.State == null)
            {
                yield return new WaitForSeconds(1f);
                continue;
            }

            try
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
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }

            //Debug.Log(Move.Instance.FuelPenaltyKg);
            yield return new WaitForSeconds(1);
        }
    }

    public void InterpolateVS()
    {
        if (Session.State == null)
            return;

        double Altitude = ChartAltitude(CAltitude);
        double vv1, vv2, vv3, vv4; // 4 interpolations for VS
        double[] a = new double[4];

        increasedSpeed = 0;

        if (Session.State.VS || Session.State.AH || Session.State.VNAV || Session.State.GSCaptured)
        {
            int F = ChartBand(Altitude);

            // Path modes command pitch from RVS; other modes follow lagged CVS.
            double vsRef = (Session.State.GSCaptured || Session.State.VNAV) ? RVS : CVS;

            int i = 0;//Calculate limit VS
            double s1 = (M[F, 1, i] - M[F, 3, i]) / (M[F, 0, 0] - M[F, 2, 0]) * (RSpeed - M[F, 2, 0]) + M[F, 3, i];
            double s2 = (M[F - 1, 1, i] - M[F - 1, 3, i]) / (M[F - 1, 0, 0] - M[F - 1, 2, 0]) * (RSpeed - M[F - 1, 2, 0]) + M[F - 1, 3, i];
            double limitVS = (s1 - s2) / -5000f * (Altitude - (8 - F + 1) * 5000) + s2 + ConfigVsDelta();
            bool pathWithinIdle = vsRef > limitVS;
            double power = 0.0;
            if (pathWithinIdle)
            {    // Normal VS mode
                power = PowerFraction(vsRef, limitVS);
                for (i = 1; i < 4; i++)
                {
                    if (i == 1)
                    {
                        vv1 = PitchOnVs(F, 0, vsRef);
                        vv2 = PitchOnVs(F, 2, vsRef);
                        vv3 = PitchOnVs(F - 1, 0, vsRef);
                        vv4 = PitchOnVs(F - 1, 2, vsRef);
                    }
                    else
                    {
                        vv1 = (M[F, 0, i] - M[F, 1, i]) / -IdleDenom(F, 1) * (vsRef - IdleVs(F, 1)) + M[F, 1, i];
                        vv2 = (M[F, 2, i] - M[F, 3, i]) / -IdleDenom(F, 3) * (vsRef - IdleVs(F, 3)) + M[F, 3, i];
                        vv3 = (M[F - 1, 0, i] - M[F - 1, 1, i]) / -IdleDenom(F - 1, 1) * (vsRef - IdleVs(F - 1, 1)) + M[F - 1, 1, i];
                        vv4 = (M[F - 1, 2, i] - M[F - 1, 3, i]) / -IdleDenom(F - 1, 3) * (vsRef - IdleVs(F - 1, 3)) + M[F - 1, 3, i];
                    }
                    s1 = (vv1 - vv2) / (M[F, 0, 0] - M[F, 2, 0]) * (CSpeed - M[F, 2, 0]) + vv2;
                    s2 = (vv3 - vv4) / (M[F - 1, 0, 0] - M[F - 1, 2, 0]) * (CSpeed - M[F - 1, 2, 0]) + vv4;
                    a[i - 1] = (s1 - s2) / -5000f * (Altitude - (8 - F + 1) * 5000) + s2;
                }
            }
            else
            {  // Path/VS steeper than idle: raise speed on the chart line, continued linearly past the steep corner.
                for (i = 1; i < 4; i++)
                {
                    s1 = (IdleCornerValue(F, 1, i) - IdleCornerValue(F, 3, i)) / (M[F, 0, 0] - M[F, 2, 0]) * (RSpeed - M[F, 2, 0]) + IdleCornerValue(F, 3, i);
                    s2 = (IdleCornerValue(F - 1, 1, i) - IdleCornerValue(F - 1, 3, i)) / (M[F - 1, 0, 0] - M[F - 1, 2, 0]) * (RSpeed - M[F - 1, 2, 0]) + IdleCornerValue(F - 1, 3, i);
                    a[i - 1] = (s1 - s2) / -5000f * (Altitude - (8 - F + 1) * 5000) + s2;
                }
                i = 0; // Find speed for Current VS on idle
                vv1 = (M[F, 0, i] - M[F, 2, i]) / IdleSpan(F) * (vsRef - IdleVs(F, 3)) + M[F, 2, i];
                vv2 = (M[F - 1, 0, i] - M[F - 1, 2, i]) / IdleSpan(F - 1) * (vsRef - IdleVs(F - 1, 3)) + M[F - 1, 2, i];
                // A fixed VS has one idle speed. GS and VNAV make VS grow with speed, so
                // chasing the chart speed of the current VS runs away. Use the speed where
                // the idle descent rate equals the path angle.
                if (Session.State.GSCaptured || Session.State.VNAV)
                    increasedSpeed = EquilibriumIdleSpeed(PathFpmPerKnot(vsRef));
                else
                    increasedSpeed = (int)((vv1 - vv2) / -5000f * (Altitude - (8 - F + 1) * 5000) + vv2);
            }
            // Idle only when decelerating AND path still within idle capability (path first).
            if (RSpeed < CSpeed - 5 && pathWithinIdle)
            {
                a[1] = M[F, 1, 2];
                a[2] = M[F, 1, 3];
                power = 0.0;
            }
            if (RSpeed > CSpeed + 5)
            {
                a[1] = AccelN1;
                a[2] = 621;
                power = 0.0;
            }
            ApplyConfig(ref a[0], ref a[1], ref a[2], power, Altitude, CSpeed);
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
        if (Session.State == null || !Session.State.LC)
            return;

        double Altitude = ChartAltitude(CAltitude);
        double s1, s2;             // 2 interpolation for Speed    
        double[] a = new double[4];                  // Final interpolation for Altitude

        {
            int F = ChartBand(Altitude);
            for (int i = 0; i < 4; i++)
            {


                s1 = (IdleCornerValue(F, 1, i) - IdleCornerValue(F, 3, i)) / (M[F, 0, 0] - M[F, 2, 0]) * (CSpeed - M[F, 2, 0]) + IdleCornerValue(F, 3, i);
                s2 = (IdleCornerValue(F - 1, 1, i) - IdleCornerValue(F - 1, 3, i)) / (M[F - 1, 0, 0] - M[F - 1, 2, 0]) * (CSpeed - M[F - 1, 2, 0]) + IdleCornerValue(F - 1, 3, i);
                a[i] = (s1 - s2) / -5000f * (Altitude - (8 - F + 1) * 5000) + s2;
            }

            ApplyConfig(ref a[1], ref a[2], ref a[3], 0.0, Altitude, CSpeed);
            a[0] += ConfigVsDelta();

            N1 = (int)a[2];
            FF = (int)a[3];

            if (SpeedTime > 1) excessSpeedCo += 100;
            if ((int)CSpeed == RSpeed) excessSpeedCo = 0;
            int idleVs = (int)a[0];
            // Below the MCP altitude, LVL CHG climbs. Above it, idle descent.
            if ((int)CAltitude < RAltitude)
            {
                int climbVs = Math.Abs(idleVs);
                RVS = (int)CSpeed - RSpeed > 10 ? climbVs + excessSpeedCo :
                    RSpeed - (int)CSpeed > 0 ? 300 : climbVs;
                if (RVS > 4000) RVS = 4000;
                N1 = N1For(Altitude, RVS, CSpeed);
                FF = FuelFlowFor(Altitude, RVS, CSpeed);
            }
            else
            {
                RVS = (int)CSpeed - RSpeed > 10 ? -300 :
                    RSpeed - (int)CSpeed > 0 ? idleVs - excessSpeedCo : idleVs;
            }
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
            if (VSline == null)
                return;

            float[] VSlineY = new float[7] { 0, 50.9f, 64.4f, 66.0f, 67.7f, 69.0f, 70.5f };
            int aCVS = Mathf.Abs(CVS);
            float rotation = aCVS < 6000 ? Mathf.LerpUnclamped(VSlineY[aCVS / 1000], VSlineY[aCVS / 1000 + 1], (float)(aCVS % 1000) / 1000) * -Mathf.Sign(CVS) : 70.5f * -Mathf.Sign(CVS);
            float lenght = rotation != 0 ? 500 / Mathf.Cos(rotation * Mathf.Deg2Rad) : 500;
            VSline.transform.localEulerAngles = new Vector3(0, 0, rotation);
            if (VSline.sprite != null)
                VSline.rectTransform.sizeDelta = new Vector2(lenght, VSline.sprite.rect.height);
            if (txtCVS != null)
                txtCVS.transform.localPosition = CVS > 0 ? new Vector2(200, 90) : new Vector2(200, -100);
        }
        if (Session.State != null && Session.Settings != null && ((CVS != 0) || (RVS != 0)))
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
        if (txtCVS != null)
            txtCVS.text = (Mathf.Abs(CVS) > 300) ? "" + CVS / 100 * 100 : "";

        DrawVSline();

        float vsDelay = (float)(12.75 - CSpeed * 0.025) / 100f;
        if (float.IsNaN(vsDelay) || vsDelay < 0.01f)
            vsDelay = 0.01f;
        Invoke(nameof(VS_Equalize), vsDelay);

    }

    private IEnumerator Altitude_Equalize()
    {
        while (true)
        {
            if (Session.Settings == null || Session.State == null)
            {
                yield return new WaitForSeconds(1f);
                continue;
            }

            try
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
                        SetLabel(txtRAltitude, "" + RAltitude);
                        SetLabel(txtRAltitude_overTape, txtRAltitude != null ? txtRAltitude.text : "" + RAltitude);
                        CVS = 0;
                        RVS = 0;
                        _captureLeadVs = 0;
                        // VNAV: hold MCP as VNAV ALT (keep VNAV). Other modes → ALT HOLD.
                        if (!Session.State.VNAV)
                            Session.State.AutoSetAH(true);
                    }
                }
                SetLabel(txtCAltitude, "" + (int)(CAltitude / 10) * 10);
                SetLabel(txtMeter, "" + (int)(CAltitude / 3.28084 / 10) * 10 + "M");
            }
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
            yield return new WaitForSeconds(1f);
        }
    }
    static double IdleVsOnRow(int row, double speed)
    {
        double span = M[row, 0, 0] - M[row, 2, 0];
        if (Math.Abs(span) < 1.0)
            return M[row, 1, 0];
        double t = (speed - M[row, 2, 0]) / span;
        return (M[row, 1, 0] - M[row, 3, 0]) * t + M[row, 3, 0];
    }

    /// <summary>N1 (hundredths) the autothrottle sets to accelerate.</summary>
    const int AccelN1 = 9650;
    /// <summary>Inside 5 kt of the target the autothrottle trims with part power.</summary>
    const float TrimMaxKtPerMin = 60f;
    const float MinSpeedChangeKtPerMin = 2f;
    const float MaxSpeedChangeKtPerMin = 140f;

    /// <summary>
    /// IAS change rate. Energy balance against the config idle path (flap, gear and speedbrake drag come in
    /// through its VS) at idle thrust. Accelerating outside LVL CHG descent: AccelN1 against the N1 the path needs.
    /// </summary>
    float SpeedChangeKtPerMin(bool accelerating, bool fullThrustRange)
    {
        double altitude = CAltitude > 39900 ? 39900 : (CAltitude < 0 ? 0 : CAltitude);
        double idleVs = ConfigIdleVsAt(altitude, CSpeed) + SpeedbrakeLevelDragVs(altitude);
        float idlePathRate = B737_Performance.IasRateFromVsGap((float)altitude, (float)CSpeed, (float)(idleVs - CVS));
        float rate = -idlePathRate;
        if (accelerating)
        {
            bool idleDescent = Session.State.LC && (int)CAltitude >= RAltitude;
            float thrustRate = idleDescent ? 0f
                : B737_Performance.IasRateFromN1((float)altitude, (float)CSpeed, N1For(altitude, CVS, CSpeed) / 100f, AccelN1 / 100f);
            rate = Mathf.Max(thrustRate, idlePathRate);
        }
        if (!fullThrustRange)
            rate = Mathf.Min(rate, TrimMaxKtPerMin);
        return Mathf.Clamp(rate, MinSpeedChangeKtPerMin, MaxSpeedChangeKtPerMin);
    }

    float FiniteSpeedRate(bool accelerating, bool fullThrustRange)
    {
        try
        {
            float rate = SpeedChangeKtPerMin(accelerating, fullThrustRange);
            if (float.IsNaN(rate) || float.IsInfinity(rate))
                return MinSpeedChangeKtPerMin;
            return Mathf.Clamp(rate, MinSpeedChangeKtPerMin, MaxSpeedChangeKtPerMin);
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            return MinSpeedChangeKtPerMin;
        }
    }

    /// <summary>
    /// Extra speedbrake drag (as idle VS) beyond its VS delta, flaps up only. FCTM: speedbrakes halve level-flight
    /// deceleration time, so in level flight the speedbrake is worth the clean idle path. Fades to 0 on the config idle path.
    /// </summary>
    static double SpeedbrakeLevelDragVs(double altitude)
    {
        if (!SpeedbrakeFull || Flap_Idx != 0)
            return 0.0;
        double configIdle = ConfigIdleVsAt(altitude, CSpeed);
        double sbDelta = ConfigVsDelta() - GearVsDelta();
        double cleanIdle = configIdle - ConfigVsDelta();
        double extra = Math.Min(0.0, cleanIdle - sbDelta);
        return extra * PowerFraction(CVS, configIdle);
    }

    private void Speed_Equalize()
    {
        if (LevelTestMode.IsActive)
            LevelTestMode.EnforceFlightConfig();

        int rrSpeed = RSpeed;
        try
        {
            EnsureSpeedWindowSynced();
            if (FmcOwnsSpeed())
                ApplyFmcSpeed();
            else if (!LevelTestMode.IsActive && Session.State != null && Session.State.VNAV)
                _fmcScheduleSpeed = ApplyVnavPointSpeedLimit(_fmcScheduleSpeed);
            rrSpeed = RSpeed > increasedSpeed ? RSpeed : increasedSpeed;

            void DrawSpeedTrend(float secondsPerKnot)
            {
                if (SpeedTrend == null || secondsPerKnot < 0.01f || float.IsNaN(secondsPerKnot) || float.IsInfinity(secondsPerKnot))
                    return;
                int direction = CSpeed > rrSpeed + 0.1 ? -1 : (CSpeed < rrSpeed - 0.1 ? 1 : 0);
                SpeedTrend.transform.localScale = new Vector3(1, direction * 10f / secondsPerKnot, 1);
            }

            bool offSpeed = Math.Abs(rrSpeed - CSpeed) > 0.5;
            if (!offSpeed)
            {
                _speedKnotCredit = 0f;
                SpeedTime = 1f;
                DrawSpeedTrend(1f);
            }
            else
            {
                bool accelerating = rrSpeed > CSpeed;
                bool fullRange = RSpeed > CSpeed + 5 || RSpeed < CSpeed - 5;
                float rate = FiniteSpeedRate(accelerating, fullRange);
                float elapsed = _speedStepDelay;
                if (elapsed < 0.05f || float.IsNaN(elapsed) || float.IsInfinity(elapsed))
                    elapsed = 1f;

                // 1 kt costs 60/rate seconds. Bank the fraction and step when a full knot is earned.
                _speedKnotCredit += rate * elapsed / 60f;
                int guard = 0;
                double before = CSpeed;
                while (_speedKnotCredit >= 1f && Math.Abs(rrSpeed - CSpeed) > 0.5 && guard < 8)
                {
                    if (rrSpeed < CSpeed) CSpeed -= 1.0;
                    else CSpeed += 1.0;
                    _speedKnotCredit -= 1f;
                    guard++;
                }
                if (Math.Abs(rrSpeed - CSpeed) <= 0.5)
                    _speedKnotCredit = 0f;
                if (CSpeed != before)
                    SetLabel(txtCSpeed, "" + (int)CSpeed);

                float secondsPerKnot = 60f / rate;
                SpeedTime = secondsPerKnot;
                DrawSpeedTrend(secondsPerKnot);
            }

            if (Move.Instance != null && Session.PlayerAircraft != null)
                SetLabel(txtDTG, Move.Instance.DME().ToString("F1"));

            PFD_Animation pfd = FindPfd();
            if (pfd != null)
            {
                pfd.SpeedTapeUpdate();
                pfd.SpeedIndexUpdate_Click();
            }
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            if (SpeedTime < 0.05f || float.IsNaN(SpeedTime) || float.IsInfinity(SpeedTime))
                SpeedTime = 1f;
        }

        // SpeedTime stays seconds-per-knot (LVL CHG pitch boost reads it). The invoke is capped at 1 s
        // while off speed so the rate is sampled again after the variometer reaches the bleed pitch.
        float poll = SpeedTime;
        if (Math.Abs(rrSpeed - CSpeed) > 0.5)
            poll = Mathf.Min(poll, 1f);
        if (poll < 0.05f || float.IsNaN(poll) || float.IsInfinity(poll))
            poll = 1f;

        _speedStepDelay = poll;
        float multiplier = Session.Settings != null ? Session.Settings.SpeedMultiplier : 1f;
        if (multiplier < 0.01f || float.IsNaN(multiplier) || float.IsInfinity(multiplier))
            multiplier = 1f;
        Invoke("Speed_Equalize", poll / multiplier);
    }
    private void SetAttPitch(int P)
    {
        PFD_Animation PFDScript = FindPfd();
        if (PFDScript != null)
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

    /// <summary>Along-track NM of the leg that ends at destIndex.</summary>
    public static double AlongTrackDistanceOnSegment(RouteScriptableObject route, int destIndex)
    {
        double published = 0;
        if (route?.Points != null && destIndex >= 0 && destIndex < route.Points.Length)
            published = Math.Max(0, route.Points[destIndex].Distance);

        var lines = route?.TracedRoute?.ComputedLines;
        if (lines != null && destIndex >= 1 && destIndex < lines.Length)
        {
            var line = lines[destIndex];
            // A straight leg drops repeated samples, so TracedNMLength collapses to one
            // tick (~0.3 NM) while the vertices still span the whole leg. TOD then
            // sits on the aircraft no matter what Distance says. Use the leg distance.
            if (line != null && line.TracedNMLength > 0.05f &&
                (published < 0.05 || line.TracedNMLength >= published * 0.5))
                return line.TracedNMLength;
        }

        return published;
    }

    /// <summary>
    /// Remaining NM on the current leg. The live distance (the number LEGS counts down)
    /// moves the path. The traced walk is only a fallback: it can stay put while that
    /// distance is still decreasing, which freezes VDI.
    /// </summary>
    public static double RemainingAlongCurrentSegmentNm(RouteScriptableObject route, int passedNodeIndex,
        double cartesianLeft)
    {
        var dest = passedNodeIndex + 1;
        var alongSeg = AlongTrackDistanceOnSegment(route, dest);

        if (cartesianLeft > 0.05)
        {
            var published = route?.Points != null && dest >= 0 && dest < route.Points.Length
                ? Math.Max(1e-4, route.Points[dest].Distance)
                : alongSeg;
            if (published < 1e-4 || alongSeg < 1e-4)
                return cartesianLeft;
            return Math.Max(0, cartesianLeft) * (alongSeg / published);
        }

        var aircraft = Session.PlayerAircraft;
        if (aircraft != null && aircraft.IsOnRoute && aircraft.CurrentSegmentIndex == dest)
            return Math.Max(0, alongSeg - aircraft.NMWalkedOnCurrentSegment);

        return 0;
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
    /// Top LEGS line: first waypoint the FMS shows at or after the active TO index.
    /// </summary>
    public static bool TryGetFmsTopPoint(RouteScriptableObject route, int searchFromIndex,
        out int pointIndex, out double calculatedAltitude)
    {
        pointIndex = -1;
        calculatedAltitude = -1;
        if (route?.Points == null || route.Points.Length == 0)
            return false;

        var start = Mathf.Clamp(searchFromIndex, 0, route.Points.Length - 1);
        for (var i = start; i < route.Points.Length; i++)
        {
            if (!IsFmsLegsWaypoint(route.Points[i]))
                continue;

            var alt = GetPointCalculatedAltitude(route.Points[i]);
            if (alt < 0)
                continue;

            pointIndex = i;
            calculatedAltitude = alt;
            return true;
        }

        return false;
    }

    static bool IsFmsLegsWaypoint(RoutePoint point)
    {
        if (point == null || point.IsSkippable || string.IsNullOrEmpty(point.Name))
            return false;
        if (point.Name == "_START_" || point.Name == "_Position_")
            return false;
        return true;
    }

    static double AlongTrackNmFromStart(RouteScriptableObject route, int pointIndex)
    {
        double walked = 0;
        var last = Mathf.Min(pointIndex, route.Points.Length - 1);
        for (var i = 1; i <= last; i++)
            walked += AlongTrackDistanceOnSegment(route, i);
        return walked;
    }

    /// <summary>First shown waypoint. The leg that arrives there is the 3° leg.</summary>
    static bool TryGetFirstLegWaypoint(RouteScriptableObject route, out int pointIndex, out double calculatedAltitude)
    {
        return TryGetFmsTopPoint(route, 0, out pointIndex, out calculatedAltitude);
    }

    /// <summary>Published NM of the leg that ends at destIndex. Same unit as the LEGS distance.</summary>
    static double PublishedLegNm(RouteScriptableObject route, int destIndex)
    {
        if (route?.Points != null && destIndex >= 0 && destIndex < route.Points.Length
            && route.Points[destIndex].Distance > 0.05f)
            return route.Points[destIndex].Distance;
        return AlongTrackDistanceOnSegment(route, destIndex);
    }

    /// <summary>
    /// NM still to fly on this leg. Uses the live LEGS distance directly.
    /// Scaling it by the traced line shrinks a 1 NM move to a fraction, so the 3° path barely moves.
    /// </summary>
    static double LiveLegRemainingNm(RouteScriptableObject route, int passedNodeIndex, double distanceLeftOnSegment)
    {
        if (distanceLeftOnSegment > 0.05)
            return distanceLeftOnSegment;
        return RemainingAlongCurrentSegmentNm(route, passedNodeIndex, distanceLeftOnSegment);
    }

    /// <summary>
    /// Feet per NM on this leg. The first leg is 3°. Later legs use the calculated-altitude slope.
    /// Positive means the path descends toward the waypoint.
    /// </summary>
    static double LegSlopeFtPerNm(RouteScriptableObject route, int fromIndex, int toIndex)
    {
        if (route?.Points == null || toIndex <= 0 || toIndex >= route.Points.Length)
            return VdiPathFtPerNm;

        if (TryGetFirstLegWaypoint(route, out var firstIndex, out _) && toIndex <= firstIndex)
            return VdiPathFtPerNm;

        var altFrom = GetPointCalculatedAltitude(route.Points[fromIndex]);
        var altTo = GetPointCalculatedAltitude(route.Points[toIndex]);
        var legNm = PublishedLegNm(route, toIndex);
        if (altFrom < 0 || altTo < 0 || legNm < 0.05)
            return 0;
        return (altFrom - altTo) / legNm;
    }

    /// <summary>
    /// Path altitude at the aircraft. First leg: 3° to that waypoint's calculated altitude.
    /// Later legs: that leg's calculated-altitude angle over the distance still on the leg.
    /// </summary>
    static bool TryLegReference(RouteScriptableObject route, int passedNodeIndex, double distanceLeftOnSegment,
        out double referenceAltitude, out double calculatedAltitude, out double distanceNm, out int pointIndex,
        out double slopeFtPerNm)
    {
        referenceAltitude = -1;
        calculatedAltitude = -1;
        distanceNm = 0;
        pointIndex = -1;
        slopeFtPerNm = VdiPathFtPerNm;
        if (route?.Points == null || route.Points.Length < 2)
            return false;

        passedNodeIndex = Mathf.Clamp(passedNodeIndex, 0, route.Points.Length - 2);
        var toIndex = passedNodeIndex + 1;
        var onFirstLeg = TryGetFirstLegWaypoint(route, out var firstIndex, out var firstAlt) && toIndex <= firstIndex;

        if (onFirstLeg)
        {
            pointIndex = firstIndex;
            calculatedAltitude = firstAlt;
            distanceNm = LiveLegRemainingNm(route, passedNodeIndex, distanceLeftOnSegment);
            for (var i = toIndex + 1; i <= firstIndex; i++)
                distanceNm += PublishedLegNm(route, i);
            slopeFtPerNm = VdiPathFtPerNm;
        }
        else
        {
            pointIndex = toIndex;
            calculatedAltitude = GetPointCalculatedAltitude(route.Points[toIndex]);
            distanceNm = LiveLegRemainingNm(route, passedNodeIndex, distanceLeftOnSegment);
            slopeFtPerNm = LegSlopeFtPerNm(route, passedNodeIndex, toIndex);
        }

        if (calculatedAltitude < 0)
            return false;

        referenceAltitude = calculatedAltitude + slopeFtPerNm * Math.Max(0.0, distanceNm);
        return true;
    }

    /// <summary>
    /// reference = waypoint calculated altitude + this leg's slope × remaining NM.
    /// </summary>
    public static bool TryGetReferenceAltitude(RouteScriptableObject route, out double referenceAltitude,
        out double calculatedAltitude, out double distanceNm, out int pointIndex)
    {
        var passed = 0;
        double left = 0;
        if (Session.PlayerAircraft != null && route?.Points != null && route.Points.Length > 1)
        {
            passed = Mathf.Clamp(PositionVirtualNode.PassedNodeIndex, 0, route.Points.Length - 2);
            left = Session.PlayerAircraft.ComputedDistanceLeftOnSegment;
            if (left < 0)
                left = Session.PlayerAircraft.DistanceToNextPoint;
        }

        return TryLegReference(route, passed, left, out referenceAltitude, out calculatedAltitude, out distanceNm,
            out pointIndex, out _);
    }

    /// <summary>
    /// Path at the aircraft: FMS top-point calculated altitude plus a 3° rise back
    /// over the remaining distance (ft = alt + 318 × NM).
    /// </summary>
    public static bool TryComputeVnavPath(RouteScriptableObject route, int passedNodeIndex,
        double distanceLeftOnSegment, double segmentLength, double startAltitude,
        out double pathTarget, out double slopeFtPerNm, out double nextConstraintAlt)
    {
        pathTarget = -1;
        slopeFtPerNm = VdiPathFtPerNm;
        nextConstraintAlt = -1;

        if (!TryLegReference(route, passedNodeIndex, distanceLeftOnSegment,
                out pathTarget, out nextConstraintAlt, out _, out _, out slopeFtPerNm))
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// VDI / VNAV path: 3° back from the FMS top point's calculated altitude.
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
    /// TOD: cruise intersects the 3° path on the first leg, aimed at that leg's
    /// calculated altitude. If 3° needs more NM than the route has, TOD is
    /// clamped to the start of the route. Distances follow the magenta curve.
    /// segmentIndex is the destination node index.
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

        var dToConstraintAtTod = (cruiseAltitude - constraintAlt) / VdiPathFtPerNm;
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

        if (!TryGetFirstLegWaypoint(route, out anchorIndex, out constraintAlt))
            return false;

        if (!IsDescentConstraintAltitude(constraintAlt, cruiseAltitude))
            return false;

        distFromStart = AlongTrackNmFromStart(route, anchorIndex);
        return true;
    }

    /// <summary>True once the aircraft has reached the 3° TOD on the first leg.</summary>
    static bool IsPastFirstLegTod(RouteScriptableObject route, double cruiseAltitude)
    {
        if (route?.Points == null || Session.PlayerAircraft == null)
            return false;
        if (!TryGetFirstLegWaypoint(route, out var firstIndex, out var firstAlt))
            return false;
        if (!IsDescentConstraintAltitude(firstAlt, cruiseAltitude))
            return false;

        var distToFirst = AlongTrackNmFromStart(route, firstIndex);
        var backNm = Math.Clamp((cruiseAltitude - firstAlt) / VdiPathFtPerNm, 0, distToFirst);
        var todFromStart = distToFirst - backNm;

        var passed = Mathf.Clamp(PositionVirtualNode.PassedNodeIndex, 0, route.Points.Length - 2);
        var left = Session.PlayerAircraft.ComputedDistanceLeftOnSegment;
        if (left < 0)
            left = Session.PlayerAircraft.DistanceToNextPoint;
        var remainingToNext = LiveLegRemainingNm(route, passed, left);
        var aircraftFromStart = AlongTrackNmFromStart(route, passed + 1) - remainingToNext;
        return aircraftFromStart >= todFromStart - 0.05;
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
        RouteScriptableObject _route = Session.IsMod ? Session.ModRoute : Session.ActiveRoute;
        if (_route == null) _route = Session.ActiveRoute;

        double reference = CAltitude;
        var haveVnav = _route != null && TryGetReferenceAltitude(_route, out reference, out _, out _, out _);

        var blend = GsPathBlend();
        if (blend > 0f && TryGetIlsGsPathAltitude(out var gsAlt))
        {
            reference = haveVnav ? reference * (1.0 - blend) + gsAlt * blend : gsAlt;
        }
        else if (!haveVnav)
        {
            return;
        }

        // Path relative to the airplane, same sense as a glideslope pointer.
        // Positive: the 3° path is above. Full scale is 400 ft.
        float pathAboveFt = (float)(reference - CAltitude);
        const float fullScaleFt = 400f;
        float pointerY = Mathf.Clamp(pathAboveFt / fullScaleFt, -1f, 1f) * (100f / 125f);
        VDI_Index.transform.localPosition = new Vector2(0.3f, pointerY);

        if (Mathf.Abs(pathAboveFt) > fullScaleFt)
        {
            VDI_Text.text = "" + Mathf.RoundToInt(Mathf.Abs(pathAboveFt));
            VDI_Text.transform.localPosition = new Vector2(0.1f, pathAboveFt > 0f ? 1f : -1f);
        }
        else
            VDI_Text.text = "";
    }
    private int ApplyVnavPointSpeedLimit(int targetSpeed)
    {
        // Level test: pilot sets IAS manually; do not clamp to route RawSpeed.
        if (LevelTestMode.IsActive)
            return targetSpeed;

        var points = Session.ActiveRoute?.Points;
        if (Session.State == null || !Session.State.VNAV || points == null || points.Length == 0)
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
        if (Session.State == null)
            return;

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
        if (node0?.Altitude == null || node1?.Altitude == null || Session.PlayerAircraft == null)
            return;

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

        // Hold below the path until the first-leg 3° TOD. After that, descend to intercept.
        const double belowPathHoldBand = 300;
        var pastEnergyTod = IsPastFirstLegTod(route, StartAltitude);
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
        // Overspeed: raise VS only until idle-path speed equals 360 kt.
        double holdVs = VsHoldingKnots(360);
        if (VNAV_VS < holdVs)
            VNAV_VS = holdVs;
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
            if (route.Points.Length > 1)
            {
                pastTod = IsPastFirstLegTod(route, StartAltitude);
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
    /// GS track after capture: geometric slope plus altitude correction at twice
    /// the previous rate, with a deadband and RVS rate-limit.
    /// </summary>
    private void FlyGlideSlope()
    {
        float glide = Session.CurrentLevel.levelInfo.GlideSlope;
        float geometricVs = -6076f * Mathf.Tan(glide * Mathf.Deg2Rad) * (GS / 60f);

        float altErr = Move.Instance != null
            ? Move.Instance.GsAltitudeDeviation(glide)
            : 0f; // + above path

        // Ignore small errors. Close the rest in ~22 s, twice the previous capture rate.
        const float deadbandFt = 60f;
        const float tauSec = 22.5f;
        const float maxCorrectionFpm = 360f;
        float err = 0f;
        if (Mathf.Abs(altErr) > deadbandFt)
            err = altErr - Mathf.Sign(altErr) * deadbandFt;

        float correction = Mathf.Clamp(err * (60f / tauSec), -maxCorrectionFpm, maxCorrectionFpm);
        float desired = geometricVs - correction;

        // 1 Hz path loop: commanded VS may move twice as fast onto the glideslope.
        const float maxDeltaFpmPerSec = 120f;
        float next = Mathf.MoveTowards(RVS, desired, maxDeltaFpmPerSec);
        VNAV_VS = next;
        RVS = Mathf.RoundToInt(next);
    }

    /// <summary>
    /// First route point whose ID is 0. Later points that also use 0 are left alone.
    /// </summary>
    public static RoutePoint FirstRoutePointWithId(RouteScriptableObject route, int id)
    {
        var points = route?.Points;
        if (points == null)
            return null;

        for (int i = 0; i < points.Length; i++)
        {
            var point = points[i];
            if (point != null && point.ID == id)
                return point;
        }

        return null;
    }

    /// <summary>Exact altitude of the route point with ID 0, or 0 when it has none.</summary>
    public static long RouteIdZeroAltitude(RouteScriptableObject route)
    {
        var point = FirstRoutePointWithId(route, 0);
        if (point?.Altitude == null)
            return 0;

        _ = point.AltitudeRegulation;
        return point.Altitude.RestrictionExact > 0 ? point.Altitude.RestrictionExact : 0;
    }

    public static void SetRouteIdZeroAltitude(RouteScriptableObject route, int altitudeFeet)
    {
        var point = FirstRoutePointWithId(route, 0);
        if (point == null)
            return;

        point.RawAltitude = altitudeFeet > 0
            ? altitudeFeet.ToString(System.Globalization.CultureInfo.InvariantCulture)
            : "";
        if (point.Altitude != null)
            _ = point.AltitudeRegulation;
    }

    static void SetSessionRouteIdZeroAltitude(int altitudeFeet)
    {
        SetRouteIdZeroAltitude(Session.ActiveRoute, altitudeFeet);
        SetRouteIdZeroAltitude(Session.ModRoute, altitudeFeet);
        SetRouteIdZeroAltitude(Session.ModeSetWithPosition, altitudeFeet);
    }

    /// <summary>
    /// Clears the ID 0 altitude when VNAV descent begins (CRZ page shows 00000).
    /// Does not clear <see cref="StartAltitude"/> used by VNAV path math.
    /// </summary>
    public void ClearFmcCruiseAltitudeOnVnavDescentStart()
    {
        if (_fmcCruiseClearedOnVnavDescent)
            return;

        _fmcCruiseClearedOnVnavDescent = true;
        SetSessionRouteIdZeroAltitude(0);

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
    /// New cruise altitude (feet) on the route point with ID 0. Updates TOD and VNAV, redraws the route.
    /// </summary>
    public void ApplyFmcCruiseAltitude(int altitudeFeet)
    {
        altitudeFeet = Mathf.Clamp(altitudeFeet, 0, 41000);
        StartAltitude = altitudeFeet;
        _fmcCruiseClearedOnVnavDescent = altitudeFeet <= 0;
        _resetMcpAltitudePosted = false;

        SetSessionRouteIdZeroAltitude(altitudeFeet);

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
        if (Session.State == null)
            return;

        if (Session.State.HDG) SetLabel(FMA2, "HDG");
        if (Session.State.LNAV) SetLabel(FMA2, "LNAV");
        if (Session.State.LNAVArmed) SetLabel(FMAarmed, "LNAV");


        if (!Session.State.GSCaptured)
        {
            if (Session.State.LC)
            {
                SetLabel(txtRVS, "");
                SetLabel(FMA1, "IDLE");
                SetLabel(FMA3, "MCP SPD");
            }
            if (Session.State.AH)
            {
                SetLabel(txtRVS, "");
                SetLabel(FMA1, "MCP SPD");
                SetLabel(FMA3, "ALT");
            }
            if (Session.State.VS)
            {
                SetLabel(FMA1, "MCP SPD");
                SetLabel(FMA3, "VS");
            }
            if (Session.State.VNAV)
            {
                SetLabel(txtRVS, "");
                SetLabel(FMA1, FmcOwnsSpeed() ? "FMC SPD" : "MCP SPD");
                SetLabel(FMA3, "VNAV");
            }
        }

        else
        {
            SetLabel(txtRVS, "");
            SetLabel(FMA1, "MCP SPD");
            SetLabel(FMA3, "GS");
        }

        // Lateral capture does not replace the active vertical mode.
        if (Session.State.LOCCaptured)
            SetLabel(FMA2, "LOC");

        if (Session.State.GSCaptured)
            SetLabel(FMAarmed, "");
        else if (Session.State.LOCCaptured)
            SetLabel(FMAarmed, "                         GS");
        else if (Session.State.AppArmed)
            SetLabel(FMAarmed, "LOC                 GS");

    }
    private void MatchAltitudes()
    {
        PFD_Animation PFDScript = FindPfd();
        if (PFDScript != null)
        {
            PFDScript.AltUpdate((int)CAltitude);
            PFDScript.CheckAltitudeIndicator(RAltitude, (int)CAltitude);
        }
        if (CAltitude >= TransitionAltitudeFeet) SetLabel(Qnh, "STD");
        else SetLabel(Qnh, "1013");
    }

    static bool SpeedbrakeFull => SBUp;

    /// <summary>Speedbrake/flap N1, FF and pitch. VS also carries the gear; gear N1/FF come from ApplyConfig.</summary>
    static (double n1Pct, double ffMul, double vsFpm, double pitchDeg) ConfigCorrection()
    {
        bool landingFlap = GetFlapSettingDegrees(Flap_Idx) == 30 || GetFlapSettingDegrees(Flap_Idx) == 40;
        var c = landingFlap && SpeedbrakeFull
            ? B738_75T_Corrections.ApplyFlapSB(0, 1, 0, 0)
            : B738_75T_Corrections.Apply(0, 1, 0, 0, SpeedbrakeFull);
        return (c.N1, c.FF, c.VS + GearVsDelta(), c.Pitch);
    }

    static double GearVsDelta() => LGDown ? B738_75T_Corrections.GearVsFpm : 0.0;

    static double ConfigVsDelta() => ConfigCorrection().vsFpm;

    static double IdleVs(int row, int corner) => M[row, corner, 0] + ConfigVsDelta();

    /// <summary>Idle VS used as a divisor. Near-zero chart values become ±1 so pitch stays finite.</summary>
    static double IdleDenom(int row, int corner)
    {
        double v = IdleVs(row, corner);
        if (Math.Abs(v) < 1.0)
            return v < 0 ? -1.0 : 1.0;
        return v;
    }

    static double IdleSpan(int row)
    {
        double span = IdleVs(row, 1) - IdleVs(row, 3);
        return Math.Abs(span) < 1.0 ? 1.0 : span;
    }

    static double ChartAltitude(double altitude)
    {
        if (double.IsNaN(altitude) || double.IsInfinity(altitude) || altitude < 0)
            return 0;
        return altitude > 39900 ? 39900 : altitude;
    }

    /// <summary>Chart row. 8 is sea level, 1 is FL350. Never 0 or 9, so the row above exists.</summary>
    static int ChartBand(double altitude)
    {
        int band = 8 - Mathf.FloorToInt((float)ChartAltitude(altitude) / 5000f);
        return Mathf.Clamp(band, 1, 8);
    }

    /// <summary>Pitch on the chart's pitch/VS line without the gear shift, continued past idle down to the gear idle path.</summary>
    static double PitchOnVs(int row, int levelCorner, double vs)
    {
        int idleCorner = levelCorner + 1;
        row = Mathf.Clamp(row, 0, 8);
        double idleVs = IdleVs(row, idleCorner) - GearVsDelta();
        if (Math.Abs(idleVs) < 1.0)
            return M[row, idleCorner, 1];
        return (M[row, levelCorner, 1] - M[row, idleCorner, 1]) / -idleVs * (vs - idleVs) + M[row, idleCorner, 1];
    }

    /// <summary>Idle-corner value; pitch is taken on the config idle path.</summary>
    static double IdleCornerValue(int row, int idleCorner, int i) =>
        i == 1 ? PitchOnVs(row, idleCorner - 1, IdleVs(row, idleCorner)) : M[row, idleCorner, i];

    /// <summary>0 on the idle path, 1 in level flight or climb.</summary>
    static double PowerFraction(double vs, double idleVs) =>
        idleVs < -1.0 ? Mathf.Clamp01((float)(1.0 - vs / idleVs)) : 1.0;

    static double ConfigIdleVsAt(double altitude, double speed)
    {
        int F = 8 - Mathf.FloorToInt((float)altitude / 5000);
        F = Mathf.Clamp(F, 1, 8);
        double at = IdleVsOnRow(F, speed);
        double above = IdleVsOnRow(F - 1, speed);
        return (at - above) / -5000.0 * (altitude - (8 - F + 1) * 5000.0) + above + ConfigVsDelta();
    }

    /// <summary>
    /// Descent rate of the active path, fpm per knot. GS uses the glideslope angle.
    /// VNAV uses the commanded rate over current groundspeed, which stays put while
    /// both vary together.
    /// </summary>
    static double PathFpmPerKnot(double vsRef)
    {
        if (Session.State != null && Session.State.GSCaptured && Session.CurrentLevel?.levelInfo != null)
        {
            float glide = Session.CurrentLevel.levelInfo.GlideSlope;
            return 6076.0 * Math.Tan(glide * Math.PI / 180.0) / 60.0;
        }

        double gs = GS > 1f ? GS : 1.0;
        return Math.Max(0.5, -vsRef / gs);
    }

    /// <summary>
    /// Speed at which the config idle vertical speed matches a path of fpmPerKnot.
    /// 0 when idle is already at least that steep at 110 kt (do not speed up).
    /// 360 when the path is still steeper than idle there.
    /// </summary>
    static int EquilibriumIdleSpeed(double fpmPerKnot)
    {
        if (fpmPerKnot < 0.2)
            return 0;

        double alt = CAltitude > 39900 ? 39900 : (CAltitude < 0 ? 0 : CAltitude);
        double Gap(double speed) => ConfigIdleVsAt(alt, speed) + fpmPerKnot * speed;

        const double lo = 110;
        const double hi = 360;
        if (Gap(lo) <= 0)
            return 0;
        if (Gap(hi) > 0)
            return (int)hi;

        double a = lo;
        double b = hi;
        for (int n = 0; n < 24; n++)
        {
            double mid = (a + b) * 0.5;
            if (Gap(mid) > 0)
                a = mid;
            else
                b = mid;
        }

        return (int)Math.Round(b);
    }

    /// <summary>Same idle-path speed InterpolateVS stores in increasedSpeed for this VS.</summary>
    static int IdleChartSpeed(double vsRef)
    {
        double altitude = CAltitude > 39900 ? 39900 : (CAltitude < 0 ? 0 : CAltitude);
        int F = 8 - Mathf.FloorToInt((float)altitude / 5000f);
        if (F < 1) F = 1;
        if (F > 8) F = 8;

        double SpeedOnRow(int row)
        {
            double span = IdleVs(row, 1) - IdleVs(row, 3);
            if (Math.Abs(span) < 1.0)
                return M[row, 0, 0];
            return (M[row, 0, 0] - M[row, 2, 0]) / span * (vsRef - IdleVs(row, 3)) + M[row, 2, 0];
        }

        double lower = SpeedOnRow(F);
        double upper = SpeedOnRow(F - 1);
        return (int)((lower - upper) / -5000.0 * (altitude - (8 - F + 1) * 5000.0) + upper);
    }

    /// <summary>Shallowest search result whose idle-path speed is still at or below the cap.</summary>
    static double VsHoldingKnots(int knots)
    {
        double steep = -12000.0;
        double shallow = 4000.0;
        for (int n = 0; n < 30; n++)
        {
            double mid = (steep + shallow) * 0.5;
            if (IdleChartSpeed(mid) > knots)
                steep = mid;
            else
                shallow = mid;
        }
        return shallow;
    }

    static void ApplyConfig(ref double pitchHundredths, ref double n1Hundredths, ref double ff, double power, double altitude, double speed)
    {
        var c = ConfigCorrection();
        pitchHundredths += c.pitchDeg * 100.0;
        n1Hundredths += c.n1Pct * 100.0;
        ff *= c.ffMul;
        if (LGDown)
        {
            var gear = B738_75T_Corrections.Gear(altitude, power, speed);
            n1Hundredths += gear.N1 * 100.0;
            ff *= gear.FFMul;
        }
    }

    public static int FuelFlowFor(double Altitude, double VS, double Speed)
    {
        if (Altitude > 39900) Altitude = 39900;
        if (Altitude < 0) Altitude = 0;

        int F = ChartBand(Altitude);
        int i = 3;// for fuel flow only

        double vv1 = (M[F, 0, i] - M[F, 1, i]) / -IdleDenom(F, 1) * (VS - IdleVs(F, 1)) + M[F, 1, i];
        double vv2 = (M[F, 2, i] - M[F, 3, i]) / -IdleDenom(F, 3) * (VS - IdleVs(F, 3)) + M[F, 3, i];
        double vv3 = (M[F - 1, 0, i] - M[F - 1, 1, i]) / -IdleDenom(F - 1, 1) * (VS - IdleVs(F - 1, 1)) + M[F - 1, 1, i];
        double vv4 = (M[F - 1, 2, i] - M[F - 1, 3, i]) / -IdleDenom(F - 1, 3) * (VS - IdleVs(F - 1, 3)) + M[F - 1, 3, i];
        double s1 = (vv1 - vv2) / (M[F, 0, 0] - M[F, 2, 0]) * (Speed - M[F, 2, 0]) + vv2;
        double s2 = (vv3 - vv4) / (M[F - 1, 0, 0] - M[F - 1, 2, 0]) * (Speed - M[F - 1, 2, 0]) + vv4;
        int ff = (int)((s1 - s2) / -5000f * (Altitude - (8 - F + 1) * 5000) + s2);
        double pitch = 0, n1 = 0, ffOut = ff;
        ApplyConfig(ref pitch, ref n1, ref ffOut, PowerFraction(VS, ConfigIdleVsAt(Altitude, Speed)), Altitude, Speed);
        return ApplyOperationalFf((int)ffOut, Altitude);
    }

    static double AltBandLerp(double atBand, double atBandAbove, int band, double altitude)
    {
        return (atBand - atBandAbove) / -5000.0 * (altitude - (8 - band + 1) * 5000) + atBandAbove;
    }

    static int ApplyOperationalFf(int ff, double altitude)
    {
        if (ff <= 210)
            return ff;

        double factor = 1.0 + 0.03 * (IsaDeviationC / 10.0);
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

        int F = ChartBand(Altitude);
        int i = 2;// for N1 only

        double vv1 = (M[F, 0, i] - M[F, 1, i]) / -IdleDenom(F, 1) * (VS - IdleVs(F, 1)) + M[F, 1, i];
        double vv2 = (M[F, 2, i] - M[F, 3, i]) / -IdleDenom(F, 3) * (VS - IdleVs(F, 3)) + M[F, 3, i];
        double vv3 = (M[F - 1, 0, i] - M[F - 1, 1, i]) / -IdleDenom(F - 1, 1) * (VS - IdleVs(F - 1, 1)) + M[F - 1, 1, i];
        double vv4 = (M[F - 1, 2, i] - M[F - 1, 3, i]) / -IdleDenom(F - 1, 3) * (VS - IdleVs(F - 1, 3)) + M[F - 1, 3, i];
        double s1 = (vv1 - vv2) / (M[F, 0, 0] - M[F, 2, 0]) * (Speed - M[F, 2, 0]) + vv2;
        double s2 = (vv3 - vv4) / (M[F - 1, 0, 0] - M[F - 1, 2, 0]) * (Speed - M[F - 1, 2, 0]) + vv4;
        double n1 = (s1 - s2) / -5000f * (Altitude - (8 - F + 1) * 5000) + s2;
        double pitch = 0, ff = 1;
        ApplyConfig(ref pitch, ref n1, ref ff, PowerFraction(VS, ConfigIdleVsAt(Altitude, Speed)), Altitude, Speed);
        return (int)n1;
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
        if (Session.Settings == null || co == null)
            return;

        int engines = EnginesRunning <= 1 ? 1 : 2;
        double burned = (double)FF * engines / 3600 * Session.Settings.SpeedMultiplier;
        totalFuel -= burned;
        SetLabel(txtTotalFuel, "" + System.Math.Round(totalFuel / 100, 2));
        RefreshFuelPenaltyLabel();

        CMach = Speed2Mach(CSpeed, CAltitude);
        SetLabel(txtMach, CMach > 0.4 ? FormatMach(CMach) : "GS " + GS);

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

        Flap_Idx = Mathf.Clamp(Flap_Idx, 0, MaxFlapIdx);

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

        PFD_Animation PFDScript = FindPfd();
        if (PFDScript != null)
            PFDScript.Flaps_Indexchange(Flap_Idx);
        minimumSpeed = FlapMinSpeeds[Flap_Idx];
        if (FmcOwnsSpeed())
        {
            ApplyFmcSpeed();
            RefreshSpeedWindow();
        }
    }

    bool CanSelectFlap(int flapIdx)
    {
        return CSpeed <= PFD_Animation.FlapSelectLimitKnots(flapIdx);
    }
    public static bool GearLeverLocked()
    {
        return CSpeed > 270 || Speed2Mach(CSpeed, CAltitude) > 0.82;
    }

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
    }

    public void SetSB(bool Up, bool fromUI)
    {
        if (SBUp == !Up) return;
        if (fromUI) SBUp = !Up;
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
        var mcp = UYServiceLocator.Get<McpUI>();
        if (mcp != null)
            mcp.RefreshHS();
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

        if (Calculator.Instance == null)
            return;

        if (Calculator.Instance.co != null && Calculator.Instance.co.isOn)                                    //Mach
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
        PFD_Animation pfdAnimation = FindPfd();
        if (pfdAnimation == null)
            return;

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

        var mcp = UYServiceLocator.Get<McpUI>();
        if (mcp != null)
            mcp.RefreshHS();
    }

    public void OnClick_S(bool positive)
    {
        if (FmcOwnsSpeed())
            SpeedWindowOpen = true;

        if (co != null && co.isOn) //Mach
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
        if (windArrow != null)
            windArrow.transform.localEulerAngles = new Vector3(0, 0, 180 - we.relativeWindD);
        if (windTxt != null)
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
        var items = CurrentWindTable?.WindInfoItems;
        if (items == null || items.Length < 2)
            return 0;

        altitude = Math.Max(0, altitude);

        int maxIndex = items.Length - 1;

        int baseAlt = maxIndex - (int)(altitude / 5000.0);
        baseAlt = Mathf.Clamp(baseAlt, 1, maxIndex);

        double frac = (altitude % 5000.0) / 5000.0;

        float deg1 = Mathf.Repeat((float)items[baseAlt].Degrees, 360f);
        float deg2 = Mathf.Repeat((float)items[baseAlt - 1].Degrees, 360f);

        float result = Mathf.LerpAngle(deg1, deg2, (float)frac);

        return (int)result;
    }
    public static int GetWindMagnitude(double altitude)
    {
        var items = CurrentWindTable?.WindInfoItems;
        if (items == null || items.Length < 2)
            return 0;

        altitude = Math.Max(0, altitude);

        int maxIndex = items.Length - 1;

        int baseAlt = maxIndex - (int)(altitude / 5000.0);
        baseAlt = Mathf.Clamp(baseAlt, 1, maxIndex);

        double frac = (altitude % 5000.0) / 5000.0;

        float kts1 = items[baseAlt].Knots;
        float kts2 = items[baseAlt - 1].Knots;

        float result = Mathf.Lerp(kts1, kts2, (float)frac);

        result = Mathf.Max(0, result);

        return (int)result;
    }
    public static WindElements CalculateWindElements(double Altitude, double IAS, int Track)
    {
        if (Altitude > 39900) Altitude = 39900;
        if (Altitude < 0 || double.IsNaN(Altitude)) Altitude = 0;

        WindElements WE = new WindElements();

        WE.WindD = GetWindDirection(Altitude);
        WE.WindM = GetWindMagnitude(Altitude);

        WE.relativeWindD = WE.WindD - Track;

        double HeadWind = Mathf.Cos(WE.relativeWindD * Mathf.Deg2Rad) * WE.WindM;
        double CrossWind = Mathf.Sin(WE.relativeWindD * Mathf.Deg2Rad) * WE.WindM;


        WE.TAS = (int)(IAS + (Altitude / 1000 * 0.02 * IAS));

        int gs = (int)(WE.TAS - HeadWind);
        WE.GS = gs < 1 ? 1 : gs;
        WE.HeadingWindAddition = (int)(Mathf.Atan((float)(CrossWind / WE.GS)) * Mathf.Rad2Deg);


        return WE;
    }
    private bool _approachEndTriggered;

    public void CheckStabilization()
    {
        if (_approachEndTriggered)
            return;

        if (Move.Instance == null)
            return;

        // Level test finishes at RW (DME), not via the normal short-final dialog.
        if (LevelTestMode.IsActive)
            return;

        // CLEAR ILS is already out. Leaving the final course by 10 NM ends the level.
        // Once LOC is captured, the airplane is established and may turn back onto the line.
        if (Move.Instance.IlsArmCommandIssued
            && (Session.State == null || !Session.State.LOCCaptured)
            && Move.Instance.FinalApproachCrossTrackNm() >= 10f)
        {
            _approachEndTriggered = true;
            ApproachStatusDialog.Show("APP NOT ARMED", "", "Exit", () =>
            {
                Aircraft aircraft = FindAnyObjectByType<Aircraft>();
                if (aircraft != null)
                {
                    aircraft.FinishGame(isStable: false);
                    CAltitude = 10000;
                }
            });
            return;
        }

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

}

public class SpeedWindowClickRelay : MonoBehaviour, IPointerClickHandler
{
    public void OnPointerClick(PointerEventData eventData)
    {
        if (Calculator.Instance != null)
            Calculator.Instance.OnSpeedWindowClicked();
    }
}
