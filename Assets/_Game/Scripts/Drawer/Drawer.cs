using System;
using System.Collections.Generic;
using UnityEngine;
using Lean.Pool;
using Gamelogic.Extensions;
using Navigation;
using Unyawn.Utils;

public class Drawer : MonoBehaviour 
{
    [SerializeField] private GameConfigScriptableObject _gameConfig;

    public Animator cameraAnimator;
    [Space] [SerializeField] private Transform dynamicHolder;
    [SerializeField] private Transform dynamicHolderMod;
    [SerializeField] private Transform dynamicHolderCircles;
    [SerializeField] private Transform dynamicHolderRays;
    [SerializeField] private Transform dynamicHolderOtheriarcrafts;

    [Space] [SerializeField] private LeanGameObjectPool linesPool;
    [SerializeField] private LeanGameObjectPool linesPoolMod;
    [SerializeField] private LeanGameObjectPool circlePool;
    [SerializeField] private LeanGameObjectPool rayPool;
    [SerializeField] private LeanGameObjectPool otherAircraftsPool;

    [Space] [SerializeField] private Transform pivot;
    [SerializeField] private Transform compasPivot;
    [SerializeField] private Transform mobilePlaneIndicatorPivot;
    [SerializeField] private Transform freeFlightPivot;
    [SerializeField] private Transform bananaIndicatorPivot;

    [Header("modes visuals")] 
    [SerializeField] private GameObject[] mapHolder;
    [SerializeField] private GameObject[] centerHolder;
    [SerializeField] private GameObject[] planHolder;


    public Color CMagenta => _gameConfig.Settings.cMagenta;
    public Color CLightYellow => _gameConfig.Settings.cLightYellow;

   
    [SerializeField] private float _debugStarDistance = 1.3f;

    private const bool WalkOnMod = false;

    private void Awake()
    {
        Session.ZoomMultiplier = _gameConfig.Settings.StartingZoom;
        UYServiceLocator.Register(this);
        var mcpUI = UYServiceLocator.Get<McpUI>();
        mcpUI.OnCenterModeSet += OnUICenterModeSet;
        mcpUI.OnMapModeSet += OnUIMapModeSet;
        mcpUI.OnPlanModeSet += OnUIPlanModeSet;
    }

    private void OnDestroy()
    {
        var mcpUI = UYServiceLocator.Get<McpUI>();
        mcpUI.OnCenterModeSet -= OnUICenterModeSet;
        mcpUI.OnMapModeSet -= OnUIMapModeSet;
        mcpUI.OnPlanModeSet -= OnUIPlanModeSet;
    }

    public void ResetMode()
    {
        Session.Mode = DrawerMode.Map;
        OnUIMapModeSet();
    }


    private void OnUIMapModeSet()
    {
        cameraAnimator.SetTrigger("Map");
        ShowCurrentMode();
    }

    private void OnUICenterModeSet()
    {
        cameraAnimator.SetTrigger("Center");
        ShowCurrentMode();
    }

    private void OnUIPlanModeSet()
    {
        cameraAnimator.SetTrigger("Center");
        ShowCurrentMode();
    }


    private void ShowCurrentMode()
    {
        foreach (var x in mapHolder)
        {
            x.SetActive(Session.Mode == DrawerMode.Map);
        }

        foreach (var x in centerHolder)
        {
            x.SetActive(Session.Mode == DrawerMode.Center);
        }

        foreach (var x in planHolder)
        {
            x.SetActive(Session.Mode == DrawerMode.Plan);
        }

        Clear();
        Display();
    }

    public void OnZoomIn()
    {
        Clear();
        Session.ZoomMultiplier += 0.2f;
        Display();
    }

    public void OnZoomOut()
    {
        Clear();
        Session.ZoomMultiplier -= 0.2f;
        Display();
    }

    public void Clear()
    {
        Extension.DespawnChildren<LineDrawer>(dynamicHolder, linesPool);
        Extension.DespawnChildren<LineDrawer>(dynamicHolderMod, linesPoolMod);
        Extension.DespawnChildren<FixedCircleDrawer>(dynamicHolderCircles, circlePool);
        Extension.DespawnChildren<FixedRayDrawer>(dynamicHolderRays, rayPool);
        Extension.DespawnChildren<OtherAircrafIndicator>(dynamicHolderOtheriarcrafts, otherAircraftsPool);
    }

    // public static bool GetCircleFix(RoutePoint linkedPoint, Vector3 from, FixedPointInfo linkedInfo,
    //     out FixCircle circle)
    // {
    //     if (linkedInfo.NM != null)
    //     {
    //         circle = new FixCircle(linkedPoint, linkedInfo);
    //         circle.Init(from);
    //         return true;
    //     }
    //
    //     circle = null;
    //     return false;
    // }
    //
    // public static bool GetRayFix(RoutePoint linkedPoint, Vector3 from, FixedPointInfo linkedInfo, out FixRay ray)
    // {
    //     if (linkedInfo.RawDegrees != null)
    //     {
    //         ray = new FixRay(linkedPoint, linkedInfo);
    //         ray.Init(from);
    //         return true;
    //     }
    //
    //     ray = null;
    //     return false;
    // }

    public void Display()
    {
        DisplaySet(Session.ActiveRoute?.TracedRoute.ComputedLines, LinesType.Active);
        if (Session.ModRoute != null)
        {
            DisplaySet(Session.ModeSetWithPosition?.TracedRoute?.ComputedLines, LinesType.Mod);
        }

        DisplayFixCircles();
        DisplayFixRays();
        freeFlightPivot.gameObject.SetActive(Session.PlayerAircraft.IsFreeFlight);
        bananaIndicatorPivot.SetLocalY(Calculator.Instance.GetBananaPosition);

        if (Session.PlayerAircraft.IsRejoining && Session.PlayerAircraft.RejoinPathLines != null)
        {
            DisplaySet(Session.PlayerAircraft.RejoinPathLines.ComputedLines,  LinesType.Rejoin);
        }
        
        DisplayOtherTraffic();

        DisplayRotations();
        
    }

