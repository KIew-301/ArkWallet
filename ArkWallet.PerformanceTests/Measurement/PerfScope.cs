using System.Diagnostics;
using System.Text.Json;

namespace ArkWallet.PerformanceTests.Measurement;

public sealed record StepReport(string Name, double Ms, int Queries);

public sealed record PerfReport(IReadOnlyList<StepReport> Steps, double TotalMs, int TotalQueries)
{
    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
}

public sealed class PerfScope : IDisposable
{
    private readonly QueryCounter _counter;
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly List<StepReport> _steps = new();
    private readonly int _baselineQueries;

    public PerfScope(QueryCounter counter)
    {
        _counter = counter ?? throw new ArgumentNullException(nameof(counter));
        _baselineQueries = counter.Snapshot().Count;
    }

    public PerfStep Step(string name) => new(this, name);

    internal int CurrentQueries => _counter.Snapshot().Count;

    internal void AddStep(string name, double ms, int queries) => _steps.Add(new StepReport(name, ms, queries));

    public PerfReport Report()
    {
        var totalQueries = _counter.Snapshot().Count - _baselineQueries;
        return new PerfReport(_steps.ToArray(), _clock.Elapsed.TotalMilliseconds, totalQueries);
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

    internal PerfStep(PerfScope scope, string name)
    {
        _scope = scope;
        _name = name;
        _startQueries = scope.CurrentQueries;
    }

    public void Dispose()
    {
        _scope.AddStep(_name, _clock.Elapsed.TotalMilliseconds, _scope.CurrentQueries - _startQueries);
    }
}
