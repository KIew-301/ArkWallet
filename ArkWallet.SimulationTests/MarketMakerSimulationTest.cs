using ArkWallet.Core.General.Application.Common;
using ArkWallet.Core.General.Application.Contracts.Other;
using ArkWallet.Core.General.Application.Contracts.Orchestrators;
using ArkWallet.Core.General.Domain.ValueObjects;
using ArkWallet.Core.TradingContext.Application.Contracts.MarketMaker;
using ArkWallet.Core.TradingContext.Application.Contracts.TradeOrderServices;
using ArkWallet.Core.TradingContext.Application.Services.CharacterTokenServices;
using ArkWallet.Core.TradingContext.Application.Services.MarketMaker;
using ArkWallet.Core.General.Application.Services.Orchestrators;
using ArkWallet.Core.PortfolioContext.Application.Services.PortfolioServices;
using ArkWallet.Core.TradingContext.Application.Services.TradeOrderServices;
using ArkWallet.Core.TradingContext.Application.Services.TraderServices;
using ArkWallet.Core.TradingContext.Domain.Engines;
using ArkWallet.Core.TradingContext.Domain.MarketMakerAggregate;
using ArkWallet.Infrastructure;
using ArkWallet.Infrastructure.Data;
using ArkWallet.SimulationTests.HelpTools;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit.Abstractions;
using ArkWallet.Infrastructure.Workers;

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
        var minutes = GetSimulationMinutes();
        var ticks = minutes * 60;
        using var db = DbTest.CreateDbContext();
        db.Database.EnsureCreated();

        var timeProvider = new TestTimeProvider();

        await HelpMethods.CreateToken(db, Symbol, price: 100m);

        // Регистрация ботов через сервис: создаёт трейдеров (isBot=true) и ботов автоматически
        var botLogger = NullLogger<MarketMakerBotRegistrationService>.Instance;
        var botRegistration = new MarketMakerBotRegistrationService(db, botLogger, timeProvider);

        var buyerResult = await botRegistration.RegisterBotAsync(Symbol, BotRole.Buyer);
        Assert.True(buyerResult.IsSuccess, buyerResult.Message);
        Assert.True(buyerResult.TryGetData(out var buyerData));

        var sellerResult = await botRegistration.RegisterBotAsync(Symbol, BotRole.Seller);
        Assert.True(sellerResult.IsSuccess, sellerResult.Message);
        Assert.True(sellerResult.TryGetData(out var sellerData));

        var wallerResult = await botRegistration.RegisterBotAsync(Symbol, BotRole.Waller);
        Assert.True(wallerResult.IsSuccess, wallerResult.Message);
        Assert.True(wallerResult.TryGetData(out var wallerData));

        var balanceService = new TraderBalanceUpdatingService(db, NullLogger<TraderBalanceUpdatingService>.Instance);
        Assert.True((await balanceService.AddToBalanceAsync(buyerData.TraderId, 100_000_000m)).IsSuccess);
        Assert.True((await balanceService.AddToBalanceAsync(sellerData.TraderId, 100_000_000m)).IsSuccess);
        Assert.True((await balanceService.AddToBalanceAsync(wallerData.TraderId, 100_000_000m)).IsSuccess);
        await HelpMethods.AddPortfolio(db, sellerData.TraderId, Symbol, 10_000_000);
        await HelpMethods.AddPortfolio(db, wallerData.TraderId, Symbol, 10_000_000);

        await HelpMethods.CreatePriceCandle(db, Symbol, 100m, timeProvider.Now.AddMinutes(-1).UtcDateTime);

        var orchestrator = BuildOrchestrator(db, timeProvider);
        var recorder = new RecordingBotOrchestrator(orchestrator);

        // --- Worker setup: DI container for BotOrchestratorWorker ---
        var services = new ServiceCollection();
        services.AddSingleton<TimeProvider>(timeProvider);
        services.AddSingleton<ArkWalletDbContext>(db);
        services.AddSingleton<IBotOrchestrator>(recorder);
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        var rootServiceProvider = services.BuildServiceProvider();
        var worker = new BotOrchestratorWorker(rootServiceProvider, NullLogger<BotOrchestratorWorker>.Instance, timeProvider);

        var prevCandles = 0;
        for (int tick = 0; tick < ticks; tick++)
        {
            timeProvider.SkipInSeconds(1);

            if (tick % 60 == 0)
            {
                var tok = await db.CharacterTokens.FirstOrDefaultAsync();
                var mkt = await db.AppStates.FindAsync(new object[] { "BotMarketNextExecution" }, CancellationToken.None);
                var grd = await db.AppStates.FindAsync(new object[] { "BotGridsNextExecution" }, CancellationToken.None);
                var candlesNow = await db.PriceCandles.CountAsync();
                var diagLine = $"[diag {tick} of {ticks}] orders={await db.TradeOrders.CountAsync()} trades={await db.Trades.CountAsync()} candles={candlesNow} tracked={db.ChangeTracker.Entries().Count()} price={tok?.CurrentPrice} marketNext={mkt?.Value} gridNext={grd?.Value} mkCalls={recorder.MarketCalls} mkFails={recorder.MarketFails}";

                if (tick > 0 && candlesNow == prevCandles)
                {
                    var failSnippet = recorder.FailLogSinceReset();
                    if (failSnippet.Length > 0)
                    {
                        diagLine += " FAILS[" + failSnippet + "]";
                    }
                    var price = tok?.CurrentPrice ?? 0m;
                    var buyTarget = FixedGridEngine.RoundToStep(price * 1.2m);
                    var sellTarget = FixedGridEngine.RoundToStep(price * 0.8m);
                    var crossAsks = (await db.TradeOrders
                            .Where(o => o.CharacterTokenId == Symbol && o.Status == OrderStatus.Active && o.Type == OrderType.Sell && o.Price <= buyTarget)
                            .ToListAsync())
                        .OrderBy(o => o.Price)
                        .Select(o => $"{o.Price:N1}x{o.RemainingQuantity}")
                        .ToList();
                    var crossBids = (await db.TradeOrders
                            .Where(o => o.CharacterTokenId == Symbol && o.Status == OrderStatus.Active && o.Type == OrderType.Buy && o.Price >= sellTarget)
                            .ToListAsync())
                        .OrderByDescending(o => o.Price)
                        .Select(o => $"{o.Price:N1}x{o.RemainingQuantity}")
                        .ToList();
                    var allAsks = (await db.TradeOrders
                            .Where(o => o.CharacterTokenId == Symbol && o.Status == OrderStatus.Active && o.Type == OrderType.Sell)
                            .ToListAsync())
                        .OrderBy(o => o.Price).Select(o => o.Price).Take(12).ToList();
                    var allBids = (await db.TradeOrders
                            .Where(o => o.CharacterTokenId == Symbol && o.Status == OrderStatus.Active && o.Type == OrderType.Buy)
                            .ToListAsync())
                        .OrderByDescending(o => o.Price).Select(o => o.Price).Take(12).ToList();
                    diagLine += $" MISS: price={price:N2} buyT={buyTarget:N2} asks<=buyT=[{string.Join(",", crossAsks)}] sellT={sellTarget:N2} bids>=sellT=[{string.Join(",", crossBids)}] allAsks=[{string.Join(",", allAsks)}] allBids=[{string.Join(",", allBids)}]";
                }

                prevCandles = candlesNow;
                File.AppendAllText(Path.Combine(Path.GetTempPath(), "arkwallet_simdiag.log"), diagLine + Environment.NewLine);
                Console.WriteLine(diagLine);
                recorder.ResetMinute();
            }

            await worker.RunScheduledJobsAsync(CancellationToken.None);
        }

        // Проверка привязки ботов к трейдерам
        var simBots = await db.MarketMakerBots
            .Where(b => b.Symbol == Symbol && b.IsActive)
            .OrderBy(b => b.Role)
            .ToListAsync();
        Assert.Equal(3, simBots.Count); // Buyer + Seller + Waller на токен
        var botTraderIds = simBots.Select(b => b.TraderId).Distinct().ToList();
        Assert.Equal(3, botTraderIds.Count); // у каждого бота свой трейдер
        foreach (var bot in simBots)
        {
            var trader = await db.Traders.FirstOrDefaultAsync(t => t.Id == bot.TraderId);
            Assert.NotNull(trader);
            Assert.StartsWith("MarketMakerBot_", trader.Username);
        }

        var candles = await db.PriceCandles
            .Where(c => c.CharacterTokenId == Symbol)
            .OrderBy(c => c.Timestamp)
            .ToListAsync();

        var token = await db.CharacterTokens.SingleAsync(t => t.Symbol == Symbol);
        var trades = await db.Trades.CountAsync();

        _output.WriteLine($"Simulation: {minutes} min ({ticks} ticks of 1s). Final price: {token.CurrentPrice:N2}, trades: {trades}, candles: {candles.Count}");
        _output.WriteLine($"Simulation bots: {simBots.Count} (Buyer/Seller/Waller), each with own Trader");

        var path = SimulationChart.RenderAndOpen(
            $"Market Maker · {Symbol} · ~{minutes} мин ({ticks} тиков по 1 с)",
            $"Начальная цена 1000.00 · Финальная цена {token.CurrentPrice:N2} · Сделок: {trades} · Свечей: {candles.Count} · 5-минутные свечи",
            Symbol,
            candles);

        _output.WriteLine($"Chart: {path}");

        Assert.NotEmpty(candles);
    }

    private static int GetSimulationMinutes()
    {
        var raw = Environment.GetEnvironmentVariable("ARKWALLET_SIM_MINUTES");
        if (int.TryParse(raw, out var minutes) && minutes > 0)
        {
            return minutes;
        }

        return 1440;
    }

    private static IBotOrchestrator BuildOrchestrator(ArkWalletDbContext db, TimeProvider timeProvider)
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
            new MediatREventPublisher(TestMediatorFactory.Create(db, candleUpdateService)),
            mockTaskDispatcher.Object,
            NullLogger<OrderCreationService>.Instance);

    return new BotOrchestrator(
        db,
        new PlanModifierCollection(),
        new MarketMakerBotRegistrationService(db, NullLogger<MarketMakerBotRegistrationService>.Instance, timeProvider),
        new OrderCollector(),
        orderCreationService,
        new UpdatingService(db, NullLogger<UpdatingService>.Instance),
        new OrderCancellationService(db, NullLogger<OrderCancellationService>.Instance),
        new MediatREventPublisher(TestMediatorFactory.Create(db, candleUpdateService)),
        NullLogger<BotOrchestrator>.Instance);
    }
}

