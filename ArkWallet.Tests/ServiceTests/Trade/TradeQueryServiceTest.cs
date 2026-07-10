using ArkWallet.Application.Common;
using ArkWallet.Application.Contracts.TradeServices;
using ArkWallet.Application.Services.TradeServices;
using ArkWallet.Domain.Entities;
using ArkWallet.Domain.ValueObjects;
using ArkWallet.Infrastructure.Data;
using ArkWallet.Tests.HelpTools;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ArkWallet.Tests.ServiceTests.Trade;

public class TradeQueryServiceTest
{
    [Fact]
    public async Task GetTraderTradesAsync_WhenNoTrades_ReturnsEmptyList()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.RegisterTrader(db, 101);

        var logger = NullLogger<TradeQueryService>.Instance;
        var service = new TradeQueryService(db, logger);

        var result = await service.GetTraderTradesAsync(101);

        Assert.True(result.IsSuccess);
        Assert.True(result.TryGetData(out var data));
        Assert.Empty(data);
    }

    [Fact]
    public async Task GetTraderTradesAsync_WhenTradesExist_ReturnsAllTrades()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.RegisterTrader(db, 101);
        await HelpMethods.RegisterTrader(db, 102);
        await HelpMethods.CreateToken(db, "ZZZ", "Zero");
        await HelpMethods.AddPortfolio(db, 102, "ZZZ", 10);

        await HelpMethods.PlaceOrder(db, 102, "продать", "ZZZ", 5, 100);
        await HelpMethods.PlaceOrder(db, 101, "купить", "ZZZ", 5, 100);

        var logger = NullLogger<TradeQueryService>.Instance;
        var service = new TradeQueryService(db, logger);

        var result = await service.GetTraderTradesAsync(101);

        Assert.True(result.IsSuccess);
        Assert.True(result.TryGetData(out var data));
        Assert.Single(data);
    }

    [Fact]
    public async Task GetTraderTradesAsync_AsBuyer_ReturnsCorrectProfit()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.RegisterTrader(db, 101);
        await HelpMethods.RegisterTrader(db, 102);
        await HelpMethods.CreateToken(db, "ZZZ", "Zero");
        await HelpMethods.AddPortfolio(db, 102, "ZZZ", 10);

        await HelpMethods.PlaceOrder(db, 102, "продать", "ZZZ", 5, 100);
        await HelpMethods.PlaceOrder(db, 101, "купить", "ZZZ", 5, 100);

        var logger = NullLogger<TradeQueryService>.Instance;
        var service = new TradeQueryService(db, logger);

        var result = await service.GetTraderTradesAsync(101);

        Assert.True(result.IsSuccess);
        Assert.True(result.TryGetData(out var data));

        var trade = data.First();
        Assert.Equal("Buyer", trade.TraderRole);
        Assert.Equal(-500m, trade.Profit);
    }

    [Fact]
    public async Task GetTraderTradesAsync_AsSeller_ReturnsCorrectProfit()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.RegisterTrader(db, 101);
        await HelpMethods.RegisterTrader(db, 102);
        await HelpMethods.CreateToken(db, "ZZZ", "Zero");
        await HelpMethods.AddPortfolio(db, 102, "ZZZ", 10);

        await HelpMethods.PlaceOrder(db, 102, "продать", "ZZZ", 5, 100);
        await HelpMethods.PlaceOrder(db, 101, "купить", "ZZZ", 5, 100);

        var logger = NullLogger<TradeQueryService>.Instance;
        var service = new TradeQueryService(db, logger);

        var result = await service.GetTraderTradesAsync(102);

        Assert.True(result.IsSuccess);
        Assert.True(result.TryGetData(out var data));

        var trade = data.First();
        Assert.Equal("Seller", trade.TraderRole);
        Assert.Equal(500m, trade.Profit);
    }

    [Fact]
    public async Task GetTraderTradesAsync_ReturnsCorrectTradeInfo()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.RegisterTrader(db, 101);
        await HelpMethods.RegisterTrader(db, 102);
        await HelpMethods.CreateToken(db, "ZZZ", "Zero");
        await HelpMethods.AddPortfolio(db, 102, "ZZZ", 10);

        await HelpMethods.PlaceOrder(db, 102, "продать", "ZZZ", 5, 100);
        await HelpMethods.PlaceOrder(db, 101, "купить", "ZZZ", 5, 100);

        var logger = NullLogger<TradeQueryService>.Instance;
        var service = new TradeQueryService(db, logger);

        var result = await service.GetTraderTradesAsync(101);

        Assert.True(result.IsSuccess);
        Assert.True(result.TryGetData(out var data));

        var trade = data.First();
        Assert.Equal("ZZZ", trade.TokenInfo.Symbol);
        Assert.Equal(100m, trade.ExecutionPrice);
        Assert.Equal(5m, trade.Quantity);
    }

    [Fact]
    public async Task GetTraderTradesAsync_WithTokenInfo_ReturnsIcon()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.RegisterTrader(db, 101);
        await HelpMethods.RegisterTrader(db, 102);
        await HelpMethods.CreateToken(db, "ZZZ", "Zero", CharacterRarity.FourStar, 1000, 100m, true, "image.png", "icon.png");
        await HelpMethods.AddPortfolio(db, 102, "ZZZ", 10);

        await HelpMethods.PlaceOrder(db, 102, "продать", "ZZZ", 5, 100);
        await HelpMethods.PlaceOrder(db, 101, "купить", "ZZZ", 5, 100);

        var logger = NullLogger<TradeQueryService>.Instance;
        var service = new TradeQueryService(db, logger);

        var result = await service.GetTraderTradesAsync(101, withTokenInfo: true);

        Assert.True(result.IsSuccess);
        Assert.True(result.TryGetData(out var data));

        var trade = data.First();
        Assert.Equal("icon.png", trade.TokenInfo.IconUrl);
    }
}