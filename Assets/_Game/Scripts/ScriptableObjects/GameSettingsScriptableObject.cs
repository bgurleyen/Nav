using Navigation;
using UnityEngine;

[CreateAssetMenu]
public class GameSettingsScriptableObject : ScriptableObject
{
    public float StepDistanceDeltaTime => 
        Session.PlayerAircraft.AircraftSpeed *
        //Calculator.Acceleration() * 
        Session.Settings.FlyingTickDuration;
    
    public float StepDistanceTracer =>
        Session.PlayerAircraft.AircraftSpeed *
        //Calculator.Acceleration() * 
        Session.Settings.TracerTickDuration;
    
    public Color cMagenta;
    public Color cLightYellow;

    [Space]
    [Header("Map")]
    [Space] public float StartingZoom = 2;
    public float MapReferenceLength80 = 2.82f;
    public float PlanReferenceLength80 = 3.82f;
    
    [Header("Aircraft")]
    public float ForwardThreshold = 2f; // @$# this has to be in sync with the minimum turn radius 
    public float RejoinDistance = 2.8f;
    public float PilotSeekDistancePathFollow = 1.2f;
    public float PilotMaxDegreesPathFollow = 20;
    
    [Space]
    public float AirplaneInitialSpeed = 170;
    [Space]
    [Range(0, 0.001f)]
    public float FlyingTickDuration = 0.00017f;
    
    
    [Header("Tracer")]
    public float SegmentGranularity = 0.1f;
    [Range(0.0015f, 0.005f)]
    public float TracerTickDuration = 0.002f;

    #region Plane Speeds

    private const float FtToNm = 0.000164579f;

    [Header("===pending===")]
    public float MaxTurningSpeedPerNM = 1f;
    public float HGDAutoNextPointDistance = 1.3f;
    public float MaxTurningSpeedPerUnitLength => MaxTurningSpeedPerNM * 0.6f;
    

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