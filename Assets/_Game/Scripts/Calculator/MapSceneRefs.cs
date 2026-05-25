using System.Collections.Generic;
using UnityEngine;

public class MapSceneRefs
{
    static MapSceneRefs _instance;

    public static MapSceneRefs Instance => _instance ??= new MapSceneRefs();

    readonly Dictionary<int, GameObject> _routePoints = new Dictionary<int, GameObject>();
    readonly Dictionary<int, GameObject> _virtualPoints = new Dictionary<int, GameObject>();
    GameObject _playerAircraft;

    public GameObject GetRoutePoint(int index)
    {
        if (!_routePoints.TryGetValue(index, out var point))
        {
            point = GameObject.Find("pt (" + index + ")");
            if (point != null)
                _routePoints[index] = point;
        }

        return point;
    }

    public GameObject GetVirtualPoint(int index)
    {
        if (!_virtualPoints.TryGetValue(index, out var point))
        {
            point = GameObject.Find("pt (" + (index + 50) + ")");
            if (point != null)
                _virtualPoints[index] = point;
        }

        return point;
    }

    public GameObject PlayerAircraft
    {
        get
        {
            if (_playerAircraft == null)
                _playerAircraft = GameObject.Find("AC (0)");
            return _playerAircraft;
        }
    }
}
