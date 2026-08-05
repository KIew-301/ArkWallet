using System.Text.Json;

namespace ArkWallet.PerformanceTests.Measurement;

public sealed record GateReport(
    string Scenario,
    DateTime Timestamp,
    int QueryBudget,
    int SaveChangesBudget,
    PerfReport Perf,
    QuerySnapshot Queries,
    int SaveChanges);

internal static class PerfReporter
{
    public static string ReportsDirectory { get; } = FindReportsDirectory();

    public static void Save(GateReport report)
    {
        Directory.CreateDirectory(ReportsDirectory);
        var path = Path.Combine(ReportsDirectory, $"{report.Scenario}.json");
        File.WriteAllText(path, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
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
