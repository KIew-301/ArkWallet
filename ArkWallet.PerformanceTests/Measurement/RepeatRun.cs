namespace ArkWallet.PerformanceTests.Measurement;

internal static class RepeatConfig
{
    public static int Repeats { get; } = ParseRepeats();
    public static string DatabaseProvider { get; } = ParseDatabaseProvider();

    private static int ParseRepeats()
    {
        var raw = Environment.GetEnvironmentVariable("ARKWALLET_PERF_REPEAT");
        return int.TryParse(raw, out var n) && n > 0 ? n : 0;
    }

    private static string ParseDatabaseProvider()
    {
        var raw = Environment.GetEnvironmentVariable("ARKWALLET_PERF_DB");
        return string.IsNullOrWhiteSpace(raw) ? "sqlite" : raw.Trim().ToLowerInvariant();
    }
}

internal static class RepeatRun
{
    public static async Task<RepeatReport> RunAsync(
        string scenario, int repeats, Func<Task<PerfReport>> runOnce)
    {
        var samples = new List<PerfReport>(repeats);
        for (var i = 0; i < repeats; i++)
            samples.Add(await runOnce());

        var totalMs = samples.Select(s => s.TotalMs).ToArray();
        var totalQueries = samples.Select(s => (double)s.TotalQueries).ToArray();
        var totalRows = samples.Select(s => (double)s.TotalRows).ToArray();
        var stepNames = samples.SelectMany(s => s.Steps.Select(st => st.Name)).Distinct().ToArray();

        var steps = stepNames
            .Select(name =>
            {
                var ms = samples.Select(s => s.Steps.FirstOrDefault(st => st.Name == name)?.Ms ?? 0).ToArray();
                var queries = samples.Select(s => (double)(s.Steps.FirstOrDefault(st => st.Name == name)?.Queries ?? 0)).ToArray();
                var rows = samples.Select(s => (double)(s.Steps.FirstOrDefault(st => st.Name == name)?.Rows ?? 0)).ToArray();
                return new RepeatStepStats(name, RepeatStatsCalculator.Calculate(ms), RepeatStatsCalculator.Calculate(queries), RepeatStatsCalculator.Calculate(rows));
            })
            .ToArray();

        return new RepeatReport(
            scenario, DateTime.UtcNow, repeats,
            RepeatStatsCalculator.Calculate(totalMs),
            RepeatStatsCalculator.Calculate(totalQueries),
            RepeatStatsCalculator.Calculate(totalRows),
            steps,
            samples[^1].Counters);
    }
}
