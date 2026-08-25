using ArkWallet.Application.Common;
using ArkWallet.Application.Contracts.PortfolioServices;
using ArkWallet.Application.Contracts.TradeOrderServices;
using ArkWallet.Application.Contracts.TraderServices;
using ArkWallet.Application.Services.Orchestrators;
using ArkWallet.Domain.Entities;
using ArkWallet.Domain.Engines;
using ArkWallet.Infrastructure.Data;
using ArkWallet.Tests.HelpTools;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ArkWallet.Tests.Orchestrators;

public class MarketWallBlockerOrchestratorTest
{
    private const long TraderId = 103;

    private static MarketWallBlockerOrchestrator Build(ArkWalletDbContext db, TimeProvider? timeProvider = null)
    {
        var registration = new Mock<ITraderRegistrationService>();
        registration.Setup(x => x.RegisterTraderAsync(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<bool>()))
            .ReturnsAsync(Result.Ok());

        var portfolio = new Mock<IPortfolioUpdatingService>();
        portfolio.Setup(x => x.CreateOrUpdatePortfolioAsync(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<int>()))
            .ReturnsAsync(Result.Ok());

        var cancellation = new Mock<IOrderCancellationService>();
        cancellation.Setup(x => x.CancelAllOrderAsync(It.IsAny<long>()))
            .ReturnsAsync(Result<int>.Ok(0));

        var creation = new Mock<IOrderCreationService>();
        creation.Setup(x => x.CreateOrdersAsync(It.IsAny<IEnumerable<CreateOrderCommand>>()))
            .ReturnsAsync(Result<List<OrderCreationData>>.Ok(new List<OrderCreationData>()));

        return new MarketWallBlockerOrchestrator(
            db,
            registration.Object,
            portfolio.Object,
            cancellation.Object,
            creation.Object,
            new WallBlockerEngine(),
            NullLogger<MarketWallBlockerOrchestrator>.Instance,
            timeProvider);
    }

    [Fact]
    public async Task EnsureRegisteredAsync_WhenTraderNotExists_RegistersTrader()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        var registration = new Mock<ITraderRegistrationService>();
        registration.Setup(x => x.RegisterTraderAsync(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<bool>()))
            .ReturnsAsync(Result.Ok());

        var orchestrator = new MarketWallBlockerOrchestrator(
            db, registration.Object, null!, null!, null!, new WallBlockerEngine(),
            NullLogger<MarketWallBlockerOrchestrator>.Instance);

        var result = await orchestrator.EnsureRegisteredAsync();

