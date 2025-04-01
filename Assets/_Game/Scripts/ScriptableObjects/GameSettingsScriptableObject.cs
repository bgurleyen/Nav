using Gamelogic.Extensions;
using UnityEngine;

[CreateAssetMenu]
public class GameSettingsScriptableObject : ScriptableObject
{
    public float PlayerTickDistance => AircraftTickDistance(Calculator.GS);
    public float AircraftTickDistance(float gs) => gs / 60 / 60 * TickDuration(false);
    
    public float TracerTickDistance() => AirplaneDesignSpeed  / 60 / 60 * TickDuration(true);

    public float TickMaxRotation(bool isTracer) => _pilotMaxTurningDegreesPerNM  * TickDuration(isTracer);

    public float TickFlyingRotationDelayMultiplier =>  FlyingTickDuration / _tracerTickDuration;

    public float TickDuration(bool isTracer) => isTracer ? _tracerTickDuration : FlyingTickDuration;

    
    public Color cMagenta;
    public Color cLightYellow;

    [Space]
    [Header("Map")]
    [Space] public float StartingZoom = 2;
    public float MapReferenceLength80 = 2.82f; //2.56f -- TestValue
    public float PlanReferenceLength80 = 3.82f;
    
    [Header("Aircraft")]
    public float HDGProximityAdvanceDistance = 5f;
    public float HDGCloseRejoinDistance = 2.8f;
    [SerializeField] private float _pilotMaxTurningDegreesPerNM= 3;
    [Space]
    public float AirplaneDesignSpeed = 170;

    [Space] [Range(1, 25)] 
    [SerializeField] public int SpeedMultiplier = 1;
    
    [Header("DESIGN - Do Not Edit")]
    public float ForwardThreshold = 2f; // @$# this has to be in sync with the minimum turn radius 
    public float PilotSeekDistancePathFollow = 1.2f;
    public float SegmentGranularity = 0.1f;
    [SerializeField] private float _tracerTickDuration = 7f;
    
    [SerializeField, ReadOnly] public float FlyingTickDuration = 0.15f;
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