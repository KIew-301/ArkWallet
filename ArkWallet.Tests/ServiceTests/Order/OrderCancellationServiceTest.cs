using ArkWallet.Tests.HelpTools;

namespace ArkWallet.Tests.ServiceTests.Order;

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
        var result2 = await HelpMethods.CancelOrder(db, 101, result1);

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
        var result2 = await HelpMethods.CancelOrder(db, 101, result1);

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

        var resultShortOrder1 = await HelpMethods.PlaceOrder(db, 102, "продать", "ZZZ", 3, 60);
        var resultShortOrder2 = await HelpMethods.PlaceOrder(db, 102, "продать", "ZZZ", 3, 80);
        var resultLongOrder = await HelpMethods.PlaceOrder(db, 101, "купить", "ZZZ", 9, 90);
        var resultCancelLongOrder = await HelpMethods.CancelOrder(db, 101, resultLongOrder);

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

        var resultShortOrder1 = await HelpMethods.PlaceOrder(db, 102, "продать", "ZZZ", 3, 60);
        var resultShortOrder2 = await HelpMethods.PlaceOrder(db, 102, "продать", "ZZZ", 7, 80);
        var resultShortOrder3 = await HelpMethods.PlaceOrder(db, 102, "продать", "ZZZ", 10, 100);
        var resultLongOrder = await HelpMethods.PlaceOrder(db, 101, "купить", "ZZZ", 9, 90);
        var resultCancelShortOrder = await HelpMethods.CancelOrder(db, 102, resultShortOrder3);

        var trader = await HelpMethods.GetTrader(db, 102);
        var portfolio = await HelpMethods.GetPortfolio(db, 102, "ZZZ");

        Assert.Equal(1660, trader.Balance);
        Assert.Equal(20, portfolio.Quantity);
    }

    [Fact]
    public async Task ProcessOrderAsync_CancelFilledOrders_ReturnsFail()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.RegisterTrader(db, 101);
        await HelpMethods.RegisterTrader(db, 102);
        await HelpMethods.CreateToken(db, "ZZZ");
        await HelpMethods.AddPortfolio(db, 102, "ZZZ", 5);

        var resultShortOrder = await HelpMethods.PlaceOrder(db, 102, "продать", "ZZZ", 3, 100);
        var resultLongOrder = await HelpMethods.PlaceOrder(db, 101, "купить", "ZZZ", 3, 100);
        var resultCancelShortOrder = await HelpMethods.CancelOrder(db, 102, resultShortOrder);

        var traderSorter = await HelpMethods.GetTrader(db, 102);
        var portfolioShorter = await HelpMethods.GetPortfolio(db, 102, "ZZZ");
        var traderLonger = await HelpMethods.GetTrader(db, 101);
        var portfolioLonger = await HelpMethods.GetPortfolio(db, 101, "ZZZ");

        Assert.False(resultCancelShortOrder.IsSuccess);
        Assert.Equal("ћожно отменить только активный ордер", resultCancelShortOrder.Message);
    }

    [Fact]
    public async Task ProcessOrderAsync_CancelAllOrders_ReturnsSuccess()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.RegisterTrader(db, 101);
        await HelpMethods.CreateToken(db, "ZZZ");
        await HelpMethods.AddPortfolio(db, 101, "ZZZ", 5);

        await HelpMethods.PlaceOrder(db, 101, "продать", "ZZZ", 3, 200);
        await HelpMethods.PlaceOrder(db, 101, "купить", "ZZZ", 3, 150);
        await HelpMethods.PlaceOrder(db, 101, "купить", "ZZZ", 3, 100);
        var result = await HelpMethods.CancelAllOrders(db, 101);

        var orders = await HelpMethods.GetTraderOrders(db, 101);

        Assert.True(result.IsSuccess);
        Assert.Empty(orders);
    }

    [Fact]
    public async Task ProcessOrderAsync_CancelAllOrdersWithoutActiveOrders_ReturnsFail()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.RegisterTrader(db, 101);
        var result = await HelpMethods.CancelAllOrders(db, 101);

        var orders = await HelpMethods.GetTraderOrders(db, 101);

        Assert.False(result.IsSuccess);
        Assert.Equal("Ќет активных ордеров дл€ отмены", result.Message);
    }

    [Fact]
    public async Task CancelOrderAsync_OrderNotExist_RetursFail()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.RegisterTrader(db, 101);
        await HelpMethods.CreateToken(db, "ZZZ");

        var result = await HelpMethods.CancelOrder(db, 101, "");

        Assert.False(result.IsSuccess);
        Assert.Equal("ќрдера не существует", result.Message);
    }

    [Fact]
    public async Task CancelOrderAsync_TraderNotExist_RetursFail()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.CreateToken(db, "ZZZ");

        var result = await HelpMethods.CancelOrder(db, 101, "");

        Assert.False(result.IsSuccess);
        Assert.Equal("“рейдер не найден", result.Message);
    }


    [Fact]
    public async Task CancelAllOrders_CancelBuyOrders_RestoresBalance()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.RegisterTrader(db, 101);
        await HelpMethods.CreateToken(db, "ZZZ");

        await HelpMethods.PlaceOrder(db, 101, "купить", "ZZZ", 3, 100);
        await HelpMethods.PlaceOrder(db, 101, "купить", "ZZZ", 2, 200);
        var result = await HelpMethods.CancelAllOrders(db, 101);

        var trader = await HelpMethods.GetTrader(db, 101);

        Assert.True(result.IsSuccess);
        Assert.Equal(1000, trader.Balance);
    }

    [Fact]
    public async Task CancelAllOrders_CancelSellOrders_RestoresTokens()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.RegisterTrader(db, 101);
        await HelpMethods.CreateToken(db, "ZZZ");
        await HelpMethods.AddPortfolio(db, 101, "ZZZ", 10);

        await HelpMethods.PlaceOrder(db, 101, "продать", "ZZZ", 3, 100);
        await HelpMethods.PlaceOrder(db, 101, "продать", "ZZZ", 2, 200);
        var result = await HelpMethods.CancelAllOrders(db, 101);

        var trader = await HelpMethods.GetTrader(db, 101);
        var portfolio = await HelpMethods.GetPortfolio(db, 101);

        Assert.True(result.IsSuccess);
        Assert.Equal(1000, trader.Balance);
        Assert.Equal(10, portfolio.Quantity);
    }

}