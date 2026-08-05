using ArkWallet.Application.Services.Leaders;
using ArkWallet.Application.Services.TraderServices;
using ArkWallet.Infrastructure.Data;
using ArkWallet.PerformanceTests.Helpers;
using ArkWallet.PerformanceTests.Measurement;
using Microsoft.Extensions.Logging.Abstractions;

namespace ArkWallet.PerformanceTests.Gates;

public class GetTopsGateTests
{
    private const int TraderCount = 50;
    private const int QueryBudget = 110;

    [Fact]
    public async Task GetTopAsync_With50Traders_StaysWithinQueryBudget()
    {
        var counter = new QueryCounter();
        using var db = PerfDb.CreateDbContext(counter);
        await db.Database.EnsureCreatedAsync();
        await GatesSeed.SeedLeaderboardAsync(db, TraderCount);

        var snapshotService = new BalanceSnapshotService(db, NullLogger<BalanceSnapshotService>.Instance);
        var service = new LeadersTopByBalanceQueryService(db, snapshotService, NullLogger<LeadersTopByBalanceQueryService>.Instance);

        counter.Reset();

        using var scope = new PerfScope(counter);
        using (scope.Step("GetTopAsync(10)"))
        {
            var result = await service.GetTopAsync(10);
            Assert.True(result.IsSuccess, result.Message);
        }

        GateAssert.QueryBudget("leaders-top-50t", QueryBudget, counter, scope);
    }
}
