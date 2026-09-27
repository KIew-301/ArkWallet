using ArkWallet.Core.TradingContext.Application.Services.CharacterTokenServices;
using ArkWallet.Core.TradingContext.Application.Services.MarketMaker;
using ArkWallet.Core.General.Application.Services.Orchestrators;
using ArkWallet.Core.TradingContext.Application.Services.TradeOrderServices;
using ArkWallet.Core.General.Application.Contracts.Orchestrators;
using ArkWallet.Core.TradingContext.Application.Contracts.TradeOrderServices;
using ArkWallet.Core.TradingContext.Domain.Engines;
using ArkWallet.Infrastructure;
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
            var result = await orchestrator.UpdateBotsGridsAsync();
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
            await warmupOrchestrator.UpdateBotsGridsAsync();
        });
    }

    private static IBotOrchestrator BuildOrchestrator(ArkWalletDbContext db)
    {
        var candleUpdateService = new TokenPriceCandleUpdateService(
            db, TimeProvider.System, NullLogger<TokenPriceCandleUpdateService>.Instance);

        var orderCreationService = new OrderCreationService(
            db,
            new TradingEngine(),
            new MediatREventPublisher(TestMediatorFactory.Create(db, candleUpdateService)),
            new FakeTaskDispatcher(),
            NullLogger<OrderCreationService>.Instance);

        return new BotOrchestrator(
            db,
            new PlanModifierCollection(),
            new OrderCollector(),
            orderCreationService,
            null!,
            null!,
            new MediatREventPublisher(TestMediatorFactory.Create(db, candleUpdateService)),
            NullLogger<BotOrchestrator>.Instance);
    }
}
