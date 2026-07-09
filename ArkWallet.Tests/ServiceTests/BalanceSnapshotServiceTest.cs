using ArkWallet.Application.Services.TraderServices;
using ArkWallet.Tests.HelpTools;

namespace ArkWallet.Tests.ServiceTests;

public class BalanceSnapshotServiceTest
{
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
}
