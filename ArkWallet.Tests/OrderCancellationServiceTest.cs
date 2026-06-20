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
}