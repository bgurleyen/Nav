using System;
using Navigation;
using UnityEngine;
using Unyawn.Utils;

public class GameManager : MonoBehaviour
{
    [SerializeField] private GameConfigScriptableObject _gameConfig;

    [SerializeField] private ComputedRoutes _routes = new();

    private Simulation _simulation;

    private LegsScreen _legsScreen;

    //public RouteScriptableObject currentRouteScriptableObject;

    private void Awake()
    {

        UYServiceLocator.Register(this);

        Session.Settings = _gameConfig.Settings;
    }

    private void Start()
    {
        var mcpUI = UYServiceLocator.Get<McpUI>();

        _simulation = UYServiceLocator.Get<Simulation>();

        Session.State = new State(mcpUI);

        _legsScreen = UYServiceLocator.Get<LegsScreen>();
        _legsScreen.OnLeftCornerPressErase += LEGS_OnLeftCornerPressErase;
        _legsScreen.OnExecButtonPress += LEGS_OnExecButtonPress;

        InitForLevel(PlayerPrefsHolder.Level);
    }

    private void InitForLevel(int index)
    {
        index = 0; //Start Level  remove

        var levelData = _gameConfig.LevelsData[index];
        
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

        //_routes.FixedPoints = FixedPointsScriptableObject.CreateDemo();
        //Debug.Log(_routes.FixedPoints.Entries.Length);

        _routes.ILSRoute = ComputeILS();
        _routes.ILSRoute.ComputeTrace();
        //_routes.ILSRoute.ComputeCartesianPositions(true);
        //_routes.ILSRoute.ComputeTrace();
        //Debug.Log("_routes.ActiveRoute: "+_routes.ActiveRoute);


        /*currentRouteScriptableObject = levelData.MainRoute.CloneAndInit();

        for (var i = 0; i < currentRouteScriptableObject.Points.Length; i++) {
            Debug.Log("newSet.Point+" + i + ":" + currentRouteScriptableObject.Points[i].ID + "||" + currentRouteScriptableObject.Points[i].Name);
        }*/


        Session.Routes = _routes;


        Move.Instance.Init(levelData);

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

        //[ESA] High Performance, normal time calculation attempt regarding the solution of the original developer.
        //I am suspicious that SplittedTickComputeTrace() may be needed to run on demand for some situtations.
        Session.Settings.FlyingTickDuration = Time.deltaTime * Session.Settings.SpeedMultiplier;

        _pendingDeltaTime += Time.deltaTime;
        var tickDuration = Session.Settings.TickDuration(false) / Session.Settings.SpeedMultiplier;
        var ticksInDeltaTime = (int)(_pendingDeltaTime / tickDuration);

        _simulation.SplittedTickModHandling();
        _simulation.SplittedTickComputeTrace();
        for (int i = 0; i < ticksInDeltaTime; i++)
        {
            _simulation.SplittedTickSimulation();
        }
        _simulation.SplittedTickDraw();
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


        //Current Rounte Scriptable Object 
        Session.ModRoute.ClearModifiedFlags();
        Session.ModeSetWithPosition.ClearModifiedFlags();
        Session.ActiveRoute = Session.ModeSetWithPosition.CloneAndInit();
        //var routeScriptableObject = Session.ModeSetWithPosition.CloneAndInit();

        /* for (int i = 0; i < routeScriptableObject.Points.Length; i++) {
             Debug.Log("Session.ModeSetWithPosition[" + i + "]: " + routeScriptableObject.Points[i].ID + "||" + routeScriptableObject.Points[i].Name);
             var ele = routeScriptableObject.Points[i];
             int count = 0;
             for (int j = 0; j < routeScriptableObject.Points.Length; j++) {
                 if (ele.Name == routeScriptableObject.Points[j].Name) {
                     count++;
                 }
                 if (count > 1) {
                     routeScriptableObject.RemovePoint(j);
                     count = 1;
                 }
             }
         }*/
        //Session.ModeSetWithPosition = routeScriptableObject;
        //Session.ActiveRoute = Session.ModeSetWithPosition;

        //Debug.Log("Session.ActiveRoute: "+ Session.ActiveRoute);
        // for (int i = 0; i < Session.ActiveRoute.Points.Length; i++) {
        //Debug.Log("Session.ActiveRoute[" + i + "]: " + Session.ActiveRoute.Points[i].ID + "||" + Session.ActiveRoute.Points[i].Name);
        // }

        //Session.State.AutoSetLNAV(true, false);

        Session.ModRoute = null;
        Session.IsMod = false;
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
            Session.PlayerAircraft.ResetSeekProgress(PositionVirtualNode.PassedNodeIndex + 2, 0);
        }
    }

    private RouteScriptableObject ComputeILS()
    {
        int nodeCount = Session.CurrentLevel.MainRoute.Points.Length-1;
  
        RouteScriptableObject _ILS = Session.CurrentLevel.MainRoute.CloneAndInit();

        RoutePoint rwPoint = Session.CurrentLevel.MainRoute.Points[nodeCount];

        int course = Session.CurrentLevel.levelInfo.Course;

        _ILS.Points = new RoutePoint[] { rwPoint };

        for (int i = 0; (i < nodeCount); i++)
        {
            float distanceBetween = Session.CurrentLevel.MainRoute.Points[nodeCount-i].Distance;
            _ILS.AddNodeAtLast(1, $"NODE_{i}", course, distanceBetween);
        }
        _ILS.ComputeCartesianPositions(true);
        return _ILS;
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
