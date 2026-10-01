using ArkWallet.Core.General.Application.Common;
using ArkWallet.Core.TradingContext.Application.Contracts.TradeServices;
using ArkWallet.Core.TradingContext.Application.Services.TradeServices;
using ArkWallet.Core.General.Domain.ValueObjects;
using ArkWallet.Core.TradingContext.Domain.TraderAggregate;
using ArkWallet.Core.TradingContext.Domain.TokenAggregate;
using ArkWallet.Core.TradingContext.Domain.MarketMakerAggregate;
using ArkWallet.Core.TradingContext.Domain.TradeAggregate;
using ArkWallet.Core.TradingContext.Domain.Engines;
using ArkWallet.Core.PortfolioContext.Domain.Position;
using ArkWallet.Core.GiftContext.Domain.User;
using ArkWallet.Core.MailContext.Domain.Message;
using ArkWallet.Core.MiningContext.Domain.Machine;
using ArkWallet.Core.MiningContext.Domain.GlobalRule;
using ArkWallet.Core.MiningContext.Domain.Engines;
using ArkWallet.Infrastructure.Data;
using ArkWallet.Tests.HelpTools;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ArkWallet.Tests.Core.TradingContext.Application.Services.TradeServices;

public class TradeQueryServiceTest
{
    [Fact]
    public async Task GetTraderTradesAsync_WhenNoTrades_ReturnsEmptyList()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.RegisterTrader(db, 1001);

        var logger = NullLogger<TradeQueryService>.Instance;
        var service = new TradeQueryService(db, logger);

        var result = await service.GetTraderTradesAsync(1001);

