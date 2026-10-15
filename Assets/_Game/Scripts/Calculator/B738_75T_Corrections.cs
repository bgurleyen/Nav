public static class B738_75T_Corrections
{
    // Base clean matrisinden düzeltme uygula
    public static (double N1, double FF, double VS, double Pitch) Apply(
        double n1Clean, double ffClean, double vsClean, double pitchClean,
        bool gearDown, bool speedbrakeFull)
    {
        double n1 = n1Clean;
        double ff = ffClean;
        double vs = vsClean;
        double pitch = pitchClean;

        if (gearDown && !speedbrakeFull) // Sadece Gear
        {
            n1 += 8.0;
            ff *= 1.25;
            vs -= 800;
            pitch += 2.0;
        }
        else if (!gearDown && speedbrakeFull) // Sadece SB FULL
        {
            n1 += 6.0;
            ff *= 1.25;
            vs -= 1000;
            pitch += 1.5;
        }
        else if (gearDown && speedbrakeFull) // Gear + SB FULL
        {
            n1 += 14.0;
            ff *= 1.50;
            vs -= 1800;
            pitch += 3.5;
        }

        return (n1, ff, vs, pitch);
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