    private void DisplayRotations()
    {
        switch (Session.Mode)
        {
            case DrawerMode.Center:
            case DrawerMode.Map:
                //rotate compass
                compasPivot.SetLocalRotationZ(Session.PlayerAircraft.DisplayHeadingDegrees);
                if (Session.PlayerAircraft.IsFreeFlight)
                {
                    freeFlightPivot.SetLocalRotationZ(Session.PlayerAircraft.DisplayHeadingDegrees - Calculator.RHeading);
                }

                break;
            case DrawerMode.Plan:
                //rotate compass
                compasPivot.SetLocalRotationZ(0);
                mobilePlaneIndicatorPivot.position =
                    Session.PlayerAircraft.NMPosition.ToDisplay();
                mobilePlaneIndicatorPivot.SetLocalRotationZ(-Session.PlayerAircraft.DisplayHeadingDegrees);
                break;
            case DrawerMode.Suspeded:
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    private void DisplayOtherTraffic()
    {
        var positions = Move.Instance.ACPositions;
        var texts = Move.Instance.ACTexts;

        foreach (var key in positions.Keys)
        {
            // Debug.Log(_positions[_key]);
            var drawer = otherAircraftsPool.Spawn(Vector3.zero, Quaternion.identity, dynamicHolderOtheriarcrafts)
                .GetComponent<OtherAircrafIndicator>();
            drawer.name = key;
            drawer.Init(texts[key], Color.yellow);
            drawer.transform.localPosition = Extension.ToDisplay(positions[key]);
        }

        // demo - shows a debug star for seeing the distance
        var objective = otherAircraftsPool.Spawn(Vector3.zero, Quaternion.identity, dynamicHolderOtheriarcrafts)
            .GetComponent<OtherAircrafIndicator>();
        objective.name = "My objective";
        objective.Init("|", Color.yellow);
        objective.transform.localPosition = Extension.ToDisplay((Session.PlayerAircraft.NMPosition +
                                                                 Vector2.right * _debugStarDistance));

        // demo - shows a debug star for seeing the distance
        objective = otherAircraftsPool.Spawn(Vector3.zero, Quaternion.identity, dynamicHolderOtheriarcrafts)
            .GetComponent<OtherAircrafIndicator>();
        objective.name = "origin";
        objective.Init("o", Color.blue);
        objective.transform.localPosition = Extension.ToDisplay(Vector2.zero);
    }

 

    private void DisplaySet(IReadOnlyList<TracedLine> lines, LinesType linesType)
    {
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
                throw new ArgumentOutOfRangeException(nameof(linesType), linesType, null);
        }

        var rejoinSegmentIndex = 0;
        var rejoinPoint = Vector2.zero;
        if (Session.PlayerAircraft.IsRejoining)
        {
            rejoinSegmentIndex = Session.PlayerAircraft.CachedExitSegmentOfHeadingRejoinIntersection;
            rejoinPoint = Session.PlayerAircraft.CachedExitPointFromHeading;
        }

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

            var drawer = pool.Spawn(Vector3.zero, Quaternion.identity, holder).GetComponent<LineDrawer>();
            drawer.name = $"{linesType} {point.Name}";

            if (linesType == LinesType.Mod && Session.ActiveRoute.GetPoint(point.ID, out var activePoint))
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

    private void DisplayFixCircles()
    {
        // var circles = Session.ActiveRoute.TracedRoute.ComputedCircles;
        // var pool = circlePool;
        // var holder = dynamicHolderCircles;
        //
        // for (var i = 0; i < circles.Count; i++)
        // {
        //     var line = circles[i];
        //
        //     if (line == null)
        //     {
        //         continue;
        //     }
        //
        //     var point = line.LinkedPoint;
        //
        //     var drawer = pool.Spawn(Vector3.zero, Quaternion.identity, holder).GetComponent<FixedCircleDrawer>();
        //     drawer.name = $"{line.GetName} {point.Name}";
        //     drawer.Display(line);
        // }
    }

    private void DisplayFixRays()
    {
        // var rays = Session.ActiveRoute.TracedRoute.ComputedRays;
        // var pool = rayPool;
        // var holder = dynamicHolderRays;
        //
        // for (var i = 0; i < rays.Count; i++)
        // {
        //     var ray = rays[i];
        //
        //     if (ray == null)
        //     {
        //         continue;
        //     }
        //
        //     var point = ray.LinkedPoint;
        //
        //     var drawer = pool.Spawn(Vector3.zero, Quaternion.identity, holder).GetComponent<FixedRayDrawer>();
        //     drawer.name = $"{ray.GetName} {point.Name}";
        //     drawer.Display(ray);
        // }
    }


    private void OnDrawGizmos()
    {
        if (!Application.isPlaying)
        {
            return;
        }
        
        Gizmos.color = Color.green;
        var up = pivot.up;
        Gizmos.DrawSphere(up * Session.Settings.MapReferenceLength80, 0.05f);

        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(up * Session.Settings.PlanReferenceLength80, 0.065f);

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(up * Session.Settings.PlanReferenceLength80 / 2f, 0.025f);


        // if (GameManager.Instance.ActiveRoute?.PathLines?.ComputedLines == null)
        // {
        //     return;
        // }
        //
        // Gizmos.DrawSphere(Extension.ToDisplay(GameManager.Instance.Aircraft.PositionFreeOrOnCurvedPath), 0.05f);
    }
}