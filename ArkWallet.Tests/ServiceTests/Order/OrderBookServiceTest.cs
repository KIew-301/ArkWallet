using ArkWallet.Application.Services.TradeOrderServices;
using ArkWallet.Tests.HelpTools;
using Microsoft.Extensions.Logging.Abstractions;

namespace ArkWallet.Tests.ServiceTests.Order;

public class OrderBookServiceTest
{
    [Fact]
    public async Task GetOrderBookAsync_EmptySymbol_ReturnsFail()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        var service = new OrderBookService(db, NullLogger<OrderBookService>.Instance);

        var result = await service.GetOrderBookAsync("", 10, 10);

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task GetOrderBookAsync_ZeroBuyOrders_ReturnsFail()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.CreateToken(db, "ZZZ");

        var service = new OrderBookService(db, NullLogger<OrderBookService>.Instance);

        var result = await service.GetOrderBookAsync("ZZZ", 0, 10);

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task GetOrderBookAsync_ZeroSellOrders_ReturnsFail()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.CreateToken(db, "ZZZ");

        var service = new OrderBookService(db, NullLogger<OrderBookService>.Instance);

        var result = await service.GetOrderBookAsync("ZZZ", 10, 0);

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task GetOrderBookAsync_NoOrders_ReturnsEmptyBook()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.CreateToken(db, "ZZZ");

        var service = new OrderBookService(db, NullLogger<OrderBookService>.Instance);

        var result = await service.GetOrderBookAsync("ZZZ", 10, 10);

