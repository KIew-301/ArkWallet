using ArkWallet.Application.Common;
using ArkWallet.Application.Contracts.CharacterTokenServices;
using ArkWallet.Domain.ValueObjects;
using ArkWallet.Tests.HelpTools;
using Microsoft.EntityFrameworkCore;
using Moq;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace ArkWallet.Tests.ServiceTests;
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

        var orders1 = await HelpMethods.GetTraderOrders(db, 101, "ZZZ", OrderStatus.Filled);
        var orders2 = await HelpMethods.GetTraderOrders(db, 102, "ZZZ", OrderStatus.Filled);

        Assert.True(result1.IsSuccess);
        Assert.True(result2.IsSuccess);

        Assert.Single(orders1);
        Assert.Single(orders2);
    }

    [Fact]
    public async Task ProcessOrdersAsync_SimpleLongOrder_ReturnsSuccess()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.RegisterTrader(db, 101);
        await HelpMethods.CreateToken(db, "ZZZ");
        var result = await HelpMethods.PlaceOrder(db, 101, "купить", "ZZZ", 5, 100);

        Assert.True(result.IsSuccess, $"Order failed: {result.Message}");
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

        Assert.True(result.IsSuccess, $"Order failed: {result.Message}");
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

        var filledOrders1 = await HelpMethods.GetTraderOrders(db, 101, "ZZZ", OrderStatus.Filled);
        var filledOrders2 = await HelpMethods.GetTraderOrders(db, 102, "ZZZ", OrderStatus.Filled);

        var activeOrders1 = await HelpMethods.GetTraderOrders(db, 101, "ZZZ", OrderStatus.Active);
        var activeOrders2 = await HelpMethods.GetTraderOrders(db, 102, "ZZZ", OrderStatus.Active);

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

        Assert.Single(activeOrders1);
        Assert.Single(activeOrders2);

        Assert.Empty(filledOrders1);
        Assert.Equal(2, filledOrders2.Length);

        Assert.Equal(9, activeOrders1[0].Quantity);
        Assert.Equal(6, activeOrders1[0].FilledQuantity);
        Assert.Equal(3, activeOrders2[0].Quantity);
        Assert.Equal(0, activeOrders2[0].FilledQuantity);
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

        var filledOrders1 = await HelpMethods.GetTraderOrders(db, 101, "ZZZ", OrderStatus.Filled);
        var filledOrders2 = await HelpMethods.GetTraderOrders(db, 102, "ZZZ", OrderStatus.Filled);

        var activeOrders1 = await HelpMethods.GetTraderOrders(db, 101, "ZZZ", OrderStatus.Active);
        var activeOrders2 = await HelpMethods.GetTraderOrders(db, 102, "ZZZ", OrderStatus.Active);

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

        Assert.Single(activeOrders1);
        Assert.Single(activeOrders2);

        Assert.Empty(filledOrders1);
        Assert.Equal(2, filledOrders2.Length);

        Assert.Equal(9, activeOrders1[0].Quantity);
        Assert.Equal(6, activeOrders1[0].FilledQuantity);
        Assert.Equal(3, activeOrders2[0].Quantity);
        Assert.Equal(0, activeOrders2[0].FilledQuantity);
    }

    [Fact]
    public async Task ProcessOrderAsync_FullMatchingTest_ReturnsSuccess()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.RegisterTrader(db, 101); // 1 - 1000, 0
        await HelpMethods.RegisterTrader(db, 102); // 2 - 1000, 0
        await HelpMethods.RegisterTrader(db, 103); // 3 - 1000, 0

        await HelpMethods.CreateToken(db, "ZZZ");
        await HelpMethods.AddPortfolio(db, 102, "ZZZ", 5);  // 2 - 1000, 5
        await HelpMethods.AddPortfolio(db, 103, "ZZZ", 50); // 2 - 1000, 50
        await HelpMethods.GiveMoney(db, 101, 1000);         // 2 - 2000, 0

        await HelpMethods.PlaceOrder(db, 101, "купить", "ZZZ", 5, 50);      // 1 - 1750, 0 | 2 - 1000, 5 | 3 - 1000, 50 - первый 
        await HelpMethods.PlaceOrder(db, 102, "купить", "ZZZ", 10, 60);     // 1 - 1750, 0 | 2 - 400, 5 | 3 - 1000, 50 - второй /матчится 3-им полностью
        await HelpMethods.PlaceOrder(db, 103, "продать", "ZZZ", 10, 30);    // 1 - 1750, 0 | 2 - 400, 15 | 3 - 1600, 40 - третий /матчится 2-ым полностью
        await HelpMethods.PlaceOrder(db, 102, "купить", "ZZZ", 2, 90);      // 1 - 1750, 0 | 2 - 220, 15 | 3 - 1600, 40 - четвёртый /матчится 8-ым полностью
        await HelpMethods.PlaceOrder(db, 101, "продать", "ZZZ", 100, 150);  // Error
        await HelpMethods.PlaceOrder(db, 103, "продать", "ZZZ", 5, 150);    // 1 - 1750, 0 | 2 - 220, 15 | 3 - 1600, 35 - пятый /матчится седьмым полностью
        await HelpMethods.PlaceOrder(db, 102, "продать", "ZZZ", 5, 150);    // 1 - 1750, 0 | 2 - 220, 10 | 3 - 1600, 35 - шестой /не матчится
        await HelpMethods.PlaceOrder(db, 101, "купить", "ZZZ", 2, 15000);   // Error
        await HelpMethods.PlaceOrder(db, 101, "купить", "ZZZ", 2, 150);     // 1 - 1450, 2 | 2 - 220, 10 | 3 - 1900, 35 - седьмой /матчится вторым полностью
        await HelpMethods.PlaceOrder(db, 103, "продать", "ZZZ", 30, 10);    // 1 - 1450, 7 | 2 - 220, 12 | 3 - 2330, 5 - восьмой /матчится 4-ым на 2

        var trader1 = await HelpMethods.GetTrader(db, 101);
        var trader2 = await HelpMethods.GetTrader(db, 102);
        var trader3 = await HelpMethods.GetTrader(db, 103);

        var portfolio1 = await HelpMethods.GetPortfolio(db, 101, "ZZZ");
        var portfolio2 = await HelpMethods.GetPortfolio(db, 102, "ZZZ");
        var portfolio3 = await HelpMethods.GetPortfolio(db, 103, "ZZZ");

        var filledOrders1 = await HelpMethods.GetTraderOrders(db, 101, "ZZZ", OrderStatus.Filled);
        var filledOrders2 = await HelpMethods.GetTraderOrders(db, 102, "ZZZ", OrderStatus.Filled);
        var filledOrders3 = await HelpMethods.GetTraderOrders(db, 103, "ZZZ", OrderStatus.Filled);

        var activeOrders1 = await HelpMethods.GetTraderOrders(db, 101, "ZZZ", OrderStatus.Active);
        var activeOrders2 = await HelpMethods.GetTraderOrders(db, 102, "ZZZ", OrderStatus.Active);
        var activeOrders3 = await HelpMethods.GetTraderOrders(db, 103, "ZZZ", OrderStatus.Active);

        Assert.NotNull(trader1);
        Assert.NotNull(trader2);
        Assert.NotNull(trader3);

        Assert.Equal(1450, trader1.Balance);
        Assert.Equal(220, trader2.Balance);
        Assert.Equal(2330, trader3.Balance);

        // Портфель 101
        Assert.Equal(7, portfolio1.Quantity);
        Assert.Equal(78.57m, portfolio1.AverageBuyPrice, 2);
        Assert.Equal(0, portfolio1.ReserveQuantity);
        Assert.Equal(0, portfolio1.AverageReservePrice);
        Assert.Equal(0, portfolio1.SellingQuantity);
        Assert.Equal(0, portfolio1.AverageSellPrice);

        // Портфель 102
        Assert.Equal(12, portfolio2.Quantity);
        Assert.Equal(2826.11m, portfolio2.AverageBuyPrice, 2);
        Assert.Equal(5, portfolio2.ReserveQuantity);
        Assert.Equal(150m, portfolio2.AverageReservePrice);
        Assert.Equal(0, portfolio2.SellingQuantity);
        Assert.Equal(0, portfolio2.AverageSellPrice);

        // Портфель 103
        Assert.Equal(5, portfolio3.Quantity);
        Assert.Equal(10000m, portfolio3.AverageBuyPrice, 2);
        Assert.Equal(26, portfolio3.ReserveQuantity);
        Assert.Equal(22.73m, portfolio3.AverageReservePrice, 2);
        Assert.Equal(19, portfolio3.SellingQuantity);
        Assert.Equal(70.00m, portfolio3.AverageSellPrice, 2);

        Assert.Empty(activeOrders1);
        Assert.Single(activeOrders2);
        Assert.Equal(2, activeOrders3.Length);

        Assert.Equal(2, filledOrders1.Length);
        Assert.Equal(2, filledOrders2.Length);
        Assert.Single(filledOrders3);
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

        var orders = await HelpMethods.GetTraderOrders(db, 101, "ZZZ", OrderStatus.Active);
        var trader = await HelpMethods.GetTrader(db, 101);
        var portfolio = await HelpMethods.GetPortfolio(db, 101, "ZZZ");

        Assert.Equal(2, orders.Length);
        Assert.Equal(500, trader.Balance);
        Assert.Equal(5, portfolio.Quantity);
    }

    [Fact]
    public async Task CreateOrderAsync_WhenPriceUpdateFails_ReturnsFail()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.RegisterTrader(db, 101);
        await HelpMethods.RegisterTrader(db, 102);
        await HelpMethods.CreateToken(db, "ZZZ");
        await HelpMethods.AddPortfolio(db, 102, "ZZZ", 10);

        var mockTokenPriceCandleUpdateService = new Mock<ITokenPriceCandleUpdateService>();
        mockTokenPriceCandleUpdateService
            .Setup(x => x.UpdateTokenPriceCandleAsync(It.IsAny<string>(), It.IsAny<decimal>()))
            .ReturnsAsync(Result.Fail("Ошибка обновления цены"));

        await HelpMethods.PlaceOrder(
            db, 102, "продать", "ZZZ", 5, 100,
            tokenPriceCandleUpdateService: mockTokenPriceCandleUpdateService.Object);
        var result = await HelpMethods.PlaceOrder(
            db, 101, "купить", "ZZZ", 5, 100,
            tokenPriceCandleUpdateService: mockTokenPriceCandleUpdateService.Object);

        Assert.False(result.IsSuccess);
        Assert.Equal("Ошибка обновления цены", result.Message);
    }

    [Fact]
    public async Task CreateOrderAsync_WhenPriceUpdateSuccess_UpdatesPriceByLastTrade()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.RegisterTrader(db, 101);
        await HelpMethods.RegisterTrader(db, 102);
        await HelpMethods.CreateToken(db, "ZZZ");
        await HelpMethods.AddPortfolio(db, 102, "ZZZ", 10);

        var mockTokenPriceCandleUpdateService = new Mock<ITokenPriceCandleUpdateService>();
        mockTokenPriceCandleUpdateService
            .Setup(x => x.UpdateTokenPriceCandleAsync(It.IsAny<string>(), It.IsAny<decimal>()))
            .ReturnsAsync(Result.Ok());

        var result = await HelpMethods.PlaceOrder(
            db, 101, "купить", "ZZZ", 5, 100,
            tokenPriceCandleUpdateService: mockTokenPriceCandleUpdateService.Object);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task CreateOrderAsync_WhenNoTrades_DoesNotUpdatePrice()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.RegisterTrader(db, 101);
        await HelpMethods.CreateToken(db, "ZZZ");

        var mockTokenPriceCandleUpdateService = new Mock<ITokenPriceCandleUpdateService>();

        var result = await HelpMethods.PlaceOrder(
            db, 101, "купить", "ZZZ", 5, 100,
            tokenPriceCandleUpdateService: mockTokenPriceCandleUpdateService.Object);

        Assert.True(result.IsSuccess);

        mockTokenPriceCandleUpdateService.Verify(
            x => x.UpdateTokenPriceCandleAsync(It.IsAny<string>(), It.IsAny<decimal>()),
            Times.Never);
    }

    [Fact]
    public async Task CreateOrderAsync_WithMultipleTrades_UpdatesPriceByLastTrade()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.RegisterTrader(db, 101);
        await HelpMethods.RegisterTrader(db, 102);
        await HelpMethods.RegisterTrader(db, 103);
        await HelpMethods.CreateToken(db, "ZZZ");
        await HelpMethods.AddPortfolio(db, 102, "ZZZ", 10);
        await HelpMethods.AddPortfolio(db, 103, "ZZZ", 10);

        decimal? lastUpdatedPrice = null;

        var mockTokenPriceCandleUpdateService = new Mock<ITokenPriceCandleUpdateService>();
        mockTokenPriceCandleUpdateService
            .Setup(x => x.UpdateTokenPriceCandleAsync(It.IsAny<string>(), It.IsAny<decimal>()))
            .Callback<string, decimal>((symbol, price) =>
            {
                lastUpdatedPrice = price;
            })
            .ReturnsAsync(Result.Ok());

        await HelpMethods.PlaceOrder(
            db, 101, "купить", "ZZZ", 5, 115,
            tokenPriceCandleUpdateService: mockTokenPriceCandleUpdateService.Object);
        await HelpMethods.PlaceOrder(
            db, 102, "купить", "ZZZ", 5, 110,
            tokenPriceCandleUpdateService: mockTokenPriceCandleUpdateService.Object);
        await HelpMethods.PlaceOrder(
            db, 103, "продать", "ZZZ", 5, 90,
            tokenPriceCandleUpdateService: mockTokenPriceCandleUpdateService.Object);
        await HelpMethods.PlaceOrder(
            db, 101, "продать", "ZZZ", 5, 120,
            tokenPriceCandleUpdateService: mockTokenPriceCandleUpdateService.Object);
        await HelpMethods.PlaceOrder(
            db, 101, "купить", "ZZZ", 5, 60,
            tokenPriceCandleUpdateService: mockTokenPriceCandleUpdateService.Object);
        await HelpMethods.PlaceOrder(
            db, 103, "продать", "ZZZ", 5, 50,
            tokenPriceCandleUpdateService: mockTokenPriceCandleUpdateService.Object);

        Assert.Equal(110m, lastUpdatedPrice);

        mockTokenPriceCandleUpdateService.Verify(
            x => x.UpdateTokenPriceCandleAsync("ZZZ", It.IsAny<decimal>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task ProcessOrderAsync_CalculatesAverageExecutionPrice_ForMultipleMatches()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.RegisterTrader(db, 101, "Buyer");
        await HelpMethods.RegisterTrader(db, 102, "Seller1");
        await HelpMethods.RegisterTrader(db, 103, "Seller2");
        await HelpMethods.RegisterTrader(db, 104, "Seller3");
        await HelpMethods.CreateToken(db, "ZZZ");
        await HelpMethods.AddPortfolio(db, 102, "ZZZ", 10);
        await HelpMethods.AddPortfolio(db, 103, "ZZZ", 10);
        await HelpMethods.AddPortfolio(db, 104, "ZZZ", 10);
        await HelpMethods.GiveMoney(db, 101, 10000);

        await HelpMethods.PlaceOrder(db, 102, "продать", "ZZZ", 3, 90);
        await HelpMethods.PlaceOrder(db, 103, "продать", "ZZZ", 4, 95);
        await HelpMethods.PlaceOrder(db, 104, "продать", "ZZZ", 5, 100);

        var result = await HelpMethods.PlaceOrder(db, 101, "купить", "ZZZ", 10, 100);

        Assert.True(result.TryGetData(out var data));

        var order = await db.TradeOrders.FirstOrDefaultAsync(o => o.Id == data.Order.Id);
        Assert.NotNull(order);
        Assert.Equal(95m, order.AverageExecutePrice);
    }

    [Fact]
    public async Task ProcessOrderAsync_CalculatesAverageExecutionPrice_ForPartialFill()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.RegisterTrader(db, 101, "Buyer");
        await HelpMethods.RegisterTrader(db, 102, "Seller1");
        await HelpMethods.RegisterTrader(db, 103, "Seller2");
        await HelpMethods.CreateToken(db, "ZZZ");
        await HelpMethods.AddPortfolio(db, 102, "ZZZ", 10);
        await HelpMethods.AddPortfolio(db, 103, "ZZZ", 10);
        await HelpMethods.GiveMoney(db, 101, 10000);

        await HelpMethods.PlaceOrder(db, 102, "продать", "ZZZ", 3, 90);
        await HelpMethods.PlaceOrder(db, 103, "продать", "ZZZ", 4, 95);

        var result = await HelpMethods.PlaceOrder(db, 101, "купить", "ZZZ", 5, 100);

        Assert.True(result.TryGetData(out var data));

        var order = await db.TradeOrders.FirstOrDefaultAsync(o => o.Id == data.Order.Id);
        Assert.NotNull(order);
        Assert.Equal(92m, order.AverageExecutePrice);
    }
}
