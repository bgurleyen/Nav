using UnityEngine;

/// <summary>
/// 75 t 737-800 enerji modeli. İniş hızı sabit süratteki yol açısıdır.
/// Düz uçuş ivmesi ayrı sorudur; ikisi aynı fazla kuvvetten birden alınmaz.
/// Gear ve speedbrake yalnızca Cd ile girer. N1 ve yakıt, 75 t idle tablosundan başlar.
/// </summary>
public class B737_Performance
{
    const float G = 9.81f;
    const float KIAS_TO_MS = 0.514444f;
    const float FT_TO_M = 0.3048f;
    const float RhoSl = 1.225f;
    const float GammaAir = 1.4f;
    const float RSpecific = 287.05f;
    const float Tropopause_m = 11000f;
    const float P0 = 101325f;
    const float SoundSl_ms = 340.294f;
    // Lock: ΔCd = 20 (M − Mcrit)^4. Mdd 0.80 → Mcrit ≈ 0.69.
    const float MachCrit = 0.692f;

    public float Weight_kg = 75000f;
    public float WingArea_m2 = 125f;
    const float Cd0_clean = 0.020f;
    const float K_induced = 0.045f;

    // SL, 280 kt, sabit sürat: gear yaklaşık −800 ft/dk, full speedbrake yaklaşık −1000 ft/dk.
    const float Cd_speedbrake = 0.016f;
    const float Cd_gear = 0.013f;
    const float Cd_flap1 = 0.008f;
    const float Cd_flap5 = 0.020f;
    const float Cd_flap15 = 0.035f;
    const float Cd_flap30 = 0.055f;
    const float Cd_flap40 = 0.070f;

    // İki CFM56-7B26. Deniz seviyesi statik itiş, motor başına yaklaşık 117 kN.
    const float MaxThrust_sl = 234000f;
    // Temiz 280 kt idle, deniz seviyesinde yaklaşık 3°.
    const float IdleThrustFactor = 0.0368f;
    const float ThrustLapseExponent = 0.6f;

    public bool SpeedbrakeDeployed = false;
    public bool SpeedbrakeFull = false;
    public int FlapSetting = 0;
    public bool GearDown = false;
    public float Throttle = 0f;

    /// <summary>
    /// vs_fpm sabit IAS yoludur. accel_ms2 aynı anda değil, düz uçuşta kalınırsa oluşan ivmedir.
    /// </summary>
    public void CalculatePerformance(float alt_ft, float ias_kt, out float vs_fpm, out float accel_ms2, out float n1_pct, out float ff_kgh)
    {
        Atmosphere(alt_ft * FT_TO_M, out float tempK, out float pressure, out float rho);
        float mach = MachFromCas(ias_kt, pressure);
        float tas_ms = mach * SoundSpeed(tempK);
        float q = 0.5f * rho * tas_ms * tas_ms;
        float weightN = Weight_kg * G;
        float thrust = GetThrust(rho, mach);

        float drag = DragN(q, weightN, mach);
        float sinG = Mathf.Clamp((thrust - drag) / weightN, -0.3f, 0.3f);
        float cosG = Mathf.Sqrt(Mathf.Max(0f, 1f - sinG * sinG));
        drag = DragN(q, weightN * cosG, mach);
        sinG = Mathf.Clamp((thrust - drag) / weightN, -0.3f, 0.3f);

        vs_fpm = tas_ms * sinG / FT_TO_M * 60f;
        accel_ms2 = (thrust - DragN(q, weightN, mach)) / Weight_kg;

        float idleN1 = IdleN1(alt_ft);
        float maxN1 = Mathf.Lerp(100f, 94f, Mathf.Clamp01(alt_ft / 40000f));
        float throttle = Mathf.Clamp01(Throttle);
        n1_pct = Mathf.Lerp(idleN1, maxN1, throttle) + GetN1Correction() * (1f - throttle);
        n1_pct = Mathf.Clamp(n1_pct, 0f, 104f);

        float idleFf = IdleFuelKgH(alt_ft);
        float takeoffFf = Mathf.Lerp(3400f, 1500f, Mathf.Clamp01(alt_ft / 40000f));
        float u = Mathf.InverseLerp(idleN1, maxN1, Mathf.Lerp(idleN1, maxN1, throttle));
        float ffFactor = Mathf.Lerp(GetFFCorrection(), 1f, throttle);
        ff_kgh = Mathf.Lerp(idleFf, takeoffFf, u * u) * ffFactor;
    }

    public float GetVarioFor3Deg(float alt_ft, float ias_kt)
    {
        Atmosphere(alt_ft * FT_TO_M, out float tempK, out float pressure, out _);
        float tas_ms = MachFromCas(ias_kt, pressure) * SoundSpeed(tempK);
        return tas_ms * Mathf.Sin(3f * Mathf.Deg2Rad) / FT_TO_M * 60f;
    }

