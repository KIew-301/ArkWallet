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

    [Fact]
    public void OverviewHtml_QueryIncreaseUnderTenCount_WithImprovedRows_IsImproved()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "arkwallet-perf-overview", Guid.NewGuid().ToString("N"));
        try
        {
            var current = new RunReport(DateTime.UtcNow, new[] { Scenario("heavy-balance-get", 5, 1702, 31) });
            var previousRuns = new RunReport[]
            {
                new(DateTime.UtcNow.AddMinutes(-1), new[] { Scenario("heavy-balance-get", 3, 500201, 3223) }),
            };

            OverviewReporter.Save(tempDir, current, previousRuns);
            var html = File.ReadAllText(Path.Combine(tempDir, "overview.html"));

            Assert.Contains("badge-ok\">Улучшено", html);
            Assert.DoesNotContain("badge-bad\">Регресс", html);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void OverviewHtml_QueryIncreaseAtLeastTenCount_IsRegressed()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "arkwallet-perf-overview", Guid.NewGuid().ToString("N"));
        try
        {
            var current = new RunReport(DateTime.UtcNow, new[] { Scenario("balance-main-changes", 12, 3, 5) });
            var previousRuns = new RunReport[]
            {
                new(DateTime.UtcNow.AddMinutes(-1), new[] { Scenario("balance-main-changes", 9, 3, 5) }),
            };

            OverviewReporter.Save(tempDir, current, previousRuns);
            var html = File.ReadAllText(Path.Combine(tempDir, "overview.html"));

            Assert.Contains("badge-bad\">Регресс", html);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void OverviewHtml_WithSelectedTarget_UsesTargetAsBaseline()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "arkwallet-perf-overview", Guid.NewGuid().ToString("N"));
        try
        {
            var current = new RunReport(DateTime.UtcNow, new[] { Scenario("heavy-balance-get", 5, 1702, 10) });
            var targetRun = new RunReport(DateTime.UtcNow.AddHours(-2), new[] { Scenario("heavy-balance-get", 6, 1000402, 6402) });

            OverviewReporter.Save(tempDir, current, new[] { targetRun }, "run-20260807-173936-855.json");
            var html = File.ReadAllText(Path.Combine(tempDir, "overview.html"));

            Assert.Contains("6402", html);
            Assert.Contains("выбранный прогон run-20260807-173936-855.json", html);
            Assert.Contains("badge-ok\">Улучшено", html);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void OverviewHtml_WithoutTarget_ShowsLatestBaselineLabel()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "arkwallet-perf-overview", Guid.NewGuid().ToString("N"));
        try
        {
            var current = new RunReport(DateTime.UtcNow, new[] { Scenario("heavy-balance-get", 5, 1702, 10) });
            var previousRuns = new RunReport[]
            {
                new(DateTime.UtcNow.AddMinutes(-1), new[] { Scenario("heavy-balance-get", 6, 1000402, 6402) }),
            };

            OverviewReporter.Save(tempDir, current, previousRuns);
            var html = File.ReadAllText(Path.Combine(tempDir, "overview.html"));

            Assert.Contains("база: последний прогон", html);
            Assert.DoesNotContain("выбранный прогон", html);
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
