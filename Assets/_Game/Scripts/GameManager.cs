using Navigation;
using UnityEngine;
using Unyawn.Utils;

public class GameManager : MonoBehaviour
{
    [SerializeField] private GameConfigScriptableObject _gameConfig;

    [SerializeField] private ComputedRoutes _routes = new();

    private Simulation _simulation;

    private void Awake()
    {
        UYServiceLocator.Register(this);
        Session.Settings = _gameConfig.Settings;
    }

    private void Start()
    {
        var mcpUI = UYServiceLocator.Get<McpUI>();

        var legsScreen = UYServiceLocator.Get<LegsScreen>();
        legsScreen.OnLeftCornerPressErase += LEGS_OnLeftCornerPressErase;
        legsScreen.OnExecButtonPress += LEGS_OnExecButtonPress;


        Session.State = new State(mcpUI);
        _simulation = UYServiceLocator.Get<Simulation>();



        InitForLevel(0);
    }

    private void InitForLevel(int index)
    {
        var levelData = _gameConfig.LevelsData[index];
        Session.CurrentLevel = levelData;

        levelData.MainRoute.Init(true);
        levelData.MainRoute.ComputeCartesianPositions();
        Session.OriginalReferenceRoute = levelData.MainRoute.CloneAndInit();

        _routes.ActiveRoute = levelData.MainRoute.CloneAndInit();

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
        if (Session.PlayerAircraft.Speed == 0)
        {
            return;
        }

        // normal time calculation, but big computations
        // _pendingDeltaTime += Time.deltaTime;
        // var tickDuration = Session.Settings.TickDuration(false) / Session.Settings.SpeedMultiplier;
        // var ticksInDeltaTime = (int)(_pendingDeltaTime  / tickDuration);
        // for (int i = 0; i < ticksInDeltaTime; i++)
        // {
        //     _simulation.Tick();
        // }
        //
        // _pendingDeltaTime -= ticksInDeltaTime * tickDuration;

        // incorrect game speed up
        Session.Settings.FlyingTickDuration =
            Time.deltaTime * Session.Settings.SpeedMultiplier;
        _simulation.Tick();
    }

    private void LEGS_OnLeftCornerPressErase()
    {
        _simulation.EraseMod();
    }

    private void LEGS_OnExecButtonPress()
    {
        if (Session.IsMod)
        {
            ApplyMod();
        }
    }

    private void ApplyMod()
    {
        Session.ModRoute.ClearModifiedFlags();
        Session.ModeSetWithPosition.ClearModifiedFlags();

        Session.ActiveRoute = Session.ModeSetWithPosition;
        Session.PlayerAircraft.ResetSeekProgress(PositionVirtualNode.PassedNodeIndex + 2, 0);

        if (!Session.PlayerAircraft.IsOnRoute)
        {
            Session.PlayerAircraft.StartLNavMode();
        }

        Session.ModRoute = null;
        Session.IsMod = false;
        MainScreen.Instance.DisplayOperation("0k");

        if (Session.ActiveRoute.ActiveDirectApproach)
        {
            SwitchThroughHeading();
        }
    }

    public void SwitchThroughHeading()
    {
        Calculator.RHeading = (int)Session.PlayerAircraft.TargetHeading;
        Session.PlayerAircraft.StartHeadingMode();
        if (!Session.PlayerAircraft.IsOnRoute)
        {
            Session.PlayerAircraft.StartLNavMode();
        }

        UYServiceLocator.Get<McpUI>().RefreshHS();
    }
}
