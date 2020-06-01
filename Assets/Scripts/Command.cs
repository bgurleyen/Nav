using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface ICommand { }


public struct InsertRelativeCommand : ICommand
{
    public int FromNodeId;
    public int RawDegrees;
    public int Distance;
}

public struct ExecuteShortcutOnModeCommand : ICommand
{
    public int FromNodeId;
    public int ToNodeId;
}

public struct ExecuteRelativeOnDirectionOnMod : ICommand
{
    public int FromNodeId;
    public int Distance;
}

public struct ExecuteAddLinearApproachCommand : ICommand
{
    public int ToNodeId;
    public int Angle;
}
