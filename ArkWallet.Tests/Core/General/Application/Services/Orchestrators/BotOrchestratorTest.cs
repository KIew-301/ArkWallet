using ArkWallet.Core.General.Application.Common;
using ArkWallet.Core.General.Application.Contracts.Orchestrators;
using ArkWallet.Core.General.Application.Services.Orchestrators;
using ArkWallet.Core.General.Domain.Common;
using ArkWallet.Core.PortfolioContext.Application.Contracts.PortfolioServices;
using ArkWallet.Core.TradingContext.Application.Contracts.MarketMaker;
using ArkWallet.Core.TradingContext.Application.Contracts.TradeOrderServices;
using ArkWallet.Core.TradingContext.Domain.Events;
using ArkWallet.Core.TradingContext.Domain.MarketMakerAggregate;
using ArkWallet.Infrastructure.Data;
using ArkWallet.Tests.HelpTools;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using static ArkWallet.Core.General.Application.Common.Result;

namespace ArkWallet.Tests.Core.General.Application.Services.Orchestrators;

public class BotOrchestratorTest : IDisposable
{
    private readonly List<IAsyncDisposable> _disposables = new();

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        foreach (var d in _disposables)
            d.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    private async Task<ArkWalletDbContext> SeedTokenAsync()
    {
        var db = DbTest.CreateDbContext();
        _disposables.Add(db);
        await db.Database.EnsureCreatedAsync();
        await HelpMethods.CreateToken(db, "TKN01", isActive: true);
        await HelpMethods.CreatePriceCandle(db, "TKN01", 100m, DateTime.UtcNow.AddDays(-2));
        await HelpMethods.CreatePriceCandle(db, "TKN01", 100m, DateTime.UtcNow);
        return db;
    }

    private static BotOrchestrator CreateOrchestrator(
        ArkWalletDbContext db,
        Mock<IEventPublisher>? eventPublisher = null,
        Mock<IOrderCollector>? orderCollector = null,
        Mock<IMarketMakerBotRegistrationService>? botRegistration = null,
        Mock<IUpdatingService>? updatingService = null)
    {
        eventPublisher ??= new();
        orderCollector ??= new();
        orderCollector.Setup(c => c.TakeAll()).Returns(() => Array.Empty<IReadOnlyCollection<CreateOrderCommand>>());
        botRegistration ??= new();
        updatingService ??= new();
        updatingService.Setup(s => s.CreateOrUpdatePortfolioAsync(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<int>()))
            .ReturnsAsync(Result.Ok());
        var cancellationService = new Mock<IOrderCancellationService>();
        cancellationService.Setup(c => c.CancelAllOrderAsync(It.IsAny<long>()))
            .ReturnsAsync(Result<int>.Ok(0));
        return new BotOrchestrator(
            db,
            new PlanModifierCollection(),
            botRegistration.Object,
            orderCollector.Object,
            new Mock<IOrderCreationService>().Object,
            updatingService.Object,
            cancellationService.Object,
            eventPublisher.Object,
            NullLogger<BotOrchestrator>.Instance);
    }

    // ═══════ UpdateAllBotsBalancesAsync ═══════

    [Fact]
    public async Task UpdateAllBotsBalancesAsync_HappyPath_UpdatesTraderBalanceAndPortfolio()
    {
        var db = await SeedTokenAsync();
        await HelpMethods.RegisterTrader(db, 5001);
        await db.Database.ExecuteSqlRawAsync("UPDATE Traders SET Balance = 0 WHERE Id = 5001");

        var bot = MarketMakerBotRecord.Create(5001, "TKN01", BotRole.Buyer, 50m);
        await db.MarketMakerBots.AddAsync(bot);
        await db.SaveChangesAsync();

        var upMock = new Mock<IUpdatingService>();
        var orch = CreateOrchestrator(db, updatingService: upMock);
        var result = await orch.UpdateAllBotsBalancesAsync();

        Assert.True(result.IsSuccess);
        var updatedTrader = await db.Traders.FirstAsync(t => t.Id == 5001);
        Assert.Equal(MarketMakerBot.DefaultBalance, updatedTrader.Balance);
        upMock.Verify(s => s.CreateOrUpdatePortfolioAsync(5001, "TKN01", It.IsAny<int>()), Times.Once);
        var freshBot = await db.MarketMakerBots.FirstAsync(b => b.Id == bot.Id);
        Assert.Equal(BotRole.Buyer, freshBot.Role);
    }

    [Fact]
    public async Task UpdateAllBotsBalancesAsync_NoActiveBots_ReturnsOkWithoutException()
    {
        var db = await SeedTokenAsync();

        var orch = CreateOrchestrator(db);
        var result = await orch.UpdateAllBotsBalancesAsync();

        Assert.True(result.IsSuccess);
        var traders = await db.Traders.ToListAsync();
        Assert.Empty(traders);
    }

