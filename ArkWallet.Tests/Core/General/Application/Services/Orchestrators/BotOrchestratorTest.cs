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
    private int _regCounter;

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        foreach (var d in _disposables)
            d.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    private async Task<ArkWalletDbContext> SeedTokenAsync(long traderId = 0, string symbol = "TKN01")
    {
        var db = DbTest.CreateDbContext();
        _disposables.Add(db);
        await db.Database.EnsureCreatedAsync();
        await HelpMethods.CreateToken(db, symbol, isActive: true);
        await HelpMethods.CreatePriceCandle(db, symbol, 100m, DateTime.UtcNow.AddDays(-2));
        await HelpMethods.CreatePriceCandle(db, symbol, 100m, DateTime.UtcNow);

        if (traderId > 0)
            await SeedPortfolioAsync(db, traderId, symbol);

        return db;
    }

    private static async Task SeedPortfolioAsync(ArkWalletDbContext db, long traderId, string symbol)
    {
        var existing = await db.PortfolioItems
            .FirstOrDefaultAsync(p => p.TraderId == traderId && p.CharacterToken!.Symbol == symbol);
        if (existing != null)
        {
            existing.Quantity += 10;
            await db.SaveChangesAsync();
            return;
        }

        await HelpMethods.AddPortfolio(db, traderId, symbol, 10);
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

private Mock<IMarketMakerBotRegistrationService> CreateRealRegistrationHelper(ArkWalletDbContext db)
    {
        var regMock = new Mock<IMarketMakerBotRegistrationService>();

        // A single counter seeded from the database keeps every generated trader id unique,
        // including the ones the test itself seeded, so no mock can collide on the primary key.
        _regCounter = (int)(db.Traders.Max(t => (long?)t.TelegramId) ?? 0);

        regMock.Setup(r => r.RegisterBotAsync(It.IsAny<string>(), It.IsAny<BotRole>(), It.IsAny<decimal>()))
            .ReturnsAsync((string symbol, BotRole role, decimal power) =>
            {
                var traderId = (long)Interlocked.Increment(ref _regCounter);
                var registration = HelpMethods
                    .RegisterTrader(db, traderId, $"Bot_{role}_{symbol}_{traderId}", isBot: true)
                    .GetAwaiter().GetResult();

                if (!registration.IsSuccess)
                    throw new InvalidOperationException(registration.Message);

                var bot = MarketMakerBotRecord.Create(traderId, symbol, role, power);
                db.MarketMakerBots.Add(bot);
                db.SaveChanges();

                return Result<MarketMakerBotRegistrationData>.Ok(
                    new MarketMakerBotRegistrationData(bot.Id, traderId));
            });

        regMock.Setup(r => r.CreateDedicatedTraderAsync(It.IsAny<string>(), It.IsAny<BotRole>()))
            .ReturnsAsync((string symbol, BotRole role) =>
            {
                var traderId = (long)Interlocked.Increment(ref _regCounter);
                var registration = HelpMethods
                    .RegisterTrader(db, traderId, $"Dedicated_{role}_{symbol}_{traderId}", isBot: true)
                    .GetAwaiter().GetResult();

                if (!registration.IsSuccess)
                    throw new InvalidOperationException(registration.Message);

                return Result<long>.Ok(traderId);
            });

        return regMock;
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
        await SeedPortfolioAsync(db, traderId, "TKN01");

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
        await SeedPortfolioAsync(db, traderId, "TKN01");

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
        await SeedPortfolioAsync(db, traderId, "TKN01");

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

        await HelpMethods.RegisterTrader(db, 100, "MarketMakerBot_TKN_EXISTING", isBot: true);

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
        await HelpMethods.RegisterTrader(db, 150, "SharedTrader", isBot: true);

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
        await HelpMethods.RegisterTrader(db, 160, "SharedXY", isBot: true);

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
    public async Task EnsureDefaultBotsAsync_OrphanBotTrader_RemovedAndOrdersCancelled()
    {
        var db = DbTest.CreateDbContext();
        _disposables.Add(db);
        await db.Database.EnsureCreatedAsync();
        await HelpMethods.CreateToken(db, "TKN_OK2", isActive: true);
        await HelpMethods.RegisterTrader(db, 410, "Buyer", isBot: true);
        await HelpMethods.RegisterTrader(db, 411, "Seller", isBot: true);
        await HelpMethods.RegisterTrader(db, 412, "Waller", isBot: true);
        await HelpMethods.RegisterTrader(db, 413, "WallBlockerLegacy", isBot: true);

        await db.MarketMakerBots.AddAsync(MarketMakerBotRecord.Create(410L, "TKN_OK2", BotRole.Buyer, 50m));
        await db.MarketMakerBots.AddAsync(MarketMakerBotRecord.Create(411L, "TKN_OK2", BotRole.Seller, 50m));
        await db.MarketMakerBots.AddAsync(MarketMakerBotRecord.Create(412L, "TKN_OK2", BotRole.Waller, 100m));
        await db.SaveChangesAsync();

        var cancelMock = new Mock<IOrderCancellationService>();
        cancelMock.Setup(c => c.CancelAllOrderAsync(It.IsAny<long>()))
            .ReturnsAsync(Result<int>.Ok(0));

        var orch = CreateOrchestrator(db, cancellationService: cancelMock);
        var result = await orch.EnsureDefaultBotsAsync();

        Assert.True(result.IsSuccess);
        Assert.True(result.TryGetData(out var data));
        Assert.Equal(1, data.BotsOrphanRemoved);
        Assert.True(data.Changed);
        cancelMock.Verify(c => c.CancelAllOrderAsync(413L), Times.Once);

        Assert.Null(await db.Traders.FindAsync(413L));
        Assert.NotNull(await db.Traders.FindAsync(410L));
        Assert.NotNull(await db.Traders.FindAsync(411L));
        Assert.NotNull(await db.Traders.FindAsync(412L));
    }

    [Fact]
    public async Task EnsureDefaultBotsAsync_NoOrphanBots_ReportsZeroRemoved()
    {
        var db = DbTest.CreateDbContext();
        _disposables.Add(db);
        await db.Database.EnsureCreatedAsync();
        await HelpMethods.CreateToken(db, "TKN_OK3", isActive: true);
        await HelpMethods.RegisterTrader(db, 420, "Buyer", isBot: true);
        await HelpMethods.RegisterTrader(db, 421, "Seller", isBot: true);
        await HelpMethods.RegisterTrader(db, 422, "Waller", isBot: true);

        await db.MarketMakerBots.AddAsync(MarketMakerBotRecord.Create(420L, "TKN_OK3", BotRole.Buyer, 50m));
        await db.MarketMakerBots.AddAsync(MarketMakerBotRecord.Create(421L, "TKN_OK3", BotRole.Seller, 50m));
        await db.MarketMakerBots.AddAsync(MarketMakerBotRecord.Create(422L, "TKN_OK3", BotRole.Waller, 100m));
        await db.SaveChangesAsync();

        var orch = CreateOrchestrator(db);
        var result = await orch.EnsureDefaultBotsAsync();

        Assert.True(result.IsSuccess);
        Assert.True(result.TryGetData(out var data));
        Assert.Equal(0, data.BotsOrphanRemoved);
        Assert.False(data.Changed);
    }

    [Fact]
    public async Task EnsureDefaultBotsAsync_HumanTraderWithoutBotFlag_NotRemoved()
    {
        var db = DbTest.CreateDbContext();
        _disposables.Add(db);
        await db.Database.EnsureCreatedAsync();
        await HelpMethods.CreateToken(db, "TKN_OK4", isActive: true);
        await HelpMethods.RegisterTrader(db, 425, "Buyer", isBot: true);
        await HelpMethods.RegisterTrader(db, 426, "Seller", isBot: true);
        await HelpMethods.RegisterTrader(db, 427, "Waller", isBot: true);
        await HelpMethods.RegisterTrader(db, 430, "RealHuman");

        await db.MarketMakerBots.AddAsync(MarketMakerBotRecord.Create(425L, "TKN_OK4", BotRole.Buyer, 50m));
        await db.MarketMakerBots.AddAsync(MarketMakerBotRecord.Create(426L, "TKN_OK4", BotRole.Seller, 50m));
        await db.MarketMakerBots.AddAsync(MarketMakerBotRecord.Create(427L, "TKN_OK4", BotRole.Waller, 100m));
        await db.SaveChangesAsync();

        var orch = CreateOrchestrator(db);
        var result = await orch.EnsureDefaultBotsAsync();

        Assert.True(result.IsSuccess);
        Assert.True(result.TryGetData(out var data));
        Assert.Equal(0, data.BotsOrphanRemoved);
        Assert.False(data.Changed);
        Assert.NotNull(await db.Traders.FindAsync(430L));
    }

    [Fact]
    public async Task EnsureDefaultBotsAsync_HumanTraderOnInactiveToken_GetsDedicatedTrader()
    {
        var db = DbTest.CreateDbContext();
        _disposables.Add(db);
        await db.Database.EnsureCreatedAsync();
        await HelpMethods.CreateToken(db, "TKN_DEAD", isActive: false);
        await HelpMethods.CreateToken(db, "TKN_LIVE", isActive: true);
        await HelpMethods.RegisterTrader(db, 440, "OwnerLookingTrader");

        await db.MarketMakerBots.AddAsync(MarketMakerBotRecord.Create(440L, "TKN_DEAD", BotRole.Waller, 100m));
        await db.SaveChangesAsync();

        var regMock = new Mock<IMarketMakerBotRegistrationService>();
        regMock.Setup(r => r.RegisterBotAsync(It.IsAny<string>(), It.IsAny<BotRole>(), It.IsAny<decimal>()))
            .ReturnsAsync(Result<MarketMakerBotRegistrationData>.Ok(new MarketMakerBotRegistrationData(940L, 940L)));
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
        Assert.Equal(1, data.BotsMoved);

        var wallBot = await db.MarketMakerBots.SingleAsync(b => b.Symbol == "TKN_DEAD");
        Assert.NotEqual(440L, wallBot.TraderId);
        var movedTrader = await db.Traders.FindAsync(wallBot.TraderId);
        Assert.NotNull(movedTrader);
        Assert.True(movedTrader.IsBot);
        cancelMock.Verify(c => c.CancelAllOrderAsync(440L), Times.Once);
    }

    [Fact]
    public async Task EnsureDefaultBotsAsync_AllDedicated_DoesNotRepair()
    {
        var db = DbTest.CreateDbContext();
        _disposables.Add(db);
        await db.Database.EnsureCreatedAsync();
        await HelpMethods.CreateToken(db, "TKN_OK", isActive: true);
        await HelpMethods.RegisterTrader(db, 210, "TB", isBot: true);
        await HelpMethods.RegisterTrader(db, 211, "TS", isBot: true);
        await HelpMethods.RegisterTrader(db, 212, "TW", isBot: true);

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
        await HelpMethods.RegisterTrader(db, 310, "PB", isBot: true);
        await HelpMethods.RegisterTrader(db, 311, "PS", isBot: true);
        await HelpMethods.RegisterTrader(db, 312, "PW", isBot: true);

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
        await HelpMethods.RegisterTrader(db, 320, "PB", isBot: true);
        await HelpMethods.RegisterTrader(db, 321, "PS", isBot: true);
        await HelpMethods.RegisterTrader(db, 322, "PW", isBot: true);

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
        var db = DbTest.CreateDbContext();
        _disposables.Add(db);
        await db.Database.EnsureCreatedAsync();
        await HelpMethods.CreateToken(db, "TKN01", isActive: true);
        await HelpMethods.CreatePriceCandle(db, "TKN01", 100m, DateTime.UtcNow.AddDays(-2));
        await HelpMethods.CreatePriceCandle(db, "TKN01", 100m, DateTime.UtcNow);
        await HelpMethods.CreateToken(db, "AAA", isActive: true);
        await HelpMethods.CreatePriceCandle(db, "AAA", 200m, DateTime.UtcNow.AddDays(-2));
        await HelpMethods.CreatePriceCandle(db, "AAA", 200m, DateTime.UtcNow);

        var buyerId = 13101L;
        var sellerId = 13102L;
        var wallerId = 13103L;
        await HelpMethods.RegisterTrader(db, buyerId);
        await HelpMethods.RegisterTrader(db, sellerId);
        await HelpMethods.RegisterTrader(db, wallerId);
        await SeedPortfolioAsync(db, sellerId, "TKN01");
        await SeedPortfolioAsync(db, wallerId, "AAA");

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
        var db = DbTest.CreateDbContext();
        _disposables.Add(db);
        await db.Database.EnsureCreatedAsync();
        await HelpMethods.CreateToken(db, "TKN01", isActive: true);
        await HelpMethods.CreatePriceCandle(db, "TKN01", 100m, DateTime.UtcNow.AddDays(-2));
        await HelpMethods.CreatePriceCandle(db, "TKN01", 100m, DateTime.UtcNow);

        var buyerId = 13301L;
        await HelpMethods.RegisterTrader(db, buyerId);
        await SeedPortfolioAsync(db, buyerId, "TKN01");

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
        var db = DbTest.CreateDbContext();
        _disposables.Add(db);
        await db.Database.EnsureCreatedAsync();
        await HelpMethods.CreateToken(db, "TKN01", isActive: true);
        await HelpMethods.CreatePriceCandle(db, "TKN01", 100m, DateTime.UtcNow.AddDays(-2));
        await HelpMethods.CreatePriceCandle(db, "TKN01", 100m, DateTime.UtcNow);

        var buyerId = 13302L;
        await HelpMethods.RegisterTrader(db, buyerId);
        await SeedPortfolioAsync(db, buyerId, "TKN01");

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

    // ═══════ Backoff + preflight checks ═══════

    [Fact]
    public async Task UpdateBotsGridsForRoleAsync_BotNoPortfolio_SkipsAndDoesNotPlaceOrders()
    {
        var db = DbTest.CreateDbContext();
        _disposables.Add(db);
        await db.Database.EnsureCreatedAsync();
        await HelpMethods.CreateToken(db, "TKN_NP", isActive: true);
        await HelpMethods.CreatePriceCandle(db, "TKN_NP", 100m, DateTime.UtcNow.AddDays(-2));
        await HelpMethods.CreatePriceCandle(db, "TKN_NP", 100m, DateTime.UtcNow);

        var sellerId = 14001L;
        await HelpMethods.RegisterTrader(db, sellerId);

        // Бот-продавец создан, но портфеля по символу НЕТ
        var bot = MarketMakerBotRecord.Create(sellerId, "TKN_NP", BotRole.Seller, 50m);
        await db.MarketMakerBots.AddAsync(bot);
        await db.SaveChangesAsync();

        var evMock = new Mock<IEventPublisher>();
        var collMock = new Mock<IOrderCollector>();
        collMock.Setup(c => c.TakeAll()).Returns(() => Array.Empty<IReadOnlyCollection<CreateOrderCommand>>());

        var orch = CreateOrchestrator(db, eventPublisher: evMock, orderCollector: collMock);
        var result = await orch.UpdateBotsGridsForRoleAsync(MarketMakerRole.Seller);

        Assert.True(result.IsSuccess);
        evMock.Verify(e => e.PublishAsync(It.IsAny<BotPublicOrdersEvent>(), It.IsAny<CancellationToken>()), Times.Never);
        collMock.Verify(c => c.Add(It.IsAny<IReadOnlyCollection<CreateOrderCommand>>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteMarketOrdersAsync_BuyerZeroBalance_SkipsBot()
    {
        var db = DbTest.CreateDbContext();
        _disposables.Add(db);
        await db.Database.EnsureCreatedAsync();
        await HelpMethods.CreateToken(db, "TKN_ZB", isActive: true);
        await HelpMethods.CreatePriceCandle(db, "TKN_ZB", 100m, DateTime.UtcNow.AddDays(-2));
        await HelpMethods.CreatePriceCandle(db, "TKN_ZB", 100m, DateTime.UtcNow);

        var buyerId = 14101L;
        await HelpMethods.RegisterTrader(db, buyerId);
        var buyerTrader = await db.Traders.FirstAsync(t => t.TelegramId == buyerId);
        buyerTrader.Balance = 0m;
        await db.SaveChangesAsync();

        var bot = MarketMakerBotRecord.Create(buyerId, "TKN_ZB", BotRole.Buyer, 50m);
        await db.MarketMakerBots.AddAsync(bot);
        await db.SaveChangesAsync();

        var collMock = new Mock<IOrderCollector>();
        collMock.Setup(c => c.TakeAll()).Returns(() => Array.Empty<IReadOnlyCollection<CreateOrderCommand>>());

        var orch = CreateOrchestrator(db, orderCollector: collMock);
        var result = await orch.ExecuteMarketOrdersAsync();

        Assert.True(result.IsSuccess);
        collMock.Verify(c => c.Add(It.IsAny<IReadOnlyCollection<CreateOrderCommand>>()), Times.Never);
    }

    [Fact]
    public async Task UpdateBotsGridsForRoleAsync_BotWithSufficientPortfolio_PlaceOrdersAsBefore()
    {
        var db = DbTest.CreateDbContext();
        _disposables.Add(db);
        await db.Database.EnsureCreatedAsync();
        await HelpMethods.CreateToken(db, "TKN_WP", isActive: true);
        await HelpMethods.CreatePriceCandle(db, "TKN_WP", 100m, DateTime.UtcNow.AddDays(-2));
        await HelpMethods.CreatePriceCandle(db, "TKN_WP", 100m, DateTime.UtcNow);

        var sellerId = 14201L;
        await HelpMethods.RegisterTrader(db, sellerId);
        await SeedPortfolioAsync(db, sellerId, "TKN_WP");

        var bot = MarketMakerBotRecord.Create(sellerId, "TKN_WP", BotRole.Seller, 50m);
        await db.MarketMakerBots.AddAsync(bot);
        await db.SaveChangesAsync();

        var collMock = new Mock<IOrderCollector>();
        collMock.Setup(c => c.TakeAll()).Returns(() => Array.Empty<IReadOnlyCollection<CreateOrderCommand>>());

        var orch = CreateOrchestrator(db, orderCollector: collMock);
        var result = await orch.UpdateBotsGridsForRoleAsync(MarketMakerRole.Seller);

        Assert.True(result.IsSuccess);
        // Регрессия: ордер размещается как раньше — коллбек вызван
        collMock.Verify(c => c.Add(It.IsAny<IReadOnlyCollection<CreateOrderCommand>>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteMarketOrdersAsync_Backoff_CooldownSkipsBotAfterFailures()
    {
        var db = DbTest.CreateDbContext();
        _disposables.Add(db);
        await db.Database.EnsureCreatedAsync();
        await HelpMethods.CreateToken(db, "TKN_BO", isActive: true);
        await HelpMethods.CreatePriceCandle(db, "TKN_BO", 100m, DateTime.UtcNow.AddDays(-2));
        await HelpMethods.CreatePriceCandle(db, "TKN_BO", 100m, DateTime.UtcNow);

        var sellerId = 14301L;
        await HelpMethods.RegisterTrader(db, sellerId);

        // Бот без портфеля → первые 5 тиков подряд пропускается (инкремент счётчика)
        var bot = MarketMakerBotRecord.Create(sellerId, "TKN_BO", BotRole.Seller, 50m);
        await db.MarketMakerBots.AddAsync(bot);
        await db.SaveChangesAsync();

        var collMock = new Mock<IOrderCollector>();
        collMock.Setup(c => c.TakeAll()).Returns(() => Array.Empty<IReadOnlyCollection<CreateOrderCommand>>());

        var orch = CreateOrchestrator(db, orderCollector: collMock);

        // Первые 5 вызовов — бот пропускается, счётчик растёт
        for (int i = 0; i < 6; i++)
        {
            var result = await orch.ExecuteMarketOrdersAsync();
            Assert.True(result.IsSuccess);
            collMock.Verify(c => c.Add(It.IsAny<IReadOnlyCollection<CreateOrderCommand>>()), Times.Never);
            collMock.Invocations.Clear();
        }
    }

    // ─── EnsureDefaultBotsAsync: duplicate role removal ───

    [Fact]
    public async Task EnsureDefaultBotsAsync_DuplicateRolesOnActiveSymbol_RemovesNewestAndPreservesOldest()
    {
        // Scenario A: две записи Buyer для одного символа → удаляется свежая по Id, оставляем старую
        var db = DbTest.CreateDbContext();
        _disposables.Add(db);
        await db.Database.EnsureCreatedAsync();
        await HelpMethods.CreateToken(db, "TKN_DUP", isActive: true);
        await HelpMethods.RegisterTrader(db, 20001L, "TraderDup", isBot: true);

        var olderBuyer = MarketMakerBotRecord.Create(20001L, "TKN_DUP", BotRole.Buyer, 50m);
        await db.MarketMakerBots.AddAsync(olderBuyer);
        await db.SaveChangesAsync();

        // Вторая запись того же роля на том же трейдере и символе
        var newerBuyer = MarketMakerBotRecord.Create(20001L, "TKN_DUP", BotRole.Buyer, 50m);
        await db.MarketMakerBots.AddAsync(newerBuyer);
        await db.SaveChangesAsync();

        // Seller и Waller отсутствуют — будут созданы
        var regMock = CreateRealRegistrationHelper(db);

        var cancelMock = new Mock<IOrderCancellationService>();
        cancelMock.Setup(c => c.CancelAllOrderAsync(It.IsAny<long>()))
            .ReturnsAsync(Result<int>.Ok(0));

        var orch = CreateOrchestrator(db, botRegistration: regMock, cancellationService: cancelMock);
        var result = await orch.EnsureDefaultBotsAsync();

        Assert.True(result.IsSuccess);
        Assert.True(result.TryGetData(out var data));
        Assert.True(data.Changed);
        Assert.Equal(2, data.BotsAdded);          // Seller + Waller
        // Уцелевший Buyer стоял на трейдере вместе с дублем, то есть не на выделенном,
        // поэтому перенос всё равно требуется.
        Assert.Equal(1, data.BotsMoved);
        Assert.Equal(1, data.DuplicatesRemoved);
        // Должно удалиться ровно одно дубликатное
        var buyerCount = await db.MarketMakerBots.CountAsync(b => b.Symbol == "TKN_DUP" && b.Role == BotRole.Buyer);
        Assert.Equal(1, buyerCount);
        // Сохранённый должен быть тем, у которого меньший Id
        var retainedBuyer = await db.MarketMakerBots.FirstAsync(b => b.Symbol == "TKN_DUP" && b.Role == BotRole.Buyer);
        Assert.Equal(olderBuyer.Id, retainedBuyer.Id);
        regMock.Verify(r => r.RegisterBotAsync("TKN_DUP", BotRole.Buyer, It.IsAny<decimal>()), Times.Never);
        regMock.Verify(r => r.RegisterBotAsync("TKN_DUP", BotRole.Seller, 50m), Times.Once);
        regMock.Verify(r => r.RegisterBotAsync("TKN_DUP", BotRole.Waller, 100m), Times.Once);
    }

    [Fact]
    public async Task EnsureDefaultBotsAsync_TripleDuplicateRoles_RemovesTwoNewest()
    {
        // Расширенный кейс: три записи Buyer → остаются только два удаления
        var db = DbTest.CreateDbContext();
        _disposables.Add(db);
        await db.Database.EnsureCreatedAsync();
        await HelpMethods.CreateToken(db, "TKN_TRIP", isActive: true);
        await HelpMethods.RegisterTrader(db, 20010L, "TraderTri", isBot: true);

        var buyers = new List<MarketMakerBotRecord>();
        for (int i = 0; i < 3; i++)
        {
            var bot = MarketMakerBotRecord.Create(20010L, "TKN_TRIP", BotRole.Buyer, 50m);
            await db.MarketMakerBots.AddAsync(bot);
            await db.SaveChangesAsync();
            buyers.Add(bot);
        }

        var regMock = CreateRealRegistrationHelper(db);

        var cancelMock = new Mock<IOrderCancellationService>();
        cancelMock.Setup(c => c.CancelAllOrderAsync(It.IsAny<long>()))
            .ReturnsAsync(Result<int>.Ok(0));

        var orch = CreateOrchestrator(db, botRegistration: regMock, cancellationService: cancelMock);
        var result = await orch.EnsureDefaultBotsAsync();

        Assert.True(result.IsSuccess);
        Assert.True(result.TryGetData(out var data));
        Assert.True(data.Changed);
        Assert.Equal(2, data.BotsAdded);
        Assert.Equal(2, data.DuplicatesRemoved);
        var finalBuyerCount = await db.MarketMakerBots.CountAsync(b => b.Symbol == "TKN_TRIP" && b.Role == BotRole.Buyer);
        Assert.Equal(1, finalBuyerCount);
        var retainedBuyer = await db.MarketMakerBots.FirstAsync(b => b.Symbol == "TKN_TRIP" && b.Role == BotRole.Buyer);
        Assert.Equal(buyers[0].Id, retainedBuyer.Id); // oldest preserved
    }

    // ─── EnsureDefaultBotsAsync: missing role creation ───

    [Fact]
    public async Task EnsureDefaultBotsAsync_MissingRoleOnActiveSymbol_CreatesIt()
    {
        // Scenario B: Seller и Waller на месте, Buyer отсутствует → создаётся один
        var db = DbTest.CreateDbContext();
        _disposables.Add(db);
        await db.Database.EnsureCreatedAsync();
        await HelpMethods.CreateToken(db, "TKN_MIS", isActive: true);
        await HelpMethods.RegisterTrader(db, 20020L, "TraderMisS", isBot: true);
        await HelpMethods.RegisterTrader(db, 20021L, "TraderMisW", isBot: true);

        await db.MarketMakerBots.AddAsync(MarketMakerBotRecord.Create(20020L, "TKN_MIS", BotRole.Seller, 50m));
        await db.MarketMakerBots.AddAsync(MarketMakerBotRecord.Create(20021L, "TKN_MIS", BotRole.Waller, 100m));
        await db.SaveChangesAsync();

        var regMock = CreateRealRegistrationHelper(db);

        var orch = CreateOrchestrator(db, botRegistration: regMock);
        var result = await orch.EnsureDefaultBotsAsync();

        Assert.True(result.IsSuccess);
        Assert.True(result.TryGetData(out var data));
        Assert.True(data.Changed);
        Assert.Equal(1, data.BotsAdded);             // Buyer
        Assert.Equal(0, data.BotsMoved);
        regMock.Verify(r => r.RegisterBotAsync("TKN_MIS", BotRole.Buyer, 50m), Times.Once);
        regMock.Verify(r => r.RegisterBotAsync("TKN_MIS", BotRole.Seller, It.IsAny<decimal>()), Times.Never);
        regMock.Verify(r => r.RegisterBotAsync("TKN_MIS", BotRole.Waller, It.IsAny<decimal>()), Times.Never);
    }

    // ─── EnsureDefaultBotsAsync: multiple symbols, different missing roles ───

    [Fact]
    public async Task EnsureDefaultBotsAsync_MultipleSymbolsWithDifferentMissingRoles_CreatesCorrectly()
    {
        // Scenario C: TKN_A не хватает Buyer, TKN_B не хватает Seller и Waller
        var db = DbTest.CreateDbContext();
        _disposables.Add(db);
        await db.Database.EnsureCreatedAsync();
        await HelpMethods.CreateToken(db, "SYM_A", isActive: true);
        await HelpMethods.CreateToken(db, "SYM_B", isActive: true);
        await HelpMethods.RegisterTrader(db, 20030L, "SymBSeller", isBot: true);
        await HelpMethods.RegisterTrader(db, 20031L, "SymBWaller", isBot: true);

        await db.MarketMakerBots.AddAsync(MarketMakerBotRecord.Create(20030L, "SYM_B", BotRole.Seller, 50m));
        await db.MarketMakerBots.AddAsync(MarketMakerBotRecord.Create(20031L, "SYM_B", BotRole.Waller, 100m));
        await db.SaveChangesAsync();

        var regMock = CreateRealRegistrationHelper(db);

        var orch = CreateOrchestrator(db, botRegistration: regMock);
        var result = await orch.EnsureDefaultBotsAsync();

        Assert.True(result.IsSuccess);
        Assert.True(result.TryGetData(out var data));
        Assert.True(data.Changed);
        Assert.Equal(4, data.BotsAdded);
        // SYM_A пуст — добавляются все три роли; у SYM_B не хватает только Buyer
        regMock.Verify(r => r.RegisterBotAsync("SYM_A", BotRole.Buyer, 50m), Times.Once);
        regMock.Verify(r => r.RegisterBotAsync("SYM_A", BotRole.Seller, 50m), Times.Once);
        regMock.Verify(r => r.RegisterBotAsync("SYM_A", BotRole.Waller, 100m), Times.Once);
        regMock.Verify(r => r.RegisterBotAsync("SYM_B", BotRole.Buyer, 50m), Times.Once);
        regMock.Verify(r => r.RegisterBotAsync("SYM_B", BotRole.Seller, It.IsAny<decimal>()), Times.Never);
        regMock.Verify(r => r.RegisterBotAsync("SYM_B", BotRole.Waller, It.IsAny<decimal>()), Times.Never);

        Assert.Equal(3, await db.MarketMakerBots.CountAsync(b => b.Symbol == "SYM_A"));
        Assert.Equal(3, await db.MarketMakerBots.CountAsync(b => b.Symbol == "SYM_B"));
    }

    // ─── EnsureDefaultBotsAsync: inactive tokens preserved but migrated from human trader ───

    [Fact]
    public async Task EnsureDefaultBotsAsync_InactiveTokenOnHumanTrader_PreservesRecordAndMovesTrader()
    {
        // Scenario D: токен неактивен — запись остаётся в БД, но трейдер мигрируется из human → dedicated bot
        var db = DbTest.CreateDbContext();
        _disposables.Add(db);
        await db.Database.EnsureCreatedAsync();
        await HelpMethods.CreateToken(db, "TKN_INACT", isActive: false);
        await HelpMethods.RegisterTrader(db, 20040L, "HumanInactive");

        await db.MarketMakerBots.AddAsync(MarketMakerBotRecord.Create(20040L, "TKN_INACT", BotRole.Waller, 100m));
        await db.SaveChangesAsync();

        var regMock = new Mock<IMarketMakerBotRegistrationService>();
        regMock.Setup(r => r.RegisterBotAsync(It.IsAny<string>(), It.IsAny<BotRole>(), It.IsAny<decimal>()))
            .ReturnsAsync(Result<MarketMakerBotRegistrationData>.Ok(new MarketMakerBotRegistrationData(9010L, 9010L)));
        regMock.Setup(r => r.CreateDedicatedTraderAsync(It.IsAny<string>(), It.IsAny<BotRole>()))
            .Returns(async (string symbol, BotRole role) =>
            {
                var trader = Trader.Create($"BBot_{symbol}_{role}", isBot: true);
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
        Assert.Equal(1, data.BotsMoved);

        // Запись бота сохраняется даже при неактивном токене
        var wallerBot = await db.MarketMakerBots.SingleAsync(b => b.Symbol == "TKN_INACT");
        Assert.Equal(BotRole.Waller, wallerBot.Role);
        Assert.NotEqual(20040L, wallerBot.TraderId);

        // Новый трейдер — bot trader
        var movedTrader = await db.Traders.FindAsync(wallerBot.TraderId);
        Assert.NotNull(movedTrader);
        Assert.True(movedTrader.IsBot);
    }

    // ─── EnsureDefaultBotsAsync: orphan bot trader removal ───

    [Fact]
    public async Task EnsureDefaultBotsAsync_OrphanBotTraderZeroBots_RemovesTraderAndCancelsOrders()
    {
        // Scenario E: IsBot=true, MarketMakerBots пусто → трейдер удаляется
        var db = DbTest.CreateDbContext();
        _disposables.Add(db);
        await db.Database.EnsureCreatedAsync();
        await HelpMethods.CreateToken(db, "TKN_ORPH", isActive: true);
        await HelpMethods.RegisterTrader(db, 20050L, "OrphanBot", isBot: true);
        await HelpMethods.RegisterTrader(db, 20051L, "OrphanBot2", isBot: true);

        // Никакие MarketMakerBots с этим трейдером не привязаны
        var regMock = CreateRealRegistrationHelper(db);

        var cancelMock = new Mock<IOrderCancellationService>();
        cancelMock.Setup(c => c.CancelAllOrderAsync(It.IsAny<long>()))
            .ReturnsAsync(Result<int>.Ok(0));

        var orch = CreateOrchestrator(db, botRegistration: regMock, cancellationService: cancelMock);
        var result = await orch.EnsureDefaultBotsAsync();

        Assert.True(result.IsSuccess);
        Assert.True(result.TryGetData(out var data));
        Assert.Equal(2, data.BotsOrphanRemoved);
        Assert.True(data.Changed);

        Assert.Null(await db.Traders.FindAsync(20050L));
        Assert.Null(await db.Traders.FindAsync(20051L));
        cancelMock.Verify(c => c.CancelAllOrderAsync(20050L), Times.Once);
        cancelMock.Verify(c => c.CancelAllOrderAsync(20051L), Times.Once);
    }

    // ─── EnsureDefaultBotsAsync: combined dirty state ───

    [Fact]
    public async Task EnsureDefaultBotsAsync_CombinedDirtyState_FixesAllInvariantsInOneRun()
    {
        // Scenario F: несколько активный символов, есть дубликаты, пропущенные роли, орфан трейдеры — всё исправляется за один вызов
        var db = DbTest.CreateDbContext();
        _disposables.Add(db);
        await db.Database.EnsureCreatedAsync();
        await HelpMethods.CreateToken(db, "SYN_MIX", isActive: true);
        await HelpMethods.CreateToken(db, "SYN_MIX2", isActive: true);
        await HelpMethods.RegisterTrader(db, 20060L, "MixedHuman", isBot: false);

        // Дубликат Buyer на SYN_MIX (старый)
        var oldBuyer = MarketMakerBotRecord.Create(20060L, "SYN_MIX", BotRole.Buyer, 50m);
        await db.MarketMakerBots.AddAsync(oldBuyer);
        await db.SaveChangesAsync();

        // Дубликат Buyer на SYN_MIX (новый) — будет удалён
        var newBuyer = MarketMakerBotRecord.Create(20060L, "SYN_MIX", BotRole.Buyer, 50m);
        await db.MarketMakerBots.AddAsync(newBuyer);
        await db.SaveChangesAsync();

        // Орфан Bot-трейдер (без ботов)
        await HelpMethods.RegisterTrader(db, 20065L, "OrphanMix", isBot: true);
        await HelpMethods.RegisterTrader(db, 20066L, "OrphanMix2", isBot: true);

        // На SYN_MIX2 отсутствует Seller
        await HelpMethods.RegisterTrader(db, 20067L, "SynMix2Owner", isBot: true);
        await db.MarketMakerBots.AddAsync(MarketMakerBotRecord.Create(20067L, "SYN_MIX2", BotRole.Buyer, 50m));
        await db.MarketMakerBots.AddAsync(MarketMakerBotRecord.Create(20067L, "SYN_MIX2", BotRole.Waller, 100m));
        await db.SaveChangesAsync();

        var regMock = CreateRealRegistrationHelper(db);

        var cancelMock = new Mock<IOrderCancellationService>();
        cancelMock.Setup(c => c.CancelAllOrderAsync(It.IsAny<long>()))
            .ReturnsAsync(Result<int>.Ok(0));

        var orch = CreateOrchestrator(db, botRegistration: regMock, cancellationService: cancelMock);
        var result = await orch.EnsureDefaultBotsAsync();

        Assert.True(result.IsSuccess);
        Assert.True(result.TryGetData(out var data));
        Assert.True(data.Changed);
        Assert.Equal(3, data.BotsAdded);              // SYN_MIX: Seller+Waller, SYN_MIX2: Seller
        Assert.Equal(1, data.DuplicatesRemoved);       // второй Buyer на SYN_MIX удалён
        Assert.Equal(3, data.BotsOrphanRemoved);       // OrphanMix, OrphanMix2 и SynMix2Owner,
        // у которого обе записи уехали на выделенных трейдеров

        // Проверки итога
        var mixBuyerCount = await db.MarketMakerBots.CountAsync(b => b.Symbol == "SYN_MIX" && b.Role == BotRole.Buyer);
        Assert.Equal(1, mixBuyerCount);
        var mix2SellerCount = await db.MarketMakerBots.CountAsync(b => b.Symbol == "SYN_MIX2" && b.Role == BotRole.Seller);
        Assert.Equal(1, mix2SellerCount);
        Assert.Null(await db.Traders.FindAsync(20065L));
        Assert.Null(await db.Traders.FindAsync(20066L));
    }

    // ─── EnsureDefaultBotsAsync: idempotent when fully correct ───

    [Fact]
    public async Task EnsureDefaultBotsAsync_FullyCorrectState_NoChangesDetected()
    {
        // Scenario G: все символы имеют по три роля, все трейдеры выделены, нет орфанов → Changed=false
        var db = DbTest.CreateDbContext();
        _disposables.Add(db);
        await db.Database.EnsureCreatedAsync();
        await HelpMethods.CreateToken(db, "SYN_IDM", isActive: true);
        await HelpMethods.CreateToken(db, "SYN_IDM2", isActive: true);
        await HelpMethods.RegisterTrader(db, 20070L, "IdmB1", isBot: true);
        await HelpMethods.RegisterTrader(db, 20071L, "IdmS1", isBot: true);
        await HelpMethods.RegisterTrader(db, 20072L, "IdmW1", isBot: true);
        await HelpMethods.RegisterTrader(db, 20073L, "IdmB2", isBot: true);
        await HelpMethods.RegisterTrader(db, 20074L, "IdmS2", isBot: true);
        await HelpMethods.RegisterTrader(db, 20075L, "IdmW2", isBot: true);

        await db.MarketMakerBots.AddRangeAsync(
            MarketMakerBotRecord.Create(20070L, "SYN_IDM", BotRole.Buyer, 50m),
            MarketMakerBotRecord.Create(20071L, "SYN_IDM", BotRole.Seller, 50m),
            MarketMakerBotRecord.Create(20072L, "SYN_IDM", BotRole.Waller, 100m),
            MarketMakerBotRecord.Create(20073L, "SYN_IDM2", BotRole.Buyer, 50m),
            MarketMakerBotRecord.Create(20074L, "SYN_IDM2", BotRole.Seller, 50m),
            MarketMakerBotRecord.Create(20075L, "SYN_IDM2", BotRole.Waller, 100m));
        await db.SaveChangesAsync();

        var regMock = new Mock<IMarketMakerBotRegistrationService>();
        SetupRoleRegistration(regMock, "SYN_IDM", "SYN_IDM2");

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
        Assert.Equal(0, data.BotsOrphanRemoved);
        Assert.Equal(0, data.DuplicatesRemoved);
        regMock.Verify(r => r.RegisterBotAsync(It.IsAny<string>(), It.IsAny<BotRole>(), It.IsAny<decimal>()), Times.Never);
        cancelMock.Verify(c => c.CancelAllOrderAsync(It.IsAny<long>()), Times.Never);
    }

    // ─── Power normalization edge cases ───

    [Fact]
    public async Task EnsureDefaultBotsAsync_MoveToDedicatedBeforeCleanup_NormalizesBothTraders()
    {
        // Extended: сначала миграция из human → dedicated bot, затем power нормализация обоих
        var db = DbTest.CreateDbContext();
        _disposables.Add(db);
        await db.Database.EnsureCreatedAsync();
        await HelpMethods.CreateToken(db, "TKN_NORM", isActive: true);
        await HelpMethods.RegisterTrader(db, 20080L, "NormHuman");

        await db.MarketMakerBots.AddAsync(MarketMakerBotRecord.Create(20080L, "TKN_NORM", BotRole.Buyer, 88m));
        await db.MarketMakerBots.AddAsync(MarketMakerBotRecord.Create(20080L, "TKN_NORM", BotRole.Seller, 44m));
        await db.SaveChangesAsync();

        var regMock = new Mock<IMarketMakerBotRegistrationService>();
        regMock.Setup(r => r.RegisterBotAsync(It.IsAny<string>(), It.IsAny<BotRole>(), It.IsAny<decimal>()))
            .ReturnsAsync(Result<MarketMakerBotRegistrationData>.Ok(new MarketMakerBotRegistrationData(9020L, 9020L)));
        regMock.Setup(r => r.CreateDedicatedTraderAsync(It.IsAny<string>(), It.IsAny<BotRole>()))
            .Returns(async (string symbol, BotRole role) =>
            {
                var trader = Trader.Create($"DDedicated_{symbol}_{role}", isBot: true);
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
        // Buyers/Sellers перемещены (2 шт), потом нормализована сила всех трёх ботов (Buyer+Seller+Waller)
        Assert.True(data.BotsMoved >= 2);
        Assert.True(data.BotsPowerNormalized >= 2);

        // Все существующие боты должны иметь корректную силу после нормализации
        var botsAfter = await db.MarketMakerBots.Where(b => b.Symbol == "TKN_NORM").ToListAsync();
        foreach (var bot in botsAfter)
        {
            if (bot.Role == BotRole.Buyer || bot.Role == BotRole.Seller)
                Assert.Equal(50m, bot.BasePower);
            else if (bot.Role == BotRole.Waller)
                Assert.Equal(100m, bot.BasePower);
        }
    }
}
