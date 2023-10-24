using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface ICommand { }

public struct DeleteRestrictionsCommand : ICommand
{
    public int NodeId;
}

public struct AddAltitudeRegulationCommand : ICommand
{
    public int NodeId;
    public string Regulation;
}


public struct AddSpeedRegulationCommand : ICommand
{
    public int NodeId;
    public int Regulation;
}


public struct InsertRelativeCommand : ICommand
{
    /// <summary>
    /// The node before which the discontinuity will be applied
    /// </summary>
    public int BeforeNodeId;
    public int RawDegrees;
    public int Distance;
    /// <summary>
    /// The node in the scratchpad on which the relative calculations will be made
    /// </summary>
    public int RelativeNodeId;
    
}

public struct ExecuteRelativeOnDirectionOnMod : ICommand
{
    public int FromNodeId;
    public int Distance;
    public int RelativeNodeId;
}

public struct ExecuteOriginalInsertOnModCommand : ICommand
{
    public int OriginalRouteNodeId;
    public int OnTopNodeId;
}

public struct ExecuteShortcutOnModeCommand : ICommand
{
    public int FromNodeId;
    public int ToNodeId;
}

public struct ExecuteAddLinearApproachCommand : ICommand
{
    public int ToNodeId;
    public int Angle;
}
