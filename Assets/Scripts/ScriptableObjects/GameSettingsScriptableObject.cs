using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu]
public class GameSettingsScriptableObject : ScriptableObject
{
    public Color cMagenta;
    public Color cLightYellow;

    [Space] public float StartingZoom = 2;
    [Header("Aircraft navigation")]
    // frequency of points
    public float DrawerUnitLength = 0.6f;
    public float MaxTurningSpeedPerNM = 1f;
    public float MaxTurningSpeedPerUnitLength => MaxTurningSpeedPerNM * DrawerUnitLength;

    [Space]
    [SerializeField] float deltaTime = 0.0057f;
    // const float DeltaTime = 0.0000003f;
    public float DeltaTime => deltaTime /1000f;

    [Header("Route distances(NM)")]
    public float ForwardThreshold = 2f; // @$# this has to be in sync with the minimum turn radius 
    public float RejoinDistance = 2.8f;
    public float HGDAutoNextPointDistance = 1.3f;
}
// 9:45