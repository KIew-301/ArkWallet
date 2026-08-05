using ArkWallet.PerformanceTests.Measurement;

namespace ArkWallet.PerformanceTests.Helpers;

internal static class GateAssert
{
    public static void QueryBudget(
        string scenario,
        int budget,
        QueryCounter counter,
        PerfScope scope,
        int saveChangesBudget = int.MaxValue,
        SaveChangesCounter? saveChangesCounter = null)
    {
        var report = scope.Report();
        var snapshot = counter.Snapshot();

        PerfReporter.Save(new GateReport(
            scenario,
            DateTime.UtcNow,
            budget,
            saveChangesBudget,
            report,
            snapshot,
            saveChangesCounter?.Count ?? 0));

        Assert.True(snapshot.Count <= budget,
            $"[{scenario}] SQL queries = {snapshot.Count}, budget = {budget}");

        if (saveChangesCounter != null)
        {
            Assert.True(saveChangesCounter.Count <= saveChangesBudget,
                $"[{scenario}] SaveChanges = {saveChangesCounter.Count}, budget = {saveChangesBudget}");
        }
    }
}
