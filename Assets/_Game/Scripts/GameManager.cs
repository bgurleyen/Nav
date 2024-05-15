using Navigation;
using UnityEngine;
using Unyawn.Utils;

public class GameManager : MonoBehaviour {
    [SerializeField] private GameConfigScriptableObject _gameConfig;

    [SerializeField] private ComputedRoutes _routes = new();

    private Simulation _simulation;

    private LegsScreen _legsScreen;

    //public RouteScriptableObject currentRouteScriptableObject;

    private void Awake() {

        UYServiceLocator.Register(this);

        Session.Settings = _gameConfig.Settings;
    }

    private void Start() {
        var mcpUI = UYServiceLocator.Get<McpUI>();

        _simulation = UYServiceLocator.Get<Simulation>();

        Session.State = new State(mcpUI);

        _legsScreen = UYServiceLocator.Get<LegsScreen>();
        _legsScreen.OnLeftCornerPressErase += LEGS_OnLeftCornerPressErase;
        _legsScreen.OnExecButtonPress += LEGS_OnExecButtonPress;

        InitForLevel(0);
    }

    private void InitForLevel(int index) {
        var levelData = _gameConfig.LevelsData[index];
        Session.CurrentLevel = levelData;

        levelData.MainRoute.Init(true);
        levelData.MainRoute.ComputeCartesianPositions();
        Debug.Log("Init For Level  1111111 ");
        Session.OriginalReferenceRoute = levelData.MainRoute.CloneAndInit();
       //Debug.Log("OriginalReferenceRoute: "+ Session.OriginalReferenceRoute);

        Debug.Log("Init For Level  222222 ");
        _routes.ActiveRoute = levelData.MainRoute.CloneAndInit();
        //Debug.Log("_routes.ActiveRoute: "+_routes.ActiveRoute);

        /*currentRouteScriptableObject = levelData.MainRoute.CloneAndInit();

        for (var i = 0; i < currentRouteScriptableObject.Points.Length; i++) {
            Debug.Log("newSet.Point+" + i + ":" + currentRouteScriptableObject.Points[i].ID + "||" + currentRouteScriptableObject.Points[i].Name);
        }*/
       

        Session.Routes = _routes;


        Move.Instance.Init(levelData);

        _simulation.Init();
    }

    private void OnDestroy() {

        if (UYServiceLocator.Has<LegsScreen>()) {
            var legsScreen = UYServiceLocator.Get<LegsScreen>();
            legsScreen.OnLeftCornerPressErase -= LEGS_OnLeftCornerPressErase;
            legsScreen.OnExecButtonPress -= LEGS_OnExecButtonPress;
        }
    }

    private float _pendingDeltaTime;

    private void Update() {
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
        for (int i = 0; i < ticksInDeltaTime; i++) {
            _simulation.SplittedTickSimulation();
        }
        _simulation.SplittedTickDraw();
        _pendingDeltaTime -= ticksInDeltaTime * tickDuration;
    }

    private void LEGS_OnLeftCornerPressErase() {
        _simulation.EraseMod();
    }

    private void LEGS_OnExecButtonPress() {
        //Debug.Log("Session Mod " + Session.IsMod);
        if (Session.IsMod) {
            //Debug.Log("Apply Mod " + Session.IsMod);  
            ApplyMod();
        }
    }

    private void ApplyMod() {


        //Current Rounte Scriptable Object 
        Session.ModRoute.ClearModifiedFlags();
        Session.ModeSetWithPosition.ClearModifiedFlags();
        Debug.Log("Apply Mod 333333333");
        Session.ActiveRoute = Session.ModeSetWithPosition.CloneAndInit();
        for (int i = 0; i < Session.ActiveRoute.Points.Length; i++) {
            Debug.Log("Session.ModeSetWithPosition["+i+"]: " + Session.ActiveRoute.Points[i].ID + "||" + Session.ActiveRoute.Points[i].Name);
        }
        //Debug.Log("Session.ActiveRoute: "+ Session.ActiveRoute);

        for (int i = 0; i < Session.ActiveRoute.Points.Length; i++) {
            Debug.Log("Session.ActiveRoute["+i+"]: " + Session.ActiveRoute.Points[i].ID + "||" + Session.ActiveRoute.Points[i].Name);
        }

        //Session.State.AutoSetLNAV(true, false);

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
    }
}
