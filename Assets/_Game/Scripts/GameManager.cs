using System;
using Gamelogic.Extensions;
using System.Collections.Generic;
using UnityEngine;

public class GameManager : Singleton<GameManager>
{
    [SerializeField] private GameConfigScriptableObject _gameConfig;
    [SerializeField] private RouteScriptableObject initialRoute;

    [Header("Computed")]
    public RouteScriptableObject ActiveRoute;
    public RouteScriptableObject ModRoute;
    public RouteScriptableObject ModeSetWithPosition;
    public FixedPointsScriptableObject FixedPoints;
    
    public bool isDebug;

    public delegate void OnOperationMadeDelegate();
    public event OnOperationMadeDelegate OnOperationMade;

    public Aircraft Aircraft;
    public bool IsMod { get; private set; }

    private bool queueEraseMode;

    // if we detect that during MOD the current node has passed we reExecute all the commands until that point
    private int modReExecutedForIndex = -1;

    private List<ICommand> cachedCommands;

    private void Awake()
    {
        LinesComputer.Init(_gameConfig.Settings);
        Aircraft = new Aircraft(_gameConfig);
    }

    private void Start()
    {
        FixedPoints = FixedPointsScriptableObject.CreateDemo();

        ActiveRoute = initialRoute.Clone();
        ActiveRoute.InitIds();
 

        DataHandler.BuildSetDetails(ActiveRoute);

        Drawer.Instance.ResetMode();
        ComputeActive();
        ComputeMod();
        Drawer.Instance.Display();
        Drawer.Instance.ShowMapMode();

        Aircraft.ResetOnActiveSet(170, 21600);
    }

    private void FixedUpdate()
    {
        if (IsMod && queueEraseMode)
        {
            EraseMod();
        }

        queueEraseMode = false;

        ComputeActive();
        ComputeMod();
      
        Aircraft.Advance();
      
        if (Drawer.Instance.Mode != DrawerMode.Suspeded)
        {
            Drawer.Instance.Clear();
            Drawer.Instance.Display();
        }
    }

    public void ComputeActive()
    {
        if (ActiveRoute == null)
        {
            return;
        }

        ActiveRoute.ComputeSet(false);
    }

    private void ComputeMod()
    {
        if (ModRoute == null)
        {
            modReExecutedForIndex = -1;
            return;
        }

        if (Aircraft.IsOnRoute)
        {
            // mod always has to include the last passed active node ( all the passed nodes ) 
            // otherwise it is invalid - will reapply all the commands
            var passedNodeIndex = PositionVirtualNode.PassedNodeIndex;

            if (passedNodeIndex != modReExecutedForIndex)
            {
                if (ActiveRoute.Points[passedNodeIndex].ID != ModRoute.Points[passedNodeIndex].ID)
                {
                    ReExecuteCachedCommands();
                    Debug.Log("Reapplied MOD");
                }

                modReExecutedForIndex = passedNodeIndex;
            }
        }

        ModeSetWithPosition = ModRoute.Clone(); // refactor

        ModeSetWithPosition.AddDisplayPositionNode();

        ModeSetWithPosition.ComputeSet(true);
    }

    private void OnDrawGizmos()
    {
        Aircraft?.DrawGizmos();
    }

    private void CheckModForOperation()
    {
        if (IsMod) return;

        // if this is the first modification generate a new mod from current active
        ModRoute = ActiveRoute.Clone();

        cachedCommands = new List<ICommand>();
        IsMod = true;
        
        // in case the aircraft was in free flight with intersection valid shortcut mod until the node after intersection
        if (!Aircraft.IsOnRoute)
        {
            ExecuteShortcutOnMod(new ExecuteShortcutOnModeCommand
            {
                FromNodeId = ModRoute.Points[1].ID,
                ToNodeId = ModRoute.Points[Aircraft.RoutePathLocalization.CurrentNodeIndex].ID
            });
        }
    }

    public void PressSwitchFreeFlight(bool state)
    {
        switch (state)
        {
            case true:
                Aircraft.StartHeadingMode();
                break;
            default:
                if (!Aircraft.IsOnRoute)
                {
                    Aircraft.StartLNavMode(Aircraft.RejoinRouteMode.Manual);
                }
                break;
        }
    }

    public void ExecuteDeleteRestrictions(DeleteRestrictionsCommand command)
    {
        CheckModForOperation();
        cachedCommands.Add(command);
        
        MainScreen.Instance.DisplayOperation("ERASE");

        ModRoute.GetPoint(command.NodeId, out var node);
        node.RawAltitude = "";
        node.RawSpeed = 0;
        
        DataHandler.BuildSetDetails(ModRoute);
    }

