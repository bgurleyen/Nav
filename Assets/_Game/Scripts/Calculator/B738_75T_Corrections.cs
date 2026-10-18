public static class B738_75T_Corrections
{
    // QRH gear-down descent (PI-QRH.13.2, VREF40 + 70) time and distance to 1500 ft.
    public const double GearVsFpm = -1000;

    // QRH gear-down holding (75 t, 220 kt) minus the clean chart at the same point. Gear does not change pitch.
    static readonly double[] GearAltFt = { 1500, 5000, 10000, 15000, 20000 };
    static readonly double[] GearLevelN1 = { 12.7, 11.0, 11.5, 14.5, 14.7 };
    static readonly double[] GearLevelFfMul = { 1.44, 1.46, 1.49, 1.53, 1.56 };

    // Speedbrake FULL on the clean matrix. Gear is added separately by Gear().
    public static (double N1, double FF, double VS, double Pitch) Apply(
        double n1Clean, double ffClean, double vsClean, double pitchClean,
        bool speedbrakeFull)
    {
        if (!speedbrakeFull)
            return (n1Clean, ffClean, vsClean, pitchClean);

        return (n1Clean + 6.0, ffClean * 1.25, vsClean - 1000, pitchClean + 1.5);
    }

    /// <summary>Gear N1 add and FF factor. power 0 = idle (unchanged), 1 = level flight. Scales with speed from 220 kt.</summary>
    public static (double N1, double FFMul) Gear(double altitudeFt, double power, double speedKt)
    {
        double n1 = GearLevelN1[GearLevelN1.Length - 1];
        double mul = GearLevelFfMul[GearLevelFfMul.Length - 1];
        if (altitudeFt <= GearAltFt[0])
        {
            n1 = GearLevelN1[0];
            mul = GearLevelFfMul[0];
        }
        else
        {
            for (int i = 0; i < GearAltFt.Length - 1; i++)
            {
                if (altitudeFt > GearAltFt[i + 1])
                    continue;
                double t = (altitudeFt - GearAltFt[i]) / (GearAltFt[i + 1] - GearAltFt[i]);
                n1 = GearLevelN1[i] + (GearLevelN1[i + 1] - GearLevelN1[i]) * t;
                mul = GearLevelFfMul[i] + (GearLevelFfMul[i + 1] - GearLevelFfMul[i]) * t;
                break;
            }
        }

        if (power < 0.0) power = 0.0;
        if (power > 1.0) power = 1.0;
        double speedScale = speedKt / 220.0;
        if (speedScale < 0.5) speedScale = 0.5;
        if (speedScale > 1.3) speedScale = 1.3;
        double k = power * speedScale;
        return (n1 * k, 1.0 + (mul - 1.0) * k);
    }

    // Flap + SB için (F30/F40)
    public static (double N1, double FF, double VS, double Pitch) ApplyFlapSB(
        double n1Flap, double ffFlap, double vsFlap, double pitchFlap)
    {
        return (
            n1Flap + 3.0,
            ffFlap * 1.12,
            vsFlap - 300,
            pitchFlap + 1.0
        );
    }
}
