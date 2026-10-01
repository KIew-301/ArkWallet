using ArkWallet.Core.TradingContext.Application.Services.TraderServices;
using ArkWallet.Infrastructure.Data;
using ArkWallet.PerformanceTests.Helpers;
using ArkWallet.PerformanceTests.Measurement;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace ArkWallet.PerformanceTests.Gates;

[Collection("Perf")]
public class BalanceChangesGateTests
{
    private const string TraderSymbol = "TKN000";

    private static async Task<ArkWalletDbContext> CreateSeededDbAsync(QueryCounter counter)
    {
        var db = PerfDb.CreateDbContext(counter);
        await db.Database.EnsureCreatedAsync();

        var trader = await GatesSeed.SeedTraderAsync(db, 101, 3500m);
        await GatesSeed.SaveBalanceSnapshotAsync(db, trader.Id, 1000m, DateTime.UtcNow.AddDays(-7));
        await GatesSeed.SaveBalanceSnapshotAsync(db, trader.Id, 1500m, DateTime.UtcNow.AddDays(-1));
        await GatesSeed.SeedTokenCatalogAsync(db, 1);
        await GatesSeed.SeedTraderPortfolioAsync(db, trader.Id, TraderSymbol, 10);

        return db;
    }

    private static BalanceChangesCalculationService BuildService(ArkWalletDbContext db)
    {
        var snapshotService = new BalanceSnapshotService(db, NullLogger<BalanceSnapshotService>.Instance);
        return new BalanceChangesCalculationService(db, snapshotService, NullLogger<BalanceChangesCalculationService>.Instance);
    }

    [Fact]
    public async Task TakeMainBalanceChanges_StaysWithinQueryBudget()
    {
        var counter = new QueryCounter();
        using var db = await CreateSeededDbAsync(counter);

        var trader = await db.Traders.FirstAsync();
        var service = BuildService(db);

        await PerfWarmup.RunAsync(async () => await service.TakeMainBalanceChanges(trader.Id, 1));
        counter.Reset();

        using var scope = new PerfScope(counter);
        using (scope.Step("TakeMainBalanceChanges"))
        {
            var result = await service.TakeMainBalanceChanges(trader.Id, 1);
            Assert.True(result.IsSuccess, result.Message);
        }

        GateAssert.QueryBudget("balance-main-changes", GateBudgets.BalanceMainChanges, counter, scope);
    }

    [Fact]
    public async Task TakeTotalBalanceChanges_StaysWithinQueryBudget()
    {
        var counter = new QueryCounter();
        using var db = await CreateSeededDbAsync(counter);

        var trader = await db.Traders.FirstAsync();
        var service = BuildService(db);

        await PerfWarmup.RunAsync(async () => await service.TakeTotalBalanceChanges(trader.Id, 1));
        counter.Reset();

        using var scope = new PerfScope(counter);
        using (scope.Step("TakeTotalBalanceChanges"))
        {
            var result = await service.TakeTotalBalanceChanges(trader.Id, 1);
            Assert.True(result.IsSuccess, result.Message);
        }

        GateAssert.QueryBudget("balance-total-changes", GateBudgets.BalanceTotalChanges, counter, scope);
    }
}
