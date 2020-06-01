using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "FixSet", menuName = "ScriptableObjects/FixSet")]
public class FixSetScriptableObject : ScriptableObject
{
    public FixEntry[] Entries = new FixEntry[3];

    public static FixSetScriptableObject CreateDemo()
    {
        return new FixSetScriptableObject
        {
            Entries = new[]
            {
                new FixEntry { Name = "NORTA", Infos = new []
                    {
                       new FixInfo { RawDegrees = 300, NM = 30},
                       new FixInfo { RawDegrees = 30, NM = 10},
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

[Serialisable]
public class FixEntry
{
    public string Name;
    public FixInfo[] Infos = new FixInfo[3];
}

[Serialisable]
public class FixInfo
{
    public int? RawDegrees;
    public int? NM;

    public int? Degrees => RawDegrees == null ? null : 360 - RawDegrees;

    public FixInfo()
    {
        RawDegrees = null;
        NM = null;
    }
}