internal sealed class RecordingBotOrchestrator(IBotOrchestrator inner) : IBotOrchestrator
{
    public int MarketCalls { get; private set; }
    public int MarketFails { get; private set; }
    private readonly List<string> _failLog = new();

    public void ResetMinute()
    {
        MarketCalls = 0;
        MarketFails = 0;
        _failLog.Clear();
    }

    public string FailLogSinceReset() => string.Join("|", _failLog);

    public async Task<Result> ExecuteMarketOrdersAsync(CancellationToken ct = default)
    {
        MarketCalls++;
        var result = await inner.ExecuteMarketOrdersAsync(ct);
        if (!result.IsSuccess)
        {
            MarketFails++;
            _failLog.Add($"market:{result.Message}");
        }

        return result;
    }

    public Task<Result> UpdateAllBotsBalancesAsync(CancellationToken ct = default) => inner.UpdateAllBotsBalancesAsync(ct);
    public Task<Result> UpdateBotsGridsAsync(CancellationToken ct = default) => inner.UpdateBotsGridsAsync(ct);
    public Task<Result> UpdateWallBotGridsAsync(CancellationToken ct = default) => inner.UpdateWallBotGridsAsync(ct);
    public Task<Result> RebalanceAllBotsPowerAsync(CancellationToken ct = default) => inner.RebalanceAllBotsPowerAsync(ct);
    public Task<Result<BotEnsuringResult>> EnsureDefaultBotsAsync(CancellationToken ct = default) => inner.EnsureDefaultBotsAsync(ct);
}
