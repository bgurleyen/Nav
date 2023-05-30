using Navigation;
using UnityEngine;

[CreateAssetMenu]
public class GameSettingsScriptableObject : ScriptableObject
{
    public float TickStepDistance(bool isTracer) => Session.PlayerAircraft.Speed  / 60 / 60 * TickDuration(isTracer);

    public float TickMaxRotation(bool isTracer) => _pilotMaxTurningDegreesPerNM  * TickDuration(isTracer);

    public float TickFlyingRotationDelayMultiplier =>  _flyingTickDuration / _tracerTickDuration;

    private float TickDuration(bool isTracer) => isTracer ? _tracerTickDuration : _flyingTickDuration;

    
    public Color cMagenta;
    public Color cLightYellow;

    [Space]
    [Header("Map")]
    [Space] public float StartingZoom = 2;
    public float MapReferenceLength80 = 2.82f;
    public float PlanReferenceLength80 = 3.82f;
    
    [Header("Aircraft")]
    public float HDGProximityAdvanceDistance = 5f;
    public float HDGCloseRejoinDistance = 2.8f;
    [SerializeField] private float _pilotMaxTurningDegreesPerNM= 3;
    [Space]
    public float AirplaneInitialSpeed = 170;
    [Space]
    [Range(0.04f, 8f)]
    [SerializeField] private float _flyingTickDuration = 0.04f;
    
    
    [Header("DESIGN - Do Not Edit")]
    public float ForwardThreshold = 2f; // @$# this has to be in sync with the minimum turn radius 
    public float PilotSeekDistancePathFollow = 1.2f;
    public float SegmentGranularity = 0.1f;
    [SerializeField] private float _tracerTickDuration = 7f;

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