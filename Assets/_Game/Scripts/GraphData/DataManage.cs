using Navigation;
using System;
using System.Collections.Generic;
using UnityEngine;

public class DataManage : MonoBehaviour
{
    public const int DefaultProgressLevelCount = 40;

    public FirestoreController firestoreController;

    [SerializeField] private DDL_data m_data = new DDL_data();

    private const double DistanceRecordInterval = 0.1;
    private double accumulatedDistance;
    private double totalDistanceTraveled;
    private bool _flightFinalized;

    public void Start()
    {
        _flightFinalized = false;
        m_data.time.Add(DateTime.Now.ToString());
        RecordSample(0);
    }

    public DDL_data CaptureFlightSnapshot()
    {
        EnsureFlightFinalized();
        return CloneFlightData(m_data);
    }

    public static DDL_data CloneFlightData(DDL_data source)
    {
        if (source == null)
            return null;

        return JsonUtility.FromJson<DDL_data>(JsonUtility.ToJson(source));
    }

    public void PushFlightToCloud(DDL_data flightData)
    {
        if (flightData == null || firestoreController == null)
            return;

        L_data cloudData = ToLData(flightData);
        firestoreController.UpdateStats(cloudData, _ => { });
        firestoreController.UpdateLevelProgressStats(flightData.remainingFuel, _ => { });
    }

    void EnsureFlightFinalized()
    {
        if (_flightFinalized)
            return;

        FlushPendingDistanceSample();
        m_data.remainingFuel = Math.Round(Calculator.totalFuel / 100, 2);
        m_data.time.Add(DateTime.Now.ToString());
        _flightFinalized = true;
    }

    void FlushPendingDistanceSample()
    {
        if (Session.Settings == null || accumulatedDistance <= 0)
            return;

        totalDistanceTraveled += accumulatedDistance;
        accumulatedDistance = 0;
        RecordSample(totalDistanceTraveled);
    }

    public static bool HasValidDistanceData(DDL_data data)
    {
        return data?.distance != null
            && data.distance.Count > 0
            && data.altitude != null
            && data.distance.Count == data.altitude.Count;
    }

    public static bool HasValidDistanceData(L_data data)
    {
        return data?.distance != null
            && data.distance.Count > 0
            && data.altitude != null
            && data.distance.Count == data.altitude.Count;
    }

    public static bool HasAltitudeProfile(L_data data)
    {
        return data?.altitude != null && data.altitude.Count > 0;
    }

    public static void TryNormalizeProfile(L_data data)
    {
        if (!HasAltitudeProfile(data))
            return;

        int count = data.altitude.Count;

        if (data.distance == null || data.distance.Count != count)
        {
            data.distance = new List<double>(count);
            for (int i = 0; i < count; i++)
                data.distance.Add(Math.Round(i * DebriefMetrics.DistanceSampleNm, 1));
        }

        data.speed = EnsureListLength(data.speed, count);
        data.flap = EnsureListLength(data.flap, count);
        data.speedBrake = EnsureListLength(data.speedBrake, count);
        data.landingGear = EnsureListLength(data.landingGear, count);
        data.verticalMode = EnsureListLength(data.verticalMode, count);
        data.fuelFlow = EnsureListLength(data.fuelFlow, count);
    }

    static List<double> EnsureListLength(List<double> values, int count)
    {
        if (values == null)
            values = new List<double>();

        while (values.Count < count)
            values.Add(0);

        return values;
    }

    public static L_data ToLData(DDL_data data)
    {
        if (data?.altitude == null)
            return null;

        return new L_data
        {
            altitude = data.altitude,
            speed = data.speed,
            flap = data.flap,
            speedBrake = data.speedBrake,
            landingGear = data.landingGear,
            verticalMode = data.verticalMode,
            fuelFlow = data.fuelFlow,
            distance = data.distance,
            remainingFuel = data.remainingFuel,
            time = data.time,
        };
    }

    public static L_data NormalizeProfile(L_data data)
    {
        if (data == null)
            return null;

        TryNormalizeProfile(data);
        return HasValidDistanceData(data) ? data : null;
    }

    public static double DistanceIncrement(double deltaTimeSeconds, double speedKnots, float speedMultiplier)
    {
        return deltaTimeSeconds * speedKnots * speedMultiplier / 3600.0;
    }

    public double ConvertToLowerHundred(double value) => Math.Floor(value / 100.0) * 100;

    public double RoundDownToNearestTen(double value) => Math.Floor(value / 10) * 10;

    void RecordSample(double cumulativeDistance)
    {
        m_data.distance.Add(Math.Round(cumulativeDistance, 1));
        m_data.speed.Add(RoundDownToNearestTen(Calculator.CSpeed));
        m_data.altitude.Add(ConvertToLowerHundred(Calculator.CAltitude));
        m_data.flap.Add(Calculator.Flap_Idx);
        m_data.speedBrake.Add(Convert.ToInt32(Calculator.SBUp));
        m_data.landingGear.Add(Convert.ToInt32(Calculator.LGDown));
        m_data.fuelFlow.Add((Calculator.dispFF + (Calculator.FF - Calculator.FF / 10 * 10)) / 100);

        if (Session.State == null)
            return;

        if (Session.State.LC) m_data.verticalMode.Add(1);
        else if (Session.State.AH) m_data.verticalMode.Add(2);
        else if (Session.State.VS) m_data.verticalMode.Add(3);
        else if (Session.State.VNAV) m_data.verticalMode.Add(4);
        else if (Session.State.GSCaptured) m_data.verticalMode.Add(5);
        else m_data.verticalMode.Add(0);
    }

    void Update()
    {
        if (Session.Settings == null)
            return;

        accumulatedDistance += DistanceIncrement(
            Time.deltaTime,
            Calculator.CSpeed,
            Session.Settings.SpeedMultiplier);

        while (accumulatedDistance >= DistanceRecordInterval)
        {
            accumulatedDistance -= DistanceRecordInterval;
            totalDistanceTraveled += DistanceRecordInterval;
            RecordSample(totalDistanceTraveled);
        }
    }
}
