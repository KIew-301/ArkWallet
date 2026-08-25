using ArkWallet.Application.Common;
using ArkWallet.Domain.ValueObjects;
using ArkWallet.Tests.HelpTools;
using Microsoft.EntityFrameworkCore;

namespace ArkWallet.Tests.ServiceTests.Order;

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

        await HelpMethods.RegisterTrader(db, BotId, "Bot");
        await HelpMethods.RegisterTrader(db, UserId, "User");
        await HelpMethods.CreateToken(db, "ZZZ");
        await HelpMethods.AddPortfolio(db, UserId, "ZZZ", 10);

        await HelpMethods.PlaceOrder(db, UserId, "продать", "ZZZ", 5, 100);
        var result = await HelpMethods.PlaceOrder(db, BotId, "купить", "ZZZ", 5, 100);

        Assert.True(result.IsSuccess, result.Message);

        var botOrders = await db.TradeOrders
            .Where(o => o.TraderTelegramId == BotId)
            .ToArrayAsync();

        Assert.Empty(botOrders);
    }

    [Fact]
    public async Task BotSellOrder_FullFill_DeletedFromDb()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.RegisterTrader(db, BotId, "Bot");
        await HelpMethods.RegisterTrader(db, UserId, "User");
        await HelpMethods.CreateToken(db, "ZZZ");
        await HelpMethods.AddPortfolio(db, BotId, "ZZZ", 10);

        await HelpMethods.PlaceOrder(db, UserId, "купить", "ZZZ", 5, 100);
        var result = await HelpMethods.PlaceOrder(db, BotId, "продать", "ZZZ", 5, 100);

        Assert.True(result.IsSuccess, result.Message);

        var botOrders = await db.TradeOrders
            .Where(o => o.TraderTelegramId == BotId)
            .ToArrayAsync();

        Assert.Empty(botOrders);
    }

    [Fact]
    public async Task BotOrder_Cancelled_DeletedFromDb()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.RegisterTrader(db, BotId, "Bot");
        await HelpMethods.CreateToken(db, "ZZZ");

        var placeResult = await HelpMethods.PlaceOrder(db, BotId, "купить", "ZZZ", 5, 100);
        Assert.True(placeResult.IsSuccess, placeResult.Message);

        var cancelResult = await HelpMethods.CancelOrder(db, BotId, placeResult);
        Assert.True(cancelResult.IsSuccess, cancelResult.Message);

        var botOrders = await db.TradeOrders
            .Where(o => o.TraderTelegramId == BotId)
            .ToArrayAsync();

        Assert.Empty(botOrders);
    }

    [Fact]
    public async Task BotOrder_CancelAll_DeletedFromDb()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.RegisterTrader(db, BotId, "Bot");
        await HelpMethods.CreateToken(db, "ZZZ");

        await HelpMethods.PlaceOrder(db, BotId, "купить", "ZZZ", 3, 100);
        await HelpMethods.PlaceOrder(db, BotId, "купить", "ZZZ", 2, 200);

        var result = await HelpMethods.CancelAllOrders(db, BotId);
        Assert.True(result.IsSuccess, result.Message);

        var botOrders = await db.TradeOrders
            .Where(o => o.TraderTelegramId == BotId)
            .ToArrayAsync();

        Assert.Empty(botOrders);
    }

    [Fact]
    public async Task BotBotTrade_NotSavedToDb()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.RegisterTrader(db, BotId, "Bot1");
        await HelpMethods.RegisterTrader(db, AnotherBotId, "Bot2");
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
        await HelpMethods.RegisterTrader(db, BotId, "Bot");
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
        await HelpMethods.RegisterTrader(db, AnotherBotId, "Bot");
        await HelpMethods.CreateToken(db, "ZZZ");
        await HelpMethods.AddPortfolio(db, AnotherBotId, "ZZZ", 10);

        await HelpMethods.PlaceOrder(db, AnotherBotId, "продать", "ZZZ", 5, 100);
        var result = await HelpMethods.PlaceOrder(db, UserId, "купить", "ZZZ", 5, 100);

        Assert.True(result.IsSuccess, result.Message);

        var userOrders = await db.TradeOrders
            .Where(o => o.TraderTelegramId == UserId && o.Status == OrderStatus.Filled)
            .ToArrayAsync();

        Assert.Single(userOrders);
    }

    [Fact]
    public async Task BotPartialFill_NotDeletedUntilFullyFilled()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.RegisterTrader(db, BotId, "Bot");
        await HelpMethods.RegisterTrader(db, UserId, "User");
        await HelpMethods.CreateToken(db, "ZZZ");
        await HelpMethods.AddPortfolio(db, UserId, "ZZZ", 10);

        await HelpMethods.PlaceOrder(db, UserId, "продать", "ZZZ", 3, 100);
        var result = await HelpMethods.PlaceOrder(db, BotId, "купить", "ZZZ", 5, 100);

        Assert.True(result.IsSuccess, result.Message);

        var botOrders = await db.TradeOrders
            .Where(o => o.TraderTelegramId == BotId)
            .ToArrayAsync();

        Assert.Single(botOrders);
        Assert.Equal(OrderStatus.Active, botOrders[0].Status);
        Assert.Equal(3, botOrders[0].FilledQuantity);
    }

    [Fact]
    public async Task BotFilter_IsBot_ReturnsCorrectly()
    {
        Assert.False(BotFilter.IsBot(50));
        Assert.True(BotFilter.IsBot(100));
        Assert.True(BotFilter.IsBot(101));
        Assert.True(BotFilter.IsBot(500));
        Assert.True(BotFilter.IsBot(1000));
        Assert.False(BotFilter.IsBot(1001));
        Assert.False(BotFilter.IsBot(5000));
    }

    [Fact]
    public async Task BotFilter_IsBotBotTrade_ReturnsCorrectly()
    {
        Assert.True(BotFilter.IsBotBotTrade(101, 102));
        Assert.True(BotFilter.IsBotBotTrade(500, 1000));
        Assert.False(BotFilter.IsBotBotTrade(101, 1001));
        Assert.False(BotFilter.IsBotBotTrade(1001, 101));
        Assert.False(BotFilter.IsBotBotTrade(1001, 1002));
    }
}
