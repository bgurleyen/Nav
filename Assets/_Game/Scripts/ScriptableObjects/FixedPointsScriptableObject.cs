using Navigation;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEngine;

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

    /*public void AddOrUpdateFixedPointEntry(string _name, FixedPointInfo addPointInfo)
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

    public void AddFixedPoint(FixedPointEntry fixedPoint)
    {
        if (fixedPoint != null)
        {
            FixedPointEntry[] newEntry = new FixedPointEntry[Entries.Length + 1];

            for (int i = 0; i < Entries.Length; i++)
            {
                newEntry[i] = Entries[i];
            }

            newEntry[newEntry.Length - 1] = fixedPoint;

            Entries = newEntry;

            //Debug.Log(pointEntry.Name);
            //Debug.Log($"Added Enteries Info : {pointEntry.Name}");
        }
        else
        {
            Debug.LogWarning("Input FixedPointEntry");
        }
    }*/

    public void AddOrUpdateFixedPoint(FixedPointEntry fixedPoint, int atIndex = -1)
    {
        FixedPointEntry pointEntry = atIndex == -1 ? Entries.FirstOrDefault(x => x.Name == fixedPoint.Name) : fixedPoint;

        if (pointEntry == null)
        {
            FixedPointEntry[] newEntry = new FixedPointEntry[Entries.Length + 1];

            for (int i = 0; i < Entries.Length; i++)
            {
                newEntry[i] = Entries[i];
            }

            newEntry[newEntry.Length - 1] = fixedPoint;

            Entries = newEntry;

            Debug.Log(fixedPoint.Name);
        }
        else
        {
            if (Entries.Length > atIndex)
            {
                Entries[atIndex] = fixedPoint;
                //Debug.Log($"Updated {atIndex}: {Entries[atIndex].Name}");
            }
            else
            {
                //Debug.LogError("Index_Outof_Bound_Entry");

                //comment below code if not use it. add because of if getting elament null at index
                FixedPointEntry[] newEntry = new FixedPointEntry[Entries.Length + 1];

                for (int i = 0; i < Entries.Length; i++)
                {
                    newEntry[i] = Entries[i];
                }

                newEntry[newEntry.Length - 1] = fixedPoint;

                Entries = newEntry;

                Debug.Log(fixedPoint.Name);
            }
        }
    }

    public void AddOrUpdateFixedPointInfo(FixedPointInfo pointInfo, int atIndex = -1, int entryIndx = 0)
    {
        if (atIndex == -1)
        {
            FixedPointInfo[] newPointsInfo = new FixedPointInfo[Entries[entryIndx].Infos.Length + 1];

            for (int i = 0; i < Entries[entryIndx].Infos.Length; i++)
            {
                newPointsInfo[i] = Entries[entryIndx].Infos[i];
            }

            newPointsInfo[newPointsInfo.Length - 1] = pointInfo;

            Entries[entryIndx].Infos = newPointsInfo;
        }
        else
        {
            FixedPointInfo[] newPointsInfo = new FixedPointInfo[Entries[entryIndx].Infos.Length];

            if (Entries[entryIndx].Infos.Length > atIndex)
            {

                for (int i = 0; i < Entries[entryIndx].Infos.Length; i++)
                {
                    newPointsInfo[i] = Entries[entryIndx].Infos[i];
                }

                newPointsInfo[atIndex] = pointInfo;

                Entries[entryIndx].Infos = newPointsInfo;
            }
            else
            {
                //Debug.LogError("Index_Outof_Bound_Info");

                newPointsInfo = new FixedPointInfo[Entries[entryIndx].Infos.Length + 1];

                for (int i = 0; i < Entries[entryIndx].Infos.Length; i++)
                {
                    newPointsInfo[i] = Entries[entryIndx].Infos[i];
                }

                newPointsInfo[newPointsInfo.Length - 1] = pointInfo;

                Entries[entryIndx].Infos = newPointsInfo;
            }
        }
    }

    public void RemovePoint(int atIndex)
    {
        if (atIndex < Entries.Length)
            Entries[atIndex] = null;
    }

    public void RemovePointInfo(int atIndex, int entryIndx)
    {
        if (entryIndx < Entries.Length)
        {
            if (atIndex < Entries[entryIndx]?.Infos.Length)
                Entries[entryIndx].Infos[atIndex] = null;
        }
    }

    public FixedPointEntry GetWaypointAt(int point)
    {
        if (Entries.Length > point)
        {
            return Entries[point];
        }
        else
        {
            return null;
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
