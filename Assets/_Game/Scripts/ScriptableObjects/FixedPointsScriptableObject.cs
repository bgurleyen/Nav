using Navigation;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEngine;
using static UnityEditor.PlayerSettings;

[CreateAssetMenu(fileName = "FixSet", menuName = "ScriptableObjects/FixSet")]
public class FixedPointsScriptableObject : ScriptableObject
{
    public FixedPointEntry[] Entries = new FixedPointEntry[3];

    public static FixedPointsScriptableObject CreateDemo()
    {
        var demo = CreateInstance<FixedPointsScriptableObject>();

        demo.Entries = new[]
        {   new FixedPointEntry
            {
                Name = "DLE", Infos = new[]
                {
                    //new FixedPointInfo {RawDegrees = 300, NM = 8},
                    new FixedPointInfo {RawDegrees = 300, NM = 5},
                    new FixedPointInfo {RawDegrees = 30, NM = 3},
                    new FixedPointInfo {RawDegrees = 30, NM = 1},
                    //new FixedPointInfo {RawDegrees = 300, NM = 30},
                    //new FixedPointInfo {RawDegrees = 30, NM = 10},
                }
            },
            new FixedPointEntry
            {
                Name = "DV581", Infos = new[]
                {
                    //new FixedPointInfo {RawDegrees = 300, NM = 8},
                    new FixedPointInfo {RawDegrees = 300, NM = 5},
                    new FixedPointInfo {RawDegrees = 30, NM = 3},
                    new FixedPointInfo {RawDegrees = 30, NM = 1},
                    //new FixedPointInfo {RawDegrees = 300, NM = 30},
                    //new FixedPointInfo {RawDegrees = 30, NM = 10},
                }
            },
            //new FixEntry { Name = "DV575", Infos = new []
            //    {
            //       new FixInfo { RawDegrees = 300, NM = 30},
            //    }
            //},
        };
        return demo;
    }

    public static FixedPointsScriptableObject CloneAndInit()
    {
        var newSet = CreateInstance<FixedPointsScriptableObject>();

        newSet.Entries = new FixedPointEntry[] { };

        return newSet;
    }

    public void AddOrUpdateFixedPointEntry(string _name,FixedPointInfo addPointInfo)
    {
        //if(Entries.Length <= 0)
        //{
        //    Entries = new FixedPointEntry[1];
        //    var info = new FixedPointInfo[Entries[0].Infos.Length + 1];
        //    Debug.Log(info.Length);
        //}
        //else
        //{
        //    //Entries[0].Infos
        //    Entries[0].Infos = new[] { new FixedPointInfo { RawDegrees = 50, NM = 4 } };
        //}

        //Debug.Log("Entries :"+Entries.Length);

        //----------------------

        FixedPointEntry pointEntry = Entries.FirstOrDefault(x => x.Name == _name);

        if (pointEntry == null) 
        {
            FixedPointEntry[] newEntry = new FixedPointEntry[Entries.Length + 1];

            pointEntry = new FixedPointEntry
            {
                Name = _name,
                Infos = new FixedPointInfo[] { }
            };

            for (int i = 0; i < Entries.Length; i++)
            {
                newEntry[i] = Entries[i];
            }

            newEntry[newEntry.Length - 1] = pointEntry;

            Entries = newEntry;

            //Debug.Log(pointEntry.Name);
            //Debug.Log($"Added Enteries Info : {pointEntry.Name}");
        }
        else
        {
            FixedPointInfo[] newInfos = new FixedPointInfo[pointEntry.Infos.Length + 1];

            //FixedPointInfo addPointInfo = new FixedPointInfo()
            //{
            //    RawDegrees = 50,
            //    NM = newInfos.Length
            //};

            for (int i = 0; i < pointEntry.Infos.Length; i++)
            {
                newInfos[i] = pointEntry.Infos[i];
            }

            newInfos[newInfos.Length - 1] = addPointInfo;

            pointEntry.Infos = newInfos;

            Debug.Log($"Updated Enteries Info : {pointEntry.Name} --- {pointEntry.Infos[newInfos.Length - 1].NM}");
        }
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
