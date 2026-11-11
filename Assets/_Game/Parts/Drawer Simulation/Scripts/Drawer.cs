using System;
using System.Collections.Generic;
using UnityEngine;
using Lean.Pool;
using Gamelogic.Extensions;
using Navigation;
using TMPro;
using Unyawn.Utils;
using Lean.Common.Examples;
using Gamelogic.Extensions.Internal;

public class Drawer : MonoBehaviour
{
    [SerializeField] private GameConfigScriptableObject _gameConfig;

    public Animator cameraAnimator;
    [Space][SerializeField] private Transform dynamicHolder;
    [SerializeField] private Transform dynamicHolderMod;
    [SerializeField] private Transform dynamicHolderCircles;
    [SerializeField] private Transform dynamicHolderRays;
    [SerializeField] private Transform dynamicHolderOtheriarcrafts;

    [Space][SerializeField] private LeanGameObjectPool linesPool;
    [SerializeField] private LeanGameObjectPool linesPoolMod;
    [SerializeField] private LeanGameObjectPool circlePool;
    [SerializeField] private LeanGameObjectPool rayPool;
    [SerializeField] private LeanGameObjectPool otherAircraftsPool;

    [Space][SerializeField] private Transform pivot;
    [SerializeField] private Transform compasPivot;
    [SerializeField] private Transform mobilePlaneIndicatorPivot;
    [SerializeField] private RadialIndicators radialPivots;
    [SerializeField] private Transform _freeFlightPivot;
    [SerializeField] private Transform _windPivot;
    [SerializeField] private Transform bananaIndicatorPivot;
    [SerializeField] private TMP_Text _reference40;
    [SerializeField] private TMP_Text _reference80;

    [SerializeField] private TMP_Text _Mapreference40;

    [Space][SerializeField] private GameObject rangelineHolder;

    [Header("modes visuals")]
    [SerializeField] private GameObject[] mapHolder;
    [SerializeField] private GameObject[] centerHolder;
    [SerializeField] private GameObject[] planHolder;

    // Plan/Center rose sits slightly left of the ND center. Vertical nudge is
    // small; the previous full mask offset (-1.67) dropped the rose too far.
    const float PlanCenterPivotX = 0.39f;
    const float PlanCenterPivotY = -0.4f;
    static readonly Vector3 MapMaskLocalPosition = new Vector3(0f, -1.6686f, 0f);

    Transform _ndMask;

    private void Awake()
    {
        Session.ZoomMultiplier = _gameConfig.Settings.StartingZoom;
        UYServiceLocator.Register(this);

        var pauseOverlay = GetComponent<NdPauseOverlay>();
        if (pauseOverlay == null)
            pauseOverlay = gameObject.AddComponent<NdPauseOverlay>();
        pauseOverlay.Initialize(pivot, _reference40);

        if (pivot != null)
            _ndMask = pivot.Find("Compas/Masking");
    }

    private void LateUpdate()
    {
        if (pivot == null || Session.State == null)
            return;

        // Animator write-defaults puts the pivot back at the origin in these modes.
        bool centeredRose = Session.State.MapMode != MapMode.Map;
        if (centeredRose)
            pivot.localPosition = new Vector3(PlanCenterPivotX, PlanCenterPivotY, 0.12f);

        if (_ndMask == null)
            return;

        // Keep the round clip on the bezel while the rose moves.
        _ndMask.localPosition = centeredRose
            ? MapMaskLocalPosition - new Vector3(PlanCenterPivotX, PlanCenterPivotY, 0f)
            : MapMaskLocalPosition;
    }

    private void Start()
    {
        if (Session.State != null)
            Session.State.OnMapModeChanged += OnUIMapModeSet;
    }

    // todo daniel - move this through session.State
    public void ResetMode()
    {
        if (Session.State == null)
            return;

        Session.State.AutoSetMapMode(MapMode.Map);
        OnUIMapModeSet(Session.State.MapMode);
    }


    public void OnUIMapModeSet(MapMode mode)
    {
        switch (mode)
        {
            case MapMode.Map:
                if (cameraAnimator != null)
                    cameraAnimator.SetTrigger("Map");
                break;
            case MapMode.Center:
                if (cameraAnimator != null)
                    cameraAnimator.SetTrigger("Center");
                break;
            case MapMode.Plan:
                if (cameraAnimator != null)
                    cameraAnimator.SetTrigger("Plan");
                break;
        }
        ShowCurrentMode();
        ShowRangeLine();
    }



