using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace ArkWallet.PerformanceTests.Measurement;

public sealed record QuerySummary(string CommandText, int Count, double TotalMs, int Rows);

public sealed record QuerySnapshot(int Count, double TotalMs, int TotalRows, IReadOnlyList<QuerySummary> TopHeavy)
{
    public static readonly QuerySnapshot Empty = new(0, 0, 0, Array.Empty<QuerySummary>());
}

public sealed class QueryCounter : IDbCommandInterceptor
{
    private const int TopHeavyLimit = 10;

    private readonly object _lock = new();
    private readonly Dictionary<string, CommandStats> _byText = new();
    private int _totalCount;
    private long _totalTicks;
    private int _totalRows;

    public QuerySnapshot Snapshot()
    {
        lock (_lock)
        {
            var topHeavy = _byText.Values
                .Select(s => new QuerySummary(s.CommandText, s.Count, ToMs(s.TotalTicks), s.Rows))
                .OrderByDescending(s => s.TotalMs)
                .ThenByDescending(s => s.Count)
                .Take(TopHeavyLimit)
                .ToArray();

            return new QuerySnapshot(_totalCount, ToMs(_totalTicks), _totalRows, topHeavy);
        }
    }

    public void Reset()
    {
        lock (_lock)
        {
            _byText.Clear();
            _totalCount = 0;
            _totalTicks = 0;
            _totalRows = 0;
        }
    }

    public DbDataReader ReaderExecuted(DbCommand command, CommandExecutedEventData eventData, DbDataReader result)
        => WrapReader(Record(eventData.Duration, eventData.Command.CommandText, 0), result);

    public object? ScalarExecuted(DbCommand command, CommandExecutedEventData eventData, object? result)
    {
        Record(eventData.Duration, eventData.Command.CommandText, 1);
        return result;
    }

    public int NonQueryExecuted(DbCommand command, CommandExecutedEventData eventData, int result)
    {
        Record(eventData.Duration, eventData.Command.CommandText, result);
        return result;
    }

    public void CommandFailed(DbCommand command, CommandErrorEventData eventData)
        => Record(eventData.Duration, eventData.Command.CommandText, 0);

    public async ValueTask<DbDataReader> ReaderExecutedAsync(
        DbCommand command, CommandExecutedEventData eventData, DbDataReader result, CancellationToken cancellationToken = default)
        => WrapReader(Record(eventData.Duration, eventData.Command.CommandText, 0), result);

    public async ValueTask<object?> ScalarExecutedAsync(DbCommand command, CommandExecutedEventData eventData, object? result, CancellationToken cancellationToken = default)
    {
        Record(eventData.Duration, eventData.Command.CommandText, 1);
        return result;
    }

    public async ValueTask<int> NonQueryExecutedAsync(
        DbCommand command, CommandExecutedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        Record(eventData.Duration, eventData.Command.CommandText, result);
        return result;
    }

    public async Task CommandFailedAsync(DbCommand command, CommandErrorEventData eventData, CancellationToken cancellationToken = default)
        => Record(eventData.Duration, eventData.Command.CommandText, 0);

    private CommandStats Record(TimeSpan duration, string commandText, int rows)
    {
        lock (_lock)
        {
            _totalCount++;
            _totalTicks += duration.Ticks;
            _totalRows += rows;

            if (!_byText.TryGetValue(commandText, out var stats))
            {
                stats = new CommandStats(commandText);
                _byText.Add(commandText, stats);
}

            stats.Count++;
            stats.TotalTicks += duration.Ticks;
            stats.Rows += rows;
            return stats;
        }
    }

    private DbDataReader WrapReader(CommandStats stats, DbDataReader result)
        => new CountingDbDataReader(result, () =>
        {
            lock (_lock)
            {
                _totalRows++;
                stats.Rows++;
            }
        });

    private static double ToMs(long ticks) => TimeSpan.FromTicks(ticks).TotalMilliseconds;

    private sealed class CommandStats
    {
        public CommandStats(string commandText) => CommandText = commandText;

        public string CommandText { get; }
        public int Count;
        public long TotalTicks;
        public int Rows;
    }
}


