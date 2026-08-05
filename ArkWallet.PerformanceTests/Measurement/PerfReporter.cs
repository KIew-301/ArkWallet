using System.Text.Json;

namespace ArkWallet.PerformanceTests.Measurement;

public sealed record GateReport(
    string Scenario,
    DateTime Timestamp,
    int QueryBudget,
    int TimeBudget,
    int? SaveChangesBudget,
    PerfReport Perf,
    QuerySnapshot Queries,
    int SaveChanges,
    IReadOnlyDictionary<string, string>? Conditions = null);

internal static class PerfReporter
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static string ReportsDirectory { get; } = FindReportsDirectory();

    public static string ArchiveDirectory => Path.Combine(ReportsDirectory, "archive");

    public static void Save(GateReport report)
    {
        Directory.CreateDirectory(ReportsDirectory);
        var json = JsonSerializer.Serialize(report, JsonOptions);
        File.WriteAllText(Path.Combine(ReportsDirectory, $"{report.Scenario}.json"), json);

        Directory.CreateDirectory(ArchiveDirectory);
        File.WriteAllText(
            Path.Combine(ArchiveDirectory, $"{report.Scenario}-{report.Timestamp:yyyyMMdd-HHmmss-fff}.json"),
            json);
    }

    private static string FindReportsDirectory()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "ArkWallet.sln")))
                return Path.Combine(dir.FullName, "ArkWallet.PerformanceTests", "Reports");

            dir = dir.Parent;
        }

        return Path.Combine(AppContext.BaseDirectory, "Reports");
    }
}
