using ArkWallet.Application.Common;
using ArkWallet.Application.Contracts.CharacterTokenServices;
using ArkWallet.Application.Contracts.Other;
using ArkWallet.Application.Contracts.TradeOrderServices;
using ArkWallet.Application.Services.CharacterTokenServices;
using ArkWallet.Application.Services.TradeOrderServices;
using ArkWallet.Domain.Engines;
using ArkWallet.Infrastructure;
using ArkWallet.Infrastructure.Data;
using ArkWallet.Tests.HelpTools;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ArkWallet.Tests.ServiceTests.Order;

/// <summary>
/// Reproduction of the Aug-22 incident: a user opened a Sell 2000@45 wall and a Buy@44.4 on SHZA.
/// The market maker filled the bid tick by tick (each tick is its own DbContext scope).
/// Observed in the DB: Trades are created and FilledQuantity grows while PortfolioItems.Quantity
/// stays flat (tokens never reach the portfolio).
/// </summary>
public class OrderFillPortfolioConsistencyTest : IDisposable
{
    private const long UserId = 5728254515;
    private const long BotId = 1002;
    private const string Sym = "SHZA";

    private readonly SqliteConnection _connection;

    public OrderFillPortfolioConsistencyTest()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        using var db = NewScope();
        db.Database.EnsureCreated();
    }

    private ArkWalletDbContext NewScope()
    {
        var options = new DbContextOptionsBuilder<ArkWalletDbContext>()
            .UseSqlite(_connection)
            .Options;
        return new ArkWalletDbContext(options);
    }

    private static OrderCreationService BatchService(ArkWalletDbContext db)
    {
        var candleUpdateService = new TokenPriceCandleUpdateService(
            db, TimeProvider.System, NullLogger<TokenPriceCandleUpdateService>.Instance);

        return new OrderCreationService(
            db,
            new TradingEngine(),
            new OrderValidationService(db),
            new MediatREventPublisher(TestMediatorFactory.Create(db, candleUpdateService)),
            new Mock<ITaskDispatcher>().Object,
            NullLogger<OrderCreationService>.Instance);
    }

    [Fact]
    public async Task UserWallAndBid_BotTicksFillBid_PortfolioQuantityMustMatchFills()
    {
        // --- seed (each block uses its own scope, like production requests) ---
        using (var db = NewScope())
        {
            await HelpMethods.RegisterTrader(db, UserId, "Testes");
            await HelpMethods.RegisterTrader(db, BotId, "MMBot");
            await HelpMethods.CreateToken(db, Sym, price: 44);
            await HelpMethods.GiveMoney(db, UserId, 100_000);
            await HelpMethods.GiveMoney(db, BotId, 1_000_000);
            await HelpMethods.AddPortfolio(db, UserId, Sym, 2000);
            await HelpMethods.AddPortfolio(db, BotId, Sym, 5000);
            await db.SaveChangesAsync();
        }

        // --- step 1: user places a Sell 2000@45 wall ---
        using (var db = NewScope())
        {
            var r = await HelpMethods.PlaceOrder(db, UserId, "продать", Sym, 2000, 45);
            Assert.True(r.IsSuccess, $"sell wall failed: {r.Message}");
        }

        // --- step 2: user places a Buy 100@44.4 ---
        using (var db = NewScope())
        {
            var r = await HelpMethods.PlaceOrder(db, UserId, "купить", Sym, 100, 44.4m);
            Assert.True(r.IsSuccess, $"buy failed: {r.Message}");
        }

        decimal balanceAfterPlacement;
        using (var db = NewScope())
        {
            balanceAfterPlacement = (await HelpMethods.GetTrader(db, UserId)).Balance;
        }

        // --- steps 3..N: bot sells at 44.4 tick by tick, each tick gets a fresh scope ---
        int[] tickQuantities = [19, 16]; // actual trade sizes at 12:04:19 and 12:05:26 UTC
        var cumulative = 0;
        foreach (var qty in tickQuantities)
        {
            using var db = NewScope();
            var r = await BatchService(db)
                .CreateOrdersAsync([new CreateOrderCommand(BotId, "продать", Sym, qty, 44.4m)]);
            Assert.True(r.IsSuccess, $"bot tick failed: {r.Message}");
            cumulative += qty;
        }

        // --- assertions (fresh scope, committed data only) ---
        using var verify = NewScope();

        var trades = await verify.Trades
            .Where(t => t.BuyerId == UserId && t.CharacterTokenId == Sym)
            .ToArrayAsync();
        Assert.Equal(tickQuantities.Length, trades.Length); // trades recorded

        var userBuyOrder = await verify.TradeOrders
            .Where(o => o.TraderTelegramId == UserId && o.Type == Domain.ValueObjects.OrderType.Buy)
            .SingleAsync();
        Assert.Equal(cumulative, userBuyOrder.FilledQuantity); // FilledQuantity persisted

        var userItem = await verify.PortfolioItems
            .SingleAsync(p => p.TraderTelegramId == UserId && p.CharacterTokenId == Sym);

        // KEY: bought tokens must land in the free portfolio quantity
        Assert.Equal(cumulative, userItem.Quantity);

        // The wall is untouched by the buy fills
        Assert.Equal(2000, userItem.ReserveQuantity);

        // Cash: reserve was taken on placement, fills at exactly the limit -> balance unchanged
        var balanceAfterFills = (await HelpMethods.GetTrader(verify, UserId)).Balance;
        Assert.Equal(balanceAfterPlacement, balanceAfterFills);

        // Token conservation: bot component sum stays constant (reserve merely moves between ledgers)
        var botItem = await verify.PortfolioItems
            .SingleAsync(p => p.TraderTelegramId == BotId && p.CharacterTokenId == Sym);
        Assert.Equal(5000, botItem.Quantity + botItem.SellingQuantity + botItem.ReserveQuantity);

        // User: initial 2000 (locked by the wall) + bought amount
        Assert.Equal(2000 + cumulative,
            userItem.Quantity + userItem.SellingQuantity + userItem.ReserveQuantity);
    }

    /// <summary>
    /// A single market maker batch contains TWO command groups (sells and buys).
    /// Group-1 fills the user's bid (Quantity +19),
    /// group-2 fills the user's wall (moves Reserve to Selling).
    /// If SyncTradersAndPortfolios runs only after all groups, group-2 aggregates are built
    /// before group-1 changes land and the absolute ApplyState erases Quantity.
    /// </summary>
    [Fact]
    public async Task BatchWithTwoGroups_BothFillSameUser_SecondGroupMustNotEraseFirstGroupQuantity()
    {
        using (var db = NewScope())
        {
            await HelpMethods.RegisterTrader(db, UserId, "Testes");
            await HelpMethods.RegisterTrader(db, BotId, "MMBot");
            await HelpMethods.RegisterTrader(db, 1003, "MMBot2");
            await HelpMethods.CreateToken(db, Sym, price: 44);
            await HelpMethods.GiveMoney(db, UserId, 100_000);
            await HelpMethods.GiveMoney(db, BotId, 1_000_000);
            await HelpMethods.GiveMoney(db, 1003, 1_000_000);
            await HelpMethods.AddPortfolio(db, UserId, Sym, 2000);
            await HelpMethods.AddPortfolio(db, BotId, Sym, 5000);
            await HelpMethods.AddPortfolio(db, 1003, Sym, 5000);
            await db.SaveChangesAsync();
        }

        using (var db = NewScope())
        {
            var wall = await HelpMethods.PlaceOrder(db, UserId, "продать", Sym, 2000, 45);
            Assert.True(wall.IsSuccess, $"sell wall failed: {wall.Message}");

            var bid = await HelpMethods.PlaceOrder(db, UserId, "купить", Sym, 100, 44.4m);
            Assert.True(bid.IsSuccess, $"buy failed: {bid.Message}");
        }

        // ONE batch, TWO groups: sells first (fill the bid), then buys (cross the wall at 45)
        var commands = new List<CreateOrderCommand>
        {
            new(BotId, "продать", Sym, 19, 44.4m), // sell SHZA group
            new(1003, "купить", Sym, 10, 45m)       // buy SHZA group - crosses the 45 wall
        };

        using (var db = NewScope())
        {
            var r = await BatchService(db).CreateOrdersAsync(commands);
            Assert.True(r.IsSuccess, $"batch failed: {r.Message}");
        }

        using var verify = NewScope();

        var userItem = await verify.PortfolioItems
            .SingleAsync(p => p.TraderTelegramId == UserId && p.CharacterTokenId == Sym);

        Assert.Equal(19, userItem.Quantity);          // group-1 purchase must survive
        Assert.Equal(10, userItem.SellingQuantity);   // sold by group-2
        Assert.Equal(1990, userItem.ReserveQuantity); // wall remainder

        var userBuy = await verify.TradeOrders.SingleAsync(o =>
            o.TraderTelegramId == UserId && o.Type == Domain.ValueObjects.OrderType.Buy);
        Assert.Equal(19, userBuy.FilledQuantity);

        var userWall = await verify.TradeOrders.SingleAsync(o =>
            o.TraderTelegramId == UserId && o.Type == Domain.ValueObjects.OrderType.Sell);
        Assert.Equal(10, userWall.FilledQuantity);
    }

    public void Dispose()
    {
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }
}

