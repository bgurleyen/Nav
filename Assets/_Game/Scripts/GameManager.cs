using System;
using Navigation;
using UnityEngine;
using Unyawn.Utils;

[DefaultExecutionOrder(-100)]
public class GameManager : MonoBehaviour
{
    [SerializeField] private GameConfigScriptableObject _gameConfig;

    [SerializeField] private ComputedRoutes _routes = new();

    private Simulation _simulation;

    private LegsScreen _legsScreen;

    //public RouteScriptableObject currentRouteScriptableObject;

    private void Awake()
    {
        Calculator.totalFuel = 1000;

        UYServiceLocator.Register(this);

        if (_gameConfig == null || _gameConfig.Settings == null)
        {
            Debug.LogError("GameManager: GameConfig or Settings is not assigned.");
            return;
        }

        Session.Settings = _gameConfig.Settings;
    }

    private void Start()
    {
        if (!UYServiceLocator.Has<McpUI>() || !UYServiceLocator.Has<Simulation>() || !UYServiceLocator.Has<LegsScreen>())
        {
            Debug.LogError("GameManager: required services are not registered.");
            return;
        }

        var mcpUI = UYServiceLocator.Get<McpUI>();

        _simulation = UYServiceLocator.Get<Simulation>();

        Session.State = new State(mcpUI);

        _legsScreen = UYServiceLocator.Get<LegsScreen>();
        _legsScreen.OnLeftCornerPressErase += LEGS_OnLeftCornerPressErase;
        _legsScreen.OnExecButtonPress += LEGS_OnExecButtonPress;
        //PlayerPrefsHolder.Level = 1;                                        //REmove for level
        InitForLevel(PlayerPrefsHolder.Level);
    }

    private void InitForLevel(int index)
    {
        Session.IsRunning = false;
        index = PlayerPrefsHolder.ClampLevel(index);
        if (_gameConfig.LevelsData == null || index < 0 || index >= _gameConfig.LevelsData.Length
            || _gameConfig.LevelsData[index] == null)
        {
            Debug.LogError($"[GameManager] No level data for level {index}.");
            return;
        }

        var levelData = _gameConfig.LevelsData[index];
        if (levelData.MainRoute == null || _routes == null)
        {
            Debug.LogError($"[GameManager] Level {index} has no route.");
            return;
        }

        Session.CurrentLevel = levelData;

        levelData.MainRoute.Init(true);

        levelData.MainRoute.ComputeCartesianPositions();

        //Debug.Log("Init For Level  1111111 ");
        Session.OriginalReferenceRoute = levelData.MainRoute.CloneAndInit();
        /*for (int i = 0; i < Session.OriginalReferenceRoute.Points.Length; i++) {
            Debug.Log("Session.OriginalReferenceRoute["+i+"]: " + "ID: ["+Session.OriginalReferenceRoute.Points[i].ID+"] => Name: ["+Session.OriginalReferenceRoute.Points[i].Name+"]");
        }*/
        //Debug.Log("OriginalReferenceRoute: "+ Session.OriginalReferenceRoute);
        //Debug.Log("Init For Level  222222 ");
        _routes.ActiveRoute = levelData.MainRoute.CloneAndInit();

        //Debug.Log("_routes.ActiveRoute: "+_routes.ActiveRoute);


        /*currentRouteScriptableObject = levelData.MainRoute.CloneAndInit();

        for (var i = 0; i < currentRouteScriptableObject.Points.Length; i++) {
            Debug.Log("newSet.Point+" + i + ":" + currentRouteScriptableObject.Points[i].ID + "||" + currentRouteScriptableObject.Points[i].Name);
        }*/


        Session.Routes = _routes;

        Session.RollRteOrigin();

        try
        {
            if (Move.Instance != null)
                Move.Instance.Init(levelData);
        }
        catch (Exception e)
        {
            Debug.LogException(e);
        }

        if (_simulation == null)
        {
            Debug.LogError("[GameManager] Simulation is not available.");
            return;
        }

        _simulation.Init();
    }
 
    private void OnDestroy()
    {

        if (UYServiceLocator.Has<LegsScreen>())
        {
            var legsScreen = UYServiceLocator.Get<LegsScreen>();
            legsScreen.OnLeftCornerPressErase -= LEGS_OnLeftCornerPressErase;
            legsScreen.OnExecButtonPress -= LEGS_OnExecButtonPress;
        }
    }

    private float _pendingDeltaTime;

