using ArkWallet.Application.Services.CharacterTokenServices;
using ArkWallet.Application.Services.MarketMaker;
using ArkWallet.Application.Services.Orchestrators;
using ArkWallet.Application.Services.TradeOrderServices;
using ArkWallet.Domain.Engines;
using ArkWallet.Infrastructure.Data;
using ArkWallet.PerformanceTests.Helpers;
using ArkWallet.PerformanceTests.Measurement;
using Microsoft.Extensions.Logging.Abstractions;

namespace ArkWallet.PerformanceTests.Gates;

[Collection("Perf")]
public class MarketMakerTickGateTests
{
    [Theory]
    [InlineData(10, "market-maker-tick-10t")]
    [InlineData(20, "market-maker-tick-20t")]
    public async Task ProcessBotsAsync_WithTokens_StaysWithinQueryBudget(int tokenCount, string scenario)
    {
        var budget = BudgetFor(tokenCount);
        await WarmUpAsync();
        var counter = new QueryCounter();
        var saveChangesCounter = new SaveChangesCounter();
        using var db = PerfDb.CreateDbContext(counter, saveChangesCounter);
        await db.Database.EnsureCreatedAsync();
        await GatesSeed.SeedMarketMakerScenarioAsync(db, tokenCount);

        var orchestrator = BuildOrchestrator(db);
        counter.Reset();
        saveChangesCounter.Reset();

        using var scope = new PerfScope(counter);
        using (scope.Step($"ProcessBotsAsync({tokenCount}t)"))
        {
            var result = await orchestrator.ProcessBotsAsync();
            Assert.True(result.IsSuccess, result.Message);
        }

        GateAssert.QueryBudget(scenario, budget, counter, scope, saveChangesCounter);
    }

    private static Budget BudgetFor(int tokenCount)
        => tokenCount == 20 ? GateBudgets.MarketMakerTick20T : GateBudgets.MarketMakerTick10T;

    private static async Task WarmUpAsync()
    {
        await PerfWarmup.WithDbAsync(async warmupDb =>
        {
            await GatesSeed.SeedMarketMakerScenarioAsync(warmupDb, 1);
            var warmupOrchestrator = BuildOrchestrator(warmupDb);
            await warmupOrchestrator.ProcessBotsAsync();
        });
    }

    private static MarketMakerOrchestrator BuildOrchestrator(ArkWalletDbContext db)
    {
        var candleUpdateService = new TokenPriceCandleUpdateService(
            db, TimeProvider.System, NullLogger<TokenPriceCandleUpdateService>.Instance);

        var orderCreationService = new OrderCreationService(
            db,
            new TradingEngine(),
            new OrderValidationService(db),
            candleUpdateService,
            new FakeTaskDispatcher(),
            NullLogger<OrderCreationService>.Instance);

        var marketMakerOrderService = new MarketMakerOrderService(
            db, orderCreationService, NullLogger<MarketMakerOrderService>.Instance);

        return new MarketMakerOrchestrator(
            db,
            null!,
            null!,
            orderCreationService,
            marketMakerOrderService,
            new MarketMakerGridEngine(new FixedGridEngine()),
            NullLogger<MarketMakerOrchestrator>.Instance);
    }
}
