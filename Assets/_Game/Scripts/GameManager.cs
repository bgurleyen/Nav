using Navigation;
using UnityEngine;
using Unyawn.Utils;

public class GameManager : MonoBehaviour
{
    [SerializeField] private GameConfigScriptableObject _gameConfig;

    [SerializeField] private RouteScriptableObject _initialRoute;

    [SerializeField] private ComputedRoutes _routes = new();

    private Simulation _simulation;

    private void Awake()
    {
        UYServiceLocator.Register(this);
        Session.Settings = _gameConfig.Settings;
        
        _initialRoute.Init(true);
        _initialRoute.ComputeCartesianPositions();
        Session.OriginalReferenceRoute = _initialRoute.CloneAndInit();
    }

    private void Start()
    {
        var mcpUI = UYServiceLocator.Get<McpUI>();
        mcpUI.OnCenterModeSet += MCP_OnUICenterModeSet;
        mcpUI.OnMapModeSet += MCP_OnUIMapModeSet;
        mcpUI.OnPlanModeSet += MCP_OnUIPlanModeSet;
        mcpUI.OnFreeFlightToggle += MCP_OnUIFreeFlightToggle;

        var legsScreen = UYServiceLocator.Get<LegsScreen>();
        legsScreen.OnLeftCornerPressErase += LEGS_OnLeftCornerPressErase;
        legsScreen.OnExecButtonPress += LEGS_OnExecButtonPress;


        _simulation = UYServiceLocator.Get<Simulation>();


        _routes.ActiveRoute = _initialRoute.CloneAndInit();

        Session.Routes = _routes;

        _simulation.Init();
    }

    private void OnDestroy()
    {
        var mcpUI = UYServiceLocator.Get<McpUI>();
        mcpUI.OnCenterModeSet -= MCP_OnUICenterModeSet;
        mcpUI.OnMapModeSet -= MCP_OnUIMapModeSet;
        mcpUI.OnPlanModeSet -= MCP_OnUIPlanModeSet;
        mcpUI.OnFreeFlightToggle -= MCP_OnUIFreeFlightToggle;

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
        Session.Settings._flyingTickDuration =
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

    private void MCP_OnUIMapModeSet()
    {
        Session.Mode = DrawerMode.Map;
    }

    private void MCP_OnUICenterModeSet()
    {
        Session.Mode = DrawerMode.Center;
    }

    private void MCP_OnUIPlanModeSet()
    {
        Session.Mode = DrawerMode.Plan;
    }

    private void MCP_OnUIFreeFlightToggle(bool state)
    {
        switch (state)
        {
            case true:
                Session.PlayerAircraft.StartHeadingMode();
                break;
            default:
                if (!Session.PlayerAircraft.IsOnRoute)
                {
                    Session.PlayerAircraft.StartLNavMode();
                }

                break;
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
        MCP_OnUIFreeFlightToggle(true);
        MCP_OnUIFreeFlightToggle(false);
        UYServiceLocator.Get<McpUI>().RefreshHS();
    }
}
