namespace ArkWallet.Tests;

public class OrderCancellationServiceTest
{
    [Fact]
    public async Task CancelOrderAsync_CancelLongOrder_ReturnSuccess()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.RegisterTrader(db, 101);
        await HelpMethods.CreateToken(db, "ZZZ");

        var result1 = await HelpMethods.PlaceOrder(db, 101, "купить", "ZZZ", 5, 100);
        var result2 = await HelpMethods.CancelOrder(db, 101, result1.Order.Id);

        var trader = await HelpMethods.GetTrader(db, 101);

        Assert.True(result2.IsSuccess);
        Assert.Equal(1000, trader.Balance);
    }

    [Fact]
    public async Task CancelOrderAsync_CancelShortOrder_ReturnSuccess()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.RegisterTrader(db, 101);
        await HelpMethods.CreateToken(db, "ZZZ");
        await HelpMethods.AddPortfolio(db, 101, "ZZZ", 10);

        var result1 = await HelpMethods.PlaceOrder(db, 101, "продать", "ZZZ", 5, 100);
        var result2 = await HelpMethods.CancelOrder(db, 101, result1.Order.Id);

        var trader = await HelpMethods.GetTrader(db, 101);
        var portfolio = await HelpMethods.GetPortfolio(db, 101);

        Assert.True(result2.IsSuccess);
        Assert.Equal(1000, trader.Balance);
        Assert.Equal(10, portfolio.Quantity);
    }

    [Fact]
    public async Task ProcessOrderAsync_CancelLongOrderAfterPartialExecution_ReturnsSuccess()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.RegisterTrader(db, 101);
        await HelpMethods.RegisterTrader(db, 102);
        await HelpMethods.CreateToken(db, "ZZZ");
        await HelpMethods.AddPortfolio(db, 102, "ZZZ", 30);

        var result1 = await HelpMethods.PlaceOrder(db, 102, "продать", "ZZZ", 3, 60);
        var result2 = await HelpMethods.PlaceOrder(db, 102, "продать", "ZZZ", 3, 80);
        var result3 = await HelpMethods.PlaceOrder(db, 101, "купить", "ZZZ", 9, 90);
        var result4 = await HelpMethods.CancelOrder(db, 101, result3.Order.Id);

        var trader = await HelpMethods.GetTrader(db, 101);
        var portfolio = await HelpMethods.GetPortfolio(db, 101, "ZZZ");

        Assert.Equal(580, trader.Balance);
        Assert.Equal(6, portfolio.Quantity);
    }

    [Fact]
    public async Task ProcessOrderAsync_CancelShortOrderAfterPartialExecution_ReturnsSuccess()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.RegisterTrader(db, 101);
        await HelpMethods.RegisterTrader(db, 102);
        await HelpMethods.CreateToken(db, "ZZZ");
        await HelpMethods.AddPortfolio(db, 102, "ZZZ", 30);

        var result1 = await HelpMethods.PlaceOrder(db, 102, "продать", "ZZZ", 3, 60);
        var result2 = await HelpMethods.PlaceOrder(db, 102, "продать", "ZZZ", 3, 80);
        var result3 = await HelpMethods.PlaceOrder(db, 101, "купить", "ZZZ", 9, 90);
        var result4 = await HelpMethods.CancelOrder(db, 102, result1.Order.Id);

        var trader = await HelpMethods.GetTrader(db, 102);
        var portfolio = await HelpMethods.GetPortfolio(db, 102, "ZZZ");

        Assert.Equal(1420, trader.Balance);
        Assert.Equal(24, portfolio.Quantity);
    }
}