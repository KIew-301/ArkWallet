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
}
