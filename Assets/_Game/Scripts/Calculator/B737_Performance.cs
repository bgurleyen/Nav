using UnityEngine;

/// <summary>
/// 75 t 737-800 enerji modeli. İniş hızı sabit süratteki yol açısıdır.
/// Düz uçuş ivmesi ayrı sorudur; ikisi aynı fazla kuvvetten birden alınmaz.
/// Gear ve speedbrake yalnızca Cd ile girer. N1 ve yakıt, 75 t idle tablosundan başlar.
/// Idle itiş, N1–itiş, yakıt ve flap Cd değerleri PerformanceChart'a en küçük kareler ile kalibredir.
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

    public const float ChartWeight_kg = 75000f;
    public float Weight_kg = ChartWeight_kg;
    public float WingArea_m2 = 125f;
    const float Cd0_clean = 0.020f;
    const float K_induced = 0.045f;

    // SL, 280 kt, sabit sürat: gear yaklaşık −800 ft/dk, full speedbrake yaklaşık −1000 ft/dk.
    const float Cd_speedbrake = 0.016f;
    const float Cd_gear = 0.013f;
    const float Cd_flap1 = 0.0043f;
    const float Cd_flap2 = 0.0055f;
    const float Cd_flap5 = 0.0064f;
    const float Cd_flap10 = 0.0082f;
    const float Cd_flap15 = 0.0115f;
    const float Cd_flap25 = 0.0216f;
    const float Cd_flap30 = 0.0268f;
    const float Cd_flap40 = 0.0565f;

    // Uçuşta flap 15 ve üstü approach idle seçer.
    const int ApproachIdleFlap = 15;

    // İki CFM56-7B26. Deniz seviyesi statik itiş, motor başına yaklaşık 117 kN.
    const float MaxThrust_sl = 234000f;
    const float ThrustLapseExponent = 0.6f;
    // Yakıt = idle + span · x^p, x = (N1 − idleN1) / (100 − idleN1).
    const float FfExponent = 1.45f;

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
        float idleN1 = IdleN1(alt_ft);
        float idleT = LerpBands(alt_ft, IdleThrustN);
        float idleFf = IdleFuelKgH(alt_ft);
        float n1K = LerpBands(alt_ft, N1ThrustK);
        if (ApproachIdle)
        {
            float apprN1 = Mathf.Max(idleN1, LerpBands(alt_ft, ApprIdleN1Table));
            idleT += n1K * (apprN1 * apprN1 * apprN1 - idleN1 * idleN1 * idleN1);
            idleN1 = apprN1;
            idleFf = Mathf.Max(idleFf, LerpBands(alt_ft, ApprIdleFfKgH));
        }
        float thrust = GetThrust(idleT, rho, mach);

        float drag = DragN(q, weightN, mach);
        float sinG = Mathf.Clamp((thrust - drag) / weightN, -0.3f, 0.3f);
        float cosG = Mathf.Sqrt(Mathf.Max(0f, 1f - sinG * sinG));
        drag = DragN(q, weightN * cosG, mach);
        sinG = Mathf.Clamp((thrust - drag) / weightN, -0.3f, 0.3f);

        vs_fpm = tas_ms * sinG / FT_TO_M * 60f;
        accel_ms2 = (thrust - DragN(q, weightN, mach)) / Weight_kg;

        float throttle = Mathf.Clamp01(Throttle);
        float n1Cube = idleN1 * idleN1 * idleN1 + Mathf.Max(0f, thrust - idleT) / n1K;
        float engineN1 = Mathf.Min(Mathf.Pow(n1Cube, 1f / 3f), 104f);
        n1_pct = Mathf.Clamp(engineN1 + GetN1Correction() * (1f - throttle), 0f, 104f);

        float x = Mathf.Max(0f, (engineN1 - idleN1) / (100f - idleN1));
        float ffFactor = Mathf.Lerp(GetFFCorrection(), 1f, throttle);
        ff_kgh = (idleFf + LerpBands(alt_ft, FfSpanKgH) * Mathf.Pow(x, FfExponent)) * ffFactor;
    }

    /// <summary>
    /// IAS change in kt/min when the path is vsGapFpm steeper (positive, accelerates) or shallower (negative)
    /// than the constant-IAS path for the same thrust. Energy balance: dV/dt = g · gap · f / TAS.
    /// f = 1 + (TAS/g)·dTAS/dh at constant IAS: on a constant-IAS descent part of the drag is paid by the falling TAS.
    /// </summary>
    public static float IasRateFromVsGap(float alt_ft, float ias_kt, float vsGapFpm)
    {
        float tas = TasMs(alt_ft, ias_kt);
        float dTasDh = (TasMs(alt_ft + 500f, ias_kt) - TasMs(alt_ft - 500f, ias_kt)) / (1000f * FT_TO_M);
        float energyShare = 1f + tas * dTasDh / G;
        float accelTas = G * vsGapFpm * FT_TO_M / 60f / tas * energyShare;
        return accelTas * ias_kt / tas * 60f;
    }

    /// <summary>
    /// IAS gain in kt/min with the engines at n1Cmd while n1Req holds the current path. N1 in percent.
    /// n1Cmd is capped at the N1 of maximum thrust.
    /// </summary>
    public static float IasRateFromN1(float alt_ft, float ias_kt, float n1Req, float n1Cmd)
    {
        Atmosphere(alt_ft * FT_TO_M, out float tempK, out float pressure, out float rho);
        float mach = MachFromCas(ias_kt, pressure);
        float tas = mach * SoundSpeed(tempK);
        float idleN1 = IdleN1(alt_ft);
        float idleT = LerpBands(alt_ft, IdleThrustN);
        float k = LerpBands(alt_ft, N1ThrustK);

        float sigma = Mathf.Clamp(rho / RhoSl, 0.12f, 1f);
        float speedFactor = Mathf.Clamp(1f - 0.55f * mach, 0.4f, 1f);
        float maxT = Mathf.Max(idleT, MaxThrust_sl * Mathf.Pow(sigma, ThrustLapseExponent) * speedFactor);
        float maxN1 = Mathf.Pow(idleN1 * idleN1 * idleN1 + (maxT - idleT) / k, 1f / 3f);

        float cmd = Mathf.Min(n1Cmd, maxN1);
        float req = Mathf.Max(n1Req, idleN1);
        float accelTas = k * (cmd * cmd * cmd - req * req * req) / ChartWeight_kg;
        return accelTas * ias_kt / tas * 60f;
    }

    static float TasMs(float alt_ft, float ias_kt)
    {
        Atmosphere(alt_ft * FT_TO_M, out float tempK, out float pressure, out _);
        return MachFromCas(ias_kt, pressure) * SoundSpeed(tempK);
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

    bool ApproachIdle => FlapSetting >= ApproachIdleFlap;

    static float SoundSpeed(float tempK) => Mathf.Sqrt(GammaAir * RSpecific * tempK);

    /// <summary>Subsonic pitot relation. Same impact pressure at sea level and at altitude.</summary>
    static float MachFromCas(float cas_kt, float pressure)
    {
        float cas = cas_kt * KIAS_TO_MS / SoundSl_ms;
        float impact = P0 * (Mathf.Pow(1f + 0.2f * cas * cas, 3.5f) - 1f);
        return Mathf.Sqrt(5f * (Mathf.Pow(impact / pressure + 1f, 2f / 7f) - 1f));
    }

    float GetThrust(float idleT, float rho, float mach)
    {
        float sigma = Mathf.Clamp(rho / RhoSl, 0.12f, 1f);
        float lapse = Mathf.Pow(sigma, ThrustLapseExponent);

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
            case 2: return Cd_flap2;
            case 5: return Cd_flap5;
            case 10: return Cd_flap10;
            case 15: return Cd_flap15;
            case 25: return Cd_flap25;
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
        return 0f;
    }

    float GetFFCorrection()
    {
        if (GearDown && SpeedbrakeOn) return 1.50f;
        if (GearDown || SpeedbrakeOn) return 1.25f;
        return 1f;
    }

    static readonly float[] AltFt = { 40000f, 35000f, 30000f, 25000f, 20000f, 15000f, 10000f, 5000f, 0f };
    static readonly float[] IdleN1Table = { 38.5f, 38.5f, 38.0f, 37.2f, 36.0f, 34.9f, 33.8f, 32.8f, 31.8f };
    static readonly float[] IdleFfKgH = { 218f, 236f, 259f, 281f, 295f, 313f, 336f, 354f, 376f };
    // Net idle itişi. FL400'deki negatif değer, yüksek Mach'ta poların eksik kalan sürüklenmesini taşır.
    static readonly float[] IdleThrustN = { -5514f, 13464f, 15762f, 14450f, 13748f, 13633f, 13069f, 12644f, 13388f };
    // T − Tidle = K · (N1³ − idleN1³), N1 yüzde.
    static readonly float[] N1ThrustK = { 0.06393f, 0.05240f, 0.05102f, 0.06648f, 0.08652f, 0.10298f, 0.12398f, 0.14558f, 0.17432f };
    static readonly float[] FfSpanKgH = { 996f, 1325f, 1479f, 1709f, 2015f, 2269f, 2505f, 2821f, 3281f };
    // FL200 üstünde flap verisi yok; FL200 değeri taşınır.
    static readonly float[] ApprIdleN1Table = { 38.5f, 38.5f, 38.0f, 37.8f, 37.8f, 36.8f, 35.5f, 34.5f, 33.5f };
    static readonly float[] ApprIdleFfKgH = { 381f, 381f, 381f, 381f, 381f, 403f, 429f, 435f, 447f };

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