    /// <summary>Mach for this indicated (calibrated) airspeed and altitude.</summary>
    public float GetMach(float alt_ft, float ias_kt)
    {
        Atmosphere(alt_ft * FT_TO_M, out _, out float pressure, out _);
        return MachFromCas(ias_kt, pressure);
    }

    bool SpeedbrakeOn => SpeedbrakeFull || SpeedbrakeDeployed;

    static float SoundSpeed(float tempK) => Mathf.Sqrt(GammaAir * RSpecific * tempK);

    /// <summary>Subsonic pitot relation. Same impact pressure at sea level and at altitude.</summary>
    static float MachFromCas(float cas_kt, float pressure)
    {
        float cas = cas_kt * KIAS_TO_MS / SoundSl_ms;
        float impact = P0 * (Mathf.Pow(1f + 0.2f * cas * cas, 3.5f) - 1f);
        return Mathf.Sqrt(5f * (Mathf.Pow(impact / pressure + 1f, 2f / 7f) - 1f));
    }

    float GetThrust(float rho, float mach)
    {
        float sigma = Mathf.Clamp(rho / RhoSl, 0.12f, 1f);
        float lapse = Mathf.Pow(sigma, ThrustLapseExponent);
        float idleT = MaxThrust_sl * IdleThrustFactor * lapse;

        float speedFactor = Mathf.Clamp(1f - 0.55f * mach, 0.4f, 1f);
        float maxT = Mathf.Max(idleT, MaxThrust_sl * lapse * speedFactor);
        return Mathf.Lerp(idleT, maxT, Mathf.Clamp01(Throttle));
    }

    float DragN(float q, float liftN, float mach)
    {
        float cl = liftN / Mathf.Max(1f, q * WingArea_m2);
        float cd = Cd0_clean + FlapCd() + GearCd() + SpeedbrakeCd() + WaveCd(mach) + K_induced * cl * cl;
        return q * WingArea_m2 * cd;
    }

    static float WaveCd(float mach)
    {
        float over = mach - MachCrit;
        if (over <= 0f)
            return 0f;
        float over2 = over * over;
        return 20f * over2 * over2;
    }

    float FlapCd()
    {
        switch (FlapSetting)
        {
            case 1: return Cd_flap1;
            case 5: return Cd_flap5;
            case 10: return Cd_flap5 + 0.005f;
            case 15: return Cd_flap15;
            case 30: return Cd_flap30;
            case 40: return Cd_flap40;
            default: return 0f;
        }
    }

    float GearCd() => GearDown ? Cd_gear : 0f;

    float SpeedbrakeCd() => SpeedbrakeOn ? Cd_speedbrake : 0f;

    float GetN1Correction()
    {
        if (GearDown && SpeedbrakeOn) return 14f;
        if (GearDown) return 8f;
        if (SpeedbrakeOn) return 6f;
        if (FlapSetting >= 30) return 3f;
        return 0f;
    }

    float GetFFCorrection()
    {
        if (GearDown && SpeedbrakeOn) return 1.50f;
        if (GearDown || SpeedbrakeOn) return 1.25f;
        if (FlapSetting >= 30) return 1.12f;
        return 1f;
    }

    static readonly float[] AltFt = { 40000f, 35000f, 30000f, 25000f, 20000f, 15000f, 10000f, 5000f, 0f };
    static readonly float[] IdleN1Table = { 38.5f, 38.5f, 37.5f, 36.5f, 35.5f, 34.5f, 33.5f, 32.5f, 31.5f };
    static readonly float[] IdleFfKgH = { 218f, 236f, 263f, 290f, 327f, 381f, 436f, 508f, 599f };

    static float IdleN1(float alt_ft) => LerpBands(alt_ft, IdleN1Table);

    static float IdleFuelKgH(float alt_ft) => LerpBands(alt_ft, IdleFfKgH);

    static float LerpBands(float alt_ft, float[] values)
    {
        alt_ft = Mathf.Clamp(alt_ft, 0f, 40000f);
        for (int i = 0; i < AltFt.Length - 1; i++)
        {
            if (alt_ft <= AltFt[i] && alt_ft >= AltFt[i + 1])
            {
                float t = (AltFt[i] - alt_ft) / (AltFt[i] - AltFt[i + 1]);
                return Mathf.Lerp(values[i], values[i + 1], t);
            }
        }

        return values[values.Length - 1];
    }

    static void Atmosphere(float alt_m, out float tempK, out float pressure, out float rho)
    {
        const float T0 = 288.15f;
        const float Lapse = 0.0065f;
        if (alt_m < Tropopause_m)
        {
            tempK = T0 - Lapse * alt_m;
            pressure = P0 * Mathf.Pow(tempK / T0, G / (RSpecific * Lapse));
        }
        else
        {
            tempK = 216.65f;
            const float pTrop = 22632.1f;
            pressure = pTrop * Mathf.Exp(-G * (alt_m - Tropopause_m) / (RSpecific * tempK));
        }

        rho = pressure / (RSpecific * tempK);
    }
}
