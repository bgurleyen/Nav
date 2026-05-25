using System;
using System.Collections.Generic;
using UnityEngine;

public static class DebriefMetrics
{
    public const double DistanceSampleNm = 0.1;

    public struct ColumnValues
    {
        public string Me;
        public string Average;
        public string Best;
    }

    public static void LogPanel(
        DDL_data me,
        L_data average,
        L_data best,
        int? rank,
        int totalPlayers)
    {
        if (me == null)
            return;

        ColumnValues avgAlt = BuildAverageAltitude(me, best, average);
        ColumnValues lgAlt = BuildLgAltitude(me, best, average);
        ColumnValues flapsAlt = BuildAverageFlapsAltitude(me, best, average);
        ColumnValues sbUsage = BuildSpeedBrakeUsage(me, best, average);

        Debug.Log(
            $"[Debrief] Average Altitude ME={avgAlt.Me} AVG={avgAlt.Average} BEST={avgAlt.Best}\n" +
            $"[Debrief] LG Altitude ME={lgAlt.Me} AVG={lgAlt.Average} BEST={lgAlt.Best}\n" +
            $"[Debrief] Avg Flaps Altitude ME={flapsAlt.Me} AVG={flapsAlt.Average} BEST={flapsAlt.Best}\n" +
            $"[Debrief] Total S/B Usage ME={sbUsage.Me} AVG={sbUsage.Average} BEST={sbUsage.Best}\n" +
            $"[Debrief] Remaining Fuel={FormatFuel(me.remainingFuel)} Rank={FormatRank(rank, totalPlayers)}");
    }

    public static ColumnValues BuildAverageAltitude(DDL_data me, L_data best, L_data average)
    {
        return new ColumnValues
        {
            Me = FormatAltitude(Average(me?.altitude)),
            Average = FormatAltitude(Average(average?.altitude)),
            Best = FormatAltitude(Average(best?.altitude)),
        };
    }

    public static ColumnValues BuildLgAltitude(DDL_data me, L_data best, L_data average)
    {
        return new ColumnValues
        {
            Me = FormatAltitude(TransitionAltitude(me?.altitude, me?.landingGear)),
            Average = FormatAltitude(TransitionAltitude(average?.altitude, average?.landingGear)),
            Best = FormatAltitude(TransitionAltitude(best?.altitude, best?.landingGear)),
        };
    }

    public static ColumnValues BuildAverageFlapsAltitude(DDL_data me, L_data best, L_data average)
    {
        return new ColumnValues
        {
            Me = FormatAltitude(AverageFlapTransitionAltitude(me?.altitude, me?.flap)),
            Average = FormatAltitude(AverageFlapTransitionAltitude(average?.altitude, average?.flap)),
            Best = FormatAltitude(AverageFlapTransitionAltitude(best?.altitude, best?.flap)),
        };
    }

    public static ColumnValues BuildSpeedBrakeUsage(DDL_data me, L_data best, L_data average)
    {
        return new ColumnValues
        {
            Me = FormatDuration(TotalSpeedBrakeSeconds(me?.speedBrake, me?.speed)),
            Average = FormatDuration(TotalSpeedBrakeSeconds(average?.speedBrake, average?.speed)),
            Best = FormatDuration(TotalSpeedBrakeSeconds(best?.speedBrake, best?.speed)),
        };
    }

    public static L_data BuildAverageProfile(DDL_data me, List<S_data> averageRawData)
    {
        var averageData = new L_data
        {
            altitude = new List<double>(),
            speed = new List<double>(),
            flap = new List<double>(),
            speedBrake = new List<double>(),
            landingGear = new List<double>(),
            distance = new List<double>(),
        };

        if (averageRawData == null)
            return averageData;

        for (int i = 0; i < averageRawData.Count; i++)
        {
            S_data point = averageRawData[i];
            averageData.altitude.Add(point.altitude);
            averageData.speed.Add(Math.Floor(point.speed / 10.0) * 10.0);
            averageData.landingGear.Add(point.landingGear);
            averageData.flap.Add(point.flap);
            averageData.speedBrake.Add(point.speedBrake);
            averageData.distance.Add(
                point.distance > 0 || i == 0
                    ? point.distance
                    : me?.distance != null && i < me.distance.Count
                        ? me.distance[i]
                        : Math.Round(i * DistanceSampleNm, 1));
        }

        return averageData;
    }

    public static string FormatFuel(double tons) => $"{tons:F2} T";

    public static string FormatRank(int? rank, int totalPlayers)
    {
        if (rank.HasValue)
            return $"#{rank.Value}";

        return "—";
    }

    static double? Average(IList<double> values)
    {
        if (values == null || values.Count == 0)
            return null;

        double sum = 0;
        for (int i = 0; i < values.Count; i++)
            sum += values[i];

        return sum / values.Count;
    }

    static double? TransitionAltitude(IList<double> altitude, IList<double> signal)
    {
        if (altitude == null || signal == null || altitude.Count == 0 || signal.Count != altitude.Count)
            return null;

        for (int i = 1; i < signal.Count; i++)
        {
            if (signal[i] > 0.5 && signal[i - 1] <= 0.5)
                return altitude[i];
        }

        return null;
    }

    static double? AverageFlapTransitionAltitude(IList<double> altitude, IList<double> flap)
    {
        if (altitude == null || flap == null || altitude.Count == 0 || flap.Count != altitude.Count)
            return null;

        double sum = 0;
        int count = 0;

        for (int i = 1; i < flap.Count; i++)
        {
            if (Math.Abs(flap[i] - flap[i - 1]) < 0.5 || flap[i] <= 0.5)
                continue;

            sum += altitude[i];
            count++;
        }

        if (count == 0)
            return null;

        return sum / count;
    }

    static double TotalSpeedBrakeSeconds(IList<double> speedBrake, IList<double> speed)
    {
        if (speedBrake == null || speed == null || speedBrake.Count == 0 || speed.Count != speedBrake.Count)
            return 0;

        double totalSeconds = 0;
        for (int i = 0; i < speedBrake.Count; i++)
        {
            if (speedBrake[i] <= 0.5)
                continue;

            double speedKnots = Math.Max(1, speed[i]);
            totalSeconds += DistanceSampleNm / speedKnots * 3600.0;
        }

        return totalSeconds;
    }

    static string FormatAltitude(double? feet)
    {
        if (!feet.HasValue)
            return "—";

        return $"{feet.Value:N0} FT";
    }

    static string FormatDuration(double totalSeconds)
    {
        if (totalSeconds <= 0)
            return "0:00";

        int total = (int)Math.Round(totalSeconds);
        int minutes = total / 60;
        int seconds = total % 60;
        return $"{minutes}:{seconds:D2}";
    }
}
