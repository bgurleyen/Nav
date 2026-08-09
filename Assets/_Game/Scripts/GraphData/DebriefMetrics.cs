using UnityEngine;

public static class DebriefMetrics
{
    public struct ColumnValues
    {
        public string Me;
        public string Average;
        public string Best;
    }

    public static void LogPanel(LevelStat me, LevelStat average, LevelStat best, int? rank, int totalPlayers)
    {
        if (me == null)
            return;

        ColumnValues avgAlt = BuildAverageAltitude(me, best, average);
        ColumnValues lgAlt = BuildLgAltitude(me, best, average);
        ColumnValues flapsAlt = BuildAverageFlapsAltitude(me, best, average);
        ColumnValues sbUsage = BuildSpeedBrakeUsage(me, best, average);
        ColumnValues fuel = BuildFuelRemaining(me, best, average);

        Debug.Log(
            $"[Debrief] Average Altitude ME={avgAlt.Me} AVG={avgAlt.Average} BEST={avgAlt.Best}\n" +
            $"[Debrief] LG Altitude ME={lgAlt.Me} AVG={lgAlt.Average} BEST={lgAlt.Best}\n" +
            $"[Debrief] Avg Flaps Altitude ME={flapsAlt.Me} AVG={flapsAlt.Average} BEST={flapsAlt.Best}\n" +
            $"[Debrief] Total S/B Usage ME={sbUsage.Me} AVG={sbUsage.Average} BEST={sbUsage.Best}\n" +
            $"[Debrief] Fuel Remaining ME={fuel.Me} AVG={fuel.Average} BEST={fuel.Best}\n" +
            $"[Debrief] Rank={FormatRank(rank, totalPlayers)}");
    }

    public static ColumnValues BuildAverageAltitude(LevelStat me, LevelStat best, LevelStat average)
    {
        return new ColumnValues
        {
            Me = FormatAltitude(me?.averageAltitude),
            Average = FormatAltitude(average?.averageAltitude),
            Best = FormatAltitude(best?.averageAltitude),
        };
    }

    public static ColumnValues BuildLgAltitude(LevelStat me, LevelStat best, LevelStat average)
    {
        return new ColumnValues
        {
            Me = FormatAltitude(me?.lgAltitude),
            Average = FormatAltitude(average?.lgAltitude),
            Best = FormatAltitude(best?.lgAltitude),
        };
    }

    public static ColumnValues BuildAverageFlapsAltitude(LevelStat me, LevelStat best, LevelStat average)
    {
        return new ColumnValues
        {
            Me = FormatAltitude(me?.averageFlapAltitude),
            Average = FormatAltitude(average?.averageFlapAltitude),
            Best = FormatAltitude(best?.averageFlapAltitude),
        };
    }

    public static ColumnValues BuildSpeedBrakeUsage(LevelStat me, LevelStat best, LevelStat average)
    {
        return new ColumnValues
        {
            Me = FormatDuration(me?.speedBrakeSeconds),
            Average = FormatDuration(average?.speedBrakeSeconds),
            Best = FormatDuration(best?.speedBrakeSeconds),
        };
    }

    public static ColumnValues BuildFuelRemaining(LevelStat me, LevelStat best, LevelStat average)
    {
        return new ColumnValues
        {
            Me = FormatFuel(me?.remainingFuel),
            Average = FormatFuel(average?.remainingFuel),
            Best = FormatFuel(best?.remainingFuel),
        };
    }

    public static string FormatFuel(int? kilograms)
    {
        if (!kilograms.HasValue)
            return "—";

        return $"{kilograms.Value:N0} KG";
    }

    public static string FormatRank(int? rank, int totalPlayers)
    {
        if (rank.HasValue && rank.Value > 0)
            return $"#{rank.Value}";

        return "—";
    }

    static string FormatAltitude(int? feet)
    {
        if (!feet.HasValue || feet.Value <= 0)
            return "—";

        return $"{feet.Value:N0} FT";
    }

    static string FormatDuration(int? totalSeconds)
    {
        if (!totalSeconds.HasValue || totalSeconds.Value <= 0)
            return "0:00";

        int minutes = totalSeconds.Value / 60;
        int seconds = totalSeconds.Value % 60;
        return $"{minutes}:{seconds:D2}";
    }
}
