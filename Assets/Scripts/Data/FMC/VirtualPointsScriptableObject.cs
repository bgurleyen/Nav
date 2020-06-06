using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Virtual Points Data", menuName = "ScriptableObjects/Virtual Points Data")]
public class VirtualPointsScriptableObject : ScriptableObject
{
    public VirtualPoints[] VirtualPointsItems;
}

[Serializable]
public class VirtualPoints
{
    public int Number;
    public float x;
    public float y;
}