    [Fact]
    public async Task UpdateAllBotsBalancesAsync_InactiveTokens_SkipsPortfolioRefresh()
    {
        var db = DbTest.CreateDbContext();
        _disposables.Add(db);
        await db.Database.EnsureCreatedAsync();

        await HelpMethods.CreateToken(db, "TKN_INACTIVE", isActive: false);

        var traderId = 6001L;
        await HelpMethods.RegisterTrader(db, traderId);

        var bot = MarketMakerBotRecord.Create(traderId, "TKN_INACTIVE", BotRole.Seller, 50m);
        await db.MarketMakerBots.AddAsync(bot);
        await db.SaveChangesAsync();

        var orch = CreateOrchestrator(db);
        var result = await orch.UpdateAllBotsBalancesAsync();

        Assert.True(result.IsSuccess);
        var trader = await db.Traders.FirstAsync(t => t.Id == traderId);
        Assert.Equal(MarketMakerBot.DefaultBalance, trader.Balance);
        var portfolios = await db.PortfolioItems.Where(p => p.TraderId == traderId).ToListAsync();
        Assert.Empty(portfolios);
    }

    // ═══════ UpdateBotsGridsAsync ═══════

    [Fact]
    public async Task UpdateBotsGridsAsync_BotsWithPrice_PublishesEvents()
    {
        var db = await SeedTokenAsync();

        var traderId = 7001L;
        await HelpMethods.RegisterTrader(db, traderId);

        var bot = MarketMakerBotRecord.Create(traderId, "TKN01", BotRole.Seller, 50m);
        await db.MarketMakerBots.AddAsync(bot);
        await db.SaveChangesAsync();

        var evMock = new Mock<IEventPublisher>();
        var collMock = new Mock<IOrderCollector>();
        collMock.Setup(c => c.TakeAll()).Returns(() => Array.Empty<IReadOnlyCollection<CreateOrderCommand>>());

        var orch = CreateOrchestrator(db, eventPublisher: evMock, orderCollector: collMock);
        var result = await orch.UpdateBotsGridsAsync();

        Assert.True(result.IsSuccess);
        evMock.Verify(e => e.PublishAsync(It.IsAny<BotPublicOrdersEvent>(), It.IsAny<CancellationToken>()),
            Times.Once);
        collMock.Verify(c => c.Add(It.IsAny<IReadOnlyCollection<CreateOrderCommand>>()), Times.Once);
    }

