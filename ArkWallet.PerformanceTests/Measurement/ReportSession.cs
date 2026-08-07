using ArkWallet.PerformanceTests.Gates;

namespace ArkWallet.PerformanceTests.Measurement;

internal static class ReportSession
{
    private static readonly object Sync = new();
    private static readonly Dictionary<string, RepeatReport> Repeats = new(StringComparer.OrdinalIgnoreCase);

    public static void RecordRepeat(RepeatReport report)
    {
        lock (Sync)
            Repeats[report.Scenario] = report;
    }

    public static void RecordSingle(string scenario, PerfReport report, SaveChangesCounter? saveChanges = null)
    {
        IReadOnlyList<CounterRecord>? counters = saveChanges != null
            ? new[] { new CounterRecord("save-changes", saveChanges.Count) }
            : null;

        RecordRepeat(new RepeatReport(
            scenario,
            DateTime.UtcNow,
            1,
            RepeatStatsCalculator.Calculate(new[] { report.TotalMs }),
            RepeatStatsCalculator.Calculate(new[] { (double)report.TotalQueries }),
            RepeatStatsCalculator.Calculate(new[] { (double)report.TotalRows }),
            report.Steps.Select(step => new RepeatStepStats(
                step.Name,
                RepeatStatsCalculator.Calculate(new[] { step.Ms }),
                RepeatStatsCalculator.Calculate(new[] { (double)step.Queries }),
                RepeatStatsCalculator.Calculate(new[] { (double)step.Rows }))).ToArray(),
            counters));
    }

    public static RunReport Snapshot(DateTime timestamp)
    {
        lock (Sync)
        {
            var scenarios = new List<RunScenario>();
            foreach (var id in Repeats.Keys.OrderBy(x => x, StringComparer.Ordinal))
            {
                var repeat = Repeats[id];
                var definition = ScenarioCatalog.GetById(id);
                GateBudgets.ById.TryGetValue(id, out var budget);

                scenarios.Add(new RunScenario(
                    id,
                    definition?.Title ?? id,
                    definition?.Kind ?? "Сервис",
                    repeat.TotalQueries.Median,
                    repeat.TotalMs.Median,
                    repeat.Repeats,
                    budget?.Queries,
                    budget?.TimeMs,
                    repeat.Steps.Select(s => new StepReport(s.Name, s.Ms.Median, (int)Math.Round(s.Queries.Median), (int)Math.Round(s.Rows.Median))).ToArray(),
                    (int)Math.Round(repeat.TotalRows.Median),
                    budget?.Rows,
                    repeat.Counters));
            }

            return new RunReport(timestamp, scenarios);
        }
    }

    public static void Close()
    {
        lock (Sync)
        {
            if (Repeats.Count == 0)
                return;
        }

        ReportTarget.ConfigureFromEnvironment();

        var timestamp = DateTime.UtcNow;
        var run = Snapshot(timestamp);
        var previousRuns = RunArchive.ReadAllBefore(timestamp);
        var targetRun = ReportTarget.GetResolved();

        var baselineRuns = targetRun != null
            ? new[] { targetRun }
            : previousRuns;

        var runDirectory = Path.Combine(PerfReporter.ReportsDirectory, timestamp.ToString("yyyyMMdd-HHmmss-fff"));
        Directory.CreateDirectory(runDirectory);

        HtmlReporter.SaveSummary(runDirectory, run);
        OverviewReporter.Save(runDirectory, run, baselineRuns, ReportTarget.FileName);
        RunArchive.WriteRun(run);
    }
}
