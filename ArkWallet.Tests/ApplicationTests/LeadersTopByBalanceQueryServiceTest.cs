using ArkWallet.Application.Common;
using ArkWallet.Application.Contracts.Leaders;
using ArkWallet.Application.Contracts.TraderServices;
using ArkWallet.Application.Services.Leaders;
using ArkWallet.Application.Services.TraderServices;
using ArkWallet.Infrastructure.Data;
using ArkWallet.Tests.HelpTools;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ArkWallet.Tests.ApplicationTests;

public class LeadersTopByBalanceQueryServiceTest : IDisposable
{
    private readonly ArkWalletDbContext _db;
    private readonly LeadersTopByBalanceQueryService _service;
    private readonly Mock<IBalanceSnapshotService> _mockSnapshotService;

    public LeadersTopByBalanceQueryServiceTest()
    {
        _db = DbTest.CreateDbContext();
        _db.Database.EnsureCreated();

        _mockSnapshotService = new Mock<IBalanceSnapshotService>();
        _service = new LeadersTopByBalanceQueryService(
            _db,
            _mockSnapshotService.Object,
            NullLogger<LeadersTopByBalanceQueryService>.Instance);
    }

    public void Dispose()
    {
        _db.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task GetTopAsync_EmptyDb_ReturnsEmptyList()
    {
        var result = await _service.GetTopAsync(10);

        Assert.True(result.IsSuccess, $"IsSuccess={result.IsSuccess}, Message={result.Message}");
        Assert.True(result.TryGetData(out var data));
        Assert.Empty(data);
    }

    [Fact]
    public async Task GetTopAsync_ReturnsTradersSortedByTotalBalance()
    {
        await HelpMethods.RegisterTrader(_db, 1001, "Alice");
        await HelpMethods.RegisterTrader(_db, 1002, "Bob");
        await HelpMethods.RegisterTrader(_db, 1003, "Charlie");

        await HelpMethods.GiveMoney(_db, 1001, 500);
        await HelpMethods.GiveMoney(_db, 1002, 1500);
        await HelpMethods.GiveMoney(_db, 1003, 3000);

        var snapshotBalances = new Dictionary<long, decimal>
        {
            { 1001, 5000m },
            { 1002, 2000m },
            { 1003, 8000m }
        };

        _mockSnapshotService
            .Setup(s => s.TakeTotalTraderBalanceSnapshot(It.IsAny<long>()))
            .ReturnsAsync((long id) =>
            {
                var trader = _db.Traders.First(t => t.TelegramId == id);
                var totalBalance = snapshotBalances.GetValueOrDefault(id, trader.Balance);
                return Result<BalanceSnapshotData>.Ok(
                    new BalanceSnapshotData(id, totalBalance, trader.Balance, 0, 0, 0, DateTime.UtcNow));
            });

        var result = await _service.GetTopAsync(3);

        Assert.True(result.IsSuccess, $"Failed: {result.Message}");
        Assert.True(result.TryGetData(out var data));
        Assert.Equal(3, data.Count);
        Assert.Equal(1003, data[0].TraderId);
        Assert.Equal(8000m, data[0].TotalBalance);
        Assert.Equal(1001, data[1].TraderId);
        Assert.Equal(5000m, data[1].TotalBalance);
        Assert.Equal(1002, data[2].TraderId);
        Assert.Equal(2000m, data[2].TotalBalance);
    }

    [Fact]
    public async Task GetTopAsync_ExcludesBots_Ids100To1000()
    {
        await HelpMethods.RegisterTrader(_db, 500, "Bot1");
        await HelpMethods.RegisterTrader(_db, 501, "Bot2");
        await HelpMethods.RegisterTrader(_db, 1001, "Alice");
        await HelpMethods.RegisterTrader(_db, 1002, "Bob");

        _mockSnapshotService
            .Setup(s => s.TakeTotalTraderBalanceSnapshot(It.IsAny<long>()))
            .ReturnsAsync((long id) =>
            {
                var trader = _db.Traders.First(t => t.TelegramId == id);
                return Result<BalanceSnapshotData>.Ok(
                    new BalanceSnapshotData(id, trader.Balance, trader.Balance, 0, 0, 0, DateTime.UtcNow));
            });

        var result = await _service.GetTopAsync(10);

        Assert.True(result.IsSuccess, $"Failed: {result.Message}");
        Assert.True(result.TryGetData(out var data));
        Assert.Equal(2, data.Count);
        Assert.All(data, e => Assert.True(e.TraderId > 1000));
    }

    [Fact]
    public async Task GetTopAsync_CountExceedsAvailable_ReturnsAllAvailable()
    {
        await HelpMethods.RegisterTrader(_db, 1001, "Alice");
        await HelpMethods.RegisterTrader(_db, 1002, "Bob");

        _mockSnapshotService
            .Setup(s => s.TakeTotalTraderBalanceSnapshot(It.IsAny<long>()))
            .ReturnsAsync((long id) =>
            {
                var trader = _db.Traders.First(t => t.TelegramId == id);
                return Result<BalanceSnapshotData>.Ok(
                    new BalanceSnapshotData(id, trader.Balance, trader.Balance, 0, 0, 0, DateTime.UtcNow));
            });

        var result = await _service.GetTopAsync(50);

        Assert.True(result.IsSuccess, $"Failed: {result.Message}");
        Assert.True(result.TryGetData(out var data));
        Assert.Equal(2, data.Count);
    }

    [Fact]
    public async Task GetTopAsync_CorrectPositionNumbers()
    {
        await HelpMethods.RegisterTrader(_db, 1001, "Alice");
        await HelpMethods.RegisterTrader(_db, 1002, "Bob");

        _mockSnapshotService
            .Setup(s => s.TakeTotalTraderBalanceSnapshot(It.IsAny<long>()))
            .ReturnsAsync((long id) =>
            {
                var trader = _db.Traders.First(t => t.TelegramId == id);
                return Result<BalanceSnapshotData>.Ok(
                    new BalanceSnapshotData(id, trader.Balance, trader.Balance, 0, 0, 0, DateTime.UtcNow));
            });

        var result = await _service.GetTopAsync(10);

        Assert.True(result.IsSuccess, $"Failed: {result.Message}");
        Assert.True(result.TryGetData(out var data));
        Assert.Equal(1, data[0].Position);
        Assert.Equal(2, data[1].Position);
    }

    [Fact]
    public async Task GetTraderPositionAsync_ReturnsCorrectPosition()
    {
        await HelpMethods.RegisterTrader(_db, 1001, "Alice");
        await HelpMethods.RegisterTrader(_db, 1002, "Bob");
        await HelpMethods.RegisterTrader(_db, 1003, "Charlie");

        _mockSnapshotService
            .Setup(s => s.TakeTotalTraderBalanceSnapshot(It.IsAny<long>()))
            .ReturnsAsync((long id) =>
            {
                var balances = new Dictionary<long, decimal> { { 1001, 3000m }, { 1002, 5000m }, { 1003, 1000m } };
                var balance = balances.GetValueOrDefault(id, 0m);
                return Result<BalanceSnapshotData>.Ok(
                    new BalanceSnapshotData(id, balance, balance, 0, 0, 0, DateTime.UtcNow));
            });

        var result = await _service.GetTraderPositionAsync(1002);

        Assert.True(result.IsSuccess, $"Failed: {result.Message}");
        Assert.True(result.TryGetData(out var data));
        Assert.Equal(1, data.Position);
        Assert.Equal(3, data.TotalTraders);
    }

    [Fact]
    public async Task GetTraderPositionAsync_BotIds_ExcludedFromCount()
    {
        await HelpMethods.RegisterTrader(_db, 500, "Bot");
        await HelpMethods.RegisterTrader(_db, 1001, "Alice");
        await HelpMethods.RegisterTrader(_db, 1002, "Bob");

        _mockSnapshotService
            .Setup(s => s.TakeTotalTraderBalanceSnapshot(It.IsAny<long>()))
            .ReturnsAsync((long id) =>
            {
                var balances = new Dictionary<long, decimal> { { 1001, 3000m }, { 1002, 5000m } };
                var balance = balances.GetValueOrDefault(id, 0m);
                return Result<BalanceSnapshotData>.Ok(
                    new BalanceSnapshotData(id, balance, balance, 0, 0, 0, DateTime.UtcNow));
            });

        var result = await _service.GetTraderPositionAsync(1002);

        Assert.True(result.IsSuccess, $"Failed: {result.Message}");
        Assert.True(result.TryGetData(out var data));
        Assert.Equal(1, data.Position);
        Assert.Equal(2, data.TotalTraders);
    }

    [Fact]
    public async Task GetLocalTopAsync_ReturnsSurroundingTraders()
    {
        for (long i = 1001; i <= 1010; i++)
            await HelpMethods.RegisterTrader(_db, i, $"User{i}");

        _mockSnapshotService
            .Setup(s => s.TakeTotalTraderBalanceSnapshot(It.IsAny<long>()))
            .ReturnsAsync((long id) =>
            {
                var balance = id * 10m;
                return Result<BalanceSnapshotData>.Ok(
                    new BalanceSnapshotData(id, balance, balance, 0, 0, 0, DateTime.UtcNow));
            });

        var result = await _service.GetLocalTopAsync(1005, 2, 2);

        Assert.True(result.IsSuccess, $"Failed: {result.Message}");
        Assert.True(result.TryGetData(out var data));
        Assert.True(data.Count <= 5);
        Assert.Contains(data, e => e.TraderId == 1005);
    }

    [Fact]
    public async Task GetLocalTopAsync_FirstTrader_ShowsNoOneAbove()
    {
        for (long i = 1001; i <= 1005; i++)
            await HelpMethods.RegisterTrader(_db, i, $"User{i}");

        _mockSnapshotService
            .Setup(s => s.TakeTotalTraderBalanceSnapshot(It.IsAny<long>()))
            .ReturnsAsync((long id) =>
            {
                var balances = new Dictionary<long, decimal>
                {
                    { 1001, 1000m }, { 1002, 2000m }, { 1003, 3000m },
                    { 1004, 4000m }, { 1005, 5000m }
                };
                var balance = balances.GetValueOrDefault(id, 0m);
                return Result<BalanceSnapshotData>.Ok(
                    new BalanceSnapshotData(id, balance, balance, 0, 0, 0, DateTime.UtcNow));
            });

        var result = await _service.GetLocalTopAsync(1005, 2, 2);

        Assert.True(result.IsSuccess, $"Failed: {result.Message}");
        Assert.True(result.TryGetData(out var data));
        Assert.Contains(data, e => e.TraderId == 1005);
    }

    [Fact]
    public async Task GetTopAsync_SnapshotFailure_ReturnsFail()
    {
        await HelpMethods.RegisterTrader(_db, 1001, "Alice");

        _mockSnapshotService
            .Setup(s => s.TakeTotalTraderBalanceSnapshot(It.IsAny<long>()))
            .ReturnsAsync(Result<BalanceSnapshotData>.Fail("Snapshot unavailable"));

        var result = await _service.GetTopAsync(10);

        Assert.False(result.IsSuccess);
        Assert.Contains("не удалось", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetTopAsync_MaxCount_ClampedTo100()
    {
        await HelpMethods.RegisterTrader(_db, 1001, "Alice");

        _mockSnapshotService
            .Setup(s => s.TakeTotalTraderBalanceSnapshot(It.IsAny<long>()))
            .ReturnsAsync((long id) =>
            {
                return Result<BalanceSnapshotData>.Ok(
                    new BalanceSnapshotData(id, 1000m, 1000m, 0, 0, 0, DateTime.UtcNow));
            });

        var result = await _service.GetTopAsync(1000);

        Assert.True(result.IsSuccess, $"Failed: {result.Message}");
        Assert.True(result.TryGetData(out var data));
        Assert.Single(data);
    }
}