        Assert.True(result.IsSuccess);
        Assert.True(result.TryGetData(out var data));
        Assert.Equal("ZZZ", data.Symbol);
        Assert.Equal(0m, data.BestBid);
        Assert.Equal(0m, data.BestAsk);
        Assert.Equal(0m, data.Spread);
        Assert.Empty(data.Bids);
        Assert.Empty(data.Asks);
    }

    [Fact]
    public async Task GetOrderBookAsync_WithBuyOrders_ReturnsBidsSortedDescending()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.RegisterTrader(db, 101);
        await HelpMethods.CreateToken(db, "ZZZ");
        await HelpMethods.GiveMoney(db, 101, 10_000_000m);

        await HelpMethods.PlaceOrder(db, 101, "Купить", "ZZZ", 5, 100);
        await HelpMethods.PlaceOrder(db, 101, "Купить", "ZZZ", 3, 110);
        await HelpMethods.PlaceOrder(db, 101, "Купить", "ZZZ", 7, 90);

        var service = new OrderBookService(db, NullLogger<OrderBookService>.Instance);

        var result = await service.GetOrderBookAsync("ZZZ", 10, 10);

        Assert.True(result.IsSuccess);
        Assert.True(result.TryGetData(out var data));
        Assert.Equal(3, data.Bids.Count);
        Assert.Equal(110m, data.Bids[0].Price);
        Assert.Equal(100m, data.Bids[1].Price);
        Assert.Equal(90m, data.Bids[2].Price);
        Assert.Empty(data.Asks);
    }

    [Fact]
    public async Task GetOrderBookAsync_WithSellOrders_ReturnsAsksSortedAscending()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.RegisterTrader(db, 101);
        await HelpMethods.CreateToken(db, "ZZZ");
        await HelpMethods.AddPortfolio(db, 101, "ZZZ", 100);

        await HelpMethods.PlaceOrder(db, 101, "Продать", "ZZZ", 5, 200);
        await HelpMethods.PlaceOrder(db, 101, "Продать", "ZZZ", 3, 180);
        await HelpMethods.PlaceOrder(db, 101, "Продать", "ZZZ", 7, 210);

        var service = new OrderBookService(db, NullLogger<OrderBookService>.Instance);

        var result = await service.GetOrderBookAsync("ZZZ", 10, 10);

        Assert.True(result.IsSuccess);
        Assert.True(result.TryGetData(out var data));
        Assert.Empty(data.Bids);
        Assert.Equal(3, data.Asks.Count);
        Assert.Equal(180m, data.Asks[0].Price);
        Assert.Equal(200m, data.Asks[1].Price);
        Assert.Equal(210m, data.Asks[2].Price);
    }

    [Fact]
    public async Task GetOrderBookAsync_MixedOrders_CalculatesSpreadCorrectly()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.RegisterTrader(db, 101);
        await HelpMethods.CreateToken(db, "ZZZ");
        await HelpMethods.GiveMoney(db, 101, 10_000_000m);
        await HelpMethods.AddPortfolio(db, 101, "ZZZ", 100);

        await HelpMethods.PlaceOrder(db, 101, "Купить", "ZZZ", 5, 100);
        await HelpMethods.PlaceOrder(db, 101, "Продать", "ZZZ", 3, 105);

        var service = new OrderBookService(db, NullLogger<OrderBookService>.Instance);

        var result = await service.GetOrderBookAsync("ZZZ", 10, 10);

        Assert.True(result.IsSuccess);
        Assert.True(result.TryGetData(out var data));
        Assert.Equal(100m, data.BestBid);
        Assert.Equal(105m, data.BestAsk);
        Assert.Equal(5m, data.Spread);
    }

    [Fact]
    public async Task GetOrderBookAsync_LimitsBuyOrdersToRequestedCount()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.RegisterTrader(db, 101);
        await HelpMethods.CreateToken(db, "ZZZ");
        await HelpMethods.GiveMoney(db, 101, 10_000_000m);

        await HelpMethods.PlaceOrder(db, 101, "Купить", "ZZZ", 1, 100);
        await HelpMethods.PlaceOrder(db, 101, "Купить", "ZZZ", 1, 110);
        await HelpMethods.PlaceOrder(db, 101, "Купить", "ZZZ", 1, 120);

        var service = new OrderBookService(db, NullLogger<OrderBookService>.Instance);

        var result = await service.GetOrderBookAsync("ZZZ", 2, 10);

        Assert.True(result.IsSuccess);
        Assert.True(result.TryGetData(out var data));
        Assert.Equal(2, data.Bids.Count);
        Assert.Equal(120m, data.Bids[0].Price);
        Assert.Equal(110m, data.Bids[1].Price);
    }

    [Fact]
    public async Task GetOrderBookAsync_LimitsSellOrdersToRequestedCount()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.RegisterTrader(db, 101);
        await HelpMethods.CreateToken(db, "ZZZ");
        await HelpMethods.AddPortfolio(db, 101, "ZZZ", 100);

        await HelpMethods.PlaceOrder(db, 101, "Продать", "ZZZ", 1, 100);
        await HelpMethods.PlaceOrder(db, 101, "Продать", "ZZZ", 1, 110);
        await HelpMethods.PlaceOrder(db, 101, "Продать", "ZZZ", 1, 120);

        var service = new OrderBookService(db, NullLogger<OrderBookService>.Instance);

        var result = await service.GetOrderBookAsync("ZZZ", 10, 2);

        Assert.True(result.IsSuccess);
        Assert.True(result.TryGetData(out var data));
        Assert.Equal(2, data.Asks.Count);
        Assert.Equal(100m, data.Asks[0].Price);
        Assert.Equal(110m, data.Asks[1].Price);
    }

    [Fact]
    public async Task GetOrderBookAsync_EntryHasCorrectTotalCost()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.RegisterTrader(db, 101);
        await HelpMethods.CreateToken(db, "ZZZ");
        await HelpMethods.GiveMoney(db, 101, 10_000_000m);

        await HelpMethods.PlaceOrder(db, 101, "Купить", "ZZZ", 10, 100);

        var service = new OrderBookService(db, NullLogger<OrderBookService>.Instance);

        var result = await service.GetOrderBookAsync("ZZZ", 10, 10);

        Assert.True(result.IsSuccess);
        Assert.True(result.TryGetData(out var data));
        var entry = data.Bids[0];
        Assert.Equal("Buy", entry.Side);
        Assert.Equal(100m, entry.Price);
        Assert.Equal(10, entry.Quantity);
        Assert.Equal(1000m, entry.TotalCost);
    }

    [Fact]
    public async Task GetOrderBookAsync_IgnoresFilledAndCancelledOrders()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.RegisterTrader(db, 101);
        await HelpMethods.CreateToken(db, "ZZZ");
        await HelpMethods.GiveMoney(db, 101, 10_000_000m);

        var order1 = await HelpMethods.PlaceOrder(db, 101, "Купить", "ZZZ", 5, 100);
        var order2 = await HelpMethods.PlaceOrder(db, 101, "Купить", "ZZZ", 5, 200);
        await HelpMethods.CancelOrder(db, 101, order2);

        var service = new OrderBookService(db, NullLogger<OrderBookService>.Instance);

        var result = await service.GetOrderBookAsync("ZZZ", 10, 10);

        Assert.True(result.IsSuccess);
        Assert.True(result.TryGetData(out var data));
        Assert.Single(data.Bids);
        Assert.Equal(100m, data.Bids[0].Price);
    }

    [Fact]
    public async Task GetOrderBookAsync_CaseInsensitiveSymbol()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.RegisterTrader(db, 101);
        await HelpMethods.CreateToken(db, "ZZZ");
        await HelpMethods.GiveMoney(db, 101, 10_000_000m);

        await HelpMethods.PlaceOrder(db, 101, "Купить", "ZZZ", 5, 100);

        var service = new OrderBookService(db, NullLogger<OrderBookService>.Instance);

        var result = await service.GetOrderBookAsync("zzz", 10, 10);

        Assert.True(result.IsSuccess);
        Assert.True(result.TryGetData(out var data));
        Assert.Single(data.Bids);
    }

    [Fact]
    public async Task GetOrderBookAsync_NonExistentSymbol_ReturnsFail()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        var service = new OrderBookService(db, NullLogger<OrderBookService>.Instance);

        var result = await service.GetOrderBookAsync("NONEXISTENT", 10, 10);

        Assert.False(result.IsSuccess);
        Assert.Contains("Токена не существует", result.Message);
    }
}