        Assert.True(result.IsSuccess, result.Message);
        registration.Verify(x => x.RegisterTraderAsync(TraderId, "WallBlocker", false), Times.Once);
    }

    [Fact]
    public async Task EnsureRegisteredAsync_WhenTraderExists_SkipsRegistration()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await db.Traders.AddAsync(Trader.Create(TraderId, "WallBlocker"));
        await db.SaveChangesAsync();

        var registration = new Mock<ITraderRegistrationService>();

        var orchestrator = new MarketWallBlockerOrchestrator(
            db, registration.Object, null!, null!, null!, new WallBlockerEngine(),
            NullLogger<MarketWallBlockerOrchestrator>.Instance);

        var result = await orchestrator.EnsureRegisteredAsync();

        Assert.True(result.IsSuccess, result.Message);
        registration.Verify(x => x.RegisterTraderAsync(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<bool>()), Times.Never);
    }

    [Fact]
    public async Task EnsureRegisteredAsync_WhenRegistrationFails_ReturnsFail()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        var registration = new Mock<ITraderRegistrationService>();
        registration.Setup(x => x.RegisterTraderAsync(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<bool>()))
            .ReturnsAsync(Result.Fail("Registration failed"));

        var orchestrator = new MarketWallBlockerOrchestrator(
            db, registration.Object, null!, null!, null!, new WallBlockerEngine(),
            NullLogger<MarketWallBlockerOrchestrator>.Instance);

        var result = await orchestrator.EnsureRegisteredAsync();

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task EnsureBalancesAsync_WhenTraderHasInsufficientBalance_AddsMoneyAndUpdatesPortfolio()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.CreateToken(db, "ZZZ", price: 100);

        var trader = Trader.Create(TraderId, "WallBlocker");
        trader.AddToBalance(500_000_000m);
        await db.Traders.AddAsync(trader);
        await db.SaveChangesAsync();

        var portfolio = new Mock<IPortfolioUpdatingService>();
        portfolio.Setup(x => x.CreateOrUpdatePortfolioAsync(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<int>()))
            .ReturnsAsync(Result.Ok());

        var orchestrator = new MarketWallBlockerOrchestrator(
            db, null!, portfolio.Object, null!, null!, new WallBlockerEngine(),
            NullLogger<MarketWallBlockerOrchestrator>.Instance);

        var result = await orchestrator.EnsureBalancesAsync();

        Assert.True(result.IsSuccess, result.Message);

        var updated = await db.Traders.FirstAsync(t => t.TelegramId == TraderId);
        Assert.Equal(1_000_000_000m, updated.Balance);
        portfolio.Verify(x => x.CreateOrUpdatePortfolioAsync(TraderId, "ZZZ", 100_000_000), Times.Once);
    }

    [Fact]
    public async Task EnsureBalancesAsync_WhenTraderMissing_LogsWarningAndContinues()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.CreateToken(db, "ZZZ", price: 100);

        var orchestrator = Build(db);

        var result = await orchestrator.EnsureBalancesAsync();

        Assert.True(result.IsSuccess, result.Message);
    }

    [Fact]
    public async Task ExecuteIterationAsync_WhenDue_CancelsOldOrdersAndCreatesNew()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.CreateToken(db, "ZZZ", price: 100);

        await db.MarketMakerBots.AddAsync(MarketMakerBot.Create(101, "ZZZ", BotRole.Buyer, 20));
        await db.MarketMakerBots.AddAsync(MarketMakerBot.Create(102, "ZZZ", BotRole.Seller, 40));
        await db.SaveChangesAsync();

        var cancellation = new Mock<IOrderCancellationService>();
        cancellation.Setup(x => x.CancelAllOrderAsync(It.IsAny<long>()))
            .ReturnsAsync(Result<int>.Ok(0));

        var creation = new Mock<IOrderCreationService>();
        creation.Setup(x => x.CreateOrdersAsync(It.IsAny<IEnumerable<CreateOrderCommand>>()))
            .ReturnsAsync(Result<List<OrderCreationData>>.Ok(new List<OrderCreationData>()));

        var orchestrator = new MarketWallBlockerOrchestrator(
            db, null!, null!, cancellation.Object, creation.Object, new WallBlockerEngine(),
            NullLogger<MarketWallBlockerOrchestrator>.Instance, new TestTimeProvider());

        var result = await orchestrator.ExecuteIterationAsync();

        Assert.True(result.IsSuccess, result.Message);
        cancellation.Verify(x => x.CancelAllOrderAsync(TraderId), Times.Once);

        creation.Verify(
            x => x.CreateOrdersAsync(It.Is<IEnumerable<CreateOrderCommand>>(cmds =>
                cmds.Count() == 10 &&
                cmds.All(c => c.TraderId == TraderId && c.Symbol == "ZZZ") &&
                cmds.Count(c => c.Direction == "купить") == 5 &&
                cmds.Count(c => c.Direction == "продать") == 5)),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteIterationAsync_QuantityIsAverageBotPowerTimes_20To100()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.CreateToken(db, "ZZZ", price: 100);

        await db.MarketMakerBots.AddAsync(MarketMakerBot.Create(101, "ZZZ", BotRole.Buyer, 20));
        await db.MarketMakerBots.AddAsync(MarketMakerBot.Create(102, "ZZZ", BotRole.Seller, 40));
        await db.SaveChangesAsync();

        IEnumerable<CreateOrderCommand>? captured = null;

        var cancellation = new Mock<IOrderCancellationService>();
        cancellation.Setup(x => x.CancelAllOrderAsync(It.IsAny<long>()))
            .ReturnsAsync(Result<int>.Ok(0));

        var creation = new Mock<IOrderCreationService>();
        creation.Setup(x => x.CreateOrdersAsync(It.IsAny<IEnumerable<CreateOrderCommand>>()))
            .ReturnsAsync(Result<List<OrderCreationData>>.Ok(new List<OrderCreationData>()))
            .Callback<IEnumerable<CreateOrderCommand>>(cmds => captured = cmds);

        var orchestrator = new MarketWallBlockerOrchestrator(
            db, null!, null!, cancellation.Object, creation.Object, new WallBlockerEngine(),
            NullLogger<MarketWallBlockerOrchestrator>.Instance, new TestTimeProvider());

        var result = await orchestrator.ExecuteIterationAsync();

        Assert.True(result.IsSuccess, result.Message);

        var avg = (20m + 40m) / 2m;
        foreach (var cmd in captured!)
            Assert.InRange(cmd.Quantity, (int)(avg * 20m), (int)(avg * 100m * 1.4m));
    }

    [Fact]
    public async Task ExecuteIterationAsync_WhenNotDue_DoesNothing()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.CreateToken(db, "ZZZ", price: 100);

        var timeProvider = new TestTimeProvider();

        var cancellation = new Mock<IOrderCancellationService>();
        cancellation.Setup(x => x.CancelAllOrderAsync(It.IsAny<long>()))
            .ReturnsAsync(Result<int>.Ok(0));

        var creation = new Mock<IOrderCreationService>();
        creation.Setup(x => x.CreateOrdersAsync(It.IsAny<IEnumerable<CreateOrderCommand>>()))
            .ReturnsAsync(Result<List<OrderCreationData>>.Ok(new List<OrderCreationData>()));

        var orchestrator = new MarketWallBlockerOrchestrator(
            db, null!, null!, cancellation.Object, creation.Object, new WallBlockerEngine(),
            NullLogger<MarketWallBlockerOrchestrator>.Instance, timeProvider);

        var first = await orchestrator.ExecuteIterationAsync();
        Assert.True(first.IsSuccess, first.Message);

        var second = await orchestrator.ExecuteIterationAsync();

        Assert.True(second.IsSuccess, second.Message);
        cancellation.Verify(x => x.CancelAllOrderAsync(TraderId), Times.Once);
        creation.Verify(x => x.CreateOrdersAsync(It.IsAny<IEnumerable<CreateOrderCommand>>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteIterationAsync_WhenTimePassed_RunsAgain()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.CreateToken(db, "ZZZ", price: 100);

        var timeProvider = new TestTimeProvider();

        var cancellation = new Mock<IOrderCancellationService>();
        cancellation.Setup(x => x.CancelAllOrderAsync(It.IsAny<long>()))
            .ReturnsAsync(Result<int>.Ok(0));

        var creation = new Mock<IOrderCreationService>();
        creation.Setup(x => x.CreateOrdersAsync(It.IsAny<IEnumerable<CreateOrderCommand>>()))
            .ReturnsAsync(Result<List<OrderCreationData>>.Ok(new List<OrderCreationData>()));

        var orchestrator = new MarketWallBlockerOrchestrator(
            db, null!, null!, cancellation.Object, creation.Object, new WallBlockerEngine(),
            NullLogger<MarketWallBlockerOrchestrator>.Instance, timeProvider);

        var first = await orchestrator.ExecuteIterationAsync();
        Assert.True(first.IsSuccess, first.Message);

        timeProvider.SkipInSeconds(150 * 60);

        var second = await orchestrator.ExecuteIterationAsync();

        Assert.True(second.IsSuccess, second.Message);
        cancellation.Verify(x => x.CancelAllOrderAsync(TraderId), Times.Exactly(2));
        creation.Verify(x => x.CreateOrdersAsync(It.IsAny<IEnumerable<CreateOrderCommand>>()), Times.Exactly(2));
    }

    [Fact]
    public async Task ExecuteIterationAsync_WhenOrderCreationFails_ReturnsFail()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.CreateToken(db, "ZZZ", price: 100);

        var cancellation = new Mock<IOrderCancellationService>();
        cancellation.Setup(x => x.CancelAllOrderAsync(It.IsAny<long>()))
            .ReturnsAsync(Result<int>.Ok(0));

        var creation = new Mock<IOrderCreationService>();
        creation.Setup(x => x.CreateOrdersAsync(It.IsAny<IEnumerable<CreateOrderCommand>>()))
            .ReturnsAsync(Result<List<OrderCreationData>>.Fail("Order creation failed"));

        var orchestrator = new MarketWallBlockerOrchestrator(
            db, null!, null!, cancellation.Object, creation.Object, new WallBlockerEngine(),
            NullLogger<MarketWallBlockerOrchestrator>.Instance, new TestTimeProvider());

        var result = await orchestrator.ExecuteIterationAsync();

        Assert.False(result.IsSuccess);
        Assert.Contains("Order creation failed", result.Message);
    }
}
