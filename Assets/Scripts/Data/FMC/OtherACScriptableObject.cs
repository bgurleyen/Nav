using System;
using UnityEngine;

[CreateAssetMenu(fileName = "Other AC Data", menuName = "ScriptableObjects/Other AC Data")]
public class OtherACScriptableObject : ScriptableObject
{
    public ACInfo[] ACItems;
}


[Serializable]
public class ACInfo
{
    public int Point;
    public int Altitude; // keep in integer limits
    public int Speed;
}
