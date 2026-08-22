using ArkWallet.Application.Services.TraderServices;
using ArkWallet.Domain.Entities;
using ArkWallet.Tests.HelpTools;
using Microsoft.Extensions.Logging.Abstractions;

namespace ArkWallet.Tests.ServiceTests.Balance;

public class BalanceSnapshotServiceTest
{
    private static readonly long[] TraderIds = [101L, 102L];

    [Fact]
    public async Task TakeSnapshot_ValidData_ReturnsSuccess()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.RegisterTrader(db, 101);
        await HelpMethods.RegisterTrader(db, 102);
        await HelpMethods.CreateToken(db, "ZZZ");
        await HelpMethods.AddPortfolio(db, 102, "ZZZ", 30);

        await HelpMethods.PlaceOrder(db, 102, "продать", "ZZZ", 5, 60);
        await HelpMethods.PlaceOrder(db, 102, "продать", "ZZZ", 5, 80);
        await HelpMethods.PlaceOrder(db, 102, "продать", "ZZZ", 5, 100);
        await HelpMethods.PlaceOrder(db, 102, "купить", "ZZZ", 10, 50);
        await HelpMethods.PlaceOrder(db, 101, "купить", "ZZZ", 10, 90);

        var resultSnapshot = await HelpMethods.TakeBalanceSnapshot(db, 102);