    private void Update()
    {
        //// normal time calculation, but big computations
        //_pendingDeltaTime += Time.deltaTime;
        //var tickDuration = Session.Settings.TickDuration(false) / Session.Settings.SpeedMultiplier;
        //var ticksInDeltaTime = (int)(_pendingDeltaTime / tickDuration);
        //for (int i = 0; i < ticksInDeltaTime; i++)
        //{
        //    _simulation.Tick();
        //}
        //_pendingDeltaTime -= ticksInDeltaTime * tickDuration; 

        //// incorrect, but faster game speed up
        //Session.Settings.FlyingTickDuration =
        //    Time.deltaTime * Session.Settings.SpeedMultiplier;
        //_simulation.Tick();

        // Route geometry is rebuilt when the level or an FMC edit changes the route.
        if (Session.Settings == null || _simulation == null)
        {
            return;
        }

        Session.Settings.FlyingTickDuration = Time.deltaTime * Session.Settings.SpeedMultiplier;

        _pendingDeltaTime += Time.deltaTime;
        var tickDuration = Session.Settings.TickDuration(false) / Session.Settings.SpeedMultiplier;
        // timeScale==0 → deltaTime 0 → avoid 0/0 NaN ticks that freeze movement while UI still works
        var ticksInDeltaTime = tickDuration > 0f
            ? (int)(_pendingDeltaTime / tickDuration)
            : 0;

        _simulation.SplittedTickModHandling();
        for (int i = 0; i < ticksInDeltaTime; i++)
        {
            _simulation.SplittedTickSimulation();
        }
        _simulation.SplittedTickComputeMod();
        _simulation.SplittedTickDraw();
        if (tickDuration > 0f)
            _pendingDeltaTime -= ticksInDeltaTime * tickDuration;
    }

    private void LEGS_OnLeftCornerPressErase()
    {
        _simulation.EraseMod();
    }

    private void LEGS_OnExecButtonPress()
    {
        //Debug.Log("Session Mod " + Session.IsMod);
        if (Session.IsMod)
        {
            //Debug.Log("Apply Mod " + Session.IsMod);  
            ApplyMod();
        }
    }



    private void ApplyMod()
    {
        if (_simulation == null || Session.ModRoute == null || Session.PlayerAircraft == null || Session.State == null)
            return;

        _simulation.RebuildModeSetWithPosition();
        if (Session.ModeSetWithPosition == null)
            return;

        Session.ModRoute.ClearModifiedFlags();
        Session.ModeSetWithPosition.ClearModifiedFlags();
        Session.ActiveRoute = Session.ModeSetWithPosition.CloneAndInit();
        if (Session.ActiveRoute == null)
            return;

        Session.ActiveRoute.ComputeTrace();

        Session.ModRoute = null;
        Session.IsMod = false;
        _simulation.ClearModPreview();
        if (_legsScreen != null)
            _legsScreen.DisplayOperation("0k");
        if (Session.ActiveRoute.HasActiveDirectApproach(out var linearApproachIndex) && Session.State.LNAV)
        {
            if (!Session.PlayerAircraft.TryRejoinRoute())
            {
                Session.PlayerAircraft.ResetSeekProgress(linearApproachIndex, 0);
            }
        }
        else
        {
            var seekIndex = Session.ActiveRoute.FindForwardPositionNodeIndex();
            var seekVertex = 0;
            var pointCount = Session.ActiveRoute.Points != null ? Session.ActiveRoute.Points.Length : 0;
            if (Session.ActiveRoute.IsCourseInterceptRoute)
            {
                // The route now starts on the 200 NM course. Join the closest point on it.
                Session.ActiveRoute.FindClosestPointOnRoute(
                    Session.PlayerAircraft.NMPosition, out seekIndex, out seekVertex);
            }
            else if (seekIndex < 0)
            {
                if (pointCount < 2)
                    return;
                seekIndex = Mathf.Clamp(
                    PositionVirtualNode.PassedNodeIndex + 2,
                    1,
                    pointCount - 1);
            }

            Session.PlayerAircraft.ResetSeekProgress(seekIndex, seekVertex);
        }
    }

    public static void RemoveAt<T>(ref T[] arr, int index)
    {
        for (int a = index; a < arr.Length - 1; a++)
        {
            // moving elements downwards, to fill the gap at [index]
            arr[a] = arr[a + 1];
        }
        // finally, let's decrement Array's size by one
        Array.Resize(ref arr, arr.Length - 1);
    }


    /*private void ApplyMod() {
        Session.ModRoute.ClearModifiedFlags();
        Session.ModeSetWithPosition.ClearModifiedFlags();
        Debug.Log("Apply Mod 333333333");
        Session.ActiveRoute = Session.ModeSetWithPosition.CloneAndInit();

        // Use HashSet to keep track of unique elements
        HashSet<RoutePoint> uniquePoints = new HashSet<RoutePoint>();
        List<RoutePoint> uniquePointList = new List<RoutePoint>();

        foreach (var point in Session.ActiveRoute.Points) {
            if (!uniquePoints.Contains(point)) {
                uniquePoints.Add(point);
                uniquePointList.Add(point);
            }
        }

        // Convert back to array if necessary
        Session.ActiveRoute.Points = uniquePointList.ToArray();

        // Now, Session.ActiveRoute.Points contains unique elements only
        for (int i = 0; i < Session.ActiveRoute.Points.Length; i++) {
            Debug.Log("Session.ActiveRoute[" + i + "]: " + Session.ActiveRoute.Points[i].ID + "||" + Session.ActiveRoute.Points[i].Name);
        }

        Session.ModRoute = null;
        Session.IsMod = false;
        _legsScreen.DisplayOperation("0k");
        if (Session.ActiveRoute.HasActiveDirectApproach(out var linearApproachIndex) && Session.State.LNAV) {
            if (!Session.PlayerAircraft.TryRejoinRoute()) {
                Session.PlayerAircraft.ResetSeekProgress(linearApproachIndex, 0);
            }
        }
        else {
            Session.PlayerAircraft.ResetSeekProgress(PositionVirtualNode.PassedNodeIndex + 2, 0);
        }
    }*/
}
