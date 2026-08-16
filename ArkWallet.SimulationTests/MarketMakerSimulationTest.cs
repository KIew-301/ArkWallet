using ArkWallet.Application.Contracts.Other;
using ArkWallet.Application.Services.CharacterTokenServices;
using ArkWallet.Application.Services.MarketMaker;
using ArkWallet.Application.Services.Orchestrators;
using ArkWallet.Application.Services.PortfolioServices;
using ArkWallet.Application.Services.TradeOrderServices;
using ArkWallet.Application.Services.TraderServices;
using ArkWallet.Domain.Engines;
using ArkWallet.Domain.Entities;
using ArkWallet.Domain.ValueObjects;
using ArkWallet.Infrastructure;
using ArkWallet.Infrastructure.Data;
using ArkWallet.SimulationTests.HelpTools;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit.Abstractions;

namespace ArkWallet.SimulationTests;

public class MarketMakerSimulationTest
{
    private const string Symbol = "ZZZ";

    private readonly ITestOutputHelper _output;

    public MarketMakerSimulationTest(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task Simulate_TokenWithTwoBots_OutputsPriceHistory()
    {
        var ticks = GetSimulationTicks();
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        var timeProvider = new TestTimeProvider();

        await HelpMethods.RegisterTrader(db, 101);
        await HelpMethods.RegisterTrader(db, 102);
        await HelpMethods.CreateToken(db, Symbol, price: 100m);
        await HelpMethods.GiveMoney(db, 101, 100_000_000m);
        await HelpMethods.GiveMoney(db, 102, 100_000_000m);
        await HelpMethods.AddPortfolio(db, 102, Symbol, 10_000_000);

        var buyerBot = MarketMakerBot.Create(101, Symbol, BotRole.Buyer, 100, timeProvider);
        var sellerBot = MarketMakerBot.Create(102, Symbol, BotRole.Seller, 100, timeProvider);
        await db.MarketMakerBots.AddRangeAsync(buyerBot, sellerBot);
        await db.SaveChangesAsync();

        await HelpMethods.CreatePriceCandle(db, Symbol, 100m, timeProvider.Now.AddMinutes(-1).UtcDateTime);

        var orchestrator = BuildOrchestrator(db, timeProvider);
        var wallBlockerOrchestrator = BuildWallBlockerOrchestrator(db, timeProvider);

        for (int tick = 0; tick < ticks; tick++)
        {
            timeProvider.SkipInSeconds(60);

            var gridResult = await orchestrator.UpdateAllBotsGridAsync();
            Assert.True(gridResult.IsSuccess, gridResult.Message);

            var processResult = await orchestrator.ProcessBotsAsync();
            Assert.True(processResult.IsSuccess, processResult.Message);

            var wallBlockerRegisterResult = await wallBlockerOrchestrator.EnsureRegisteredAsync();
            Assert.True(wallBlockerRegisterResult.IsSuccess, wallBlockerRegisterResult.Message);

            var wallBlockerBalanceResult = await wallBlockerOrchestrator.EnsureBalancesAsync();
            Assert.True(wallBlockerBalanceResult.IsSuccess, wallBlockerBalanceResult.Message);

            var wallBlockerResult = await wallBlockerOrchestrator.ExecuteIterationAsync();
            Assert.True(wallBlockerResult.IsSuccess, wallBlockerResult.Message);
        }

        var candles = await db.PriceCandles
            .Where(c => c.CharacterTokenId == Symbol)
            .OrderBy(c => c.Timestamp)
            .ToListAsync();

        var token = await db.CharacterTokens.SingleAsync(t => t.Symbol == Symbol);
        var trades = await db.Trades.CountAsync();

        _output.WriteLine($"Simulation: {ticks} ticks (~{ticks} min). Final price: {token.CurrentPrice:N2}, trades: {trades}, candles: {candles.Count}");

        var path = SimulationChart.RenderAndOpen(
            $"Market Maker · {Symbol} · {ticks} тиков (~{ticks} мин)",
            $"Начальная цена 1000.00 · Финальная цена {token.CurrentPrice:N2} · Сделок: {trades} · Свечей: {candles.Count} · 5-минутные свечи",
            Symbol,
            candles);

        _output.WriteLine($"Chart: {path}");

        Assert.NotEmpty(candles);
    }

    private static int GetSimulationTicks()
    {
        var raw = Environment.GetEnvironmentVariable("ARKWALLET_SIM_TICKS");
        if (int.TryParse(raw, out var ticks) && ticks > 0)
        {
            return ticks;
        }

        return 180;
    }

    private static MarketMakerOrchestrator BuildOrchestrator(ArkWalletDbContext db, TimeProvider timeProvider)
    {
        var candleUpdateService = new TokenPriceCandleUpdateService(
            db, timeProvider, NullLogger<TokenPriceCandleUpdateService>.Instance);

        var mockTaskDispatcher = new Mock<ITaskDispatcher>();
        mockTaskDispatcher
            .Setup(x => x.SendTaskAsync(It.IsAny<string>(), It.IsAny<object>()))
            .Returns(Task.CompletedTask);

        var orderCreationService = new OrderCreationService(
            db,
            new TradingEngine(timeProvider),
            new OrderValidationService(db),
            new MediatREventPublisher(TestMediatorFactory.Create(db, candleUpdateService)),
            mockTaskDispatcher.Object,
            NullLogger<OrderCreationService>.Instance);

        var marketMakerOrderService = new MarketMakerOrderService(
            db,
            orderCreationService,
            NullLogger<MarketMakerOrderService>.Instance);

        return new MarketMakerOrchestrator(
            db,
            null!,
            null!,
            orderCreationService,
            marketMakerOrderService,
            new MarketMakerGridEngine(new FixedGridEngine()),
            NullLogger<MarketMakerOrchestrator>.Instance,
            timeProvider);
    }

    private static MarketWallBlockerOrchestrator BuildWallBlockerOrchestrator(ArkWalletDbContext db, TimeProvider timeProvider)
    {
        var candleUpdateService = new TokenPriceCandleUpdateService(
            db, timeProvider, NullLogger<TokenPriceCandleUpdateService>.Instance);

        var mockTaskDispatcher = new Mock<ITaskDispatcher>();
        mockTaskDispatcher
            .Setup(x => x.SendTaskAsync(It.IsAny<string>(), It.IsAny<object>()))
            .Returns(Task.CompletedTask);

        var orderCreationService = new OrderCreationService(
            db,
            new TradingEngine(timeProvider),
            new OrderValidationService(db),
            new MediatREventPublisher(TestMediatorFactory.Create(db, candleUpdateService)),
            mockTaskDispatcher.Object,
            NullLogger<OrderCreationService>.Instance);

        return new MarketWallBlockerOrchestrator(
            db,
            new TraderRegistrationService(db, NullLogger<TraderRegistrationService>.Instance),
            new PortfolioUpdatingService(db, NullLogger<PortfolioUpdatingService>.Instance),
            new OrderCancellationService(db, NullLogger<OrderCancellationService>.Instance),
            orderCreationService,
            new WallBlockerEngine(),
            NullLogger<MarketWallBlockerOrchestrator>.Instance,
            timeProvider);
    }
}