    public void ExecuteAddSpeedRegulation(AddSpeedRegulationCommand command)
    {
        CheckModForOperation();
        cachedCommands.Add(command);
        
        MainScreen.Instance.DisplayOperation("ERASE");
        ModRoute.GetPoint(command.NodeId, out var node);
        node.RawSpeed = command.Regulation;
        node.IsSpeedModified = true;
        DataHandler.BuildSetDetails(ModRoute);

        OnOperationMade?.Invoke();
    }
    
    public void ExecuteAddAltitudeRegulation(AddAltitudeRegulationCommand command)
    {
        CheckModForOperation();
        cachedCommands.Add(command);
        
        MainScreen.Instance.DisplayOperation("ERASE");
        ModRoute.GetPoint(command.NodeId, out var _node);
        //maybe move this to route class to also do some tests?
        _node.RawAltitude = command.Regulation;
        _node.IsAltitudeModified = true;
        DataHandler.BuildSetDetails(ModRoute);

        OnOperationMade?.Invoke();
    }

    public void ExecuteShortcutOnMod(ExecuteShortcutOnModeCommand command)
    {
        CheckModForOperation();
        cachedCommands.Add(command);

        ModRoute.GetPoint(command.FromNodeId, out var node);
        MainScreen.Instance.DisplayOperation("ERASE", ToDegreesDisplay(node.RawDegrees));
        ModRoute.ShortcutNodes(command.FromNodeId, command.ToNodeId, out var _);
        DataHandler.BuildSetDetails(ModRoute);

        OnOperationMade?.Invoke();
    }

    private static string ToDegreesDisplay(float value)
    {
        return $"{value:000}°";
    }

    public void ExecuteInsertRelativeOnDirectionOnMod(ExecuteRelativeOnDirectionOnMod command)
    {
        CheckModForOperation();
        cachedCommands.Add(command);

        ModRoute.AddRelativeNodeOnDirection(command.FromNodeId, command.Distance, command.RelativeNodeId, out var _, out var _);
        DataHandler.BuildSetDetails(ModRoute);
        MainScreen.Instance.DisplayOperation("ERASE");

        OnOperationMade?.Invoke();
    }


    public void ExecuteInsertRelativeOnMod(InsertRelativeCommand command)
    {
        CheckModForOperation();
        cachedCommands.Add(command);

        ModRoute.AddRelativeNodeBefore(command.BeforeNodeId, command.RawDegrees, command.Distance, command.RelativeNodeId, out _, true);
        DataHandler.BuildSetDetails(ModRoute);
        MainScreen.Instance.DisplayOperation(MainScreen.Keywords.ERASE);

        OnOperationMade?.Invoke();
    }

    public void ExecuteLinearApproachOnMod(ExecuteAddLinearApproachCommand command)
    {
        CheckModForOperation();
        cachedCommands.Add(command);

        ModRoute.CreateLinearApproach(command.ToNodeId, command.Angle);
        DataHandler.BuildSetDetails(ModRoute);
        MainScreen.Instance.DisplayOperation(MainScreen.Keywords.ERASE, ToDegreesDisplay(command.Angle), true );

        OnOperationMade?.Invoke();
    }

    public void ReExecuteCachedCommands()
    {
        EraseMod();

        for (var i = 0; i < cachedCommands.Count; i++)
        {
            var command = cachedCommands[i];
            switch (command)
            {
                case InsertRelativeCommand relativeCommand:
                    ExecuteInsertRelativeOnMod(relativeCommand);
                    break;
                case ExecuteShortcutOnModeCommand modeCommand:
                    ExecuteShortcutOnMod(modeCommand);
                    break;
                case ExecuteAddLinearApproachCommand approachCommand:
                    ExecuteLinearApproachOnMod(approachCommand);
                    break;
            }
        }
    }

    public void ApplyMod()
    {
        ModRoute.ClearModifiedFlags();
        ModeSetWithPosition.ClearModifiedFlags();

        // if it's free flight, aircraft will switch to the temp path computed until rejoining active path
        ActiveRoute = Aircraft.IsFreeFlight ? ModRoute : ModeSetWithPosition;

        if (!Aircraft.IsOnRoute)
        {
            Aircraft.StartLNavMode(Aircraft.RejoinRouteMode.NextRouteNode);
        }

        ModRoute = null;
        IsMod = false;
        MainScreen.Instance.DisplayOperation("0k");

        if (ActiveRoute.ActiveDirectApproach)
        {
            SwitchThroughHeading();
        }
        
    }

    public void EraseMod()
    {
        ModRoute = null;
        IsMod = false;
        MainScreen.Instance.DisplayOperation("0k");
    }

    public void QueueEraseMode()
    {
        queueEraseMode = true;
    }

    public void SwitchThroughHeading()
    {
        Calculator.RHeading = (int) Aircraft.TargetHeading;
        PressSwitchFreeFlight(true);
        PressSwitchFreeFlight(false);
        McpUI.Instance.RefreshHS();
    }
}
