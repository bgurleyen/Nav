using System;
using System.Collections.Generic;
using UnityEngine;
using Lean.Pool;
using Gamelogic.Extensions;

public class Drawer : Singleton<Drawer>
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

    [Header("modes visuals")] [SerializeField]
    private GameObject[] mapHolder;

    [SerializeField] private GameObject[] centerHolder;
    [SerializeField] private GameObject[] planHolder;

    [Header("Adjust")] [SerializeField] private float mapReferenceLength80 = 1.6f;
    [SerializeField] private float planReferenceLength80 = 1.6f;
    [SerializeField] private float _debugStarDistance = 1.3f;

    public Color CMagenta => _gameConfig.Settings.cMagenta;
    public Color CLightYellow => _gameConfig.Settings.cLightYellow;

    public DrawerMode Mode { get; private set; } = DrawerMode.Suspeded;

    public float Zoom => (Mode == DrawerMode.Plan
        ? planReferenceLength80
        : _zoomMultiplier * mapReferenceLength80) / 80f;

    private float _zoomMultiplier;


    private Aircraft Aircraft => GameManager.Instance.Aircraft;

    private const bool WalkOnMod = false;

    private void Awake()
    {
        _zoomMultiplier = _gameConfig.Settings.StartingZoom;
    }

    public void ResetMode()
    {
        Mode = DrawerMode.Map;
    }

    public void ShowMapMode()
    {
        cameraAnimator.SetTrigger("Map");
        Mode = DrawerMode.Map;
        ShowCurrentMode();
    }

    public void ShowCenterMode()
    {
        cameraAnimator.SetTrigger("Center");
        Mode = DrawerMode.Center;
        ShowCurrentMode();
    }

    public void ShowPlanMode()
    {
        cameraAnimator.SetTrigger("Center");
        Mode = DrawerMode.Plan;
        ShowCurrentMode();
    }

    private void ShowCurrentMode()
    {
        foreach (var x in mapHolder)
        {
            x.SetActive(Mode == DrawerMode.Map);
        }

        foreach (var x in centerHolder)
        {
            x.SetActive(Mode == DrawerMode.Center);
        }

        foreach (var x in planHolder)
        {
            x.SetActive(Mode == DrawerMode.Plan);
        }

        Clear();
        Display();
    }

    public void OnZoomIn()
    {
        Clear();
        _zoomMultiplier += 0.2f;
        Display();
    }

    public void OnZoomOut()
    {
        Clear();
        _zoomMultiplier -= 0.2f;
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

    public static bool GetCircleFix(RoutePoint linkedPoint, Vector3 from, FixedPointInfo linkedInfo,
        out FixCircle circle)
    {
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
        if (linkedInfo.RawDegrees != null)
        {
            ray = new FixRay(linkedPoint, linkedInfo);
            ray.Init(from);
            return true;
        }

        ray = null;
        return false;
    }

    public void Display()
    {
        DisplaySet(GameManager.Instance.ActiveRoute?.PathLines?.ComputedLines, LinesType.Active);
        if (GameManager.Instance.ModRoute != null)
        {
            DisplaySet(GameManager.Instance.ModeSetWithPosition?.PathLines?.ComputedLines, LinesType.Mod);
        }

        DisplayFixCircles();
        DisplayFixRays();
        freeFlightPivot.gameObject.SetActive(Aircraft.IsFreeFlight);
        bananaIndicatorPivot.SetLocalY(Calculator.Instance.GetBananaPosition);

        if (Aircraft.IsRejoining && Aircraft.RejoinPathLines != null)
        {
            DisplaySet(Aircraft.RejoinPathLines.ComputedLines,  LinesType.Rejoin);
        }
        
        DisplayOtherTraffic();

        switch (Mode)
        {
            case DrawerMode.Center:
            case DrawerMode.Map:
                //rotate compass
                compasPivot.SetLocalRotationZ(Aircraft.HeadingDegrees);
                if (Aircraft.IsFreeFlight)
                {
                    freeFlightPivot.SetLocalRotationZ(Aircraft.HeadingDegrees - Calculator.RHeading);
                }

                break;
            case DrawerMode.Plan:
                //rotate compass
                compasPivot.SetLocalRotationZ(0);
                mobilePlaneIndicatorPivot.position =
                    Aircraft.PositionFreeOrOnCurvedPath.ToDisplay();
                mobilePlaneIndicatorPivot.SetLocalRotationZ(-Aircraft.HeadingDegrees);
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
            drawer.transform.localPosition = positions[key].ToDisplay();
        }

        // demo - shows a debug star for seeing the distance
        var objective = otherAircraftsPool.Spawn(Vector3.zero, Quaternion.identity, dynamicHolderOtheriarcrafts)
            .GetComponent<OtherAircrafIndicator>();
        objective.name = "My objective";
        objective.Init("|", Color.yellow);
        objective.transform.localPosition = (GameManager.Instance.Aircraft.PositionFreeOrOnCurvedPath +
                                             Vector2.right * _debugStarDistance)
            .ToDisplay();

        // demo - shows a debug star for seeing the distance
        objective = otherAircraftsPool.Spawn(Vector3.zero, Quaternion.identity, dynamicHolderOtheriarcrafts)
            .GetComponent<OtherAircrafIndicator>();
        objective.name = "origin";
        objective.Init("o", Color.blue);
        objective.transform.localPosition = Vector2.zero.ToDisplay();
    }

    private void DisplaySet(IReadOnlyList<MarkLine> lines, LinesType linesType)
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
        if (Aircraft.IsRejoining)
        {
            rejoinSegmentIndex = Aircraft.CachedExitSegmentOfHeadingRejoinIntersection;
            rejoinPoint = Aircraft.CachedExitPointFromHeading;
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

            // $%^ this is bad. Ask if we need to keep the rejoin arc after rejoining
            // we are offsetting the display start of the line to be just after the rejoin path
            // Update: we want to keep displaying the active route as original so this is not needed
            // if (linesType == LinesType.Active && Aircraft.IsRejoining)
            // {
            //     if (i < rejoinSegmentIndex )
            //     {
            //         // dont' hide line even if the aircraft is away rejoining
            //         //hiddenLine = true;
            //     }
            //     else if (i == rejoinSegmentIndex)
            //     {
            //         // find intersection vertex index of generated line with rejoin line.. 
            //         if (line.Vertexes.Length > 4)
            //         {
            //             var dist = (line.Vertexes[0].To2DXY() - rejoinPoint).sqrMagnitude;
            //
            //             for (int p = 1; p < line.Vertexes.Length; p++)
            //             {
            //                 var nextDist = (line.Vertexes[p].To2DXY() - rejoinPoint).sqrMagnitude;
            //                 if (nextDist > dist)
            //                 {
            //                     fromPointIndex = p - 1;
            //                     break;
            //                 }
            //
            //                 dist = nextDist;
            //             }
            //         }
            //     }
            // }

            var point = line.LinkedPoint;

            var drawer = pool.Spawn(Vector3.zero, Quaternion.identity, holder).GetComponent<LineDrawer>();
            drawer.name = $"{linesType} {line.GetName} {point.Name}";

            if (linesType == LinesType.Mod && GameManager.Instance.ActiveRoute.GetPoint(point.ID, out var activePoint))
            {
                // $^% error at cartesian position
                if (RoutePoint.HaveSamePosition(activePoint, point))
                {
                    hiddenLabel = true;
                }
                else
                {
                    Debug.LogWarning(activePoint.Name + " " +
                                     (activePoint.CartesianPosition - point.CartesianPosition).magnitude);
                }
            }

            if (linesType == LinesType.Rejoin && line.StartPosition == Vector3.zero)
            {
                hiddenLine = true;
            }

            drawer.Display(line, point, hiddenLabel || linesType == LinesType.Rejoin, hiddenLine, fromPointIndex);
        }
    }
    
    public enum LinesType
    {
        Mod, Active, Rejoin
    }

    private void DisplayFixCircles()
    {
        var circles = GameManager.Instance.ActiveRoute.PathLines.ComputedCircles;
        var pool = circlePool;
        var holder = dynamicHolderCircles;

        for (var i = 0; i < circles.Count; i++)
        {
            var line = circles[i];

            if (line == null)
            {
                continue;
            }

            var point = line.LinkedPoint;

            var drawer = pool.Spawn(Vector3.zero, Quaternion.identity, holder).GetComponent<FixedCircleDrawer>();
            drawer.name = $"{line.GetName} {point.Name}";
            drawer.Display(line);
        }
    }

    private void DisplayFixRays()
    {
        var rays = GameManager.Instance.ActiveRoute.PathLines.ComputedRays;
        var pool = rayPool;
        var holder = dynamicHolderRays;

        for (var i = 0; i < rays.Count; i++)
        {
            var ray = rays[i];

            if (ray == null)
            {
                continue;
            }

            var point = ray.LinkedPoint;

            var drawer = pool.Spawn(Vector3.zero, Quaternion.identity, holder).GetComponent<FixedRayDrawer>();
            drawer.name = $"{ray.GetName} {point.Name}";
            drawer.Display(ray);
        }
    }


    private void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        var up = pivot.up;
        Gizmos.DrawSphere(up * mapReferenceLength80, 0.05f);

        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(up * planReferenceLength80, 0.065f);

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(up * planReferenceLength80 / 2f, 0.025f);


        if (GameManager.Instance.ActiveRoute?.PathLines?.ComputedLines == null)
        {
            return;
        }

        Gizmos.DrawSphere(GameManager.Instance.Aircraft.PositionFreeOrOnCurvedPath.ToDisplay(), 0.05f);
    }
}