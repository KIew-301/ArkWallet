namespace ArkWallet.PerformanceTests.Measurement;

public sealed record RepeatStats(double Mean, double Median, double P95, double Min, double Max, double StdDev);

public sealed record RepeatStepStats(string Name, RepeatStats Ms, RepeatStats Queries, RepeatStats Rows);

public sealed record RepeatReport(
    string Scenario,
    DateTime Timestamp,
    int Repeats,
    RepeatStats TotalMs,
    RepeatStats TotalQueries,
    RepeatStats TotalRows,
    IReadOnlyList<RepeatStepStats> Steps,
    IReadOnlyList<CounterRecord>? Counters = null);

internal static class RepeatStatsCalculator
{
    public static RepeatStats Calculate(IReadOnlyList<double> values)
    {
        if (values.Count == 0)
            return new RepeatStats(0, 0, 0, 0, 0, 0);

        var sorted = values.OrderBy(v => v).ToArray();
        var mean = values.Average();
        var variance = values.Sum(v => (v - mean) * (v - mean)) / Math.Max(1, values.Count - 1);

        return new RepeatStats(
            mean,
            Percentile(sorted, 0.50),
            Percentile(sorted, 0.95),
            sorted[0],
            sorted[^1],
            Math.Sqrt(variance));
    }

    private static double Percentile(double[] sorted, double q)
    {
        if (sorted.Length == 1)
            return sorted[0];

        var pos = q * (sorted.Length - 1);
        var index = (int)Math.Floor(pos);
        var frac = pos - index;
        return sorted[index] + (sorted[index + 1] - sorted[index]) * frac;
    }
}
