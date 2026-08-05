using ArkWallet.Application.Contracts.TradeOrderServices;
using ArkWallet.Application.Services.CharacterTokenServices;
using ArkWallet.Application.Services.TradeOrderServices;
using ArkWallet.Domain.Engines;
using ArkWallet.Infrastructure.Data;
using ArkWallet.PerformanceTests.Helpers;
using ArkWallet.PerformanceTests.Measurement;
using Microsoft.Extensions.Logging.Abstractions;

namespace ArkWallet.PerformanceTests.Gates;

public class OrderCreationGateTests
{
    private const int BuyBudget = 10;
    private const int SellBudget = 11;
    private const string Symbol = "TKN000";

    private static async Task<ArkWalletDbContext> CreateSeededDbAsync(QueryCounter counter)
    {
        var db = PerfDb.CreateDbContext(counter);
        await db.Database.EnsureCreatedAsync();

        await GatesSeed.SeedTraderAsync(db, 101, 100_000_000m);
        await GatesSeed.SeedTokenCatalogAsync(db, 1);
        await GatesSeed.SeedTraderPortfolioAsync(db, 101, Symbol, 1_000_000);

        return db;
    }

    private static OrderCreationService BuildService(ArkWalletDbContext db)
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

    [Fact]
    public async Task CreateBuyOrder_StaysWithinQueryBudget()
    {
        var counter = new QueryCounter();
        using var db = await CreateSeededDbAsync(counter);

        var service = BuildService(db);
        counter.Reset();

        using var scope = new PerfScope(counter);
        using (scope.Step("CreateBuyOrder"))
        {
            var result = await service.CreateOrderAsync(new CreateOrderCommand(101, "купить", Symbol, 10, 1000m));
            Assert.True(result.IsSuccess, result.Message);
        }

        GateAssert.QueryBudget("order-create-buy", BuyBudget, counter, scope);
    }

    [Fact]
    public async Task CreateSellOrder_StaysWithinQueryBudget()
    {
        var counter = new QueryCounter();
        using var db = await CreateSeededDbAsync(counter);

        var service = BuildService(db);
        counter.Reset();

        using var scope = new PerfScope(counter);
        using (scope.Step("CreateSellOrder"))
        {
            var result = await service.CreateOrderAsync(new CreateOrderCommand(101, "продать", Symbol, 10, 1000m));
            Assert.True(result.IsSuccess, result.Message);
        }

        GateAssert.QueryBudget("order-create-sell", SellBudget, counter, scope);
    }
}