    private void SetModeHolders(GameObject[] holders, MapMode mode)
    {
        if (holders == null || Session.State == null)
            return;

        for (var i = 0; i < holders.Length; i++)
        {
            if (holders[i] != null)
                holders[i].SetActive(Session.State.MapMode == mode);
        }
    }

    private void ShowCurrentMode()
    {
        if (Session.State == null)
            return;

        SetModeHolders(mapHolder, MapMode.Map);
        SetModeHolders(centerHolder, MapMode.Center);
        SetModeHolders(planHolder, MapMode.Plan);

        if (radialPivots != null)
            radialPivots.RefreshScale();

        Clear();
        Display();
    }

    private void ShowRangeLine()
    {
        if (rangelineHolder == null || Session.State == null)
            return;

        rangelineHolder.SetActive(Session.State.MapMode != MapMode.Plan);
    }

    public void OnZoomIn()
    {
        Clear();
        //Session.ZoomMultiplier *= 2f;
        //Debug.Log($"Zoom : {Session.ZoomMultiplier}");
        Session.ZoomMultiplier = Mathf.Clamp(Session.ZoomMultiplier * 2f, 0.5f, 16f);
        Display();
        RefreshDisplayDistances();
    }

    public void OnZoomOut()
    {
        Clear();
        //Session.ZoomMultiplier /= 2f;
        Session.ZoomMultiplier = Mathf.Clamp(Session.ZoomMultiplier / 2f, 0.5f, 16f);
        Display();
        RefreshDisplayDistances();
    }

    public void Clear()
    {
        Extension.DespawnChildren<LineDrawer>(dynamicHolder, linesPool);
        Extension.DespawnChildren<LineDrawer>(dynamicHolderMod, linesPoolMod);
        Extension.DespawnChildren<FixedCircleDrawer>(dynamicHolderCircles, circlePool);
        Extension.DespawnChildren<FixedRayDrawer>(dynamicHolderRays, rayPool);
        Extension.DespawnChildren<OtherAircrafIndicator>(dynamicHolderOtheriarcrafts, otherAircraftsPool);
    }

    public static bool GetCircleFix(RoutePoint linkedPoint, Vector3 from, FixedPointInfo linkedInfo,
        out FixCircle circle)
    {
        if (linkedInfo == null)
        {
            circle = new FixCircle(linkedPoint, linkedInfo);
            circle.Init(from, true);
            return true;
        }

        if (linkedInfo.NM != null)
        {
            circle = new FixCircle(linkedPoint, linkedInfo);
            circle.Init(from);
            return true;
        }

        circle = null;
        return false;
    }

    public static bool GetRayFix(RoutePoint linkedPoint, Vector3 from, FixedPointInfo linkedInfo, out FixRay ray)
    {
        if (linkedInfo?.RawDegrees != null)
        {
            ray = new FixRay(linkedPoint, linkedInfo);
            ray.Init(from);
            return true;
        }

        ray = null;
        return false;
    }

    public void RefreshDisplayDistances()
    {

        var zoomDistance = Math.Round(80f / Session.ZoomMultiplier / 2f, 1);
        if (_reference40 != null)
            _reference40.text = zoomDistance.ToString();
        if (_Mapreference40 != null)
            _Mapreference40.text = zoomDistance.ToString();
        if (_reference80 != null)
            _reference80.text = (zoomDistance * 2).ToString();

    }

    public void Display()
    {
        DisplaySet(Session.ActiveRoute?.TracedRoute?.ComputedLines, LinesType.Active);
        if (Session.ModRoute != null)
        {
            DisplaySet(Session.ModeSetWithPosition?.TracedRoute?.ComputedLines, LinesType.Mod);
        }

        DisplayFixCircles();
        DisplayFixRays();
        if (radialPivots != null)
            radialPivots.RefreshZoomAndHDG();
        if (Calculator.Instance != null && bananaIndicatorPivot != null)
        {
            float BananaPos = Calculator.Instance.GetBananaPosition;
            bananaIndicatorPivot.SetLocalY(BananaPos);
            if (BananaPos == 0) bananaIndicatorPivot.SetLocalY(-1000);
        }
        //Debug.Log(BananaPos);
        DisplayOtherTraffic();

        DisplayRotations();

        DisplayTopOfDescentMarker();
    }

