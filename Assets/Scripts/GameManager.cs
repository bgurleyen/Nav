using System;
using Gamelogic.Extensions;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public class GameManager : Singleton<GameManager>
{
    public delegate void OnOperationMadeDelegate();
    public event OnOperationMadeDelegate OnOperationMade;

    [SerializeField] RouteScriptableObject initialRoute;

    public Aircraft Aircraft = new Aircraft();
    public PathLines PathLines = new PathLines();

    [Header("Computed")]
    public RouteScriptableObject ActiveRoute;
    public RouteScriptableObject ModRoute;
    public RouteScriptableObject ModeSetWithPosition;
    public FixedPointsScriptableObject FixedPoints;

    public RouteScriptableObject InitialRoute => initialRoute;

    public int UnreachedNodeIndex => Aircraft.UnreachedVertex.CurrentLine;

    public bool IsMod { get; private set; }
    bool queueEraseMode;

    List<ICommand> cachedCommands;

    void Start()
    {
        FixedPoints = FixedPointsScriptableObject.CreateDemo();

        ActiveRoute = initialRoute.Clone();
        ActiveRoute.InitIds();
 

        DataHandler.BuildSetDetails(ActiveRoute);

        Drawer.Instance.ResetMode();
        Drawer.Instance.ComputeActive();
        Drawer.Instance.ComputeMod();
        Drawer.Instance.Display();
        Drawer.Instance.ShowMapMode();

        Aircraft.ResetOnActiveSet(270, 21600);
    }
    
    

    void FixedUpdate()
    {
        if (IsMod && queueEraseMode)
        {
            EraseMod();
        }

        queueEraseMode = false;

        Drawer.Instance.ComputeActive();
        Drawer.Instance.ComputeMod();
      
        Aircraft.Advance();
      
        if (Drawer.Instance.Mode != DrawerMode.Suspeded)
        {
            Drawer.Instance.Clear();
            Drawer.Instance.Display();
        }
    }

    void CheckModForOperation()
    {
        if (IsMod) return;
        
        // if this is the first modification generate a new mod from current active
        ModRoute = ActiveRoute.Clone();

        cachedCommands = new List<ICommand>();
        IsMod = true;
    }

    public void SwitchFreeFlight(bool state)
    {
        switch (state)
        {
            case true:
                Aircraft.StartHeadingMode();
                break;
            default:
                Aircraft.StartLNavMode();
                break;
        }
    }

    public void ExecuteAddSpeedRegulation(AddSpeedRegulationCommand command)
    {
        CheckModForOperation();
        cachedCommands.Add(command);
        
        MainScreen.Instance.DisplayOperation("ERASE");
        ModRoute.GetPoint(command.NodeId).RawSpeed = command.Regulation;
        DataHandler.BuildSetDetails(ModRoute);

        OnOperationMade?.Invoke();
    }
    
    public void ExecuteAddAltitudeRegulation(AddAltitudeRegulationCommand command)
    {
        CheckModForOperation();
        cachedCommands.Add(command);
        
        MainScreen.Instance.DisplayOperation("ERASE");
        ModRoute.GetPoint(command.NodeId).RawAltitude = command.Regulation;
        DataHandler.BuildSetDetails(ModRoute);

        OnOperationMade?.Invoke();
    }

    public void ExecuteShortcutOnMod(ExecuteShortcutOnModeCommand command)
    {
        CheckModForOperation();
        cachedCommands.Add(command);

        MainScreen.Instance.DisplayOperation("ERASE", $"{ModRoute.GetPoint(command.FromNodeId).RawDegrees:000}°");
        ModRoute.ShortcutNodes(command.FromNodeId, command.ToNodeId, out var _);
        DataHandler.BuildSetDetails(ModRoute);

        OnOperationMade?.Invoke();
    }

    public void ExecuteInsertRelativeOnDirectionOnMod(ExecuteRelativeOnDirectionOnMod command)
    {
        CheckModForOperation();
        cachedCommands.Add(command);

        ModRoute.AddRelativeNodeOnDirection(command.FromNodeId, command.Distance, out var _, out var _);
        DataHandler.BuildSetDetails(ModRoute);
        MainScreen.Instance.DisplayOperation("ERASE");

        OnOperationMade?.Invoke();
    }


    public void ExecuteInsertRelativeOnMod(InsertRelativeCommand command)
    {
        CheckModForOperation();
        cachedCommands.Add(command);

        ModRoute.AddRelativeNodeBefore(command.FromNodeId, command.RawDegrees, command.Distance, out var _, true);
        DataHandler.BuildSetDetails(ModRoute);
        MainScreen.Instance.DisplayOperation("ERASE");

        OnOperationMade?.Invoke();
    }

    public void ExecuteLinearApproachOnMod(ExecuteAddLinearApproachCommand command)
    {
        CheckModForOperation();
        cachedCommands.Add(command);

        ModRoute.CreateLinearApproach(command.ToNodeId, command.Angle);
        DataHandler.BuildSetDetails(ModRoute);
        MainScreen.Instance.DisplayOperation("ERASE");

        OnOperationMade?.Invoke();
    }

    public void ReExecuteCachedCommands()
    {
        EraseMod();
        var _cachedCommands = this.cachedCommands;

        for (var i = 0; i < _cachedCommands.Count; i++)
        {
            var _command = _cachedCommands[i];
            switch (_command)
            {
                case InsertRelativeCommand _relativeCommand:
                    ExecuteInsertRelativeOnMod(_relativeCommand);
                    break;
                case ExecuteShortcutOnModeCommand _modeCommand:
                    ExecuteShortcutOnMod(_modeCommand);
                    break;
                case ExecuteAddLinearApproachCommand _approachCommand:
                    ExecuteLinearApproachOnMod(_approachCommand);
                    break;
            }
        }
    }

    public void ApplyMod()
    {
        ModRoute.ClearModifiedFlags();

        ActiveRoute = ModeSetWithPosition;
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
        SwitchFreeFlight(true);
        SwitchFreeFlight(false);
        McpUI.Instance.RefreshHS();
    }
}
