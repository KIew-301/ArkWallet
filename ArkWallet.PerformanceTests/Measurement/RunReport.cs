namespace ArkWallet.PerformanceTests.Measurement;

public sealed record RunReport(DateTime Timestamp, IReadOnlyList<RunScenario> Scenarios);

public sealed record RunScenario(
    string Id,
    string Title,
    string Kind,
    double Queries,
    double TotalMs,
    int Repeats,
    int? QueryBudget,
    int? TimeBudget,
    IReadOnlyList<StepReport> Steps,
    int Rows = 0,
    int? RowsBudget = null,
    IReadOnlyList<CounterRecord>? Counters = null);
