using ArkWallet.PerformanceTests.Measurement;

namespace ArkWallet.PerformanceTests;

[CollectionDefinition("Perf")]
public sealed class PerfCollection : ICollectionFixture<PerfSummaryFinalizer>
{
}

public sealed class PerfSummaryFinalizer : IDisposable
{
    public void Dispose() => ReportSession.Close();
}
