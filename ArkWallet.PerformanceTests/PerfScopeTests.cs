using ArkWallet.PerformanceTests.Measurement;
using Microsoft.EntityFrameworkCore;

namespace ArkWallet.PerformanceTests;

public class PerfScopeTests
{
    [Fact]
    public async Task RecordsSteps_WithElapsedAndQueryDeltas()
    {
        var counter = new QueryCounter();
        await using var db = PerfDb.CreateDbContext(counter);
        await db.Database.EnsureCreatedAsync();
        counter.Reset();

        PerfReport report;
        using (var scope = new PerfScope(counter))
        {
            using (scope.Step("step1"))
            {
                await db.Traders.ToListAsync();
            }

            using (scope.Step("step2"))
            {
                await db.Traders.ToListAsync();
                await db.Traders.ToListAsync();
            }

            report = scope.Report();
        }

        Assert.Equal(2, report.Steps.Count);
        Assert.Equal("step1", report.Steps[0].Name);
        Assert.Equal("step2", report.Steps[1].Name);
        Assert.Equal(1, report.Steps[0].Queries);
        Assert.Equal(2, report.Steps[1].Queries);
        Assert.Equal(3, report.TotalQueries);
        Assert.True(report.TotalMs >= 0);
        Assert.True(report.Steps.All(s => s.Ms >= 0));
    }

    [Fact]
    public void ReportJson_ContainsStepsAndTotals()
    {
        var counter = new QueryCounter();
        using (var scope = new PerfScope(counter))
        {
            using (scope.Step("empty"))
            {
            }

            var json = scope.Report().ToJson();

            Assert.Contains("\"empty\"", json);
            Assert.Contains("TotalMs", json);
            Assert.Contains("TotalQueries", json);
        }
    }
}