        Assert.True(resultSnapshot.TryGetData(out var data));
        Assert.Equal(3300, data.totalBalance);
        Assert.Equal(1200, data.mainBalance);
        Assert.Equal(500, data.longOrderReserve);
        Assert.Equal(400, data.shortOrderReserve);
        Assert.Equal(1200, data.balanceInTokens);
    }

    [Fact]
    public async Task TakeSnapshot_NotExistTrader_ReturnsFail()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        var resultSnapshot = await HelpMethods.TakeBalanceSnapshot(db, 101);

        Assert.False(resultSnapshot.IsSuccess);
        Assert.Equal("Трейдер на найден", resultSnapshot.Message);
    }

    [Fact]
    public async Task SaveSnapshot_ValidData_ReturnsSuccess()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.RegisterTrader(db, 101);
        var resultSnapshot = new BalanceSnapshotData(101, 2000, 1000, 250, 250, 500, DateTime.UtcNow);

        var saveSnapshotResult = await HelpMethods.SaveBalanceSnapshot(
            db,
            resultSnapshot.traderTelegramId,
            resultSnapshot.totalBalance,
            resultSnapshot.mainBalance,
            resultSnapshot.longOrderReserve,
            resultSnapshot.shortOrderReserve,
            resultSnapshot.balanceInTokens,
            resultSnapshot.dateTimeSnapshot
        );

        var balanceHistory = await HelpMethods.GetBalanceHistory(db, resultSnapshot.traderTelegramId);

        Assert.True(saveSnapshotResult.IsSuccess);
        Assert.Equal(2000, balanceHistory[0].TotalBalance);
        Assert.Equal(1000, balanceHistory[0].MainBalance);
        Assert.Equal(250, balanceHistory[0].LongOrderReserveBalance);
        Assert.Equal(250, balanceHistory[0].ShortOrderReserveBalance);
        Assert.Equal(500, balanceHistory[0].BalanceInTokens);
    }

    [Fact]
    public async Task SaveSnapshot_WithDefaultDate_ReturnsFail()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.RegisterTrader(db, 101);
        var resultSnapshot = new BalanceSnapshotData(101, 2000, 1000, 250, 250, 500, DateTime.UtcNow);

        var saveSnapshotResult = await HelpMethods.SaveBalanceSnapshot(
            db,
            resultSnapshot.traderTelegramId,
            resultSnapshot.totalBalance,
            resultSnapshot.mainBalance,
            resultSnapshot.longOrderReserve,
            resultSnapshot.shortOrderReserve,
            resultSnapshot.balanceInTokens,
            default
        );

        var balanceHistory = await HelpMethods.GetBalanceHistory(db, resultSnapshot.traderTelegramId);

        Assert.False(saveSnapshotResult.IsSuccess);
        Assert.Equal($"Некорректная дата и время снимка (default)", saveSnapshotResult.Message);
    }

    [Fact]
    public async Task TakeSnapshot_IgnoreUnactiveOrders_ReturnsSuccess()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.RegisterTrader(db, 101);
        await HelpMethods.RegisterTrader(db, 102);
        await HelpMethods.CreateToken(db, "ZZZ");
        await HelpMethods.AddPortfolio(db, 102, "ZZZ", 30);
        await HelpMethods.AddPortfolio(db, 101, "ZZZ", 30);

        await HelpMethods.PlaceOrder(db, 102, "купить", "ZZZ", 5, 50);
        await HelpMethods.PlaceOrder(db, 101, "продать", "ZZZ", 5, 50);
        await HelpMethods.PlaceOrder(db, 102, "купить", "ZZZ", 5, 50);
        await HelpMethods.CancelAllOrders(db, 102);

        var resultSnapshot = await HelpMethods.TakeBalanceSnapshot(db, 102);

        Assert.True(resultSnapshot.TryGetData(out var data));
        Assert.Equal(0, data.longOrderReserve);
    }

    [Fact]
    public async Task TakeTotalTraderBalanceSnapshots_MultipleTraders_ReturnsSnapshots()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.RegisterTrader(db, 101);
        await HelpMethods.RegisterTrader(db, 102);
        await HelpMethods.CreateToken(db, "ZZZ");
        await HelpMethods.AddPortfolio(db, 102, "ZZZ", 10);
        await HelpMethods.PlaceOrder(db, 102, "продать", "ZZZ", 5, 60);

        var service = new BalanceSnapshotService(db, NullLogger<BalanceSnapshotService>.Instance);
        var result = await service.TakeTotalTraderBalanceSnapshotsAsync(TraderIds);

        Assert.True(result.IsSuccess, result.Message);
        Assert.True(result.TryGetData(out var data));
        Assert.Equal(2, data.Count);
        Assert.Contains(101L, data.Keys);
        Assert.Contains(102L, data.Keys);
        Assert.Equal(1000m, data[101L].mainBalance);
    }

    [Fact]
    public async Task TakeTotalTraderBalanceSnapshots_EmptyIds_ReturnsEmptyDictionary()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        var service = new BalanceSnapshotService(db, NullLogger<BalanceSnapshotService>.Instance);
        var result = await service.TakeTotalTraderBalanceSnapshotsAsync(Array.Empty<long>());

        Assert.True(result.IsSuccess, result.Message);
        Assert.True(result.TryGetData(out var data));
        Assert.Empty(data);
    }

    [Fact]
    public async Task TakeSnapshot_WithActiveMiningSlots_IncludesSlotCostInTotalBalance()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.RegisterTrader(db, 101);
        await HelpMethods.CreateMiningSlot(db, 101, 500, MiningMachineSlotStatus.Active);

        var result = await HelpMethods.TakeBalanceSnapshot(db, 101);

        Assert.True(result.TryGetData(out var data));
        Assert.Equal(1500m, data.totalBalance);
    }

    [Fact]
    public async Task TakeSnapshot_WithPassiveMiningSlots_IncludesSlotCostInTotalBalance()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.RegisterTrader(db, 101);
        await HelpMethods.CreateMiningSlot(db, 101, 300, MiningMachineSlotStatus.Passive);

        var result = await HelpMethods.TakeBalanceSnapshot(db, 101);

        Assert.True(result.TryGetData(out var data));
        Assert.Equal(1300m, data.totalBalance);
    }

    [Fact]
    public async Task TakeSnapshot_WithSwitchingMiningSlots_IncludesSlotCostInTotalBalance()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.RegisterTrader(db, 101);
        await HelpMethods.CreateMiningSlot(db, 101, 700, MiningMachineSlotStatus.Switching);

        var result = await HelpMethods.TakeBalanceSnapshot(db, 101);

        Assert.True(result.TryGetData(out var data));
        Assert.Equal(1700m, data.totalBalance);
    }

    [Fact]
    public async Task TakeSnapshot_WithSoldMiningSlots_ExcludesSlotCostFromTotalBalance()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.RegisterTrader(db, 101);
        await HelpMethods.CreateMiningSlot(db, 101, 500, MiningMachineSlotStatus.Sold);

        var result = await HelpMethods.TakeBalanceSnapshot(db, 101);

        Assert.True(result.TryGetData(out var data));
        Assert.Equal(1000m, data.totalBalance);
    }

    [Fact]
    public async Task TakeSnapshot_WithMixedMiningSlots_SumsOnlyNonSold()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.RegisterTrader(db, 101);
        await HelpMethods.CreateMiningSlot(db, 101, 200, MiningMachineSlotStatus.Active);
        await HelpMethods.CreateMiningSlot(db, 101, 300, MiningMachineSlotStatus.Passive);
        await HelpMethods.CreateMiningSlot(db, 101, 400, MiningMachineSlotStatus.Sold);
        await HelpMethods.CreateMiningSlot(db, 101, 500, MiningMachineSlotStatus.Active);

        var result = await HelpMethods.TakeBalanceSnapshot(db, 101);

        Assert.True(result.TryGetData(out var data));
        Assert.Equal(2000m, data.totalBalance);
    }

    [Fact]
    public async Task TakeSnapshot_WithMiningSlotsAndOrders_SumsAllComponents()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.RegisterTrader(db, 101);
        await HelpMethods.CreateToken(db, "ZZZ");
        await HelpMethods.AddPortfolio(db, 101, "ZZZ", 10);
        await HelpMethods.CreateMiningSlot(db, 101, 600, MiningMachineSlotStatus.Active);

        var result = await HelpMethods.TakeBalanceSnapshot(db, 101);

        Assert.True(result.TryGetData(out var data));
        Assert.Equal(1000m, data.mainBalance);
        Assert.Equal(0m, data.longOrderReserve);
        Assert.Equal(0m, data.shortOrderReserve);
        Assert.Equal(100000m, data.balanceInTokens);
        Assert.Equal(600m, data.totalBalance - data.mainBalance - data.balanceInTokens);
        Assert.Equal(101600m, data.totalBalance);
    }

    [Fact]
    public async Task TakeTotalTraderBalanceSnapshots_MultipleTraders_IncludesMiningSlots()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.RegisterTrader(db, 101);
        await HelpMethods.RegisterTrader(db, 102);
        await HelpMethods.CreateMiningSlot(db, 101, 300, MiningMachineSlotStatus.Active);
        await HelpMethods.CreateMiningSlot(db, 102, 700, MiningMachineSlotStatus.Active);

        var service = new BalanceSnapshotService(db, NullLogger<BalanceSnapshotService>.Instance);
        var result = await service.TakeTotalTraderBalanceSnapshotsAsync(TraderIds);

        Assert.True(result.IsSuccess, result.Message);
        Assert.True(result.TryGetData(out var data));
        Assert.Equal(1300m, data[101L].totalBalance);
        Assert.Equal(1700m, data[102L].totalBalance);
    }

    [Fact]
    public async Task TakeTotalTraderBalanceSnapshots_MultipleTraders_SoldSlotsExcluded()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.RegisterTrader(db, 101);
        await HelpMethods.RegisterTrader(db, 102);
        await HelpMethods.CreateMiningSlot(db, 101, 500, MiningMachineSlotStatus.Sold);
        await HelpMethods.CreateMiningSlot(db, 102, 500, MiningMachineSlotStatus.Active);

        var service = new BalanceSnapshotService(db, NullLogger<BalanceSnapshotService>.Instance);
        var result = await service.TakeTotalTraderBalanceSnapshotsAsync(TraderIds);

        Assert.True(result.IsSuccess, result.Message);
        Assert.True(result.TryGetData(out var data));
        Assert.Equal(1000m, data[101L].totalBalance);
        Assert.Equal(1500m, data[102L].totalBalance);
    }

    [Fact]
    public async Task TakeSnapshot_ComplexMatchingScenario_ReturnsCorrectBalance()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.RegisterTrader(db, 101);
        await HelpMethods.RegisterTrader(db, 102);
        await HelpMethods.RegisterTrader(db, 103);

        await HelpMethods.CreateToken(db, "ZZZ");

        await HelpMethods.AddPortfolio(db, 102, "ZZZ", 20);
        await HelpMethods.AddPortfolio(db, 103, "ZZZ", 30);
        await HelpMethods.PlaceOrder(db, 102, "купить", "ZZZ", 20, 20);
        await HelpMethods.PlaceOrder(db, 102, "продать", "ZZZ", 20, 40);
        await HelpMethods.PlaceOrder(db, 101, "купить", "ZZZ", 15, 40);
        await HelpMethods.PlaceOrder(db, 101, "продать", "ZZZ", 15, 20);
        await HelpMethods.PlaceOrder(db, 101, "купить", "ZZZ", 1, 30);
        await HelpMethods.PlaceOrder(db, 103, "продать", "ZZZ", 3, 30);

        var result = await HelpMethods.TakeBalanceSnapshot(db, 102);

        Assert.True(result.TryGetData(out var data));

        Assert.Equal(100m, data.longOrderReserve);
        Assert.Equal(150m, data.shortOrderReserve);
        Assert.Equal(450m, data.balanceInTokens);
        Assert.Equal(1200m, data.mainBalance);
        Assert.Equal(1900m, data.totalBalance);
    }
}
