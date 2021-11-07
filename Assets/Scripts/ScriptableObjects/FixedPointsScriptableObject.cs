using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "FixSet", menuName = "ScriptableObjects/FixSet")]
public class FixedPointsScriptableObject : ScriptableObject
{
    public FixedPointEntry[] Entries = new FixedPointEntry[3];

    public static FixedPointsScriptableObject CreateDemo()
    {
        return new FixedPointsScriptableObject
        {
            Entries = new[]
            {
                new FixedPointEntry
                {
                    Name = "NORTA", Infos = new[]
                    {
                        new FixedPointInfo {RawDegrees = 300, NM = 30},
                        new FixedPointInfo {RawDegrees = 30, NM = 10},
                    }
                },
                //new FixEntry { Name = "DV575", Infos = new []
                //    {
                //       new FixInfo { RawDegrees = 300, NM = 30},
                //    }
                //},
            }
        };
    }

}

[Serializable]
public class FixedPointEntry
{
    public string Name;
    public FixedPointInfo[] Infos = new FixedPointInfo[3];
}

[Serializable]
public class FixedPointInfo
{
    public int? RawDegrees;
    public int? NM;

    public int? Degrees => 360 - RawDegrees;

    public FixedPointInfo()
    {
        RawDegrees = null;
        NM = null;
    }
}
