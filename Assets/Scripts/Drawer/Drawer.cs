using System;
using System.Collections.Generic;
using UnityEngine;
using Lean.Pool;
using Gamelogic.Extensions;

public class Drawer : Singleton<Drawer>
{
    [SerializeField] Color cMagenta;
    [SerializeField] Color cLightYellow;

    public Animator cameraAnimator;
    [Space] [SerializeField] Transform dynamicHolder;
    [SerializeField] Transform dynamicHolderMod;
    [SerializeField] Transform dynamicHolderCircles;
    [SerializeField] Transform dynamicHolderRays;
    [SerializeField] Transform dynamicHolderOtheriarcrafts;

    [Space] [SerializeField] LeanGameObjectPool linesPool;
    [SerializeField] LeanGameObjectPool linesPoolMod;
    [SerializeField] LeanGameObjectPool circlePool;
    [SerializeField] LeanGameObjectPool rayPool;
    [SerializeField] LeanGameObjectPool otherAircraftsPool;

    [Space] [SerializeField] Transform pivot;
    [SerializeField] Transform compasPivot;
    [SerializeField] Transform mobilePlaneIndicatorPivot;
    [SerializeField] Transform freeFlightPivot;
    [SerializeField] Transform bananaIndicatorPivot;

    [Header("modes visuals")] [SerializeField]
    GameObject[] mapHolder;

    [SerializeField] GameObject[] centerHolder;
    [SerializeField] GameObject[] planHolder;

    [Header("Adjust")] [SerializeField] float mapReferenceLength80 = 1.6f;
    [SerializeField] float planReferenceLength80 = 1.6f;

    public Color CMagenta => cMagenta;

    public Color CLightYellow => cLightYellow;

    public DrawerMode Mode { get; private set; } = DrawerMode.Suspeded;

    public float Zoom => (Mode == DrawerMode.Plan
        ? planReferenceLength80
        : zoomMultiplier * mapReferenceLength80) / 80f;

    float zoomMultiplier = 1f;

    public const float RelaxedRadius = 17;

    const float FtToNm = 0.000164579f;

    // turn radius
    const float IAS = 240;
    const float Altitude = 1000;
    const float Headwind = 5;
    static float TAS => IAS + Altitude / 1000 * 0.02f * IAS;
    static float GS => TAS - Headwind;
    static float Bank => Mathf.Deg2Rad * Mathf.Min(30, TAS * 0.15f);
    public static float GetMinRadius => Mathf.Pow(GS, 2) / (11.29f * Mathf.Tan(Bank)) * FtToNm;
    const bool WalkOnMod = false;




    


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

