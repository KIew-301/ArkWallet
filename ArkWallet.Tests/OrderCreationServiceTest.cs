using ArkWallet.Domain.ValueObjects;

namespace ArkWallet.Tests;
public class OrderCreationServiceTest
{
    [Fact]
    public async Task ProcessOrdersAsync_MatchingTest_ReturnsSuccess()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.RegisterTrader(db, 101, "FirstUser");
        await HelpMethods.RegisterTrader(db, 102, "SecondUser");
        await HelpMethods.CreateToken(db, "ZZZ");
        await HelpMethods.AddPortfolio(db, 102, "ZZZ", 10);

        var result1 = await HelpMethods.PlaceOrder(db, 101, "купить", "ZZZ", 5, 100);
        var result2 = await HelpMethods.PlaceOrder(db, 102, "продать", "ZZZ", 5, 100);

        Assert.True(result1.IsSuccess);
        Assert.True(result2.IsSuccess);
    }

    [Fact]
    public async Task ProcessOrdersAsync_SimpleLongOrder_ReturnsSuccess()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.RegisterTrader(db, 101);
        await HelpMethods.CreateToken(db, "ZZZ");
        var result = await HelpMethods.PlaceOrder(db, 101, "купить", "ZZZ", 5, 100);

        Assert.True(result.IsSuccess, $"Order failed: {result.ErrorMessage}");
    }

    [Fact]
    public async Task ProcessOrdersAsync_SimpleShortOrder_ReturnsSuccess()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.RegisterTrader(db, 101);
        await HelpMethods.CreateToken(db, "ZZZ");
        await HelpMethods.AddPortfolio(db, 101, "ZZZ", 10);
        var result = await HelpMethods.PlaceOrder(db, 101, "продать", "ZZZ", 5, 100);

        Assert.True(result.IsSuccess, $"Order failed: {result.ErrorMessage}");
    }

    [Fact]
    public async Task ProcessOrderAsync_ComplexExecutionLimitLongOrder_ReturnsSuccess()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.RegisterTrader(db, 101);
        await HelpMethods.RegisterTrader(db, 102);
        await HelpMethods.CreateToken(db, "ZZZ");
        await HelpMethods.AddPortfolio(db, 102, "ZZZ", 30);

        var result1 = await HelpMethods.PlaceOrder(db, 101, "купить", "ZZZ", 9, 90);
        var result2 = await HelpMethods.PlaceOrder(db, 102, "продать", "ZZZ", 3, 60);
        var result3 = await HelpMethods.PlaceOrder(db, 102, "продать", "ZZZ", 3, 80);
        var result4 = await HelpMethods.PlaceOrder(db, 102, "продать", "ZZZ", 3, 100);

        var trader1 = await HelpMethods.GetTrader(db, 101);
        var trader2 = await HelpMethods.GetTrader(db, 102);

        var portfolio1 = await HelpMethods.GetPortfolio(db, 101, "ZZZ");
        var portfolio2 = await HelpMethods.GetPortfolio(db, 102, "ZZZ");

        Assert.True(result1.IsSuccess);
        Assert.True(result2.IsSuccess);
        Assert.True(result3.IsSuccess);
        Assert.True(result4.IsSuccess);

        Assert.NotNull(trader1);
        Assert.NotNull(trader2);

        Assert.Equal(190, trader1.Balance);
        Assert.Equal(1540, trader2.Balance);

        Assert.Equal(6, portfolio1.Quantity);
        Assert.Equal(21, portfolio2.Quantity);
    }

    [Fact]
    public async Task ProcessOrderAsync_ComplexExecutionMarketLongOrder_ReturnsSuccess()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.RegisterTrader(db, 101);
        await HelpMethods.RegisterTrader(db, 102);
        await HelpMethods.CreateToken(db, "ZZZ");
        await HelpMethods.AddPortfolio(db, 102, "ZZZ", 30);

        var result1 = await HelpMethods.PlaceOrder(db, 102, "продать", "ZZZ", 3, 60);
        var result2 = await HelpMethods.PlaceOrder(db, 102, "продать", "ZZZ", 3, 80);
        var result3 = await HelpMethods.PlaceOrder(db, 102, "продать", "ZZZ", 3, 100);
        var result4 = await HelpMethods.PlaceOrder(db, 101, "купить", "ZZZ", 9, 90);

        var trader1 = await HelpMethods.GetTrader(db, 101);
        var trader2 = await HelpMethods.GetTrader(db, 102);

        var portfolio1 = await HelpMethods.GetPortfolio(db, 101, "ZZZ");
        var portfolio2 = await HelpMethods.GetPortfolio(db, 102, "ZZZ");

        Assert.True(result1.IsSuccess);
        Assert.True(result2.IsSuccess);
        Assert.True(result3.IsSuccess);
        Assert.True(result4.IsSuccess);

        Assert.NotNull(trader1);
        Assert.NotNull(trader2);

        Assert.Equal(310, trader1.Balance);
        Assert.Equal(1420, trader2.Balance);

        Assert.Equal(6, portfolio1.Quantity);
        Assert.Equal(21, portfolio2.Quantity);
    }

    [Fact]
    public async Task ProcessOrderAsync_IgnoreYourOrders_ReturnsSuccess()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.RegisterTrader(db, 101);
        await HelpMethods.CreateToken(db, "ZZZ");
        await HelpMethods.AddPortfolio(db, 101, "ZZZ", 10);

        var resultShortOrder = await HelpMethods.PlaceOrder(db, 101, "продать", "ZZZ", 5, 100);
        var resultLongOrder = await HelpMethods.PlaceOrder(db, 101, "купить", "ZZZ", 5, 100);

        var orders = await HelpMethods.GetTraderOrders(db, 101, "ZZZ",OrderStatus.Active);
        var trader = await HelpMethods.GetTrader(db, 101);
        var portfolio = await HelpMethods.GetPortfolio(db, 101, "ZZZ");

        Assert.Equal(2, orders.Length);
        Assert.Equal(500, trader.Balance);
        Assert.Equal(5, portfolio.Quantity);
    }
}