    [Fact]
    public async Task UpdateBotsGridsAsync_NoActiveBots_ReturnsOk()
    {
        var db = await SeedTokenAsync();
        var orch = CreateOrchestrator(db);
        var result = await orch.UpdateBotsGridsAsync();
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task UpdateBotsGridsAsync_InactiveToken_SafeNoOp()
    {
        var db = DbTest.CreateDbContext();
        _disposables.Add(db);
        await db.Database.EnsureCreatedAsync();

        await HelpMethods.CreateToken(db, "TKN_STOPPED", isActive: false);
        var traderId = 7501L;
        await HelpMethods.RegisterTrader(db, traderId);

        var bot = MarketMakerBotRecord.Create(traderId, "TKN_STOPPED", BotRole.Buyer, 30m);
        bot.IsActive = true;
        await db.MarketMakerBots.AddAsync(bot);
        await db.SaveChangesAsync();

        var orch = CreateOrchestrator(db);
        var result = await orch.UpdateBotsGridsAsync();
        Assert.True(result.IsSuccess);
    }

    // ═══════ ExecuteMarketOrdersAsync ═══════

    [Fact]
    public async Task ExecuteMarketOrdersAsync_BotWithPrice_PublishesEvents()
    {
        var db = await SeedTokenAsync();
        var traderId = 8001L;
        await HelpMethods.RegisterTrader(db, traderId);

        var bot = MarketMakerBotRecord.Create(traderId, "TKN01", BotRole.Seller, 50m);
        await db.MarketMakerBots.AddAsync(bot);
        await db.SaveChangesAsync();

        var collMock = new Mock<IOrderCollector>();
        collMock.Setup(c => c.TakeAll()).Returns(() => Array.Empty<IReadOnlyCollection<CreateOrderCommand>>());

        var orch = CreateOrchestrator(db, orderCollector: collMock);
        var result = await orch.ExecuteMarketOrdersAsync();

        Assert.True(result.IsSuccess);
        collMock.Verify(c => c.Add(It.IsAny<IReadOnlyCollection<CreateOrderCommand>>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteMarketOrdersAsync_EmptyBotsList_ReturnsOk()
    {
        var db = await SeedTokenAsync();
        var orch = CreateOrchestrator(db);
        var result = await orch.ExecuteMarketOrdersAsync();
        Assert.True(result.IsSuccess);
    }

    // ═══════ UpdateWallBotGridsAsync ═══════

    [Fact]
    public async Task UpdateWallBotGridsAsync_WallerBot_ReturnsOk()
    {
        var db = await SeedTokenAsync();
        var traderId = 9001L;
        await HelpMethods.RegisterTrader(db, traderId);

        var bot = MarketMakerBotRecord.Create(traderId, "TKN01", BotRole.Waller, 400m);
        await db.MarketMakerBots.AddAsync(bot);
        await db.SaveChangesAsync();

        var collMock = new Mock<IOrderCollector>();
        collMock.Setup(c => c.TakeAll())
            .Returns(() => Array.Empty<IReadOnlyCollection<CreateOrderCommand>>());

        var orch = CreateOrchestrator(db, orderCollector: collMock);
        var result = await orch.UpdateWallBotGridsAsync();

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task UpdateWallBotGridsAsync_NoWallerBots_ReturnsOk()
    {
        var db = await SeedTokenAsync();
        var orch = CreateOrchestrator(db);
        var result = await orch.UpdateWallBotGridsAsync();
        Assert.True(result.IsSuccess);
    }

    // ═══════ RebalanceAllBotsPowerAsync ═══════

    [Fact]
    public async Task RebalanceAllBotsPowerAsync_ActiveBot_CalculatesPower()
    {
        var db = await SeedTokenAsync();
        var traderId = 10001L;
        await HelpMethods.RegisterTrader(db, traderId);

        var initialPower = 100m;
        var bot = MarketMakerBotRecord.Create(traderId, "TKN01", BotRole.Buyer, initialPower);
        await db.MarketMakerBots.AddAsync(bot);
        await db.SaveChangesAsync();

        var orch = CreateOrchestrator(db);
        var result = await orch.RebalanceAllBotsPowerAsync();

        Assert.True(result.IsSuccess);
        var updatedBot = await db.MarketMakerBots.FirstAsync(b => b.Id == bot.Id);
        Assert.NotNull(updatedBot);
        Assert.Equal(initialPower, updatedBot.BasePower);
    }

    // ═══════ EnsureDefaultBotsAsync ═══════

    [Fact]
    public async Task EnsureDefaultBotsAsync_TokenExists_CreatesThreeRoles()
    {
        var db = DbTest.CreateDbContext();
        _disposables.Add(db);
        await db.Database.EnsureCreatedAsync();
        await HelpMethods.CreateToken(db, "TKN_DEF", isActive: true);

        var regMock = new Mock<IMarketMakerBotRegistrationService>();
        regMock.Setup(r => r.RegisterBotAsync(It.IsAny<string>(), It.IsAny<BotRole>(), 100m))
            .ReturnsAsync(Result<MarketMakerBotRegistrationData>.Ok(new MarketMakerBotRegistrationData(1L, 1L)));

        var orch = CreateOrchestrator(db, botRegistration: regMock);
        var result = await orch.EnsureDefaultBotsAsync();

        Assert.True(result.IsSuccess);
        regMock.Verify(r => r.RegisterBotAsync("TKN_DEF", BotRole.Buyer, 100m), Times.Once);
        regMock.Verify(r => r.RegisterBotAsync("TKN_DEF", BotRole.Seller, 100m), Times.Once);
        regMock.Verify(r => r.RegisterBotAsync("TKN_DEF", BotRole.Waller, 100m), Times.Once);
    }

    [Fact]
    public async Task EnsureDefaultBotsAsync_AlreadyExists_Idempotent()
    {
        var db = DbTest.CreateDbContext();
        _disposables.Add(db);
        await db.Database.EnsureCreatedAsync();
        await HelpMethods.CreateToken(db, "TKN_EXISTING", isActive: true);

        var existingBot = MarketMakerBotRecord.Create(100L, "TKN_EXISTING", BotRole.Buyer, 50m);
        await db.MarketMakerBots.AddAsync(existingBot);
        await db.SaveChangesAsync();

        var regMock = new Mock<IMarketMakerBotRegistrationService>();
        regMock.Setup(r => r.RegisterBotAsync(It.IsAny<string>(), It.IsAny<BotRole>(), It.IsAny<decimal>()))
            .ReturnsAsync(Result<MarketMakerBotRegistrationData>.Ok(new MarketMakerBotRegistrationData(200L, 200L)));

        var orch = CreateOrchestrator(db, botRegistration: regMock);
        await orch.EnsureDefaultBotsAsync();

        var buyerCount = await db.MarketMakerBots.CountAsync(b => b.Symbol == "TKN_EXISTING" && b.Role == BotRole.Buyer);
        Assert.Equal(1, buyerCount);
        regMock.Verify(r => r.RegisterBotAsync("TKN_EXISTING", BotRole.Buyer, It.IsAny<decimal>()), Times.Never);
    }

    [Fact]
    public async Task EnsureDefaultBotsAsync_NoActiveTokens_ReturnsOk()
    {
        var db = DbTest.CreateDbContext();
        _disposables.Add(db);
        await db.Database.EnsureCreatedAsync();

        var regMock = new Mock<IMarketMakerBotRegistrationService>();
        var orch = CreateOrchestrator(db, botRegistration: regMock);
        var result = await orch.EnsureDefaultBotsAsync();

        Assert.True(result.IsSuccess);
        regMock.Verify(r => r.RegisterBotAsync(It.IsAny<string>(), It.IsAny<BotRole>(), It.IsAny<decimal>()), Times.Never);
    }

    [Fact]
    public async Task EnsureDefaultBotsAsync_MultipleSymbols_CreatesRolesForEach()
    {
        var db = DbTest.CreateDbContext();
        _disposables.Add(db);
        await db.Database.EnsureCreatedAsync();

        await HelpMethods.CreateToken(db, "SYM_A", isActive: true);
        await HelpMethods.CreateToken(db, "SYM_B", isActive: true);

        var regMock = new Mock<IMarketMakerBotRegistrationService>();
        regMock.Setup(r => r.RegisterBotAsync(It.IsAny<string>(), It.IsAny<BotRole>(), 100m))
            .ReturnsAsync(Result<MarketMakerBotRegistrationData>.Ok(new MarketMakerBotRegistrationData(300L, 300L)));

        var orch = CreateOrchestrator(db, botRegistration: regMock);
        var result = await orch.EnsureDefaultBotsAsync();

        Assert.True(result.IsSuccess);
        regMock.Verify(r => r.RegisterBotAsync("SYM_A", BotRole.Buyer, 100m), Times.Once);
        regMock.Verify(r => r.RegisterBotAsync("SYM_A", BotRole.Seller, 100m), Times.Once);
        regMock.Verify(r => r.RegisterBotAsync("SYM_A", BotRole.Waller, 100m), Times.Once);
        regMock.Verify(r => r.RegisterBotAsync("SYM_B", BotRole.Buyer, 100m), Times.Once);
        regMock.Verify(r => r.RegisterBotAsync("SYM_B", BotRole.Seller, 100m), Times.Once);
        regMock.Verify(r => r.RegisterBotAsync("SYM_B", BotRole.Waller, 100m), Times.Once);
    }

    // ─── Edge: bot without matching trader ───

    [Fact]
    public async Task UpdateAllBotsBalancesAsync_BotMissingTrader_LogsWarningAndContinues()
    {
        var db = await SeedTokenAsync();

        var orphanBot = MarketMakerBotRecord.Create(99999L, "TKN01", BotRole.Buyer, 50m);
        await db.MarketMakerBots.AddAsync(orphanBot);
        await db.SaveChangesAsync();

        var orch = CreateOrchestrator(db);
        var result = await orch.UpdateAllBotsBalancesAsync();

        Assert.True(result.IsSuccess);
        var bots = await db.MarketMakerBots.ToListAsync();
        Assert.Single(bots);
    }

    // ─── Grid modifiers: empty orders when no market data ───

    [Fact]
    public async Task UpdateBotsGridsAsync_BotWithoutToken_ReturnsOkSafely()
    {
        var db = DbTest.CreateDbContext();
        _disposables.Add(db);
        await db.Database.EnsureCreatedAsync();
        // No tokens seeded — bots with wildcard symbol can't find active symbols

        var traderId = 11001L;
        await HelpMethods.RegisterTrader(db, traderId);

        var bot = MarketMakerBotRecord.Create(traderId, "*", BotRole.Buyer, 50m);
        await db.MarketMakerBots.AddAsync(bot);
        await db.SaveChangesAsync();

        var orch = CreateOrchestrator(db);
        var result = await orch.UpdateBotsGridsAsync();
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task RebalanceAllBotsPowerAsync_BothBuyerAndSeller_UpdateAll()
    {
        var db = await SeedTokenAsync();

        var buyerId = 12001L;
        var sellerId = 12002L;
        await HelpMethods.RegisterTrader(db, buyerId);
        await HelpMethods.RegisterTrader(db, sellerId);

        var buyerBot = MarketMakerBotRecord.Create(buyerId, "TKN01", BotRole.Buyer, 50m);
        var sellerBot = MarketMakerBotRecord.Create(sellerId, "TKN01", BotRole.Seller, 60m);
        await db.MarketMakerBots.AddRangeAsync(buyerBot, sellerBot);
        await db.SaveChangesAsync();

        var orch = CreateOrchestrator(db);
        var result = await orch.RebalanceAllBotsPowerAsync();

        Assert.True(result.IsSuccess);
        var allBots = await db.MarketMakerBots.ToListAsync();
        Assert.Equal(2, allBots.Count);
    }
}