    private void DisplayRotations()
    {
        if (!Session.IsRunning || Session.State == null || Session.PlayerAircraft == null)
        {
            return;
        }

        switch (Session.State.MapMode)
        {
            case MapMode.Center:
            case MapMode.Map:
                compasPivot?.SetLocalRotationZ(Session.PlayerAircraft.DisplayHeadingDegrees);
                _freeFlightPivot?.SetLocalRotationZ(Session.PlayerAircraft.DisplayHeadingDegrees - Calculator.RHeading - 1);
                mobilePlaneIndicatorPivot?.SetLocalRotationZ(0);
                break;
            case MapMode.Plan:
                compasPivot?.SetLocalRotationZ(0);
                mobilePlaneIndicatorPivot?.SetLocalRotationZ(-Session.PlayerAircraft.DisplayHeadingDegrees);

                if (Session.State.HDG)
                    _freeFlightPivot?.SetLocalRotationZ(-Calculator.RTrack);

                break;

            default:
                return;
        }
        _windPivot?.SetLocalRotationZ(-Calculator.CHeadingWindAddition);

        if (mobilePlaneIndicatorPivot != null)
            mobilePlaneIndicatorPivot.localPosition = Session.PlayerAircraft.NMPosition.ToDisplay();
    }

    private void DisplayOtherTraffic()
    {
        if (Move.Instance == null || otherAircraftsPool == null)
            return;

        var positions = Move.Instance.ACPositions;
        var texts = Move.Instance.ACTexts;
        if (positions == null)
            return;

        foreach (var key in positions.Keys)
        {
            var spawned = otherAircraftsPool.Spawn(Vector3.zero, Quaternion.identity, dynamicHolderOtheriarcrafts);
            if (spawned == null)
                continue;
            var drawer = spawned.GetComponent<OtherAircrafIndicator>();
            if (drawer == null)
                continue;
            drawer.name = key;
            string label = texts != null && texts.ContainsKey(key) ? texts[key] : "";
            drawer.Init(label, Color.white, 0f, 45f);
            drawer.transform.localPosition = positions[key].ToDisplay();
        }
    }



    private void DisplaySet(IReadOnlyList<TracedLine> lines, LinesType linesType)
    {
        //Debug.Log("LinesType: "+linesType.ToString());
        if (lines == null)
        {
            return;
        }

        LeanGameObjectPool pool;
        Transform holder;

        switch (linesType)
        {
            case LinesType.Mod:
                pool = linesPoolMod;
                holder = dynamicHolderMod;
                break;
            case LinesType.Active:
            case LinesType.Rejoin:
                pool = linesPool;
                holder = dynamicHolder;
                break;
            default:
                return;
        }

        if (pool == null)
            return;

        for (var i = 0; i < lines.Count; i++)
        {
            var hiddenLabel = false;
            var hiddenLine = false;
            var fromPointIndex = 0;
            var line = lines[i];

            if (line == null)
            {
                continue;
            }

            var point = line.LinkedPoint;
            if (point == null)
                continue;

            var spawned = pool.Spawn(Vector3.zero, Quaternion.identity, holder);
            if (spawned == null)
                continue;
            var drawer = spawned.GetComponent<LineDrawer>();
            if (drawer == null)
                continue;
            drawer.name = $"{linesType} {point.Name}";

            if (linesType == LinesType.Mod
                && Session.ActiveRoute != null
                && Session.ActiveRoute.GetPoint(point.ID, out var activePoint, out _)
                && activePoint != null)
            {
                if (RoutePoint.HaveSamePosition(activePoint, point))
                {
                    hiddenLabel = true;
                }
                else
                {
                    // $^% error at cartesian position
                    Debug.LogWarning(activePoint.Name + " " +
                                     (activePoint.CartesianPosition - point.CartesianPosition).magnitude);
                }
            }

            if (linesType == LinesType.Rejoin && line.StartNMPosition == Vector2.zero)
            {
                hiddenLine = true;
            }

            drawer.Display(line, point, hiddenLabel || linesType == LinesType.Rejoin, hiddenLine, fromPointIndex);
        }
    }

