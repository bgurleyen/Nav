using System;
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

    // if we detect that during MOD the current node has passed we reExecute all the commands until that point
    int modReExecutedForIndex = -1;

    static RouteScriptableObject ActiveRoute => GameManager.Instance.ActiveRoute;
    static RouteScriptableObject ModRoute => GameManager.Instance.ModRoute;
    static RouteScriptableObject DisplayMod => GameManager.Instance.ModeSetWithPosition;


    public void ComputeActive()
    {
        if (ActiveRoute == null)
        {
            return;
        }

        GameManager.Instance.PathLines.ComputeSet(ActiveRoute, false);
    }

    public void ComputeMod()
    {
        if (ModRoute == null)
        {
            modReExecutedForIndex = -1;
            return;
        }

        if (GameManager.Instance.Aircraft.IsOnPath)
        {
            // mod always has to include the last passed active node ( all the passed nodes ) 
            // otherwise it is invalid - will reapply all the commands
            var _passedNodeIndex = PositionVirtualNode.PassedNodeIndex;

            if (_passedNodeIndex != modReExecutedForIndex)
            {
                if (ActiveRoute.Points[_passedNodeIndex].ID != ModRoute.Points[_passedNodeIndex].ID)
                {
                    GameManager.Instance.ReExecuteCachedCommands();
                    Debug.Log("Reapplied MOD");
                }

                modReExecutedForIndex = _passedNodeIndex;
            }
        }

        GameManager.Instance.ModeSetWithPosition = ModRoute.Clone(); // refactor
        
        DisplayMod.AddDisplayPositionNode();

        GameManager.Instance.PathLines.ComputeSet(DisplayMod, true);
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
        DisplaySet(false);
        DisplaySet(true);
        DisplayFixCircles();
        DisplayFixRays();
        freeFlightPivot.gameObject.SetActive(GameManager.Instance.Aircraft.IsFreeFlight);
        bananaIndicatorPivot.SetLocalY(Calculator.Instance.GetBananaPosition);


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
                mobilePlaneIndicatorPivot.position = GameManager.Instance.Aircraft.PositionFreeOrOnCurvedPath.ToDisplay();
                mobilePlaneIndicatorPivot.SetLocalRotationZ(-GameManager.Instance.Aircraft.Heading);
                break;
            case DrawerMode.Suspeded:
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    private void DisplayOtherTraffic()
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
        var _onjective = otherAircraftsPool.Spawn(Vector3.zero, Quaternion.identity, dynamicHolderOtheriarcrafts)
            .GetComponent<OtherAircrafIndicator>();
        _onjective.name = "My objective";
        _onjective.Init("*", Color.red);
        _onjective.transform.localPosition = new Vector2(100, 100).ToDisplay();
    }

    void DisplaySet(bool mod)
    {
        if (mod)
        {
            if (GameManager.Instance.ModRoute == null || GameManager.Instance.PathLines.ComputedLinesMod == null)
            {
                return;
            }
        }

        var _lines = mod
            ? GameManager.Instance.PathLines.ComputedLinesMod
            : GameManager.Instance.PathLines.ComputedLines;
        var _pool = mod ? linesPoolMod : linesPool;
        var _holder = mod ? dynamicHolderMod : dynamicHolder;

        for (var i = 0; i < _lines.Length; i++)
        {
            var _line = _lines[i];

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
        var _circles = GameManager.Instance.PathLines.ComputedCircles;
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
        var _rays = GameManager.Instance.PathLines.ComputedRays;
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

    public static bool GetNextLine(MarkLine lastLine, int fromDataPointIndex, RoutePoint[] points, out MarkLine line,
        out int toDataPointIndex)
    {
        // the line may be already begun if previous was a curve 
        line = null;
        if (fromDataPointIndex == points.Length - 1)
        {
            toDataPointIndex = fromDataPointIndex;
            return false;
        }

        toDataPointIndex = fromDataPointIndex + 1;
        var _nextPoint = points[toDataPointIndex];

        var _forceEndStraight = false;

        if (toDataPointIndex + 1 < points.Length)
        {
            _forceEndStraight = points[toDataPointIndex + 1].IsAfterDiscontinuity;
        }

        RoutePoint _notToCloseSecondPoint = null;

        // there are no more points to create a curve to ( in which case continue with straight line on current segment )
        if (toDataPointIndex != points.Length - 1)
        {
            var _secondPoint = points[toDataPointIndex + 1];
            if (_secondPoint.Distance > 0.5f)
            {
                _notToCloseSecondPoint = _secondPoint;
            }
        }

        return ComputeLine(lastLine, out line, _nextPoint, _notToCloseSecondPoint, _forceEndStraight);
    }

    public static bool ComputeLine(MarkLine lastLine, out MarkLine line, RoutePoint nextPoint,
        RoutePoint notTooCloseSecondPoint = null, bool forceEndStraight = false)
    {
        float _angleBetween = 180;

        // there are no more points to create a curve to ( in which case continue with straight line on current segment )
        if (notTooCloseSecondPoint != null)
        {
            _angleBetween = Geometry.AngleBetweenNodes(nextPoint.Degrees, notTooCloseSecondPoint.Degrees);
        }

        var _startsStraight = lastLine.LinkedPoint != null &&
                              (lastLine.LinkedPoint.IsAfterDiscontinuity || lastLine.LinkedPoint.IsHiddenLine);

        var _endsStraight = forceEndStraight;

        var _lastEndOffset = lastLine.LinkedPoint != null && _startsStraight
            ? lastLine.EndPosition
            : lastLine.EndOffsetPosition;

        if (Math.Abs(_angleBetween - 180) < 0.2f || _endsStraight)
        {
            // straight line  
            GenerateLine(nextPoint, lastLine.EndPosition, _lastEndOffset, out line);
        }
        else
        {
            // try relaxed turn. Update: don't use relaxed as the radius can become very big, and there is no advantage to it. Just go with regular curve
            if (true || !GenerateCurve(RelaxedRadius, nextPoint, notTooCloseSecondPoint, _angleBetween, lastLine.EndPosition,
                _lastEndOffset,
                out line))
            {
                if (!GenerateCurve(GetMinRadius, nextPoint, notTooCloseSecondPoint, _angleBetween, lastLine.EndPosition,
                    _lastEndOffset,
                    out line))
                {
                    GenerateDoubleCurve(GetMinRadius, RelaxedRadius, nextPoint, notTooCloseSecondPoint, _angleBetween,
                        lastLine.EndPosition,
                        _lastEndOffset, out line);
                }
            }
        }

        return true;
    }

    static void GenerateLine(RoutePoint nextPoint, Vector3 lastEndPosition, Vector3 lastEndOffset, out MarkLine line)
    {
        var _l = new MarkLine(nextPoint);
        _l.Init(lastEndPosition, lastEndOffset, nextPoint);
        line = _l;
    }

    static bool GenerateCurve(float chosenRadius, RoutePoint nextPoint, RoutePoint secondPoint, float angleBetween,
        Vector3 lastEndPosition, Vector3 lastEndOffset, out MarkLine line)
    {
        var _tangentToMiddle = Line.GetTangentToMiddle(chosenRadius, angleBetween);

        if (_tangentToMiddle <= nextPoint.Distance && _tangentToMiddle <= secondPoint.Distance &&
            _tangentToMiddle <= chosenRadius)
        {
            var l = new Curve(nextPoint);
            l.Init(lastEndPosition, lastEndOffset, nextPoint, secondPoint, _tangentToMiddle, chosenRadius);
            line = l;
            return true;
        }

        line = null;
        return false;
    }

    static void GenerateDoubleCurve(float smallRadius, float bigRadius, RoutePoint nextPoint, RoutePoint secondPoint,
        float angleBetween, Vector3 lastEndPosition, Vector3 lastEndOffset, out MarkLine line)
    {
        var l = new DoubleCurve(nextPoint);
        l.Init(lastEndPosition, lastEndOffset, nextPoint, secondPoint, angleBetween, smallRadius, bigRadius);
        line = l;
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


        if (GameManager.Instance.PathLines.ComputedLines == null)
        {
            return;
        }

        Gizmos.DrawSphere(GameManager.Instance.Aircraft.PositionFreeOrOnCurvedPath.ToDisplay(), 0.05f);
    }
}