using UnityEngine;

/// <summary>
/// IAS değişimi: sabit sürat yolundan sapınca ve N1 fazlası varken.
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

    public const float ChartWeight_kg = 75000f;

    // İki CFM56-7B26. Deniz seviyesi statik itiş, motor başına yaklaşık 117 kN.
    const float MaxThrust_sl = 234000f;
    const float ThrustLapseExponent = 0.6f;

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

    static float SoundSpeed(float tempK) => Mathf.Sqrt(GammaAir * RSpecific * tempK);

    /// <summary>Subsonic pitot relation. Same impact pressure at sea level and at altitude.</summary>
    static float MachFromCas(float cas_kt, float pressure)
    {
        float cas = cas_kt * KIAS_TO_MS / SoundSl_ms;
        float impact = P0 * (Mathf.Pow(1f + 0.2f * cas * cas, 3.5f) - 1f);
        return Mathf.Sqrt(5f * (Mathf.Pow(impact / pressure + 1f, 2f / 7f) - 1f));
    }

    static readonly float[] AltFt = { 40000f, 35000f, 30000f, 25000f, 20000f, 15000f, 10000f, 5000f, 0f };
    static readonly float[] IdleN1Table = { 38.5f, 38.5f, 38.0f, 37.2f, 36.0f, 34.9f, 33.8f, 32.8f, 31.8f };
    // Net idle itişi. FL400'deki negatif değer, yüksek Mach'ta poların eksik kalan sürüklenmesini taşır.
    static readonly float[] IdleThrustN = { -5514f, 13464f, 15762f, 14450f, 13748f, 13633f, 13069f, 12644f, 13388f };
    // T − Tidle = K · (N1³ − idleN1³), N1 yüzde.
    static readonly float[] N1ThrustK = { 0.06393f, 0.05240f, 0.05102f, 0.06648f, 0.08652f, 0.10298f, 0.12398f, 0.14558f, 0.17432f };

    static float IdleN1(float alt_ft) => LerpBands(alt_ft, IdleN1Table);

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
