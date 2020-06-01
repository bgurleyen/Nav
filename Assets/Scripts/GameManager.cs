using Gamelogic.Extensions;
using System.Collections.Generic;
using UnityEngine;

public class GameManager : Singleton<GameManager>
{
    public delegate void OnOperationMadeDelegate();
    public event OnOperationMadeDelegate OnOperationMade;

    [SerializeField] DataSetScriptableObject initialDataSet;

    public Aircraft Aircraft = new Aircraft();
    public PathLines PathLines = new PathLines();

    [Header("Computed")]
    public DataSetScriptableObject ActiveSet;
    public DataSetScriptableObject ModSet;
    public DataSetScriptableObject ModeSetWithPosition;
    public FixSetScriptableObject FixSet;

    public int UnreachedNodeIndex => Aircraft.UnreachedVertex.CurrentLine;

    public bool IsMod { get; private set; }
    bool queueEraseMode;

    List<ICommand> cachedCommands;

    void Start()
    {
        FixSet = FixSetScriptableObject.CreateDemo();

        ActiveSet = initialDataSet.Clone();
        ActiveSet.InitIds();
 

        DataHandler.BuildSetDetails(ActiveSet);

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
        ModSet = ActiveSet.Clone();

        cachedCommands = new List<ICommand>();
        IsMod = true;
    }

    public void ExecuteActivateFreeFlight()
    {
        if (Aircraft.IsFreeFlight)
        {
            Aircraft.EndFreeFlight();
        }
        else
        {
            Aircraft.StartFreeFlight();
        }
    }

    public void SteerFlight(float degrees)
    {
        Aircraft.IndicateTargetHeading( Aircraft.Heading + degrees);
    }
    
    public void ExecuteShortcutOnMod(ExecuteShortcutOnModeCommand command)
    {
        CheckModForOperation();
        cachedCommands.Add(command);

        MainScreen.Instance.DisplayOperation("ERASE", $"{ModSet.GetPoint(command.FromNodeId).RawDegrees:000}°");
        ModSet.ShortcutNodes(command.FromNodeId, command.ToNodeId, out var _);
        DataHandler.BuildSetDetails(ModSet);

        OnOperationMade?.Invoke();
    }

    public void ExecuteInsertRelativeOnDirectionOnMod(ExecuteRelativeOnDirectionOnMod command)
    {
        CheckModForOperation();
        cachedCommands.Add(command);

        ModSet.AddRelativeNodeOnDirection(command.FromNodeId, command.Distance, out var _, out var _);
        DataHandler.BuildSetDetails(ModSet);
        MainScreen.Instance.DisplayOperation("ERASE");

        OnOperationMade?.Invoke();
    }


    public void ExecuteInsertRelativeOnMod(InsertRelativeCommand command)
    {
        CheckModForOperation();
        cachedCommands.Add(command);

        ModSet.AddRelativeNodeBefore(command.FromNodeId, command.RawDegrees, command.Distance, out var _, true);
        DataHandler.BuildSetDetails(ModSet);
        MainScreen.Instance.DisplayOperation("ERASE");

        OnOperationMade?.Invoke();
    }

    public void ExecuteLinearApproachOnMod(ExecuteAddLinearApproachCommand command)
    {
        CheckModForOperation();
        cachedCommands.Add(command);

        ModSet.CreateLinearApproach(command.ToNodeId, command.Angle);
        DataHandler.BuildSetDetails(ModSet);
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
        ModSet.ClearModifiedFlags();

        ActiveSet = ModeSetWithPosition;
        ModSet = null;
        IsMod = false;
        MainScreen.Instance.DisplayOperation("0k");
    }

    public void EraseMod()
    {
        ModSet = null;
        IsMod = false;
        MainScreen.Instance.DisplayOperation("0k");
    }

    public void QueueEraseMode()
    {
        queueEraseMode = true;
    }
}
