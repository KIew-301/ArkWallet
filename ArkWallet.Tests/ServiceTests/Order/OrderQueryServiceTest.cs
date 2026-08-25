using ArkWallet.Application.Services.TradeOrderServices;
using ArkWallet.Domain.Entities;
using ArkWallet.Domain.ValueObjects;
using ArkWallet.Infrastructure.Data;
using ArkWallet.Tests.HelpTools;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ArkWallet.Tests.ServiceTests.Order;

public class OrderQueryServiceTest
{
    [Fact]
    public async Task GetTraderOrdersAsync_WhenNoOrders_ReturnsEmptyList()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.RegisterTrader(db, 1001);

        var logger = NullLogger<OrderQueryService>.Instance;
        var service = new OrderQueryService(db, logger);

        var result = await service.GetTraderOrdersAsync(1001);

        Assert.True(result.IsSuccess);
        Assert.True(result.TryGetData(out var data));
        Assert.Empty(data);
    }

    [Fact]
    public async Task GetTraderOrdersAsync_WhenOrdersExist_ReturnsAllOrders()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.RegisterTrader(db, 1001);
        await HelpMethods.CreateToken(db, "ZZZ", "Zero");
        await HelpMethods.AddPortfolio(db, 1001, "ZZZ", 10);
        await HelpMethods.PlaceOrder(db, 1001, "купить", "ZZZ", 5, 100);
        await HelpMethods.PlaceOrder(db, 1001, "продать", "ZZZ", 3, 200);

        var logger = NullLogger<OrderQueryService>.Instance;
        var service = new OrderQueryService(db, logger);

        var result = await service.GetTraderOrdersAsync(1001);

        Assert.True(result.IsSuccess);
        Assert.True(result.TryGetData(out var data));
        Assert.Equal(2, data.Count);
    }

    [Fact]
    public async Task GetTraderOrdersAsync_WhenOrdersExist_ReturnsCorrectOrderInfo()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.RegisterTrader(db, 1001);
        await HelpMethods.CreateToken(db, "ZZZ", "Zero");
        await HelpMethods.PlaceOrder(db, 1001, "купить", "ZZZ", 5, 100);

        var logger = NullLogger<OrderQueryService>.Instance;
        var service = new OrderQueryService(db, logger);

        var result = await service.GetTraderOrdersAsync(1001);

        Assert.True(result.IsSuccess);
        Assert.True(result.TryGetData(out var data));

        var orderInfo = data.First();
        Assert.Equal("ZZZ", orderInfo.Symbol);
        Assert.Equal("Zero", orderInfo.TokenName);
        Assert.Equal("Buy", orderInfo.Direction);
        Assert.Equal(5m, orderInfo.TotalQuantity);
        Assert.Equal(0m, orderInfo.FilledQuantity);
        Assert.Equal(0m, orderInfo.FillPercent);
        Assert.Equal(100m, orderInfo.Price);
        Assert.Equal("Active", orderInfo.Status);
    }

    [Fact]
    public async Task GetTraderOrdersAsync_FilledOrder_ReturnsCorrectFillPercentAndStatus()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.RegisterTrader(db, 1001);
        await HelpMethods.RegisterTrader(db, 1002);
        await HelpMethods.CreateToken(db, "ZZZ", "Zero");
        await HelpMethods.AddPortfolio(db, 1002, "ZZZ", 10);

        await HelpMethods.PlaceOrder(db, 1002, "продать", "ZZZ", 5, 100);
        await HelpMethods.PlaceOrder(db, 1001, "купить", "ZZZ", 5, 100);

        var logger = NullLogger<OrderQueryService>.Instance;
        var service = new OrderQueryService(db, logger);

        var result = await service.GetTraderOrdersAsync(1001);

        Assert.True(result.IsSuccess);
        Assert.True(result.TryGetData(out var data));

        var orderInfo = data.First();
        Assert.Equal(5m, orderInfo.TotalQuantity);
        Assert.Equal(5m, orderInfo.FilledQuantity);
        Assert.Equal(100m, orderInfo.FillPercent);
        Assert.Equal("Filled", orderInfo.Status);
    }

    [Fact]
    public async Task GetTraderOrdersAsync_PartiallyFilledOrder_ReturnsCorrectFillPercentAndStatus()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.RegisterTrader(db, 1001);
        await HelpMethods.RegisterTrader(db, 1002);
        await HelpMethods.CreateToken(db, "ZZZ", "Zero");
        await HelpMethods.AddPortfolio(db, 1002, "ZZZ", 10);

        await HelpMethods.PlaceOrder(db, 1002, "продать", "ZZZ", 3, 100);
        await HelpMethods.PlaceOrder(db, 1001, "купить", "ZZZ", 5, 100);

        var logger = NullLogger<OrderQueryService>.Instance;
        var service = new OrderQueryService(db, logger);

        var result = await service.GetTraderOrdersAsync(1001);

        Assert.True(result.IsSuccess);
        Assert.True(result.TryGetData(out var data));

        var orderInfo = data.First();
        Assert.Equal(5m, orderInfo.TotalQuantity);
        Assert.Equal(3m, orderInfo.FilledQuantity);
        Assert.Equal(60m, orderInfo.FillPercent);
        Assert.Equal("Active", orderInfo.Status);
    }

    [Fact]
    public async Task GetTraderOrdersAsync_OnlyActiveOrders_ReturnsOnlyActive()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.RegisterTrader(db, 1001);
        await HelpMethods.RegisterTrader(db, 1002);
        await HelpMethods.CreateToken(db, "ZZZ", "Zero");
        await HelpMethods.AddPortfolio(db, 1002, "ZZZ", 10);

        await HelpMethods.PlaceOrder(db, 1002, "продать", "ZZZ", 5, 100);
        await HelpMethods.PlaceOrder(db, 1001, "купить", "ZZZ", 5, 100);

        var logger = NullLogger<OrderQueryService>.Instance;
        var service = new OrderQueryService(db, logger);

        var result = await service.GetTraderOrdersAsync(1001, includeActive: true, includeFilled: false, includeCancelled: false);

        Assert.True(result.IsSuccess);
        Assert.True(result.TryGetData(out var data));
        Assert.Empty(data);
    }

    [Fact]
    public async Task GetTraderOrdersAsync_OnlyFilledOrders_ReturnsOnlyFilled()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.RegisterTrader(db, 1001);
        await HelpMethods.RegisterTrader(db, 1002);
        await HelpMethods.CreateToken(db, "ZZZ", "Zero");
        await HelpMethods.AddPortfolio(db, 1002, "ZZZ", 10);

        await HelpMethods.PlaceOrder(db, 1002, "продать", "ZZZ", 5, 100);
        await HelpMethods.PlaceOrder(db, 1001, "купить", "ZZZ", 5, 100);

        var logger = NullLogger<OrderQueryService>.Instance;
        var service = new OrderQueryService(db, logger);

        var result = await service.GetTraderOrdersAsync(1001, includeActive: false, includeFilled: true, includeCancelled: false);

        Assert.True(result.IsSuccess);
        Assert.True(result.TryGetData(out var data));
        Assert.Single(data);
        Assert.Equal("Filled", data.First().Status);
    }

    [Fact]
    public async Task GetTraderOrdersAsync_NoStatusesSelected_ReturnsEmptyList()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.RegisterTrader(db, 1001);
        await HelpMethods.CreateToken(db, "ZZZ", "Zero");
        await HelpMethods.PlaceOrder(db, 1001, "купить", "ZZZ", 5, 100);

        var logger = NullLogger<OrderQueryService>.Instance;
        var service = new OrderQueryService(db, logger);

        var result = await service.GetTraderOrdersAsync(1001, includeActive: false, includeFilled: false, includeCancelled: false);

        Assert.True(result.IsSuccess);
        Assert.True(result.TryGetData(out var data));
        Assert.Empty(data);
    }

    [Fact]
    public async Task GetTraderOrdersAsync_WithTokenInfo_ReturnsIconAndCurrentPrice()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.RegisterTrader(db, 1001);
        await HelpMethods.CreateToken(db, "ZZZ", "Zero", CharacterRarity.FourStar, 1000, 100m,  true, "image.png", "icon.png");
        await HelpMethods.PlaceOrder(db, 1001, "купить", "ZZZ", 5, 100);

        var logger = NullLogger<OrderQueryService>.Instance;
        var service = new OrderQueryService(db, logger);

        var result = await service.GetTraderOrdersAsync(1001, withTokenInfo: true);

        Assert.True(result.IsSuccess);
        Assert.True(result.TryGetData(out var data));

        var orderInfo = data.First();
        Assert.Equal("icon.png", orderInfo.IconUrl);
        Assert.Equal(100m, orderInfo.CurrentPrice);
    }

    [Fact]
    public async Task GetTraderOrdersAsync_WithoutTokenInfo_IconAndCurrentPriceAreNull()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.RegisterTrader(db, 1001);
        await HelpMethods.CreateToken(db, "ZZZ", "Zero", CharacterRarity.FourStar, 1000, 100m, true, "image.png", "icon.png");
        await HelpMethods.PlaceOrder(db, 1001, "купить", "ZZZ", 5, 100);

        var logger = NullLogger<OrderQueryService>.Instance;
        var service = new OrderQueryService(db, logger);

        var result = await service.GetTraderOrdersAsync(1001, withTokenInfo: false);

        Assert.True(result.IsSuccess);
        Assert.True(result.TryGetData(out var data));

        var orderInfo = data.First();
        Assert.Null(orderInfo.IconUrl);
        Assert.Null(orderInfo.CurrentPrice);
    }

    [Fact]
    public async Task GetTraderOrdersAsync_WithTokenInfoAndMultipleOrders_ReturnsAllWithTokenInfo()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.RegisterTrader(db, 1001);
        await HelpMethods.CreateToken(db, "ZZZ", "Zero", CharacterRarity.FourStar, 1000, 100m, true, "image1.png", "icon1.png");
        await HelpMethods.CreateToken(db, "YYY", "One", CharacterRarity.FiveStar, 500, 50m, true, "image2.png", "icon2.png");
        await HelpMethods.AddPortfolio(db, 1001, "ZZZ", 10);
        await HelpMethods.AddPortfolio(db, 1001, "YYY", 10);
        await HelpMethods.PlaceOrder(db, 1001, "купить", "ZZZ", 5, 100);
        await HelpMethods.PlaceOrder(db, 1001, "продать", "YYY", 3, 200);

        var logger = NullLogger<OrderQueryService>.Instance;
        var service = new OrderQueryService(db, logger);

        var result = await service.GetTraderOrdersAsync(1001, withTokenInfo: true);

        Assert.True(result.IsSuccess);
        Assert.True(result.TryGetData(out var data));
        Assert.Equal(2, data.Count);

        foreach (var orderInfo in data)
        {
            Assert.NotNull(orderInfo.IconUrl);
            Assert.NotNull(orderInfo.CurrentPrice);
        }
    }
}