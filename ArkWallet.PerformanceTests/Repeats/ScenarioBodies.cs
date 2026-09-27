using ArkWallet.Core.General.Application.Services.Leaders;
using ArkWallet.Core.General.Application.Services.Orchestrators;
using ArkWallet.Core.General.Application.Common;
using ArkWallet.Core.General.Application.Contracts.Orchestrators;
using ArkWallet.Core.TradingContext.Application.Contracts.TradeOrderServices;
using ArkWallet.Core.TradingContext.Application.Services.CharacterTokenServices;
using ArkWallet.Core.TradingContext.Application.Services.MarketMaker;
using ArkWallet.Core.TradingContext.Application.Services.TradeOrderServices;
using ArkWallet.Core.TradingContext.Application.Services.TraderServices;
using ArkWallet.Core.TradingContext.Domain.Engines;
using ArkWallet.Infrastructure;
using ArkWallet.Infrastructure.Data;
using ArkWallet.PerformanceTests.Helpers;
using ArkWallet.PerformanceTests.Measurement;
using Microsoft.Extensions.Logging.Abstractions;

namespace ArkWallet.PerformanceTests.Repeats;

internal static class ScenarioBodies
{
    private const string Symbol = "TKN000";
    private const long TraderTelegramId = 101;
    private const decimal TraderBalance = 100_000_000m;

    public static async Task<PerfReport> TokenQueryAsync(QueryCounter counter)
    {
        using var db = PerfDb.CreateDbContext(counter);
        await db.Database.EnsureCreatedAsync();
        await GatesSeed.SeedTokenCatalogAsync(db, 50);

        var service = new TokenQueryService(db, TimeProvider.System, NullLogger<TokenQueryService>.Instance);

        counter.Reset();
        using var scope = new PerfScope(counter);
        using (scope.Step("GetAllActiveTokensAsync"))
        {
            var result = await service.GetAllActiveTokensAsync();
            if (!result.IsSuccess)
                throw new InvalidOperationException(result.Message);
        }

        return scope.Report();
    }

    public static async Task<PerfReport> BalanceMainAsync(QueryCounter counter)
    {
        using var db = await CreateBalanceSeededDbAsync(counter);
        var service = BuildBalanceService(db);

        counter.Reset();
        using var scope = new PerfScope(counter);
        using (scope.Step("TakeMainBalanceChanges"))
        {
            var result = await service.TakeMainBalanceChanges(TraderTelegramId, 1);
            if (!result.IsSuccess)
                throw new InvalidOperationException(result.Message);
        }

        return scope.Report();
    }

    public static async Task<PerfReport> BalanceTotalAsync(QueryCounter counter)
    {
        using var db = await CreateBalanceSeededDbAsync(counter);
        var service = BuildBalanceService(db);

        counter.Reset();
        using var scope = new PerfScope(counter);
        using (scope.Step("TakeTotalBalanceChanges"))
        {
            var result = await service.TakeTotalBalanceChanges(TraderTelegramId, 1);
            if (!result.IsSuccess)
                throw new InvalidOperationException(result.Message);
        }

        return scope.Report();
    }

    public static async Task<PerfReport> LeadersTopAsync(QueryCounter counter)
    {
        using var db = PerfDb.CreateDbContext(counter);
        await db.Database.EnsureCreatedAsync();
        await GatesSeed.SeedLeaderboardAsync(db, 50);

        var snapshotService = new BalanceSnapshotService(db, NullLogger<BalanceSnapshotService>.Instance);
        var service = new LeadersTopByBalanceQueryService(db, snapshotService, NullLogger<LeadersTopByBalanceQueryService>.Instance);

        counter.Reset();
        using var scope = new PerfScope(counter);
        using (scope.Step("GetTopAsync(10)"))
        {
            var result = await service.GetTopAsync(10);
            if (!result.IsSuccess)
                throw new InvalidOperationException(result.Message);
        }

        return scope.Report();
    }

    public static async Task<PerfReport> OrderCreateAsync(QueryCounter counter, string direction)
    {
        using var db = await CreateOrderSeededDbAsync(counter);
        long realTraderId = db.Traders.First().Id;
        var service = BuildOrderService(db);

        counter.Reset();
        using var scope = new PerfScope(counter);
        using (scope.Step(direction == "купить" ? "CreateBuyOrder" : "CreateSellOrder"))
        {
            var result = await service.CreateOrderAsync(new CreateOrderCommand(realTraderId, direction, Symbol, 10, 1000m));
            if (!result.IsSuccess)
                throw new InvalidOperationException(result.Message);
        }

        return scope.Report();
    }

    public static async Task<PerfReport> MmTickAsync(QueryCounter counter, int tokenCount)
    {
        using var db = PerfDb.CreateDbContext(counter);
        await db.Database.EnsureCreatedAsync();
        await GatesSeed.SeedMarketMakerScenarioAsync(db, tokenCount);

        var orchestrator = BuildMmOrchestrator(db);

        counter.Reset();
        using var scope = new PerfScope(counter);
        using (scope.Step($"ProcessBotsAsync({tokenCount}t)"))
        {
            var result = await orchestrator.UpdateBotsGridsAsync();
            if (!result.IsSuccess)
                throw new InvalidOperationException(result.Message);
        }

        return scope.Report();
    }

    private static async Task<ArkWalletDbContext> CreateBalanceSeededDbAsync(QueryCounter counter)
    {
        var db = PerfDb.CreateDbContext(counter);
        await db.Database.EnsureCreatedAsync();
        await GatesSeed.SeedTraderAsync(db, TraderTelegramId, 3500m);
        await GatesSeed.SaveBalanceSnapshotAsync(db, TraderTelegramId, 1000m, DateTime.UtcNow.AddDays(-7));
        await GatesSeed.SaveBalanceSnapshotAsync(db, TraderTelegramId, 1500m, DateTime.UtcNow.AddDays(-1));
        await GatesSeed.SeedTokenCatalogAsync(db, 1);
        await GatesSeed.SeedTraderPortfolioAsync(db, TraderTelegramId, Symbol, 10);
        return db;
    }

    private static BalanceChangesCalculationService BuildBalanceService(ArkWalletDbContext db)
    {
        var snapshotService = new BalanceSnapshotService(db, NullLogger<BalanceSnapshotService>.Instance);
        return new BalanceChangesCalculationService(db, snapshotService, NullLogger<BalanceChangesCalculationService>.Instance);
    }

    private static async Task<ArkWalletDbContext> CreateOrderSeededDbAsync(QueryCounter counter)
    {
        var db = PerfDb.CreateDbContext(counter);
        await db.Database.EnsureCreatedAsync();
        await GatesSeed.SeedTraderAsync(db, TraderTelegramId, TraderBalance);
        await GatesSeed.SeedTokenCatalogAsync(db, 1);
        await GatesSeed.SeedTraderPortfolioAsync(db, TraderTelegramId, Symbol, 1_000_000);
        return db;
    }

    private static OrderCreationService BuildOrderService(ArkWalletDbContext db)
    {
        var candleUpdateService = new TokenPriceCandleUpdateService(
            db, TimeProvider.System, NullLogger<TokenPriceCandleUpdateService>.Instance);

        return new OrderCreationService(
            db,
            new TradingEngine(),
            new MediatREventPublisher(TestMediatorFactory.Create(db, candleUpdateService)),
            new FakeTaskDispatcher(),
            NullLogger<OrderCreationService>.Instance);
    }

    private static IBotOrchestrator BuildMmOrchestrator(ArkWalletDbContext db)
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
