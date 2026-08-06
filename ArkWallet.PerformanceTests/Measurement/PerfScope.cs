using System.Diagnostics;
using System.Text.Json;

namespace ArkWallet.PerformanceTests.Measurement;

public sealed record StepReport(string Name, double Ms, int Queries, int Rows = 0);

public sealed record CounterRecord(string Name, long Value);

public sealed record PerfReport(
    IReadOnlyList<StepReport> Steps,
    double TotalMs,
    int TotalQueries,
    int TotalRows = 0,
    IReadOnlyList<CounterRecord>? Counters = null)
{
    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
}

public sealed class PerfScope : IDisposable
{
    private readonly QueryCounter _counter;
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly List<StepReport> _steps = new();
    private readonly int _baselineQueries;
    private readonly int _baselineRows;

    public PerfScope(QueryCounter counter)
    {
        _counter = counter ?? throw new ArgumentNullException(nameof(counter));
        _baselineQueries = counter.Snapshot().Count;
        _baselineRows = counter.Snapshot().TotalRows;
    }

    public PerfStep Step(string name) => new(this, name);

    internal int CurrentQueries => _counter.Snapshot().Count;

    internal int CurrentRows => _counter.Snapshot().TotalRows;

    internal void AddStep(string name, double ms, int queries, int rows) => _steps.Add(new StepReport(name, ms, queries, rows));

    public PerfReport Report()
    {
        var snapshot = _counter.Snapshot();
        var totalQueries = snapshot.Count - _baselineQueries;
        var totalRows = snapshot.TotalRows - _baselineRows;
        return new PerfReport(_steps.ToArray(), _clock.Elapsed.TotalMilliseconds, totalQueries, totalRows);
    }

    public void Dispose()
    {
    }
}

public sealed class PerfStep : IDisposable
{
    private readonly PerfScope _scope;
    private readonly string _name;
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly int _startQueries;
    private readonly int _startRows;

    internal PerfStep(PerfScope scope, string name)
    {
        _scope = scope;
        _name = name;
        _startQueries = scope.CurrentQueries;
        _startRows = scope.CurrentRows;
    }

    public void Dispose()
    {
        _scope.AddStep(_name, _clock.Elapsed.TotalMilliseconds, _scope.CurrentQueries - _startQueries, _scope.CurrentRows - _startRows);
    }
}
