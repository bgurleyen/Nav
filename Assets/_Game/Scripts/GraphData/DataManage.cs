using Navigation;
using System;
using UnityEngine;

public class DataManage : MonoBehaviour
{
    public FirestoreController firestoreController;

    public FirestoreController Cloud =>
        FirestoreController.Instance != null ? FirestoreController.Instance : firestoreController;

    double _averageAltitude;
    double _lgAltitude;
    bool _lgCaptured;
    double _firstFlapAltitude;
    double _lastFlapAltitude;
    bool _firstFlapCaptured;
    int _prevFlapIdx;
    int _speedBrakeSeconds;
    double _secondAccumulator;

    public void Start()
    {
        ResetTracking();
    }

    void ResetTracking()
    {
        _averageAltitude = 0;
        _lgAltitude = 0;
        _lgCaptured = false;
        _firstFlapAltitude = 0;
        _lastFlapAltitude = 0;
        _firstFlapCaptured = false;
        _prevFlapIdx = Calculator.Flap_Idx;
        _speedBrakeSeconds = 0;
        _secondAccumulator = 0;
    }

    void Update()
    {
        if (Session.Settings == null)
            return;

        float step = Time.deltaTime * Session.Settings.SpeedMultiplier;
        if (float.IsNaN(step) || float.IsInfinity(step) || step < 0f)
            return;

        _secondAccumulator += step;
        if (double.IsNaN(_secondAccumulator) || double.IsInfinity(_secondAccumulator))
        {
            _secondAccumulator = 0;
            return;
        }

        const int maxTicksPerFrame = 120;
        int ticks = 0;
        while (_secondAccumulator >= 1.0 && ticks < maxTicksPerFrame)
        {
            _secondAccumulator -= 1.0;
            ticks++;
            Tick();
        }
        if (_secondAccumulator >= 1.0)
            _secondAccumulator = 0;
    }

    // Samples flight state once per (simulated) second.
    void Tick()
    {
        double altitude = Calculator.CAltitude;

        // 1) Running average altitude: seed with first sample, then average with each new sample.
        _averageAltitude = _averageAltitude == 0
            ? altitude
            : (_averageAltitude + altitude) / 2.0;

        // 2) First landing-gear extension altitude.
        if (!_lgCaptured && Calculator.LGDown)
        {
            _lgAltitude = altitude;
            _lgCaptured = true;
        }

        // Altitudes at the first and last flap setting changes.
        int flap = Calculator.Flap_Idx;
        if (flap > 0 && flap != _prevFlapIdx)
        {
            if (!_firstFlapCaptured)
            {
                _firstFlapAltitude = altitude;
                _firstFlapCaptured = true;
            }

            _lastFlapAltitude = altitude;
        }
        _prevFlapIdx = flap;

        // 3) Total speed-brake usage, counted in whole seconds.
        if (Calculator.SBUp)
            _speedBrakeSeconds++;
    }

    public LevelStat CaptureFlightResult()
    {
        // Calculator.totalFuel is centi-tons (tons * 100) → kg = totalFuel * 10.
        return new LevelStat
        {
            averageAltitude = (int)Math.Round(_averageAltitude),
            lgAltitude = (int)Math.Round(_lgAltitude),
            averageFlapAltitude = _firstFlapCaptured
                ? (int)Math.Round((_firstFlapAltitude + _lastFlapAltitude) / 2.0)
                : 0,
            speedBrakeSeconds = _speedBrakeSeconds,
            remainingFuel = (int)Math.Round(Calculator.totalFuel * 10.0),
        };
    }

    public void PushToCloud(LevelStat result, Action onSaved = null)
    {
        if (result == null || Cloud == null)
        {
            onSaved?.Invoke();
            return;
        }

        if (PlayerPrefsHolder.IsTestLevel)
        {
            Debug.Log("[DataManage] Skipping Firestore save (test level)");
            onSaved?.Invoke();
            return;
        }

        Debug.Log(
            $"[DataManage] Saving {PlayerPrefsHolder.FirestoreLevelKey}: " +
            $"fuel={result.remainingFuel} kg avgAlt={result.averageAltitude} " +
            $"lgAlt={result.lgAltitude} flapAlt={result.averageFlapAltitude} sb={result.speedBrakeSeconds}s");

        Cloud.SaveLevelStat(result, _ => onSaved?.Invoke());
    }
}
