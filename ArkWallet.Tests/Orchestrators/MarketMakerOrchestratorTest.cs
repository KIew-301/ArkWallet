using ArkWallet.Application.Common;
using ArkWallet.Application.Contracts.MarketMaker;
using ArkWallet.Application.Contracts.PortfolioServices;
using ArkWallet.Application.Contracts.TradeOrderServices;
using ArkWallet.Application.Dtos;
using ArkWallet.Application.Services.Orchestrators;
using ArkWallet.Domain.Entities;
using ArkWallet.Domain.Engines;
using ArkWallet.Domain.ValueObjects;
using ArkWallet.Infrastructure.Data;
using ArkWallet.Tests.HelpTools;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Reflection;

namespace ArkWallet.Tests.Orchestrators;

public class MarketMakerOrchestratorTest
{
    private readonly FixedGridEngine _fixedGridEngine = new();

    [Fact]
    public async Task EnsureBotsRegisteredAsync_WhenBotsNotExist_RegistersThem()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.CreateToken(db, "ZZZ", price: 100);

        var mockBotRegistrationService = new Mock<IMarketMakerBotRegistrationService>();

        mockBotRegistrationService
            .Setup(x => x.RegisterBotAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<BotRole>(), It.IsAny<decimal>()))
            .ReturnsAsync(Result<MarketMakerBotRegistrationData>.Ok(new MarketMakerBotRegistrationData(1, 101)));

        var logger = NullLogger<MarketMakerOrchestrator>.Instance;
        var orchestrator = new MarketMakerOrchestrator(
            db,
            mockBotRegistrationService.Object,
            null!,
            null!,
            null!,
            null!,
            logger);

        var result = await orchestrator.EnsureBotsRegisteredAsync();

        Assert.True(result.IsSuccess, result.Message);

        mockBotRegistrationService.Verify(
            x => x.RegisterBotAsync(101, "ZZZ", BotRole.Buyer, 20m),
            Times.Once);
        mockBotRegistrationService.Verify(
            x => x.RegisterBotAsync(102, "ZZZ", BotRole.Seller, 20m),
            Times.Once);
    }

    [Fact]
    public async Task EnsureBotsRegisteredAsync_WhenBotsExist_SkipsRegistration()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.CreateToken(db, "ZZZ", price: 100);

        var bot1 = MarketMakerBot.Create(101, "ZZZ", BotRole.Buyer, 20);
        var bot2 = MarketMakerBot.Create(102, "ZZZ", BotRole.Seller, 20);
        await db.MarketMakerBots.AddRangeAsync(bot1, bot2);
        await db.SaveChangesAsync();

        var mockBotRegistrationService = new Mock<IMarketMakerBotRegistrationService>();

        var logger = NullLogger<MarketMakerOrchestrator>.Instance;
        var orchestrator = new MarketMakerOrchestrator(
            db,
            mockBotRegistrationService.Object,
            null!,
            null!,
            null!,
            null!,
            logger);

        var result = await orchestrator.EnsureBotsRegisteredAsync();

        Assert.True(result.IsSuccess, result.Message);

        mockBotRegistrationService.Verify(
            x => x.RegisterBotAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<BotRole>(), It.IsAny<decimal>()),
            Times.Never);
    }

    [Fact]
    public async Task EnsureBotsRegisteredAsync_WhenFirstBotExists_RegistersOnlySecond()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.CreateToken(db, "ZZZ", price: 100);

        var existingBot = MarketMakerBot.Create(101, "ZZZ", BotRole.Buyer, 20);
        await db.MarketMakerBots.AddAsync(existingBot);
        await db.SaveChangesAsync();

        var mockBotRegistrationService = new Mock<IMarketMakerBotRegistrationService>();

        mockBotRegistrationService
            .Setup(x => x.RegisterBotAsync(102, "ZZZ", BotRole.Seller, 20m))
            .ReturnsAsync(Result<MarketMakerBotRegistrationData>.Ok(new MarketMakerBotRegistrationData(2, 102)));

        var logger = NullLogger<MarketMakerOrchestrator>.Instance;
        var orchestrator = new MarketMakerOrchestrator(
            db,
            mockBotRegistrationService.Object,
            null!,
            null!,
            null!,
            null!,
            logger);

        var result = await orchestrator.EnsureBotsRegisteredAsync();

        Assert.True(result.IsSuccess, result.Message);

        mockBotRegistrationService.Verify(
            x => x.RegisterBotAsync(101, It.IsAny<string>(), It.IsAny<BotRole>(), It.IsAny<decimal>()),
            Times.Never);
        mockBotRegistrationService.Verify(
            x => x.RegisterBotAsync(102, "ZZZ", BotRole.Seller, 20m),
            Times.Once);
    }

    [Fact]
    public async Task EnsureBotsRegisteredAsync_WhenRegistrationFails_ReturnsFail()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.CreateToken(db, "ZZZ", price: 100);

        var mockBotRegistrationService = new Mock<IMarketMakerBotRegistrationService>();

        mockBotRegistrationService
            .Setup(x => x.RegisterBotAsync(101, "ZZZ", BotRole.Buyer, 20m))
            .ReturnsAsync(Result<MarketMakerBotRegistrationData>.Fail("Registration error"));

        var logger = NullLogger<MarketMakerOrchestrator>.Instance;
        var orchestrator = new MarketMakerOrchestrator(
            db,
            mockBotRegistrationService.Object,
            null!,
            null!,
            null!,
            null!,
            logger);

        var result = await orchestrator.EnsureBotsRegisteredAsync();

        Assert.False(result.IsSuccess);
        Assert.Contains("Registration error", result.Message);

        var bots = await db.MarketMakerBots.ToListAsync();
        Assert.Empty(bots);
    }

    [Fact]
    public async Task EnsureBotsRegisteredAsync_MultipleTokens_RegistersBotsForEach()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.CreateToken(db, "AAA", price: 50);
        await HelpMethods.CreateToken(db, "BBB", price: 75);

        var mockBotRegistrationService = new Mock<IMarketMakerBotRegistrationService>();
        mockBotRegistrationService
            .Setup(x => x.RegisterBotAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<BotRole>(), It.IsAny<decimal>()))
            .ReturnsAsync(Result<MarketMakerBotRegistrationData>.Ok(new MarketMakerBotRegistrationData(1, 101)));

        var logger = NullLogger<MarketMakerOrchestrator>.Instance;
        var orchestrator = new MarketMakerOrchestrator(
            db,
            mockBotRegistrationService.Object,
            null!,
            null!,
            null!,
            null!,
            logger);

        var result = await orchestrator.EnsureBotsRegisteredAsync();

        Assert.True(result.IsSuccess, result.Message);

        mockBotRegistrationService.Verify(
            x => x.RegisterBotAsync(101, "AAA", BotRole.Buyer, 20m), Times.Once);
        mockBotRegistrationService.Verify(
            x => x.RegisterBotAsync(102, "AAA", BotRole.Seller, 20m), Times.Once);
        mockBotRegistrationService.Verify(
            x => x.RegisterBotAsync(101, "BBB", BotRole.Buyer, 20m), Times.Once);
        mockBotRegistrationService.Verify(
            x => x.RegisterBotAsync(102, "BBB", BotRole.Seller, 20m), Times.Once);
    }

    [Fact]
    public async Task EnsureTraderBalancesAsync_WhenTraderNotFound_LogsWarningAndContinues()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        var mockPortfolioUpdatingService = new Mock<IPortfolioUpdatingService>();

        var logger = NullLogger<MarketMakerOrchestrator>.Instance;
        var orchestrator = new MarketMakerOrchestrator(
            db,
            null!,
            mockPortfolioUpdatingService.Object,
            null!,
            null!,
            null!,
            logger);

        var result = await orchestrator.EnsureTraderBalancesAsync();

        Assert.True(result.IsSuccess, result.Message);
    }

    [Fact]
    public async Task EnsureTraderBalancesAsync_WhenTraderHasInsufficientBalance_AddsMoney()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.CreateToken(db, "ZZZ", price: 100);

        var trader = Trader.Create(101, "MarketMakerBot_ZZZ_101");
        trader.AddToBalance(500_000_000m);
        await db.Traders.AddAsync(trader);
        await db.SaveChangesAsync();

        var mockPortfolioUpdatingService = new Mock<IPortfolioUpdatingService>();
        mockPortfolioUpdatingService
            .Setup(x => x.CreateOrUpdatePortfolioAsync(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<int>()))
            .ReturnsAsync(Result.Ok());

        var logger = NullLogger<MarketMakerOrchestrator>.Instance;
        var orchestrator = new MarketMakerOrchestrator(
            db,
            null!,
            mockPortfolioUpdatingService.Object,
            null!,
            null!,
            null!,
            logger);

        var result = await orchestrator.EnsureTraderBalancesAsync();

        Assert.True(result.IsSuccess, result.Message);

        var updatedTrader = await db.Traders.FirstAsync(t => t.TelegramId == 101);
        Assert.Equal(1_000_000_000m, updatedTrader.Balance);
    }

    [Fact]
    public async Task EnsureTraderBalancesAsync_WhenTraderHasSufficientBalance_SkipsUpdate()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.CreateToken(db, "ZZZ", price: 100);

        var trader = Trader.Create(101, "MarketMakerBot_ZZZ_101");
        trader.AddToBalance(1_499_999_000m);
        await db.Traders.AddAsync(trader);
        await db.SaveChangesAsync();

        var mockPortfolioUpdatingService = new Mock<IPortfolioUpdatingService>();
        mockPortfolioUpdatingService
            .Setup(x => x.CreateOrUpdatePortfolioAsync(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<int>()))
            .ReturnsAsync(Result.Ok());

        var logger = NullLogger<MarketMakerOrchestrator>.Instance;
        var orchestrator = new MarketMakerOrchestrator(
            db,
            null!,
            mockPortfolioUpdatingService.Object,
            null!,
            null!,
            null!,
            logger);

        var result = await orchestrator.EnsureTraderBalancesAsync();

        Assert.True(result.IsSuccess, result.Message);

        var updatedTrader = await db.Traders.FirstAsync(t => t.TelegramId == 101);
        Assert.Equal(1_500_000_000m, updatedTrader.Balance);
    }

    [Fact]
    public async Task EnsureTraderBalancesAsync_WhenPortfolioMissing_CreatesPortfolio()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.CreateToken(db, "ZZZ", price: 100);

        var trader = Trader.Create(101, "MarketMakerBot_ZZZ_101");
        await db.Traders.AddAsync(trader);
        await db.SaveChangesAsync();

        var mockPortfolioUpdatingService = new Mock<IPortfolioUpdatingService>();
        mockPortfolioUpdatingService
            .Setup(x => x.CreateOrUpdatePortfolioAsync(It.IsAny<long>(), "ZZZ", 100_000_000))
            .ReturnsAsync(Result.Ok());

        var logger = NullLogger<MarketMakerOrchestrator>.Instance;
        var orchestrator = new MarketMakerOrchestrator(
            db,
            null!,
            mockPortfolioUpdatingService.Object,
            null!,
            null!,
            null!,
            logger);

        var result = await orchestrator.EnsureTraderBalancesAsync();

        Assert.True(result.IsSuccess, result.Message);

        mockPortfolioUpdatingService.Verify(
            x => x.CreateOrUpdatePortfolioAsync(101, "ZZZ", 100_000_000),
            Times.Once);
        mockPortfolioUpdatingService.Verify(
            x => x.CreateOrUpdatePortfolioAsync(102, "ZZZ", 100_000_000),
            Times.Once);
    }

    [Fact]
    public async Task EnsureTraderBalancesAsync_WhenPortfolioUpdateFails_ReturnsFail()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.CreateToken(db, "ZZZ", price: 100);

        var trader = Trader.Create(101, "MarketMakerBot_ZZZ_101");
        await db.Traders.AddAsync(trader);
        await db.SaveChangesAsync();

        var mockPortfolioUpdatingService = new Mock<IPortfolioUpdatingService>();
        mockPortfolioUpdatingService
            .Setup(x => x.CreateOrUpdatePortfolioAsync(It.IsAny<long>(), "ZZZ", 100_000_000))
            .ReturnsAsync(Result.Fail("Portfolio update failed"));

        var logger = NullLogger<MarketMakerOrchestrator>.Instance;
        var orchestrator = new MarketMakerOrchestrator(
            db,
            null!,
            mockPortfolioUpdatingService.Object,
            null!,
            null!,
            null!,
            logger);

        var result = await orchestrator.EnsureTraderBalancesAsync();

        Assert.False(result.IsSuccess);
        Assert.Contains("Portfolio update failed", result.Message);
    }

    [Fact]
    public async Task UpdateAllBotsGridAsync_WhenNoBots_ReturnsFail()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        var logger = NullLogger<MarketMakerOrchestrator>.Instance;
        var orchestrator = new MarketMakerOrchestrator(
            db,
            null!,
            null!,
            null!,
            null!,
            null!,
            logger);

        var result = await orchestrator.UpdateAllBotsGridAsync();

        Assert.False(result.IsSuccess);
        Assert.Equal("Список ботов пуст", result.Message);
    }

    [Fact]
    public async Task UpdateAllBotsGridAsync_WhenTokenNotFound_ReturnsFail()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        var bot = MarketMakerBot.Create(101, "ZZZ", BotRole.Buyer, 20);
        await db.MarketMakerBots.AddAsync(bot);
        await db.SaveChangesAsync();

        var logger = NullLogger<MarketMakerOrchestrator>.Instance;
        var orchestrator = new MarketMakerOrchestrator(
            db,
            null!,
            null!,
            null!,
            null!,
            null!,
            logger);

        var result = await orchestrator.UpdateAllBotsGridAsync();

        Assert.False(result.IsSuccess);
        Assert.Equal("Токен ZZZ не найден", result.Message);
    }

    [Fact]
    public async Task UpdateAllBotsGridAsync_WhenTokenExists_UpdatesGrid()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.CreateToken(db, "ZZZ", price: 100);

        var bot = MarketMakerBot.Create(101, "ZZZ", BotRole.Buyer, 20);
        await db.MarketMakerBots.AddAsync(bot);
        await db.SaveChangesAsync();

        var fixedGridEngine = new FixedGridEngine();
        var marketMakerGridEngine = new MarketMakerGridEngine(fixedGridEngine);

        var mockOrderCreationService = new Mock<IOrderCreationService>();
        mockOrderCreationService
            .Setup(x => x.CreateOrdersAsync(It.IsAny<IEnumerable<CreateOrderCommand>>()))
            .ReturnsAsync(Result<List<OrderCreationData>>.Ok(new List<OrderCreationData>
            {
                new(true, new OrderDto("1", OrderType.Buy, 101, "ZZZ", 6, 100, OrderStatus.Active, DateTime.UtcNow))
            }));

        var logger = NullLogger<MarketMakerOrchestrator>.Instance;
        var orchestrator = new MarketMakerOrchestrator(
            db,
            null!,
            null!,
            mockOrderCreationService.Object,
            null!,
            marketMakerGridEngine,
            logger);

        var result = await orchestrator.UpdateAllBotsGridAsync();

        Assert.True(result.IsSuccess, result.Message);

        mockOrderCreationService.Verify(
            x => x.CreateOrdersAsync(It.IsAny<IEnumerable<CreateOrderCommand>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task UpdateAllBotsGridAsync_WhenOrderCreationFails_Continues()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.CreateToken(db, "ZZZ", price: 100);

        var bot = MarketMakerBot.Create(101, "ZZZ", BotRole.Buyer, 20);
        await db.MarketMakerBots.AddAsync(bot);
        await db.SaveChangesAsync();

        var fixedGridEngine = new FixedGridEngine();
        var marketMakerGridEngine = new MarketMakerGridEngine(fixedGridEngine);

        var mockOrderCreationService = new Mock<IOrderCreationService>();
        mockOrderCreationService
            .Setup(x => x.CreateOrdersAsync(It.IsAny<IEnumerable<CreateOrderCommand>>()))
            .ReturnsAsync(Result<List<OrderCreationData>>.Fail("Order creation failed"));

        var logger = NullLogger<MarketMakerOrchestrator>.Instance;
        var orchestrator = new MarketMakerOrchestrator(
            db,
            null!,
            null!,
            mockOrderCreationService.Object,
            null!,
            marketMakerGridEngine,
            logger);

        var result = await orchestrator.UpdateAllBotsGridAsync();

        Assert.True(result.IsSuccess, result.Message);
    }

    [Fact]
    public async Task ProcessBotsAsync_WhenNoBots_ReturnsFail()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        var logger = NullLogger<MarketMakerOrchestrator>.Instance;
        var orchestrator = new MarketMakerOrchestrator(
            db,
            null!,
            null!,
            null!,
            null!,
            null!,
            logger);

        var result = await orchestrator.ProcessBotsAsync();

        Assert.False(result.IsSuccess);
        Assert.Equal("Список ботов пуст", result.Message);
    }

    [Fact]
    public async Task ProcessBotsAsync_WhenBotsExist_UpdatesPowerAndGrid()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.CreateToken(db, "ZZZ", price: 100);

        var bot = MarketMakerBot.Create(101, "ZZZ", BotRole.Buyer, 20);

        var nextPowerField = typeof(MarketMakerBot).GetField("<NextPowerChange>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
        var nextRebalanceField = typeof(MarketMakerBot).GetField("<NextRebalance>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);

        nextPowerField?.SetValue(bot, DateTime.UtcNow.AddMinutes(-1));
        nextRebalanceField?.SetValue(bot, DateTime.UtcNow.AddMinutes(-1));

        await db.MarketMakerBots.AddAsync(bot);
        await db.SaveChangesAsync();

        var fixedGridEngine = new FixedGridEngine();
        var marketMakerGridEngine = new MarketMakerGridEngine(fixedGridEngine);

        var mockOrderCreationService = new Mock<IOrderCreationService>();
        mockOrderCreationService
            .Setup(x => x.CreateOrdersAsync(It.IsAny<IEnumerable<CreateOrderCommand>>()))
            .ReturnsAsync(Result<List<OrderCreationData>>.Ok(new List<OrderCreationData>
            {
                new(true, new OrderDto("1", OrderType.Buy, 101, "ZZZ", 6, 100, OrderStatus.Active, DateTime.UtcNow))
            }));

        var mockMarketMakerOrderService = new Mock<IMarketMakerOrderService>();
        mockMarketMakerOrderService
            .Setup(x => x.ExecuteMarketOrderAsync(It.IsAny<long>(), It.IsAny<string>()))
            .ReturnsAsync(Result.Ok());

        var logger = NullLogger<MarketMakerOrchestrator>.Instance;
        var orchestrator = new MarketMakerOrchestrator(
            db,
            null!,
            null!,
            mockOrderCreationService.Object,
            mockMarketMakerOrderService.Object,
            marketMakerGridEngine,
            logger);

        var result = await orchestrator.ProcessBotsAsync();

        Assert.True(result.IsSuccess, result.Message);

        mockOrderCreationService.Verify(
            x => x.CreateOrdersAsync(It.IsAny<IEnumerable<CreateOrderCommand>>()),
            Times.AtLeastOnce);

        mockMarketMakerOrderService.Verify(
            x => x.ExecuteMarketOrderAsync(It.IsAny<long>(), It.IsAny<string>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task ProcessBotsAsync_WhenMarketOrderFails_LogsAndContinues()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.CreateToken(db, "ZZZ", price: 100);

        var bot = MarketMakerBot.Create(101, "ZZZ", BotRole.Buyer, 20);

        var nextPowerField = typeof(MarketMakerBot).GetField("<NextPowerChange>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
        nextPowerField?.SetValue(bot, DateTime.UtcNow.AddMinutes(-1));

        await db.MarketMakerBots.AddAsync(bot);
        await db.SaveChangesAsync();

        var fixedGridEngine = new FixedGridEngine();
        var marketMakerGridEngine = new MarketMakerGridEngine(fixedGridEngine);

        var mockOrderCreationService = new Mock<IOrderCreationService>();
        mockOrderCreationService
            .Setup(x => x.CreateOrdersAsync(It.IsAny<IEnumerable<CreateOrderCommand>>()))
            .ReturnsAsync(Result<List<OrderCreationData>>.Ok(new List<OrderCreationData>
            {
                new(true, new OrderDto("1", OrderType.Buy, 101, "ZZZ", 6, 100, OrderStatus.Active, DateTime.UtcNow))
            }));

        var mockMarketMakerOrderService = new Mock<IMarketMakerOrderService>();
        mockMarketMakerOrderService
            .Setup(x => x.ExecuteMarketOrderAsync(It.IsAny<long>(), It.IsAny<string>()))
            .ReturnsAsync(Result.Fail("Market order failed"));

        var logger = NullLogger<MarketMakerOrchestrator>.Instance;
        var orchestrator = new MarketMakerOrchestrator(
            db,
            null!,
            null!,
            mockOrderCreationService.Object,
            mockMarketMakerOrderService.Object,
            marketMakerGridEngine,
            logger);

        var result = await orchestrator.ProcessBotsAsync();

        Assert.True(result.IsSuccess, result.Message);

        mockMarketMakerOrderService.Verify(
            x => x.ExecuteMarketOrderAsync(It.IsAny<long>(), It.IsAny<string>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task EnsureBotsRegisteredAsync_WhenExceptionThrown_ReturnsFail()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.CreateToken(db, "ZZZ", price: 100);

        var mockBotRegistrationService = new Mock<IMarketMakerBotRegistrationService>();
        mockBotRegistrationService
            .Setup(x => x.RegisterBotAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<BotRole>(), It.IsAny<decimal>()))
            .ThrowsAsync(new InvalidOperationException("db connection lost"));

        var logger = NullLogger<MarketMakerOrchestrator>.Instance;
        var orchestrator = new MarketMakerOrchestrator(
            db,
            mockBotRegistrationService.Object,
            null!,
            null!,
            null!,
            null!,
            logger);

        var result = await orchestrator.EnsureBotsRegisteredAsync();

        Assert.False(result.IsSuccess);
        Assert.Contains("db connection lost", result.Message);
    }

    [Fact]
    public async Task EnsureTraderBalancesAsync_WhenPortfolioUpdateThrowsException_ReturnsFail()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        await HelpMethods.CreateToken(db, "ZZZ", price: 100);

        var trader = Trader.Create(101, "Bot");
        await db.Traders.AddAsync(trader);
        await db.SaveChangesAsync();

        var mockPortfolioUpdatingService = new Mock<IPortfolioUpdatingService>();
        mockPortfolioUpdatingService
            .Setup(x => x.CreateOrUpdatePortfolioAsync(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<int>()))
            .ThrowsAsync(new InvalidOperationException("portfolio error"));

        var logger = NullLogger<MarketMakerOrchestrator>.Instance;
        var orchestrator = new MarketMakerOrchestrator(
            db,
            null!,
            mockPortfolioUpdatingService.Object,
            null!,
            null!,
            null!,
            logger);

        var result = await orchestrator.EnsureTraderBalancesAsync();

        Assert.False(result.IsSuccess);
        Assert.Contains("portfolio error", result.Message);
    }

    [Fact]
    public async Task UpdateAllBotsGridAsync_WhenUpdateBotGridThrowsException_ReturnsFail()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        var bot = MarketMakerBot.Create(101, "ZZZ", BotRole.Buyer, 20);
        await db.MarketMakerBots.AddAsync(bot);
        await db.SaveChangesAsync();

        await HelpMethods.CreateToken(db, "ZZZ", price: 100);

        var mockOrderCreationService = new Mock<IOrderCreationService>();
        mockOrderCreationService
            .Setup(x => x.CreateOrdersAsync(It.IsAny<IEnumerable<CreateOrderCommand>>()))
            .ThrowsAsync(new InvalidOperationException("order error"));

        var fixedGridEngine = new FixedGridEngine();
        var marketMakerGridEngine = new MarketMakerGridEngine(fixedGridEngine);

        var logger = NullLogger<MarketMakerOrchestrator>.Instance;
        var orchestrator = new MarketMakerOrchestrator(
            db,
            null!,
            null!,
            mockOrderCreationService.Object,
            null!,
            marketMakerGridEngine,
            logger);

        var result = await orchestrator.UpdateAllBotsGridAsync();

        Assert.False(result.IsSuccess);
        Assert.Contains("order error", result.Message);
    }

    [Fact]
    public async Task ProcessBotsAsync_WhenExceptionThrown_ReturnsFail()
    {
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        var bot = MarketMakerBot.Create(101, "ZZZ", BotRole.Buyer, 20);
        await db.MarketMakerBots.AddAsync(bot);
        await db.SaveChangesAsync();

        await HelpMethods.CreateToken(db, "ZZZ", price: 100);

        var mockOrderCreationService = new Mock<IOrderCreationService>();
        mockOrderCreationService
            .Setup(x => x.CreateOrdersAsync(It.IsAny<IEnumerable<CreateOrderCommand>>()))
            .ThrowsAsync(new InvalidOperationException("process error"));

        var fixedGridEngine = new FixedGridEngine();
        var marketMakerGridEngine = new MarketMakerGridEngine(fixedGridEngine);

        var logger = NullLogger<MarketMakerOrchestrator>.Instance;
        var orchestrator = new MarketMakerOrchestrator(
            db,
            null!,
            null!,
            mockOrderCreationService.Object,
            null!,
            marketMakerGridEngine,
            logger);

        var result = await orchestrator.ProcessBotsAsync();

        Assert.False(result.IsSuccess);
        Assert.Contains("process error", result.Message);
    }
}