    private void DisplayTopOfDescentMarker()
    {
        var route = TodDisplayRoute();
        if (route == null || route.Points == null || route.Points.Length < 2)
        {
            return;
        }

        var cruiseAltitude = ResolveCruiseAltitude(route);
        if (!Calculator.TryFindVdiCenterCrossing(route, cruiseAltitude, out var topOfDescentPosition,
                out var segmentIndex, out var segmentT))
        {
            return;
        }

        // Snap to the drawn leg (includes turn curves) so TOD stays on the magenta path.
        if (route.TracedRoute != null &&
            route.TracedRoute.TryGetPositionOnLine(segmentIndex, segmentT, out var onDrawnRoute))
        {
            topOfDescentPosition = onDrawnRoute;
        }

        var todMarker = otherAircraftsPool.Spawn(Vector3.zero, Quaternion.identity, dynamicHolderOtheriarcrafts)
            .GetComponent<OtherAircrafIndicator>();
        todMarker.name = "Top Of Descent";
        todMarker.Init("●TOD", Color.green, 0f);
        todMarker.transform.localPosition = topOfDescentPosition.ToDisplay();
    }

    /// <summary>
    /// TOD sits on the drawn magenta path: MOD preview while pending EXEC, else active.
    /// </summary>
    private static RouteScriptableObject TodDisplayRoute()
    {
        if (Session.IsMod)
        {
            if (Session.ModeSetWithPosition?.Points != null && Session.ModeSetWithPosition.Points.Length >= 2)
            {
                return Session.ModeSetWithPosition;
            }

            if (Session.ModRoute?.Points != null && Session.ModRoute.Points.Length >= 2)
            {
                return Session.ModRoute;
            }
        }

        return Session.VisibleRoute ?? Session.ActiveRoute;
    }

    private static double ResolveCruiseAltitude(RouteScriptableObject route)
    {
        // Route point ID 0 altitude drives TOD. Cleared to 0 when VNAV descent starts.
        return Calculator.RouteIdZeroAltitude(route);
    }

    private void DisplayFixCircles()
    {
        var traced = Session.ActiveRoute?.TracedRoute;
        if (traced == null || circlePool == null)
            return;

        var circles = traced.ComputedCircles;
        var pool = circlePool;
        var holder = dynamicHolderCircles;

        if (circles == null)
        {
            return;
        }

        for (var i = 0; i < circles.Count; i++)
        {
            var line = circles[i];

            if (line?.LinkedPoint == null)
            {
                continue;
            }

            var point = line.LinkedPoint;

            var spawned = pool.Spawn(Vector3.zero, Quaternion.identity, holder);
            if (spawned == null)
                continue;
            var drawer = spawned.GetComponent<FixedCircleDrawer>();
            if (drawer == null)
                continue;
            drawer.name = $"{line.GetName} {point.Name}";
            drawer.Display(line);
        }
    }

    private void DisplayFixRays()
    {
        var traced = Session.ActiveRoute?.TracedRoute;
        if (traced == null || rayPool == null)
            return;

        var rays = traced.ComputedRays;
        var pool = rayPool;
        var holder = dynamicHolderRays;

        if (rays == null)
        {
            return;
        }

        for (var i = 0; i < rays.Count; i++)
        {
            var ray = rays[i];

            if (ray?.LinkedPoint == null)
            {
                continue;
            }

            var point = ray.LinkedPoint;

            var spawned = pool.Spawn(Vector3.zero, Quaternion.identity, holder);
            if (spawned == null)
                continue;
            var drawer = spawned.GetComponent<FixedRayDrawer>();
            if (drawer == null)
                continue;
            drawer.name = $"{ray.GetName} {point.Name}";
            drawer.Display(ray);
        }
    }


    private void OnDrawGizmos()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        Gizmos.color = Color.green;
        var up = pivot.up;
        var r = 0.1f;
        Gizmos.DrawSphere(pivot.position + up * Session.Settings.MapReferenceLength80, r);

        Gizmos.color = Color.blue;
        Gizmos.DrawSphere(pivot.position + up * Session.Settings.PlanReferenceLength80, r);

        Gizmos.color = Color.cyan;
        Gizmos.DrawSphere(pivot.position + up * Session.Settings.PlanReferenceLength80 / 2f, r);


        // if (GameManager.Instance.ActiveRoute?.PathLines?.ComputedLines == null)
        // {
        //     return;
        // }
        //
        // Gizmos.DrawSphere(Extension.ToDisplay(GameManager.Instance.Aircraft.PositionFreeOrOnCurvedPath), 0.05f);
    }
}