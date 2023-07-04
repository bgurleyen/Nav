using Navigation;
using UnityEngine;
using Unyawn.Utils;

public class GameManager : MonoBehaviour
{
    [SerializeField] private GameConfigScriptableObject _gameConfig;

    [SerializeField] private ComputedRoutes _routes = new();

    private Simulation _simulation;

    private LegsScreen _legsScreen;

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

        // incorrect, but faster game speed up
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
        Session.ModeSetFromPosition.ClearModifiedFlags();

        Session.ActiveRoute = Session.ModeSetFromPosition.CloneAndInit();

        //Session.State.AutoSetLNAV(true, false);

        Session.ModRoute = null;
        Session.IsMod = false;
        _legsScreen.DisplayOperation("ok");

        if (Session.ActiveRoute.HasActiveDirectApproach(out _) && Session.State.LNAV)
        {
            Session.PlayerAircraft.TryRejoinRoute();
        }

        Session.PlayerAircraft.ResetSeekProgress(Session.ActiveRoute.GetFirstViableNode.index, 0);
    }
}