    void ShowCurrentMode()
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
        zoomMultiplier += 0.2f;
        Display();
    }

    public void OnZoomOut()
    {
        Clear();
        zoomMultiplier -= 0.2f;
        Display();
    }

    public void Clear()
    {
        Extension.DespawnChildred<LineDrawer>(dynamicHolder, linesPool);
        Extension.DespawnChildred<LineDrawer>(dynamicHolderMod, linesPoolMod);
        Extension.DespawnChildred<FixedCircleDrawer>(dynamicHolderCircles, circlePool);
        Extension.DespawnChildred<FixedRayDrawer>(dynamicHolderRays, rayPool);
        Extension.DespawnChildred<OtherAircrafIndicator>(dynamicHolderOtheriarcrafts, otherAircraftsPool);
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
        DisplaySet(GameManager.Instance.ActiveRoute?.PathLines?.ComputedLines, false);
        if (GameManager.Instance.ModRoute != null)
        {
            DisplaySet(GameManager.Instance.ModeSetWithPosition?.PathLines?.ComputedLines, true);
        }

        DisplayFixCircles();
        DisplayFixRays();
        freeFlightPivot.gameObject.SetActive(GameManager.Instance.Aircraft.IsFreeFlight);
        bananaIndicatorPivot.SetLocalY(Calculator.Instance.GetBananaPosition);

        if (GameManager.Instance.Aircraft.tempPathLines != null)
        {
            DisplaySet(GameManager.Instance.Aircraft.tempPathLines.ComputedLines, false);
        }
        
        DisplayOtherTraffic();


        switch (Mode)
        {
            case DrawerMode.Center:
            case DrawerMode.Map:
                //rotate compass
                compasPivot.SetLocalRotationZ(GameManager.Instance.Aircraft.Heading);
                if (GameManager.Instance.Aircraft.IsFreeFlight)
                {
                    freeFlightPivot.SetLocalRotationZ(GameManager.Instance.Aircraft.Heading - Calculator.RHeading);
                }

                break;
            case DrawerMode.Plan:
                //rotate compass
                compasPivot.SetLocalRotationZ(0);
                mobilePlaneIndicatorPivot.position =
                    GameManager.Instance.Aircraft.PositionFreeOrOnCurvedPath.ToDisplay();
                mobilePlaneIndicatorPivot.SetLocalRotationZ(-GameManager.Instance.Aircraft.Heading);
                break;
            case DrawerMode.Suspeded:
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    void DisplayOtherTraffic()
    {
        var _positions = Move.Instance.ACPositions;
        var _texts = Move.Instance.ACTexts;

        foreach (var _key in _positions.Keys)
        {
            // Debug.Log(_positions[_key]);
            var _drawer = otherAircraftsPool.Spawn(Vector3.zero, Quaternion.identity, dynamicHolderOtheriarcrafts)
                .GetComponent<OtherAircrafIndicator>();
            _drawer.name = _key;
            _drawer.Init(_texts[_key], Color.yellow);
            _drawer.transform.localPosition = _positions[_key].ToDisplay();
        }

        // demo
        var _objective = otherAircraftsPool.Spawn(Vector3.zero, Quaternion.identity, dynamicHolderOtheriarcrafts)
            .GetComponent<OtherAircrafIndicator>();
        _objective.name = "My objective";
        _objective.Init("*", Color.red);
        _objective.transform.localPosition = new Vector2(100, 100).ToDisplay();
    }

    void DisplaySet(IReadOnlyList<MarkLine> lines, bool mod)
    {
        if (lines == null)
        {
            return;
        }

        var _pool = mod ? linesPoolMod : linesPool;
        var _holder = mod ? dynamicHolderMod : dynamicHolder;

        for (var i = 0; i < lines.Count; i++)
        {
            var _line = lines[i];

            if (_line == null)
            {
                continue;
            }

            var _point = _line.LinkedPoint;

            var _drawer = _pool.Spawn(Vector3.zero, Quaternion.identity, _holder).GetComponent<LineDrawer>();
            _drawer.name = _line.GetName + " " + _point.Name;

            var _hiddenLabel = false;
            if (mod && GameManager.Instance.ActiveRoute.GetPoint(_point.ID, out var _activePoint))
            {
                // @#$ error at cartesian position
                if (RoutePoint.HaveSamePosition(_activePoint, _point))
                {
                    _hiddenLabel = true;
                }
                else
                {
                    Debug.LogWarning(_activePoint.Name + " " +
                                     (_activePoint.CartesianPosition - _point.CartesianPosition).magnitude);
                }
            }

            _drawer.Display(_line, _point, _hiddenLabel);
        }
    }

    void DisplayFixCircles()
    {
        var _circles = GameManager.Instance.ActiveRoute.PathLines.ComputedCircles;
        var _pool = circlePool;
        var _holder = dynamicHolderCircles;

        for (var i = 0; i < _circles.Count; i++)
        {
            var _line = _circles[i];

            if (_line == null)
            {
                continue;
            }

            var _point = _line.LinkedPoint;

            var _drawer = _pool.Spawn(Vector3.zero, Quaternion.identity, _holder).GetComponent<FixedCircleDrawer>();
            _drawer.name = _line.GetName + " " + _point.Name;
            _drawer.Display(_line);
        }
    }

    void DisplayFixRays()
    {
        var _rays = GameManager.Instance.ActiveRoute.PathLines.ComputedRays;
        var _pool = rayPool;
        var _holder = dynamicHolderRays;

        for (var i = 0; i < _rays.Count; i++)
        {
            var _ray = _rays[i];

            if (_ray == null)
            {
                continue;
            }

            var _point = _ray.LinkedPoint;

            var _drawer = _pool.Spawn(Vector3.zero, Quaternion.identity, _holder).GetComponent<FixedRayDrawer>();
            _drawer.name = _ray.GetName + " " + _point.Name;
            _drawer.Display(_ray);
        }
    }





    void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        var _up = pivot.up;
        Gizmos.DrawSphere(_up * mapReferenceLength80, 0.05f);

        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(_up * planReferenceLength80, 0.065f);

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(_up * planReferenceLength80 / 2f, 0.025f);


        if (GameManager.Instance.ActiveRoute?.PathLines?.ComputedLines == null)
        {
            return;
        }

        Gizmos.DrawSphere(GameManager.Instance.Aircraft.PositionFreeOrOnCurvedPath.ToDisplay(), 0.05f);
    }
}