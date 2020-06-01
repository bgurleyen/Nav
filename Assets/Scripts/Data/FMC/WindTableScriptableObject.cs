using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "WindTableData", menuName = "ScriptableObjects/WindTableData")]
public class WindTableScriptableObject : ScriptableObject
{
    public WindInfo[] WindInfoItems;
}

[Serialisable]
public class WindInfo
{
    public long Altitude;
    public int Degrees;
    public int Knots;
}