        Assert.True(result.IsSuccess);
        Assert.True(result.TryGetData(out var data));
        Assert.Empty(data);
    }

    [Fact]
    public async Task GetTraderTradesAsync_WhenTradesExist_ReturnsAllTrades()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.RegisterTrader(db, 1001);
        await HelpMethods.RegisterTrader(db, 1002);
        await HelpMethods.CreateToken(db, "ZZZ", "Zero");
        await HelpMethods.AddPortfolio(db, 1002, "ZZZ", 10);

        await HelpMethods.PlaceOrder(db, 1002, "продать", "ZZZ", 5, 100);
        await HelpMethods.PlaceOrder(db, 1001, "купить", "ZZZ", 5, 100);

        var logger = NullLogger<TradeQueryService>.Instance;
        var service = new TradeQueryService(db, logger);

        var result = await service.GetTraderTradesAsync(1001);

        Assert.True(result.IsSuccess);
        Assert.True(result.TryGetData(out var data));
        Assert.Single(data);
    }

    [Fact]
    public async Task GetTraderTradesAsync_AsBuyer_ReturnsCorrectProfit()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.RegisterTrader(db, 1001);
        await HelpMethods.RegisterTrader(db, 1002);
        await HelpMethods.CreateToken(db, "ZZZ", "Zero");
        await HelpMethods.AddPortfolio(db, 1002, "ZZZ", 10);

        await HelpMethods.PlaceOrder(db, 1002, "продать", "ZZZ", 5, 100);
        await HelpMethods.PlaceOrder(db, 1001, "купить", "ZZZ", 5, 100);

        var logger = NullLogger<TradeQueryService>.Instance;
        var service = new TradeQueryService(db, logger);

        var result = await service.GetTraderTradesAsync(1001);

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

        await HelpMethods.RegisterTrader(db, 1001);
        await HelpMethods.RegisterTrader(db, 1002);
        await HelpMethods.CreateToken(db, "ZZZ", "Zero");
        await HelpMethods.AddPortfolio(db, 1002, "ZZZ", 10);

        await HelpMethods.PlaceOrder(db, 1002, "продать", "ZZZ", 5, 100);
        await HelpMethods.PlaceOrder(db, 1001, "купить", "ZZZ", 5, 100);

        var logger = NullLogger<TradeQueryService>.Instance;
        var service = new TradeQueryService(db, logger);

        var result = await service.GetTraderTradesAsync(1002);

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

        await HelpMethods.RegisterTrader(db, 1001);
        await HelpMethods.RegisterTrader(db, 1002);
        await HelpMethods.CreateToken(db, "ZZZ", "Zero");
        await HelpMethods.AddPortfolio(db, 1002, "ZZZ", 10);

        await HelpMethods.PlaceOrder(db, 1002, "продать", "ZZZ", 5, 100);
        await HelpMethods.PlaceOrder(db, 1001, "купить", "ZZZ", 5, 100);

        var logger = NullLogger<TradeQueryService>.Instance;
        var service = new TradeQueryService(db, logger);

        var result = await service.GetTraderTradesAsync(1001);

        Assert.True(result.IsSuccess);
        Assert.True(result.TryGetData(out var data));

        var trade = data.First();
        Assert.NotNull(trade.TokenInfo);
        Assert.Equal("ZZZ", trade.TokenInfo.Symbol);
        Assert.Equal(100m, trade.ExecutionPrice);
        Assert.Equal(5m, trade.Quantity);
    }

    [Fact]
    public async Task GetTraderTradesAsync_WithTokenInfo_ReturnsIcon()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.RegisterTrader(db, 1001);
        await HelpMethods.RegisterTrader(db, 1002);
        await HelpMethods.CreateToken(db, "ZZZ", "Zero", CharacterRarity.FourStar, 1000, 100m, true, "image.png", "icon.png");
        await HelpMethods.AddPortfolio(db, 1002, "ZZZ", 10);

        await HelpMethods.PlaceOrder(db, 1002, "продать", "ZZZ", 5, 100);
        await HelpMethods.PlaceOrder(db, 1001, "купить", "ZZZ", 5, 100);

        var logger = NullLogger<TradeQueryService>.Instance;
        var service = new TradeQueryService(db, logger);

        var result = await service.GetTraderTradesAsync(1001, withTokenInfo: true);

        Assert.True(result.IsSuccess);
        Assert.True(result.TryGetData(out var data));

        var trade = data.First();
        Assert.NotNull(trade.TokenInfo);
        Assert.Equal("icon.png", trade.TokenInfo.IconUrl);
    }

    [Fact]
    public async Task GetTraderTradesPageAsync_ReturnsFirstPageWithTotalCount()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.RegisterTrader(db, 1001);
        await HelpMethods.RegisterTrader(db, 1002);
        await HelpMethods.CreateToken(db, "ZZZ", "Zero");
        var baseTime = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        for (int i = 0; i < 4; i++)
        {
            db.Trades.Add(new ArkWallet.Infrastructure.Data.Trade
            {
                BuyerId = 1001,
                SellerId = 1002,
                CharacterTokenId = "ZZZ",
                Price = 100 + i,
                Quantity = 5,
                ExecutedAt = baseTime.AddMinutes(i)
            });
        }
        await db.SaveChangesAsync();

        var logger = NullLogger<TradeQueryService>.Instance;
        var service = new TradeQueryService(db, logger);

        var result = await service.GetTraderTradesPageAsync(1001, page: 1, pageSize: 3);

        Assert.True(result.IsSuccess);
        Assert.True(result.TryGetData(out var data));
        Assert.Equal(4, data.TotalCount);
        Assert.Equal(3, data.PageSize);
        Assert.Equal(1, data.Page);
        Assert.Equal(3, data.Items.Count);
        Assert.Equal(2, data.TotalPages);
        Assert.True(data.HasNext);
        Assert.False(data.HasPrevious);
    }

    [Fact]
    public async Task GetTraderTradesPageAsync_PagesNoOverlapAndFullSet()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.RegisterTrader(db, 1001);
        await HelpMethods.RegisterTrader(db, 1002);
        await HelpMethods.CreateToken(db, "ABC", "Alpha");
        await HelpMethods.AddPortfolio(db, 1002, "ABC", 15);

        var execPrices = new[] { 50m, 100m, 150m };
        var baseTime = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        for (int i = 0; i < execPrices.Length; i++)
        {
            db.Trades.Add(new ArkWallet.Infrastructure.Data.Trade
            {
                BuyerId = 1001,
                SellerId = 1002,
                CharacterTokenId = "ABC",
                Price = execPrices[i],
                Quantity = 5,
                ExecutedAt = baseTime.AddMinutes(i)
            });
        }
        await db.SaveChangesAsync();

        var logger = NullLogger<TradeQueryService>.Instance;
        var service = new TradeQueryService(db, logger);

        var resultPage1 = await service.GetTraderTradesPageAsync(1001, page: 1, pageSize: 2);
        var resultPage2 = await service.GetTraderTradesPageAsync(1001, page: 2, pageSize: 2);
        var resultPage3 = await service.GetTraderTradesPageAsync(1001, page: 3, pageSize: 2);

        Assert.True(resultPage1.IsSuccess);
        Assert.True(resultPage1.TryGetData(out var data1));
        Assert.True(resultPage2.IsSuccess);
        Assert.True(resultPage2.TryGetData(out var data2));
        Assert.True(resultPage3.IsSuccess);
        Assert.True(resultPage3.TryGetData(out var data3));

        Assert.Equal(3, data1.TotalCount);
        Assert.Equal(2, data1.Items.Count);
        Assert.Single(data2.Items);
        Assert.Empty(data3.Items);
        Assert.Equal(1, data1.Page);
        Assert.Equal(2, data2.Page);
        Assert.Equal(3, data3.Page);

        Assert.True(data1.HasNext);
        Assert.False(data1.HasPrevious);
        Assert.False(data2.HasNext);
        Assert.True(data2.HasPrevious);
        Assert.False(data3.HasNext);
        Assert.True(data3.HasPrevious);

        Assert.Equal(2, data1.TotalPages);
        Assert.Equal(2, data2.TotalPages);
        Assert.Equal(2, data3.TotalPages);

        var allItems = data1.Items.Concat(data2.Items).Concat(data3.Items).ToArray();

        Assert.Equal(3, allItems.Length);
        Assert.Equal(execPrices[2], allItems[0].ExecutionPrice);
        Assert.Equal(execPrices[1], allItems[1].ExecutionPrice);
        Assert.Equal(execPrices[0], allItems[2].ExecutionPrice);
    }

    [Fact]
    public async Task GetTraderTradesPageAsync_OutOfRangePage_ReturnsEmptyItemsWithCorrectMetadata()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.RegisterTrader(db, 1001);
        await HelpMethods.RegisterTrader(db, 1002);
        await HelpMethods.CreateToken(db, "XYZ", "Xenon");
        await HelpMethods.AddPortfolio(db, 1002, "XYZ", 10);

        await HelpMethods.PlaceOrder(db, 1002, "продать", "XYZ", 5, 80);
        await HelpMethods.PlaceOrder(db, 1001, "купить", "XYZ", 5, 80);

        var logger = NullLogger<TradeQueryService>.Instance;
        var service = new TradeQueryService(db, logger);

        var result = await service.GetTraderTradesPageAsync(1001, page: 5, pageSize: 10);

        Assert.True(result.IsSuccess);
        Assert.True(result.TryGetData(out var data));
        Assert.Equal(1, data.TotalCount);
        Assert.Empty(data.Items);
        Assert.Equal(5, data.Page);
        Assert.Equal(10, data.PageSize);
        Assert.Equal(1, data.TotalPages);
        Assert.False(data.HasNext);
        Assert.True(data.HasPrevious);
    }

    [Fact]
    public async Task GetTraderTradesPageAsync_OtherTraderTradesNotIncluded()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.RegisterTrader(db, 1001);
        await HelpMethods.RegisterTrader(db, 1002);
        await HelpMethods.CreateToken(db, "TOK", "Token");
        await HelpMethods.AddPortfolio(db, 1002, "TOK", 20);

        await HelpMethods.PlaceOrder(db, 1002, "продать", "TOK", 10, 100);
        await HelpMethods.PlaceOrder(db, 1001, "купить", "TOK", 10, 100);

        var logger = NullLogger<TradeQueryService>.Instance;
        var service = new TradeQueryService(db, logger);

        var resultFor1002 = await service.GetTraderTradesPageAsync(1002, page: 1, pageSize: 10);

        Assert.True(resultFor1002.IsSuccess);
        Assert.True(resultFor1002.TryGetData(out var data1002));
        Assert.Single(data1002.Items);

        var resultFor999 = await service.GetTraderTradesPageAsync(999, page: 1, pageSize: 10);

        Assert.True(resultFor999.IsSuccess);
        Assert.True(resultFor999.TryGetData(out var data999));
        Assert.Empty(data999.Items);
        Assert.Equal(0, data999.TotalCount);
        Assert.Equal(0, data999.TotalPages);
    }

    [Fact]
    public async Task GetTraderTradesPageAsync_HasPreviousTrueOnSecondPage()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.RegisterTrader(db, 1001);
        await HelpMethods.RegisterTrader(db, 1002);
        await HelpMethods.CreateToken(db, "PRE", "Previous");
        await HelpMethods.AddPortfolio(db, 1002, "PRE", 30);

        var baseTime = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        for (int i = 0; i < 4; i++)
        {
            db.Trades.Add(new ArkWallet.Infrastructure.Data.Trade
            {
                BuyerId = 1001,
                SellerId = 1002,
                CharacterTokenId = "PRE",
                Price = 100 + i,
                Quantity = 5,
                ExecutedAt = baseTime.AddMinutes(i)
            });
        }
        await db.SaveChangesAsync();

        var logger = NullLogger<TradeQueryService>.Instance;
        var service = new TradeQueryService(db, logger);

        var resultPage1 = await service.GetTraderTradesPageAsync(1001, page: 1, pageSize: 2);
        var resultPage2 = await service.GetTraderTradesPageAsync(1001, page: 2, pageSize: 2);
        var resultPage3 = await service.GetTraderTradesPageAsync(1001, page: 3, pageSize: 2);

        Assert.True(resultPage1.IsSuccess);
        Assert.True(resultPage1.TryGetData(out var data1));
        Assert.True(resultPage2.IsSuccess);
        Assert.True(resultPage2.TryGetData(out var data2));
        Assert.True(resultPage3.IsSuccess);
        Assert.True(resultPage3.TryGetData(out var data3));

        Assert.False(data1.HasPrevious);
        Assert.True(data2.HasPrevious);
        Assert.True(data3.HasPrevious);

        Assert.Equal(2, data1.TotalPages);
        Assert.Equal(2, data2.TotalPages);
        Assert.Equal(2, data3.TotalPages);
    }
}
