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

        _initialRoute.Init(true);

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

        var legsScreen = UYServiceLocator.Get<LegsScreen>();
        legsScreen.OnLeftCornerPressErase -= LEGS_OnLeftCornerPressErase;
        legsScreen.OnExecButtonPress -= LEGS_OnExecButtonPress;
    }

    private void Update()
    {
        _simulation.Tick(Time.fixedDeltaTime);
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
                    Session.PlayerAircraft.StartLNavMode(Aircraft.RejoinRouteMode.Manual);
                }

                break;
        }
    }

    private void ApplyMod()
    {
        Session.ModRoute.ClearModifiedFlags();
        Session.ModeSetWithPosition.ClearModifiedFlags();

        // if it's free flight, aircraft will switch to the temp path computed until rejoining active path
        if (Session.PlayerAircraft.IsFreeFlight)
        {
            Session.ActiveRoute = Session.ModRoute;
        }
        else
        {
            Session.ActiveRoute = Session.ModeSetWithPosition;
            Session.PlayerAircraft.ResetSeekProgress(PositionVirtualNode.PassedNodeIndex+2 ,0);
        }


        if (!Session.PlayerAircraft.IsOnRoute)
        {
            Session.PlayerAircraft.StartLNavMode(Aircraft.RejoinRouteMode.NextRouteNode);
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
