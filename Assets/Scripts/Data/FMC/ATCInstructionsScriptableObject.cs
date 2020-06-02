using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "ATC Instructions Data", menuName = "ScriptableObjects/ATC Instructions Data")]
public class ATCInstructionsScriptableObject : ScriptableObject
{
        public ATCInstructionInfo[] ATCInstrucitonItems;
}


[Serializable]
public class ATCInstructionInfo
{
        public int point;
        public int mode; //0 nochange, 1 DCT , 2 HDG , 3 ClrILS
        public long Altitude;
        public int VS;
        public int VS_nx; //0 exact, 1 min , 2 max
        public int Speed;
        public int Speed_nx;
}
