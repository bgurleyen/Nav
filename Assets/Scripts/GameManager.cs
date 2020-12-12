using Gamelogic.Extensions;
using System.Collections.Generic;
using UnityEngine;

public class GameManager : Singleton<GameManager>
{
    public delegate void OnOperationMadeDelegate();
    public event OnOperationMadeDelegate OnOperationMade;

    [SerializeField] RouteScriptableObject initialRoute;

    public readonly Aircraft Aircraft = new Aircraft();

    [Header("Computed")]
    public RouteScriptableObject ActiveRoute;
    public RouteScriptableObject ModRoute;
    public RouteScriptableObject ModeSetWithPosition;
    public FixedPointsScriptableObject FixedPoints;

    public bool IsMod { get; private set; }
    bool queueEraseMode;

    // if we detect that during MOD the current node has passed we reExecute all the commands until that point
    int modReExecutedForIndex = -1;
    
    List<ICommand> cachedCommands;

    void Start()
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

        Aircraft.ResetOnActiveSet(270, 21600);
    }
    
    

    void FixedUpdate()
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

    void ComputeMod()
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
            var _passedNodeIndex = PositionVirtualNode.PassedNodeIndex;

            if (_passedNodeIndex != modReExecutedForIndex)
            {
                if (ActiveRoute.Points[_passedNodeIndex].ID != ModRoute.Points[_passedNodeIndex].ID)
                {
                    ReExecuteCachedCommands();
                    Debug.Log("Reapplied MOD");
                }

                modReExecutedForIndex = _passedNodeIndex;
            }
        }

        ModeSetWithPosition = ModRoute.Clone(); // refactor

        ModeSetWithPosition.AddDisplayPositionNode();

        ModRoute.ComputeSet(true);
    }

    void OnDrawGizmos()
    {
        Aircraft.DrawGizmos();
    }

    void CheckModForOperation()
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

    public void SwitchFreeFlight(bool state)
    {
        switch (state)
        {
            case true:
                Aircraft.StartHeadingMode();
                break;
            default:
                if (!Aircraft.IsOnRoute)
                {
                    Aircraft.StartLNavMode();
                }
                break;
        }
    }

    public void ExecuteDeleteRestrictions(DeleteRestrictionsCommand command)
    {
        CheckModForOperation();
        cachedCommands.Add(command);
        
        MainScreen.Instance.DisplayOperation("ERASE");

        ModRoute.GetPoint(command.NodeId, out var _node);
        _node.RawAltitude = "";
        _node.RawSpeed = 0;
        
        DataHandler.BuildSetDetails(ModRoute);
    }

    public void ExecuteAddSpeedRegulation(AddSpeedRegulationCommand command)
    {
        CheckModForOperation();
        cachedCommands.Add(command);
        
        MainScreen.Instance.DisplayOperation("ERASE");
        ModRoute.GetPoint(command.NodeId, out var _node);
        _node.RawSpeed = command.Regulation;
        _node.IsSpeedModified = true;
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

        ModRoute.GetPoint(command.FromNodeId, out var _node);
        MainScreen.Instance.DisplayOperation("ERASE", ToDegreesDisplay(_node.RawDegrees));
        ModRoute.ShortcutNodes(command.FromNodeId, command.ToNodeId, out var _);
        DataHandler.BuildSetDetails(ModRoute);

        OnOperationMade?.Invoke();
    }

    static string ToDegreesDisplay(float value)
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
        var _cachedCommands = cachedCommands;

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
        ModeSetWithPosition.ClearModifiedFlags();

        ActiveRoute = ModeSetWithPosition;
        
        Aircraft.OnAppliedMod();
        
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
