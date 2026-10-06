using ArkWallet.Core.General.Application.Common;
using ArkWallet.Core.General.Domain.ValueObjects;
using ArkWallet.Tests.HelpTools;
using Microsoft.EntityFrameworkCore;

namespace ArkWallet.Tests.Core.TradingContext.Application.Services.TradeOrderServices;

public class BotOrderFilteringTests
{
    private const long BotId = 101;
    private const long UserId = 10001;
    private const long AnotherBotId = 102;

    [Fact]
    public async Task BotBuyOrder_FullFill_DeletedFromDb()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.RegisterTrader(db, BotId, "Bot", isBot: true);
        await HelpMethods.RegisterTrader(db, UserId, "User");
        await HelpMethods.CreateToken(db, "ZZZ");
        await HelpMethods.AddPortfolio(db, UserId, "ZZZ", 10);

        await HelpMethods.PlaceOrder(db, UserId, "продать", "ZZZ", 5, 100);
        var result = await HelpMethods.PlaceOrder(db, BotId, "купить", "ZZZ", 5, 100);

        Assert.True(result.IsSuccess, result.Message);

        var botOrders = await db.TradeOrders
            .Where(o => o.TraderId == BotId)
            .ToArrayAsync();

        Assert.Empty(botOrders);
    }

    [Fact]
    public async Task BotSellOrder_FullFill_DeletedFromDb()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.RegisterTrader(db, BotId, "Bot", isBot: true);
        await HelpMethods.RegisterTrader(db, UserId, "User");
        await HelpMethods.CreateToken(db, "ZZZ");
        await HelpMethods.AddPortfolio(db, BotId, "ZZZ", 10);

        await HelpMethods.PlaceOrder(db, UserId, "купить", "ZZZ", 5, 100);
        var result = await HelpMethods.PlaceOrder(db, BotId, "продать", "ZZZ", 5, 100);

        Assert.True(result.IsSuccess, result.Message);

        var botOrders = await db.TradeOrders
            .Where(o => o.TraderId == BotId)
            .ToArrayAsync();

        Assert.Empty(botOrders);
    }

    [Fact]
    public async Task BotOrder_Cancelled_DeletedFromDb()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.RegisterTrader(db, BotId, "Bot", isBot: true);
        await HelpMethods.CreateToken(db, "ZZZ");

        var placeResult = await HelpMethods.PlaceOrder(db, BotId, "купить", "ZZZ", 5, 100);
        Assert.True(placeResult.IsSuccess, placeResult.Message);

        var cancelResult = await HelpMethods.CancelOrder(db, BotId, placeResult);
        Assert.True(cancelResult.IsSuccess, cancelResult.Message);

        var botOrders = await db.TradeOrders
            .Where(o => o.TraderId == BotId)
            .ToArrayAsync();

        Assert.Empty(botOrders);
    }

    [Fact]
    public async Task BotOrder_CancelAll_DeletedFromDb()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.RegisterTrader(db, BotId, "Bot", isBot: true);
        await HelpMethods.CreateToken(db, "ZZZ");

        await HelpMethods.PlaceOrder(db, BotId, "купить", "ZZZ", 3, 100);
        await HelpMethods.PlaceOrder(db, BotId, "купить", "ZZZ", 2, 200);

        var result = await HelpMethods.CancelAllOrders(db, BotId);
        Assert.True(result.IsSuccess, result.Message);

        var botOrders = await db.TradeOrders
            .Where(o => o.TraderId == BotId)
            .ToArrayAsync();

        Assert.Empty(botOrders);
    }

    [Fact]
    public async Task BotBotTrade_NotSavedToDb()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.RegisterTrader(db, BotId, "Bot1", isBot: true);
        await HelpMethods.RegisterTrader(db, AnotherBotId, "Bot2", isBot: true);
        await HelpMethods.CreateToken(db, "ZZZ");
        await HelpMethods.AddPortfolio(db, AnotherBotId, "ZZZ", 10);

        await HelpMethods.PlaceOrder(db, AnotherBotId, "продать", "ZZZ", 5, 100);
        await HelpMethods.PlaceOrder(db, BotId, "купить", "ZZZ", 5, 100);

        var trades = await db.Trades
            .Where(t => t.CharacterTokenId == "ZZZ")
            .ToArrayAsync();

        Assert.Empty(trades);
    }

    [Fact]
    public async Task HumanBotTrade_SavedToDb()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.RegisterTrader(db, UserId, "User");
        await HelpMethods.RegisterTrader(db, BotId, "Bot", isBot: true);
        await HelpMethods.CreateToken(db, "ZZZ");
        await HelpMethods.AddPortfolio(db, BotId, "ZZZ", 10);

        await HelpMethods.PlaceOrder(db, BotId, "продать", "ZZZ", 5, 100);
        await HelpMethods.PlaceOrder(db, UserId, "купить", "ZZZ", 5, 100);

        var trades = await db.Trades
            .Where(t => t.CharacterTokenId == "ZZZ")
            .ToArrayAsync();

        Assert.Single(trades);
        Assert.Equal(UserId, trades[0].BuyerId);
        Assert.Equal(BotId, trades[0].SellerId);
    }

    [Fact]
    public async Task UserOrder_NotDeletedWhenFilled()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.RegisterTrader(db, UserId, "User");
        await HelpMethods.RegisterTrader(db, AnotherBotId, "Bot", isBot: true);
        await HelpMethods.CreateToken(db, "ZZZ");
        await HelpMethods.AddPortfolio(db, AnotherBotId, "ZZZ", 10);

        await HelpMethods.PlaceOrder(db, AnotherBotId, "продать", "ZZZ", 5, 100);
        var result = await HelpMethods.PlaceOrder(db, UserId, "купить", "ZZZ", 5, 100);

        Assert.True(result.IsSuccess, result.Message);

        var userOrders = await db.TradeOrders
            .Where(o => o.TraderId == UserId && o.Status == OrderStatus.Filled)
            .ToArrayAsync();

        Assert.Single(userOrders);
    }

    [Fact]
    public async Task BotPartialFill_NotDeletedUntilFullyFilled()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.RegisterTrader(db, BotId, "Bot", isBot: true);
        await HelpMethods.RegisterTrader(db, UserId, "User");
        await HelpMethods.CreateToken(db, "ZZZ");
        await HelpMethods.AddPortfolio(db, UserId, "ZZZ", 10);

        await HelpMethods.PlaceOrder(db, UserId, "продать", "ZZZ", 3, 100);
        var result = await HelpMethods.PlaceOrder(db, BotId, "купить", "ZZZ", 5, 100);

        Assert.True(result.IsSuccess, result.Message);

        var botOrders = await db.TradeOrders
            .Where(o => o.TraderId == BotId)
            .ToArrayAsync();

        Assert.Single(botOrders);
        Assert.Equal(OrderStatus.Active, botOrders[0].Status);
        Assert.Equal(3, botOrders[0].FilledQuantity);
    }

    [Fact]
    public async Task BotStatus_DrivenByIsBotColumn_NotByTraderIdRange()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.RegisterTrader(db, BotId, "BotInLegacyRange", isBot: true);
        await HelpMethods.RegisterTrader(db, 5000, "BotOutsideLegacyRange", isBot: true);
        await HelpMethods.RegisterTrader(db, 500, "HumanInsideLegacyRange");

        var bots = await db.Traders.Where(t => t.IsBot).Select(t => t.TelegramId).ToListAsync();

        Assert.Contains(BotId, bots);
        Assert.Contains(5000, bots);
        Assert.DoesNotContain(500, bots);
    }

    [Fact]
    public async Task BotBotTrade_DetectedByIsBotColumn_EvenOutsideLegacyIdRange()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.RegisterTrader(db, 7001, "BotOutsideRange", isBot: true);
        await HelpMethods.RegisterTrader(db, 7002, "SecondBotOutsideRange", isBot: true);

        var botIds = await db.Traders
            .Where(t => t.IsBot)
            .Select(t => t.Id)
            .ToListAsync();

        Assert.Equal(2, botIds.Count);
        Assert.All(botIds, id => Assert.True(id > 1000));
    }
}
