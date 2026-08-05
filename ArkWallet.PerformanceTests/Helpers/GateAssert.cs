using ArkWallet.PerformanceTests.Gates;
using ArkWallet.PerformanceTests.Measurement;

namespace ArkWallet.PerformanceTests.Helpers;

internal static class GateAssert
{
    public static void QueryBudget(
        string scenario,
        Budget budget,
        QueryCounter counter,
        PerfScope scope,
        SaveChangesCounter? saveChangesCounter = null)
    {
        var report = scope.Report();
        var snapshot = counter.Snapshot();

        PerfReporter.Save(new GateReport(
            scenario,
            DateTime.UtcNow,
            budget.Queries,
            budget.TimeMs,
            budget.SaveChanges,
            report,
            snapshot,
            saveChangesCounter?.Count ?? 0,
            ScenarioCatalog.GetById(scenario)?.Conditions));

        Assert.True(snapshot.Count <= budget.Queries,
            $"[{scenario}] SQL queries = {snapshot.Count}, budget = {budget.Queries}");

        Assert.True(report.TotalMs <= budget.TimeMs,
            $"[{scenario}] time = {report.TotalMs:0.##} ms, budget = {budget.TimeMs} ms");

        if (budget.SaveChanges.HasValue && saveChangesCounter != null)
        {
            Assert.True(saveChangesCounter.Count <= budget.SaveChanges,
                $"[{scenario}] SaveChanges = {saveChangesCounter.Count}, budget = {budget.SaveChanges}");
        }
    }
}
