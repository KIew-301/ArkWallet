using ArkWallet.Application.Contracts.TradeOrderServices;
using ArkWallet.Application.Services.CharacterTokenServices;
using ArkWallet.Application.Services.Leaders;
using ArkWallet.Application.Services.MarketMaker;
using ArkWallet.Application.Services.Orchestrators;
using ArkWallet.Application.Services.TradeOrderServices;
using ArkWallet.Application.Services.TraderServices;
using ArkWallet.Domain.Engines;
using ArkWallet.Infrastructure.Data;
using ArkWallet.PerformanceTests.Helpers;
using ArkWallet.PerformanceTests.Measurement;
using Microsoft.Extensions.Logging.Abstractions;

namespace ArkWallet.PerformanceTests.Repeats;

internal static class ScenarioBodies
{
    private const string Symbol = "TKN000";
    private const long TraderId = 101;
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
            var result = await service.TakeMainBalanceChanges(TraderId, 1);
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
            var result = await service.TakeTotalBalanceChanges(TraderId, 1);
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
        var service = BuildOrderService(db);

        counter.Reset();
        using var scope = new PerfScope(counter);
        using (scope.Step(direction == "купить" ? "CreateBuyOrder" : "CreateSellOrder"))
        {
            var result = await service.CreateOrderAsync(new CreateOrderCommand(TraderId, direction, Symbol, 10, 1000m));
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
            var result = await orchestrator.ProcessBotsAsync();
            if (!result.IsSuccess)
                throw new InvalidOperationException(result.Message);
        }

        return scope.Report();
    }

    private static async Task<ArkWalletDbContext> CreateBalanceSeededDbAsync(QueryCounter counter)
    {
        var db = PerfDb.CreateDbContext(counter);
        await db.Database.EnsureCreatedAsync();
        await GatesSeed.SeedTraderAsync(db, TraderId, 3500m);
        await GatesSeed.SaveBalanceSnapshotAsync(db, TraderId, 1000m, DateTime.UtcNow.AddDays(-7));
        await GatesSeed.SaveBalanceSnapshotAsync(db, TraderId, 1500m, DateTime.UtcNow.AddDays(-1));
        await GatesSeed.SeedTokenCatalogAsync(db, 1);
        await GatesSeed.SeedTraderPortfolioAsync(db, TraderId, Symbol, 10);
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
        await GatesSeed.SeedTraderAsync(db, TraderId, TraderBalance);
        await GatesSeed.SeedTokenCatalogAsync(db, 1);
        await GatesSeed.SeedTraderPortfolioAsync(db, TraderId, Symbol, 1_000_000);
        return db;
    }

    private static OrderCreationService BuildOrderService(ArkWalletDbContext db)
    {
        var candleUpdateService = new TokenPriceCandleUpdateService(
            db, TimeProvider.System, NullLogger<TokenPriceCandleUpdateService>.Instance);

        return new OrderCreationService(
            db,
            new TradingEngine(),
            new OrderValidationService(db),
            candleUpdateService,
            new FakeTaskDispatcher(),
            NullLogger<OrderCreationService>.Instance);
    }

    private static MarketMakerOrchestrator BuildMmOrchestrator(ArkWalletDbContext db)
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
            NullLogger<MarketMakerOrchestrator>.Instance);
    }
}
