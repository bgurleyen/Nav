using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu]
public class GameSettingsScriptableObject : ScriptableObject
{
    public Color cMagenta;
    public Color cLightYellow;

    [Space] public float StartingZoom = 2;
    [Space] public float MinZoom = 1;
    [Space] public float MAxZoom = 3;
    [Header("Aircraft navigation")]
    // frequency of points
    public float DrawerUnitLength = 0.6f;
    public float MaxTurningSpeedPerNM = 1f;
    public float MaxTurningSpeedPerUnitLength => MaxTurningSpeedPerNM * DrawerUnitLength;

    [Space]
    [SerializeField]
    private float deltaTime = 0.0057f;
    // const float DeltaTime = 0.0000003f;
    public float DeltaTime => deltaTime /1000f;

    [Header("Route distances(NM)")]
    public float ForwardThreshold = 2f; // @$# this has to be in sync with the minimum turn radius 
    public float RejoinDistance = 2.8f;
    public float HGDAutoNextPointDistance = 1.3f;

    #region Plane Speeds

    private const float FtToNm = 0.000164579f;

    // turn radius
    private const float IAS = 240;
    private const float Altitude = 1000;
    private const float Headwind = 5;
    private static float TAS => IAS + Altitude / 1000 * 0.02f * IAS;
    private static float GS => TAS - Headwind;
    private static float Bank => Mathf.Deg2Rad * Mathf.Min(30, TAS * 0.15f);
    public static float GetMinRadius => Mathf.Pow(GS, 2) / (11.29f * Mathf.Tan(Bank)) * FtToNm;

    public static float GetRelaxedRadius = GetMinRadius * 3;
    
    #endregion
}