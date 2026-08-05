using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace ArkWallet.PerformanceTests.Measurement;

public sealed record QuerySummary(string CommandText, int Count, double TotalMs);

public sealed record QuerySnapshot(int Count, double TotalMs, IReadOnlyList<QuerySummary> TopHeavy)
{
    public static readonly QuerySnapshot Empty = new(0, 0, Array.Empty<QuerySummary>());
}

public sealed class QueryCounter : IDbCommandInterceptor
{
    private const int TopHeavyLimit = 10;

    private readonly object _lock = new();
    private readonly Dictionary<string, CommandStats> _byText = new();
    private int _totalCount;
    private long _totalTicks;

    public QuerySnapshot Snapshot()
    {
        lock (_lock)
        {
            var topHeavy = _byText.Values
                .Select(s => new QuerySummary(s.CommandText, s.Count, ToMs(s.TotalTicks)))
                .OrderByDescending(s => s.TotalMs)
                .ThenByDescending(s => s.Count)
                .Take(TopHeavyLimit)
                .ToArray();

            return new QuerySnapshot(_totalCount, ToMs(_totalTicks), topHeavy);
        }
    }

    public void Reset()
    {
        lock (_lock)
        {
            _byText.Clear();
            _totalCount = 0;
            _totalTicks = 0;
        }
    }

    public DbDataReader ReaderExecuted(DbCommand command, CommandExecutedEventData eventData, DbDataReader result)
    {
        Record(eventData.Duration, eventData.Command.CommandText);
        return result;
    }

    public object? ScalarExecuted(DbCommand command, CommandExecutedEventData eventData, object? result)
    {
        Record(eventData.Duration, eventData.Command.CommandText);
        return result;
    }

    public int NonQueryExecuted(DbCommand command, CommandExecutedEventData eventData, int result)
    {
        Record(eventData.Duration, eventData.Command.CommandText);
        return result;
    }

    public void CommandFailed(DbCommand command, CommandErrorEventData eventData)
        => Record(eventData.Duration, eventData.Command.CommandText);

    public async ValueTask<DbDataReader> ReaderExecutedAsync(
        DbCommand command, CommandExecutedEventData eventData, DbDataReader result, CancellationToken cancellationToken = default)
    {
        Record(eventData.Duration, eventData.Command.CommandText);
        return result;
    }

    public async ValueTask<object?> ScalarExecutedAsync(DbCommand command, CommandExecutedEventData eventData, object? result, CancellationToken cancellationToken = default)
    {
        Record(eventData.Duration, eventData.Command.CommandText);
        return result;
    }

    public async ValueTask<int> NonQueryExecutedAsync(
        DbCommand command, CommandExecutedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        Record(eventData.Duration, eventData.Command.CommandText);
        return result;
    }

    public async Task CommandFailedAsync(DbCommand command, CommandErrorEventData eventData, CancellationToken cancellationToken = default)
        => Record(eventData.Duration, eventData.Command.CommandText);

    private void Record(TimeSpan duration, string commandText)
    {
        lock (_lock)
        {
            _totalCount++;
            _totalTicks += duration.Ticks;

            if (!_byText.TryGetValue(commandText, out var stats))
            {
                stats = new CommandStats(commandText);
                _byText.Add(commandText, stats);
            }

            stats.Count++;
            stats.TotalTicks += duration.Ticks;
        }
    }

    private static double ToMs(long ticks) => TimeSpan.FromTicks(ticks).TotalMilliseconds;

    private sealed class CommandStats
    {
        public CommandStats(string commandText) => CommandText = commandText;

        public string CommandText { get; }
        public int Count;
        public long TotalTicks;
    }
}

