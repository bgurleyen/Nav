using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Wind Table Data", menuName = "ScriptableObjects/Wind Table Data")]
public class WindTableScriptableObject : ScriptableObject
{
    public WindInfo[] WindInfoItems;
}

[Serializable]
public class WindInfo
{
    public long Altitude;
    public int Degrees;
    public int Knots;
}
