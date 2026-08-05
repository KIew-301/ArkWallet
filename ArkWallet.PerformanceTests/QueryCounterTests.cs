using ArkWallet.PerformanceTests.Measurement;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ArkWallet.PerformanceTests;

public class QueryCounterTests
{
    [Fact]
    public async Task Counts_ExecutedQueries_WithCommandText()
    {
        var counter = new QueryCounter();
        await using var db = PerfDb.CreateDbContext(counter);
        await db.Database.EnsureCreatedAsync();
        counter.Reset();

        var traders = await db.Traders.ToListAsync();

        Assert.NotNull(traders);
        var snapshot = counter.Snapshot();
        Assert.Equal(1, snapshot.Count);
        Assert.True(snapshot.TotalMs >= 0);
        var top = Assert.Single(snapshot.TopHeavy);
        Assert.Contains("Traders", top.CommandText, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, top.Count);
    }

    [Fact]
    public async Task Counts_FailedQueries()
    {
        var counter = new QueryCounter();
        await using var db = PerfDb.CreateDbContext(counter);
        await db.Database.EnsureCreatedAsync();
        counter.Reset();

        await Assert.ThrowsAsync<SqliteException>(() =>
            db.Database.ExecuteSqlRawAsync("SELECT * FROM missing_table"));

        var snapshot = counter.Snapshot();
        Assert.Equal(1, snapshot.Count);
        Assert.Contains(snapshot.TopHeavy, s => s.CommandText.Contains("missing_table", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Reset_ClearsAccumulatedCounters()
    {
        var counter = new QueryCounter();
        await using var db = PerfDb.CreateDbContext(counter);
        await db.Database.EnsureCreatedAsync();
        counter.Reset();

        await db.Traders.ToListAsync();
        Assert.Equal(1, counter.Snapshot().Count);

        counter.Reset();

        var snapshot = counter.Snapshot();
        Assert.Equal(0, snapshot.Count);
        Assert.Equal(0, snapshot.TotalMs);
        Assert.Empty(snapshot.TopHeavy);
    }
}
