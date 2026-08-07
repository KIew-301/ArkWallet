using System.Net;
using ArkWallet.PerformanceTests.Measurement;

namespace ArkWallet.PerformanceTests;

[Collection("Perf")]
public class SummaryReportTests
{
    [Fact]
    public void GenerateSummaryHtml_ListsAllCatalogScenarios()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "arkwallet-perf-summary", Guid.NewGuid().ToString("N"));
        try
        {
            var run = new RunReport(DateTime.UtcNow, Array.Empty<RunScenario>());
            var path = HtmlReporter.SaveSummary(tempDir, run);

            Assert.True(File.Exists(path));
            var html = File.ReadAllText(path);

            foreach (var definition in ScenarioCatalog.All)
            {
                Assert.Contains(definition.Id, html, StringComparison.OrdinalIgnoreCase);
                Assert.Contains(WebUtility.HtmlEncode(definition.Title), html, StringComparison.OrdinalIgnoreCase);
            }

            Assert.Contains("NOT IMPLEMENTED", html);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void OverviewHtml_UsesLatestRunContainingScenarioAsBaseline()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "arkwallet-perf-overview", Guid.NewGuid().ToString("N"));
        try
        {
            var current = new RunReport(DateTime.UtcNow, new[] { Scenario("market-maker-tick-10t", 900, 617, 90) });
            var previousRuns = new RunReport[]
            {
                new(DateTime.UtcNow.AddMinutes(-1), new[] { Scenario("balance-main-changes", 3, 3, 0.5) }),
                new(DateTime.UtcNow.AddMinutes(-2), new[] { Scenario("market-maker-tick-10t", 1060, 777, 75) }),
            };

            OverviewReporter.Save(tempDir, current, previousRuns);
            var html = File.ReadAllText(Path.Combine(tempDir, "overview.html"));

            Assert.DoesNotContain("badge-warn\">Нет данных", html);
            Assert.Contains("1060", html);
            Assert.Contains("777", html);
            Assert.Contains("badge-ok\">Улучшено", html);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void OverviewHtml_MarksMissingScenarioAsNoData()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "arkwallet-perf-overview", Guid.NewGuid().ToString("N"));
        try
        {
            var current = new RunReport(DateTime.UtcNow, new[] { Scenario("market-maker-tick-10t", 900, 617, 90) });
            var previousRuns = new RunReport[]
            {
                new(DateTime.UtcNow.AddMinutes(-1), new[] { Scenario("balance-main-changes", 3, 3, 0.5) }),
            };

            OverviewReporter.Save(tempDir, current, previousRuns);
            var html = File.ReadAllText(Path.Combine(tempDir, "overview.html"));

            Assert.Contains("Нет данных", html);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    private static RunScenario Scenario(string id, double queries, int rows, double ms)
        => new(id, "Сценарий " + id, "Сервис", queries, ms, 100, null, null, Array.Empty<StepReport>(), rows, null, null);
}
