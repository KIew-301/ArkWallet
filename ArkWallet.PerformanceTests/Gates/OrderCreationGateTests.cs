using ArkWallet.Core.TradingContext.Application.Contracts.TradeOrderServices;
using ArkWallet.Core.TradingContext.Application.Services.CharacterTokenServices;
using ArkWallet.Core.TradingContext.Application.Services.TradeOrderServices;
using ArkWallet.Core.TradingContext.Domain.Engines;
using ArkWallet.Infrastructure;
using ArkWallet.Infrastructure.Data;
using ArkWallet.PerformanceTests.Helpers;
using ArkWallet.PerformanceTests.Measurement;
using Microsoft.Extensions.Logging.Abstractions;

namespace ArkWallet.PerformanceTests.Gates;

[Collection("Perf")]
public class OrderCreationGateTests
{
    private const string Symbol = "TKN000";

    [Fact]
    public async Task CreateBuyOrder_StaysWithinQueryBudget()
    {
        await PerfWarmup.WithDbAsync(async warmupDb =>
        {
            var warmupTrader = await GatesSeed.SeedTraderAsync(warmupDb, 102, 100_000_000m);
            await GatesSeed.SeedTokenCatalogAsync(warmupDb, 1);
            await GatesSeed.SeedTraderPortfolioAsync(warmupDb, warmupTrader.Id, Symbol, 1_000_000);

            var candleUpdateService = new TokenPriceCandleUpdateService(
                warmupDb, TimeProvider.System, NullLogger<TokenPriceCandleUpdateService>.Instance);

            var warmupService = new OrderCreationService(
                warmupDb,
                new TradingEngine(),
                new MediatREventPublisher(TestMediatorFactory.Create(warmupDb, candleUpdateService)),
                new FakeTaskDispatcher(),
                NullLogger<OrderCreationService>.Instance);

            await warmupService.CreateOrderAsync(new CreateOrderCommand(warmupTrader.Id, "купить", Symbol, 10, 1000m));
        });

        var counter = new QueryCounter();
        using var db = PerfDb.CreateDbContext(counter);
        await db.Database.EnsureCreatedAsync();

        var trader = await GatesSeed.SeedTraderAsync(db, 101, 100_000_000m);
        await GatesSeed.SeedTokenCatalogAsync(db, 1);
        await GatesSeed.SeedTraderPortfolioAsync(db, trader.Id, Symbol, 1_000_000);

        var service = BuildService(db);
        counter.Reset();

        using var scope = new PerfScope(counter);
        using (scope.Step("CreateBuyOrder"))
        {
            var result = await service.CreateOrderAsync(new CreateOrderCommand(trader.Id, "купить", Symbol, 10, 1000m));
            Assert.True(result.IsSuccess, result.Message);
        }

        GateAssert.QueryBudget("order-create-buy", GateBudgets.OrderCreateBuy, counter, scope);
    }

    [Fact]
    public async Task CreateSellOrder_StaysWithinQueryBudget()
    {
        await PerfWarmup.WithDbAsync(async warmupDb =>
        {
            var warmupTrader = await GatesSeed.SeedTraderAsync(warmupDb, 102, 100_000_000m);
            await GatesSeed.SeedTokenCatalogAsync(warmupDb, 1);
            await GatesSeed.SeedTraderPortfolioAsync(warmupDb, warmupTrader.Id, Symbol, 1_000_000);

            var candleUpdateService = new TokenPriceCandleUpdateService(
                warmupDb, TimeProvider.System, NullLogger<TokenPriceCandleUpdateService>.Instance);

            var warmupService = new OrderCreationService(
                warmupDb,
                new TradingEngine(),
                new MediatREventPublisher(TestMediatorFactory.Create(warmupDb, candleUpdateService)),
                new FakeTaskDispatcher(),
                NullLogger<OrderCreationService>.Instance);

            await warmupService.CreateOrderAsync(new CreateOrderCommand(warmupTrader.Id, "купить", Symbol, 10, 1000m));
        });

        var counter = new QueryCounter();
        using var db = PerfDb.CreateDbContext(counter);
        await db.Database.EnsureCreatedAsync();

        var trader = await GatesSeed.SeedTraderAsync(db, 101, 100_000_000m);
        await GatesSeed.SeedTokenCatalogAsync(db, 1);
        await GatesSeed.SeedTraderPortfolioAsync(db, trader.Id, Symbol, 1_000_000);

        var service = BuildService(db);
        counter.Reset();

        using var scope = new PerfScope(counter);
        using (scope.Step("CreateSellOrder"))
        {
            var result = await service.CreateOrderAsync(new CreateOrderCommand(trader.Id, "продать", Symbol, 10, 1000m));
            Assert.True(result.IsSuccess, result.Message);
        }

        GateAssert.QueryBudget("order-create-sell", GateBudgets.OrderCreateSell, counter, scope);
    }

    private static OrderCreationService BuildService(ArkWalletDbContext db)
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
}
