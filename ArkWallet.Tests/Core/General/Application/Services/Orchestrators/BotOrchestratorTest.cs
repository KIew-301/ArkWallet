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
        Mock<IUpdatingService>? updatingService = null,
        Mock<IOrderCancellationService>? cancellationService = null)
    {
        eventPublisher ??= new();
        orderCollector ??= new();
        orderCollector.Setup(c => c.TakeAll()).Returns(() => Array.Empty<IReadOnlyCollection<CreateOrderCommand>>());
        botRegistration ??= new();
        updatingService ??= new();
        updatingService.Setup(s => s.CreateOrUpdatePortfolioAsync(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<int>()))
            .ReturnsAsync(Result.Ok());
        cancellationService ??= new();
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

    private static void SetupRoleRegistration(Mock<IMarketMakerBotRegistrationService> regMock, params string[] symbols)
    {
        foreach (var symbol in symbols)
        {
            regMock.Setup(r => r.RegisterBotAsync(symbol, BotRole.Buyer, 50m))
                .ReturnsAsync(Result<MarketMakerBotRegistrationData>.Ok(new MarketMakerBotRegistrationData(1L, 1L)));
            regMock.Setup(r => r.RegisterBotAsync(symbol, BotRole.Seller, 50m))
                .ReturnsAsync(Result<MarketMakerBotRegistrationData>.Ok(new MarketMakerBotRegistrationData(2L, 2L)));
            regMock.Setup(r => r.RegisterBotAsync(symbol, BotRole.Waller, 100m))
                .ReturnsAsync(Result<MarketMakerBotRegistrationData>.Ok(new MarketMakerBotRegistrationData(3L, 3L)));
        }
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
        SetupRoleRegistration(regMock, "TKN_DEF");

        var orch = CreateOrchestrator(db, botRegistration: regMock);
        var result = await orch.EnsureDefaultBotsAsync();

        Assert.True(result.IsSuccess);
        Assert.True(result.TryGetData(out var data));
        Assert.True(data.Changed);
        Assert.Equal(3, data.BotsAdded);
        Assert.Equal(0, data.BotsMoved);
        regMock.Verify(r => r.RegisterBotAsync("TKN_DEF", BotRole.Buyer, 50m), Times.Once);
        regMock.Verify(r => r.RegisterBotAsync("TKN_DEF", BotRole.Seller, 50m), Times.Once);
        regMock.Verify(r => r.RegisterBotAsync("TKN_DEF", BotRole.Waller, 100m), Times.Once);
    }

    [Fact]
    public async Task EnsureDefaultBotsAsync_AlreadyExists_Idempotent()
    {
        var db = DbTest.CreateDbContext();
        _disposables.Add(db);
        await db.Database.EnsureCreatedAsync();
        await HelpMethods.CreateToken(db, "TKN_EXISTING", isActive: true);

        await HelpMethods.RegisterTrader(db, 100, "MarketMakerBot_TKN_EXISTING");

        var existingBot = MarketMakerBotRecord.Create(100L, "TKN_EXISTING", BotRole.Buyer, 50m);
        await db.MarketMakerBots.AddAsync(existingBot);
        await db.SaveChangesAsync();

        var regMock = new Mock<IMarketMakerBotRegistrationService>();
        SetupRoleRegistration(regMock, "TKN_EXISTING");

        var orch = CreateOrchestrator(db, botRegistration: regMock);
        var result = await orch.EnsureDefaultBotsAsync();

        Assert.True(result.IsSuccess);
        Assert.True(result.TryGetData(out var data));
        Assert.Equal(2, data.BotsAdded);
        Assert.Equal(0, data.BotsMoved);

        var buyerCount = await db.MarketMakerBots.CountAsync(b => b.Symbol == "TKN_EXISTING" && b.Role == BotRole.Buyer);
        Assert.Equal(1, buyerCount);
        regMock.Verify(r => r.RegisterBotAsync("TKN_EXISTING", BotRole.Buyer, It.IsAny<decimal>()), Times.Never);
        regMock.Verify(r => r.RegisterBotAsync("TKN_EXISTING", BotRole.Seller, 50m), Times.Once);
        regMock.Verify(r => r.RegisterBotAsync("TKN_EXISTING", BotRole.Waller, 100m), Times.Once);
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
        SetupRoleRegistration(regMock, "SYM_A", "SYM_B");

        var orch = CreateOrchestrator(db, botRegistration: regMock);
        var result = await orch.EnsureDefaultBotsAsync();

        Assert.True(result.IsSuccess);
        regMock.Verify(r => r.RegisterBotAsync("SYM_A", BotRole.Buyer, 50m), Times.Once);
        regMock.Verify(r => r.RegisterBotAsync("SYM_A", BotRole.Seller, 50m), Times.Once);
        regMock.Verify(r => r.RegisterBotAsync("SYM_A", BotRole.Waller, 100m), Times.Once);
        regMock.Verify(r => r.RegisterBotAsync("SYM_B", BotRole.Buyer, 50m), Times.Once);
        regMock.Verify(r => r.RegisterBotAsync("SYM_B", BotRole.Seller, 50m), Times.Once);
        regMock.Verify(r => r.RegisterBotAsync("SYM_B", BotRole.Waller, 100m), Times.Once);
    }

    [Fact]
    public async Task EnsureDefaultBotsAsync_ThreeRolesShareTrader_MigratesTwoToDedicated()
    {
        var db = DbTest.CreateDbContext();
        _disposables.Add(db);
        await db.Database.EnsureCreatedAsync();
        await HelpMethods.CreateToken(db, "TKN_SHARED", isActive: true);
        await HelpMethods.RegisterTrader(db, 150, "SharedTrader");

        await db.MarketMakerBots.AddAsync(MarketMakerBotRecord.Create(150L, "TKN_SHARED", BotRole.Buyer, 50m));
        await db.MarketMakerBots.AddAsync(MarketMakerBotRecord.Create(150L, "TKN_SHARED", BotRole.Seller, 50m));
        await db.MarketMakerBots.AddAsync(MarketMakerBotRecord.Create(150L, "TKN_SHARED", BotRole.Waller, 50m));
        await db.SaveChangesAsync();

        long traderForBuyerId = 0;
        long traderForSellerId = 0;
        var regMock = new Mock<IMarketMakerBotRegistrationService>();
        regMock.Setup(r => r.RegisterBotAsync(It.IsAny<string>(), It.IsAny<BotRole>(), 100m))
            .ReturnsAsync(Result<MarketMakerBotRegistrationData>.Ok(new MarketMakerBotRegistrationData(900L, 900L)));
        regMock.Setup(r => r.CreateDedicatedTraderAsync(It.IsAny<string>(), It.IsAny<BotRole>()))
            .Returns(async (string symbol, BotRole role) =>
            {
                var trader = Trader.Create($"MarketMakerBot_{symbol}_{role}", isBot: true);
                await db.Traders.AddAsync(trader);
                await db.SaveChangesAsync();
                if (role == BotRole.Buyer && traderForBuyerId == 0) traderForBuyerId = trader.Id;
                if (role == BotRole.Seller && traderForSellerId == 0) traderForSellerId = trader.Id;
                return Result<long>.Ok(trader.Id);
            });

        var cancelMock = new Mock<IOrderCancellationService>();
        cancelMock.Setup(c => c.CancelAllOrderAsync(It.IsAny<long>()))
            .ReturnsAsync(Result<int>.Ok(0));

        var orch = CreateOrchestrator(db, botRegistration: regMock, cancellationService: cancelMock);
        var result = await orch.EnsureDefaultBotsAsync();

        Assert.True(result.IsSuccess);
        Assert.True(result.TryGetData(out var data));
        Assert.True(data.Changed);
        Assert.Equal(0, data.BotsAdded);
        Assert.Equal(2, data.BotsMoved);
        regMock.Verify(r => r.CreateDedicatedTraderAsync(It.IsAny<string>(), It.IsAny<BotRole>()), Times.Exactly(2));
        cancelMock.Verify(c => c.CancelAllOrderAsync(150L), Times.Exactly(2));
        regMock.Verify(r => r.RegisterBotAsync(It.IsAny<string>(), It.IsAny<BotRole>(), It.IsAny<decimal>()), Times.Never);

        var botsAfter = await db.MarketMakerBots.Where(b => b.Symbol == "TKN_SHARED").ToListAsync();
        Assert.Equal(3, botsAfter.Count);
        var uniqueTraderIds = botsAfter.Select(b => b.TraderId).Distinct().ToList();
        Assert.Equal(3, uniqueTraderIds.Count);
        Assert.Contains(150L, uniqueTraderIds);
        var newTraders = await db.Traders.Where(t => t.Username != null && t.Username.StartsWith("MarketMakerBot_TKN_SHARED_")).CountAsync();
        Assert.Equal(2, newTraders);
    }

    [Fact]
    public async Task EnsureDefaultBotsAsync_SharedTraderAcrossSymbols_MigratesOneBuyer()
    {
        var db = DbTest.CreateDbContext();
        _disposables.Add(db);
        await db.Database.EnsureCreatedAsync();
        await HelpMethods.CreateToken(db, "SYM_X", isActive: true);
        await HelpMethods.CreateToken(db, "SYM_Y", isActive: true);
        await HelpMethods.RegisterTrader(db, 160, "SharedXY");

        await db.MarketMakerBots.AddAsync(MarketMakerBotRecord.Create(160L, "SYM_X", BotRole.Buyer, 50m));
        await db.MarketMakerBots.AddAsync(MarketMakerBotRecord.Create(160L, "SYM_Y", BotRole.Buyer, 50m));
        await db.SaveChangesAsync();

        var regMock = new Mock<IMarketMakerBotRegistrationService>();
        regMock.Setup(r => r.RegisterBotAsync(It.IsAny<string>(), It.IsAny<BotRole>(), It.IsAny<decimal>()))
            .ReturnsAsync(Result<MarketMakerBotRegistrationData>.Ok(new MarketMakerBotRegistrationData(910L, 910L)));
        regMock.Setup(r => r.CreateDedicatedTraderAsync(It.IsAny<string>(), It.IsAny<BotRole>()))
            .Returns(async (string symbol, BotRole role) =>
            {
                var trader = Trader.Create($"MarketMakerBot_{symbol}_{role}", isBot: true);
                await db.Traders.AddAsync(trader);
                await db.SaveChangesAsync();
                return Result<long>.Ok(trader.Id);
            });

        var cancelMock = new Mock<IOrderCancellationService>();
        cancelMock.Setup(c => c.CancelAllOrderAsync(It.IsAny<long>()))
            .ReturnsAsync(Result<int>.Ok(0));

        var orch = CreateOrchestrator(db, botRegistration: regMock, cancellationService: cancelMock);
        var result = await orch.EnsureDefaultBotsAsync();

        Assert.True(result.IsSuccess);
        Assert.True(result.TryGetData(out var data));
        Assert.True(data.Changed);
        Assert.Equal(4, data.BotsAdded);
        Assert.Equal(1, data.BotsMoved);
        cancelMock.Verify(c => c.CancelAllOrderAsync(160L), Times.Once);
        regMock.Verify(r => r.CreateDedicatedTraderAsync(It.IsAny<string>(), It.IsAny<BotRole>()), Times.Once);

        var buyerXBots = await db.MarketMakerBots.Where(b => b.Symbol == "SYM_X" && b.Role == BotRole.Buyer).ToListAsync();
        var buyerYBots = await db.MarketMakerBots.Where(b => b.Symbol == "SYM_Y" && b.Role == BotRole.Buyer).ToListAsync();
        Assert.Single(buyerXBots);
        Assert.Single(buyerYBots);
        Assert.NotEqual(buyerXBots[0].TraderId, buyerYBots[0].TraderId);
        Assert.Contains(160L, new[] { buyerXBots[0].TraderId, buyerYBots[0].TraderId });
    }

    [Fact]
    public async Task EnsureDefaultBotsAsync_BotWithHumanTrader_MigratesToBotTrader()
    {
        var db = DbTest.CreateDbContext();
        _disposables.Add(db);
        await db.Database.EnsureCreatedAsync();
        await HelpMethods.CreateToken(db, "TKN_HUMAN", isActive: true);
        await HelpMethods.RegisterTrader(db, 7001, "Human");

        await db.MarketMakerBots.AddAsync(MarketMakerBotRecord.Create(7001L, "TKN_HUMAN", BotRole.Buyer, 50m));
        await db.SaveChangesAsync();

        var regMock = new Mock<IMarketMakerBotRegistrationService>();
        regMock.Setup(r => r.RegisterBotAsync(It.IsAny<string>(), It.IsAny<BotRole>(), It.IsAny<decimal>()))
            .ReturnsAsync(Result<MarketMakerBotRegistrationData>.Ok(new MarketMakerBotRegistrationData(920L, 920L)));
        regMock.Setup(r => r.CreateDedicatedTraderAsync(It.IsAny<string>(), It.IsAny<BotRole>()))
            .Returns(async (string symbol, BotRole role) =>
            {
                var trader = Trader.Create($"MarketMakerBot_{symbol}_{role}", isBot: true);
                await db.Traders.AddAsync(trader);
                await db.SaveChangesAsync();
                return Result<long>.Ok(trader.Id);
            });

        var cancelMock = new Mock<IOrderCancellationService>();
        cancelMock.Setup(c => c.CancelAllOrderAsync(It.IsAny<long>()))
            .ReturnsAsync(Result<int>.Ok(0));

        var orch = CreateOrchestrator(db, botRegistration: regMock, cancellationService: cancelMock);
        var result = await orch.EnsureDefaultBotsAsync();

        Assert.True(result.IsSuccess);
        Assert.True(result.TryGetData(out var data));
        Assert.True(data.Changed);
        Assert.Equal(2, data.BotsAdded);
        Assert.Equal(1, data.BotsMoved);
        cancelMock.Verify(c => c.CancelAllOrderAsync(7001L), Times.Once);
        regMock.Verify(r => r.CreateDedicatedTraderAsync(It.IsAny<string>(), It.IsAny<BotRole>()), Times.Once);
        regMock.Verify(r => r.RegisterBotAsync("TKN_HUMAN", BotRole.Buyer, It.IsAny<decimal>()), Times.Never);

        var buyerBots = await db.MarketMakerBots.Where(b => b.Symbol == "TKN_HUMAN" && b.Role == BotRole.Buyer).ToListAsync();
        Assert.Single(buyerBots);
        Assert.NotEqual(7001L, buyerBots[0].TraderId);
        var movedTrader = await db.Traders.FindAsync(buyerBots[0].TraderId);
        Assert.NotNull(movedTrader);
        Assert.True(movedTrader.IsBot);
    }

    [Fact]
    public async Task EnsureDefaultBotsAsync_AllDedicated_DoesNotRepair()
    {
        var db = DbTest.CreateDbContext();
        _disposables.Add(db);
        await db.Database.EnsureCreatedAsync();
        await HelpMethods.CreateToken(db, "TKN_OK", isActive: true);
        await HelpMethods.RegisterTrader(db, 210, "TB");
        await HelpMethods.RegisterTrader(db, 211, "TS");
        await HelpMethods.RegisterTrader(db, 212, "TW");

        await db.MarketMakerBots.AddAsync(MarketMakerBotRecord.Create(210L, "TKN_OK", BotRole.Buyer, 50m));
        await db.MarketMakerBots.AddAsync(MarketMakerBotRecord.Create(211L, "TKN_OK", BotRole.Seller, 50m));
        await db.MarketMakerBots.AddAsync(MarketMakerBotRecord.Create(212L, "TKN_OK", BotRole.Waller, 100m));
        await db.SaveChangesAsync();

        var regMock = new Mock<IMarketMakerBotRegistrationService>();
        regMock.Setup(r => r.RegisterBotAsync(It.IsAny<string>(), It.IsAny<BotRole>(), 100m))
            .ReturnsAsync(Result<MarketMakerBotRegistrationData>.Ok(new MarketMakerBotRegistrationData(930L, 930L)));

        var cancelMock = new Mock<IOrderCancellationService>();
        cancelMock.Setup(c => c.CancelAllOrderAsync(It.IsAny<long>()))
            .ReturnsAsync(Result<int>.Ok(0));

        var orch = CreateOrchestrator(db, botRegistration: regMock, cancellationService: cancelMock);
        var result = await orch.EnsureDefaultBotsAsync();

        Assert.True(result.IsSuccess);
        Assert.True(result.TryGetData(out var data));
        Assert.False(data.Changed);
        Assert.Equal(0, data.BotsAdded);
        Assert.Equal(0, data.BotsMoved);
        regMock.Verify(r => r.CreateDedicatedTraderAsync(It.IsAny<string>(), It.IsAny<BotRole>()), Times.Never);
        cancelMock.Verify(c => c.CancelAllOrderAsync(It.IsAny<long>()), Times.Never);
        regMock.Verify(r => r.RegisterBotAsync(It.IsAny<string>(), It.IsAny<BotRole>(), It.IsAny<decimal>()), Times.Never);

        var allBots = await db.MarketMakerBots.Where(b => b.Symbol == "TKN_OK").ToListAsync();
        Assert.Equal(3, allBots.Count);
        Assert.Equal(210L, allBots.First(b => b.Role == BotRole.Buyer).TraderId);
        Assert.Equal(211L, allBots.First(b => b.Role == BotRole.Seller).TraderId);
        Assert.Equal(212L, allBots.First(b => b.Role == BotRole.Waller).TraderId);
    }

    [Fact]
    public async Task EnsureDefaultBotsAsync_WrongBasePower_NormalizesToDefaults()
    {
        var db = DbTest.CreateDbContext();
        _disposables.Add(db);
        await db.Database.EnsureCreatedAsync();
        await HelpMethods.CreateToken(db, "TKN_PWR", isActive: true);
        await HelpMethods.RegisterTrader(db, 310, "PB");
        await HelpMethods.RegisterTrader(db, 311, "PS");
        await HelpMethods.RegisterTrader(db, 312, "PW");

        await db.MarketMakerBots.AddAsync(MarketMakerBotRecord.Create(310L, "TKN_PWR", BotRole.Buyer, 77m));
        await db.MarketMakerBots.AddAsync(MarketMakerBotRecord.Create(311L, "TKN_PWR", BotRole.Seller, 30m));
        await db.MarketMakerBots.AddAsync(MarketMakerBotRecord.Create(312L, "TKN_PWR", BotRole.Waller, 50m));
        await db.SaveChangesAsync();

        var regMock = new Mock<IMarketMakerBotRegistrationService>();
        var orch = CreateOrchestrator(db, botRegistration: regMock);
        var result = await orch.EnsureDefaultBotsAsync();

        Assert.True(result.IsSuccess);
        Assert.True(result.TryGetData(out var data));
        Assert.True(data.Changed);
        Assert.Equal(0, data.BotsAdded);
        Assert.Equal(0, data.BotsMoved);
        Assert.Equal(3, data.BotsPowerNormalized);
        regMock.Verify(r => r.RegisterBotAsync(It.IsAny<string>(), It.IsAny<BotRole>(), It.IsAny<decimal>()), Times.Never);

        var bots = await db.MarketMakerBots.Where(b => b.Symbol == "TKN_PWR").ToListAsync();
        Assert.Equal(50m, bots.Single(b => b.Role == BotRole.Buyer).BasePower);
        Assert.Equal(50m, bots.Single(b => b.Role == BotRole.Seller).BasePower);
        Assert.Equal(100m, bots.Single(b => b.Role == BotRole.Waller).BasePower);
    }

    [Fact]
    public async Task EnsureDefaultBotsAsync_DefaultsInPlace_IsIdempotent()
    {
        var db = DbTest.CreateDbContext();
        _disposables.Add(db);
        await db.Database.EnsureCreatedAsync();
        await HelpMethods.CreateToken(db, "TKN_PWR2", isActive: true);
        await HelpMethods.RegisterTrader(db, 320, "PB");
        await HelpMethods.RegisterTrader(db, 321, "PS");
        await HelpMethods.RegisterTrader(db, 322, "PW");

        await db.MarketMakerBots.AddAsync(MarketMakerBotRecord.Create(320L, "TKN_PWR2", BotRole.Buyer, 50m));
        await db.MarketMakerBots.AddAsync(MarketMakerBotRecord.Create(321L, "TKN_PWR2", BotRole.Seller, 50m));
        await db.MarketMakerBots.AddAsync(MarketMakerBotRecord.Create(322L, "TKN_PWR2", BotRole.Waller, 100m));
        await db.SaveChangesAsync();

        var orch = CreateOrchestrator(db);
        var result = await orch.EnsureDefaultBotsAsync();

        Assert.True(result.IsSuccess);
        Assert.True(result.TryGetData(out var data));
        Assert.False(data.Changed);
        Assert.Equal(0, data.BotsPowerNormalized);
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

    // ═══════ New feature tests ═══════

    [Fact]
    public async Task RebalanceAllBotsPowerAsync_PersistsActivePowerToDatabase()
    {
        var db = await SeedTokenAsync();

        var traderId = 13001L;
        await HelpMethods.RegisterTrader(db, traderId);

        var basePower = 50m;
        var bot = MarketMakerBotRecord.Create(traderId, "TKN01", BotRole.Buyer, basePower);
        await db.MarketMakerBots.AddAsync(bot);
        await db.SaveChangesAsync();

        var orch = CreateOrchestrator(db);
        var result = await orch.RebalanceAllBotsPowerAsync();

        Assert.True(result.IsSuccess);
        var updatedBot = await db.MarketMakerBots.FirstAsync(b => b.Id == bot.Id);
        Assert.True(updatedBot.ActivePower >= 25m && updatedBot.ActivePower < 75m);
    }

    [Fact]
    public async Task RebalanceAllBotsPowerAsync_AmplifiesWallPowerByFactorEight()
    {
        var db = await SeedTokenAsync();

        var traderId = 13002L;
        await HelpMethods.RegisterTrader(db, traderId);

        var bot = MarketMakerBotRecord.Create(traderId, "TKN01", BotRole.Waller, 100m);
        await db.MarketMakerBots.AddAsync(bot);
        await db.SaveChangesAsync();

        var orch = CreateOrchestrator(db);
        var result = await orch.RebalanceAllBotsPowerAsync();

        Assert.True(result.IsSuccess);
        var updatedBot = await db.MarketMakerBots.FirstAsync(b => b.Id == bot.Id);
        Assert.True(updatedBot.ActivePower >= 400m && updatedBot.ActivePower < 1200m);
    }

    [Fact]
    public async Task UpdateBotsGridsForRoleAsync_UpdatesOnlyRequestedRole()
    {
        var db = await SeedTokenAsync();

        var buyerId = 13101L;
        var sellerId = 13102L;
        var wallerId = 13103L;
        await HelpMethods.RegisterTrader(db, buyerId);
        await HelpMethods.RegisterTrader(db, sellerId);
        await HelpMethods.RegisterTrader(db, wallerId);

        var buyerBot = MarketMakerBotRecord.Create(buyerId, "TKN01", BotRole.Buyer, 50m);
        var sellerBot = MarketMakerBotRecord.Create(sellerId, "TKN01", BotRole.Seller, 50m);
        var wallerBot = MarketMakerBotRecord.Create(wallerId, "AAA", BotRole.Waller, 100m);
        await db.MarketMakerBots.AddRangeAsync(buyerBot, sellerBot, wallerBot);
        await db.SaveChangesAsync();

        var evMock = new Mock<IEventPublisher>();
        var collMock = new Mock<IOrderCollector>();
        collMock.Setup(c => c.TakeAll()).Returns(() => Array.Empty<IReadOnlyCollection<CreateOrderCommand>>());

        var orch = CreateOrchestrator(db, eventPublisher: evMock, orderCollector: collMock);
        var result = await orch.UpdateBotsGridsForRoleAsync(MarketMakerRole.Seller);

        Assert.True(result.IsSuccess);
        evMock.Verify(
            e => e.PublishAsync(It.IsAny<BotPublicOrdersEvent>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task UpdateBotsGridsForRoleAsync_DoesNotCancelOrders_ForBuyerByDefault()
    {
        var db = await SeedTokenAsync();

        var buyerId = 13301L;
        await HelpMethods.RegisterTrader(db, buyerId);

        var buyerBot = MarketMakerBotRecord.Create(buyerId, "TKN01", BotRole.Buyer, 50m);
        await db.MarketMakerBots.AddAsync(buyerBot);
        await db.SaveChangesAsync();

        var cancellationMock = new Mock<IOrderCancellationService>();
        cancellationMock.Setup(c => c.CancelAllOrderAsync(It.IsAny<long>()))
            .ReturnsAsync(Result<int>.Ok(0));

        var orch = CreateOrchestrator(db, cancellationService: cancellationMock);
        var result = await orch.UpdateBotsGridsForRoleAsync(MarketMakerRole.Buyer);

        Assert.True(result.IsSuccess);
        cancellationMock.Verify(c => c.CancelAllOrderAsync(It.IsAny<long>()), Times.Never);
    }

    [Fact]
    public async Task UpdateBotsGridsForRoleAsync_CancelsOrders_ForBuyerWhenForced()
    {
        var db = await SeedTokenAsync();

        var buyerId = 13302L;
        await HelpMethods.RegisterTrader(db, buyerId);

        var buyerBot = MarketMakerBotRecord.Create(buyerId, "TKN01", BotRole.Buyer, 50m);
        await db.MarketMakerBots.AddAsync(buyerBot);
        await db.SaveChangesAsync();

        var cancellationMock = new Mock<IOrderCancellationService>();
        cancellationMock.Setup(c => c.CancelAllOrderAsync(It.IsAny<long>()))
            .ReturnsAsync(Result<int>.Ok(0));

        var orch = CreateOrchestrator(db, cancellationService: cancellationMock);
        var result = await orch.UpdateBotsGridsForRoleAsync(MarketMakerRole.Buyer, cancelExistingOrders: true);

        Assert.True(result.IsSuccess);
        cancellationMock.Verify(c => c.CancelAllOrderAsync(buyerId), Times.Once);
    }

    [Fact]
    public async Task UpdateBotsGridsForRoleAsync_CancelsOrders_ForWallerByDefault()
    {
        var db = await SeedTokenAsync();

        var wallerId = 13303L;
        await HelpMethods.RegisterTrader(db, wallerId);

        var wallerBot = MarketMakerBotRecord.Create(wallerId, "AAA", BotRole.Waller, 100m);
        await db.MarketMakerBots.AddAsync(wallerBot);
        await db.SaveChangesAsync();

        var cancellationMock = new Mock<IOrderCancellationService>();
        cancellationMock.Setup(c => c.CancelAllOrderAsync(It.IsAny<long>()))
            .ReturnsAsync(Result<int>.Ok(0));

        var orch = CreateOrchestrator(db, cancellationService: cancellationMock);
        var result = await orch.UpdateBotsGridsForRoleAsync(MarketMakerRole.Waller);

        Assert.True(result.IsSuccess);
        cancellationMock.Verify(c => c.CancelAllOrderAsync(wallerId), Times.Once);
    }

    [Fact]
    public async Task UpdateBotsGridsForRoleAsync_ReturnsOk_WhenNoBotsForRole()
    {
        var db = await SeedTokenAsync();

        var traderId = 13201L;
        await HelpMethods.RegisterTrader(db, traderId);

        var bot = MarketMakerBotRecord.Create(traderId, "TKN01", BotRole.Buyer, 50m);
        await db.MarketMakerBots.AddAsync(bot);
        await db.SaveChangesAsync();

        var collMock = new Mock<IOrderCollector>();
        collMock.Setup(c => c.TakeAll()).Returns(() => Array.Empty<IReadOnlyCollection<CreateOrderCommand>>());

        var orch = CreateOrchestrator(db, orderCollector: collMock);
        var result = await orch.UpdateBotsGridsForRoleAsync(MarketMakerRole.Waller);

        Assert.True(result.IsSuccess);
        collMock.Verify(c => c.Add(It.IsAny<IReadOnlyCollection<CreateOrderCommand>>()), Times.Never);
    }
}
