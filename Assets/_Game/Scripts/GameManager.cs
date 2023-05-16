using System;
using Gamelogic.Extensions;
using System.Collections.Generic;
using Legacy;
using Navigation;
using UnityEngine;
using Unyawn.Utils;

public class GameManager : Singleton<GameManager>
{
    [SerializeField] private GameConfigScriptableObject _gameConfig;

    [SerializeField] private RouteScriptableObject _initialRoute;

    [SerializeField] private ComputedRoutes _routes = new();

    private Simulation _simulation;

    private void Start()
    {
        var mcpUI = UYServiceLocator.Get<McpUI>();
        mcpUI.OnCenterModeSet += OnUICenterModeSet;
        mcpUI.OnMapModeSet += OnUIMapModeSet;
        mcpUI.OnPlanModeSet += OnUIPlanModeSet;
        mcpUI.OnFreeFlightToggle += OnUIFreeFlightToggle;

        var legsScreen = UYServiceLocator.Get<LegsScreen>();
        legsScreen.OnLeftCornerPressErase += LEGS_OnLeftCornerPressErase;
        
        _simulation = UYServiceLocator.Get<Simulation>();

        _initialRoute.Init(_gameConfig.Settings.DrawerUnitLength, _gameConfig.Settings.ForwardThreshold);

        _routes.ActiveRoute = _initialRoute.CloneAndInit();
        
        Session.Routes = _routes;
        

        _simulation.Init(_gameConfig.Settings);
    }
    
    private void OnDestroy()
    {
        var mcpUI = UYServiceLocator.Get<McpUI>();
        mcpUI.OnCenterModeSet -= OnUICenterModeSet;
        mcpUI.OnMapModeSet -= OnUIMapModeSet;
        mcpUI.OnPlanModeSet -= OnUIPlanModeSet;
        mcpUI.OnFreeFlightToggle -= OnUIFreeFlightToggle;

        var legsScreen = UYServiceLocator.Get<LegsScreen>();
        legsScreen.OnLeftCornerPressErase -= LEGS_OnLeftCornerPressErase;
    }


    private void LEGS_OnLeftCornerPressErase()
    {
        _simulation.EraseMod();
    }


    public void PressSwitchFreeFlight(bool state)
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

    private void OnUIMapModeSet()
    {
        Session.Mode = DrawerMode.Map;
    }

    private void OnUICenterModeSet()
    {
        Session.Mode = DrawerMode.Center;
    }

    private void OnUIPlanModeSet()
    {
        Session.Mode = DrawerMode.Plan;
    }

    private void OnUIFreeFlightToggle(bool state)
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

    public void ApplyMod()
    {
        Session.ModRoute.ClearModifiedFlags();
        Session.ModeSetWithPosition.ClearModifiedFlags();

        // if it's free flight, aircraft will switch to the temp path computed until rejoining active path
        Session.ActiveRoute = Session.PlayerAircraft.IsFreeFlight ? Session.ModRoute : Session.ModeSetWithPosition;

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
        PressSwitchFreeFlight(true);
        PressSwitchFreeFlight(false);
        UYServiceLocator.Get<McpUI>().RefreshHS();
    }
}

[Serializable]
public class ComputedRoutes
{
    [ReadOnly] public RouteScriptableObject ActiveRoute;
    [ReadOnly] public RouteScriptableObject ModRoute;
    [ReadOnly] public RouteScriptableObject ModeSetWithPosition;
    [ReadOnly] public FixedPointsScriptableObject FixedPoints;
}