using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "LevelInfoData", menuName = "ScriptableObjects/LevelInfoData")]
public class LevelInfoScriptableObject : ScriptableObject
{
    public int LevelNumber;
    public string Destination;
    public string Star;
    public string Transition;
    public string Runway;
    public string FieldInfo;
    public string FreqCourse;
    public float ZFW;
    public float Fuel;
    public long CrzAltitude;
    public int CrzSpeed;
    public int F30Speed;
    public int DesEconSpeed;
    public int DesEconMach;
    public int GateIdx;
}
